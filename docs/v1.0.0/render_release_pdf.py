from __future__ import annotations
import sys,re,json,hashlib,math
from pathlib import Path
from xml.sax.saxutils import escape
from decimal import Decimal,ROUND_CEILING
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import render_plan_pdf as base
from reportlab.lib import colors
from reportlab.lib.units import mm
from reportlab.lib.pagesizes import A4
from reportlab.platypus import Flowable,BaseDocTemplate,Frame,PageTemplate
from reportlab.pdfbase import pdfmetrics
ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).resolve().parent
SCREENS=json.loads((HERE/'screens.json').read_text())
BY_ID={s['id']:s for s in SCREENS}
OUTPUT=ROOT/'output/pdf/cursor-hunter-game-design-v1.0.0.pdf'
SOURCE=ROOT/'docs/cursor-hunter-game-design-v1.0.0.md'

def markup(text):
    text=escape(text)
    text=re.sub(r'\[([^\]]+)\]\(([^)]+)\)',lambda m:f'<link href="{m[2]}" color="#177A91">{m[1]}</link>',text)
    text=re.sub(r'`([^`]+)`',r'<font name="KoreanCode">\1</font>',text)
    return re.sub(r'\*\*(.+?)\*\*',r'<b>\1</b>',text)
base.inline_markup=markup

