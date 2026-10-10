# Cursor Hunter 협업 누적 인수인계 로그

이 문서는 두 담당자의 작업이 이어질 때마다 끝에 기록을 추가하는 누적 로그다. 이전
기록은 수정하지 않고, 현재 상태와 다음 합류에 필요한 경계를 함께 남긴다. 기획의
정본은 [cursor-hunter-game-design-v0.0.4.md](../cursor-hunter-game-design-v0.0.4.md),
현재 특성 화면의 실제 매핑은 [trait-ui-assets.md](trait-ui-assets.md)다.

## 이전 기준 (2026-09-21)

| 항목 | 값 |
|---|---|
| Unity | 6000.3.13f1 (Unity 6.3 LTS) |
| 작업 브랜치 | `feature/ui-stat` |
| 런타임 진입 | `Assets/CursorHunter/App/Scenes/Bootstrap.unity` (One Scene) |
| 개발 씬 | `CombatSandbox.unity`, `ProgressionSandbox.unity` |
| 준영 소유 | Stat/Skill/Monster 특성, 젬스톤 경제·밸런스, 저장·정산 데이터 |
| 승기 소유 | 일반·보스 전투, 커서 판정, 생성·타이머, 파훼, 스킬 실행, HUD·VFX |
| 공통 합류 | App 화면 전환, 읽기 전용 스냅샷 계약, 빌드 설정 |

특성 화면은 카탈로그 데이터로 행과 노드를 동적으로 만든다. 스탯은 공격력 10,
반경 11, 다중 클릭 5, 치명타 8, 젬 수집 5, 보스 공격력 5와 젬 해금·발견 15개로
구성한다. 스킬은 파이어볼·라이트닝 볼트·프리즈닝·허리케인·메테오·드래곤 브레스
6종×4노드(24개), 몬스터는 `Monster1`~`Monster20` 해금 20개와 종별 생산량 강화
60개다. 모든 행은 인접 노드를 런타임 선으로 연결하고, 노드 셀은 동일한 정사각형으로 그린다.

## 누적 변경 기록

### 2026-10-02 · 커서 기본 스탯과 특성 보너스 스냅샷 분리

- Main 씬의 `PlayerCombatStatsRuntime` 오브젝트에 `PlayerCombatStatsDefaults`를 추가했다. 이 컴포넌트가 모든 새 런의 커서 기본값을 소유하며, 런타임 컴포넌트는 매 런 시작 때 기본값과 현재 보유 특성 보너스를 합산해 이번 런 수치로 캡처한다.
- 공격력·공격 반경·공격 쿨타임·치명타 확률·보스 피해 특성을 절대값 지정에서 가산 보너스로 전환했다. 쿨타임 감소는 음수 delta이며 최종값은 0.05초 이상, 치명타 확률은 100% 이하로 제한한다. 필드 지속시간·스킬·몬스터 특성 처리는 유지하고, 설정된 공격력-스킬 피해 연동도 기본 공격력 대비 기존 비율 계산을 유지했다.
- `CursorCombatStatBonusesSnapshot`을 Contracts에 추가하고 HuntManager가 특성 상태로부터 스냅샷을 만들어 런타임 전투 스탯으로 전달한다. 스킬 스냅샷은 확장 가능하도록 별도 계약을 유지한다.
- 런타임 Inspector의 커서 반경 값은 실행 중 자동 공격 영역에 반영된다. 새 런을 시작하면 Defaults 컴포넌트 값과 구매 상태를 다시 합산한다.
- 새 `add` 계약에 맞춰 progression JSON Schema, 문서 생성 설명, 데이터 검사 스크립트를 갱신했다.
- 정적 협업 검사와 diff 공백 검사를 수행했다. Unity import/컴파일/Play Mode는 실행하지 않았다.

### v0.0.3~v0.0.4 기준 정리

- 준영과 승기의 담당 영역, One Scene + 모듈별 Sandbox, 디렉터리 소유권과 병합
  규칙을 `README.md`, `architecture.md`, `workflow.md`에 정리했다.
- 기본 게임 길이는 약 2시간 30분이다. 보스 HP와 일반 몬스터 물량은 후반으로 갈수록
  크게 증가하고, 해금한 일반 몬스터는 후반에도 계속 중첩 출현한다.
- 특성 화면의 한국어 기본 표시, Stat/Skill/Monster 탭, 좌측 젬 목록, 우측 요약·상세,
  전투 필드 배경, 정사각 노드와 연결선을 공통 방향으로 확정했다.

### 2026-09-15 · 특성 UI 프로토타입

- 파일: `Assets/CursorHunter/Progression/Runtime/TraitScreenController.cs`,
  `TraitCatalog.cs`, `Assets/CursorHunter/App/Scenes/Main.unity`.
- 카탈로그가 없을 때도 런타임 데모 카탈로그를 만들고, 카탈로그를 지정하면 같은
  화면 코드로 행·노드를 그리도록 했다.
- 스탯·스킬은 세로 스크롤, 몬스터 20종은 긴 가로 체인으로 탐색한다. 선택 상세,
  구매 가능·재화 부족·보스 잠금·선행 노드 상태를 같은 셀 규칙으로 표시한다.
- `Main.unity`의 직렬화 아이콘 배열과 몬스터 Body 스프라이트 순서를
  [trait-ui-assets.md](trait-ui-assets.md)에 기록했다.

### 2026-09-17 · 화질·아이콘 정리

- 작은 원본을 확대해 생기던 흐림을 줄이기 위해 특성 화면의 무기·젬·보스 아이콘을
  512px 스프라이트로 교체했다. 해당 `.meta`는 텍스처 압축을 끄고, 원본 PNG는
  변경하지 않았다.
- NanumGothic을 동적 48pt로 설정하고 Canvas 픽셀 퍼펙트와 UI 글자 1.18배 배율을
  적용했다. 행 높이와 젬 아이콘도 함께 키워 작은 Legacy Text가 뭉개지는 현상을
  완화했다.
- 아이콘 의미를 역할에 맞춰 검(공격력), 방패(반경), 활(다중 클릭), 별(치명타),
  가방(젬 수집), 해골(보스 공격력)로 통일했다. 스킬 첫 노드는 불·번개·방패·책·별
  계열을 분산하고, 후속 수치 노드는 검·방패·타이머를 공통으로 사용한다.
- 화질 점검에서 `combatBackgroundSprite`의 원본 `field_a.png`가 202×111 타일임을
  확인했다. 특성 화면은 이를 전체 화면으로 확대하지 않고 `Image.Type.Tiled`로 반복해
  저해상도 확대 블러를 줄인다. 최종 배경은 전투 맵 프리팹/`SpriteRenderer`를 직접
  렌더링하는 방식으로 교체할 수 있도록 이 임시 타일의 한계를 기록한다.
- 실제 경로·GUID는 [assets.md](assets.md)와 [asset-map.json](asset-map.json)에
  기록했다. 몬스터 아이콘은 지정된 `EnemyMonster 2/ResourcesData/Sprites`의
  Body 스프라이트를 `Monster1`~`Monster20` 순서로 사용한다.

### 2026-09-17 · 젬스톤 해금·랜덤 확률

- 젬스톤 슬롯은 보스 처치로 자동 추가하지 않는다. 게임 시작에는 가넷만 노출한다.
- Stat의 `stat.gemstone` 행에 다음 해금 순서를 둔다.

  `stat.gemstone.unlock.topaz → .unlock.amethyst → .unlock.sapphire → .unlock.diamond`

  해금 노드는 이전 해금 노드를 선행 조건으로 요구한다. 수집 방식(접촉 클릭 →
  반경 수집 → 넓은 수집 → 자동 수집)은 같은 행의 별도 노드로 유지한다.
