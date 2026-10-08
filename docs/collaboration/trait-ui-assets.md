> 2026-10-01 변경: 아래 v0.0.4 본문은 개편 전 기록입니다. 현재 JSON·노드 UI·몬스터 10종·유물·자동 반경/쿨타임·30초 규칙은 [새 구현 기준](game-information-refactor-2026-10-01.md)과 실제 balance-v2.json을 우선합니다. 기존 캡처는 현재 UI 검증 자료가 아닙니다.

# 특성 화면 데이터·에셋 매핑

특성 화면 프로토타입의 실제 적용 매핑이다. 원본 파일을 복제하지 않고
`Assets/DownLoadAssets`의 스프라이트를 `Main.unity`에 직접 참조한다. 화면 바탕은
전투 필드와 같은 `field_a.png`이며, 짙은 남색 셰이드를 한 겹 덧씌운다. 이 파일은
원본이 202×111 타일이므로 화면 전체에 늘려 그리지 않고 `Image.Type.Tiled`로 반복한다.
원본을 1920×1080으로 확대해 저장하거나 새 PNG로 대체하면 다시 흐려질 수 있으므로,
최종 전투 배경은 전투 맵의 `SpriteRenderer`/프리팹을 사용하고 이 타일은 보조 레이어로
남긴다.

## 현재 적용 기준 · v0.0.4 최신 확정안 · 2026-09-18

아래 표가 현재 `TraitCatalog.CreateRuntimeDemo()`와 `Main.unity` 직렬화 배열의 기준이다.
이전 버전의 상세 표는 에셋 후보를 추적하기 위한 기록으로 남겨 두며, 노드 수·ID·표시명은
아래 표를 우선한다.

| 탭 | 카테고리/행 | 노드 수 | 화면 동작 |
|---|---|---:|---|
| 스탯 | 공격력, 반경, 다중 클릭, 치명타, 젬 수집, 보스 공격력, 일반 필드 시간 | 10+11+5+8+5+5+10 + 젬 해금·발견 15 = 69 | 카테고리별 한 행, 가로 연결선 |
| 스킬 | 파이어볼, 라이트닝 볼트, 프리즈닝, 허리케인, 메테오, 드래곤 브레스 | 6×4 = 24 | 스킬별 한 행, 해금→피해→범위→쿨타임 |
| 몬스터 | Monster1~20 해금 | 20 | 한 행 순서 연결 |
| 몬스터 생산량 | Monster1~20 생산량 I~III | 60 | 해금 행과 분리한 한 행 |
| 전리품 | 일반 Monster1~20, 보스 v1~v5 | 125 | 랜덤 드롭 도감 + 조각 해금·4단계 강화 |
| 펫 | 펫 1 해금 + 영역/공격력/주기/영역 강화 | 5 | 자동 공격 보조 행 |

### 현재 런타임 배열의 실제 에셋

| 컨트롤러 필드/배열 | 인덱스 규칙 | 실제 에셋 |
|---|---|---|
| `gemstoneIcons` | 0 가넷, 1 토파즈, 2 자수정, 3 사파이어, 4 다이아몬드, 5 드래곤 젬 | `Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Red.png`, `...Yellow.png`, `...Purple.png`, `...Blue.png`, `...Green.png`, `...Gray.png` |
| `skillIcons` | 0 파이어볼, 1 라이트닝, 2 프리즈닝, 3 허리케인, 4 메테오, 5 드래곤 브레스 | `IconMisc/Icon_Fire01_512.png`, `Icon_ItemIcons/512/ItemIcon_Badge_Energy.png`, `IconMisc/Icon_Shield05_512.png`, `IconMisc/Skill1.png`, `Icon_ItemIcons/512/ItemIcon_Weapon_Hammer.png`, `IconPack/2D Minimal-IconPack/Icons/512/Misc_Dragon_01_Red.png` |
| `monsterIcons` | 0~19 = Monster1~20 | 지정 경로 `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites`의 Body 스프라이트 20개 |
| `lootIcons` | 0~19 일반 전리품, 20~24 보스 전리품 | `Icon_ItemIcons/512/ItemIcon_Bag02.png`, `ItemIcon_Coin.png`, `ItemIcon_Bag01.png`를 의미에 맞춰 반복 매핑 |
| `petIcons` | 0 펫 1 | `Icon_ItemIcons/512/ItemIcon_Fox.png` |

