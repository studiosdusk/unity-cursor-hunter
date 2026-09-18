# Cursor Hunter 협업 누적 인수인계 로그

이 문서는 두 담당자의 작업이 이어질 때마다 끝에 기록을 추가하는 누적 로그다. 이전
기록은 수정하지 않고, 현재 상태와 다음 합류에 필요한 경계를 함께 남긴다. 기획의
정본은 [cursor-hunter-game-design-v0.0.4.md](../cursor-hunter-game-design-v0.0.4.md),
현재 특성 화면의 실제 매핑은 [trait-ui-assets.md](trait-ui-assets.md)다.

## 현재 기준

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
반경 11, 다중 클릭 5, 치명타 8, 젬 수집 6, 보스 공격력 5와 젬 해금·발견 15개로
구성한다. 스킬은 파이어볼·라이트닝 볼트·프리즈닝·허리케인·메테오·드래곤 브레스
6종×4노드(24개), 몬스터는 `Monster1`~`Monster20` 해금 20개와 종별 생산량 강화
60개다. 모든 행은 인접 노드를 런타임 선으로 연결하고, 노드 셀은 동일한 정사각형으로 그린다.

## 누적 변경 기록

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
- Stat을 공격력 10, 반경 11, 다중 클릭 5, 치명타 8, 젬 수집 6, 보스 공격력 5와
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
