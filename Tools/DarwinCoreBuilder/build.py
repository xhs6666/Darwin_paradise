"""Unity 构建适配层；不修改游戏版本，也不以旧产物冒充本次成功。"""
from pathlib import Path
import os
import subprocess
import uuid
from package import create_package, read_json, validate_release
from diagnostics import check

METHODS = {
    'windows-headless': 'BuildWindowsHeadless',
    'windows-server': 'BuildWindows',
    'linux-server': 'BuildLinux',
}


def build(unity, project, output, target, log=print):
    unity, project, output = Path(unity).resolve(), Path(project).resolve(), Path(output).resolve()
    version = (project / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ', 1)[1]
    log(f'Project Unity: {version}; executable: {unity}')
    log(check(unity, project))
    release = validate_release(read_json(project / 'Assets/Resources/Entities/server_release.json'))
    build_root = project / 'Artifacts/CoreBuilder' / uuid.uuid4().hex
    build_root.mkdir(parents=True)
    # ★ 修复（防Bug回归）：Burst 等子进程需要可写临时目录；每次构建独立目录避免继承不可访问的用户 TEMP。
    temporary = build_root / 'temp'
    temporary.mkdir()
    environment = dict(os.environ, TEMP=str(temporary), TMP=str(temporary), TMPDIR=str(temporary))
    entry = build_root / 'player' / ('DarwinServer.exe' if target.startswith('windows') else 'DarwinServer')
    entry.parent.mkdir()
    logfile = build_root / 'unity-build.log'
    command = [str(unity), '-batchmode', '-nographics', '-quit', '-projectPath', str(project),
               '-executeMethod', 'DarwinPark.EditorTools.DedicatedServerBuild.' + METHODS[target],
               '-server-output', str(entry), '-logFile', str(logfile)]
    log(f'Building {target}; log: {logfile}')
    launch_log = build_root / 'unity-launch.log'
    with launch_log.open('w', encoding='utf-8') as stream:
        result = subprocess.run(command, check=False, stdout=stream, stderr=subprocess.STDOUT, env=environment)
    if result.returncode:
        detail = str(logfile) if logfile.exists() else f'Editor exited before writing a build log; launch output: {launch_log}'
        raise RuntimeError(f'Unity build failed ({result.returncode}). {detail}')
    if read_json(entry.parent / 'release.json') != release:
        raise RuntimeError('Release changed during build; rebuild from a stable workspace')
    output.mkdir(parents=True, exist_ok=True)
    package = output / f'DarwinCore-{release["release"]}-{target}-{build_root.name[:8]}.zip'
    create_package(entry.parent, package, target)
    log(f'Package ready: {package}')
    return package
