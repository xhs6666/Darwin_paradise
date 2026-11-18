"""真实 ZIP 与 Windows 子进程回归；模拟器只替代 Unity，不替代更新事务。"""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile
from package import create_package, inspect_package, read_json, safe_name, validate_release
import deploy
import diagnostics
import build as builder

RELEASE = dict(protocol=2, contentEpoch=1, minimumSave=1, maximumSave=1, release='0.1.0')


class Diagnostics(unittest.TestCase):
    def test_cache_failure_stops_before_editor_launch(self):
        project = Path(__file__).resolve().parents[2]
        with patch.object(builder, 'check', side_effect=PermissionError('cache denied')), patch.object(builder.subprocess, 'run') as launch:
            with self.assertRaises(PermissionError):
                builder.build('Unity.exe', project, project / 'Artifacts/TestUnused', 'windows-headless', lambda _: None)
            launch.assert_not_called()

    def test_cache_error_skips_even_version_launch(self):
        project = Path(__file__).resolve().parents[2]
        expected = (project / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ', 1)[1]
        result = subprocess.CompletedProcess([], 0, (expected + '\n').encode(), b'')
        with patch.object(diagnostics.subprocess, 'run', return_value=result) as launch, patch.object(diagnostics, 'cache_problem', return_value='cache denied'):
            with self.assertRaisesRegex(PermissionError, 'cache denied'):
                diagnostics.check('Unity.exe', project)
            launch.assert_not_called()

    def test_wrong_editor_version_rejected(self):
        project = Path(__file__).resolve().parents[2]
        result = subprocess.CompletedProcess([], 0, b'1.0\n', b'')
        with patch.object(diagnostics.subprocess, 'run', return_value=result), patch.object(diagnostics, 'cache_problem', return_value=None):
            with self.assertRaisesRegex(RuntimeError, 'version mismatch'):
                diagnostics.check('Unity.exe', project)


class Packages(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.base = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def package(self, version='0.1.0', mode='ready', epoch=1):
        source = self.base / ('source-' + version)
        source.mkdir()
        (source / 'DarwinServer.exe').write_text(mode)
        (source / 'UnityPlayer.dll').write_bytes(b'runtime')
        (source / 'DarwinServer_Data').mkdir()
        (source / 'DarwinServer_Data/resources.assets').write_bytes(b'resources')
        (source / 'release.json').write_text(json.dumps(dict(RELEASE, release=version, contentEpoch=epoch)))
        (source / 'server.example.json').write_text('{"port":47777}')
        output = self.base / (version + '.zip')
        create_package(source, output, 'windows-headless')
        return output

    def test_roundtrip_and_checksum(self):
        package = self.package()
        manifest = inspect_package(package, self.base / 'extract')
        self.assertEqual(manifest['release'], RELEASE)
        self.assertEqual((self.base / 'extract/DarwinServer_Data/resources.assets').read_bytes(), b'resources')
        self.assertTrue(package.with_suffix('.zip.sha256').exists())

    def test_tampered_payload(self):
        package = self.package()
        with zipfile.ZipFile(package) as src, zipfile.ZipFile(self.base / 'bad.zip', 'w') as dst:
            for name in src.namelist():
                dst.writestr(name, b'corrupt' if name == 'UnityPlayer.dll' else src.read(name))
        with self.assertRaisesRegex(ValueError, 'mismatch'):
            inspect_package(self.base / 'bad.zip', self.base / 'extract')
        self.assertFalse((self.base / 'extract').exists())

    def test_unsafe_paths(self):
        for name in ('../x', '/x', 'C:/x', 'a\\b', 'a/../b', 'a//b', 'file:stream', 'CON', 'x.', './x'):
            with self.subTest(name=name), self.assertRaises(ValueError):
                safe_name(name)

    def test_unlisted_file(self):
        package = self.package()
        with zipfile.ZipFile(package, 'a') as archive:
            archive.writestr('extra.dll', 'extra')
        with self.assertRaisesRegex(ValueError, 'mismatch'):
            inspect_package(package)

    def test_duplicate_case(self):
        package = self.package()
        with zipfile.ZipFile(package, 'a') as archive:
            archive.writestr('UNITYPLAYER.DLL', 'extra')
        with self.assertRaisesRegex(ValueError, 'colliding'):
            inspect_package(package)

    def test_version_range(self):
        with self.assertRaises(ValueError):
            validate_release(dict(RELEASE, release='../escape'))


@unittest.skipUnless(os.name == 'nt', 'Windows deployment tests')
class Deployment(Packages):
    def setUp(self):
        super().setUp()
        self.root = self.base / 'installation'
        self.processes = []
        real_popen = subprocess.Popen
        def fake_popen(command, **kwargs):
            process = real_popen([sys.executable, str(Path(__file__).with_name('test_server.py')), *command], **kwargs)
            self.processes.append(process)
            return process
        self.patcher = patch.object(deploy.subprocess, 'Popen', side_effect=fake_popen)
        self.patcher.start()

    def tearDown(self):
        self.patcher.stop()
        for process in self.processes:
            if process.poll() is None:
                process.kill()
            process.wait(timeout=10)
        super().tearDown()

    def test_update_preserves_data_and_config(self):
        deploy.install(self.root, self.package(), 10, lambda _: None)
        (self.root / 'data/custom.txt').write_text('keep')
        deploy.install(self.root, self.package('0.1.1'), 10, lambda _: None)
        self.assertEqual((self.root / 'data/custom.txt').read_text(), 'keep')
        self.assertEqual(deploy.status(self.root / 'data')['release'], '0.1.1')
        self.assertEqual(len(list((self.root / 'backups').iterdir())), 2)
        deploy.stop_installed(self.root, 10)
        self.assertTrue(deploy.unlocked(self.root / 'data'))

    def test_failed_candidate_restores_data_and_old_binary(self):
        deploy.install(self.root, self.package(), 10, lambda _: None)
        before = read_json(self.root / 'current.json')
        with self.assertRaisesRegex(RuntimeError, 'exited'):
            deploy.install(self.root, self.package('0.1.1', 'fail'), 10, lambda _: None)
        self.assertEqual(read_json(self.root / 'current.json'), before)
        self.assertFalse((self.root / 'data/candidate-mutation.txt').exists())
        self.assertEqual(deploy.status(self.root / 'data')['release'], '0.1.0')
        self.assertFalse((self.root / 'transaction.json').exists())
        deploy.stop_installed(self.root, 10)

    def test_incompatible_package_does_not_stop_server(self):
        deploy.install(self.root, self.package(), 10, lambda _: None)
        before = deploy.status(self.root / 'data')['runId']
        with self.assertRaisesRegex(ValueError, 'migration'):
            deploy.install(self.root, self.package('0.2.0', epoch=2), 10, lambda _: None)
        self.assertEqual(deploy.status(self.root / 'data')['runId'], before)
        self.assertFalse((self.root / 'data/stop.request').exists())
        deploy.stop_installed(self.root, 10)

    def test_failed_save_never_replaces_online_data(self):
        deploy.install(self.root, self.package(mode='save-fail'), 10, lambda _: None)
        before = read_json(self.root / 'current.json')
        with self.assertRaises(TimeoutError):
            deploy.install(self.root, self.package('0.1.1'), .5, lambda _: None)
        self.assertEqual(read_json(self.root / 'current.json'), before)
        self.assertFalse(deploy.unlocked(self.root / 'data'))
        self.assertTrue((self.root / 'transaction.json').exists())
        with self.assertRaisesRegex(RuntimeError, 'Interrupted'):
            deploy.start(self.root, 1)


if __name__ == '__main__':
    unittest.main()
