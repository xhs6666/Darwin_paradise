"""★ 新增：按主题一次迁移旧文档，重定位章节、附件和交叉链接；重复执行不重复合并。"""
from pathlib import Path
from urllib.parse import unquote
import hashlib
import json
import os
import re

ROOT = Path(__file__).resolve().parents[1]
LAYOUT = ROOT / 'Tools/docs_layout.json'


def slug(text):
    text = re.sub(r'<[^>]+>', '', text).lower().strip()
    return re.sub(r'[^\w\-\s]', '', text).replace(' ', '-')


def headings(text):
    """★ 修复（防Bug回归）：代码块中的注释不是章节，重复标题按Markdown规则编号。"""
    seen = {}
    fence = None
    for index, line in enumerate(text.splitlines()):
        mark = re.match(r'^\s*(`{3,}|~{3,})', line)
        if mark:
            token = mark[1]
            if fence is None:
                fence = token
            elif token[0] == fence[0] and len(token) >= len(fence):
                fence = None
            continue
        match = re.match(r'^(#{1,6})\s+(.+?)\s*#*$', line)
        if not fence and match:
            base = slug(match[2])
            count = seen.get(base, 0)
            seen[base] = count + 1
            yield index, match[1], match[2], base + (f'-{count}' if count else '')


def relative(source, target):
    return os.path.relpath(target, source.parent).replace('\\', '/')


def main():
    layout = json.loads(LAYOUT.read_text('utf-8-sig'))
    books = layout['books']
    mapping = {}
    contents = {}
    anchors = {}
    for dest, sources in books.items():
        for source in sources:
            marker = 'doc-' + hashlib.sha256(source.encode()).hexdigest()[:12]
            mapping[source] = (dest, marker)
            if (ROOT / source).exists():
                contents[source] = (ROOT / source).read_text('utf-8-sig')
                anchors[source] = {h[3]: marker + '-' + h[3] for h in headings(contents[source])}
                anchors[source].update({a: marker + '-' + a for a in re.findall(r'<a\s+(?:id|name)=[\"\']([^\"\']+)', contents[source])})
    if not contents:
        print('主题手册已合并，无需重复迁移。')
        return
    if len(contents) != len(mapping):
        raise RuntimeError('旧文档缺失，停止迁移以免覆盖已合并内容。')
    manifest = json.loads((ROOT / 'Docs/运行流程图/manifest.json').read_text('utf-8'))
    for script in manifest['scripts']:
        mapping[f"Docs/运行流程图/脚本/{script['id']}.md"] = (f"Docs/运行流程图/模块/{script['module']}.md", 's' + script['id'])

    def rewrite(text, old, new):
        def replace(match):
            target = match[1]
            if re.match(r'^[a-zA-Z][\w+.-]*:', target) or target.startswith('//'):
                return match[0]
            filepart, sep, fragment = unquote(target).partition('#')
            path = (old.parent / filepart).resolve() if filepart else old.resolve()
            try:
                key = path.relative_to(ROOT).as_posix()
            except ValueError:
                return match[0]
            if key in mapping:
                dest, marker = mapping[key]
                newfrag = anchors.get(key, {}).get(fragment, fragment) if fragment else marker
                return '](' + relative(new, ROOT / dest) + '#' + newfrag + ')'
            if not filepart and old == new:
                return match[0]
            return '](' + relative(new, path) + (sep + fragment if sep else '') + ')'
        return re.sub(r'\]\(([^\s()]+)\)', replace, text)

    # ★ 新增：所有新手册先完成写入，再删除清单内已保留全文的旧页；不递归删除目录或证据。
    for dest, sources in books.items():
        target = ROOT / dest
        parts = [f'# {target.stem}', '', '[返回文档目录](../README.md)', '',
                 '按主题汇编原有资料。各章节保留原记录日期与验证范围；历史结论不代表当前版本已重新验证。后续请直接维护相关章节。', '', '## 章节目录', '']
        for source in sources:
            parts.append(f'- [{Path(source).stem}](#{mapping[source][1]})')
        for source in sources:
            text = contents[source]
            marker = mapping[source][1]
            parts += ['', '---', '', f'<a id="{marker}"></a>', '', f'## {Path(source).stem}', '', f'原文档：`{source}`', '']
            lines = text.splitlines()
            hs = {h[0]: h for h in headings(text)}
            for index, line in enumerate(lines):
                if index in hs:
                    _, level, title, anchor = hs[index]
                    lines[index] = f'<a id="{marker}-{anchor}"></a>\n\n' + '#' * min(6, len(level) + 2) + ' ' + title
            text = '\n'.join(lines)
            text = re.sub(r'(<a\s+(?:id|name)=[\"\'])([^\"\']+)', lambda m: m[1] + (m[2] if m[2].startswith(marker + '-') else marker + '-' + m[2]), text)
            parts.append(rewrite(text, ROOT / source, target).strip())
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text('\n'.join(parts).rstrip() + '\n', encoding='utf-8')

    # 现存入口与维护文档的链接也迁移，不保留成百上千个跳转文件。
    for folder in [ROOT / 'Docs', ROOT / 'Tools']:
        for file in folder.rglob('*.md'):
            key = file.relative_to(ROOT).as_posix()
            if key in contents or key in books or '/运行流程图/脚本/' in key:
                continue
            text = file.read_text('utf-8-sig')
            updated = rewrite(text, file, file)
            if updated != text:
                file.write_text(updated, encoding='utf-8')
    for file in ROOT.glob('*.md'):
        text = file.read_text('utf-8-sig')
        updated = rewrite(text, file, file)
        if updated != text:
            file.write_text(updated, encoding='utf-8')
    for source in contents:
        path = (ROOT / source).resolve()
        assert path.is_relative_to((ROOT / 'Docs').resolve())
        path.unlink()
    layout['chapters'] = {source: {'file': dest, 'anchor': marker} for source, (dest, marker) in mapping.items() if source in contents}
    LAYOUT.write_text(json.dumps(layout, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'已将 {len(contents)} 篇文档合并为 {len(books)} 本主题手册。')


if __name__ == '__main__':
    main()
