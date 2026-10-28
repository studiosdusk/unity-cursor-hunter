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

## 2026-10-08 · 전체 화면 통합 특성 연구 보드

- `TraitScreenController`를 단일 전체 화면 보드로 전환했다. 중앙 공격 코어에서 기본 스탯이 동·서·남·북으로 시작하고, 그 바깥은 선행 조건·보스 티어·재화 티어·비용 순으로 배치한다. 통계·스킬·몬스터 노드와 기존 커서 오라 펫 4개를 한 보드에서 섞어 표시하며, 네모 노드는 동서남북 직선 커넥터로 연결한다.
- 상시 상단 요약 및 우측 선택 상세·탭 패널을 없애고, 전투 HUD에서 쓰는 전체 정보 팝업은 기존처럼 유지했다. 보유 재화 HUD, 확대/축소, 중앙 맞춤, 드래그 탐색은 화면 위에 겹쳐 배치한다.
- 노드 호버 말풍선에서 설명·효과·필요 재화·보유량·구매 상태와 강화/초기화 액션을 제공한다. 구매 완료·구매 가능·재화 부족·선행 잠금을 굵은 상태 테두리, 배지, 아이콘으로 구분하고, 호버 확대와 해금/강화 펄스 이펙트를 추가했다.
- 배경에는 기존 `field_a.png` 타일과 Main 전장을 재사용하고 남색 셰이드를 더했다. 구매 ID와 전투 데이터의 펫 `skill.cursorAura` 계약은 유지했다. 씬 미리보기용 임시 `initialScreen` 수정은 되돌렸다.
- `git diff --check`, `python3 tools/validate_collaboration.py`, `python3 tools/open_unity.py --check` 통과. 설치된 편집기와 프로젝트 잠금은 핀 버전 6000.3.13f1을 가리킨다. 이번 시점의 Unity 컴파일·Play Mode는 확인하지 않았다. Editor 캡처 API가 화면 이미지를 제공하지 않았고, 공유 `Editor.log`는 다른 6000.4.7f1 인스턴스를 가리켜 현재 소스의 컴파일 여부를 판정할 수 없다.

## 2026-10-08 · 특성 화면 컴파일 문법 오류 수정

- `BuildNodePanel()`에서 `CreatePanel()` 호출의 색상과 피벗 인자가 빠져 발생한 CS1003/CS1026 문법 오류를 수정했다. 투명 패널 색과 중앙 피벗을 명시했다.
- `git diff --check`, `python3 tools/validate_collaboration.py` 통과. 이 수정 이후 Unity 재컴파일 여부는 Editor 화면 접근이 불가능해 확인하지 못했다.

## 2026-10-08 · 연구 트리 구매 안내·테스트 도구와 정보 화면 개선

- 노드를 클릭하면 설명 말풍선을 고정하고 해금/강화 버튼을 바로 누를 수 있게 했다. 구매 후 잠금에서 구매 가능·재화 부족으로 바뀐 다음 노드는 테두리 펄스와 파편으로 표시하고, 새로 열린 항목 이름을 상태 메시지에 보여 준다.
- 마우스 휠 확대, 드래그 이동, 드래그 중 손바닥 커서를 추가하고 상단 줌 버튼을 제거했다. 통합 보드는 그대로 유지하고 상단에 유물 기획용 비활성 탭을 추가했다.
- 테스트 도구에서 임의 수량의 젬스톤을 종류별/전체 지급할 수 있다. 별도로 잠금 조건 해제와 전체 노드 해금·강화를 제공하며, 전체 초기화로 구매 상태를 되돌린다.
- 전체 정보 팝업은 스탯·전투 규칙·젬스톤·몬스터·스킬·펫·전체 노드 상태를 한국어로 표시한다. 전투 정보 JSON과 현재 진행 설정/구매 ID가 포함된 전체 JSON을 각각 복사할 수 있다. 실행 데이터의 영어 ID와 필드명은 유지한다.
- Unity 6000.3.13f1의 Roslyn 참조로 Progression 어셈블리 소스를 컴파일했다. 기존 `TMP_Text.enableWordWrapping` 경고도 새 API로 바꿔 오류·경고 없이 컴파일됐다. `python3 tools/validate_collaboration.py`, `python3 tools/open_unity.py --check`, `git diff --check`, Localization JSON 파싱도 통과했다. Unity Editor Import·Play Mode는 실행하지 않았다.

## 2026-10-08 · 통합 연구 보드 기본 화면과 구매 버튼 가시성 수정

- 보드를 다시 열 때 저장된 확대 상태가 유지되어 다수 노드가 화면 밖에 남던 문제를 수정했다. 캔버스 레이아웃이 잡힌 뒤 전체 111개 노드에 맞춰 자동 축소하고, 마우스 휠의 최소 확대율도 지도 전체를 볼 수 있게 낮췄다.
- 연구 헤더에 노드 총수와 조작 안내를 표시한다. 툴팁 구매 버튼을 카드 안쪽으로 옮기고, 구매 가능 노드는 `스킬 해금하기`·`몬스터 해금하기`·`강화하기`처럼 실제 동작을 밝은 버튼에 쓴다. 이미 해금된 기본값, 젬 부족, 선행 조건 잠금은 각각의 이유를 버튼과 상태 문구에 표시한다.
- 구매 가능한 노드의 `+` 배지를 확대했다. 사용자는 노드를 선택한 뒤 툴팁 아래의 강조 버튼을 누르며, 우측 상단 `테스트 도구`에서 재화 지급과 전체 잠금 해제도 사용할 수 있다.
- `python3 tools/validate_collaboration.py`, `git diff --check`, Localization JSON 파싱, Unity 6000.3.13f1 참조 기반 Progression Roslyn 컴파일(오류·경고 없음)을 통과했다. Unity Editor Play Mode 및 실제 Game View 시각 확인은 수행하지 않았다.

## 2026-10-08 · 중앙 시작점 기반 연구 진행과 전체 초기화

- 초기 진행도와 전체 초기화 시 구매 목록을 비운다. 중앙 `stat.attack.01`은 강화 완료가 아닌 무료 연구 시작점으로 표시하고, 나머지 노드는 코어를 활성화하기 전까지 잠근다.
- 네 기본 스탯 선택지와 스킬·몬스터·보스 스탯의 최상위 분기를 중앙 시작점에 연결했다. 코어 구매 뒤 첫 분기들이 구매 가능 또는 재화 부족 상태로 나타나며, 이후에는 각 노드의 기존 선행 조건을 따른다.
- 게임 전투 기본값으로 활성인 Fireball·Monster01이 진행도 구매로 잘못 간주되지 않도록 `startsUnlocked`만 명시적 시작 구매로 처리한다. v1~v3 저장에서 암묵적으로 구매된 시작 노드는 v4 마이그레이션에서 제외하고, 유료 구매·잔액은 유지한다. 기존 v3 저장은 첫 v4 저장 전에 `.before-v4`로 백업한다.
- 전체 초기화는 사용자를 중앙 시작점에 선택 상태로 보내고, 상태 안내도 시작점 기준으로 바꿨다. progression JSON schema/111개 루트 검사, `python3 tools/validate_collaboration.py`, `git diff --check`, Unity 6000.3.13f1 Progression Roslyn 컴파일(오류·경고 없음)을 통과했다. 통합 bilingual validator는 game-data 한국어 참고본이 오래됐다는 문서 assertion에서 멈췄으며, Unity Play Mode 시각 검증은 수행하지 않았다.

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

### 2026-10-10 · Combat — 커서 랜덤 히트 이펙트

- 사용자 지정 `Combat/Art/MouseAttack_Prefab/MouseAttack_0~3` 중 성공한 기본 타격마다
  25% 확률로 하나를 재생한다. 연속 중복은 허용한다. 사용자가 이번에 지정한 에셋이
  이전 문서의 히트 에셋 후보와 검기 제외 지침보다 우선한다. 원본 프리팹·공급사 재질은 보존했다.
- `CombatDamageApplied`에 명시적인 `CombatDamageSource`, 몸체 좌표·크기를 추가했다.
  기본 공격과 스킬 생산자를 함께 수정했고, 기존 6인자 생성자와 데미지 숫자의 좌표는 유지한다.
  `IHitEffectAnchor`/MonsterRoot의 캐시된 HitArea로 피해 전 bounds를 복사하므로 처치 후에도
  좌표가 유효하다. 이벤트 소비자는 이미 죽은 대상의 활성 상태를 다시 확인하지 않는다.
- `CursorHitEffectManager`는 네 원본을 시작 때 한 번씩 복제한다. 자동 방출을 끄고
  월드 공간에서 파티클 하나씩 방출하며, 수명은 기본 0.5초다. 타격 중 GameObject 생성/파괴,
  코루틴, 재질 복제, 컴포넌트 검색을 하지 않는다. 별도 SeededRandom 인스턴스로 전투·Unity 난수와 분리한다.
- 전체 표시 상한은 256개이며, 4종 각각은 전체 상한을 수용할 수 있게 초기화한다.
  무작위 선택 편중으로 다른 이펙트로 대체하지 않는다. 포화 시 추가 표시만 생략한다.
  기본 크기는 HitArea 최대 변 ×1.2, 0.5~2.5 월드 단위로 제한한다. sorting order는 200이다.
- `Prefabs/Vfx/CursorHitEffects.prefab`이 원본 4종과 Combat 소유 URP Particles/Unlit
  투명 재질을 참조한다. Main의 CombatManager 자식 프리팹으로 연결했다.
  전투 pause와 별도로 파티클을 Pause/Play하고, PresentationReset·비활성화·런 변경 때
  정리한다. Inspector에는 활성·최고 활성·방출·생략 수를 표시한다.
- `CombatSandbox.unity`를 추가했다. 자체 카메라, 고정 스냅샷 8개/80개 몸체, 자동 타격,
  0.05초 부하 모드, 정지·재개·재시작 패널이 있다. Main 빌드 목록에는 추가하지 않았다.
- 검증: Unity 6000.3.13f1의 별도 임시 프로젝트에 관련 코드·에셋을 복사하여 EditMode
  19개(기존 데미지 숫자 10개 + 히트 이펙트 9개)와 PlayMode 1개 모두 통과했다.
  1만 건 요청의 총량 상한, 네 시스템 재사용, 이동 경로·중복 콜라이더·쿨타임·시간 만료,
  치명타·스킬 구분, 처치 전 좌표, 난수 분리, 준비 후 128건 표시 요청의 관리 힙 할당 0B를 확인했다.
  Metal/URP Sandbox 카메라 렌더링을 PNG로 확인했다. 실제 비활성화 콜백은 PlayMode에서 검증한다.
- Combat/App/Combat.Editor/Combat.Tests 외부 Roslyn 컴파일, 협업 구조 검사와 diff 검사를 통과했다.
  Main 전체 플레이와 대상 기기의 프레임 시간/오버드로 프로파일링은 아직 수행하지 않았다.
  Sandbox IMGUI 패널은 개발용으로 문자열을 할당하므로 타격 경로의 GC와 구분한다.

