> 현재 화면과 노드 수는 이 문서의 "현재 성장 UI" 절 및 실행 데이터 `progression-config.en.json`을 기준으로 한다. 아래 v0.0.4 표와 캡처는 과거 기록이다.

## 현재 성장 UI · 2026-10-21

`Main.unity`의 런타임 UI는 스탯 43개, 시작 펫과 보스 단계별 스킬 5개의 배우기 노드,
몬스터 40개 등 89개 노드를 전체 화면 단일 연구 보드로 보여 준다. 보스 단계 1~5에
스킬 한 종류씩 배정한다. 보스 단계를 만족한 뒤 연구 시작점을 구매해야 해당 스킬 노드가
나타나며, 가넷 40개로 배우면 런 시작 스냅샷의 `enabled`로 전달된다. 전투 JSON의 헤드샷
정의는 유지하지만 현재 성장 트리에는 표시하지 않는다.
수치·비용·보스 조건은 현재 `progression-config.en.json`을 기준으로 한다.

가운데 `stat.attack.01` 시작 노드에서 상·하·좌·우로 기본 스탯을 배치하고, 바깥쪽으로
낮은 티어와 비용부터 점차 어려운 노드를 놓는다. 보드는 가로·세로가 연결된 정사각 그리드며,
모든 연결선은 동서남북으로만 뻗는다. 재화 부족은 주홍색, 선행/보스 잠금은 자홍색,
구매 가능은 금색, 구매 완료는 청록색 테두리와 배지로 구분한다.

일반 진행에서는 전체 89개 노드를 한꺼번에 표시하지 않는다. 시작점, 이미 구매한 노드,
현재 보스 단계와 구매한 선행 노드를 모두 충족해 다음 선택지가 된 노드만 보드에 배치한다.
다음에 선택할 수 있지만 아직 구매하지 않은 항목은 자물쇠 아이콘과 상태 배지로 표시하고,
아직 단계나 선행 조건을 충족하지 못해 선택할 수 없는 미래 노드는 숨긴다. 테스트 전체 해금
모드에서는 모든 노드를 표시한다.

노드에 포인터를 올리면 설명·수치·필요 재화·보유량·선행 조건·구매/강화 상태를 보여 주는
말풍선이 나타나고, 가능한 경우 그 자리에서 해금/강화하거나 환불 초기화할 수 있다.
호버 확대, 툴팁 진입 애니메이션, 구매 후 사각 펄스·파편 이펙트를 쓴다. 드래그 이동,
확대·축소와 화면 맞춤 컨트롤은 HUD로 겹쳐 표시해 그래프 공간을 줄이지 않는다.

배경은 Casual GUI의 `Background_00.png` 성 풍경을 낮은 밝기로 깔고, 중성 청회색 막으로
채도를 낮춘 뒤 남색 세로 그라데이션과 가장자리 비네팅을 얹어 아이콘·연결선 대비를
확보한다. 설명 말풍선과 HUD는
`BasicFrame_SquareSolid01_Demo01.png`를 9-slice 패널로 재사용한다. 이전 본문의 탭별 행,
고정 우측 상세 패널, 탭별 스크린샷은 과거 레이아웃 기록이다.

일반 몬스터 10종의 이름과 실제 전투 특성은 아래 표를 기준으로 한다. 잠금이 풀린
몬스터 중 새 젬스톤을 공급하는 종은 아이콘 우측 상단에 해당 젬스톤을 표시한다.

