"""★ 新增：独立核对源码覆盖、图边、版本、方法链接和生成结果的新鲜度；不导入或执行游戏脚本。"""
from pathlib import Path
import hashlib,json,re,sys
root=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parents[2]
folder=root/'Docs/运行流程图';d=json.loads((folder/'manifest.json').read_text('utf-8'));s={x['id']:x for x in d['scripts']};m={x['id']:x for x in d['methods']};checks=0

def check(value,message):
 global checks
 if not value:raise AssertionError(message)
 checks+=1
check(len(s)==len(d['scripts']),'重复脚本ID');check(len(m)==len(d['methods']),'重复方法ID')
expected={p.relative_to(root).as_posix() for p in (root/'Assets').rglob('*') if p.suffix in ('.cs','.shader','.hlsl')}
check(expected=={x['path'] for x in s.values()},'脚本覆盖有遗漏或多余文件')
check({p.stem for p in (folder/'脚本').glob('*.md') if re.fullmatch('[a-f0-9]{16}',p.stem)}==set(s),'过期或多余的脚本分图')
for script in s.values():
 source=(root/script['path']).read_bytes().decode('utf-8-sig').replace('\r\n','\n')
 check(source==script['source'],'源码副本过期：'+script['path'])
 check(hashlib.sha256(source.encode()).hexdigest()==script['hash'],'源码Hash过期：'+script['path'])
 check((folder/'脚本'/f"{script['id']}.md").exists(),'缺少脚本Mermaid分图')
 check(len(set(script['methods']))==len(script['methods']),'脚本方法重复')
 for method in script['methods']:check(method in m and m[method]['scriptId']==script['id'],'方法文件归属错误')
 for attachment in script.get('attachments',[]):check((root/attachment['asset']).exists(),'无效场景挂载')
for method in m.values():
 script=s[method['scriptId']];linecount=len(script['source'].splitlines())
 check(1<=method['line']<=method['endLine']<=linecount+1,'方法行号越界：'+method['name'])
 for profile in method['profiles']:
  check(profile in method['graphs'] or method.get('graphRefs',{}).get(profile) in method['graphs'],'缺少版本流程图')
 for graph in method['graphs'].values():
  ids={n['id'] for n in graph['nodes']};check(len(ids)==len(graph['nodes']),'流程图节点ID重复')
  check(any(n['kind']=='entry' for n in graph['nodes']),'缺少入口节点')
  for edge in graph['edges']:check(edge['from'] in ids and edge['to'] in ids,'图边指向缺失节点')
  for node in graph['nodes']:check(0<=node['line']<=linecount+1,'流程步骤源码行越界')
 for call in method['calls']:
  for target in call['targets']:check(target in m,'调用方法目标缺失')
  check(all(p in method['profiles'] for p in call.get('profiles',[call['profile']])),'调用条件编译版本错误')
 for incoming in method['incoming']:check(incoming['method'] in m,'被调用来源缺失')
 for target in method.get('implementations',[])+method.get('contracts',[]):check(target in m,'接口实现链接缺失')
for variant in d['diagnostics']:check(variant['errors']==0,'语义编译错误：'+str(variant))
for overview in d['overview']:
 ids={n['id'] for n in overview['nodes']}
 for n in overview['nodes']:
  if n.get('target'):check(any(x['owner']+'.'+x['name']==n['target'] or x['symbol']==n['target'] or x['symbol'].startswith(n['target']+'(') for x in m.values()),'主流程错误方法：'+n['target'])
 for edge in overview['edges']:check(edge[0] in ids and edge[1] in ids,'主流程边指向缺失节点')
check((folder/'atlas-data.js').read_text('utf-8')=='window.FLOW_ATLAS='+json.dumps(d,ensure_ascii=False,separators=(',',':'))+';\n' or json.loads((folder/'atlas-data.js').read_text('utf-8')[18:-2])==d,'JS与JSON不同步')
for file in ('index.html','atlas.js','atlas.css','README.md','主流程.md','coverage.json'):check((folder/file).exists(),'缺少离线资源：'+file)
html=(folder/'index.html').read_text('utf-8');check(not re.search(r'(?:src|href)=[\"\']https?://',html),'离线页面依赖网络')
# 关键流程证据：接口调度、场景挂载、条件编译差异、GPU前向include、纯数据脚本均有对应内容。
find=lambda owner,name:next(x for x in m.values() if x['owner']==owner and x['name']==name)
check(len(find('IGeneLogic','OnUpdate').get('implementations',[]))>5,'基因接口实现关系未建立')
check(any(x['attachments'] for x in s.values() if x['path'].endswith('GameBootstrap.cs')),'启动脚本挂载未解析')
quit_method=find('GameMainMenu','Quit');check('Player' in quit_method['graphs'] and 'Editor' in quit_method['graphs'],'退出方法丢失Editor/Player差异')
check(any(not x['methods'] for x in s.values()),'数据声明文件未纳入覆盖')
gpu_source=next(x for x in s.values() if x['path'].endswith('PlanetURP.shader'))
check(any(m[target]['name']=='ApplyPopulationRange' for mid in gpu_source['methods'] for c in m[mid]['calls'] for target in c['targets']),'GPU include前向调用未链接')
report={'result':'PASS','checks':checks,'scripts':len(s),'methods':len(m),'overviewFlows':len(d['overview']),'sourceHash':hashlib.sha256(''.join(x['hash'] for x in d['scripts']).encode()).hexdigest()}
output=root/'Artifacts/FlowDocs';output.mkdir(parents=True,exist_ok=True);(output/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(report,ensure_ascii=True))
