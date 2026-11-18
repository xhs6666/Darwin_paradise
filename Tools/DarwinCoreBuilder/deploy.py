"""独立 Windows 更新器：整包预检、优雅停服、离线备份、健康检查与回滚。"""
from contextlib import contextmanager
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid
from package import digest, inspect_package, read_json, write_json, safe_name


@contextmanager
def operation(root):
    if os.name != 'nt':
        raise RuntimeError('Deployment currently supports Windows only; Linux packages require manual deployment')
    root = Path(root).resolve()
    root.mkdir(parents=True, exist_ok=True)
    for name in ('data', 'releases', 'backups'):
        path = root / name
        if path.is_symlink() or path.is_junction():
            raise ValueError(f'Managed directory must not redirect outside deployment root: {path}')
    # OS 持锁，更新器崩溃也会释放；残留事务则由 journal 阻止下一次写入。
    import msvcrt
    with (root / 'updater.lock').open('a+b') as lock:
        lock.seek(0)
        lock.write(b'0')
        lock.flush()
        lock.seek(0)
        msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
        try:
            if (root / 'transaction.json').exists():
                raise RuntimeError(f'Interrupted update: inspect {root / "transaction.json"} and backups before recovery')
            yield root
        finally:
            lock.seek(0)
            msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1)


def unlocked(data):
    # .NET FileShare.None 与 Windows 文件共享检查互斥；不能用 PID/端口代替数据租约。
    try:
        with (data / 'server.lock').open('a+b'):
            return True
    except OSError:
        return False


def wait_for(predicate, timeout, message):
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        if predicate():
            return
        time.sleep(.2)
    raise TimeoutError(message)


def status(data):
    try:
        return read_json(data / 'server-status.json')
    except (OSError, ValueError):
        return {}


def stop(data, timeout):
    if unlocked(data):
        if (data / 'stop.request').exists():
            raise RuntimeError('Stale stop.request; inspect previous save failure')
        if status(data).get('state') in ('ready', 'starting'):
            raise RuntimeError('Previous server exited without clean stop receipt; verify saved data before updating')
        return
    before = status(data)
    if before.get('state') != 'ready' or not before.get('runId'):
        raise RuntimeError('Server lacks update health handshake; stop and verify it manually first')
    (data / 'stop.request').write_text('save-and-stop', encoding='utf-8')
    wait_for(lambda: unlocked(data), timeout, 'Save/stop timed out; no files were replaced')
    after = status(data)
    if after.get('state') != 'stopped' or after.get('runId') != before['runId'] or (data / 'stop.request').exists():
        raise RuntimeError('Missing clean save receipt; refusing update')


def verify_installed(release):
    if release.is_symlink() or release.is_junction():
        raise ValueError('Release directory must not be a link')
    manifest = read_json(release / 'manifest.json')
    for name, spec in manifest['files'].items():
        safe_name(name)
        path = release / name
        if path.is_symlink() or path.stat().st_size != spec['size'] or digest(path) != spec['sha256']:
            raise ValueError(f'Installed release damaged: {name}')
    return manifest