### 2026-10-10 · Combat — 히트 이펙트 종류 제거 후 전체 표시 중단 수정

- 사용자가 Main에서 MouseAttack_0 프리팹·재질을 함께 제거했지만, 매니저의 4종 필수
  검증 때문에 초기화가 중단됐다. 유효한 프리팹·재질 쌍만 초기화하고 해당 개수에서
  랜덤 선택하도록 수정했다. 정지·재개·정리 루프도 실제 생성 개수를 사용한다.
- 같은 인덱스의 프리팹 또는 재질이 없으면 해당 항목만 제외한다. 유효한 쌍이 없으면
  표시를 생성하지 않으며, Inspector에 경고와 Configured Variants 수를 표시한다.
  목록 변경은 Play 재시작 때 반영된다. 사용자 Main의 1·2·3번 구성과 크기 설정은 보존했다.
- 3종·단일 종류·빈 항목·짧은 재질 목록·전체 빈 목록 회귀 검사를 추가했다.
  격리한 Unity 프로젝트의 EditMode 25개를 통과했으며, 남은 종류의 재생·재질 대응,
  정지·재개·종료, 준비 후 타격 경로의 관리 힙 할당 0B를 확인했다.
- 기존 Sandbox PlayMode 1개도 통과했다. 협업 경계 검사와 변경 범위의 공백 검사,
  Main 씬 파일의 수정 전후 SHA-256 일치를 확인했다.

### 2026-10-10 · Combat — 히트 이펙트 배열 편집 유연화

- `CursorHitEffectManager`의 별도 프리팹·재질 배열을 `Effect Variants` 하나로 통합했다.
  각 항목에 Prefab과 Material Override가 함께 있으므로 추가·삭제·재정렬할 때 쌍이 유지된다.
  Override가 없으면 프리팹 재질을 사용하고, 비어 있거나 지원하지 않는 항목은 제외한다.
  기존과 같이 자식 없는 단일 ParticleSystem을 지원하며 재질은 URP 호환이어야 한다.
- Play 중 Inspector 변경은 다음 Update에서 반영한다. 기존 파티클과 공유 시스템을
  정리한 후 다시 준비하고, 전체 삭제 시 표시를 끄며 다시 추가하면 구독·재생을 복구한다.
  정지 상태를 유지하고 이전 시스템·중복 구독이 남지 않도록 했다. 재구성은 타격 경로에서 실행하지 않는다.
- 래퍼 프리팹과 Main의 직렬화 경로를 새 배열로 이전했다. 사용자 Main의 1·2·3번 연결,
  크기 배율 1.5/최소 2/최대 3 및 관련 없는 씬 내용은 보존했다. 이미 열려 있는 이전 씬은
  숨긴 이전 필드를 역직렬화 시 한 번 이전하고 비워, 전체 삭제 후 옛 목록이 복원되지 않는다.
- Unity 6000.3.13f1 격리 프로젝트에서 EditMode 27개와 PlayMode 2개를 통과했다.
  6개·3개·1개·빈 목록, 순서 변경, 빈 항목, 재질 기본값, 이전 설정 변환, 실행 중
  정지/비활성 상태에서 변경과 빈 목록 복구, 시스템 정리와 구독 1회를 검증했다.
  타격 경로의 관리 힙 할당 0B, 협업 경계 검사, 코드·문서 공백 검사도 통과했다.

### 2026-10-10 · App — 커서 스킨 선택과 범위 이미지 분리

- Main의 `Managers > CursorManager`에 `CursorSkinController`를 추가했다.
  `Selected Skin` 드롭다운에서 `cursor_image`와 `Joystick_Skill01_Fill_White`를
  선택하며, 기본값은 새 Joystick 이미지다. 편집/Play 모드에서 선택을 반영한다.
- `App/Art/CursorSkins`에 두 `CursorSkinDefinition` 에셋을 만들었다. 스킨마다
  고유 ID, Sprite, 범위 원 지름 비율을 저장하고 Available Skins 목록으로 확장한다.
  인게임 UI는 `TrySelectSkin(string)` 또는 UnityEvent용 `SelectSkin(string)`을
  호출한다. 목록 순서와 무관하게 ID로 선택하며 중복/없는 ID·누락 Sprite는 거부한다.
  실제 스킨 선택 화면과 영구 저장은 이번 범위에 포함하지 않았다.
- `cursor_image`의 기존 CircleCollider2D를 유지하고 SpriteRenderer를 자식
  `SkinVisual`로 옮겼다. 자식의 크기와 중심만 보정하므로 이미지 해상도/PPU/pivot과
  무관하게 범위 원이 판정에 맞고 기존 범위 강화 배율을 함께 따른다.
  이동·표시·공격 로직과 공급사 이미지·재질은 유지했다. 기존 사용자 씬 수정도 보존했다.
- Unity 6000.3.13f1 격리 프로젝트에서 두 실제 이미지 에셋과 커서 스킨 Runtime/Editor를
  임포트/컴파일하고 EditMode 7개를 모두 통과했다. 실제 이미지 교체·콜라이더 보존,
  크기/피벗 보정·확대, 추가/재정렬, 무효 ID, 숨김 상태, 잘못된 렌더러 연결을 확인했다.
  전체 App 외부 Roslyn 컴파일 및 협업 구조 검사도 통과했다.
  Main 씬의 변경 전후 YAML 블록 비교로 콜라이더와 관련 없는 모든 블록의 일치를 확인했다.
  실제 Main 전체 플레이와 화면 렌더링은 이번 검증에 포함하지 않았다.

### 2026-10-10 · App — 커서 이미지 호흡 연출

- `CursorSkinController`가 Play 중 스킨의 기본 표시 크기에 0.95~1.05배 사인파를
  적용한다. 한 왕복은 기본 2초이며 축소·확대의 양 끝에서 부드럽게 방향을 바꾼다.
  Inspector의 Breathing Enabled/Min Scale/Max Scale/Period Seconds로 조절한다.
- 스킨 전환·범위 강화 때도 보정된 기본 크기에 배율을 적용하므로 누적 확대가 없다.
  피벗 보정에도 같은 배율을 사용해 이미지 중심을 유지하며 콜라이더는 수정하지 않는다.
  시간 배율과 무관하게 재생하고 편집 모드·컴포넌트 비활성화 때는 기본 크기로 돌아온다.
- Main에 기본 연출 값을 명시했다. 이번 씬 변경은 스킨 컨트롤러 설정 네 필드뿐이며,
  변경 전후 YAML 비교로 나머지 사용자 씬 데이터가 동일함을 확인했다.
- Unity 6000.3.13f1 격리 프로젝트의 컴파일 및 기존 EditMode 회귀 테스트 7개,
  협업 구조 검사와 코드·문서 공백 검사를 통과했다. 실제 Main의 Play/시각 검증은 미실행이다.

### 2026-10-12 · Combat — 몬스터 몸체에 비례하는 피격 이펙트 크기

- `MonsterCombatTarget.TryGetHitEffectBounds`가 생성 시 캐시한 몸체 SpriteRenderer의
  현재 월드 bounds를 사용한다. 체력바·비활성 렌더러는 제외하고, 스프라이트가 없는
  대상만 기존 HitArea bounds로 대체한다. 크기와 중심은 피해 적용 전에 복사하므로
  처치 타격에도 유효하며, 타격 중 배열 할당이나 컴포넌트 검색을 하지 않는다.
- `CursorHitEffectManager`의 크기는 몸체의 가로·세로 중 긴 변 × `Size Multiplier`다.
  절대 크기 최소·최대 제한을 제거해 작은 몬스터와 큰 몬스터에 동일한 비율을 적용한다.
  래퍼 프리팹 및 Main의 제거된 필드도 정리했고, Main의 기존 배율 2와 종류 구성은 유지했다.
- Unity 6000.3.13f1 격리 프로젝트에서 EditMode 34개와 PlayMode 2개를 통과했다.
  추가한 7개 검사는 0.1/0.62/1/2/4배 몸체, 뒤집힌 부모 스케일, 체력바 제외,
  처치 전 bounds 보존 및 스프라이트 없는 대상의 대체 처리를 확인한다.
  기존 풀 재사용·수명주기·난수 분리·관리 힙 할당 0B 검사도 통과했다.
  `python3 tools/validate_collaboration.py`, `git diff --check` 통과.
  배치 테스트는 화면 없이 실행했으며 실제 Main의 시각 검증은 수행하지 않았다.

### 2026-10-14 · Combat — 헤드샷 자동 스킬 코어

- `skill.headshot` 컨트롤러와 풀링 효과 프리팹을 Main의 `CombatManager` 및 CombatSandbox에 배치했다. 지정한 `FX_Aim_Explode_Visual`을 프로젝트 소유 효과 프리팹에 중첩했고, 원본의 `HitBox`를 한 번의 폭발 범위 판정에 사용한다.
- 적이 없으면 쿨타임을 소비하지 않고 대기한다. 지목한 적을 폭발 전까지 추적하고 사라지면 마지막 위치에서 폭발한다. 주 대상은 일반전에서 남은 체력만큼의 원시 피해, 보스전에서 기본 공격력 10배의 원시 피해를 받는다. 주변 적에게는 기본 공격력 2배를 한 번 적용한다. 실제 방어·치명타·보스 배율·보상은 기존 공통 피해 경로를 사용한다.
- 일시정지와 런 종료 시 발동·피해·파티클을 멈추고, 이전 런의 효과는 반환한다. 에디터 `Skill Acquired`만 연결했으며 실제 구매 노드·강화는 범위 밖이다. 사용자가 Main에 놓은 별도 원본 VFX 인스턴스는 보존하고 재생만 껐다.
- 격리 Unity 6000.3.13f1 프로젝트에서 Combat PlayMode 8개를 통과했다. `python3 tools/validate_collaboration.py`와 `git diff --check`도 통과했다. 원본 프로젝트는 다른 Unity 인스턴스가 열고 있어 배치 검증하지 못했고 Main의 시각 확인은 미실행이다.

### 2026-10-14 · Combat — 몬스터 Y 위치 기반 표시 순서

- 공용 `MonsterRoot`의 `VisualRoot`에 `SortingGroup`을 구성하고 카메라 화면에서 아래에 있는 몬스터일수록 더 큰 Sorting Order를 적용한다. 생성 직후와 이동·순간이동으로 위치를 바꾼 직후에만 다시 계산한다.
- 종별 외형의 기존 내부 SortingGroup 및 SpriteRenderer 순서는 유지한다. 몬스터 외형의 순서는 0~199로 제한해 타격 이펙트(200)와 체력바(250)가 앞에 남게 했다.
- `python3 tools/validate_collaboration.py`와 `git diff --check`를 통과했다. Unity 컴파일과 실제 화면 검증은 미실행이다.

### 2026-10-15 · Combat — 벼락 단일 대상 자동 스킬 코어