- 해금된 종류의 랜덤 드롭 가중치는 다음 공식으로 계산한다.

  `weight = min(maxWeight, baseWeight + 발견 강화 횟수 × stepWeight)`

  `chance = weight / 해금된 모든 weight의 합 × 100`

- 기준값은 가넷 `70/+0`, 토파즈 `20/+5`, 자수정 `8/+3`, 사파이어 `3/+2`,
  다이아몬드 `1/+1`이다. 토파즈·자수정·사파이어·다이아몬드는 각각 발견 I·II
  두 노드를 갖는다. 발견 노드는 해금된 종류의 확률만 올리고 전체 합은 다시
  100%로 정규화한다.
- `TraitCatalog.TraitGemstoneDefinition`에 해금 노드 ID, 기본/증가/최대 가중치,
  발견 노드 접두사를 추가했다. 기존 5인자 생성자는 호환성을 위해 유지한다.
- `TraitScreenController`에 다음 프로토타입 API를 추가했다.
  - `IsGemstoneUnlocked(int index)`
  - `GetGemstoneDropWeight(int index)`
  - `GetGemstoneDropChance(int index)`
  - `TryRollGemstoneDrop(float normalizedRoll, out int gemstoneIndex)`
- 이 API는 화면과 카탈로그 검증용이다. 전투 모듈은 UI를 참조하지 않고, 첫 합류 때
  Progression이 계산한 읽기 전용 드롭 스냅샷을 `CombatSnapshot`에 전달한다.

## 합류 경계

1. 준영은 구매·저장·젬 가중치 계산을 담당하고, 승기는 받은 스냅샷으로 적 처치 시
   드롭을 선택한다. Combat에서 `TraitScreenController`나 ScriptableObject를 직접
   참조하지 않는다.
2. 런 시작 뒤 특성 구매는 허용하지 않는다. 구매 결과는 다음 런의 스냅샷에만 반영한다.
3. 젬 종류를 선택할 때 Combat은 자체 확률을 다시 만들지 않는다. `seed`와 전달받은
   가중치 표로 결정하고, 결과에는 안정적인 `gemstoneId`와 수량만 기록한다.
4. 보스 최초 처치 저장은 보스·스킬·몬스터 해금과 보상만 확정한다. 젬스톤 해금은
   Stat 구매 트랜잭션으로 별도 저장한다.
5. 계약을 바꿀 때는 생산자(Progression)·소비자(Combat)·테스트 더블을 같은 PR에서
   갱신하고, `schemaVersion`을 올린다. 양쪽이 동시에 `Main.unity`를 편집하지 않는다.

## 검증 상태

- 정적 점검: `git diff --check` 통과. `docs/collaboration/asset-map.json`은 JSON
  파서로 확인한다.
- 코드 위험 점검: 가중치 합과 곱셈은 `long` 중간값·상한 처리를 사용한다. 드롭 롤은
  할당 없이 실행하며, 잠긴 항목·빈 카탈로그·0 가중치·`roll == 1`·NaN 입력 경계를
  처리한다.
  UI 이벤트는 화면 수명에 맞춰 해제하고, Unity 오브젝트 접근은 메인 스레드에서만
  수행한다.
- 시각 캡처: `docs/collaboration/screenshots/`의 기존 PNG는 이전 레이아웃 기준이다.
  최신 512px 아이콘·확률 표시 캡처는 Mac이 잠겨 있어 아직 재생성하지 못했다. Mac을
  잠금 해제한 뒤 Unity 6000.3.13f1 Game View에서 Stat(가넷만 표시), Skill, Monster,
  젬 해금 상태를 다시 캡처하고 이 표를 갱신한다.
- `SetProgressionSnapshot(long, IReadOnlyList<long>, int)` 오버로드로 가넷을 포함한
  젬스톤 잔액 배열을 읽기 전용 프로필 스냅샷으로 전달할 수 있다. 기존 가넷·보스 단계
  전용 호출도 유지한다. 실제 저장·전투 드롭 지급은 첫 합류에서 Progression 서비스로
  옮긴다.

## 다음 합류 작업

- 준영: `TraitCatalog`를 ScriptableObject 인스턴스로 분리하고 구매·저장 서비스가
  젬 해금과 발견 강화 횟수를 영구 보존하도록 연결한다. 가중치 표를 읽기 전용
  `CombatSnapshot`으로 내보낸다.
- 승기: 스냅샷의 가중치와 seed로 처치 시 젬 종류를 선택하고, `RunProgress`에 실제
  `gemstoneId`·수량을 누적한다. UI나 VFX에서 지갑을 직접 변경하지 않는다.
- 함께: 시작 가넷 1종 → Stat에서 토파즈 해금 → 발견 I·II 구매 → 다음 런에서
  확률 재정규화와 전체 잔액 스냅샷이 보이는 최소 루프를 확인한다. 이후 보스·일반 몬스터·스킬을
  기존 2시간 30분 밸런스 표에 맞춰 연결한다.

### 2026-09-18 · 최신 확정 진행 카탈로그·확장 탭

- 담당: 준영. 영향 파일: `Assets/CursorHunter/Progression/Runtime/TraitCatalog.cs`,
  `TraitScreenController.cs`, `Main.unity`, `docs/cursor-hunter-game-design-v0.0.4.md`,
  `docs/collaboration/contracts.md`, `trait-ui-assets.md`.
- Stat을 공격력 10, 반경 11, 다중 클릭 5, 치명타 8, 젬 수집 5, 보스 공격력 5와
  젬스톤 해금·발견 15개로 재구성했다. 자동 무한 클릭은 마지막 노드이며 8회·16회와
  초당 횟수 분기는 제거했다. 반경은 미세 확대 5단계 뒤 1/32·1/16·1/8·1/4·전체로
  이어지고, 치명타는 5·10·20·30·50·75·100%를 사용한다.
- Skill 탭을 6종(파이어볼, 라이트닝 볼트, 프리즈닝, 허리케인, 메테오, 드래곤 브레스)으로
  줄이고 각 행을 해금+피해량+범위+쿨타임 4노드로 통일했다. 스킬별 전용 젬스톤 비용과
  드래곤 브레스 v4 해금 계약을 추가했다.
- Monster 탭에 해금 20종과 생산량 3단계 60노드를 분리했다. 전리품 탭(일반 20+보스 5)은
  `acquisitionOnly`, 펫 탭은 1마리 자동 공격과 4단계 강화로 추가했다.
- `TraitNodeDefinition.CostGemstoneId`·`AcquisitionOnly`를 추가하고, UI 구매·환불이
  가넷 외 젬스톤 잔액을 사용하도록 변경했다. 씬에는 드래곤 젬, 전리품, 펫 아이콘 배열을
  연결했다.
- 2시간 30분 목표에 맞춰 v1~v5 누적 시간, 스킬/몬스터/젬 해금, Monster1~20 HP·젠
  주기 초안을 문서와 계약에 기록했다. 몬스터 HP·전투 수치는 Combat 플레이테스트 뒤
  `balanceVersion`을 올려 조정한다.
- Unity Editor 로그에서 `CursorHunter.Progression.dll` 스크립트 컴파일 성공을 확인했다.
  Unity 6000.3.13f1 Main 씬을 Play Mode로 실행해 Stat/Skill/Monster/Loot/Pet 탭을
  순서대로 열었고, 5개 탭·2개 몬스터 행·전리품 도감·펫 행이 렌더링되는 것을 확인했다.
  이번 확인은 Game View의 16:10 미리보기였으므로 실제 빌드 해상도별 폰트 크기와 긴
  가로 스크롤은 별도 캡처 단계에서 다시 확인한다.

- 젬스톤 해금 비용은 직전 단계 재화(토파즈는 가넷, 자수정은 토파즈 등)로,
  해금 후 발견 I·II는 해당 젬스톤으로 차감하도록 비용 ID를 보정했다. 잠긴 재화를
  요구하는 구매 노드는 만들지 않는다.