| ID | 이름 | 실제 전투 특성 | 처음 공급하는 젬스톤 |
|---|---|---|---|
| monster.01 | 그루터기 | 기본 몬스터 | 가넷 (시작 공급원) |
| monster.02 | 여왕벌 | 시각·피격 영역 0.62배 | 토파즈 |
| monster.03 | 독버섯 | 이동 속도 1.8배 | — |
| monster.04 | 가면유령 | 4초마다 1.2초 은신 | 자수정 |
| monster.05 | 갑옷병사 | 4.5초마다 1.7초간 피해 65% 감소 | — |
| monster.06 | 눈요정 | 3.5초마다 순간이동 | 사파이어 |
| monster.07 | 벌집 | 체력 2.5배 | — |
| monster.08 | 골렘 | 시각 크기 2배, 피격 영역 1.8배 | 다이아몬드 |
| monster.09 | 봉오리 | 3.5초마다 반경 2.75 내 아군 체력 20% 회복 | 드래곤 젬 |
| monster.10 | 판도라 상자 | 4초마다 반경 3.5 내 쓰러진 아군을 체력 30%로 부활 | — |

# 특성 화면 데이터·에셋 매핑

특성 화면의 실제 적용 매핑이다. 원본 파일을 복제하지 않고
`Assets/DownLoadAssets`의 스프라이트를 `Main.unity`에 직접 참조한다. 화면 바탕은
성 풍경 `Background_00.png`를 전체 화면에 배치하고, 푸른 회색 색조·저채도 막·반투명
남색 그라데이션·가장자리 비네팅을 런타임으로 덮는다. 해당 원본은 2048×1152이며
번호표의 `AS-08066`이다.

## 과거 데이터 기록 · v0.0.4 · 2026-09-18

아래 표는 개편 전 `TraitCatalog.CreateRuntimeDemo()` 데이터 기록이며 현재 런타임 기준이
아니다. 최신 설정은 위의 현재 UI/특성 표와 실제 `progression-config.en.json`,
`MonsterDefinition` 에셋을 따른다.

| 탭 | 카테고리/행 | 노드 수 | 화면 동작 |
|---|---|---:|---|
| 스탯 | 공격력, 반경, 다중 클릭, 치명타, 젬 수집, 보스 공격력, 일반 필드 시간 | 10+11+5+8+5+5+10 + 젬 해금·발견 15 = 69 | 카테고리별 한 행, 가로 연결선 |
| 스킬 | 파이어볼, 라이트닝 볼트, 프리즈닝, 허리케인, 메테오, 드래곤 브레스 | 6×4 = 24 | 스킬별 한 행, 해금→피해→범위→쿨타임 |
| 몬스터 | Monster1~20 해금 | 20 | 한 행 순서 연결 |
| 몬스터 생산량 | Monster1~20 생산량 I~III | 60 | 해금 행과 분리한 한 행 |
| 전리품 | 일반 Monster1~20, 보스 v1~v5 | 125 | 랜덤 드롭 도감 + 조각 해금·4단계 강화 |
| 펫 | 펫 1 해금 + 영역/공격력/주기/영역 강화 | 5 | 자동 공격 보조 행 |

### 공용 젬스톤 아이콘 저장소 (2026-10-28)

젬스톤 이미지는 `Assets/CursorHunter/Data/Resources/GameData/GemstoneIconCatalog.asset`에서
관리한다. 각 항목은 고정 `gemstoneId`, 실제 `Sprite` 참조, 참고용 프로젝트 `assetPath`를
저장한다. 원본 이미지를 수정하거나 복제하지 않으며, 빌드에서도 Sprite 참조로 가져온다.
Inspector에서 Icon을 바꾸면 경로도 갱신된다.

```csharp
using CursorHunter.Data;

image.sprite = GemstoneIconCatalog.LoadDefault().GetIcon("gem.topaz");
```

등록 ID는 `gem.garnet`, `gem.topaz`, `gem.amethyst`, `gem.sapphire`, `gem.diamond`,
`gem.dragon`이며 차례대로 Red, Yellow, Purple, Blue, Green, Gray 이미지를 사용한다.
존재하지 않는 ID는 `GetIcon`이 null을 반환한다. 경로는 `TryGetEntry(id, out var entry)`의
`entry.AssetPath`에서 조회한다. 에디터 경로 문자열을 런타임 이미지 로딩에 사용하지 않는다.

