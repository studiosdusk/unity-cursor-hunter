"""Generate a path/GUID-verified candidate list, without modifying vendor assets."""
from pathlib import Path
import json
import re
ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT/'Assets/DownLoadAssets'

def main():
    files = sorted(p for p in SOURCE.rglob('*') if p.suffix in {'.png', '.prefab'})
    specs = [
        ('gem.garnet','Progression','Economy_Gem_01_Red.png','/256/'),
        ('gem.topaz','Progression','Economy_Gem_01_Yellow.png','/256/'),
        ('gem.amethyst','Progression','Economy_Gem_01_Purple.png','/256/'),
        ('gem.sapphire','Progression','Economy_Gem_01_Blue.png','/256/'),
        ('ui.trait.node','Progression','Button_Square01_White.prefab',''),
        ('ui.trait.tabs','Progression','TabMenu_Top_White.prefab',''),
        ('ui.boss.hp','Combat','StatusBar_White.prefab',''),
        ('field.normal','Combat','Map_3_Jungle Age.prefab','/Landscape/'),
        ('field.boss','Combat','Map_13_Medieval Age.prefab','/Landscape/'),
        ('monster.golem','Combat','Golem_Iron.prefab',''),
        ('boss.v4','Combat','Golem_Iron.prefab',''),
        ('skill.fireball','Combat','FX_AOE_Fireball.prefab','/URP/'),
        ('skill.hurricane','Combat','FX_AirTornado_Loop.prefab','/URP/'),
        ('skill.lightning','Combat','lightning1_yellow.png','Casual_VFX'),
        ('skill.freezing','Combat','FX_SnowTornado_Loop.prefab','/URP/'),
        ('skill.dragon-breath','Combat','fire2.png','Casual_VFX'),
        ('hit.basic','Combat','hit5_white.png','Casual_VFX'),
        ('hit.triple','Combat','hit1_purple.png','Casual_VFX'),
        ('parry.marker','Combat','magic_circle6_white.png','Casual_VFX'),
    ]
    rows=[]
    for key,owner,name,part in specs:
        candidates=[p for p in files if p.name==name and part in str(p)]
        if len(candidates)!=1:
            raise ValueError((key, candidates))
        p=candidates[0]
        guid=re.search(r'^guid: ([0-9a-f]{32})$',Path(str(p)+'.meta').read_text(),re.M)
        if not guid: raise ValueError(str(p))
        rows.append(dict(design_id=key,owner=owner,path=p.relative_to(ROOT).as_posix(),guid=guid[1],status='candidate-not-runtime-verified'))
    out=ROOT/'docs/collaboration/asset-map.json'
    out.write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
    header='# 에셋 후보와 기획 매핑\n\n파일 경로와 GUID를 확인한 후보 목록이다. 렌더링·애니메이션·머티리얼 호환성은 Unity에서 확인한다.\nGUI Lobby 미리보기는 육안 확인했다. 둥근 카드·테두리 스타일을 활용하되 기존 로비의 재화·상점·패스·캐릭터 UI를 그대로 가져오지 않는다. Main은 텍스트 메뉴이고 젬스톤은 표시하지 않는다.\n\n'
    header+='## 재사용 방식\n원본 Assets/DownLoadAssets는 보존한다. 각 모듈 소유 폴더에 Variant 또는 시각 래퍼를 만든다. GUID는 원본 추적용이며 런타임 로더 구현이 아니다.\n아래 링크의 에셋을 Unity에서 선택한 뒤 스프라이트 모드, pivot, 픽셀 크기, sorting, URP 머티리얼, emission/loop/stop을 점검한다.\n\n| 기획 ID | 담당 | 확인된 후보 |\n|---|---|---|\n'
    for row in rows:
        header+=f"| {row['design_id']} | {row['owner']} | [{Path(row['path']).name}](../../{row['path'].replace(' ', '%20')}) |\n"
    header+='''
## 조정·추가 제작
- 다이아몬드: 동일 Gem_01 계열에서 흰색 후보를 확정하지 않았다. 사파이어와 혼동되지 않는 흰 결정 외형 제작 후 ID gem.diamond에 연결한다. 녹색 보석을 다이아몬드로 확정하지 않는다.
- 슬라임/고블린/오크/와이번/드래곤 본체: 현재 패키지의 이름 기반 탐색에서 직접 대응 본체를 확인하지 못했다. 아이콘을 본체로 취급하지 않는다. Dragonfly는 잠자리다. 임시 도형으로 전투를 구현하고 외형만 교체한다.
- 골렘: Iron은 철 골렘이다. 일반 골렘 임시 후보이며 v4 수정 골렘은 결정 외형을 따로 제작한다. 일반과 보스는 다른 프리팹으로 분리한다.
- v1/v2/v3/v5 보스: 본체 확정 전 실루엣 임시 프리팹 사용. 기획의 몬스터 이름과 ID는 유지한다.
- 프리징: SnowTornado는 회전 눈 효과라서 얼음 정지 연출의 재료 후보일 뿐이다. 정지 표식/서리 링을 조합하고 광역 판정과 분리한다.
- 라이트닝/브레스: 텍스처 재료를 찾았으며 완성 스킬 프리팹을 확인한 것은 아니다. 별도 파티클/라인 연출 제작이 필요하다.
- 성장 타격: 흰 점 → 황금 이중 링 → 보라 파편 → 청색 맥동 → 결정·화염. 링은 자체 단순 도형으로 만들 수 있다. Sword Trails/Ink Slash/Magic Slash의 휘두르기 연출은 배제한다.
- 맵: Landscape 후보를 16:9로 배치하고 HUD 안전 영역을 제외한다. 배경 건물·장식에 적이 가려지지 않도록 명도와 밀도를 낮춘다.
- UI: 특성 좌상단 Stat/Skill/Monster, 좌중단 해금 젬, 우상단 요약 그래프, 중앙 팬·줌 트리. 보유 UI는 사용자 프리팹, 전투 안 배치 위치는 친구 프리팹에서 책임진다.
- 오디오·전용 커서·엔딩 아트는 이번 후보 목록에서 확정하지 않았다.

## 시각·성능 합류 점검
투사체/파티클은 피해 판정 주체가 아니다. 처치 효과 1,200개를 개별 장기 재생하지 않고 예산·풀·합성 연출을 적용한다. 자동 90타/초에서도 글자와 보스 파훼 표식이 읽히는지 확인한다.
이 목록은 에셋 적용 완료가 아니라 구현 담당에게 전달하는 후보·수정 방향이다. 기획의 추가 검토 내용은 PDF가 아닌 이 문서에 유지한다.
'''
    (ROOT/'docs/collaboration/assets.md').write_text(header)
    print(f'{len(rows)} asset paths and GUIDs mapped')

if __name__=='__main__': main()