- 전투 밸런스 초안도 최신 기획의 현재 절에 명시했다. 보스 HP는
  `1,300 → 12,000 → 180,000 → 18,000,000 → 2,700,000,000`, 스킬 기본 계수는
  `4A / 8A / 12A / 6A×6 / 40A / 250A` 순서다. 실제 전투 연결 후 60초 런 로그로
  `balanceVersion`을 올려 재조정한다.

### 2026-09-21 · 해상도·저장·테스트 해금·전투 스냅샷 연결

- 담당: 준영. 영향 파일: `TraitScreenController.cs`, `TraitCatalog.cs`,
  `TraitProgressionStore.cs`, `ProgressionCombatSnapshot.cs`, `CombatSnapshot.cs`,
  `HuntManager.cs`, `DisplayQualityBootstrap.cs`, `Main.unity`, ProjectSettings 및
  최신 기획서.
- 특성 UI의 모든 노드를 테스트 모드에서 선행 조건·보스 조건 없이 확인하도록 열었다.
  기본 테스트 플래그는 `testModeUnlockAll=true`, `testModeFreeUpgrades=true`이며 출시
  전 일반 모드에서 끈다. 상세 패널에 `강화/해금` 옆 `취소`를 추가했고, 연결된 다음
  노드가 구매된 경우 역순 취소만 허용한다.
- 구매·취소·초기화 결과를 `PlayerPrefs` JSON 프로토타입 저장소에 기록한다. 저장 실패는
  메모리 변경을 유지하되 경고를 남기며, 정식 출시 전 파일/클라우드 저장 서비스로 교체한다.
  `전체 정보` 팝업은 현재 구매 노드와 전투 스냅샷을 스크롤로 보여준다.
- 스킬 강화 노드는 해금·피해량·범위·쿨타임 모두 해당 스킬 기본 아이콘을 재사용한다.
  Monster1~20은 각각 `해금 → 생산량 I → II → III` 한 행으로 묶었고 생산량 노드는
  같은 몬스터 아이콘과 주황색 테두리를 사용한다. 노드는 5개 단위로 자동 개행하고
  세로 커넥터를 이어 붙인다.
- `Contracts/Runtime/ProgressionCombatSnapshot.cs`에 읽기 전용 스킬·몬스터 배열과
  `CombatSnapshot` 확장을 추가했다. Progression은 런 시작 시 공격력·반경·다중 타격·
  치명타·보스 배율·스킬·몬스터 값을 복사하고, App/HuntManager는 Combat과 SpawnPlan에
  전달한다. Combat은 Progression UI·저장 파일을 직접 참조하지 않는다. 현재 단일
  MonsterDefinition을 먼저 연결하며 다중 종 SpawnPlan은 다음 전투 작업에서 확장한다.
- Standalone 기본 해상도를 1920×1080으로 올리고 Ultra 품질의 4x MSAA, 비등방성 필터,
  수직 동기화를 적용했다. Canvas는 1920×1080 기준 중간 스케일과 비픽셀 스냅으로
  창 크기 변경 시 동적 텍스트가 뭉개지는 현상을 줄인다.
- 정적 검증: `python3 tools/validate_collaboration.py` 통과, `git diff --check` 통과.
  Unity import/compile/Play Mode는 이 환경의 정적 검증 범위에 포함되지 않아 Editor에서
  재생 후 Console과 1920×1080/1280×720 캡처를 다시 확인해야 한다.

### 2026-09-21 · 후속 UI·품질 정합성 보정

- 젬 수집 원장을 최신 요청에 맞춰 5상태로 통일했다: `접촉 클릭 → 접촉 자동 → 자석
  → 자석 반경 확대 1~2 → 화면 전체 자동 수집`. 카탈로그 ID는
  `stat.gemstone.01~05`이며 스탯 총 노드는 69개(기본 54 + 젬 해금·발견 15)다.
- `전체 정보` 팝업의 ContentSizeFitter에 세로 레이아웃을 추가해 구매 항목이 많아져도
  내용 높이를 계산하고 ScrollRect로 끝까지 읽을 수 있게 했다.
- QualitySettings의 기본 품질 인덱스를 Ultra(5)로 맞추고, 런타임 부트스트랩의 4x MSAA·
  비등방성 필터·수직 동기화와 함께 Editor의 낮은 품질 기본값이 남지 않게 했다.
- Unity Play Mode에서 Main → 특성 → Skill/Monster 탭과 `전체 정보` 오버레이를 확인했다.
  Game View 축소 미리보기 자체는 실제 빌드 해상도 판정이 아니므로 1920×1080 Standalone과
  1280×720 창에서 별도 캡처를 진행한다.

### 2026-09-21 · 전체 정보 Key/Value 정리

- `TraitScreenController`의 `전체 정보` 팝업을 단일 줄바꿈 Text에서 동적 레이아웃으로
  변경했다. `현재 전투 적용값 → 탭 → 카테고리 → Key | Value 행 → 젬스톤 보유·드롭` 순서로
  구성하며, 각 노드는 `적용됨`, `미적용`, `잠김`, `전투 중 확률 획득` 상태를 Value 열에
  표시한다.
- 스킬은 스킬 종류 아래에 해금·피해량·범위·쿨타임을, 몬스터는 Monster1~20 아래에
  해금·생산량 I/II/III을 각각 Key/Value로 노출한다. 팝업은 기존 ScrollRect 안에서
  동적으로 높이를 계산하므로 항목 수가 늘어도 잘리지 않는다.

### 2026-09-21 · 실제 렌더링 화질 보정

- `field_a.png`는 원본이 202×111인 단색 필드 타일이므로 `Main.unity`의 `field_a`
  `SpriteRenderer`를 원본 크기 기준 `Tiled` 모드(48×28)로 변경했다. 기존처럼
  100배로 한 장을 늘리지 않아 카메라가 보는 영역에서 텍스처를 불필요하게 확대하지 않는다.
- Main 씬에서 사용하는 PNG 68개의 `DefaultTexturePlatform`·`Standalone` 텍스처 압축을
  끄고 압축 품질을 100으로 맞췄다. 원본 픽셀 수가 낮은 에셋의 디테일을 새로 만들 수는
  없지만, UI 아이콘·몬스터·맵 장식의 JPEG 계열 압축 번짐은 줄어든다. 모바일·WebGL
  플랫폼 설정은 기존 값을 유지해 메모리 사용량을 불필요하게 늘리지 않는다.
- `UniversalRP.asset`을 4x MSAA로 설정하고 `DisplayQualityBootstrap`에서 전역 mipmap
  제한을 0으로 강제한다. 품질 설정은 렌더링 샘플링을 보정하며, 에디터 Game View의
  축소 미리보기 자체는 품질 측정에서 제외한다. 검증 시 Game View를 `Full HD (1920×1080)`
  또는 `WXGA (1366×768)`로 선택하고 Scale을 Fit/1x로 맞춘다.

### 2026-09-21 · 특성 액션 UX·전투 정보 접근·HP 바·밸런스 검증

- 담당: 준영. 영향 파일: `TraitScreenController.cs`, `PrototypeRunHud.cs`,
  `MonsterHealthBarView.cs`, `WalkerStumpTarget.cs`, `TraitCatalog.cs`,
  `docs/cursor-hunter-game-design-v0.0.4.md`.
- 특성 상세 패널의 액션을 중앙 행으로 재배치했다. 왼쪽은 `강화/해금`, 오른쪽은
  `노드 초기화`이며, 하단 전역 액션은 `전체 초기화`로 구분한다. 노드 초기화는
  다음 구매 노드가 남아 있으면 역순을 요구하고, 전체 초기화는 테스트 구매분을 모두
  환불한 뒤 시작 상태를 다시 구성한다. 코드 메서드도 `ResetSelectedNode`로 명시했다.
