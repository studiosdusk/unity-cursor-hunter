# 에셋 후보와 기획 매핑

파일 경로와 GUID를 확인한 후보 목록이다. 렌더링·애니메이션·머티리얼 호환성은 Unity에서 확인한다.
GUI Lobby 미리보기는 육안 확인했다. 둥근 카드·테두리 스타일을 활용하되 기존 로비의 재화·상점·패스·캐릭터 UI를 그대로 가져오지 않는다. Main은 텍스트 메뉴이고 젬스톤은 표시하지 않는다.

## 재사용 방식
원본 Assets/DownLoadAssets는 보존한다. 각 모듈 소유 폴더에 Variant 또는 시각 래퍼를 만든다. GUID는 원본 추적용이며 런타임 로더 구현이 아니다.
아래 링크의 에셋을 Unity에서 선택한 뒤 스프라이트 모드, pivot, 픽셀 크기, sorting, URP 머티리얼, emission/loop/stop을 점검한다.

| 기획 ID | 담당 | 확인된 후보 |
|---|---|---|
| gem.garnet | Progression | [Economy_Gem_01_Red.png](../../Assets/DownLoadAssets/IconPack/2D%20Minimal-IconPack/Icons/256/Economy_Gem_01_Red.png) |
| gem.topaz | Progression | [Economy_Gem_01_Yellow.png](../../Assets/DownLoadAssets/IconPack/2D%20Minimal-IconPack/Icons/256/Economy_Gem_01_Yellow.png) |
| gem.amethyst | Progression | [Economy_Gem_01_Purple.png](../../Assets/DownLoadAssets/IconPack/2D%20Minimal-IconPack/Icons/256/Economy_Gem_01_Purple.png) |
| gem.sapphire | Progression | [Economy_Gem_01_Blue.png](../../Assets/DownLoadAssets/IconPack/2D%20Minimal-IconPack/Icons/256/Economy_Gem_01_Blue.png) |
| ui.trait.node | Progression | [Button_Square01_White.prefab](../../Assets/DownLoadAssets/GUI-CasualFantasy/Prefabs/Prefabs_Component_Buttons/Button_Square01_White.prefab) |
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