Main의 `InGameObjects/GemStone/Icon`에는 `GemstoneIconView`를 연결하고 가넷으로 설정했다.
Inspector의 Gemstone Id 또는 `SetGemstone("gem.topaz")`로 종류를 바꾼다. 에디터에서
미리 보려면 컴포넌트 메뉴의 `Refresh Gemstone Icon`을 실행한다.
보유 젬스톤 목록·비용·몬스터 배지도 같은 저장소를 우선 조회한다.
기존 `gemstoneIcons` 배열은 저장소에 해당 항목이 없는 구형/개별 씬의 대체용으로만 유지한다.
Combat에서는 Data를 직접 참조하지 않고 App UI에서 아이콘을 연결한다.

### 현재 런타임 아이콘의 실제 에셋

| 컨트롤러 필드/배열 | 인덱스 규칙 | 실제 에셋 |
|---|---|---|
| `GemstoneIconCatalog` | `gem.garnet`, `gem.topaz`, `gem.amethyst`, `gem.sapphire`, `gem.diamond`, `gem.dragon` | `Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Red.png`, `...Yellow.png`, `...Purple.png`, `...Blue.png`, `...Green.png`, `...Gray.png` |
| `skillIcons` | 0~5 = 토네이도·헤드샷·벼락·화염 배기·마력 구체·레이저 | `Main.unity`에 매핑된 현재 스킬별 아이콘 |
| `monsterIcons` | 0~9 = monster.01~monster.10 | 아래 몬스터 아이콘 배열에 표시한 AS 번호의 Body 스프라이트 |
| `petIcon` / `petIcons` | 베이비 드래곤 노드 아이콘, 단일 아이콘 우선 | AS-13610 `Ghost/Ghost_Baby/Body.png` |

### 새 노드 ID와 공통 아이콘

- `stat.attack.01~10`은 AS-09904 검(`ItemIcon_Weapon_Sword.png`)을 사용한다.
- `stat.radius.01~11`은 AS-12757 표적(`UI_ETC_Target_02_Blue.png`)을 사용한다.
- `stat.cooldown.01~05` 공격 간격 노드는 AS-09635(`CardFrame01_Demo_Slider_Icon.png`)를 사용한다.
- `stat.critical.01~08`은 AS-09982(`Icon_Fire02.png`)를 사용한다.
- `stat.boss.01~05`는 AS-10024 해골(`Icon_Skull03.png`)을 사용한다.
- 현재 전투 스킬 6종은 `skillIcons[0..5]`, 베이비 드래곤은 `petIcon`을 사용한다.
- `monster.production.*`는 해당 Monster 번호의 Body 스프라이트를 그대로 재사용한다.
- 아이콘별 회색 카드 테두리는 만들지 않고 노드 셀 사각 선만 유지한다.

## 현재 표시 수량

| 탭 | 표시 방식 | 노드 수 | 비고 |
|---|---|---:|---|
| 스탯 | 연결된 연구 보드 | 43 | 공격력 10, 반경 11, 공격 간격 5, 치명타 8, 일반 필드 시간 4, 보스 피해 배수 5 |
| 스킬 | 연결된 연구 보드 | 7 | 현재 전투 스킬의 배우기 노드 |
| 몬스터 | 연결된 연구 보드 | 40 | 몬스터 10종의 해금과 생산량 강화 3단계 |

세 영역은 전체 화면의 연결된 보드 안에서 표시한다. 각 노드는 동서남북의 선으로 연결되고,
마우스 드래그와 휠로 이동·확대·축소한다. 선택한 노드의 설명과 비용은 보드 안의 고정
상세 패널에 표시한다.

노드 비용은 화폐 ID에 따라 해당 젬스톤 이름·보유량·비용을 보여준다. 테스트 무료 모드에서도
원래 비용은 표시하며, 사용 가능한 젬스톤은 `GemstoneIconCatalog`(가넷 Red, 토파즈 Yellow,
자수정 Purple, 사파이어 Blue, 다이아몬드 Green, 드래곤 Gray)에서 색상 아이콘을 가져온다.