- `TraitScreenController`의 `전체 정보` 팝업을 TraitPanel 하위가 아니라 활성 Canvas
  하위에 만들도록 바꿨다. 따라서 Trait 화면을 닫은 일반·보스 Field에서도 HUD의
  `전체 정보` 버튼이 같은 팝업을 열 수 있다. 팝업은 `Key | Value` 표를 유지하고,
  열려 있는 동안 Canvas 레이캐스트가 전투 클릭을 가린다. 런 시작 시 App이 전달한
  `ProgressionCombatSnapshot`을 결과 화면까지 고정해, 전투 중 UI 상태가 바뀌어도
  팝업의 적용값이 실제 런과 달라지지 않게 했다.
- `PrototypeRunHud`에 우측 상단 `전체 정보` 버튼을 동적으로 추가했다. 시작 시
  `TraitScreenController`를 비활성 오브젝트까지 검색하고, 버튼 클릭 때 최신 스냅샷·
  해금·젬스톤 데이터를 다시 구성한다. 보스 HUD가 같은 HUD를 사용해도 같은 접근
  경로를 재사용한다.
- 일반 몬스터 `WalkerStumpTarget`에 `MonsterHealthBarView`를 붙였다. 생성·피격 후
  1.25초 동안 월드 공간 HP 바를 표시하고, 남은 비율에 따라 초록·노랑·빨강으로
  바뀐다. 체력바 SpriteRenderer는 전투 콜라이더 bounds에서 제외해 공격 판정이
  넓어지지 않도록 했다. 정적 흰 픽셀 Sprite 1개를 공유해 개체별 텍스처 할당과
  per-frame 메모리 할당을 피한다.
- 설계 시뮬레이션에서 2클릭/초·치명타 2배·스킬 강화 완료·자동 10회/초를 가정했다.
  보스 초안 HP를 `1,300 → 12,000 → 90,000 → 240,000 → 1,800,000`으로 조정했고,
  예상 처치 시간은 약 30초·43초·51초·40초·55초다. 이는 Combat 스킬·치명타·자동화
  연결 전의 설계 계산이며, 실제 60초 런 로그가 쌓이면 `balanceVersion`만 올려 한 축씩
  조정한다. 자동 무한 노드는 v4 처치 후 사용할 수 있도록 카탈로그 요구 티어를 4로
  맞췄다.
- 기획서 `B08`에 펫 원장(기본 1마리, 반경·피해·주기·대상 수), 일반/보스 전리품
  확률·중복 변환·Key/Value 표기, 수확량·스킬 충전·전리품 행운 확장 Stat 후보와
  150분 시뮬레이션 가정을 누적했다. 새 확장 Stat은 아직 Combat 계약에 필드를
  추가하지 않았으므로 설계 후보로만 유지하며, 현재 런 스냅샷 값은 기존 필드와
  동일하게 불변 복사한다.
- 검증 시 확인할 항목: Unity Console의 `error CS`, `NullReferenceException`,
  `LogAssemblyErrors`, 일반 Field에서 HP 바가 콜라이더를 확장하지 않는지, Field HUD
  `전체 정보`가 팝업을 열고 닫은 클릭이 공격으로 전달되지 않는지, Trait 상세에서
  두 버튼이 겹치지 않는지.
- `DisplayQualityBootstrap`에서 고정 DPI 계수와 `ScalableBufferManager` 버퍼를 1:1로
  맞춰 낮은 동적 해상도 버퍼가 Play Mode에 남지 않도록 했다. Game View 축소 배율은
  실제 빌드 해상도와 다르므로 Full HD/창모드 캡처로 별도 확인한다.

### 2026-09-21 · 현재 상태·인수인계 통합본

- `docs/collaboration/current-state-handoff-v0.0.4.md`를 추가했다. 최신 Stat·Skill·Monster·
  젬스톤·전리품·펫 규칙, 150분 밸런스 표, 화면 UX, 구현 완료/부분/미착수 범위,
  Progression→App→Combat 계약, 담당별 다음 작업을 한 문서로 묶었다.
- `docs/collaboration/README.md`에서 새 문서를 첫 읽기 문서로 연결했다. 기존 기획서와
  누적 로그는 상세 원문·변경 이력으로 유지한다.

### 2026-09-21 · 일반 필드 시간·치명타·드롭·전리품 조각 계약

- 담당: 준영. 영향 파일: `TraitCatalog.cs`, `TraitScreenController.cs`,
  `ProgressionCombatSnapshot.cs`, `HuntManager.cs`, `CombatRunController.cs`,
  `RunResult.cs`, `SpawnSnapshot.cs`, `MonsterDefinition.cs`, `WalkerStumpTarget.cs`,
  `TraitProgressionStore.cs`, `PrototypeRunHud.cs`.
- 일반 필드 시간을 `15초 → 20/25/30/35/40/45/50/55/60초` 10상태로 추가했다. 노드 비용은
  `0/15/35/65/110/180/280/420/600/850 가넷`이며 `ProgressionCombatSnapshot`의
  `NormalFieldDurationSeconds`로 런 시작에 고정된다. `HuntManager.GetRunDuration`은
  일반 필드에만 이 값을 적용하고 보스는 항상 60초를 반환한다.
- Main 씬의 현재 일반 필드 반경 초안(`cursor_image` 스케일 2, 원형 콜라이더 반경
  0.86269045, RangeMultiplier 1)을 기본값으로 명시했다. 반경 Stat은 이 기준에 배율만
  적용하며 원본 씬 콜라이더를 덮어쓰지 않는다.
- `CombatRunController`가 SeededRandom으로 타격마다 치명타를 판정하고, 보스 모드에는
  `BossDamageMultiplier`, 치명타에는 2배 피해를 적용한다. `CriticalHit` 이벤트와 HUD의
  `치명타! ×2` 알림으로 발동 사실을 확인할 수 있다. 이벤트 리스너는 OnDisable에서
  해제해 중복 구독을 막는다.
- `ResourceRewardSnapshot`·`RunResult.Rewards`를 추가해 가넷·보너스 젬스톤·전리품
  조각을 한 결과에 기록한다. Monster balance와 SpawnSnapshot에는 HP·젠 주기·가넷
  보상·보너스 젬스톤 확률·전리품 조각 확률을 함께 복사한다. `HuntManager`는 Eligible
  결과를 `TraitScreenController.GrantResourceRewards()`로 저장하고 같은 `RunId`를
  메모리에서 두 번 정산하지 않는다.
- 전리품은 `fragment.loot.*` 전용 잔액을 사용하고 최초 1, 이후 2/3/5/10 조각 비용의
  노드를 카탈로그에 추가했다. `TraitProgressionStore`가 조각 지갑을 JSON으로 저장하며,
  정산은 `GrantResourceRewards`를 중복 이벤트 검증 뒤 한 번만 호출한다. 개별
  `GrantLootFragments`는 외부 정산 서비스가 특정 조각만 지급할 때 사용하는 보조 API다.
- `TraitBossRewardDefinition`에 v1~v5 기본 가넷·보너스 젬스톤·조각 확률·최초 조각
  보너스를 추가했다. 상세 수치는 통합 인수인계 문서와 게임 기획서의 보상 원장을 따른다.
- `docs/collaboration/current-state-handoff-v0.0.4.md`, `architecture.md`,
  `docs/cursor-hunter-game-design-v0.0.4.md`에 비용 원장, 몬스터 드롭/젠, 보스 보상,
  승기용 스냅샷 읽기 코드와 15~60초 정책을 반영했다.
- 검증: `git diff --check`, `python3 tools/validate_collaboration.py` PASS. Unity 에디터의
  다음 스크립트 리컴파일 후 Console에서 `error CS`, `NullReferenceException`,
  `LogAssemblyErrors`가 없는지 확인한다.

