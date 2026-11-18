"""DarwinCoreBuilder 命令行入口，参数解析与业务实现分离。"""
import argparse
from pathlib import Path
import sys
from build import build, METHODS
from package import create_package, inspect_package
import deploy
from paths import project_default, output_default


def main():
    parser = argparse.ArgumentParser(description='DarwinCoreBuilder: build, verify and safely update server cores')
    commands = parser.add_subparsers(dest='command', required=True)
    builder = commands.add_parser('build')
    builder.add_argument('--unity', required=True, type=Path)
    builder.add_argument('--project', type=Path, default=project_default() or Path.cwd())
    builder.add_argument('--output', type=Path, default=output_default())
    builder.add_argument('--target', choices=METHODS, default='windows-headless')
    doctor = commands.add_parser('doctor', help='Check Unity version and cache access before building')
    doctor.add_argument('--unity', required=True, type=Path)
    doctor.add_argument('--project', type=Path, default=project_default() or Path.cwd())
    pack = commands.add_parser('pack', help='Package an existing complete Unity server build')
    pack.add_argument('--source', required=True, type=Path)
    pack.add_argument('--output', required=True, type=Path)
    pack.add_argument('--target', choices=METHODS, default='windows-headless')
    check = commands.add_parser('verify')
    check.add_argument('package', type=Path)
    for name in ('update', 'start', 'stop'):
        command = commands.add_parser(name)
        command.add_argument('--root', required=True, type=Path)
        command.add_argument('--timeout', type=float, default=300)
        if name == 'update':
            command.add_argument('--package', required=True, type=Path)
    commands.add_parser('gui')
    args = parser.parse_args()
    if hasattr(args, 'timeout') and not 0 < args.timeout < 86400:
        parser.error('--timeout must be between 0 and 86400 seconds')
    if args.command == 'doctor':
        from diagnostics import check
        print(check(args.unity, args.project))
    elif args.command == 'build':
        build(args.unity, args.project, args.output, args.target)
    elif args.command == 'pack':
        create_package(args.source, args.output, args.target)
        print(args.output)
    elif args.command == 'verify':
        manifest = inspect_package(args.package)
        print(f'PASS: {manifest["release"]["release"]} ({len(manifest["files"])} files)')
    elif args.command == 'update':
        deploy.install(args.root, args.package, args.timeout)
    elif args.command == 'start':
        print('Server ready, PID', deploy.start(args.root, args.timeout).pid)
    elif args.command == 'stop':
        deploy.stop_installed(args.root, args.timeout)
        print('Server stopped cleanly')
    else:
        from gui import show
        show()


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(f'ERROR: {error}', file=sys.stderr)
        sys.exit(1)