## 공통 UI 스프라이트

| 컨트롤러 필드 | 적용 에셋 | 용도 |
|---|---|---|
| `combatBackgroundSprite` | `Assets/DownLoadAssets/Casual_GUI/GUI Pro-SimpleCasual/ResourcesData/Sprites/Demo/Demo_Background/Background_00.png` (2048×1152) | 성 풍경. 낮은 밝기·청회색 저채도 막 + 남색 그라데이션 + 가장자리 비네팅 |
| `dragHandCursorSprite` | `Assets/DownLoadAssets/IconPack/2D Minimal-IconPack/Icons/128/UI_Common_Hand_01.png` | 보드를 드래그할 때 표시하며, 스프라이트 영역을 48×48 커서로 축소 |
| `panelSprite` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Frame/BasicFrame_SquareSolid01_Demo01.png` | 호환용 직렬화 참조. 주요 패널은 평면 색상 사용 |
| `tabSprite` / `tabFocusSprite` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/UI_Etc/TabMenu_Top_Demo_Bg.png` / `TabMenu_Top_Demo_Focus.png` | 탭 상태 |
| `nodeSprite` / `nodePurchasedSprite` | 레거시 샘플 | 런타임 미사용. 모든 노드는 평면 정사각 셀 |
| `nodeAccentSprite` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Frame/BasicFrame_SquareOutline02_White.png` | 예약 참조. 실제 선은 런타임 4면 Image |
| `gemIcon` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Red.png` | 폴백 젬 아이콘 |
| `gemstoneIcon` | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Bag01.png` | 젬 수집 특성 |
| `lockIcon` / `checkIcon` | `IconMisc/Icon_Lock01.png` / `IconMisc/Icon_Check.png` | 잠금·구매 완료 |
| `backIcon` | `IconMisc/Icon_Arrow_Back.png` | 뒤로가기 |
| `attackIcon` | AS-09904 `Icon_ItemIcons/512/ItemIcon_Weapon_Sword.png` | 공격력 |
| `radiusIcon` | AS-12757 `IconPack/.../UI_ETC_Target_02_Blue.png` | 전투 반경·표적 |
| `attackIntervalIcon` | AS-09635 `CardFrame01_Demo_Slider_Icon.png` | 공격 간격(직렬화 필드 재사용) |
| `timerIcon` | AS-10047 `IconMisc/Icon_Timer.png` | 일반 필드 시간·스킬 쿨타임 |
| `criticalIcon` | AS-09982 `IconMisc/Icon_Fire02.png` | 치명타 |
| `bossIcon` | AS-10024 `IconMisc/Icon_Skull03.png` | 보스 피해 배수 |

선택한 512px 아이콘 메타는 압축을 끄고, `NanumGothic.ttf`는 48pt 동적 폰트로
설정했다. 캔버스는 픽셀 퍼펙트 오버레이와 1.18배 UI 글자 크기를 사용한다.

### 화면 스타일

- 노드 셀은 `84×84` 정사각형이며 회색·원형·아이콘별 카드 테두리를 사용하지 않는다.
- 상태는 셀의 네 변 런타임 선(구매 완료 초록, 구매 가능 카테고리 색, 잠금 남색)으로만 구분한다.
- 연결선은 흰색에 카테고리 색상을 14% 섞은 `Image`이며 입력을 가로채지 않는다.
- 배경에는 성 풍경 이미지와 런타임 생성 남색 그라데이션·비네팅 텍스처를 사용한다.
  노드 입력은 가로채지 않도록 이미지 레이어의 `raycastTarget = false`를 유지한다.
- 보드를 누른 채 이동할 때는 `dragHandCursorSprite`를 커서로 표시하고, 버튼을 놓으면
  시스템 기본 커서로 복원한다.

## 스탯 노드 아이콘 매핑

스탯은 카테고리별 한 행이며, 젬스톤 해금·발견 노드는 해당 보석의 실제 아이콘을
사용한다. 카탈로그에 노드를 추가하면 같은 카테고리의 기본 아이콘으로 안전하게
대체된다.

| 카탈로그 카테고리 | 노드 범위 | 아이콘 |
|---|---|---|
| `stat.attack` 공격력 | 공격력 1~100 (10개) | `attackIcon` · 512px 검 |
| `stat.radius` 공격 반경 | 작은 원 → 영역 1~5 → 1/32 → 1/16 → 1/8 → 1/4 → 전체 (11개) | `radiusIcon` · AS-12757 표적 |
| `stat.cooldown` 공격 간격 | 런 기본값에서 공격 간격 강화 (5개) | `attackIntervalIcon` · AS-09635 |
| `stat.critical` 치명타 | 0% → 5% → 10% → 20% → 30% → 50% → 75% → 100% (8개) | `criticalIcon` · AS-09982 |
| `stat.boss` 보스 피해 배수 | 보스 공격력 1~5 (5개) | `bossIcon` · AS-10024 |
| `stat.fieldDuration` 일반 필드 시간 | 15초 → 20초 → 25초 → 30초 (4개) | `timerIcon` · AS-10047 |

### 현재 스탯 노드별 에셋 매핑

아래 ID와 아이콘 매핑이 현재 기획·카탈로그의 기준이다. 모든 Stat 노드는 같은
정사각형 셀과 카테고리별 아이콘을 사용한다. 42개 스탯·8회/16회 클릭·이전 5단계
반경 표는 폐기된 캡처용 기록이며 현재 구현·기획에 사용하지 않는다.

| 노드 ID 범위 | 표시 순서 | 적용 에셋 |
|---|---|---|
| `stat.attack.01~10` | 공격력 1 → 3 → 6 → 10 → 16 → 24 → 35 → 50 → 70 → 100 | AS-09904 `ItemIcon_Weapon_Sword.png` |
| `stat.radius.01~11` | 작은 원 → 영역 1~5 → 1/32 → 1/16 → 1/8 → 1/4 → 화면 전체 | AS-12757 `UI_ETC_Target_02_Blue.png` |
| `stat.cooldown.01~05` | 기본 공격 간격 강화 | AS-09635 `CardFrame01_Demo_Slider_Icon.png` |
| `stat.critical.01~08` | 0% → 5% → 10% → 20% → 30% → 50% → 75% → 100% | AS-09982 `Icon_Fire02.png` |
| `stat.boss.01~05` | 보스 피해 배수 1 → 2 → 3 → 4 → 5 | AS-10024 `Icon_Skull03.png` |

현재 스킬 탭에는 토네이도·헤드샷·벼락·화염 배기·마력 구체·레이저·베이비 드래곤 노드가 있다.
몬스터 행은
`monsterIcons[0..9]`를 monster.01~monster.10에 순서대로 매핑하고, 생산량 I/II/III은
같은 Body 스프라이트를 재사용한다.

## 몬스터 해금과 젬스톤 표시

가넷은 기본 공급원이다. 나머지 젬스톤은 해당 종류를 공급하는 몬스터를 해금하면
`GameInformationBuilder`가 사용 가능 상태로 계산한다. 별도의 젬스톤 해금·확률 강화
노드나 랜덤 젬 등장 가중치는 사용하지 않는다.

새 젬스톤을 처음 공급하는 몬스터의 연구 노드 우측 상단에 `GemstoneIconCatalog`의
색상 아이콘을 표시한다. 잠겨 이름을 숨긴 몬스터에는 배지를 보여주지 않는다.

| 몬스터 노드 | 해금되는 젬스톤 | 아이콘 ID |
|---|---|---|
| monster.02 여왕벌 | 토파즈 | `gem.topaz` |
| monster.04 가면유령 | 자수정 | `gem.amethyst` |
| monster.06 눈요정 | 사파이어 | `gem.sapphire` |
| monster.08 골렘 | 다이아몬드 | `gem.diamond` |
| monster.09 봉오리 | 드래곤 젬 | `gem.dragon` |

가넷은 시작부터 공급되므로 배지를 붙이지 않는다. 같은 젬을 공급하는 후속 몬스터에는
새 젬 해금 배지를 반복 표시하지 않는다.

## 스킬 아이콘 배열

`TraitScreenController.skillIcons[0..5]`은 현재 구현 스킬 6종에, `petIcon`은 베이비 드래곤에
사용한다.

| 순서 | 스킬 | 적용 아이콘 |
|---:|---|---|
| 1 | 토네이도 | AS-12485 `Misc_Wing_01.png` |
| 2 | 헤드샷 | AS-10409 `Gear_Bow_01.png` |
| 3 | 벼락 | AS-12514 `Stat_Skill_01.png` |
| 4 | 화염 배기 | AS-12435 `Misc_Fire_01_Red.png` |
| 5 | 마력 구체 | AS-12140 `Economy_Gem_07_Blue.png` |
| 6 | 레이저 | AS-12757 `UI_ETC_Target_02_Blue.png` |
| 7 | 베이비 드래곤 | AS-13610 `Pet_Baby Dragon.png` |

실제 VFX와 피해 판정은 Combat 모듈이 소유하며, 노드 아이콘은 연구 화면 표시 전용이다.

## 실행 화면 캡처

과거 캡처 자료이며 현재 노드 배치와 해금 규칙의 검증 자료로 사용하지 않는다. 새 화면은
Unity 6000.3.13f1 `Main.unity` Game View에서 다시 캡처한다.

| 탭 | 캡처 파일 |
|---|---|
| 스탯 | [trait-stat.png](./screenshots/trait-stat.png) |
| 스킬 | [trait-skill.png](./screenshots/trait-skill.png) |
| 몬스터 | [trait-monster.png](./screenshots/trait-monster.png) |
| 몬스터 해금 예시 | [trait-gemstone-unlocked.png](./screenshots/trait-gemstone-unlocked.png) |

## 몬스터 아이콘 배열

`TraitScreenController.monsterIcons`의 0~9번 인덱스는 각각 `monster.01`~`monster.10`에
매핑한다.
모든 이미지는 사용자가 지정한
`Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/EnemyMonster 2` 아래의
`ResourcesData/Sprites`에서 가져온 `Body.png` 또는 동일 역할의 `Body_Fire.png`다.

| ID / 이름 | AS 번호 | 적용 스프라이트 |
|---|---:|---|
| monster.01 그루터기 | AS-13815 | `.../Walker/Walker_Stump/Body.png` |
| monster.02 여왕벌 | AS-13595 | `.../Fly/Fly_Iron/Body.png` |
| monster.03 독버섯 | AS-13808 | `.../Walker/Walker_Mushroom/Body.png` |
| monster.04 가면유령 | AS-13382 | `.../Alien/Alien_Vampire/Body.png` |
| monster.05 갑옷병사 | AS-13628 | `.../Golem/Golem_Iron/Body.png` |
| monster.06 눈요정 | AS-13524 | `.../Cactus/Cactus_Snow/Body.png` |
| monster.07 벌집 | AS-13728 | `.../Skeleton/Skeleton_Soldier/Body.png` |
| monster.08 골렘 | AS-13717 | `.../Scorpion/Scorpion_Stone/Body.png` |
| monster.09 봉오리 | AS-13498 | `.../Bud/Bud_Fire/Body_Fire.png` |
| monster.10 판도라 상자 | AS-13786 | `.../Walker/Walker_Box/Body.png` |

위 경로는 앞서 표시한 `ResourcesData/Sprites/` 아래의 상대 경로다. 아이콘 이름,
몬스터 ID, 전투 프리팹은 각기 분리되어 있지만 현재 열 종의 표시는 위 이름을 사용한다.

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