- `skill.thunder`는 전투 시작 즉시 준비되고, 적이 없으면 쿨타임을 소비하지 않는다. 무작위 적 1명을 지목해 0.1초 뒤 기본 공격력의 4배를 한 번 공통 피해 경로로 전달한다. 기본 쿨타임은 2초이며 모든 수치는 Main 컨트롤러에서 조절한다.
- `ThunderEffect` 루트의 자식으로 사용자 지정 `FX_Cartoon Thunder_Visual`을 중첩했다. 파티클은 시각 전용이고 단일 대상 판정은 루트·컨트롤러가 처리한다. 타격 전 대상이 죽으면 마지막 위치에서 연출만 끝내고 재지목·범위 피해는 없다.
- Main과 CombatSandbox에 컨트롤러를 배치하고 에디터 `Skill Acquired` 시험 토글을 제공했다. 실제 구매 해금·강화는 아직 연결하지 않았다.
- 사용자가 Main에 배치한 별도 원본 Thunder VFX 인스턴스는 보존하고 자동 반복 재생을 막기 위해 비활성화했다.
- Unity 6000.3.13f1 격리 프로젝트에서 벼락 PlayMode 3개와 전체 Combat PlayMode 11개를 통과했다. 생성·단일 타격·대상 사망·대기·일시정지·런 종료를 확인했고, Main 씬의 컨트롤러와 중첩 VFX 참조도 로드 검사로 확인했다. `python3 tools/validate_collaboration.py`도 통과했다. 실제 Main 화면의 시각적 타격 타이밍은 아직 확인하지 않았다.

### 2026-10-15 · Combat — 벼락 HitPoint 위치

- 벼락 연출의 `HitPoint`를 몬스터 루트 대신 체력바를 제외한 스프라이트 영역의 중심에 배치한다. 대상이 타격 전에 움직이면 중심을 다시 읽고, 죽으면 마지막 위치를 유지한다. 스프라이트가 없는 시험용 대상은 피격 콜라이더 중심을 사용한다.
- Unity 6000.3.13f1 격리 프로젝트에서 벼락 PlayMode 3개와 전체 Combat PlayMode 11개를 통과했다. `python3 tools/validate_collaboration.py`와 변경 경로의 `git diff --check`도 통과했다.

### 2026-10-15 · Combat — 화염 배기 자동 스킬 코어

- `skill.flame_exhaust`는 현재 커서 이미지와 범위를 45%로 축소해 한 번에 작은 커서 하나를 무작위 방향으로 발사한다. 적이 없어도 쿨타임마다 발사하며, 이전 커서가 살아 있으면 다음 커서가 추가될 수 있다. 각 작은 커서는 적을 관통하고 같은 몬스터를 한 번씩 공격한다.
- 피격 연출은 프로젝트 소유 루트 아래 `FX_MagicSlash02_Fire` URP 프리팹을 중첩하며, 몬스터 시각 중심에 표시하고 루트 Z 회전을 무작위로 정한다. 파티클은 피해 판정에 관여하지 않는다.
- Main과 CombatSandbox에 컨트롤러를 배치했다. 쿨타임 감소를 향후 유일한 강화 방향으로 정했고, 강화 단계·구매 해금은 후속 연결이다. 현재는 Main의 에디터 획득 토글로 시험한다.
- Unity 6000.3.13f1 격리 프로젝트에서 화염 배기 PlayMode 2개와 전체 Combat PlayMode 13개를 통과했다. Main 씬 커서·프리팹 참조 로드 검사도 통과했다. 실제 플레이 화면의 VFX 크기와 타격 연출은 확인하지 않았다.

### 2026-10-15 · Combat — 화염 배기 피격 범위 조정

- 작은 커서 이미지는 실제 커서의 45% 크기로 유지하고, `FlameExhaustProjectile`의 피격 반경은 실제 커서 공격 반경의 1.2배로 분리했다. 두 배율은 Main의 스킬 컨트롤러 Inspector에서 각각 조절한다.
- Unity 6000.3.13f1 격리 프로젝트의 화염 배기 PlayMode 2개를 통과했다. 테스트 커서 자체를 2배 확대한 상태에서도 시각 배율 45%와 피격 반경 배율 1.2를 확인했다.

### 2026-10-15 · Combat — 화염 배기 피격 이펙트 크기

- `FlameExhaustHitEffect`의 재생 크기를 몬스터 몸체 스프라이트 영역의 긴 변 × 기본 배율 1.2로 정했다. 체력바는 영역 계산에서 제외하고, 이펙트 풀 반환 시 원래 스케일로 복원한다. 배율은 피격 이펙트 프리팹에서 조정한다.
- Unity 6000.3.13f1 격리 프로젝트의 화염 배기 PlayMode 2개를 통과했다. 서로 다른 크기의 몸체별 이펙트 스케일과 런 종료 시 원래 스케일 복원을 확인했다.

### 2026-10-15 · Combat/Progression — 자동 스킬 런타임 스탯 개편 기획

- [자동 스킬 4종 런타임 스탯 개편안](../skill-runtime-stats-plan.md)에 BaseStat·UpgradeStat·RuntimeStat의 소유권, 스킬별 현재 수치와 런타임 항목, 런 준비 시 합성, 데이터 이전, 검증 기준을 정리했다. 이번 변경은 기획 문서만 작성했으며 스킬 코드와 강화 데이터는 아직 바꾸지 않았다.
- 사용자 결정에 따라 강화 항목·단계·비용은 추후 지정한다. 전투 진입 시 현재 상태 스냅샷을 CombatManager에 전달해 런 전체에서 고정된 RuntimeStat을 사용한다. 풀 상한 검토와 전체 정보 화면의 최종값 표시는 이번 개편 범위에서 제외한다.

### 2026-10-15 · Combat/Progression/App — 자동 스킬 4종 RuntimeStat 적용

- 토네이도·헤드샷·벼락·화염 배기 컨트롤러의 전투 기본값을 직렬화된 스킬별 BaseStat으로 묶고, 전투 준비 때 UpgradeStat과 런 공격력을 합쳐 스킬별 RuntimeStat으로 고정했다. Main 씬의 기존 프리팹 override도 새 필드 경로로 이전했다.
- Contracts의 `SkillCombatSnapshot`에 복사 방어된 증가량 목록을 추가하고, Progression이 구매한 `add` 노드를 stat ID별로 합산한다. GameInformation·영문 원본 데이터·스키마·한국어 참고본은 네 스킬을 지원한다. 현재 구매 노드는 토네이도 해금만 있으며 강화 항목·단계·비용과 나머지 스킬의 해금 노드는 후속 기획이다.
- HuntManager가 전투 진입 시 스킬 스냅샷을 RunCoordinator에 전달한다. CombatRunController는 `PrepareRun` 뒤 스폰과 `CommitStart` 전에 네 컨트롤러를 준비하며, 준비 실패 시 런을 취소한다. 런 도중에는 RuntimeStat만 사용한다.
- Unity 6000.3.13f1 격리 프로젝트에서 EditMode 99개와 PlayMode 13개를 통과했다. Main 씬 override 값, 증가량 복사·합산, 네 스킬 계산, 토네이도 개수 증가를 확인했다. Python 데이터 검증과 협업 계약 검증도 통과했다. Main 실제 화면 수동 검증과 전체 정보 화면의 최종값 표시는 후속이다.

### 2026-10-16 · Combat/App/Contracts/Data — 매직볼 코어

- `MagicBallSkillController`와 단일 재사용 `MagicBallObject`를 추가했다. 공급사 `Magic_Sphere_blue_Visual`은 게임 소유 루트 아래 시각 자식이다. 자식 `HItBox`의 원형 트리거가 적 타격 범위를, 루트의 원형 트리거가 벽 반사 범위를 정한다. 런 시드로 지정된 여덟 방향 중 하나를 선택해 화면 중앙에서 출발하고, 루트 콜라이더를 고려한 카메라 안쪽 경계에서 반사한다. 런 종료까지 공 한 개만 유지한다.
- 이동 경로를 원형 검사하고 접촉 중인 같은 적의 반복 피해를 막는다. 재접촉 최소 간격, 속도, 크기, 피해 배율, 첫 생성 시각, 화면 여백과 방향 목록은 컨트롤러 Inspector에서 조정한다. 이동속도·크기·피해 배율의 런 스냅샷 항목을 예약했다.
- Main과 CombatSandbox에 컨트롤러를 배치하고 `skill.magic_ball` ID를 데이터·스키마·계약에 추가했다. 구매 노드와 강화 단계는 후속 작업이며 Main은 에디터 획득 토글로 시험한다.
- Unity 6000.3.13f1 임시 복제 프로젝트에서 매직볼 런타임 수치 EditMode 3개, Combat PlayMode 전체 14개를 통과했다. Python 데이터·협업 검증도 통과했다. Main 실제 화면에서의 비주얼 크기와 플레이 감각은 수동 확인이 필요하다.
- 사용자 추가 `HItBox` 자식의 `CircleCollider2D`를 적 타격 범위로 연결했다. 루트 콜라이더는 벽 반사 경계에 사용한다. 자식 범위 안이면서 루트 범위 밖인 적의 피격을 PlayMode에서 확인했고, Combat PlayMode 전체 14개를 다시 통과했다.
- 매직볼 생성점을 화면 안쪽 전투 영역에서 런 시드 기반 무작위 위치로 바꾸고, 루트 콜라이더가 벽에 닿을 때 `Vector2.Reflect`로 해당 벽의 법선을 따라 반사한다. 여덟 기본 출발 방향은 축에서 22.5도 비껴나도록 조정했다. PlayMode에서 화면 내부 생성·비스듬한 입사각 반사를 확인했고 Combat 전체 14개와 Python 정적 검증을 통과했다.

### 2026-10-16 · Combat/App/Contracts/Data — 세로 레이저 코어

