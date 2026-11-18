"""Exercise the shipped PowerShell extractor; fixtures never execute a game or console payload."""
from pathlib import Path
import json
import os
import subprocess
import tempfile
import unittest
import zipfile
from package import create_package


@unittest.skipUnless(os.name == 'nt', 'Windows PowerShell launcher')
class Bootstrap(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.base = Path(self.temp.name)
        self.root = self.base / 'server with spaces'
        self.root.mkdir()
        self.source = self.base / 'source'
        self.source.mkdir()
        self.payload = {'DarwinServer.exe': b'fixture, never executed', 'UnityPlayer.dll': b'fixture runtime',
                        'Console.ps1': b'throw "PrepareOnly must not execute this"', 'Stop.ps1': b'# fixture',
                        'server.example.json': b'{"port":47777}', 'DarwinServer_Data/content.bin': b'game content'}
        for name, data in self.payload.items():
            target = self.source / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        (self.source / 'release.json').write_text(json.dumps(dict(release='0.1.0', protocol=2, contentEpoch=1, minimumSave=1, maximumSave=1)))
        self.core = self.root / 'DarwinCore.zip'
        create_package(self.source, self.core, 'windows-headless')
        self.script = Path(__file__).resolve().parents[2] / 'Server/Bootstrap.ps1'

    def tearDown(self):
        self.temp.cleanup()

    def prepare(self, succeeds=True):
        result = subprocess.run(['powershell.exe', '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass',
                                 '-File', str(self.script), '-Root', str(self.root), '-PrepareOnly'],
                                capture_output=True, encoding='utf-8', errors='replace', timeout=30)
        if succeeds:
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            reference = json.loads((self.root / '.runtime/current.json').read_text(encoding='utf-8-sig'))
            return self.root / '.runtime' / reference['directory']
        self.assertNotEqual(result.returncode, 0)
        return result

    def rewrite(self, change=None, extra=None):
        with zipfile.ZipFile(self.core) as archive:
            data = {entry.filename: archive.read(entry) for entry in archive.infolist()}
        if change:
            change(data)
        if extra:
            data.update(extra)
        with zipfile.ZipFile(self.core, 'w', zipfile.ZIP_DEFLATED) as archive:
            for name, value in data.items():
                archive.writestr(name, value)

    def test_prepare_and_reuse(self):
        runtime = self.prepare()
        self.assertEqual((runtime / 'DarwinServer_Data/content.bin').read_bytes(), b'game content')
        self.assertEqual(runtime, self.prepare())
        self.assertEqual(json.loads((self.root / 'data/server.json').read_text())['port'], 47777)

    def test_existing_config_is_not_overwritten(self):
        (self.root / 'data').mkdir()
        config = self.root / 'data/server.json'
        config.write_bytes(b'{"port":49999,"custom":"keep"}')
        self.prepare()
        self.assertEqual(config.read_bytes(), b'{"port":49999,"custom":"keep"}')

    def test_corrupt_payload_never_becomes_runtime(self):
        self.rewrite(change=lambda data: data.update({'DarwinServer.exe': b'corrupt'}))
        self.prepare(False)
        self.assertFalse((self.root / '.runtime/current.json').exists())

    def test_traversal_is_rejected_before_extraction(self):
        self.rewrite(extra={'../escaped.txt': b'unsafe'})
        self.prepare(False)
        self.assertFalse((self.root / 'escaped.txt').exists())
        self.assertFalse((self.base / 'escaped.txt').exists())

    def test_case_collision_is_rejected(self):
        self.rewrite(extra={'darwinserver.exe': b'collision'})
        self.prepare(False)

    def test_wrong_platform_is_rejected(self):
        def change(data):
            manifest = json.loads(data['manifest.json'])
            manifest['target'] = 'linux-server'
            data['manifest.json'] = json.dumps(manifest).encode()
        self.rewrite(change=change)
        self.prepare(False)

    def test_extra_file_is_rejected(self):
        self.rewrite(extra={'unexpected.dll': b'extra'})
        self.prepare(False)

    def test_damaged_cache_is_rebuilt_without_deleting_old(self):
        previous = self.prepare()
        (previous / 'DarwinServer.exe').write_bytes(b'cache damaged')
        repaired = self.prepare()
        self.assertNotEqual(previous, repaired)
        self.assertEqual((repaired / 'DarwinServer.exe').read_bytes(), self.payload['DarwinServer.exe'])
        self.assertEqual((previous / 'DarwinServer.exe').read_bytes(), b'cache damaged')

    def test_orphan_partial_extraction_does_not_block(self):
        partial = self.root / '.runtime/.extract-interrupted'
        partial.mkdir(parents=True)
        (partial / 'partial.bin').write_bytes(b'keep for diagnosis')
        self.prepare()
        self.assertTrue(partial.exists())

    def test_parallel_launcher_is_rejected(self):
        cache = self.root / '.runtime'
        cache.mkdir()
        with (cache / 'launcher.lock').open('wb'):
            self.prepare(False)


if __name__ == '__main__':
    unittest.main()