### 2026-09-21 · 비용 원장·전리품 UI 수량·치명타 표시 보정

- `current-state-handoff-v0.0.4.md`에 젬스톤 해금/발견, 스킬 6종, Monster 생산량,
  전리품 1/2/3/5/10, 펫까지 모든 노드의 비용과 결제 재화를 한 표로 추가했다.
- `trait-ui-assets.md`의 Stat 노드 수를 일반 필드 시간 행까지 포함한 69개로 정정하고,
  젬 수집 기본 행을 5개로 맞췄다. 전리품은 25개 도감이 아니라 일반 20개와 보스 5개의
  해금·4단계 강화 125개 노드이며, 구매 버튼이 있는 조각 트리로 문서와 카탈로그를
  일치시켰다.
- `contracts.md`에서 폐기된 v5 HP 예시를 제거하고 Int64 경계 규칙을 현재 보스/몬스터
  원장에 맞게 일반화했다. 핵심 Contracts 타입이 이미 구현됐다는 상태도 명시했다.
- `PrototypeRunHud.ShowRunning()`이 매 프레임 치명타 알림을 숨기던 문제를 수정했다.
  이제 새 런 초기화·정산·리셋 때만 알림을 숨기고, `CriticalHit` 알림은 0.45초 동안
  유지된다.
- 설계 문서 B08의 시뮬레이션 문구를 치명타·보스 배율·HUD는 연결되고 스킬·자동 공격은
  아직 미연결이라는 현재 구현 상태로 정정했다.
- `tools/render_current_design_pdf.py`를 추가해 최신 Markdown을 단일 원본으로 PDF에
  재생성한다. 1·36·72페이지를 렌더링해 표·와이어프레임·마지막 페이지에 잘림이 없는지
  확인했다. PDF의 역사적 예시는 최신 확정 범위 표보다 우선하지 않는다.

### 2026-09-21 · 인수인계 핵심본·요약 PDF 정리 및 Unity 검증

- `current-state-handoff-v0.0.4.md`를 220줄의 핵심본으로 재작성했다. 소유권, 런타임 계약,
  15~60초 일반 필드·60초 보스, Stat/Skill/Monster/젬스톤/전리품/Pet 규칙,
  비용·보상 기준, 승기 구현 순서, 병합·테스트 방법, 남은 작업만 남겼다.
- `tools/render_current_design_pdf.py`의 입력을 핵심 인수인계 문서로 변경했다. 최신 PDF는
  72페이지 상세 기획서가 아니라 5페이지 핵심본을 출력하며, 1·5페이지를 렌더링해 잘림과
  겹침이 없는 것을 확인했다.
- 원본 `cursor-hunter-game-design-v0.0.4.md`는 상세 수치·와이어프레임 참조 문서로 유지하고,
  README와 v0.0.4 안내문에서 핵심본과 상세 원문의 역할을 구분했다.
- `git diff --check`, `python3 tools/validate_collaboration.py`, Contracts 독립 컴파일은 PASS.
- 원본 Unity Editor 로그에서 최신 Tundra 컴파일 성공과 어셈블리 로드를 확인했다. 원본 프로젝트가
  열려 있어 같은 경로 Batch Mode는 프로젝트 잠금으로 거부됐고, 별도 임시 Batch Mode는 Unity
  Package Manager 소켓·라이선스 채널 초기화에서 중단되어 Play Mode PASS 판정은 보류했다.

### 2026-09-21 · 통합 인수인계 원장 재통합

- 지정 기준 문서 `docs/collaboration/current-state-handoff-v0.0.4.md`에 기존 요약 내용과
  최신 상세 원장을 다시 통합했다. 보스 구간별 접근권, Stat 비용, 스킬 기본값,
  Monster1~20 HP·젠·드롭, 보스 보상, 화면 UX, 구현 상태를 한 파일에서 확인할 수 있다.
- 요약 PDF도 같은 인수인계 파일을 다시 읽도록 갱신했다. 현재 10페이지이며, 상세
  와이어프레임 원문은 `docs/cursor-hunter-game-design-v0.0.4.md`에 유지한다.
- Unity Editor가 원본 프로젝트를 점유하고 있어 별도 Batch Mode는 프로젝트 잠금으로
  실행되지 않았다. 임시 Batch Mode는 Package Manager/라이선스 채널 초기화에서 중단됐다.
  Editor.log의 최신 Tundra 빌드와 어셈블리 로드는 확인했지만 Play Mode 성공 판정은
  Unity Editor를 저장 후 닫고 재실행할 때 수행한다.

### 2026-09-21 · 젬스톤 다중 재화 결제 원장 적용

- `TraitCatalog.CreateRuntimeDemo()`의 Stat 유료 노드에 `CostGemstoneId`를 구간별로
  명시했다. 공격력·반경·다중 클릭·치명타·젬 수집·일반 필드 시간은 가넷에서 시작해
  토파즈·자수정·사파이어·다이아몬드·드래곤 젬으로 이동하며, 보스 피해는 v1~v5의
  티어 젬을 그대로 사용한다.
- 스킬은 파이어볼=가넷, 라이트닝 볼트=토파즈, 프리즈닝=자수정, 허리케인=사파이어,
  메테오=다이아몬드, 드래곤 브레스=드래곤 젬의 전용 재화를 유지한다. Monster 생산량은
  해금 티어의 젬스톤, 전리품은 기존 `fragment.loot.*` 전용 조각을 사용한다.
- `TraitScreenController`가 이미 `CostGemstoneId`를 기준으로 비용명·잔액·지출·초기화를
  처리하므로 별도 지갑 로직을 만들지 않았다. 테스트 모드 무료 구매에서도 상세 패널에
  실제 젬스톤 비용을 표시한다.
- `current-state-handoff-v0.0.4.md`와 상세 기획서의 Stat 원장, 젬스톤별 역할, 공격력
  비용표를 런타임 카탈로그와 일치시켰다.
- 요약 PDF를 다시 생성해 11페이지로 갱신했다. `git diff --check`,
  `python3 tools/validate_collaboration.py`, 젬스톤 비용 마커 정적 검사를 통과했고,
  Unity Editor.log에서 `CursorHunter.Progression.dll` 포함 Tundra build success를 확인했다.
- 초기화 시 현재 노드의 재화로 환불하지 않고 구매 당시 저장된 `spentCurrencyIds`를
  우선 사용하도록 `TraitScreenController`를 보정했다. 따라서 가넷 단일 재화 시절의
  저장 데이터도 기록된 결제 재화를 기준으로 복구하고, 새 다중 젬 구매의 환불도 같은
  지갑으로 돌아간다.

### 2026-10-01 · JSON v2 / 노드 UI / 자동 반경 개편

- 요청 10개 항목의 구현 기준: [game-information-refactor-2026-10-01.md](game-information-refactor-2026-10-01.md).
- Contracts: GameInformation DTO, 유효성 검사, SourceJson 보관, 최종 스킬/몬스터 생산량 전달.
- Progression: 동일 JSON 전체 정보/복사, 노드 그래프, 젬 해금 제거, 펫→오라, 전리품→유물, v1 저장 이관.
- App/Combat: JSON 역직렬화 후 런 생성, 자동 단일 타격/쿨타임, 다중 종 스폰, 최대 30초, 팝업/포커스 일시정지.
- Data/Assets: 밸런스 JSON, 10종 몬스터 정의/아이콘, TMP/동적 한글 SDF/기본 로케일 키.
- UI: 게임 소유 Text 생성 코드 전부 TMP로 전환. Main 씬은 기존 TMP 유지 + 공용 폰트 적용.
- 문서: 현재 계약·아키텍처 정리. 구 v0.0.4 문서는 상단에 과거 기록임을 표시.
- 검증: 협업 검사, JSON/재화 경로/에셋 검사, C# 구문, diff 공백 검사.
- 실행 금지 준수: Unity, 게임, Play Mode, Unity Editor 테스트는 실행하지 않음.

