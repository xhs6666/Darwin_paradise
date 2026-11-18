"""Static architecture, release/config and Unity GUID checks for the server split."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
definitions={json.loads(p.read_text(encoding='utf-8-sig'))['name']:json.loads(p.read_text(encoding='utf-8-sig')) for p in (root/'Assets/DarwinPark').rglob('*.asmdef')}
active=set();done=set()
def visit(name):
    assert name not in active, f'Assembly dependency cycle: {name}'
    if name in done:return
    active.add(name)
    for dependency in definitions[name].get('references',[]):
        if dependency in definitions:visit(dependency)
    active.remove(name);done.add(name)
for name in definitions:visit(name)
assert definitions['DarwinPark.ServerCore'].get('noEngineReferences') is True
assert 'DarwinPark.Server.Unity' not in definitions['DarwinPark.UI']['references']
assert 'DarwinPark.Gameplay' in definitions['DarwinPark.UI']['references']
assert 'DarwinPark.Gameplay' in definitions['DarwinPark.Server.Unity']['references']
assert 'DarwinPark.UI' not in definitions['DarwinPark.Gameplay']['references']
assert 'DarwinPark.UI' not in definitions['DarwinPark.Server.Unity']['references']
for p in (root/'Assets/DarwinPark/ServerCore').glob('*.cs'):
    assert not re.search(r'\b(?:UnityEngine|UnityEditor)\s*[.;]',p.read_text(encoding='utf-8')), p
for name in ['LanServerRuntime.cs','LanWorldSnapshot.cs']:
    p=root/'Assets/DarwinPark/ServerUnity'/name;text=p.read_text(encoding='utf-8')
    code=re.sub(r'//[^\n]*|/\*[\s\S]*?\*/','',text)
    assert not re.search(r'Camera\.|Input\.|Screen\.|WaitForEndOfFrame',code), name
for p in (root/'Assets/DarwinPark/Gameplay').rglob('*.cs'):
    code=re.sub(r'//[^\n]*|/\*[\s\S]*?\*/','',p.read_text(encoding='utf-8'))
    assert not re.search(r'\b(?:Camera|Input|Screen|Cursor|GUI|GUILayout)\s*\.|\b(?:UIRoot|EntityViewController|GeneInspectorUI|EntityPlacementUtility|WaitForEndOfFrame)\b',code),p
session=(root/'Assets/DarwinPark/Gameplay/Networking/LanSession.cs').read_text(encoding='utf-8')
assert 'private void ApplyFrame(' not in session and 'private void ProcessCommand(' not in session
manifest=json.loads((root/'Assets/Resources/Entities/server_release.json').read_text())
assert manifest['protocol']==2 and manifest['contentEpoch']>=1 and manifest['minimumSave']==1 and manifest['maximumSave']==1
config=json.loads((root/'Server/server.example.json').read_text());assert config['version']==1 and 0<config['port']<=65535 and 0<config['tickRate']<=240 and config['autoSaveSeconds']>0
guids={}
for p in (root/'Assets').rglob('*.meta'):
    match=re.search(r'^guid: ([0-9a-f]{32})$',p.read_text(encoding='utf-8-sig'),re.M)
    assert match, f'Invalid meta GUID: {p}'
    assert match[1] not in guids, f'Duplicate GUID: {p} / {guids.get(match[1])}'
    guids[match[1]]=p
for p in (root/'Assets/DarwinPark').rglob('*.cs'):
    assert p.with_suffix('.cs.meta').is_file(),f'Missing source meta: {p}'
print(f'PASS: {len(definitions)} acyclic assemblies; isolated core/server; release/config; {len(guids)} unique GUIDs')
