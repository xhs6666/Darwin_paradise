"""★ 新增：检查主题覆盖、本地附件与章节链接；与流程算法校验分开，文档更新可快速自查。"""
from pathlib import Path
from urllib.parse import unquote
import json
import re
from consolidate_docs import headings

ROOT = Path(__file__).resolve().parents[1]
layout = json.loads((ROOT / 'Tools/docs_layout.json').read_text('utf-8'))
files = list((ROOT / 'Docs').rglob('*.md')) + list(ROOT.glob('*.md')) + list((ROOT / 'Tools').glob('*.md')) + list((ROOT / 'Tools/FlowDocs').glob('*.md'))
anchors = {}
errors = []
links = 0


def without_fences(text):
    lines = []
    fence = None
    for line in text.splitlines():
        mark = re.match(r'^\s*(`{3,}|~{3,})', line)
        if mark:
            token = mark[1]
            if fence is None:
                fence = token
            elif token[0] == fence[0] and len(token) >= len(fence):
                fence = None
            continue
        if fence is None:
            lines.append(line)
    return '\n'.join(lines)


for file in files:
    text = file.read_text('utf-8-sig')
    explicit = re.findall(r'<a\s+(?:id|name)=[\"\']([^\"\']+)', without_fences(text))
    if len(explicit) != len(set(explicit)):
        errors.append(f'{file.relative_to(ROOT)}: 重复显式锚点')
    anchors[file.resolve()] = set(explicit) | {h[3] for h in headings(text)}

for file in files:
    text = without_fences(file.read_text('utf-8-sig'))
    targets = re.findall(r'\]\(([^\s()]+)\)', text) + re.findall(r'\bhref=[\"\']([^\"\']+)', text)
    for target in targets:
        if re.match(r'^[a-zA-Z][\w+.-]*:', target) or target.startswith('//'):
            continue
        path, sep, fragment = unquote(target).partition('#')
        destination = (file.parent / path).resolve() if path else file.resolve()
        links += 1
        if not destination.exists():
            errors.append(f'{file.relative_to(ROOT)}: 文件不存在 {target}')
        elif fragment and destination.suffix == '.md' and fragment not in anchors.get(destination, set()):
            errors.append(f'{file.relative_to(ROOT)}: 章节不存在 {target}')

for source, chapter in layout['chapters'].items():
    destination = (ROOT / chapter['file']).resolve()
    if (ROOT / source).exists() or chapter['anchor'] not in anchors.get(destination, set()):
        errors.append('旧页清理或主题章节缺失：' + source)
manifest = json.loads((ROOT / 'Docs/运行流程图/manifest.json').read_text('utf-8'))
for script in manifest['scripts']:
    page = (ROOT / f"Docs/运行流程图/模块/{script['module']}.md").resolve()
    if 's' + script['id'] not in anchors.get(page, set()):
        errors.append('模块缺少脚本：' + script['path'])

report = {'result': 'FAIL' if errors else 'PASS', 'markdownFiles': len(files), 'localLinks': links,
          'mergedSources': len(layout['chapters']), 'topicBooks': len(layout['books']),
          'moduleBooks': len({s['module'] for s in manifest['scripts']}), 'errors': errors}
output = ROOT / 'Artifacts/Docs'
output.mkdir(parents=True, exist_ok=True)
(output / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=True))
raise SystemExit(bool(errors))