def generate_traits():
    nodes=[]
    def node(id,tab,parent=None,boss=0,cost=0,parts=1,free=False):
        nodes.append(dict(id=id,tab=tab,parent=parent,boss=boss,garnet_cost=cost,parts=parts,free=free))
    for t in ['STAT','SKILL','MONSTER']:node(t+'_ROOT',t,free=True)
    bands=[(30,'1.18'),(2000,'1.35'),(80000,'1.35'),(5000000,'1.35'),(200000000,'1.35'),(5000000000,'1.35')]
    for L in range(30):
        b,r=bands[L//5];cost=int((Decimal(b)*Decimal(r)**(L%5)).to_integral_value(rounding=ROUND_CEILING))
        node(f'PWR_{L+1:02}','STAT',f'PWR_{L:02}' if L else 'STAT_ROOT',L//5,cost,1 if L<5 else 5 if L<10 else 10)
    for i,c in enumerate([80,2000,40000,2000000,50000000,1000000000],1):node(f'RANGE_{i}','STAT',f'RANGE_{i-1}' if i>1 else 'STAT_ROOT',i-1,c)
    node('MULTI_2','STAT','STAT_ROOT',1,100);node('MULTI_3','STAT','MULTI_2',2,5000)
    node('AUTO','STAT','STAT_ROOT',3,1000000)
    for i,c in enumerate([10000000,20000000,40000000,80000000],1):node(f'SPEED_{i}','STAT',f'SPEED_{i-1}' if i>1 else 'AUTO',4,c)
    for i,(name,c,b) in enumerate(zip(['FIRE','LIGHTNING','FREEZE','HURRICANE','BREATH'],[1500,40000,1000000,30000000,300000000],[2000,80000,5000000,200000000,5000000000]),1):
        node(name,'SKILL','SKILL_ROOT',i,c)
        for L in range(5):node(f'{name}_{L+1}','SKILL',f'{name}_{L}' if L else name,i,int((Decimal(b)*Decimal('1.5')**L).to_integral_value(rounding=ROUND_CEILING)))
    for i,(name,c,b) in enumerate(zip(['SLIME','GOBLIN','ORC','GOLEM','WYVERN','DRAGON'],[0,120,250,450,800,1200],[100,2000,80000,5000000,200000000,5000000000])):
        node(name,'MONSTER','MONSTER_ROOT',i,c,free=i==0)
        for attr,mul in [('SPAWN',1),('REWARD',1.5)]:
            for L in range(3):node(f'{name}_{attr}_{L+1}','MONSTER',f'{name}_{attr}_{L}' if L else name,i,math.ceil(b*mul*2**L))
    # Secondary currencies are explicit, never inferred from UI colour.
    index={n['id']:n for n in nodes}
    secondary={'MULTI_2':{'topaz':8},'MULTI_3':{'topaz':25},'AUTO':{'amethyst':30},'FIRE':{'topaz':10},'LIGHTNING':{'amethyst':12},'FREEZE':{'amethyst':25},'HURRICANE':{'sapphire':12},'BREATH':{'diamond':10},'ORC':{'topaz':10},'GOLEM':{'amethyst':15},'WYVERN':{'sapphire':15},'DRAGON':{'diamond':5}}
    for i in range(1,5):secondary[f'SPEED_{i}']={'sapphire':10*i}
    for name in ['FIRE','LIGHTNING','FREEZE','HURRICANE','BREATH']:
        for i in range(1,6):secondary[f'{name}_{i}']={('diamond' if name=='BREATH' else 'amethyst'):(2 if name=='BREATH' else 5)*i}
    for n in nodes:n['secondary_cost']=secondary.get(n['id'],{})
    assert len(nodes)==118 and sum(not n['free'] for n in nodes)==114
    for n in nodes:
        seen=set();cur=n
        while cur['parent']:
            assert cur['id'] not in seen;seen.add(cur['id']);cur=index[cur['parent']]
    assert index['AUTO']['parent']=='STAT_ROOT'
    (HERE/'traits.json').write_text(json.dumps(nodes,ensure_ascii=False,indent=2)+'\n')
    return nodes

def assemble_source():
    old=(ROOT/'docs/cursor-hunter-game-design-v0.0.3.md').read_text()
    sections=old.split('<!-- pagebreak -->')
    def get(prefix):
        part=next(p for p in sections if prefix in p)
        part=re.sub(r'### [^\n]*와이어프레임\n```wireframe[^\n]*\n.*?```\n','',part,flags=re.S)
        return part.strip()
    core=(HERE/'core.md').read_text().replace('유료 노드','구매 노드').replace('실제 결제가 아니라 게임 재화로 구입하는 노드를 뜻한다.','게임 재화로 구입하는 노드를 뜻한다.')
    core=core.replace('‘공격력12 / 강화3 of10 / 다음100→103’','‘공격력 노드12 / 강화3 of10 / 현재→다음 피해량’')
    chunks=[core.strip()]
    attack=get('## 12.').split('### 그래프와 노드 상태')[0]
    attack=attack.replace('## 12. 기본 공격 특성 · 급격한 성장 곡선','## B01. 공격력 · 완성 노드 비용 원장')
    attack=attack.replace('현재 L의 다음 레벨 가넷 비용','다음 완성 노드의 총 가넷 비용')
    attack+='\n\n위 공격력은 완성L 기준이다. 부분 강화의 u=L+q/s와 정수 분할 비용은 A04를 우선 적용한다. 기존 완성 노드의 순서·보스 조건은 유지한다.'
    chunks.append(attack)
    for prefix,title in [('## 10.','B02. 일반 몬스터 · 종별 강화 원장'),('## 09.','B03. 스킬 · 피해·구매 원장'),('## 13.','B04. 보스 HP · 목표·보상'),('## 06.','B05. 파훼 주기 · 보상 확정'),('## 14.','B06. 물량 · 재화 공급 · 시간 예산')]:
        part=get(prefix);part=re.sub(r'^## [^\n]+',f'## {title}',part)
        part=part.replace('14장의','B06의').replace('14장','B06').replace('순서·비용은','순서·비용은')
        if prefix=='## 10.':part+='\n\n출현: t=0에 첫 무리를 생성하고 t=60 생성은 제외한다. 무리 중심은 HUD를 제외한 영역에서 무작위 선택하며 80px 안에 배치한다. 상단160px·하단96px·좌우32px은 생성에서 제외한다. 종별 생성 주기와 단계별 총 상한은 B06을 따른다. 위치 탐색은 최대8회이며 실패한 생성은 건너뛴다. 상한 도달 시 밀린 생성은 누적하지 않고, 종별 순환 순서로 기회를 배분한다. 몬스터끼리 겹침은 허용한다.'
        if prefix=='## 13.':part=part.split('### 엔딩 흐름')[0]+'\n\n엔딩·크레딧·재실행의 상세 흐름은 S31~S33을 따른다.'
        chunks.append(part)
    fx=get('## 16.').split('### 저장·예외 처리 초안')[0]
    fx=re.sub(r'^## [^\n]+','## B07. 타격 연출 · 가독성 예산',fx)
    fx+='\n\n일반 피격 숫자는 적당0.1초 묶음, 화면동시숫자60개, 보석연출60개, 충돌연출100개를 초안 상한으로 둔다. 흡수음은0.1초당1회, 타격음은동시8보이스로제한하고이상한큰음량을만들지않는다. 표식입력음은전투음보다우선한다. 성능저하에서는파티클·숫자부터줄이고적체력·보상·타격판정은바꾸지않는다.'
    chunks.append(fx)
    flow='''## C00. 전체 화면 흐름과 탐색

```text
최초: S01 -> S02 -> S04 -> S05 -> S06 -> S07 -> S45 -> S11
성장: S11 -> S12 / S13 / S14 -> S16 -> S19 -> S07
보스: S20 -> S22 -> S23 -> S24 -> S25 -> S26 / S27 -> S23
결과: 처치 S28 -> S29 -> 성장 / 시간초과 S30 -> 성장·재도전
엔딩: v5최초 S28 -> S31 -> S32 -> S33
공통: S09 -> S34~S38 -> 이전화면 / S40 저장오류 / S41 종료
```

화면 아래의 이동 링크로 해당 페이지를 열 수 있다. 자동 전이는 실제 게임 버튼이 아니라 조건 결과다. 설정·일시정지·모달의 ‘돌아가기’는 실제로 진입 화면으로 복귀하며, PDF는 대표 경로를 연결했다. 전체 화면ID는 다음 표에서 확인한다.

| 시작·일반·성장 | 성장·보스·엔딩 | 설정·오류·복구 |
| --- | --- | --- |
'''
    for i in range(16):flow+='| '+' | '.join(f'[{SCREENS[k]["id"]} {SCREENS[k]["title"].split(" · ")[0]}](#{SCREENS[k]["id"]})' for k in [i,i+16,i+32])+' |\n'
    chunks.append(flow)
    for s in SCREENS:
        chunks.append(f'''## {s['id']}. {s['title']}

```wireframe {s['id']}
{' / '.join(s['lines'])}
```

### 진입 · 목적

{s['entry']}. 이 화면은 {s['title']} 상태를 다룬다.

### 동작 · 정보 · 전환

{s['rule']}

### 예외 · 복귀 · 저장

{s['edge']}

### 연결

'''+ ' / '.join(f"{a['label']} → {a['target']}" for a in s['actions'])+'\n')
    SOURCE.write_text('\n\n<!-- pagebreak -->\n\n'.join(chunks)+'\n')

class ReleaseWire(Flowable):
    def __init__(self,kind):
        super().__init__();self.s=BY_ID[kind.upper()];self.width=170*mm;self.height=117*mm
    def wrap(self,a,b):return self.width,self.height
    def draw(self):
        c=self.canv;c.bookmarkPage(self.s['id']);c.addOutlineEntry(self.s['id']+' '+self.s['title'],self.s['id'],0,False)
        c.saveState();c.translate(0,21*mm);c.scale(self.width/1000,96*mm/562)
        self.c=c;self.H=562;self.ink='#E9F0FC';self.dim='#A9BBD6';self.gold='#F3BC52';self.bg='#111D34'
        self.rect(0,0,1000,562,self.bg,'#445774',8)
        k=self.s['kind']
        if k in ['stat','skill','monster','node','node-locked','node-poor','node-done']:self.tree(k)
        elif k.startswith('normal') or k.startswith('tutorial'):self.field(k)
        elif k.startswith('boss') and k not in ['boss-select','boss-locked','boss-result']:self.boss(k)
        elif k in ['boss-select','boss-locked']:self.selection(k)
        elif k.startswith('settings'):self.settings(k)
        elif k in ['result','boss-result','failure']:self.result(k)
        elif k in ['main','main-end']:self.main_screen(k)
        elif k=='summary':self.summary()
        elif k=='records':self.records()
        elif k in ['splash','credits','ending','saving','setup']:self.special(k)
        else:self.modal()
        c.restoreState()
        # Navigation strips are deliberately outside the pictured game screen.
        c.setFont('Korean',6.5);c.setFillColor(colors.HexColor('#53647A'));c.drawString(0,16*mm,'검토용 이동 (자동 전이와 대표 복귀 경로 포함)')
        n=len(self.s['actions']);cols=min(n,4);w=self.width/cols
        for i,a in enumerate(self.s['actions']):
            x=(i%cols)*w;y=(9-(i//cols)*7)*mm
            label=a['label']+' → '+a['target'];c.setFont('Korean',6.3);c.setFillColor(colors.HexColor('#177A91'));c.drawString(x,y,label)
            if a['target']!='END':c.linkRect('',a['target'],(x,y-2,x+w-3,y+8),relative=1,thickness=0)
    def rect(self,x,y,w,h,fill,stroke=None,r=4):
        if w<=0 or h<=0:return
        c=self.c;c.setFillColor(colors.HexColor(fill));c.setStrokeColor(colors.HexColor(stroke or fill));c.setLineWidth(.9);c.roundRect(x,562-y-h,w,h,r,fill=1,stroke=bool(stroke))
    def text(self,x,y,t,size=14,col=None):
        c=self.c;c.setFont('Korean',size);c.setFillColor(colors.HexColor(col or self.ink));c.drawString(x,562-y-size,t)
    def block(self,x,y,t,w=630,size=17,gap=9,col=None):
        lines=[];line=''
        for ch in t:
            if ch=='\n' or pdfmetrics.stringWidth(line+ch,'Korean',size)>w:
                lines.append(line);line='' if ch=='\n' else ch
            else:line+=ch
        if line:lines.append(line)
        for i,line in enumerate(lines):self.text(x,y+i*(size+gap),line,size,col)
        return y+len(lines)*(size+gap)
    def button(self,x,y,w,label,target=None,primary=False):
        self.rect(x,y,w,40,self.gold if primary else '#2A405E','#536D91',4)
        self.text(x+12,y+10,label,14,self.bg if primary else self.ink)
        if target and target!='END':self.c.linkRect('',target,(x,562-y-40,x+w,562-y),relative=1,thickness=0)
    def gems(self,left=False):
        names=['가넷','토파즈','자수정','사파이어','다이아'];cs=['#C75C61','#E7B747','#9D70CE','#569ADC','#E0EAF7']
        if left:
            self.text(24,142,'잼 스톤 보유',16)
            for i,(name,col) in enumerate(zip(names,cs)):
                y=177+i*48;self.rect(20,y,192,38,'#223852','#516682');self.rect(30,y+9,18,18,col);self.text(56,y+10,name,13);self.text(126,y+10,(['45,000','60','30','15','0'] if self.s['id']=='S19' else ['50,000','10','30','15','0'] if self.s['id']=='S18' else ['50,000','85','30','15','0'])[i],13)
        else:
            for i,(name,col) in enumerate(zip(names,cs)):
                x=505+i*96;self.rect(x,17,90,56,'#263C50');self.text(x+9,23,name,12,col);self.text(x+9,43,'0',16)
    def label_lines(self,x,y,w,size=17):
        for line in self.s['lines']:y=self.block(x,y,line,w,size)+12
    def footer_buttons(self):
        aa=[a for a in self.s['actions'] if a['target']!='END'][:4]
        for i,a in enumerate(aa):self.button(35+i*240,503,225,a['label'],a['target'],i==0)
    def main_screen(self,k):
        self.landscape();self.gems();self.text(335,121,'CURSOR HUNTER',38,'#213A31')
        if k=='main-end':self.text(343,177,'엔딩 완료 · 자유 성장',22,'#253D30')
        for i,(label,dest) in enumerate([('일반 Field','S05' if k=='main' else 'S08'),('보스','S20'),('특성','S12'),('설정','S34')]):
            self.text(425,226+i*56,label,26,'#203C30')
            self.c.linkRect('',dest,(372,562-266-i*56,626,562-226-i*56),relative=1,thickness=0)
        if k=='main-end':self.button(380,463,235,'엔딩 다시 보기','S32')
        self.button(28,505,160,'기록','S42');self.button(814,505,160,'게임 종료','S41')
    def landscape(self):
        self.rect(0,0,1000,562,'#AFCC7B')
        for x,y,r in [(70,126,60),(260,40,45),(805,132,50),(940,400,60),(148,489,48),(610,514,53)]:
            c=self.c;c.setFillColor(colors.HexColor('#739A69'));c.circle(x,562-y,r,fill=1,stroke=0)
    def field(self,k):
        if k.startswith('tutorial'):
            self.landscape();self.gems();self.rect(24,22,235,58,'#F4EED8');self.text(39,39,'연습 / 시간 정지',20,'#263E34')
            self.rect(289,268,45,45,'#668D56' if k=='tutorial' else '#C75C61');self.text(259,230,'슬라임 HP20' if k=='tutorial' else '연습 보석 / 보상0',18,'#263E34')
            self.c.setStrokeColor(colors.HexColor('#2B7590'));self.c.circle(311,562-290,25,fill=0,stroke=1)
            self.rect(490,164,480,271,'#172A43','#EFBD50');self.text(512,184,'처음 사냥 안내',24,self.gold);self.label_lines(512,230,432,16)
            self.button(25,500,190,'안내 건너뛰기' if k=='tutorial' else '특성 살펴보기','S07' if k=='tutorial' else 'S12')
            if k!='tutorial':
                self.button(694,375,245,'일반 Field 시작','S07',True)
                self.text(292,131,'보석 흡수  →  우측 상단',19,'#263E34')
            return
        self.landscape();self.gems();self.rect(24,22,166,58,'#F4EED8');self.text(39,39,'TIME 00:42' if k!='normal-late' else 'TIME 00:12',20,'#263E34')
        self.text(550,88,'이번 판 +24 가넷' if k!='normal-late' else '이번 판 +7,240만 가넷',16,'#28412F')
        late=k=='normal-late';palette=['#668D56','#B29B44','#9E7157','#5D8096','#936AA9']
        groups=5 if late else 1
        for g in range(groups):
            cx=285+g*135;cy=220+(g%2)*105
            for j in range(15 if late else 8):
                x=cx+(j%5)*(17 if late else 42);y=cy+(j//5)*(19 if late else 48)
                self.rect(x,y,12 if late else 24,12 if late else 24,palette[g],r=5)
        self.c.setStrokeColor(colors.HexColor('#2B7590'));self.c.setLineWidth(2);self.c.circle(527,562-312,118 if late else 22,fill=0,stroke=1)
        self.text(300,460,'커서 반경 · 겹친 모든 적에게 타격',18,'#28412F')
        self.button(22,504,200,'ESC 일시정지','S09');self.text(360,516,'자동 ON [A] / 30묶음 × 3타' if late else '자동 클릭: 아직 해금되지 않음',17,'#28412F')
        if k.startswith('tutorial'):
            self.rect(186,139,632,255,'#172A43','#EFBD50');self.text(216,160,'처음 사냥 안내',24,self.gold);self.label_lines(216,209,570,17);self.button(540,341,238,'계속 / 안내 완료','S06' if k=='tutorial' else 'S07',True)
    def boss(self,k):
        self.rect(0,0,1000,562,'#354657');self.gems();self.rect(22,21,163,53,'#F1E7CE');self.text(34,36,('TIME 01:00' if k=='boss-intro' else 'TIME 00:21' if k=='boss-bad' else 'TIME 00:41' if k in ['boss-input','boss-good'] else 'TIME 00:42' if k=='boss-warning' else 'TIME 00:43'),20,'#27353D')
        self.text(34,91,'v2 고블린 족장    '+('12,000' if k=='boss-intro' else '8,400')+' / 12,000',20);self.rect(26,122,947,24,'#152238');self.rect(28,124,943 if k=='boss-intro' else 660,20,'#CA655F')
        self.c.setFillColor(colors.HexColor('#837198'));self.c.circle(490,562-286,83,fill=1,stroke=0);self.text(449,276,'BOSS',28)
        self.text(346,403,'일반 몬스터 없음 / 파훼 보너스 예약 '+('0%' if k=='boss-intro' else '+10%' if k=='boss-good' else '+5%'),16)
        self.button(24,508,175,'ESC 일시정지','S09');self.text(560,513,'자동 / 스킬은 직접 파훼하지 않음',15)
        if k=='boss-intro':self.rect(184,194,634,140,'#17283F');self.text(295,218,'고블린 족장 / 준비',30);self.text(255,272,'0.6초 후 시작 · 준비 중 공격과 타이머 없음',18)
        elif k in ['boss-warning','boss-input']:
            for i in range(2):
                x=700+i*110;self.rect(x,264,78,78,('#6DA491' if i==0 else '#D2B75B') if k=='boss-input' else '#4A5362',self.gold);self.text(x+27,281,'완료' if k=='boss-input' and i==0 else str(i+1),20 if k=='boss-input' and i==0 else 32,self.bg if k=='boss-input' else self.ink)
            self.text(670,360,'예고 0.6초' if k=='boss-warning' else '표식2를 클릭 / 남은 1.2초',18)
        elif k in ['boss-good','boss-bad']:
            self.rect(228,181,552,76,'#243F46' if k=='boss-good' else '#623D47');self.text(261,204,'파훼 성공 / 처치 시 +5%' if k=='boss-good' else '파훼 실패 / 남은 시간 -4초',27)
    def selection(self,k):
        self.text(30,26,'< 돌아가기       보스 선택',28);self.gems()
        self.rect(24,116,225,362,'#21344D');self.rect(270,116,389,362,'#20354A');self.rect(680,116,294,362,'#26354C')
        for i,name in enumerate(['v1 슬라임 킹','v2 고블린 족장','v3 오크 워로드','v4 수정 골렘','v5 고대 드래곤']):
            self.rect(37,139+i*61,198,47,'#486582' if i==(2 if k=='boss-locked' else 1) else '#2D435F');self.text(47,152+i*61,name,17)
        self.text(292,145,'v3 잠김' if k=='boss-locked' else 'v2 도전 가능',25,self.gold)
        self.block(292,207,'먼저 v2를 처치하세요' if k=='boss-locked' else 'HP 12,000 / 60초\n표식2개를 순서대로 클릭\n실패하면 -4초\n입장료 없음',340,20)
        self.text(701,145,'보상 / 첫 처치 해금',21);self.block(701,196,'가넷50,000 / 자수정50\n사파이어15\n자동 / 골렘 / 프리징\nHP180,000' if k=='boss-locked' else '가넷2,000\n토파즈35 / 자수정20\n트리플 / 오크 / 라이트닝\n현재 이론DPS400',246,18)
        self.footer_buttons()
    def tree(self,k):
        tab='skill' if k=='skill' else 'monster' if k=='monster' else 'stat'
        for i,(lab,key,dest) in enumerate([('Stat','stat','S12'),('Skill','skill','S13'),('Monster','monster','S14')]):self.button(22+i*160,22,150,lab,dest,tab==key)
        self.gems(True);self.text(25,92,'전체 보기 / 선택 위치 / 확대 100%',14)
        self.rect(728,17,249,99,'#233651','#526D8F');self.text(743,27,'현재 특성   > 상세',16)
        for i,(lab,v) in enumerate([('공격 L10',1/3),('반경60',1/3),('다중3' if k=='node-done' else '다중2',1 if k=='node-done' else 2/3),('자동0',0)]):
            y=51+i*14;self.text(744,y,lab,11);self.rect(829,y+2,126,8,'#485E7B');self.rect(829,y+2,126*v,8,self.gold)
        self.c.linkRect('','S15',(728,446,977,545),relative=1,thickness=0)
        self.text(267,135,'연결선=같은 가지 선행 / 열린 다른 가지는 자유 선택',15,self.dim)
        ys=[191,251,311,371]; labels=['공격력','범위','다중','자동화']
        if tab=='skill':ys=[181,229,277,325,373];labels=['파이어','라이트닝','프리징','허리케인','브레스']
        if tab=='monster':ys=[172,228,284,340,396];labels=['슬라임','고블린','오크','골렘','와이번']
        c=self.c;c.setStrokeColor(colors.HexColor(self.gold));c.setLineWidth(2);c.line(319,562-min(ys)-18,319,562-max(ys)-18)
        self.rect(242,271,64,39,'#426961',self.gold);self.text(253,282,'시작',14)
        c.line(306,562-290,319,562-290)
        if tab=='monster':
            ys=[172,228,284,340,396]
            for i,(y,label) in enumerate(zip(ys,labels)):
                c.setStrokeColor(colors.HexColor(self.gold));c.setLineWidth(2)
                c.line(319,562-y-18,520,562-y-18)
                self.rect(354,y,134,37,'#426961' if i<2 else '#354C70',self.gold)
                self.text(365,y+11,label+(' 완료' if i<2 else ' 구매 가능' if i==2 else ' / 잠금'),12)
                c.line(520,562-y-4,520,562-y-32)
                for z,lab in [(y-8,'출현 강화 0/3'),(y+20,'보상 강화 0/3')]:
                    c.line(520,562-z-12,597,562-z-12)
                    self.rect(597,z,185,24,'#304968' if i<2 else '#35384D',self.gold if i<2 else '#7C6F8A');self.text(609,z+5,lab,12)
                self.text(813,y+11,'각각 독립',13,self.dim)
        else:
            for i,(y,label) in enumerate(zip(ys,labels)):
                count=2 if tab=='stat' and i==2 else 4
                c.setStrokeColor(colors.HexColor(self.gold));c.setLineWidth(2)
                c.line(319,562-y-18,354+(count-1)*151,562-y-18)
                for j in range(count):
                    x=354+j*151;owned=(j==0 and i<3) if tab=='stat' else (i==0 and j==0)
                    if k=='node-done' and i==2 and j==1:owned=True
                    self.rect(x,y,134,37,'#426961' if owned else '#354C70' if i<3 else '#35384D',self.gold if i<3 else '#7C6F8A')
                    content=(label if j==0 else f'피해 Lv{j}')
                    if tab=='stat':content=[['공격력10','노드11 0/10','노드12','노드13'],['반경Lv2','Lv3 /96px','Lv4 /150px','Lv5 /240px'],['더블 완료','트리플 선택'],['자동 /v3','속도15 /v4','속도20','속도25→30']][i][j]
                    self.text(x+8,y+11,content,12)
        self.text(269,443 if tab=='monster' else 421,'현재 성장 주변 보기 · 118노드 원장 · 이전/후속 단계는 팬·줌으로 탐색',10 if tab=='monster' else 13,self.dim)
        self.rect(22,457,955,84,'#233C59','#6681A1');self.block(39,468,self.s['lines'][1],717,15)
        self.text(39,513,'선택만으로 구매하지 않음 / 다른 가지 잠금 없음',13,self.dim)
        if k in ['node-locked','node-poor']:self.button(807,482,147,'구매 불가',None,False)
        elif k=='node-done':self.button(807,482,147,'MAX',None,False)
        else:self.button(807,482,147,'노드 상세' if k in ['stat','skill','monster'] else '1회 구매','S16' if k in ['stat','skill','monster'] else 'S19',True)
        if k.startswith('node'):
            self.rect(277,169,675,264,'#263D58','#EABB57');self.text(299,187,self.s['title'],24,self.gold);self.label_lines(299,239,619,18)
    def summary(self):
        self.text(28,27,'현재 특성 / 보유 효과 전체',28)
        for i,(label,value,ratio) in enumerate([('공격력','100 / L10',1/3),('공격 반경','60px / Lv2',1/3),('다중 타격','2타',2/3),('자동 속도','0 / 미구매',0)]):
            y=115+i*75;self.text(40,y,label,22);self.rect(230,y+5,395,23,'#415976');self.rect(230,y+5,395*ratio,23,self.gold);self.text(656,y,value,22)
        self.text(40,443,'장착: 파이어 볼 / 이론DPS400 / 스킬 제외, 명중100%',20);self.footer_buttons()
    def settings(self,k):
        self.text(28,24,'설정',30)
        for i,(label,dest) in enumerate([('오디오','S34'),('화면','S35'),('조작·가독성','S37'),('데이터','S38')]):self.button(29+i*240,82,224,label,dest)
        self.rect(28,144,945,324,'#243B55','#4C6888');self.label_lines(55,169,867,24)
        if k=='settings-audio':
            for i,v in enumerate([.8,.6,.8]):
                self.text(64,321+i*35,['전체','음악','효과'][i],15);self.rect(150,330+i*35,650,10,'#485D78');self.rect(150,330+i*35,650*v,10,self.gold)
        else:self.block(56,345,'선택 항목은 즉시 미리보기 / 적용 전까지 변경 취소 가능\n다른 탭으로 이동해도 편집 중인 설정 유지',825,17,col=self.dim)
        self.footer_buttons()
    def result(self,k):
        self.text(32,27,'< Main',18);self.text(306,38,'보스 도전 실패' if k=='failure' else '보스 격파' if k=='boss-result' else '일반 Field 완료',31,self.gold)
        self.rect(27,105,945,326,'#243A54','#5D7792');self.label_lines(56,124,886,22)
        self.rect(50,298,437,100,'#182B43');self.rect(512,298,436,100,'#182B43')
        self.block(65,313,'기본 보상 / 파훼 보너스 / 최종\n가넷 · 토파즈 · 자수정 · 사파이어 · 다이아',403,16)
        self.block(529,313,'종류별 처치 / 실측 DPS / 경과 시간\n보스: 파훼 성공·실패 / 잔여 HP',402,16)
        self.footer_buttons()
    def records(self):
        self.text(30,26,'기록 / 도전과제',30);self.label_lines(33,83,920,21)
        for i,name in enumerate(['첫 사냥','한 바퀴','한 걸음 성장','관문 돌파1','관문 돌파2','손이 가벼워졌다']):
            x=30+(i%2)*480;y=259+(i//2)*69;self.rect(x,y,456,55,'#29425E','#607B95');self.text(x+15,y+15,name,20);self.text(x+294,y+19,'달성' if i<5 else '자동 구매 필요',14,self.gold)
        self.footer_buttons()
    def special(self,k):
        self.text(278,63,'CURSOR HUNTER',43,self.gold)
        self.label_lines(136,180,747,25)
        if k in ['splash','saving']:
            self.rect(167,398,665,14,'#3C526E');self.rect(167,398,412,14,self.gold);self.text(347,441,'완료 전 입력을 처리하지 않습니다',17,self.dim)
        else:self.footer_buttons()
    def modal(self):
        self.text(29,24,'CURSOR HUNTER / 배경 입력 잠김',18,self.dim)
        self.rect(134,84,733,398,'#294059','#93A7BC');self.block(160,110,self.s['title'],682,27,col=self.gold);self.label_lines(162,183,672,22)
        for i,a in enumerate(self.s['actions'][:3]):self.button(163+i*226,418,211,a['label'],a['target'],i==0)
        if self.s['id']=='S39':self.rect(160,350,670,46,'#122C41','#7691AF');self.text(178,363,'입력: RESET',18)

base.WireframeFlowable=ReleaseWire

def footer(c,d):
    c.saveState();w,h=A4;c.setFillColor(colors.HexColor('#172A43'));c.rect(0,h-9*mm,w,9*mm,fill=1,stroke=0);c.setFont('Korean',8);c.setFillColor(colors.white);c.drawString(18*mm,h-6*mm,'CURSOR HUNTER / v1.0.0 RELEASE DESIGN')
    c.setFillColor(colors.HexColor('#66758A'));c.drawString(18*mm,9*mm,'출시 설계 초안 · 구현/플레이테스트 미완료');c.drawRightString(w-18*mm,9*mm,str(c.getPageNumber()));c.restoreState()

def main():
    generate_traits();assemble_source();base.register_fonts();OUTPUT.parent.mkdir(parents=True,exist_ok=True)
    doc=BaseDocTemplate(str(OUTPUT),pagesize=A4,leftMargin=18*mm,rightMargin=18*mm,topMargin=17*mm,bottomMargin=18*mm,title='Cursor Hunter v1.0.0 출시 기획·전체 화면',author='Codex')
    frame=Frame(doc.leftMargin,doc.bottomMargin,doc.width,doc.height,id='body');doc.addPageTemplates([PageTemplate(id='release',frames=[frame],onPage=footer)])
    doc.build(base.markdown_flowables(SOURCE.read_text(),base.styles()));print(OUTPUT)
if __name__=='__main__':main()
