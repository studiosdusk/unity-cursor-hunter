> 현재 전투 스킬은 [자동 스킬 기획서](../skill-design.md)의 토네이도·헤드샷·벼락·화염 배기·매직볼·레이저다. 각 스킬의 컨트롤러와 전투 오브젝트는 프로젝트 소유 프리팹이다.

| 현재 스킬 | 컨트롤러 | 전투 오브젝트 | 시각 원본 |
|---|---|---|---|
| `skill.tornado` | [TornadoSkillController.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/TornadoSkillController.prefab) | [TornadoObject.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/TornadoObject.prefab) | [FX_SnowTornado_Loop.prefab](../../Assets/DownLoadAssets/Eric%20VFX%20Studio/Game%20VFX%20-%20Tornado%20Collection/Prefab/URP/FX_SnowTornado_Loop.prefab) |
| `skill.headshot` | [HeadshotSkillController.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/HeadshotSkillController.prefab) | [HeadshotEffect.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/HeadshotEffect.prefab) | [FX_Aim_Explode_Visual.prefab](../../Assets/DownLoadAssets/Eric%20VFX%20Studio/Game%20VFX%20-%20Cartoon%20Skill%20Collection%20Vol2/Prefabs/URP/FX_Aim_Explode_Visual.prefab) |
| `skill.thunder` | [ThunderSkillController.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/ThunderSkillController.prefab) | [ThunderEffect.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/ThunderEffect.prefab) | [FX_Cartoon Thunder_Visual.prefab](../../Assets/DownLoadAssets/Eric%20VFX%20Studio/Game%20VFX%20-%20Cartoon%20Skill%20Collection%20Vol2/Prefabs/URP/FX_Cartoon%20Thunder_Visual.prefab) |
| `skill.flame_exhaust` | [FlameExhaustSkillController.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/FlameExhaustSkillController.prefab) | [FlameExhaustProjectile.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/FlameExhaustProjectile.prefab), [FlameExhaustHitEffect.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/FlameExhaustHitEffect.prefab) | [FX_MagicSlash02_Fire.prefab](../../Assets/DownLoadAssets/Eric%20VFX%20Studio/Game%20VFX%20-%20Magic%20Slash%20Collection/Prefabs/URP/FX_MagicSlash02_Fire.prefab) |
| `skill.magic_ball` | [MagicBallSkillController.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/MagicBallSkillController.prefab) | [MagicBallObject.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/MagicBallObject.prefab) | [Magic_Sphere_blue_Visual.prefab](../../Assets/DownLoadAssets/118%20sprite%20effects%20bundle/25%20sprite%20effects/_prefabs/Magic_Sphere_blue_Visual.prefab) |
| `skill.laser` | [LaserSkillController.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/LaserSkillController.prefab) | [LaserEffect.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/LaserEffect.prefab) | [Lazer_normal_Visual.prefab](../../Assets/DownLoadAssets/118%20sprite%20effects%20bundle/15%20effects/_prefabs/Lazer_normal_Visual.prefab) |
| `skill.pet_baby_dragon` | [PetSkillController.prefab](../../Assets/CursorHunter/Combat/Prefabs/Skills/PetSkillController.prefab) | Main 씬의 `Spine GameObject (BabyDragon) (1)`과 자식 고리 | [Explosion_Effect_Blue.prefab](../../Assets/DownLoadAssets/Casual_VFX/Cartoon%20Casual%20VFX%20Pack/Prefabs/Explosion/Explosion_Effect_Blue.prefab) |

펫은 Main의 BabyDragon 씬 인스턴스와 그 자식 [MagicCircle1_Green.prefab](../../Assets/DownLoadAssets/Casual_VFX/Cartoon%20Casual%20VFX%20Pack/Prefabs/MagicCircle/MagicCircle1_Green.prefab)을 그대로 사용한다. 명중 연출은 지정한 파란 폭발 원본을 복제해 컨트롤러 풀에서 한 번 재생한다. 자세한 동작은 [펫 스킬 문서](../pet-skill-design.md)를 따른다.