- `skill.laser`용 `LaserSkillController`와 풀링 `LaserEffect`를 추가했다. 공급사 `Lazer_normal_Visual`은 게임 소유 프리팹의 시각 자식이며, 별도 사각 `HitBox`가 화면 안쪽 전투 영역의 높이 전체를 덮는다. 발동마다 런 시드로 X 위치를 고르고, 활성 시간에 진입한 모든 적에게 발동당 한 번씩 피해를 준다.
- Main과 CombatSandbox의 `CombatManager` 아래에 컨트롤러 프리팹을 배치했다. Inspector 기본값은 쿨타임 5초, 지속 1초, 폭 배율 1, 피해 배율 2이며 화면 여백과 풀 상한도 조절할 수 있다. 런 스냅샷은 쿨타임·폭·피해 증가량을 받지만 구매 노드와 단계·비용은 아직 없다. 에디터 획득 토글과 Sandbox 토글로 시험한다.
- GameInformation의 여섯 번째 스킬 ID와 영문 데이터·스키마·한국어 참고본을 갱신했다. Unity 6000.3.13f1 격리 프로젝트에서 EditMode 전체 101개와 Combat PlayMode 전체 15개를 통과했다. 레이저 크기 증가량이 HitBox 및 비주얼 폭에 적용되는 것도 PlayMode에서 확인했다. Python 데이터·협업 검증도 통과했다. Main 실제 화면에서의 파티클 길이·정렬은 수동 확인 후 조절한다.
- `LaserEffect`의 비주얼 너비·높이 보정 배율을 각각 기본 2배로 올리고 프리팹 Inspector에 노출했다. HitBox의 화면 전체 높이와 폭 설정은 분리했다. Unity 격리 프로젝트에서 레이저 PlayMode 1개, Python 데이터·협업 검증을 통과했다. 실제 화면에서 보이는 길이와 타격 폭의 정렬은 수동 확인이 필요하다.
- Main 씬의 `Lazer_normal` 배치값(원본과 동일한 파티클, X·Y 배율 각각 3)을 확인했다. `LaserEffect`도 화면 높이 24인 Main에서 비주얼 배율 3이 되도록 고쳤다. 기본 HitBox 폭은 스프라이트의 실제 광선 폭에 맞춰 7.5로 늘리고, 화면 높이가 바뀌면 비주얼과 HitBox 폭을 함께 비례 조정한다.
- 사용자 스크린샷의 위·아래 화면 끝까지 닿는 레이저를 기준으로 비주얼 배율 적용 위치를 수정했다. 중간 `VisualAnchor` 대신 X축으로 회전된 `Lazer_normal_Visual` 파티클 Transform 자체를 스케일하여 Main 씬의 `Lazer_normal` 3배 설정과 같은 변환 순서를 사용한다.
- Unity 격리 프로젝트의 레이저 PlayMode에서 `ParticleSystemRenderer.bounds`가 카메라 화면 위쪽과 아래쪽을 모두 덮는지 확인했고 테스트 1개가 통과했다. 이 검사는 파티클 렌더링 경계이며 애니메이션의 모든 프레임에서 불투명한 광선이 양 끝을 채우는지까지 검사하지는 않는다.

### 2026-10-16 · Combat — 자동 스킬 등록과 풀 준비 리팩터링

- 여섯 자동 스킬 컨트롤러가 작은 `IRunSkillController` 런 수명주기 계약을 구현한다. `CombatRunController`는 자식 컨트롤러를 ID별로 찾고 중복·미등록 ID를 검사한 뒤 해금된 스킬을 준비하며, 완료·중단·취소 시 같은 목록을 정리한다. 이동·타격 규칙은 각 스킬에 남긴다.
- 화염 배기 피격 VFX는 첫 런 준비 때 80개를 모두 만들던 방식에서 기본 16개 준비로 바꿨다. 풀이 부족하면 피해는 유지하고 VFX만 생략하며 다음 프레임부터 한 번에 최대 2개씩 80개까지 늘린다. 초기 개수·증가 개수·상한은 프리팹 Inspector에서 조절한다.
- 화염 배기와 레이저는 활성 효과도 발동도 없는 프레임에 `Physics2D.SyncTransforms`를 호출하지 않는다. 이동 직후 콜라이더 영역을 읽거나 타격을 검사해야 하는 위치의 동기화는 유지했다.
- Unity 6000.3.13f1 격리 복제본에서 Combat PlayMode 17개가 통과했다. 같은 복제본의 단순 생성 측정에서 피격 VFX 16개 생성·풀 반환 중앙값은 약 10.4ms, 80개는 약 40.3ms였다(각 5회, 배치 모드). 80개 콜라이더의 Transform을 움직인 뒤 `Physics2D.SyncTransforms`를 호출한 측정은 호출당 중앙값 약 0.043ms였다(5회×100회, 배치 모드). 이 수치는 Main 화면의 전체 프레임 시간을 뜻하지 않는다. 협업 정적 검사도 통과했다.

### 2026-10-16 · Combat — 사망 애니메이션 반복 방지

- `MonsterCombatTarget`이 사망 상태의 첫 재생이 끝나면 마지막 프레임에서 Animator를 멈춘다. 부활할 때 원래 재생 속도를 복구하며, 기존 6초 부활 가능 시간은 유지한다. 공급사 원본 클립은 이번 변경에서 수정하지 않았다.
- 열린 Unity 6000.3.13f1 에디터에서 새로고침 후 콘솔 오류 0건과 Main 필드 전투 진입을 확인했다. `python3 tools/validate_collaboration.py`와 `git diff --check`를 통과했다. 고스트 사망 장면 자체의 반복 여부는 화면에서 직접 관찰하지 못했다.

### 2026-10-16 · Combat — 다른 몬스터 사망 클립 반복 수정

- 일반 몬스터 10종의 사망 클립을 조사해 박쥐·전갈·눈요정·거미·골렘·봉오리·해골군주 7종의 원본 `Dead.anim`에 Loop Time이 켜져 있음을 확인했다. 워커 2종과 고스트는 꺼져 있었다.
- 위 7종의 사망 클립을 `Combat/Art/MonsterDeath`에 복사하고 Loop Time만 껐다. 각 원본 외형의 Combat 프리팹 Variant에 Animator Override Controller를 적용해 Dead 클립만 교체하고, 해당 MonsterDefinition 7개를 Variant로 연결했다. 공급사 원본 에셋은 유지했다.
- Unity 에디터에서 7개 클립·Override Controller·Variant 참조를 확인했고 Main 필드 사냥에서 몬스터 생성과 처치가 진행되는 동안 콘솔 경고·오류 0건을 확인했다. `validate_game_information.py`가 해당 Variant의 원본 연결·사망 클립 교체·반복 해제를 검사하도록 갱신했다. 데이터·협업 검증과 `git diff --check`가 통과했다. 모든 종의 사망 장면을 각각 직접 관찰한 검증은 아니다.

### 2026-10-19 · Combat/App — BabyDragon 펫 외형·공격 연출 기획

- Main 씬의 BabyDragon 아래에 사용자가 배치한 `MagicCircle1_Green` 자식 프리팹을 펫 식별 고리로 사용한다. 기존 씬 인스턴스와 자식 구성을 보존하고, 해금된 런에서만 활성화하는 방향으로 펫 기획 초안을 갱신했다.
- 사용자가 지정한 `Explosion_Effect_Blue`를 펫 공격 명중 연출로 기록했다. 원본의 파티클 6개는 모두 반복 재생 설정이므로, 구현 시 게임 소유 래퍼에서 한 번 재생하고 파티클 종료 후 회수한다. 피해는 공통 스킬 피해 경로에서 처리한다.
- 이 변경은 기획·에셋 매핑 문서만 갱신했다. Main 씬과 공급사 프리팹의 사용자 수정은 보존했다. `python3 tools/validate_collaboration.py`와 변경 문서의 공백 검사는 통과했고 Unity 화면 검증은 수행하지 않았다.

### 2026-10-19 · Combat/App/Progression/Data — BabyDragon 펫 스킬 1차 구현

- `skill.pet_baby_dragon`을 일곱 번째 자동 스킬로 등록했다. `stat.attack.01` 이후 가넷 40개로 구매하는 해금 노드를 추가했으며, 보유 상태와 허용된 스탯 증가량이 기존 런 스냅샷을 거쳐 `IRunSkillController`로 전달된다. 펫 피해는 `TryApplySkillDamage`를 사용한다.
- Main의 기존 BabyDragon Spine 씬 개체와 사용자가 붙인 고리는 그대로 참조한다. 잠금·런 종료에는 함께 숨고 해금된 전투에서는 커서 옆에 나타나 가까운 적을 탐색·추적·공격한다. `Idle`·`Walk`·`Attack`을 사용하고 일시정지에는 애니메이션과 공격 시계를 멈춘다. 숨겨진 `cursor_image`도 기본 마우스 모드에서 월드 좌표를 갱신하게 해 펫 추종 기준이 멈추지 않도록 했다. 성장 화면은 BabyDragon 아이콘과 스킬 노드를 표시한다.
- 지정한 `Explosion_Effect_Blue`는 원본을 수정하지 않고 컨트롤러에서 최대 8개까지 미리 복제한다. 명중 시 반복을 끄고 짧게 방출한 다음 풀에 회수한다. 풀이 부족할 때도 논리 피해는 적용된다.
- Unity 6000.3.13f1 격리 복제본에서 EditMode 전체 104개와 PlayMode 전체 19개를 통과했다. Main 씬 PlayMode에서 컨트롤러·BabyDragon·고리·커서·폭발 프리팹 연결과 해금 등장·종료 숨김도 확인했다. Python 데이터·협업 검사도 통과했다. 실제 Main 전투 화면의 크기·정렬·고리 대비·폭발 크기는 후속 플레이테스트가 필요하다.

### 2026-10-19 · Combat/App — BabyDragon Y 위치 기반 표시 순서

- 몬스터와 펫의 화면 Y → 0~199 Sorting Order 계산을 `ViewportVisualSorting`으로 공유한다. 펫 루트에 `SortingGroup`을 한 번 구성해 Spine과 자식 고리를 함께 정렬한다. Main에서는 몬스터 스포너와 동일한 카메라를 연결했다.
- 몬스터의 최적화 방식을 유지했다. 펫 등장·이동 직후에만 정렬을 요청하고, Y가 이전과 같으면 카메라 변환을 생략하며, 계산된 순서가 같으면 SortingGroup에 다시 쓰지 않는다. 공급사 펫·고리 프리팹은 수정하지 않았다.
- 격리 Unity 6000.3.13f1 프로젝트의 PlayMode 전체 19개가 통과했다. Sandbox에서 Y 이동에 따른 순서 변경과 X 이동만 있을 때의 계산 생략을, Main에서 실제 BabyDragon 그룹·카메라 참조를 확인했다. 화면에서 겹침 모양은 추가 확인이 필요하다.

### 2026-10-20 · Combat/App — BabyDragon 발밑 고리 정렬 분리

- Main의 `MagicCircle1_Green`은 BabyDragon 자식으로 유지한다. 런 준비 때 고리에 별도 `SortingGroup`을 한 번 구성하고 `Sort At Root`를 켜서 Default Sorting Layer, Order -1로 고정한다. 따라서 0~199로 정렬되는 모든 몬스터 뒤에 표시된다. 펫 본체의 Y 기반 정렬과 위치 계산 최적화는 유지한다.
- Main 인스턴스의 고리 참조를 명시적으로 연결했다. 격리 Unity 6000.3.13f1 프로젝트의 PlayMode 전체 19개, Python 데이터·협업 검사, `git diff --check`가 통과했다. 실제 Main 화면의 겹침 모양은 수동 확인이 필요하다.

### 2026-10-21 · Combat/Progression — 현행 스킬 연결, 전투 준비 복원력, 유물 드롭 초안