### 새 노드 ID와 공통 아이콘

- `stat.attack.01~10`은 512px 검(`ItemIcon_Weapon_Sword.png`)을 사용한다.
- `stat.radius.01~11`은 512px 방패(`ItemIcon_Weapon_Shield.png`)를 사용한다.
- `stat.multiClick.01~05`는 512px 활(`ItemIcon_Weapon_Bow.png`)을 사용한다.
- `stat.critical.01~08`은 512px 별(`ItemIcon_Star.png`)을 사용한다.
- `stat.gemstone.01~05`은 가방(`ItemIcon_Bag01.png`), `unlock/rate.<type>`은 해당
  `gemstoneIcons` 색상을 사용한다. `gem.dragon`은 Gray 오각 젬을 사용한다.
- `stat.boss.01~05`는 해골(`ItemIcon_Skull02.png`)을 사용한다.
- 스킬의 `.damage`, `.radius`, `.cooldown` 노드는 각각 검, 방패, 타이머 아이콘을
  공통으로 사용한다.
- `monster.production.*`는 해당 Monster 번호의 Body 스프라이트를 그대로 재사용한다.
- `loot.*`는 Bag02(획득물), Coin(수확량), Bag01(도감)으로 구분하고, `pet.*`는 Fox를
  사용한다. 아이콘별 회색 카드 테두리는 만들지 않고 노드 셀 사각 선만 유지한다.

## 현재 표시 수량

| 탭 | 표시 방식 | 노드 수 | 비고 |
|---|---|---:|---|
| 스탯 | 카테고리별 1행 체인 | 69 | 공격력 10, 반경 11, 다중 클릭 5, 치명타 8, 젬 수집 5, 보스 공격력 5, 일반 필드 시간 10, 젬 해금·발견 15 |
| 스킬 | 스킬별 1행 체인 | 24 | 6종 × 해금·피해·범위·쿨타임, 마지막 행은 드래곤 브레스 |
| 몬스터 | Monster1~20별 1행 체인 | 80 | 해금 20 + 몬스터별 생산량 3단계 |
| 전리품 | 일반·보스 도감 2행 | 125 | 해금 1개 + Lv1/2/3/4 강화, 조각 1/2/3/5/10 |
| 펫 | 1마리 강화 1행 | 5 | 자동 공격 영역·피해·주기 |

각 카테고리는 헤더와 노드 체인을 가진 한 행으로 렌더링하고, 인접 노드는 런타임
선으로 연결한다. 스탯·스킬은 세로 스크롤하며 몬스터 20개 체인은 가로 스크롤한다.
우측에는 선택한 노드의 설명·비용·선행 조건을 표시한다.

노드 비용은 가넷으로 고정하지 않는다. 상세 패널은 `TraitNodeDefinition.CostGemstoneId`를
읽어 해당 젬스톤 이름·보유량·비용을 보여주며, 테스트 무료 모드에서도 `원래 비용`의
젬스톤 이름을 숨기지 않는다. 공격력·반경·다중 클릭·치명타·젬 수집·일반 필드 시간은
구간별 젬스톤으로 이동하고, 스킬은 스킬별 전용 젬스톤, 몬스터 생산량은 해금 티어 젬스톤을
사용한다. 젬스톤 보유 패널과 비용 상세는 같은 `gemstoneIcons` 배열(가넷 Red, 토파즈 Yellow,
자수정 Purple, 사파이어 Blue, 다이아몬드 Green, 드래곤 Gray)을 재사용한다.

## 공통 UI 스프라이트