## 2026-10-01 후속: 한국어/영어 전체 JSON 실파일

- game-data.ko.json / game-data.en.json 및 active-data.json 추가. 스탯/전투 규칙/젬 6/몬스터 10/스킬 7/유물 15/노드 186/보스 보상 5단계를 포함.
- GameDataDocument → GameInformationBuilder → 공통 스냅샷으로 런타임 연결. 비용·선행 조건·효과의 코드 하드코딩 제거.
- overlay/fileOnly 저장 정책 분리. fileOnly는 기존 저장을 읽거나 덮어쓰지 않음.
- 한국어/영어 기계 값 동등성, 필수 필드, 스키마, 노드 참조·순서, 프리팹 키 정적 검사 추가.
- 메모리 복사/배율 반올림/초기 지갑/상한/잘못된 참조 검토. Unity 실행 및 실제 컴파일/Editor 테스트는 하지 않음.

## 2026-10-01 정정: 한국어 JSON은 실행 파일이 아닌 설명서

- 사용자 의도를 정정: 영어 game-data.en.json만 실제 전투·성장 입력으로 고정.
- 한국어는 docs/collaboration/game-data.ko.reference.json에 영문 키·한글명·원본값·설명을 담는 참고 문서로 생성.
- Resources의 한국어 실행 복제본과 active-data 선택 파일 및 meta 제거. 기존 한국어 설명은 해설/재생성 도구로 보존.
- 데이터 언어에 따른 UI 언어 강제 변경 제거. 기존 저장 정책과 전투 수치는 유지.
- 전체 원본 복원·해설 일치·영어 단일 로더·스키마/GUID/구문 검사. 게임/Unity는 실행하지 않음.

## 2026-10-01 후속: 키 직후 한국어 표기·전투 조회 안내·9항목 재확인

- 요청: 모든 키를 영문키(한국어)로 병기하고, 친구가 전투 중 실제 값을 읽을 위치를 안내하며 기존 9개 요구의 반영 상태를 확인.
- game-data.ko.keys.json 추가: 영어 원본 전체를 같은 구조·자료형·값으로 유지하고 키만 번역 병기. 기존 상세 해설도 보존. 두 파일 모두 docs 전용이며 런타임에서 읽지 않음.
- build_korean_game_data_reference.py가 두 참고본을 생성/검증하도록 확장. 재생성 시 변경 없는 파일은 패치에서 제외. 누락 키는 오류 처리.
- test_korean_game_data_reference.py 추가: 중첩·배열·미등록 키·Int64·bool·소수·null·빈 값·전체 원본 왕복 검증, 4개 테스트.
- current-state-handoff-v0.0.4.md 앞에 최신 안내 추가. 오래된 v0.0.4 원장은 보존하고 과거 기준임을 명시. README/contracts/refactor 문서 연결 갱신.
- 저장 파일과 밸런스 설정을 구분하고, 전투 시작 스냅샷/라이브 전투 상태/현재 지갑의 차이와 공개·비공개 API를 안내.
- 9항목은 코드 반영/부분 반영/시각 검증 전을 구분. 전 문구 Localization, 스킬 고유 동작, 젬 공급원 결제 검사 등의 잔여 범위를 명시.
- 기존 버그 확인: 전체 초기화의 유물 조각 덮어쓰기, 저장 실패 복구 부족, 비활성 공급 젬 잔액 구매 제한. 이번에는 기록만 하며 런타임 로직 변경 없음.
- 검증: 한국어 두 참고본 동등성, Python 단위 테스트 4개, 영문 스키마·참조, 협업 경계, C# 구문 및 diff 공백 검사. Unity 컴파일·Editor 테스트·게임 실행·PlayerPrefs 변경 없음.
- 안전 검토: 런타임 메모리/스레드/성능/참조 동작을 변경하지 않음. 참고본은 숫자·불리언 형식을 보존하고 실행 리소스에 추가하지 않음. 적용 범위는 문서·Python 도구뿐.

## 2026-10-01 후속: 전투 정보 v3 / 유물 제거 / 몬스터 특성 자리

- 요청: 유물 전체 제외, 몬스터 행동 enum용 필드만 추가, 전투에 필요한 현재 정보만 간결하게 유지.
- game-data.en.json을 GameInformation v3 루트 구조로 축소. progression/entities는 별도 progression-config.en.json으로 분리하고 fieldHelp/implementation은 실행 문서에서 제거.
- 유물 배열·조각·강화·효과·탭·드롭·보스 조각 보상 제거. 새 일반 유물 시스템은 미정으로 남김.
- behaviorType=0과 MonsterBehaviorType.None 추가. DTO → 런 스냅샷 → 생성 스냅샷 → 몬스터 개체까지 전달만 구현. 이동/은신/무적 동작은 없음.
- 저장 version=3. 정상 첫 저장 전에 기존 v1/v2 원문을 cursor_hunter.progression.v1.before-v3에 1회 백업. 유물 데이터는 플레이어 상태로 복원하지 않으며 젬으로 임의 환전하지 않음.
- 일반 강화·지갑 유지. 기존 유물 보정이 제거되므로 최종 공격력·보스 배율·연동 스킬 피해가 낮아질 수 있음.
- 한국어 두 참고본 재생성, v3 예제/스키마와 성장 설정 스키마 추가. 이전 v2 자료는 과거 기록임을 명시.
- Python 12개 테스트와 JSON/노드/재화/에셋 연결/C# 구문/diff 검증. 저장 백업·분리 로더·enum 계약 Unity Editor 테스트는 추가만 하며 실행하지 않음.
- 기존 저장 실패 시 메모리 롤백/영속 RunId journal의 한계는 유지. 이번 백업은 원자 저장을 의미하지 않음.
- 실행 금지 준수: 게임/Unity/Editor 테스트를 실행하지 않았고 사용자 PlayerPrefs도 읽거나 변경하지 않음.

## 2026-10-01 · 게임 버전 0.0.5

- Unity PlayerSettings.bundleVersion을 0.0.5로 지정하고 협업 README·최신 인수인계에 게임 버전을 명시했다.
- 노드형 성장 UI, 자동 반경/쿨타임, 몬스터 10종, TMP·화질 설정, 전투 정보/성장 설정 분리, 유물 제거와 behaviorType 예약 필드의 누적 변경을 함께 커밋한다.
- 전투 JSON schemaVersion=3, 저장 version=3은 그대로 유지한다. 문서 파일명의 v0.0.4는 기존 링크와 역사적 본문 보존을 위해 변경하지 않는다.
- 검증: Python 테스트 12개, 한영 참고본·스키마·참조·협업 경계·C# 구문·diff 검사. Unity/게임/Editor 테스트는 실행하지 않는다.

## 2026-10-05 · 일반 몬스터 체력바 기본 숨김

- `MonsterRoot.prefab`의 `showHealthBar`를 끄고, 생성·피격 때 체력바를 표시하는 경로가 이 설정을 따르도록 했다. 전투 HP 계산과 피해 판정은 그대로 유지한다.
- `python3 tools/validate_collaboration.py`와 `git diff --check` 통과. Unity Import·컴파일·Play Mode 시각 검증은 수행하지 않았다.

## 2026-10-05 · 몬스터 이동 애니메이션 연결

- `MonsterBehaviorController`가 실제 위치 변화로 이동 여부를 판정하고, `MonsterCombatTarget`이 Animator의 `isMoving` Bool과 Idle/Walk 상태를 갱신한다. Hit/Dead 중에는 이동 상태 전환을 미루고, 일시정지·런 종료·사망 시 Bool을 끈다.
- 공급사 Walker 컨트롤러 원본은 수정하지 않았다. 디스크에 저장된 원본에는 아직 `isMoving` 파라미터가 없어서, Idle/Walk 스테이트를 직접 전환하는 경로도 넣었다.
- `python3 tools/validate_collaboration.py`와 `git diff --check` 통과. 이 프로젝트의 Unity 컴파일·Play Mode는 확인하지 않았다.