- 성장 화면의 스킬 목록을 현재 전투 구현 7종(토네이도, 헤드샷, 벼락, 화염 배기, 마력 구체, 레이저, 베이비 드래곤)으로 구성했다. 각 항목은 공격력 기본 노드 뒤 가넷 40개로 해금하고, 스킬별 강화 단계는 추가하지 않았다.
- Main 씬에는 7종의 에디터 전투 테스트 토글이 켜져 있다. 필드 전투를 시작하면 저장된 구매 상태와 무관하게 Editor에서 각 자동 스킬과 펫을 볼 수 있다.
- 스킬 스냅샷 하나가 잘못되거나 중복되거나, 특정 스킬 컨트롤러가 없거나 준비에 실패해도 전체 필드 런을 거절하지 않는다. 문제 ID는 Console에 기록하고 유효한 다른 스킬은 계속 준비한다.
- 20개 유물의 첫 효과·몬스터 출처·기본 확률 초안을 [유물 드롭 설계](../relic-drop-design.md)에 기록했다. 미획득 후보의 개별 확률 합으로 먼저 1회 드롭 여부를 굴리고, 성공하면 개별 확률 비율로 하나를 선택한다. 보유 유물은 후보에서 제외하며 남은 유물의 개별 확률은 고정한다.
- 유물 드롭/보유/효과는 현재 게임 런타임에 연결되지 않았다. 이 변경은 드롭 동작 구현이 아니라 기획 고정과 테스트 가능한 스킬 경로 개선이다.

### 2026-10-22 · Progression/Data — 전체 강화 전투 밸런스 1차 조정, 유물 카드 콘텐츠

- Main 기본 공격력 10 기준 최대 공격력은 45, 공격 간격은 0.48초, 치명타 확률은 45%, 공격 반경은 약 3.68u로 조정했다. 종전 최대 공격력 109·0.20초·100% 치명타·약 34.51u에서 범위 공격의 화면 쓸기와 반복 피해를 줄인다. 최대 보스 피해 배율은 4배에서 2배로 낮췄다.
- 몬스터 10종의 기본 체력을 약 1.5배로 올리고 생성 주기를 0.3초 단위로 앞당겼다. 종별 생산 연구 최종값은 4마리에서 5마리로 조정했다. 현재 Main Stage01 허용 roster 기준 최대 생산 시 약 13.7마리/초에서 21.0마리/초로 증가한다. 데이터의 기본값과 MonsterDefinition 에셋을 함께 갱신했다.
- 유물 도감 Catalog에 20종의 한 줄 효과 설명을 입력했다. 사용자가 재기획한 행운의 주사위(펫 확률 2배 공격), 고대의 서(펫 공격 반경), 크라켄의 눈(일반 몬스터 저체력 즉사), 보물상자(일반 몬스터 처치 보상)는 반영했다. 이 효과들은 도감 콘텐츠이며 실제 전투 효과는 아직 적용하지 않았다.
- 전투 JSON의 balanceVersion을 4로 올리고 한국어 참고 데이터를 재생성했다. Unity 플레이 검증은 수행하지 않았다.

### 2026-10-25 · App/Combat — 기획자용 전투 밸런스 창

- Main 씬에서 `Tools > Cursor Hunter > Combat Balance`로 여는 한글 에디터 창을 추가했다. 베이스·커서 공격·7종 스킬·10종 몬스터와 스폰을 직접 설정하며 입력값과 현재 적용값을 나란히 표시한다. 숫자는 Enter/포커스 해제로 확정하고 토글은 즉시 반영한다. 특성 노드·맵·구매·프리셋·원본 파일 반영 기능은 포함하지 않는다. 사용 규칙은 [전투 밸런스 창 설계](../combat-balance-editor-design.md)를 따른다.
- App의 `CombatBalanceSettings`와 `HuntManager.Balance`가 테스트 최종값을 전달한다. 전투 시작 시 기존 진행도 증가량·Stage 편성·Inspector 획득 토글을 중복 적용하지 않는다. 전투 중에는 같은 runId·경과 시간·난수를 유지하고 스킬의 활성 효과, 기존 몬스터, 생성 일정을 갱신한다. HP는 남은 비율을 유지하며 이미 맞힌 대상·위치·행동 상태를 보존한다. 오류 입력은 사전 검증하여 직전 유효값을 유지한다.
- `CombatRunController.Balance`, 스킬별 런타임 컨트롤러/효과, `MonsterBalanceSettings`, `MonsterSpawner`, `MonsterCombatTarget`, `MonsterBehaviorController`를 확장했다. Combat은 기존대로 Contracts만 참조한다. 씬·프리팹·공급사 에셋·기본 JSON은 변경하지 않았다.
- `RunCoordinator`는 테스트 정산 정책을 Discard로 유지하고 HuntManager도 정산을 차단한다. 설정은 메모리와 Edit→Play 전환용 SessionState에만 보관한다. 전투/Play 종료 시 해제하며, 창 닫기·수동 설정 해제는 테스트를 종료하고 메인 메뉴로 복귀한다. 다른 전투 대상으로 이전 설정이 넘어가지 않게 정리한다.
- Unity 6000.3.13f1 격리 복제본에서 전체 EditMode 127개, PlayMode 23개를 통과했다. Main에서 직접 공격력 적용·같은 전투 갱신·잘못된 변경 거부·보상 폐기·PlayerPrefs 불변·메인 메뉴 복귀를 검증했다. `validate_collaboration.py`, `validate_game_information.py`, `test_combat_information_v3.py` 8개 및 `git diff --check`도 통과했다. 실제 Game 뷰의 조작감과 창 배치는 수동 시각 검증이 남았다.

### 2026-10-25 · App — 전투 밸런스 설정을 다음 전투에도 유지

- 전투가 끝나면 테스트 설정을 자동 해제하던 정책을 수정했다. `HuntManager.Balance`는 완료·중도 종료·메인 메뉴 복귀 시 이번 런의 ID와 스킬 실행 연결만 정리하고, 마지막 유효 최종값을 다음 전투에 다시 전달한다. 전투 밖의 설정 캡처도 유지 중인 값의 복사본을 반환한다.
- `CombatBalanceWindow`는 전투가 끝나도 소유권과 SessionState를 유지하며 ‘다음 전투에도 적용’ 상태를 표시한다. 수동 해제·창 닫기·Play 종료·대상 교체·Play 중 스크립트 재컴파일 시에는 설정을 해제한다. 원본 데이터·유저 저장 미변경과 테스트 보상 지급 차단은 유지한다.
- Main PlayMode 회귀 테스트를 확장해 추가 편집 없이 완료 후 두 번째 전투, 메뉴 복귀 후 세 번째 전투를 시작하고 공격력·커서·스킬·몬스터 값과 저장 데이터 유지를 확인했다. Unity 6000.3.13f1 격리 복제본 PlayMode 전체 23개, 협업 정적 검사와 공백 검사를 통과했다.

### 2026-10-25 · App/Data/Progression — 최종 설정 스탯 탭과 기본값 Save

- 전투 밸런스 창에 다섯 번째 `최종 설정 스탯` 탭을 추가했다. 현재 테스트 수치 복사, 독립 최종안 편집, 저장된 기본값 비교·불러오기, `Save · 게임 기본값 저장`을 제공한다. 앞의 네 탭은 기존 실시간 테스트로 유지하며, 최종안 편집은 현재 전투를 변경하지 않는다. Play 종료·리로드에도 최종안을 보존한다.
- `CombatBalanceBaseStore`가 전체 검증·파일 준비·외부 변경 확인 후 저장하며 실패 시 복구한다. `CombatBalanceSceneStore`는 선택한 씬의 PlayerCombatStatsDefaults·Hunt.durationSeconds·7종 스킬 BaseStat override만 패치한다. 런타임 오브젝트나 씬 전체를 저장하지 않고 다른 편집값·프리팹 참조·GUID를 유지한다. Play 종료 후 편집 씬의 저장한 필드만 다시 동기화한다.
- 몬스터는 종별 Definition의 BaseStats에 최종 수치를 저장하고 `baseStatsAreFinalValues`를 켠다. HP는 Int64를 직접 사용하며 프로필의 수치 배율을 다시 곱하지 않는다. 기존 이동 모드·특수 능력·생산 보너스는 보존한다. `CombatBalanceInformationStore`는 영어 JSON의 공통 수치·상한·몬스터 표시값과 한국어 참고 문서·해시를 동기화한다. 스킬 사용/몬스터 생성 토글, 구매·해금·Stage 편성·지갑·노드는 수정하지 않는다.
- Save 후 같은 Play 세션에서도 저장된 기본값을 읽도록 Hunt의 템플릿과 진행도 화면의 원본 전투 정보 캐시를 갱신한다. 구매 내역·지갑은 다시 로드하지 않는다. 테스트 override는 별도로 유지되므로 저장한 베이스로 일반 전투를 확인할 때는 테스트 설정을 해제한다.
- Unity 6000.3.13f1 격리 복제본에서 EditMode 전체 151개·PlayMode 전체 23개가 통과했다. 임시 복제 원본의 저장·재import·Int64·HP 101·프로필 유지·비수치/가용 여부/업그레이드/생산 정책·GUID 보존, 잘못된 입력 및 외부 편집 전체 거부를 검증했다. 협업·게임 정보·한국어 참고 문서 정적 검사와 Python 테스트 12개 및 공백 검사를 통과했다. 구현 과정에서 실제 기본 데이터 파일의 수치는 바꾸지 않았다. 창의 수동 시각 확인은 별도 진행한다.

### 2026-10-25 · App — 최종 스탯 조회의 Global Light 중복 오류 수정

- Main이 열린 상태에서 기본값 조회용 OpenPreviewScene이 Main의 Global Light도 추가 등록하여 Default/Skill blend style 0 오류가 발생했다. 기존 경로에 회귀 테스트를 실행해 두 레이어의 동일 오류를 재현했다.
- CombatBalanceSceneStore의 원본 씬 추가 로드를 제거했다. 새 CombatBalanceSceneSource가 YAML과 관련 프리팹 에셋의 숫자 및 Main 오버라이드만 읽는다. 검증에는 조명·카메라·시각 오브젝트·참조가 없는 비활성 임시 컴포넌트만 사용한다. 실행 씬 조명이나 로그 설정은 바꾸지 않는다. 원본값의 권위, Int64, 저장 패치와 Play 종료 후 동기화는 유지했다.
- Default/Skill Global Light 활성 상태에서 조회·저장 준비·오류 입력 거부 후 조명 값·개체·씬 수·활성 씬·dirty 상태·파일 불변을 확인했다. Main 실제 전투 중에도 원본값만 읽고 같은 런 ID·경과 시간·실시간 공격력을 유지하며 중복 조명 로그가 없는지 검증했다.
- Unity 6000.3.13f1 격리 프로젝트에서 EditMode 전체 153개·PlayMode 전체 24개 및 협업 정적 검사·공백 검사를 통과했다. 원본 씬과 조명 데이터는 수정하지 않았다.

### 2026-10-25 · App — 전투 밸런스 창의 메뉴 계층 분리

- 창 최상단에 `실시간 테스트`와 `최종 설정 스탯` 모드를 배치하고, 그 아래에 베이스·커서 공격·스킬·몬스터·스폰의 네 분류를 공통 하위 탭으로 배치했다. 최종 설정 내부의 중복 분류 드롭다운은 제거했다.
- 모드별 선택 분류와 기존 편집값을 유지하며, 이전 다섯 번째 탭 선택 상태는 최종 모드로 이관한다. 실시간 적용과 Save의 데이터 처리 경로는 유지했다. 설계 문서와 사용 안내도 같은 계층으로 갱신했다.
- Unity 6000.3.13f1 격리 프로젝트에서 App EditMode 테스트 96개와 협업 정적 검사·공백 검사를 통과했다. 실제 에디터 창의 수동 시각 검증은 수행하지 않았다.