| 컨트롤러 필드 | 적용 에셋 | 용도 |
|---|---|---|
| `combatBackgroundSprite` | `Assets/DownLoadAssets/Map/2D Maps - Age Battle Stages/Sprite/Prop/a/field_a.png` (202×111, Repeat) | 전투와 동일한 필드 바탕. 런타임은 원본 크기로 타일링 |
| `panelSprite` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Frame/BasicFrame_SquareSolid01_Demo01.png` | 호환용 직렬화 참조. 주요 패널은 평면 색상 사용 |
| `tabSprite` / `tabFocusSprite` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/UI_Etc/TabMenu_Top_Demo_Bg.png` / `TabMenu_Top_Demo_Focus.png` | 탭 상태 |
| `nodeSprite` / `nodePurchasedSprite` | 레거시 샘플 | 런타임 미사용. 모든 노드는 평면 정사각 셀 |
| `nodeAccentSprite` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Frame/BasicFrame_SquareOutline02_White.png` | 예약 참조. 실제 선은 런타임 4면 Image |
| `gemIcon` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Red.png` | 폴백 젬 아이콘 |
| `gemstoneIcon` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Bag01.png` | 젬 수집 특성 |
| `lockIcon` / `checkIcon` | `IconMisc/Icon_Lock01.png` / `IconMisc/Icon_Check.png` | 잠금·구매 완료 |
| `backIcon` | `IconMisc/Icon_Arrow_Back.png` | 뒤로가기 |
| `attackIcon` | `Icon_ItemIcons/512/ItemIcon_Weapon_Sword.png` | 공격력 |
| `radiusIcon` | `Icon_ItemIcons/512/ItemIcon_Weapon_Shield.png` | 반경·범위 |
| `clickIcon` | `Icon_ItemIcons/512/ItemIcon_Weapon_Bow.png` | 다중 클릭 |
| `timerIcon` | `IconMisc/Icon_Timer.png` | 쿨타임 |
| `criticalIcon` | `Icon_ItemIcons/512/ItemIcon_Star.png` | 치명타 |
| `bossIcon` | `Icon_ItemIcons/512/ItemIcon_Skull02.png` | 보스 공격력 |

선택한 512px 아이콘 메타는 압축을 끄고, `NanumGothic.ttf`는 48pt 동적 폰트로
설정했다. 캔버스는 픽셀 퍼펙트 오버레이와 1.18배 UI 글자 크기를 사용한다.

### 화면 스타일

- 노드 셀은 `84×84` 정사각형이며 회색·원형·아이콘별 카드 테두리를 사용하지 않는다.
- 상태는 셀의 네 변 런타임 선(구매 완료 초록, 구매 가능 카테고리 색, 잠금 남색)으로만 구분한다.
- 연결선은 흰색에 카테고리 색상을 14% 섞은 `Image`이며 입력을 가로채지 않는다.
- 배경에는 전투 필드 텍스처와 `FieldShade` 남색 오버레이를 사용한다. 오버레이는
  `raycastTarget = false`다.

## 스탯 노드 아이콘 매핑

스탯은 카테고리별 한 행이며, 젬스톤 해금·발견 노드는 해당 보석의 실제 아이콘을
사용한다. 카탈로그에 노드를 추가하면 같은 카테고리의 기본 아이콘으로 안전하게
대체된다.

| 카탈로그 카테고리 | 노드 범위 | 아이콘 |
|---|---|---|
| `stat.attack` 공격력 | 공격력 1~100 (10개) | `attackIcon` · 512px 검 |
| `stat.radius` 반경 | 작은 원 → 영역 1~5 → 1/32 → 1/16 → 1/8 → 1/4 → 전체 (11개) | `radiusIcon` · 512px 방패 |
| `stat.multiClick` 다중 클릭 | 1회 → 더블 → 트리플 → 쿼드 → 자동 무한 (5개) | `clickIcon` · 512px 활 |
| `stat.critical` 치명타 | 0% → 5% → 10% → 20% → 30% → 50% → 75% → 100% (8개) | `criticalIcon` · 512px 별 |
| `stat.gemstone` 젬 수집 | 수집 5 + 해금·발견 15 (20개) | 수집은 가방, 해금·발견은 보석별 아이콘 |
| `stat.boss` 보스 공격력 | 보스 공격력 1~5 (5개) | `bossIcon` · 512px 해골 |
| `stat.fieldDuration` 일반 필드 시간 | 15초 → 20초 → 25초 → 30초 → 35초 → 40초 → 45초 → 50초 → 55초 → 60초 (10개) | `timerIcon` · 512px 타이머 |

### 현재 스탯 노드별 에셋 매핑

아래 ID와 아이콘 매핑이 현재 기획·카탈로그의 기준이다. 모든 Stat 노드는 같은
정사각형 셀과 카테고리별 아이콘을 사용하고, 젬스톤 해금·발견 노드만 해당
젬스톤 색상 아이콘으로 교체한다. 42개 스탯·8회/16회 클릭·이전 5단계 반경
표는 폐기된 캡처용 기록이며 현재 구현·기획에 사용하지 않는다.

| 노드 ID 범위 | 표시 순서 | 적용 에셋 |
|---|---|---|
| `stat.attack.01~10` | 공격력 1 → 3 → 6 → 10 → 16 → 24 → 35 → 50 → 70 → 100 | `Icon_ItemIcons/512/ItemIcon_Weapon_Sword.png` |
| `stat.radius.01~11` | 작은 원 → 영역 1~5 → 1/32 → 1/16 → 1/8 → 1/4 → 화면 전체 | `Icon_ItemIcons/512/ItemIcon_Weapon_Shield.png` |
| `stat.multiClick.01~05` | 1회 → 더블 → 트리플 → 쿼드 → 자동 무한 | `Icon_ItemIcons/512/ItemIcon_Weapon_Bow.png` |
| `stat.critical.01~08` | 0% → 5% → 10% → 20% → 30% → 50% → 75% → 100% | `Icon_ItemIcons/512/ItemIcon_Star.png` |
| `stat.gemstone.01~05` | 접촉 클릭 → 접촉 자동 → 자석 → 자석 반경 확대 1~2 → 화면 전체 자동 | `Icon_ItemIcons/512/ItemIcon_Bag01.png` |
| `stat.gemstone.unlock.*` / `stat.gemstone.rate.*.01~02` | 토파즈·자수정·사파이어·다이아몬드·드래곤 젬 해금·발견 I/II | 해당 `gemstoneIcons` 색상 아이콘 |
| `stat.boss.01~05` | 보스 공격력 1 → 2 → 3 → 4 → 5 | `Icon_ItemIcons/512/ItemIcon_Skull02.png` |

스킬 행의 해금 노드는 실제 스킬 아이콘을 사용하고 후속 피해량·범위·쿨타임 노드는
검·방패·타이머 공통 아이콘을 사용한다. 몬스터 행은 `monsterIcons[0..19]`를
Monster1~Monster20에 순서대로 매핑하고, 생산량 I/II/III은 같은 Body 스프라이트를
재사용한다.

## 젬스톤 해금·랜덤 드롭 UI

젬스톤은 보스 처치로 자동 추가하지 않고 **Stat → 젬 수집** 행의 해금 노드로
추가한다. 게임 시작에는 가넷만 표시하며, 토파즈·자수정·사파이어·다이아몬드·
드래곤 젬은 해당 해금 노드를 구매할 때까지 이름·아이콘·보유량을 숨긴다.

해금 노드는 `토파즈 → 자수정 → 사파이어 → 다이아몬드 → 드래곤 젬` 순서의
선행 조건을 가진다. 해금된 보석만 랜덤 드롭 표에 참여하며, 발견 I·II 노드는
해당 보석의 가중치만 올린 뒤 전체 합을 100%로 다시 정규화한다.

`W = min(최대 가중치, 기본 가중치 + 발견 강화 수 × 강화당 가중치)`  
`P = W / 해금된 보석의 W 합 × 100`

| 젬스톤 | 해금 Stat 노드 | 기본 가중치 | 강화 1회 | 확률 강화 노드 | 적용 아이콘 |
|---|---|---:|---:|---:|---|
| 가넷 | 시작 | 70 | 0 | 없음 | `Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Red.png` |
| 토파즈 | `stat.gemstone.unlock.topaz` | 20 | +5 | 2 | `Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Yellow.png` |
| 자수정 | `stat.gemstone.unlock.amethyst` | 12 | +4 | 2 | `Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Purple.png` |
| 사파이어 | `stat.gemstone.unlock.sapphire` | 8 | +3 | 2 | `Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Blue.png` |
| 다이아몬드 | `stat.gemstone.unlock.diamond` | 4 | +2 | 2 | `Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Green.png` |
| 드래곤 젬 | `stat.gemstone.unlock.dragon` | 2 | +1 | 2 | `Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Gray.png` |

Stat의 젬 수집 행은 접촉 클릭 → 접촉 자동 → 자석 → 자석 반경 확대 1~2 →
화면 전체 자동 수집의 5개 상태를 가진다. 이 수집 방식은 젬 종류 해금·발견
확률 노드와 같은 행에 배치하지만 서로 다른 노드 ID로 저장한다.

각 전투·정산·특성 화면은 현재 해금된 젬스톤만 표시하고, Main 최상위 메뉴에는
보유 젬스톤 HUD를 표시하지 않는다. `TraitScreenController.TryRollGemstoneDrop`은
Combat이 전달한 0~1 난수로 해금된 후보만 결정론적으로 선택한다.

## 스킬 아이콘 배열

`TraitScreenController.skillIcons` 배열 인덱스는 현재 6개 스킬 행 순서와 같다.
첫 노드는 스킬별 아이콘을 사용하고, 피해량·범위·쿨타임 후속 노드는 공통
`attackIcon`, `radiusIcon`, `timerIcon`을 사용한다.

| 순서 | 스킬 행 | 적용 아이콘 |
|---:|---|---|
| 1 | 파이어볼 | `IconMisc/Icon_Fire01_512.png` |
| 2 | 라이트닝 볼트 | `Icon_ItemIcons/512/ItemIcon_Badge_Energy.png` |
| 3 | 프리즈닝 | `IconMisc/Icon_Shield05_512.png` |
| 4 | 허리케인 | `IconMisc/Skill1.png` |
| 5 | 메테오 | `Icon_ItemIcons/512/ItemIcon_Weapon_Hammer.png` |
| 6 | 드래곤 브레스 | `IconPack/2D Minimal-IconPack/Icons/512/Misc_Dragon_01_Red.png` |

스킬 아이콘은 현재 UI용 시각 매핑이며 실제 VFX와 피해 판정은 Combat 모듈이 소유한다.
같은 스킬의 해금·피해량·범위·쿨타임 노드는 모두 해당 스킬 행의 기본 아이콘을
재사용한다. 노드의 차이는 Key/Value 텍스트와 상태 색상으로만 전달한다. 따라서
라이트닝 볼트의 세부 강화가 파이어볼 아이콘으로 바뀌거나 공용 검·방패 아이콘으로
섞이지 않는다.

## 실행 화면 캡처

Unity 6000.3.13f1 `Main.unity` Game View에서 캡처한다. 기본 캡처는 새 게임
(가넷만 표시) 기준이며, 젬스톤 해금 예시는 Stat 행의 해금 노드를 구매한 상태로
별도 저장한다. 최신 캡처는 잠금·해금·확률 표시가 반영된 후 다시 생성한다.

| 탭 | 캡처 파일 |
|---|---|
| 스탯 | [trait-stat.png](./screenshots/trait-stat.png) |
| 스킬 | [trait-skill.png](./screenshots/trait-skill.png) |
| 몬스터 | [trait-monster.png](./screenshots/trait-monster.png) |
| 젬스톤 해금 예시 | [trait-gemstone-unlocked.png](./screenshots/trait-gemstone-unlocked.png) |

## 몬스터 아이콘 배열

`TraitScreenController.monsterIcons`의 0번 인덱스는 `Monster1`, 19번 인덱스는
`Monster20`에 매핑한다.
모든 이미지는 사용자가 지정한
`Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2` 아래의
`ResourcesData/Sprites`에서 가져온 `Body.png` 또는 동일 역할의 `Body_Fire.png`다.

| 노드 | 원본 변형 | 적용 스프라이트 |
|---|---|---|
| Monster1 | Alien_Vampire | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Alien/Alien_Vampire/Body.png` |
| Monster2 | Alien_Wizard | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Alien/Alien_Wizard/Body.png` |
| Monster3 | Ant_Honey | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Ant/Ant_Honey/Body.png` |
| Monster4 | Ant_Jelly | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Ant/Ant_Jelly/Body.png` |
| Monster5 | Ant_Worker | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Ant/Ant_Worker/Body.png` |
| Monster6 | Ant_Yellow | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Ant/Ant_Yellow/Body.png` |
| Monster7 | Bat_Cave | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Bat/Bat_Cave/Body.png` |
| Monster8 | Bat_Pig | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Bat/Bat_Pig/Body.png` |
| Monster9 | Bat_Skull | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Bat/Bat_Skull/Body.png` |
| Monster10 | Bee_Devil | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Bee/Bee_Devil/Body.png` |
| Monster11 | Bee_Honey | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Bee/Bee_Honey/Body.png` |
| Monster12 | Bee_Hornet | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Bee/Bee_Hornet/Body.png` |
| Monster13 | Bud_Bomb | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Bud/Bud_Bomb/Body.png` |
| Monster14 | Bud_Fire | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Bud/Bud_Fire/Body_Fire.png` |
| Monster15 | Bud_Flower | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Bud/Bud_Flower/Body.png` |
| Monster16 | Cactus_Green | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Cactus/Cactus_Green/Body.png` |
| Monster17 | Cactus_Snow | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Cactus/Cactus_Snow/Body.png` |
| Monster18 | Dragonfly_Armor | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Dragonfly/Dragonfly_Armor/Body.png` |
| Monster19 | Dragonfly_Bot | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Dragonfly/Dragonfly_Bot/Body.png` |
| Monster20 | Dragonfly_Green | `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2/ResourcesData/Sprites/Dragonfly/Dragonfly_Green/Body.png` |