def launch(root, current, timeout):
    data = root / 'data'
    if not unlocked(data) or (data / 'stop.request').exists():
        raise RuntimeError('Data directory is in use or stop request is unresolved')
    directory = safe_name(current['directory'])
    if '/' in directory:
        raise ValueError('Invalid current release directory')
    release = root / 'releases' / directory
    manifest = verify_installed(release)
    config = data / 'server.json'
    if not config.exists():
        shutil.copy2(release / 'server.example.json', config)
    run_id = uuid.uuid4().hex
    command = [str(release / manifest['entry']), '-batchmode', '-nographics', '-server-data', str(data),
               '-server-config', str(config), '-server-run-id', run_id, '-logFile', str(data / 'unity-server.log')]
    if (data / 'update.guard').exists():
        command += ['-server-update-id', (data / 'update.guard').read_text(encoding='utf-8')]
    process = subprocess.Popen(command, cwd=release, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    try:
        def ready():
            if process.poll() is not None:
                raise RuntimeError(f'Server exited {process.returncode}; see {data / "unity-server.log"}')
            health = status(data)
            return (health.get('state') == 'ready' and health.get('runId') == run_id
                    and health.get('pid') == process.pid and health.get('release') == manifest['release']['release']
                    and not unlocked(data))
        wait_for(ready, timeout, 'Server health check timed out')
        # READY 后仍须存活，捕获世界完成回调后立即退出的问题。
        end = time.monotonic() + 3
        while time.monotonic() < end:
            if not ready():
                raise RuntimeError('Server lost readiness')
            time.sleep(.2)
        return process
    except BaseException:
        # 只有本次创建的新进程可以强制终止；恢复数据之前必须确认进程释放租约。
        if process.poll() is None:
            process.kill()
        process.wait(timeout=30)
        wait_for(lambda: unlocked(data), 30, 'Failed process still owns data lease')
        raise


def compatible(old, new):
    a, b = old['release'], new['release']
    if a['contentEpoch'] != b['contentEpoch'] or b['minimumSave'] > a['minimumSave'] or b['maximumSave'] < a['maximumSave']:
        raise ValueError('Update needs an explicit tested save migration; content epoch/save range incompatible')


def install(root, package, timeout=300, log=print):
    with operation(root) as root:
        manifest = inspect_package(package)
        if not manifest['target'].startswith('windows'):
            raise ValueError('Cannot run Linux server package on Windows')
        releases = root / 'releases'
        releases.mkdir(exist_ok=True)
        data = root / 'data'
        data.mkdir(exist_ok=True)
        current_path = root / 'current.json'
        old = read_json(current_path) if current_path.exists() else None
        if old:
            compatible(verify_installed(releases / old['directory']), manifest)
        elif any(p.name not in ('server.lock',) for p in data.iterdir()):
            raise ValueError('Initial installation requires empty managed data; migrate existing data offline after installation')
        directory = manifest['release']['release'] + '-' + uuid.uuid4().hex
        new = {'directory': directory}
        inspect_package(package, releases / directory)
        journal_path = root / 'transaction.json'
        journal = {'old': old, 'new': new, 'phase': 'stopping'}
        write_json(journal_path, journal)
        guard = data / 'update.guard'
        guard.write_text(directory, encoding='utf-8')
        # 停服失败不启动第二个实例、不回滚仍在线的数据；保留事务供排查。
        log('Requesting clean save/stop')
        stop(data, timeout)
        backup = root / 'backups' / directory
        backup.parent.mkdir(exist_ok=True)
        try:
            if any(p.is_symlink() or p.is_junction() for p in data.rglob('*')):
                raise ValueError('Data backup cannot follow links outside the managed directory')
            shutil.copytree(data, backup, ignore=shutil.ignore_patterns('server.lock', 'update.guard'))
        except BaseException:
            journal['phase'] = 'backup-failed'
            write_json(journal_path, journal)
            raise
        journal.update(phase='starting', backup=str(backup))
        write_json(journal_path, journal)
        log(f'Data backup: {backup}; starting candidate')
        process = None
        try:
            process = launch(root, new, timeout)
            write_json(current_path, new)
            guard.unlink()
            journal_path.unlink()
            log(f'Healthy server running, PID {process.pid}, release {manifest["release"]["release"]}')
        except BaseException:
            if process is not None and process.poll() is None:
                process.kill()
                process.wait(timeout=30)
            if not unlocked(data):
                raise RuntimeError('Cannot restore backup while a server owns the data; transaction retained')
            failed = root / ('failed-data-' + uuid.uuid4().hex)
            data.rename(failed)
            shutil.copytree(backup, data)
            guard.write_text(directory, encoding='utf-8')
            if old:
                write_json(current_path, old)
                launch(root, old, timeout)
                log('Candidate failed; old release and pre-update data restored')
            elif current_path.exists():
                current_path.unlink()
            guard.unlink()
            journal_path.unlink()
            raise


def start(root, timeout=300):
    with operation(root) as root:
        return launch(root, read_json(root / 'current.json'), timeout)


def stop_installed(root, timeout=300):
    with operation(root) as root:
        stop(root / 'data', timeout)