## 2026-10-05 · 몬스터 이동 방향에 맞춘 좌우 반전

- `MonsterBehaviorController`가 수평 이동 방향을 기준으로 `VisualRoot`만 좌우 반전한다. 경계에서 되돌아올 때는 반사된 속도 방향을 사용하고, 정지·수직 이동 중에는 마지막 방향을 유지한다.
- `HitArea`와 공급사 프리팹 원본은 변경하지 않았다.
- `python3 tools/validate_collaboration.py`와 `git diff --check` 통과. 이 프로젝트의 Unity 컴파일·Play Mode 시각 검증은 수행하지 않았다.

## 2026-10-05 · 2초 간격 몬스터 행동 패턴

- 각 몬스터가 생성 시와 이후 2초마다 정지·이동을 같은 확률로 선택한다. 이동을 다시 선택하면 새 이동 방향을 고르고, 경계 반사와 원형 이동 모드는 유지한다.
- 몬스터별 독립 시드 난수를 사용하고, 런 시계로 시간을 재어 일시정지·오래된 RunId가 행동 시간을 진행시키지 않도록 한다. `Stationary` 프로필은 계속 정지한다.
- `python3 tools/validate_collaboration.py`와 `git diff --check` 통과. 이 프로젝트의 Unity 컴파일·Play Mode는 확인하지 않았다.

## 2026-10-05 · 정지 행동의 Idle 전환 보강

- 정지 선택 시 Animator가 Walk에 있으면 Idle로 전환하고, Idle→Walk 전환 중 정지가 선택돼도 Idle로 되돌린다. Hit/Dead로의 전환은 유지한다.
- 이전 변경에서 어긋난 `ConfigureMovement`의 시드 인자 타입을 호출부와 맞췄다.
- `python3 tools/validate_collaboration.py`와 `git diff --check` 통과. 이 프로젝트의 Unity 컴파일·Play Mode는 확인하지 않았다.

## 2026-10-05 · 몬스터 좌우 바라보기 반전 수정

- Walker 비주얼의 기본 방향이 왼쪽인 점에 맞춰 오른쪽 이동에서 `VisualRoot`의 X 배율을 음수로, 왼쪽 이동에서 양수로 바꿨다.
- 경계 반사 프레임에도 실제 X 위치 변화를 기준으로 방향을 정하고, 새 이동 행동을 고른 직후 아직 움직이기 전에는 바라보는 방향을 바꾸지 않는다.
- `python3 tools/validate_collaboration.py`와 `git diff --check` 통과. 이 프로젝트의 Unity 컴파일·Play Mode는 확인하지 않았다.

## 2026-10-05 · 몬스터 피격 중 이동 정지

- 피격 시 즉시 `isMoving`을 끄고 Hit 상태가 있으면 재생한다. Hit 클립 길이 동안 실제 위치 이동을 생략하며, 연속 피격 시 정지 시간을 마지막 피격 기준으로 연장한다.
- 피격 정지 중의 반복적인 이동 상태 갱신이 Hit 애니메이션을 Idle/Walk로 덮어쓰지 않도록 막았다.
- 기존 2초 정지·이동 행동 선택 시계는 유지하고, 사망 시에는 기존 사망 처리에 따라 영구적으로 이동을 멈춘다.
- `python3 tools/validate_collaboration.py`와 `git diff --check` 통과. 이 프로젝트의 Unity 컴파일·Play Mode는 확인하지 않았다.

## 2026-10-05 · 몬스터 에셋 Animator Controller 통일

- `EnemyMonster 2`의 17종 컨트롤러에 Walker 기준 `hit` Trigger, `dead`·`isMoving` Bool, Idle↔Walk, Any State→Hit/Dead, Hit→Idle 전이를 구성했다. Walker에 빠져 있던 Walk→Idle 전이도 추가했다.
- 종별 5개 애니메이션 클립과 기존 재생 속도, 컨트롤러 GUID를 유지했다. 50개 프리팹이 각 종 컨트롤러를 계속 참조하는지 확인했다.
- 사용자 요청에 따라 이번에는 공급사 컨트롤러 원본을 직접 수정했다. 17개 컨트롤러의 상태·전이·파라미터·클립 GUID와 50개 프리팹 연결 구조 검사, `python3 tools/validate_collaboration.py`, `git diff --check`를 통과했다. Unity Import·Play Mode는 아직 확인하지 않았다.

## 2026-10-05 · 몬스터 Definition별 비주얼 프리팹 연결

- `Monster01~10.asset`의 `visualPrefab`을 기존 `prefabKey`가 지목하던 종별 프리팹으로 연결했다. Monster01의 Walker Stump 연결은 유지하고, 나머지 9개를 각자 다른 외형으로 교체했다.
- 10개 참조 모두 프리팹의 GUID와 루트 GameObject fileID가 일치하고 서로 중복되지 않는지 검사했다. 프로토타입 `SlimeDefinition.asset`은 변경하지 않았다.
- `python3 tools/validate_collaboration.py`와 `git diff --check` 통과. Unity Import·Play Mode는 확인하지 않았다.

## 2026-10-05 · 몬스터 사망 연출 제거 시점 정렬

- 17종 Dead 클립은 길이가 1.5초지만 `Dead` 시각 파츠가 1.167초에 꺼진다. 기존 클립 길이 타이머와 Walker의 1.483초 이벤트는 화면에서 사라진 뒤에도 오브젝트를 남겼다.
- 공용 `MonsterRoot`의 제거 기준을 Dead 상태 정규화 시간 0.7777778로 설정하고, `MonsterCombatTarget`은 Animator의 실제 진행률로 제거한다. 기존 이벤트가 연출 종료 전에 오더라도 제거하지 않으며, Animator 상태가 없어졌을 때만 클립 길이 타이머를 사용한다.
- 체력 0 판정과 보상 집계는 타격 시점 그대로이고, 시각 오브젝트 제거만 연출과 맞춘다. 공급사 애니메이션 클립은 변경하지 않았다.
- 17개 클립의 종료 키프레임과 프리팹 제거 비율 검사, `python3 tools/validate_collaboration.py`, `git diff --check` 통과. 접근되는 Unity 창은 다른 프로젝트여서 이 프로젝트의 Unity 컴파일·Play Mode는 확인하지 않았다.

## 2026-10-05 · 커서 이동 경로와 몬스터별 기본 공격 쿨타임

- `CombatRunController`의 공용 마지막 공격 시각을 없애고, 기본 공격 피해가 실제 적용된 몬스터 인스턴스별 시각을 저장한다. 쿨타임 길이는 기존 `PlayerCombatStatsRuntime` 값을 공통으로 읽는다. 빈 공간·쿨타임 중인 몬스터는 다른 몬스터의 타격 기회를 소모하지 않는다.
- 원형 커서의 이전·현재 위치 사이를 CircleCast하고 현재 위치를 OverlapCircle로 검사한다. 가변 결과 목록과 몬스터 중복 제거를 사용해 빠른 이동 경로의 여러 적을 같은 프레임에 타격할 수 있다.
- `CursorAttackController`는 몬스터 이동 이후 LateUpdate에서 샘플링한다. UI 포인터, 화면 밖, 포커스 상실, 일시정지, 자동 공격 해제, 런 종료 때 경로를 끊고, 테스트 버튼은 현재 위치만 판정한다. 스킬 타이머와 판정은 유지한다.
- App EditMode 테스트에 다중 타격, 타격 시각이 다른 두 몬스터의 독립 쿨타임, 경로 초기화/현재 위치 전용 판정을 추가했다.
- Unity 생성 프로젝트의 참조를 이용한 Roslyn C# 컴파일에서 Combat·App·App.Tests 3개 어셈블리가 통과했다. `python3 tools/validate_collaboration.py`와 `git diff --check`도 통과했다. 이 프로젝트의 Unity Test Runner·Play Mode는 실행하지 않았다.