### 2026-10-25 · App — 커서 범위를 기본 크기 기준 배율로 편집

- `CombatBalanceWindow`의 커서 공격 항목을 월드 반경 대신 `커서 범위 (배)`로 표시한다. 기존 기본 반경을 1로 보고 직접 입력하거나 −/+ 버튼으로 0.1배씩 조절한다. 버튼 조절은 소수점 네 자리로 정리하며 최소 0.01배를 유지한다.
- 실시간 테스트와 최종 설정의 입력값·비교값에 같은 고정 기준 `GameInformation.BaseAttackRadiusWorldUnits`를 사용한다. Save로 기본 반경이 바뀌어도 표시 기준은 바뀌지 않는다. 실제 변경 때만 월드 반경으로 변환하므로 화면 갱신만으로 값이 변하지 않는다. 전투·씬·JSON 저장 형식은 유지했다.
- Unity 6000.3.13f1 격리 프로젝트에서 App EditMode 테스트 96개와 협업 정적 검사·공백 검사를 통과했다. 실제 에디터 버튼 조작의 수동 시각 검증은 수행하지 않았다.

### 2026-10-25 · App — TraitScreen_Runtime과 실제 전투의 임시 연동 해제

- `TraitScreen_Runtime`을 만드는 `TraitScreenController`가 구매한 커서 보너스·전투 시간·스킬 해금/강화·몬스터 해금/생산 보너스를 Hunt에 전달하고, 전투 보상을 지갑에 저장하며 HUD 정보창도 제공하고 있음을 확인했다. 단순 씬 참조 제거는 자동 탐색으로 복구되므로 사용하지 않았다.
- `HuntManager.Connect Trait Screen To Combat`를 추가하고 Main에서 명시적으로 껐다. 이 상태의 정상 전투는 특성 컨트롤러 없이 원본 전투 JSON·Main PlayerCombatStatsDefaults·스킬 BaseStat·MonsterDefinition·Stage로 구성한다. `RunCoordinator`에는 커서 보너스 없이 스킬 스냅샷을 전달하는 경로를 추가했다. Stage의 명시적 편성·생산 옵션, Inspector 스킬 시험 토글, 밸런스 에디터의 실시간 적용과 Save는 유지한다.
- 보상 지급·저장, 기본값/런 정보의 특성 화면 전달, 특성 정보 팝업과 팝업 일시정지 연결을 차단했다. HUD는 특성 버튼을 만들지 않거나 숨기고 직접 호출도 무시한다. 특성 화면·구매·저장 데이터는 삭제하지 않는다. 연동 옵션을 다시 켜면 기존 경로를 사용할 수 있으며, 같은 Play 세션에서 다시 꺼도 강화된 전투 시간이 기본값에 남지 않게 런 시간을 별도 변수로 유지한다.
- Main PlayMode 회귀 4개를 추가했다. 실제 전체 노드 구매와 빈 구매, 연결 후 재해제, Stage의 종별 PackSize, Main 기본 공격력, 스킬 시험 토글의 BaseStat, 특성 컴포넌트 부재, 실제 처치 Eligible 보상에 대한 지갑/PlayerPrefs 불변, HUD 직접 호출 차단을 검증한다.
- Unity 6000.3.13f1 격리 프로젝트에서 EditMode 전체 153개와 최종 PlayMode 전체 28개를 통과했다. 게임 정보·협업 정적 검사, Python 전투 정보 테스트 8개, 공백 검사도 통과했다. 실제 사용자 저장을 변경하지 않았으며 수동 Game 뷰 조작 검증은 수행하지 않았다.

### 2026-10-26 · App/Progression — 특성 화면에서 전투 밸런스 테스트값 편집

- Main의 `Connect Trait Screen To Combat`는 Master의 현재값인 비활성 상태로 유지했다. `Tools > Cursor Hunter > Combat Balance`에서만 가능했던 최종값 편집을 특성 화면 `테스트 도구 > 전투 수치 편집`에서도 열 수 있다.
- 새 Contracts 브리지로 기본 전투 능력치, 커서 공격, 7종 스킬, 몬스터와 스폰 수치를 특성 화면에 가져오고 App에서 검증해 적용한다. Progression은 App/Combat을 참조하지 않는다. 밸런스 테스트 중에는 즉시 반영되고, 전투 전에는 다음 전투를 테스트 모드로 시작한다. 테스트 보상은 저장하지 않으며 테스트값 해제 후 원래 일반 전투 설정으로 돌아간다.
- `python3 tools/validate_collaboration.py` 및 staged `git diff --check`를 통과했다. Unity Editor에서 직접 화면을 여는 수동 확인과 Unity 컴파일/Play 확인은 수행하지 않았다.

### 2026-10-27 · App/Combat — Main 전투를 실제 특성 진행도에 고정 연결

- 사용자가 요청한 동작을 다시 정리해, 특성 화면에서 전투 수치를 임의 편집하지 않고 실제 구매·해금 데이터가 Main 전투에 반영되도록 연결했다. 특성 화면 안의 임시 수치 편집 오버레이와 그 전용 브리지 계약을 제거했다. 별도 개발용 `Tools > Cursor Hunter > Combat Balance` 경로는 유지했다.
- Main 씬에서 Trait 연결을 켜고 기본 Stage 참조를 비웠다. 일반 시작 버튼은 고정 Stage 로스터나 임시 밸런스 설정 대신 현재 특성 스냅샷의 해금 몬스터만 스폰 계획에 사용한다. 몬스터 기본 전투 특성은 기존 `MonsterDefinition`/`MonsterBehaviorProfile`, 생산 노드는 Trait 구매값에서 온다.
- 커서 기본 능력치는 구매 증가량이 포함된 최종 `CombatSnapshot`으로 한 번만 전달한다. 런타임에서 별도 증가량 스냅샷을 다시 더하지 않으며, Trait 스냅샷 누락·오류 시 기본값으로 조용히 대체하지 않고 Main 전투를 중단한다.
- 스킬 런 시작은 특성 스냅샷과 Main 컨트롤러 ID가 완전히 일치해야 한다. Trait 해금 여부만 활성 상태의 기준으로 쓰고, 해금 스킬 컨트롤러가 없거나 준비에 실패하면 런을 시작하지 않는다. 현재 설정에는 스킬 해금 노드만 있고 스킬별 강화 단계는 없으므로, 강화 노드를 추가하면 기존 `SkillCombatSnapshot.Upgrades` 런타임 경로로 전달해야 한다.
- `CombatRunController.RefreshSkillControllers()`가 실패 여부를 반환하도록 맞춰 Balance 검증의 컴파일 오류도 함께 바로잡았다. 테스트 코드는 추가/실행하지 않았다. `python3 tools/validate_collaboration.py`와 `git diff --check`는 통과했으며 Unity import/compile/Play Mode는 실행하지 않았다.

### 2026-10-27 · Combat — 보유 유물 효과를 실제 전투에 적용

- 런 시작 시 `RelicCombatModifiers`가 소유 유물의 효과를 집계한다. 커서 공격 피해/반경, 자동 스킬 피해/범위/쿨타임, 펫 피해·이동·공격 주기·탐지/공격 반경, 일반 필드 시간, 몬스터 생성 간격, 치명타 피해, 첫 타격, 저체력 즉사, 보스 피해, 가넷/젬 보상에 연결했다.
- 유물 효과가 포함된 스냅샷을 먼저 `CombatRunController`에 설정하고 그 다음 스킬 런타임 수치를 만들도록 순서를 맞췄다. 펫 피해에는 일반 자동 스킬 피해 유물을 중복 적용하지 않는다.
- 실제 처치 후 유물 드롭 굴림과 획득 데이터 저장은 아직 구현하지 않았다. 테스트 도구에서 소유 처리 후 새 전투를 시작하면 효과를 확인할 수 있다. Unity 실행/컴파일 및 테스트는 사용자가 직접 하기로 하여 수행하지 않았다.

### 2026-10-28 · Combat/App — 펫을 마우스와 독립적인 자동 전투로 변경

- `PetSkillController`의 커서 참조, 커서 옆 등장·복귀, 커서와 적의 거리 기반 추적·명중 제한을 제거했다. 씬의 펫 위치를 한 번 기억해 런 시작마다 복원하고, 대상이 없으면 현재 위치에서 0.1초마다 주변을 스캔한다. 가장 가까운 피격 영역의 적을 선택해 접근·공격하고, 유효한 대상은 유지한다. 탐지 반경은 새 대상 선택에만 적용한다.
- 유효성 검사와 후보 선택의 커서 거리 쿼리를 제거하고 거리 비교에 제곱 거리를 사용한다. 기존 재사용 대상 목록, 공격 이펙트 풀, 애니메이션·정렬 변경 시 갱신을 유지한다. 매 프레임 전체 탐색이나 새 목록 할당을 추가하지 않았다.
- `SkillRuntimeStats`, 펫 프리팹, Main 씬 및 `CombatBalanceWindow`에서 커서 추적 한계·대기 거리 설정과 참조를 제거했다. `MainCursorController`의 오래된 펫 관련 주석 및 `docs/pet-skill-design.md`를 갱신했다.
- 격리 Unity 6000.3.13f1에서 펫 PlayMode 5개 및 관련 스탯·밸런스 EditMode 38개가 통과했다. 탐색 주기·최근접 피격 영역·타깃 유지/소멸·무적·사거리·일시정지·재시작·이전 런·예약 공격 종료와 Main 커서 이동 독립성을 검증했다. 협업 정적 검사와 공백 검사도 통과했다. 수동 Game 뷰 확인 및 성능 프로파일러 실측은 수행하지 않았다.

### 2026-10-28 · Combat — 펫 공격 모션 중 이동 차단과 공격 누락 진단

- `PetSkillController`의 고정 0.5초 타이머만으로 이동 잠금을 푸는 경로를 수정했다. 실제 Spine Attack 트랙 완료까지 기다리고, 공격 자세를 Idle로 바꾼 다음 프레임부터 추적을 재개한다. 원본 에셋의 0.2초 혼합을 공격 진입·종료 트랙에서만 0으로 덮어써 이동 중 공격 자세가 남는 현상을 막는다.
- 같은 이름의 애니메이션 재생 생략은 반복 동작에만 적용한다. 단발 Attack은 연속 공격 시에도 매번 처음부터 재생한다. 공격 시작 직전에는 물리 좌표를 동기화해, 적이 이미 사거리를 벗어났는데 이전 콜라이더 위치로 공격을 시작하고 쿨타임을 소비하는 경로를 막았다. 추적 중 매 프레임 동기화는 하지 않는다.
- 약 0.167초의 선딜레이 동안 적이 사거리 밖으로 이동하면 명중이 취소되는 기존 규칙도 확인했다. 이때 무적·사망·다른 런 검사와 이미 소비된 공격 주기는 유지한다. 사거리 안에서 시작한 공격을 무조건 명중시키는 방식으로 판정을 바꾸지는 않았다.
- `PetSkillPlayModeTests`에 실제 BabyDragon Spine 프리팹을 사용하는 완료·혼합·연속 공격 회귀와 오래된 물리 좌표 회귀를 추가했다. 수정 전 3개 실패를 재현했고, 수정 후 격리 Unity 6000.3.13f1 펫 PlayMode 8개 및 협업 정적 검사·공백 검사가 통과했다. 수동 Game 뷰 검증은 수행하지 않았다.

