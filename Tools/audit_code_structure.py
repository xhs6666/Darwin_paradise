"""Reproducible per-file structural review; heuristic candidates, not semantic proof."""
from pathlib import Path
from collections import Counter, defaultdict
import re,json
root=Path(__file__).resolve().parents[1]
def mask(source):
    pattern=r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    return re.sub(pattern,lambda m:''.join('\n' if c=='\n' else ' ' for c in m.group()),source)
def review(p):
    text=p.read_text(encoding='utf-8-sig');code=mask(text)
    methods=[]
    for m in re.finditer(r'^\s*(?:public|private|internal|protected)\s+(?:(?:static|async|virtual|override|sealed|new)\s+)*(?:[\w<>\[\],.?]+\s+)?(\w+)\s*\([^;{}]*\)\s*\{',code,re.M):
        opening=code.index('{',m.start());depth=1;end=opening+1
        while end<len(code) and depth:
            depth+=int(code[end]=='{')-int(code[end]=='}');end+=1
        methods.append({'name':m.group(1),'lines':code.count('\n',opening,end)+1})
    editor='/Editor/' in p.as_posix();lines=len(text.splitlines());longest=max(methods,key=lambda x:x['lines'],default={'name':'—','lines':0})
    flags=[]
    if not editor and lines>500:flags.append('大文件')
    if not editor and longest['lines']>100:flags.append('长方法')
    if 'Camera.main' in code and ('ProcessCommand(' in code or 'SendWorld(' in code):flags.append('相机/权威混用')
    if 'WaitForEndOfFrame' in code:flags.append('渲染帧等待')
    statements=code.count(';')
    if not editor and statements>200 and len(code)/max(1,lines)>100:flags.append('高语句密度')
    return {'path':p.relative_to(root).as_posix(),'lines':lines,'statements':statements,'methods':len(methods),'longest':longest,'editor':editor,'lookups':len(re.findall(r'\bFind(?:First|Objects|Any)Object\w*\s*[<(]',code)),'flags':flags}
# ★ 新增：在全量源码中抓取重复的连续语句与高成本调用位置，避免只靠文件大小挑热点。
# 保留名字和常量，过滤引号/注释；候选需要结合调用频率和业务边界复核。
def calculation_candidates(paths):
    patterns = {
        '全局场景查找': r'\bFind(?:First|Objects|Any)Object\w*\s*[<(]',
        '距离或平方根': r'\b(?:Vector[23]\.Distance|Mathf\.Sqrt|Math\.Sqrt)\s*\(',
        '三角函数或幂': r'\b(?:Mathf|Math)\.(?:Pow|Sin|Cos|Tan|Asin|Acos|Atan2)\s*\(',
        '线性搜索': r'\.(?:Find|FindIndex|IndexOf|Exists|FirstOrDefault)\s*\(',
    }
    repeated = defaultdict(list)
    locations = []
    for path in paths:
        name = path.relative_to(root).as_posix()
        code = mask(path.read_text(encoding='utf-8-sig'))
        for kind, pattern in patterns.items():
            for match in re.finditer(pattern, code):
                locations.append({'kind': kind, 'path': name, 'line': code.count('\n', 0, match.start())+1})
        statements = list(re.finditer(r'[^;{}]+;', code))
        for index in range(len(statements)-2):
            group = statements[index:index+3]
            span = code[group[0].start():group[-1].end()]
            if '{' in span or '}' in span:
                continue
            tokens = re.findall(r'\w+|[^\w\s]', span)
            if len(tokens) < 40 or tokens[0] in {'using', 'namespace'}:
                continue
            repeated[' '.join(tokens)].append({'path':name,'line':code.count('\n',0,group[0].start())+1})
    groups = [items for items in repeated.values() if len({site['path'] for site in items}) > 1]
    groups.sort(key=len, reverse=True)
    return {'callCounts':dict(Counter(item['kind'] for item in locations)),
            'callSites':locations, 'repeatedStatementGroups':groups}

paths = sorted((root/'Assets').rglob('*.cs'))
rows=[review(p) for p in paths]
target=root/'Docs/开发设计/逐脚本结构检查.md'
summary=f'逐文件扫描 {len(rows)} 个脚本：运行时 {sum(not r["editor"] for r in rows)}，Editor {sum(r["editor"] for r in rows)}。'
out=['# 逐脚本结构检查','',summary,'','由 `python Tools/audit_code_structure.py` 生成。检查覆盖每个文件；方法统计为词法启发式，不包含 lambda 的独立复杂度。“语句”按屏蔽注释/字符串后的分号计数，包含 for 头和字段声明，用于发现压在少数行内的密集代码。标记是人工复核候选，不是 Bug 结论；空标记也不代表业务全路径已验证。','','| 文件 | 行数 | 语句 | 方法数 | 最大方法/行 | 全局查找调用数 | 结构候选 |','| --- | ---: | ---: | ---: | --- | ---: | --- |']
for r in rows:
    path=r['path'];link=Path('../../'+path).as_posix();longest=r['longest']
    out.append(f'| [{path.removeprefix("Assets/DarwinPark/")}]({link}) | {r["lines"]} | {r["statements"]} | {r["methods"]} | {longest["name"]}/{longest["lines"]} | {r["lookups"]} | {"、".join(r["flags"]) or "—"} |')
target.write_text('\n'.join(out)+'\n',encoding='utf-8')
artifact=root/'Artifacts/CodeStructure';artifact.mkdir(parents=True,exist_ok=True);(artifact/'inventory.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
candidates = calculation_candidates(paths)
(artifact/'calculation-candidates.json').write_text(json.dumps(candidates,ensure_ascii=False,indent=2),encoding='utf-8')
print(summary)
print(candidates['callCounts'], 'duplicate groups:', len(candidates['repeatedStatementGroups']))
for r in sorted((x for x in rows if not x['editor']),key=lambda x:x['lines'],reverse=True)[:12]:print(r['path'],r['lines'],r['longest'])