## 2026-10-05 · 일부 몬스터 피격·사망 연출 누락 방지

- `MonsterCombatTarget`의 사망 직후 이동 상태 갱신이 이전 Walk 상태를 보고 Idle을 강제로 재생하면, 아직 평가되지 않은 Dead 재생 요청을 덮을 수 있었다. 사망 후에는 `isMoving` 값만 끄고 Idle/Walk 직접 전환을 하지 않도록 했다.
- 17종 Animator Controller의 Hit→Idle 전이가 모두 Exit Time 0으로 설정돼 있었다. Hit 클립이 끝난 뒤에 돌아오도록 Exit Time 1로 수정했다. Dead 클립과 제거 기준은 유지했다.
- 17종 전이 값 검사, Combat·App·App.Tests Roslyn C# 컴파일, 협업 경계 검사와 diff 공백 검사를 통과했다. 올바른 프로젝트의 Unity Play Mode는 이 환경에서 접근되지 않아 재현·시각 검증은 하지 못했다.

## 2026-10-05 · 인스펙터 기반 전투 스테이지 스폰 설정

- `CombatStageDefinition`에 몬스터별 스폰 허용과 생산 특성 단계(기본·2·3·4 또는 구매한 단계 사용)를 추가했다. 허용 여부와 명시한 생산 단계는 해당 런에만 적용하고 플레이어 구매·저장은 변경하지 않는다.
- `Stage01.asset`에 Monster01~10을 등록하고 처음에는 Monster01만 허용했다. Main의 기존 진입 버튼은 `HuntManager.Default Stage`를 통해 이 스테이지로 들어간다. 다른 스테이지 에셋은 `BeginStageRun`에 전달할 수 있다.
- 런 시작 시 스테이지 설정을 게임 정보 JSON과 스폰 계획에 함께 반영해 전투 중 전체 정보 화면과 실제 등장 몬스터가 일치하게 했다. 중복 몬스터, 빈 허용 목록, 누락된 비주얼은 진입 전에 거부한다.
- App·App.Tests Roslyn C# 컴파일, 스테이지 에셋 참조, `python3 tools/validate_collaboration.py`, `git diff --check`를 통과했다. Unity Test Runner와 Play Mode는 실행하지 않았다.

## 2026-10-05 · 전투 필드 바닥의 그리드 경계 숨김

- `field_a.png`의 둘레 1픽셀이 투명해 타일 반복 시 필드 전체에 격자 경계가 드러나는 것을 확인했다. 공급사 원본은 수정하지 않았다.
- Main 씬의 `field_a` SpriteRenderer를 단일 표시로 바꾸고 카메라 바깥까지 넓혀, 반복 경계와 외곽의 투명 테두리가 화면에 보이지 않게 했다. 원본의 실제 색상 영역은 단색이어서 확대해도 바닥 무늬가 흐려지는 문제는 없다.
- `python3 tools/validate_collaboration.py`와 `git diff --check`로 정적 검증했다. 접근 가능한 Unity 창은 다른 프로젝트여서 Cursor Hunter의 Game View 시각 검증은 수행하지 않았다.


### 2026-10-10 · Combat — 풀링 월드 데미지 텍스트

- `CombatRunController.ApplyBundle`의 정상 피해에서 `CombatDamageApplied` 이벤트를
  발행한다. RunId·대상·실제 HP 감소량·치명타 여부·피해 전 위치를 담는다.
  위치는 Combat 내부 `IDamageTextAnchor`로 받으므로 처치 즉시 제거되는 대상도 표시할 수 있다.
  시작/종료/중단은 `PresentationReset`으로 표시를 정리한다. 기존 `CriticalHit` 이벤트는 유지한다.
- `Combat/Runtime/DamageTextManager`가 하나의 업데이트 루프로 연출을 진행한다.
  128개 사전 준비, 최대 256개, 0.8초 수명, 네 가지 상승/드리프트/크기 변형이 기본값이다.
  용량은 초기화 때 고정된다. 풀은 몬스터와 별개인 `CombatManager/DamageTextPool` 아래에 둔다.
- 같은 대상·같은 히트 종류의 생성 후 0.1초 이내 피해는 합산한다. 합산 시 수명/궤적은
  다시 시작하지 않으며 Int64 합산 overflow는 새 표시로 분리한다. 포화 시 가장 오래된 일반
  표시를 재사용한다. 모두 크리티컬이면 일반 표시를 생략하고 새 크리티컬은 가장 오래된
  크리티컬을 재사용한다. 전투 피해/보상 수치는 변경하지 않는다.
- `DamageText_Root`의 `Normal Hit Text` / `Critical Hit Text`에 두 TMP 자식을 연결했다.
  기존 `damageText` 참조는 `FormerlySerializedAs`로 이전하고 기존 script/prefab GUID를 유지한다.
  두 자식의 Font Asset/Material Preset을 인스펙터에서 각각 지정한다. 현재 폰트 참조는 유지했고,
  새 크리티컬 자식에는 기존 자식을 복제한 참조가 들어 있다. 코드가 폰트나 재질을 바꾸지 않는다.
- 사용자 추가 요청으로 피해 숫자는 폰트와 무관하게 항상 구분자 없이 표시한다(예: `12312591245`).
  최대 19자리 Int64를 반올림 없이 문자 버퍼에 직접 기록한다.
  슬롯별 문자 버퍼와 양쪽 스타일의 최대 길이 메시를 사전 준비하고 페이드는 정점 알파만 갱신한다.
- 프리팹 자식 원점/크기/줄바꿈과 sorting order 300을 정리했다. TMP의 월드 단위 스케일 설정은
  유지한다. `UI/Scripts/DamageText/CursorHunter.Combat.asmref`로 기존 위치의 view를 Combat에 편입하고
  이전 Assembly-CSharp 이름은 `MovedFrom`으로 명시했다. Combat에 TMP/UI 패키지 참조만 추가했다.
- `AppTextBootstrap`은 DamageText_Root 자식의 폰트를 덮어쓰지 않는다. `PrototypeRunHud`의
  중복 중앙 크리티컬 표시와 고정 ×2 문구를 제거했다.
- CombatSandbox 씬 파일은 없어서 새 씬을 만들지 않았다. Main의 기존 사용자 변경 위에
  CombatManager 컴포넌트 한 개와 직렬화 설정만 추가했다. 공급사 프리팹의 기존 변경은 건드리지 않았다.
- `DamageTextManagerEditor`에서 생성/활성/최고 활성/합산/조기 재사용/일반 생략 수를 읽기 전용으로
  확인할 수 있다. `Combat/Tests/DamageTextTests.cs`에 10개 EditMode 검사를 추가했다.
  1만 요청의 용량 상한, 합산 범위·수명, 치명타 우선, Int64, 일시정지, 런 전환, 치명타 스킬,
  처치 위치, 폰트/재질·페이드 재사용을 다룬다.
- 검증: `python3 tools/validate_collaboration.py` 통과. 설치된 Unity 참조 DLL을 사용한 외부 Roslyn
  컴파일에서 Combat/App/Combat.Editor/Combat.Tests 오류 없음. 프리팹 내부 참조와 Main의 기존 변경
  보존을 정적으로 확인했다. Unity 배치 EditMode 실행은 같은 프로젝트를 연 에디터가 있어 거절됐다.
  10개 검사는 작성/컴파일만 완료했으며 실행 성공을 주장하지 않는다. Unity Import/에디터 컴파일,
  실제 화면 크기·배치·프레임 성능·GC는 에디터에서 후속 확인이 필요하다.