> 2026-10-01 변경: 아래 v0.0.4 본문은 개편 전 기록입니다. 기존 캡처는 현재 UI 검증 자료가 아닙니다.

# 에셋 후보와 기획 매핑

파일 경로와 GUID를 확인한 목록이다. 렌더링·애니메이션·머티리얼 호환성은 Unity에서
확인한다. GUI Lobby 미리보기는 후보 확인에만 사용했다. 특성 화면은 둥근 카드나 회색
프레임을 사용하지 않고 런타임 정사각 셀·색상 선으로 구성한다. Main 최상위 메뉴에는
젬스톤을 표시하지 않으며, 게임 시작 후 특성·전투·정산 화면에는 해금된 종류만 표시한다.

## 재사용 방식
원본 Assets/DownLoadAssets는 기본적으로 보존한다. 각 모듈 소유 폴더에 Variant 또는 시각 래퍼를 만든다. GUID는 원본 추적용이며 런타임 로더 구현이 아니다.
2026-10-16 전투의 박쥐·전갈·눈요정·거미·골렘·봉오리·해골군주는
`Combat/Art/MonsterDeath`의 반복하지 않는 Dead 클립을 사용한다. 각 종의
`Combat/Prefabs/Monsters/Visuals` 프리팹 Variant가 Animator Override Controller로
해당 클립만 교체하며, MonsterDefinition은 이 Variant를 참조한다.
2026-10-05 사용자 요청에 따라 예외적으로 `EnemyMonster 2/Animations`의 17종 Animator Controller에 Walker와 같은 전이·파라미터를 직접 적용했다. 각 종의 클립, 컨트롤러 GUID, 50개 프리팹 연결은 유지한다.
아래 링크의 에셋을 Unity에서 선택한 뒤 스프라이트 모드, pivot, 픽셀 크기, sorting, URP 머티리얼, emission/loop/stop을 점검한다.

| 기획 ID | 담당 | 특성 화면 적용 에셋 |
|---|---|---|
| gem.garnet | Progression | [ItemIcon_Gem_Pentagon_Red.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Red.png) |
| gem.topaz | Progression | [ItemIcon_Gem_Pentagon_Yellow.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Yellow.png) |
| gem.amethyst | Progression | [ItemIcon_Gem_Pentagon_Purple.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Purple.png) |
| gem.sapphire | Progression | [ItemIcon_Gem_Pentagon_Blue.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Blue.png) |
| gem.diamond | Progression | [ItemIcon_Gem_Pentagon_Green.png](../../Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Green.png) *(프로토타입 임시 색상)* |
| ui.trait.node | Progression | 런타임 4면 선 + 평면 Image (`TraitScreenController.cs`) |
| ui.trait.drag-cursor | Progression | [UI_Common_Hand_01.png](../../Assets/DownLoadAssets/IconPack/2D%20Minimal-IconPack/Icons/128/UI_Common_Hand_01.png) — 드래그 중 손 모양 커서 |
| ui.trait.tabs | Progression | [TabMenu_Top_White.prefab](../../Assets/DownLoadAssets/GUI-CasualFantasy/Prefabs/Prefabs_Component_UI_Etc/TabMenu_Top_White.prefab) |
| ui.boss.hp | Combat | [StatusBar_White.prefab](../../Assets/DownLoadAssets/GUI-CasualFantasy/Prefabs/Prefabs_Component_UI_Etc/StatusBar_White.prefab) |
| field.normal | Combat | [Map_3_Jungle Age.prefab](../../Assets/DownLoadAssets/Map/2D%20Maps%20-%20Age%20Battle%20Stages/Prefabs/Landscape/Map/Map_3_Jungle%20Age.prefab) |
| field.boss | Combat | [Map_13_Medieval Age.prefab](../../Assets/DownLoadAssets/Map/2D%20Maps%20-%20Age%20Battle%20Stages/Prefabs/Landscape/Map/Map_13_Medieval%20Age.prefab) |
| monster.shared-root | Combat | [MonsterRoot.prefab](../../Assets/CursorHunter/Combat/Prefabs/Monsters/MonsterRoot.prefab) |
| monster.behavior-profile.basic | Data | [Basic.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Basic.asset) — 기본 이동 프로필 |
| monster.behavior-profile.fast | Data | [Fast.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Fast.asset) |
| monster.behavior-profile.high-health | Data | [HighHealth.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/HighHealth.asset) |
| monster.behavior-profile.small | Data | [Small.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Small.asset) |
| monster.behavior-profile.large-slow | Data | [LargeSlow.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/LargeSlow.asset) |
| monster.behavior-profile.circular | Data | [Circular.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Circular.asset) |
| monster.behavior-profile.vanish | Data | [Vanish.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Vanish.asset) |
| monster.behavior-profile.shield | Data | [Shield.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Shield.asset) |
| monster.behavior-profile.teleport | Data | [Teleport.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Teleport.asset) |
| monster.behavior-profile.healer | Data | [Healer.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Healer.asset) |
| monster.behavior-profile.resurrector | Data | [Resurrector.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Resurrector.asset) |
| monster.behavior-profile.giant | Data | [Giant.asset](../../Assets/CursorHunter/Data/Resources/MonsterBehaviorProfiles/Giant.asset) |
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