### 2026-10-28 · Combat — 마지막 타격의 데미지 숫자를 남은 체력으로 제한하지 않음

- `CombatDamageApplied.DisplayDamage`를 표시 전용 값으로 분리했다. `MonsterCombatTarget`은 보호막 피해 감소 이후, 남은 체력으로 제한하기 이전의 값을 반환하고 `CombatRunController`가 이벤트에 전달한다. `DamageTextManager`의 표시·합산·오버플로 검사는 이 값을 사용한다. 실제 체력 감소량인 `EffectiveDamage`, 누적 피해, 처치·보상과 기존 호출부 호환성은 유지한다.
- 기존 `DamageTextTests`를 갱신해 실제 몬스터 프리팹의 체력 12에 공격력 10으로 두 번 공격하면 숫자는 각각 10, 체력은 2·0, 누적 피해는 12, 처치는 1회인 것을 확인했다. 치명타 스킬의 마지막 타격도 전체 피해량을 표시하며 죽은 대상에는 추가 숫자를 만들지 않는다. 테스트의 리플렉션 호출을 현재 `ApplyBundle` 인자에 맞췄다.
- 격리 Unity 6000.3.13f1에서 컴파일과 EditMode의 `DamageTextTests` 10개·`MonsterBalanceTests` 9개가 통과했다. 협업 정적 검사와 공백 검사도 통과했다. PlayMode 및 수동 Game 뷰 시각 검증은 수행하지 않았다.


### 2026-10-28 · Data/App/Progression — 공용 젬스톤 아이콘 저장소

- `GemstoneIconCatalog`와 `Data/Resources/GameData/GemstoneIconCatalog.asset`에 6종 젬스톤의 고정 ID, 원본 이미지 경로, Sprite 참조를 등록했다. 기존 512px 오각형 Red/Yellow/Purple/Blue/Green/Gray 아이콘을 참조하며 공급사 원본은 변경하지 않았다. `LoadDefault().GetIcon(id)`로 빌드에서도 조회하고 `TryGetEntry`로 경로를 확인한다.
- `TraitScreenController`의 젬스톤 목록·비용·몬스터 배지가 공용 저장소를 우선 조회한다. 기존 배열은 구형/개별 씬의 대체용으로 보존했다. Main의 `InGameObjects/GemStone/Icon`에 `GemstoneIconView`와 기본 가넷을 연결했고, Inspector ID나 `SetGemstone`으로 종류를 바꿀 수 있다. 다른 씬 편집과 텍스트·잔액은 보존했다.
- 협업 정적 검사, 6종 ID/파일 경로/Sprite GUID 대조, 이번 씬 변경의 참조·범위·공백 검사가 통과했다. Unity 참조 DLL을 사용한 외부 Roslyn 컴파일에서 Data/Progression/App 세 어셈블리가 통과했다. Unity import/Play Mode 및 Game 뷰 시각 검증은 수행하지 않았다. 전체 작업 트리의 공백 검사에는 이번 수정 전부터 있던 Main 씬 직렬화 공백이 남아 있다.

### 2026-10-28 · App/Combat — 전투 젬스톤 획득 목록

- Main의 `GemStoneLists`에 가넷·토파즈·자수정·사파이어·다이아몬드·드래곤 젬 6개 행을 미리 배치했다. 기존 행의 배경·폰트 예외·TargetPoint를 유지하고 공용 아이콘 카탈로그의 종류별 ID와 Sprite를 연결했다. 복사되어 있던 메인 메뉴 복귀 Button은 제거하고 표시 요소의 Raycast를 껐다.
- `CombatGemstoneList`는 현재 SpawnPlan의 활성 몬스터와 실제 드롭 수량·확률로 표시 여부를 정한다. 획득 가능한 종류는 0부터 표시하고 `CombatRunController.GetResourceEarned(id)`로 이번 런의 누적 수량을 읽는다. 실시간 밸런스 편집으로 공급원을 꺼도 이미 획득한 종류는 남긴다. UI에서 영구 지갑을 읽거나 변경하지 않는다.
- `PrototypeRunHud`를 일반/밸런스 전투 시작·진행·결과·Reset에 연결했다. 마지막 프레임의 획득량은 RunResult로 보정하며 이전 RunId의 결과를 거부한다. 매 프레임에는 문자열을 재생성하지 않고 수량이 달라진 행만 갱신한다. VerticalLayoutGroup은 Upper Left, 높이 확장 해제, 간격 24로 활성 행만 위에서부터 쌓는다. 기존 타이머·처치 수는 목록과 겹치지 않도록 상단 중앙으로 옮겼다.
- Unity 6000.3.13f1 격리 복제본에서 컴파일과 관련 EditMode 24개, 신규 Main PlayMode 1개가 통과했다. 6종 씬 연결·희소 목록 정렬·실제 처치 보상·Int64·마지막 처치·재시작·오래된 결과·실시간 편집·일반/밸런스 진입·메인 메뉴 초기화·PlayerPrefs 불변을 검증했다. 협업 경계 및 변경 범위의 참조·공백 검사도 통과했다. 수동 Game 뷰 시각 검증은 수행하지 않았다.
- 기존 `CombatBalanceMainScenePlayModeTests` 2개는 현재 Main의 `connectTraitScreenToCombat = true`에서 기본 시작이 밸런스 모드라고 가정하는 40/96행에서 실패했다. 이 설정과 분기 조건은 이번 변경 이전부터 존재하며, 기존 테스트나 실제 특성 연동 설정은 수정하지 않았다. 신규 테스트는 현재 일반 진입과 명시적으로 특성 연동을 끈 밸런스 진입을 각각 확인한다.

### 2026-10-28 · App — 젬스톤 하단 2열 배치와 수량 색상

- 사용자 참고 이미지에 맞춰 첫 가넷 행은 큰 크기로 유지하고, 나머지 5종은 씬에 미리 만든 `SecondaryGemstones`의 GridLayoutGroup 아래로 이동했다. 고정 2열, 가로 간격 24·세로 간격 16으로 왼쪽부터 채운다. 작은 행과 아이콘 높이는 기존의 절반이며 숫자 최대 크기는 36이다. 최상위 VerticalLayoutGroup은 첫 행과 그리드의 실제 필요 높이만 배치한다. 비활성 종류는 빈 칸을 남기지 않는다.
- 수량 글자색은 가넷 `#FF8277`, 토파즈 `#FFD45C`, 자수정 `#D6A1FF`, 사파이어 `#79BFFF`, 다이아몬드 `#83EEA0`, 드래곤 젬 `#E4E9F1`로 지정했다. 현재 카탈로그의 빨강·노랑·보라·파랑·초록·회색 아이콘에 대응하는 밝은 색이며, 기존 TMP_TiltWarp_Outline의 검은 외곽선과 그림자·글꼴 예외 설정을 유지한다. 전투 보상 및 표시 조건 코드는 변경하지 않았다.
- 격리 Unity 6000.3.13f1에서 기존 젬 목록 EditMode 3개를 갱신해 1개만 보이는 하단 목록·전체 5종의 2열 위치·자동 높이·수량 갱신·초기화를 확인했다. 별도 임시 시각 검사까지 포함한 4개 검사가 통과했고 실제 씬 UI를 600×420 이미지로 렌더링해 숫자색·검은 외곽선·배치를 확인했다. 협업 경계와 이번 씬 변경의 로컬 참조 검사도 통과했다.

### 2026-10-28 · App/Combat — 젬스톤 흡수 연출 기획 확정

- [젬스톤 흡수 기획](../gemstone-collection-presentation-design.md)에 몬스터 위치에서 분출·가속 비행 후 종류별 TargetPoint로 흡수하고 도착 시 TMP 숫자 증가·스케일 팝업을 수행하는 흐름을 정리했다. 사용자 지시에 따라 소형 1·중형 5·대형 10을 고정했고, 43개는 대4·소3, 48개는 대4·중1·소3으로 분해한다. 풀 포화 시 크기별 개수와 출발 위치를 보관해 나눠 발사하며 임의 금액 묶기와 가상 도착 초안을 대체했다.
- Main에 미리 배치할 `GemstoneCollectionController`의 Inspector 그룹·조정값·6종 HUD 참조·Play Mode 적용 시점·값 복사와 Edit Mode 붙여넣기·보상을 지급하지 않는 검수 버튼을 명세했다. 실제 보상과 화면 표시 장부 분리, 중복 정산 차단, 종료/재시작, 숫자 정밀도, 오디오, 효과 감소 및 출시 부하 검수 기준을 포함했다. 컨트롤러와 새 이벤트는 구현 예정이며 아직 추가하지 않았다.
- 사용자 설치 DOTween의 코어 DLL/모듈/설정 및 패키지 메타 버전 1.3.045를 확인했다. 코어 Transform 스케일·진행률 Tween으로 TMP 전용 모듈 없이 구성하는 방안을 기록했다. 설치 확인은 컴파일 검증을 의미하지 않는다.
- 대화용 동작 시안에서 43/48개 분해와 도착 후 표시량, 20마리 예시의 154개 오브젝트·총수량 902, 320px 내용 폭의 HUD 배치를 브라우저로 확인했다. JavaScript 구문 검사·브라우저 오류 확인·협업 정적 검사·공백 검사가 통과했다. 이 작업은 기획 문서만 변경했으며 Unity 런타임 코드·Main 씬 배치·DOTween 연출 구현·Unity 실행/성능 검증은 수행하지 않았다.

### 2026-10-28 · App — 젬스톤 흡수 착점을 수량 텍스트로 수정

- 사용자 수정에 따라 흡수 기획과 대화용 시안의 최종 목적지를 각 젬의 수량 Text (TMP) 중심으로 변경했다. TargetPoint는 고정 수량 영역 중심에 두고 TMP 확대 Transform과 분리하며, 가운데 정렬·중앙 피벗으로 자릿수 증가와 연속 팝업에도 착점을 유지한다. 흡수광도 텍스트 뒤에 배치한다.
- 효과가 행 배경에 미리 가려지지 않도록 배경 → 비행 젬 → 수량 텍스트 순서를 명세하고 시안에 반영했다. Inspector 바인딩·텍스트 기준 착점 오프셋·출시 검수 기준도 갱신했다. Unity 런타임 구현과 씬 배치는 이번 기획 수정에 포함하지 않았다.

### 2026-10-28 · App/Combat — 젬스톤 흡수 연출 구현