몬스터 표시명과 원본 변형명은 분리되어 있으므로, 이후 최종 네이밍을 정해도
배열 순서와 전투 ID는 유지한다.

## 한국어 폰트

`uiFont`에는 `Assets/CursorHunter/Progression/Runtime/Fonts/NanumGothic.ttf`를
할당했다. Unity 6의 LegacyRuntime 폰트는 한글 글리프를 제공하지 않으므로, 이 참조가
있어야 런타임으로 생성되는 한국어 UI가 깨지지 않는다. 커스텀 씬에서 폰트를 비워도
영문 카탈로그는 LegacyRuntime으로 동작하며, 한국어 배포 화면에서는 프로젝트 폰트를
비우지 않는다.

## 데이터와 에셋의 경계

`TraitCatalog`는 ID·한국어 표시명·설명·비용·보스 티어만 소유한다. 화면 아이콘은
`Main.unity`의 배열 참조로 주입하므로, 저장 데이터나 전투 코드가 UI 오브젝트를
직접 수정하지 않는다. 커스텀 카탈로그가 기본 배열보다 긴 경우 배열 범위를 벗어난
노드는 해당 카테고리 아이콘으로 안전하게 대체된다.

## 현재 특성 요약 팝업

하단 `전체 정보` 버튼은 `TraitScreenController`가 런타임으로 생성한다. 팝업 내부는
`현재 전투 적용값 → 탭 → 카테고리 → 항목 (Key) | 현재 값 (Value)` 순서의 2열 표다.
스킬 카테고리에서는 해금·피해량·범위·쿨타임을, 몬스터 카테고리에서는 해금·생산량
I/II/III을 같은 표에 기록한다. 구매하지 않은 노드는 Value 열에 `미적용`, `잠김`,
`테스트에서 구매 가능` 중 하나를 표시하고, 해금된 젬스톤은 보유량과 정규화된 등장
확률을 함께 표시한다. 긴 목록은 기존 ScrollRect로 세로 스크롤한다.

## 전투 HUD와 몬스터 HP 바

일반·보스 Field 우측 상단의 `전체 정보`는 별도 아이콘 에셋을 복제하지 않고
`TraitScreenController`가 활성 Canvas에 만든 전역 Key/Value 오버레이를 호출한다.
전투 HUD의 버튼 배경은 기존 Casual Fantasy 패널 색상 규칙을 따르며, 표시 내용은
특성 화면과 동일하다. 일반 몬스터 HP 바는 기본적으로 숨긴다. 전투 프리팹의
`showHealthBar`를 켜면 `MonsterHealthBarView`가 공유 흰색 1px Sprite로 표시하고,
HP 비율에 따라 초록·노랑·빨강 Fill 색상을 바꾼다.
