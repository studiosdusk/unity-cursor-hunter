> 2026-10-01 변경: 아래 v0.0.4 본문은 개편 전 기록입니다. 현재 JSON·노드 UI·몬스터 10종·유물·자동 반경/쿨타임·30초 규칙은 [새 구현 기준](game-information-refactor-2026-10-01.md)및 [최신 v3 인수인계](current-state-handoff-v0.0.4.md)를 우선합니다. 기존 캡처는 현재 UI 검증 자료가 아닙니다.

# 에셋 후보와 기획 매핑

파일 경로와 GUID를 확인한 목록이다. 렌더링·애니메이션·머티리얼 호환성은 Unity에서
확인한다. GUI Lobby 미리보기는 후보 확인에만 사용했다. 특성 화면은 둥근 카드나 회색
프레임을 사용하지 않고 런타임 정사각 셀·색상 선으로 구성한다. Main 최상위 메뉴에는
젬스톤을 표시하지 않으며, 게임 시작 후 특성·전투·정산 화면에는 해금된 종류만 표시한다.

## 재사용 방식
원본 Assets/DownLoadAssets는 보존한다. 각 모듈 소유 폴더에 Variant 또는 시각 래퍼를 만든다. GUID는 원본 추적용이며 런타임 로더 구현이 아니다.
아래 링크의 에셋을 Unity에서 선택한 뒤 스프라이트 모드, pivot, 픽셀 크기, sorting, URP 머티리얼, emission/loop/stop을 점검한다.

| 기획 ID | 담당 | 특성 화면 적용 에셋 |
|---|---|---|
| gem.garnet | Progression | [ItemIcon_Gem_Pentagon_Red.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Red.png) |
| gem.topaz | Progression | [ItemIcon_Gem_Pentagon_Yellow.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Yellow.png) |
| gem.amethyst | Progression | [ItemIcon_Gem_Pentagon_Purple.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Purple.png) |
| gem.sapphire | Progression | [ItemIcon_Gem_Pentagon_Blue.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Blue.png) |
| gem.diamond | Progression | [ItemIcon_Gem_Pentagon_Green.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Green.png) *(프로토타입 임시 색상)* |
| ui.trait.node | Progression | 런타임 4면 선 + 평면 Image (`TraitScreenController.cs`) |
| ui.trait.tabs | Progression | [TabMenu_Top_White.prefab](../../Assets/DownLoadAssets/GUI-CasualFantasy/Prefabs/Prefabs_Component_UI_Etc/TabMenu_Top_White.prefab) |
| ui.boss.hp | Combat | [StatusBar_White.prefab](../../Assets/DownLoadAssets/GUI-CasualFantasy/Prefabs/Prefabs_Component_UI_Etc/StatusBar_White.prefab) |
| field.normal | Combat | [Map_3_Jungle Age.prefab](../../Assets/DownLoadAssets/Map/2D%20Maps%20-%20Age%20Battle%20Stages/Prefabs/Landscape/Map/Map_3_Jungle%20Age.prefab) |
| field.boss | Combat | [Map_13_Medieval Age.prefab](../../Assets/DownLoadAssets/Map/2D%20Maps%20-%20Age%20Battle%20Stages/Prefabs/Landscape/Map/Map_13_Medieval%20Age.prefab) |
| monster.golem | Combat | [Golem_Iron.prefab](../../Assets/DownLoadAssets/MonsterAsset/2D%20Minimal-EnemyMonster/EnemyMonster%202/Prefabs/Golem/Golem_Iron.prefab) |
| boss.v4 | Combat | [Golem_Iron.prefab](../../Assets/DownLoadAssets/MonsterAsset/2D%20Minimal-EnemyMonster/EnemyMonster%202/Prefabs/Golem/Golem_Iron.prefab) |
| skill.fireball | Combat | [FX_AOE_Fireball.prefab](../../Assets/DownLoadAssets/Eric%20VFX%20Studio/Game%20VFX%20-%20Stylized%20AOE%20Bundle/Prefabs/URP/FX_AOE_Fireball.prefab) |
| skill.hurricane | Combat | [FX_AirTornado_Loop.prefab](../../Assets/DownLoadAssets/Eric%20VFX%20Studio/Game%20VFX%20-%20Tornado%20Collection/Prefab/URP/FX_AirTornado_Loop.prefab) |
| skill.lightning | Combat | [lightning1_yellow.png](../../Assets/DownLoadAssets/Casual_VFX/Cartoon%20Casual%20VFX%20Pack/Textures/lightning1_yellow.png) |
| skill.freezing | Combat | [FX_SnowTornado_Loop.prefab](../../Assets/DownLoadAssets/Eric%20VFX%20Studio/Game%20VFX%20-%20Tornado%20Collection/Prefab/URP/FX_SnowTornado_Loop.prefab) |
| skill.dragon-breath | Combat | [fire2.png](../../Assets/DownLoadAssets/Casual_VFX/Cartoon%20Casual%20VFX%20Pack/Textures/fire2.png) |
| hit.basic | Combat | [hit5_white.png](../../Assets/DownLoadAssets/Casual_VFX/Cartoon%20Casual%20VFX%20Pack/Textures/hit5_white.png) |
| hit.triple | Combat | [hit1_purple.png](../../Assets/DownLoadAssets/Casual_VFX/Cartoon%20Casual%20VFX%20Pack/Textures/hit1_purple.png) |
| parry.marker | Combat | [magic_circle6_white.png](../../Assets/DownLoadAssets/Casual_VFX/Cartoon%20Casual%20VFX%20Pack/Textures/magic_circle6_white.png) |