| 역할 | 적용 스프라이트 | 번호 |
|---|---|---|
| 공격력 | `ItemIcon_Weapon_Sword.png` | AS-09904 |
| 공격 반경 | `UI_ETC_Target_02_Blue.png` | AS-12757 |
| 공격 간격 | `CardFrame01_Demo_Slider_Icon.png` | AS-09635 |
| 치명타 | `Icon_Fire02.png` | AS-09982 |
| 젬 수집 | `ItemIcon_Bag01.png` | 기존 매핑 유지 |
| 보스 피해 배수 | `Icon_Skull03.png` | AS-10024 |
| 일반 필드 시간 | `Icon_Timer.png` | AS-10047 |

특성 화면의 노드 아이콘은 스킬별로 통일한다. 지정되지 않은 스킬 중 허리케인은 바람을
연상시키는 날개, 메테오는 화염 충돌 아이콘으로 구성했다. 전체 배열과 GUID는
[특성 UI 에셋 매핑](trait-ui-assets.md)과 [asset-map.json](asset-map.json)을 함께 갱신한다.

| 스킬 행 | UI 아이콘 | 번호 |
|---|---|---|
| 파이어볼 | `Misc_Fire_01_Red.png` | AS-12435 |
| 라이트닝 볼트 | `Stat_Skill_01.png` | AS-12514 |
| 빙결 | `Economy_Gem_07_Blue.png` | AS-12140 |
| 허리케인 | `Misc_Wing_01.png` (바람 대용) | AS-12485 |
| 메테오 | `Skill1.png` (화염 충돌 대용) | AS-10050 |
| 드래곤 브레스 | `Misc_Dragon_01_Red.png` | AS-12429 |

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

가넷은 기본 해금이다. 나머지 젬스톤은 해당 종류를 드롭하는 몬스터를 해금하면
전투 데이터에서 공급 상태가 열린다. 연구 트리에서는 새 종류를 처음 공급하는 몬스터
노드의 우측 상단에 젬스톤 아이콘을 표시한다.

| 몬스터 | 새로 열리는 젬스톤 |
|---|---|
| 여왕벌 (monster.02) | 토파즈 |
| 가면유령 (monster.04) | 자수정 |
| 눈요정 (monster.06) | 사파이어 |
| 골렘 (monster.08) | 다이아몬드 |
| 봉오리 (monster.09) | 드래곤 젬 |
