"""完整核心包与清单；只负责不可变文件，不接触在线存档。"""
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import zipfile

PRODUCT = 'DarwinParadise.Server'
LIMIT = 16 * 1024**3


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write_json(path, value):
    path = Path(path)
    temp = path.with_suffix(path.suffix + '.tmp')
    temp.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    os.replace(temp, path)


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def safe_name(name):
    # 🔧 修复原因：ZIP 路径在 Windows 上也不能解释为盘符、ADS 或设备文件。
    parts = PurePosixPath(name).parts
    if (not name or '\\' in name or ':' in name or name.startswith('/')
            or any(p in ('', '.', '..') or p.endswith((' ', '.')) for p in parts)
            or '/'.join(parts) != name
            or any(re.fullmatch(r'(?i)(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?', p) for p in parts)):
        raise ValueError(f'Unsafe package path: {name}')
    return name


def validate_release(value):
    for key in ('protocol', 'contentEpoch', 'minimumSave', 'maximumSave'):
        if type(value.get(key)) is not int or value[key] < 1:
            raise ValueError(f'Invalid release {key}')
    if value['minimumSave'] > value['maximumSave'] or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._+-]{0,79}', value.get('release', '')):
        raise ValueError('Invalid release version/save range')
    return value


def create_package(source, output, target):
    source, output = Path(source).resolve(), Path(output).resolve()
    if output.is_relative_to(source):
        raise ValueError('Package output must be outside build directory')
    release = validate_release(read_json(source / 'release.json'))
    entry = 'DarwinServer.exe' if target.startswith('windows') else 'DarwinServer'
    required = [entry, 'DarwinServer_Data', 'UnityPlayer.dll' if target.startswith('windows') else 'UnityPlayer.so', 'server.example.json']
    if any(not (source / name).exists() for name in required):
        raise ValueError(f'Incomplete Unity server build; requires {required}')
    files = {}
    folded = set()
    for path in sorted(source.rglob('*')):
        if path.is_symlink() or path.is_junction():
            raise ValueError(f'Links are not permitted: {path}')
        if not path.is_file():
            continue
        name = safe_name(path.relative_to(source).as_posix())
        if name.lower() in folded or name == 'manifest.json':
            raise ValueError(f'Duplicate/reserved file: {name}')
        if name.split('/')[0].lower() in {'data', 'save', 'logs', 'backups', 'server.json', 'server.lock', 'stop.request'}:
            raise ValueError(f'Operational data must not be packaged: {name}')
        folded.add(name.lower())
        files[name] = {'size': path.stat().st_size, 'sha256': digest(path)}
    manifest = dict(schema=1, product=PRODUCT, target=target, entry=entry, release=release, files=files)
    output.parent.mkdir(parents=True, exist_ok=True)
    # 独占创建，不覆盖已发布包；失败保留可诊断的构建目录。
    with zipfile.ZipFile(output, 'x', zipfile.ZIP_DEFLATED) as archive:
        archive.writestr('manifest.json', json.dumps(manifest, indent=2))
        for name in files:
            archive.write(source / name, name)
    output.with_suffix(output.suffix + '.sha256').write_text(digest(output) + '  ' + output.name + '\n', encoding='utf-8')
    return manifest


def inspect_package(package, extract=None):
    with zipfile.ZipFile(package) as archive:
        entries = archive.infolist()
        if len(entries) > 100000 or sum(e.file_size for e in entries) > LIMIT:
            raise ValueError('Package exceeds extraction limits')
        names = [safe_name(e.filename) for e in entries]
        if len(set(n.lower() for n in names)) != len(names):
            raise ValueError('Duplicate/case-colliding ZIP entries')
        if any(stat.S_ISLNK(e.external_attr >> 16) or e.is_dir() for e in entries):
            raise ValueError('Package must contain regular files only')
        if 'manifest.json' not in names or archive.getinfo('manifest.json').file_size > 16 * 1024**2:
            raise ValueError('Missing/oversized manifest')
        manifest = json.loads(archive.read('manifest.json'))
        if manifest.get('schema') != 1 or manifest.get('product') != PRODUCT or manifest.get('target') not in ('windows-headless', 'windows-server', 'linux-server'):
            raise ValueError('Unsupported package')
        validate_release(manifest['release'])
        files = manifest['files']
        expected_entry = 'DarwinServer.exe' if manifest['target'].startswith('windows') else 'DarwinServer'
        if manifest['entry'] != expected_entry or expected_entry not in files or set(names) != set(files) | {'manifest.json'}:
            raise ValueError('Manifest file list/entry mismatch')
        for name, spec in files.items():
            safe_name(name)
            if archive.getinfo(name).file_size != spec['size']:
                raise ValueError(f'Size mismatch: {name}')
            with archive.open(name) as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != spec['sha256']:
                    raise ValueError(f'SHA256 mismatch: {name}')
        if json.loads(archive.read('release.json')) != manifest['release']:
            raise ValueError('Embedded release mismatch')
        if extract:
            destination = Path(extract).resolve()
            destination.mkdir(parents=True, exist_ok=False)
            for name in names:
                path = destination / name
                path.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(name) as src, path.open('xb') as dst:
                    import shutil
                    shutil.copyfileobj(src, dst)
                if name == manifest['entry']:
                    path.chmod(path.stat().st_mode | stat.S_IXUSR)
        return manifest
