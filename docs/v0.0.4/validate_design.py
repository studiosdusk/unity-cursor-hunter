"""v0.0.4 design checks: arithmetic, dependency graph, screen routes and PDF links."""
from pathlib import Path
import math,json,hashlib
from collections import deque
from pypdf import PdfReader
ROOT=Path(__file__).resolve().parents[2]
D=Path(__file__).resolve().parent
nodes=json.loads((D/'traits.json').read_text());screens=json.loads((D/'screens.json').read_text())
by={s['id']:s for s in screens};index={n['id']:n for n in nodes}
assert len(index)==118 and sum(not n['free'] for n in nodes)==114
for n in nodes:
 seen=set();cur=n
 while cur['parent']:
  assert cur['id'] not in seen;seen.add(cur['id']);cur=index[cur['parent']]
assert index['AUTO']['parent']=='STAT_ROOT'
for name in ['FIRE','LIGHTNING','FREEZE','HURRICANE','BREATH']:assert index[name]['parent']=='SKILL_ROOT'
for name in ['SLIME','GOBLIN','ORC','GOLEM','WYVERN','DRAGON']:assert index[name]['parent']=='MONSTER_ROOT'
for s in screens:
 for a in s['actions']:assert a['target']=='END' or a['target'] in by
visited=set();q=deque(['S01'])
while q:
 id=q.popleft()
 if id in visited or id=='END':continue
 visited.add(id);q.extend(a['target'] for a in by[id]['actions'])
# S08's late-game state is reachable through the postgame representative path.
assert visited==set(by),set(by)-visited
anchors=[10,21,100,1000,20000,1000000,100000000]
def damage(u):
 if u>=30:return anchors[-1]
 i=int(u//5);return math.floor(anchors[i]*(anchors[i+1]/anchors[i])**((u-5*i)/5)+.5)
checks=[]
for L in range(30):
 node=index[f'PWR_{L+1:02}'];C=node['garnet_cost'];s=node['parts'];v,r=divmod(C,s);costs=[v+(i<r) for i in range(s)]
 assert sum(costs)==C and min(costs)>0
 vals=[damage(L+i/s) for i in range(s+1)]
 assert all(a<b for a,b in zip(vals,vals[1:])),(L,vals)
 checks.append(dict(level=L,parts=s,total_cost=C,partial_min=min(costs),partial_max=max(costs),attack_before=vals[0],attack_after=vals[-1]))
late=[]
for L,income in [(19,2745000*.6),(24,120720000*.6)]:
 x=checks[L];late.append(dict(level=L,assumed_garnet_per_minute=income,old_minutes=x['total_cost']/income,new_partial_seconds=x['partial_max']/income*60))
pdf=ROOT/'output/pdf/cursor-hunter-game-design-v0.0.4.pdf';r=PdfReader(pdf)
assert len(r.outline)==48
links=0
for p in r.pages:
 for ar in p.get('/Annots',[]):
  a=ar.get_object()
  if a.get('/Subtype')=='/Link':
   links+=1
   dest=a.get('/Dest')
   if dest is not None:
    assert len(dest)>=2
    ref=dest[0];target=ref.get_object() if hasattr(ref,'get_object') else ref
    assert target.get('/Type')=='/Page'
text='\n'.join(p.extract_text() for p in r.pages)
for s in screens:assert s['id']+'. '+s['title'] in text
assert '\ufffd' not in text
report=dict(status='passed',pages=len(r.pages),screen_states=len(screens),screen_routes_reachable=len(visited),pdf_links=links,trait_nodes=len(nodes),purchasable_nodes=114,partial_upgrade_checks=checks,late_upgrade_wait=late,source_sha256=hashlib.sha256((ROOT/'docs/cursor-hunter-game-design-v0.0.4.md').read_bytes()).hexdigest(),pdf_sha256=hashlib.sha256(pdf.read_bytes()).hexdigest(),limitations=['Design arithmetic and document links only; Unity runtime behaviour is not simulated'])
(ROOT/'output/pdf/cursor-hunter-v0.0.4-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k!='partial_upgrade_checks'},ensure_ascii=False,indent=2))