## 특성 화면 아이콘 품질 기준

특성 화면의 통일감을 위해 노드 아이콘은 가능한 한 `Icon_ItemIcons/512` 또는 이름에
`_512`가 붙은 512px 스프라이트를 사용한다. 현재 적용한 512px 아이콘은 임포터의
텍스처 압축을 끄고, 원본 PNG는 수정하지 않는다. 폰트는
`Assets/CursorHunter/Progression/Runtime/Fonts/NanumGothic.ttf`(동적 48pt)를 사용하며,
`TraitScreenController`는 Canvas 픽셀 퍼펙트와 1.18배 글자 배율을 적용한다. 작은
원본 아이콘을 확대해 생기는 흐림은 새 매핑에 사용하지 않는다.

| 역할 | 적용 스프라이트 | 선택 이유 |
|---|---|---|
| 공격력 | `Icon_ItemIcons/512/ItemIcon_Weapon_Sword.png` | 공격·무기 의미가 즉시 읽힘 |
| 반경 | `Icon_ItemIcons/512/ItemIcon_Weapon_Shield.png` | 보호 범위와 외곽 반경을 연상 |
| 다중 클릭 | `Icon_ItemIcons/512/ItemIcon_Weapon_Bow.png` | 반복 발사·다중 타격을 표현 |
| 치명타 | `Icon_ItemIcons/512/ItemIcon_Star.png` | 강조 타격·확률 수치와 조합 |
| 젬 수집 | `Icon_ItemIcons/512/ItemIcon_Bag01.png` | 수집 기능을 가방으로 구분 |
| 보스 피해 | `Icon_ItemIcons/512/ItemIcon_Skull02.png` | 보스 전용 피해 가지를 구분 |

스킬 행의 첫 노드는 스킬마다 서로 다른 불·번개·방패·책·별 계열 아이콘을 배정하고,
후속 수치 노드는 검(공격력)·방패(범위)·타이머(쿨타임)로 통일한다. 전체 배열과 GUID는
[특성 UI 에셋 매핑](trait-ui-assets.md)과 [asset-map.json](asset-map.json)을 함께 갱신한다.

| 스킬 행 | UI 아이콘 | 경로 |
|---|---|---|
| 파이어볼 | 불꽃 | `IconMisc/Icon_Fire01_512.png` |
| 라이트닝 볼트 | 에너지 번개 | `Icon_ItemIcons/512/ItemIcon_Badge_Energy.png` |
| 프리즈 | 푸른 방패 | `IconMisc/Icon_Shield05_512.png` |
| 허리케인 | 하강 소용돌이 | `IconMisc/Skill1.png` |
| 메테오 | 망치 충돌 | `Icon_ItemIcons/512/ItemIcon_Weapon_Hammer.png` |
| 체인 라이트닝 | 번개 문양 | `IconMisc/Skill2.png` |
| 블리자드 | 얼음 방패 | `IconMisc/Icon_Shield01_512.png` |
| 아케인 버스트 | 청록 마법서 | `Icon_ItemIcons/512/ItemIcon_Book02_Teal.png` |
| 스타폴 | 큰 별 | `IconMisc/Icon_Star03_l.png` |
| 드래곤 브레스 | 강한 불꽃 | `IconMisc/Icon_Fire01_512.png` |