- `CombatRunController.RewardResolved`와 값 타입 `CombatRewardResolved`를 추가했다. 최종 가넷·추가 젬을 먼저 계산/검증한 뒤 함께 기록하고 사망 전 몸 위치·런 ID·처치 순번과 전달한다. 보너스가 가넷이면 합쳐 기록하며, 오버플로 시 일부 재화만 지급하지 않는다. 부활 개체의 기존 중복 정산 방지와 드롭 난수·배율 경로를 유지한다.
- Main 루트에 `GemstoneCollectionController`와 6종 고정 텍스트 착점을 배치했다. `GemstoneFlyView` 풀은 1/5/10으로 분해한 모든 젬을 원래 출처에서 분출·가속 비행시키고, 도착 시 `GemstoneCounterPresenter`가 정확한 Int64 수량과 DOTween 팝업을 갱신한다. 풀 포화는 크기별 개수를 보존한 대기로 처리한다. 비행 Canvas는 배경 위, 수량 전경 아래에 두고 실제 텍스트 영역 중심과 TargetPoint를 일치시켰다.
- `CombatGemstoneList`는 표시 종류를 관리하고 연출이 연결되면 직접 수량 덮어쓰기를 멈춘다. `PrototypeRunHud`는 종료 정산과 별도로 최대 0.30초 흡수 마무리 뒤 결과를 표시한다. 시작/취소/재시작/비활성화/포커스·일시정지에 풀·오디오·소유 트윈을 정리하거나 멈춘다. 전역 DOTween 종료 API는 사용하지 않는다.
- Main Inspector에서 크기·분출·궤적·가속 곡선·잔상·흡수광·팝업·음량·풀 예산을 설정한다. 43/48/80마리 테스트는 실제 지급 없이 전투 표시 화면만 잠시 보여주고 복원한다. Play Mode 조정값 복사와 Edit Mode 붙여넣기·Undo·씬 저장을 지원한다. App 소유 기본/추가 젬 합성 수집음을 연결했으며 교체 가능하다.
- 격리 Unity 6000.3.13f1에서 관련 EditMode 45개와 Main PlayMode 검증이 통과했다. 정확한 분해·도착 전 수량 고정·종류별 표시·중복/오래된 사건 차단·일시정지/포커스·Int64 최대값·종료·풀 포화·100회 재시작·80×43 보상의 560개 회수·실제 지갑 불변을 확인했다. 기본 풀의 합성 80마리 테스트는 표시 시계 2초 내 회수를 확인했으며 실기기 프레임 시간 측정과는 별개다.
- 별도 임시 PlayMode 렌더링으로 Main의 실제 배경/2열 HUD/비행 젬/숫자 팝업을 확인했다. 협업 경계·씬 로컬 참조·공백 검사를 수행했다. 목표 최소 기기에서의 CPU/GPU 프로파일링과 최종 사운드 믹싱은 남아 있으며 출시 성능 달성을 주장하지 않는다.

### 2026-10-28 · App — InGameObjects Timer에 남은 전투 시간 연결

- `PrototypeRunHud.timerText`에 Main의 `InGameObjects/Timer/Text (TMP)`를 직접 연결했다. 사용자 요청에 따라 `CombatRunController.RemainingSeconds`를 1/100초 단위로 올림하여 `초:1/100초`로 표시한다(15초 = `15:00`, 14.75초 = `14:75`). 값이 바뀔 때 TMP의 숫자 포맷 API로 갱신하며 매 프레임 새 문자열을 만들지 않는다. Main에서는 별도 `PrototypeTimerText`를 생성하지 않고 기존 모래시계·폰트·외곽선·배치를 사용한다. 연결이 없는 개발 씬의 임시 HUD는 유지한다.
- 남은 시간이 3초 이하이면 다홍색 `#FF664D`로 바뀐다. `PrototypeRunHud.Timer Warning Color`에서 조절할 수 있으며 초기화·재시작에는 원래 텍스트 색을 복원한다. 일시정지에는 전투 시계를 그대로 따르고, 시간이 완전히 끝나야 `00:00`으로 표시한다. Timer에 복사되어 있던 `ReturnToMainMenu` Button과 배경·텍스트 Raycast를 제거해 시간 표시를 눌러 전투가 종료되지 않도록 했다.
- 격리 Unity 6000.3.13f1에서 Main PlayMode 2개(기존 젬스톤 HUD와 새 Timer 수명주기 검증)가 통과했다. 지갑 불변·임시 타이머 중복 없음·일시정지·소수 초·3초 색상 경계·만료·메뉴 복귀·재시작을 확인했다. 협업 정적 검사와 씬 참조 검사, 변경 C# 공백 검사를 통과했다. 전체 diff에는 이번 작업 전 Unity에서 다시 저장된 Main 씬의 기존 직렬화 공백이 남아 있다. 별도 시각 캡처는 수행하지 않았다.

### 2026-10-28 · App — TimeOutPanel과 ResultUI 전투 종료 연출

- Main에 사용자가 추가한 `TimeOutPanel`/`ResultUI`를 재사용해 `BattleEndOverlay`에 연결했다. TimeOut은 0.38초 스케일 팝업 → 0.65초 유지 → 0.18초 페이드 퇴장 후 비활성화되며, ResultUI는 0.32초 진입 후 젬 6종을 0.09초 간격으로 공개하고 0.85초 동안 수량을 올린다. 마지막 젬 흡수가 끝나는 것을 확인한 뒤 결과로 넘어간다. unscaled time을 사용하며 Inspector에서 시간과 움직임 감소를 조절한다.
- 결과에는 실제 `RunResult`의 종류별 보상·처치 수·경과 시간·유효 피해량만 표시한다. 미획득 젬은 0으로 약하게 표시하고, 종류는 ID로 대응한다. Int64 카운트업은 decimal 보간과 정확한 마지막 정수 대입으로 최대값을 보존한다. 배경/패널 클릭은 결과 집계만 완료하고, 연출 완료 전에는 이동 버튼을 잠근다. 테스트 런은 미지급 안내를 표시한다.
- 업그레이드 버튼은 로비로 복귀하고, 다시 전투하기는 직전 Stage 선택과 현재 성장 상태로 새로운 RunId를 만든다. 빠른 연속 클릭·메뉴 전환·Reset·비활성화 시 연출 상태를 정리한다. HUD는 현재 런 ID와 종료 수신 여부를 확인해 중복·이전 런 결과가 새 전투를 덮지 않게 한다. 실제 보상 지급/저장은 기존 HuntManager 정산 경로에 남으며 연출은 지갑을 변경하지 않는다.
- 프레임·버튼·아이콘은 `Assets/DownLoadAssets/GUI-CasualFantasy`, 스파클과 숫자 폰트는 `Casual_GUI/GUI Pro-SimpleCasual`의 에셋을 참조한다. 한글은 기존 공용 NanumGothic 폰트를 런타임 폴백으로 사용한다. 공급사 원본은 수정하지 않았다. 기존 Main의 두 오브젝트와 씬 변경을 유지했다. `BattleResultSceneSetup`은 명시적 씬 구성 메뉴이며 Import 시 자동 변경하지 않는다.
- 격리 Unity 6000.3.13f1에서 컴파일, 관련 EditMode 48개와 Main PlayMode 4개가 통과했다. TimeOut→결과 순서·timeScale=0·6종/0 보상·Int64 최대값·재전투 중복 방지·오래된 결과·로비 복귀·취소·기존 타이머/흡수 동작·PlayerPrefs 불변을 확인했다. Main 렌더로 TimeOut 및 1920×1080/1280×960 결과 레이아웃을 확인했다. 시각 검증 결과 화면의 숫자는 테스트용이며 실제 지갑에 지급하지 않았다. 협업 경계와 Main 로컬 참조 검사도 통과했다.

### 2026-10-28 · App — 전투 결과를 투박한 스타일로 단순화하고 미획득 젬 숨김

- 사용자 피드백에 따라 결과창을 회색 사각 패널·얇은 테두리·단색 버튼으로 변경했다. 영문 보고서 제목·금색 장식·행 배경·스파클·격려 문구·버튼 부제와 부드러운 튕김/순차 등장/아이콘 맥동을 제거했다. TimeOut은 0.15초 두 단계 스케일 팝업, 결과는 즉시 등장하며 보상 숫자만 0.3초 동안 8단계로 집계한다. 전투 기록은 바로 최종값을 표시한다.
- 이번 `RunResult`에서 획득량이 양수인 젬 행만 활성화한다. 미획득 종류는 이름·아이콘·0 표시·자리까지 숨기고, VerticalLayoutGroup으로 획득한 종류를 빈칸 없이 정렬한다. 결과창 높이도 실제 표시 행 수에 맞춰 조절한다. 모두 0이면 종류를 노출하지 않고 `획득한 보상이 없습니다.`를 표시한다. 이전 전투에서 획득했던 종류도 이번에 없으면 다시 숨긴다.
- 다운로드 에셋의 사각 스프라이트를 9-slice로 사용해 모서리가 늘어나지 않게 했고, TimeOut prefab 인스턴스의 외형 override를 명시적으로 저장한다. 원본 공급사 에셋은 수정하지 않는다. 업그레이드/재전투·정산·중복/오래된 런 차단은 기존 연결을 유지한다.
- Unity 6000.3.13f1 격리 검증 프로젝트에서 컴파일과 결과 화면 PlayMode 3개가 통과했다. 일부/전체/0/다음 전투의 서로 다른 보상·빈칸 없는 정렬·첫 결과 프레임 이전 숨김·버튼 이동·PlayerPrefs 불변을 확인했다. TimeOut, 가넷만 획득한 결과, 2종/6종 결과 및 16:9·4:3 렌더를 확인했다. 협업 경계와 Main 씬 로컬 참조 검사도 통과했다.

### 2026-10-28 · App — 결과 집계 후 버튼 DOTween 순차 팝업

- 메인 결과의 보상 집계가 끝날 때까지 두 버튼을 숨긴다. 이후 DOTween Sequence의 Append로 업그레이드(로비) 스케일 팝업을 완료하고 다시 전투하기 팝업을 재생한다. 각 0.22초, 작은 OutBack 반동이며 Main의 `Button Popup Seconds`에서 조절한다. 두 연출이 끝나면 이동 버튼을 활성화한다. 결과 집계 생략 클릭은 버튼 순서를 건너뛰거나 재시작하지 않는다.
- unscaled update로 전투 시간이 멈춰도 재생하며 Hide/비활성화/새 결과에서는 해당 Sequence만 Kill하고 버튼 스케일을 초기화한다. 움직임 감소 설정에서는 두 버튼을 바로 표시한다. 기존 투박한 외형과 이번 전투의 미획득 젬 숨김을 유지한다.
- Unity 6000.3.13f1 격리 프로젝트에서 컴파일과 결과 PlayMode 5개가 통과했다. 실제 프레임의 집계→업그레이드 완료→재전투 시작 순서, timeScale=0, 연출 중 이동 차단, 중복 클릭, 취소 후 재진입, 움직임 감소, 기존 이동/보상 표시 및 PlayerPrefs 불변을 확인했다. 협업 경계 검사도 통과했다.
