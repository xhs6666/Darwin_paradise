"""构建 Windows 便携软件：静态链接原生入口 + Python/Tk 标准运行时。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile


def bundle(output, test_gui=False):
    if os.name != 'nt':
        raise RuntimeError('Windows packaging requires Windows')
    source = Path(__file__).resolve().parent
    runtime = Path(sys.base_prefix)
    output = Path(output).resolve()
    output.mkdir(parents=True, exist_ok=False)
    app = output / 'app'
    app.mkdir()
    for name in ('main.py', 'build.py', 'package.py', 'deploy.py', 'gui.py', 'paths.py', 'diagnostics.py', 'README.md'):
        shutil.copy2(source / name, app / name)
    target_runtime = output / 'runtime'
    target_runtime.mkdir()
    for pattern in ('python.exe', 'python3*.dll', 'vcruntime*.dll', 'LICENSE*'):
        for path in runtime.glob(pattern):
            shutil.copy2(path, target_runtime / path.name)
    shutil.copytree(runtime / 'DLLs', target_runtime / 'DLLs',
                    ignore=shutil.ignore_patterns('*.pdb', '__pycache__'))
    shutil.copytree(runtime / 'tcl', target_runtime / 'tcl')
    # 🔧 修复原因：发行版不带用户 site-packages，也不能从本机用户目录注入模块。
    version = f'python{sys.version_info.major}{sys.version_info.minor}'
    with zipfile.ZipFile(target_runtime / (version + '.zip'), 'x', zipfile.ZIP_DEFLATED) as archive:
        for path in (runtime / 'Lib').rglob('*'):
            relative = path.relative_to(runtime / 'Lib')
            if any(part in ('site-packages', '__pycache__', 'test', 'tests', 'idlelib', 'ensurepip', 'turtledemo') for part in relative.parts):
                continue
            if path.is_file() and path.suffix not in ('.pyc', '.pyo'):
                archive.write(path, relative.as_posix())
    (target_runtime / (version + '._pth')).write_text(f'{version}.zip\nDLLs\n../app\n', encoding='utf-8')
    candidates = list(Path(os.environ['ProgramFiles']).glob('Microsoft Visual Studio/*/*/VC/Tools/MSVC/*'))
    vc = next((p for p in sorted(candidates, reverse=True) if (p / 'bin/Hostx64/x64/cl.exe').is_file()), None)
    if vc is None:
        raise RuntimeError('Install Visual C++ x64 build tools to build native launchers')
    sdk = Path(os.environ['ProgramFiles(x86)']) / 'Windows Kits/10'
    sdk_version = sorted(p.name for p in (sdk / 'Include').iterdir() if (p / 'um/Windows.h').is_file())[-1]
    environment = dict(os.environ)
    environment['INCLUDE'] = ';'.join(map(str, [vc / 'include', *[sdk / 'Include' / sdk_version / n for n in ('ucrt', 'shared', 'um')]]))
    environment['LIB'] = ';'.join(map(str, [vc / 'lib/x64', *[sdk / 'Lib' / sdk_version / n / 'x64' for n in ('ucrt', 'um')]]))
    for name, subsystem, defines in [('DarwinCoreBuilder.exe', 'WINDOWS', []), ('DarwinCoreBuilderCLI.exe', 'CONSOLE', ['/DCONSOLE'])]:
        subprocess.run([str(vc / 'bin/Hostx64/x64/cl.exe'), '/nologo', '/EHsc', '/MT', '/O2', '/utf-8', '/DUNICODE', '/D_UNICODE',
                        '/Fo' + str(output / (name + '.obj')), '/Fe' + str(output / name), *defines,
                        str(source / 'Launcher.cpp'), '/link', '/SUBSYSTEM:' + subsystem, 'user32.lib', 'shell32.lib', 'comdlg32.lib', 'ole32.lib', 'gdi32.lib'],
                       env=environment, check=True)
        (output / (name + '.obj')).unlink()
    shutil.copy2(source / 'README.md', output / 'README.md')
    subprocess.run([str(output / 'DarwinCoreBuilderCLI.exe'), '--help'], check=True)
    # GUI 初始化依赖交互式桌面权限；受限构建环境可能在 USER32 初始化前被阻止。
    gui_exit = None
    if test_gui:
        gui_exit = subprocess.run([str(output / 'DarwinCoreBuilder.exe'), '--smoke-gui'], timeout=20).returncode
        print(f'Native GUI smoke exit: {gui_exit}')
    subprocess.run([str(target_runtime / 'python.exe'), '-c',
                    "import socket,hashlib,zipfile,msvcrt;print('Portable core runtime PASS')"], check=True)
    # 完整分发内容校验与 ZIP，不包含构建日志、开发代码或本机 Python 安装。
    files = {}
    for path in sorted(output.rglob('*')):
        if path.is_file():
            with path.open('rb') as stream:
                files[path.relative_to(output).as_posix()] = hashlib.file_digest(stream, 'sha256').hexdigest()
    (output / 'distribution.json').write_text(json.dumps({'product': 'DarwinCoreBuilder', 'python': sys.version, 'guiSmokeExit': gui_exit, 'files': files}, indent=2), encoding='utf-8')
    archive = shutil.make_archive(str(output), 'zip', output)
    print(f'Windows portable software: {output / "DarwinCoreBuilder.exe"}')
    print(f'Distribution: {archive}')
    return output


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--test-gui', action='store_true', help='Run native GUI smoke test in an interactive Windows session')
    args = parser.parse_args()
    bundle(args.output, args.test_gui)