## 조정·추가 제작
- 다이아몬드: 현재 특성 화면은 `512/ItemIcon_Gem_Pentagon_Green.png`를 프로토타입
  아이콘으로 사용한다. 최종 명칭을 다이아몬드로 유지할 경우 흰 결정 외형으로
  교체하고 `TraitScreenController.gemstoneIcons[4]`만 바꾼다.
- 슬라임/고블린/오크/와이번/드래곤 본체: 현재 패키지의 이름 기반 탐색에서 직접 대응 본체를 확인하지 못했다. 아이콘을 본체로 취급하지 않는다. Dragonfly는 잠자리다. 임시 도형으로 전투를 구현하고 외형만 교체한다.
- 골렘: Iron은 철 골렘이다. 일반 골렘 임시 후보이며 v4 수정 골렘은 결정 외형을 따로 제작한다. 일반과 보스는 다른 프리팹으로 분리한다.
- v1/v2/v3/v5 보스: 본체 확정 전 실루엣 임시 프리팹 사용. 기획의 몬스터 이름과 ID는 유지한다.
- 프리징: SnowTornado는 회전 눈 효과라서 얼음 정지 연출의 재료 후보일 뿐이다. 정지 표식/서리 링을 조합하고 광역 판정과 분리한다.
- 라이트닝/브레스: 텍스처 재료를 찾았으며 완성 스킬 프리팹을 확인한 것은 아니다. 별도 파티클/라인 연출 제작이 필요하다.
- 성장 타격: 흰 점 → 황금 이중 링 → 보라 파편 → 청색 맥동 → 결정·화염. 링은 자체 단순 도형으로 만들 수 있다. Sword Trails/Ink Slash/Magic Slash의 휘두르기 연출은 배제한다.
- 맵: Landscape 후보를 16:9로 배치하고 HUD 안전 영역을 제외한다. 배경 건물·장식에 적이 가려지지 않도록 명도와 밀도를 낮춘다.
- UI: 특성 좌상단 Stat/Skill/Monster, 좌중단 해금 젬, 우상단 요약 그래프, 중앙
  정사각 노드 체인. 스탯·스킬은 세로 ScrollRect, 몬스터는 가로 ScrollRect로
  탐색하며, 실제 적용 에셋과 배열 순서는 [trait-ui-assets.md](trait-ui-assets.md)를
  기준으로 한다.
- 오디오·전용 커서·엔딩 아트는 이번 후보 목록에서 확정하지 않았다.

## 시각·성능 합류 점검
투사체/파티클은 피해 판정 주체가 아니다. 처치 효과 1,200개를 개별 장기 재생하지 않고 예산·풀·합성 연출을 적용한다. 자동 90타/초에서도 글자와 보스 파훼 표식이 읽히는지 확인한다.
이 목록은 에셋 적용 완료 목록과 구현 담당에게 전달하는 후보·수정 방향을 함께 기록한다. 현재
특성 화면에 실제로 연결한 스프라이트와 Monster1~20 순서 매핑은
[trait-ui-assets.md](trait-ui-assets.md)에 별도로 기록한다. 기획의 추가 검토 내용은
PDF가 아닌 이 문서에 유지한다.

## 젬스톤 표시·해금 매핑

젬스톤 슬롯은 보스 처치로 자동 추가하지 않는다. `Stat → 젬 수집`의 해금 노드를
구매하면 해당 색상 아이콘이 목록에 처음 나타나고, 이후 발견 노드 구매 횟수가 그
종류의 랜덤 등장 가중치를 올린다. 시작 가중치는 가넷 70, 토파즈 20, 자수정 8,
사파이어 3, 다이아몬드 1이며, 발견 강화는 각각 +5·+3·+2·+1이다. 해금된 종류만
가중치 합으로 100% 정규화한다. 노드 ID·비용·아이콘 순서는
[trait-ui-assets.md](trait-ui-assets.md)의 젬스톤 표가 단일 기준이다.
