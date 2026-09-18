# 런과 영구 데이터의 연결 계약 v0
구현 전에 두 담당자가 이 명세를 기준으로 Contracts/Runtime에 C# 타입을 추가한다. 아래 이름은 새로 구현할 계약 이름이다.

## 데이터 경계
| 계약 | 필수 데이터 | 생산 → 소비 |
|---|---|---|
| RunRequest | runId, schemaVersion, balanceVersion, 모드, bossId(일반은 없음), seed, durationSeconds=60 | App → Combat |
| CombatSnapshot | attack(long), radiusPixels, hitsPerBundle, autoBundlesPerSecond, 자동 ON, 스킬별 계수/주기 | Progression → App → Combat |
| SpawnSnapshot | monsterId, HP(long), 주기, 무리 수, 종별 상한/전체 상한, 보상표 | Data+Progression → App → Combat |
| RunProgress | runId, sequence, 경과시간, 종별 누적 처치, 유효 피해(long), 파훼 성공/실패, 남은 시간 | Combat → App |
| RunResult | runId, 종료 사유, snapshot 버전, 종별 처치, 경과시간, 피해, 보스 성공, 파훼 수 | Combat → App → Progression |
| SettlementReceipt | runId, 지급 재화, 첫 처치 여부, 새 해금, 저장 성공/오류 | Progression → App |
배열/컬렉션은 복사한 읽기 전용 스냅샷으로 넘긴다. ScriptableObject, GameObject, 저장 파일 경로를 계약에 넣지 않는다.
초기 공개 동작은 Start(request, snapshot), Pause, Resume, Abort와 Progress/Completed 알림으로 제한한다. 실제 메서드 이름/이벤트 형태는 첫 계약 PR에서 확정한다.

## 젬스톤 드롭 스냅샷

젬스톤의 해금 여부와 등장 확률은 Progression이 소유한다. 보스 처치는 보스·몬스터·
스킬 해금 이벤트를 만들 수 있지만 젬스톤 슬롯을 자동으로 추가하지 않는다. 준영이
Stat의 `stat.gemstone.unlock.*` 노드를 구매하면 해당 젬스톤이 다음 런의 드롭 표에
들어간다. `stat.gemstone.rate.<type>.*` 구매는 해당 종류의 가중치만 올린다.

Combat이 받아야 하는 읽기 전용 값은 다음과 같다.

| 필드 | 의미 |
|---|---|
| `gemstoneId` | `gem.garnet` 등 저장·정산에 사용하는 안정적인 ID |
| `isUnlocked` | Stat 해금 노드 구매 여부. 잠긴 종류는 전투 드롭 후보에서 제외 |
| `dropWeight` | `min(maxWeight, baseWeight + rateUpgradeCount × stepWeight)` |
| `chancePercent` | 해금된 `dropWeight` 합으로 정규화한 표시·검증용 확률 |

전투는 이 표를 UI에서 다시 계산하지 않는다. 런 시작 시 받은 스냅샷과 `seed`로 한 번의
드롭을 결정하고, `RunProgress`·`RunResult`에는 실제로 선택된 `gemstoneId`와 수량만
기록한다. UI 프로토타입의 `TraitScreenController.TryRollGemstoneDrop`은 이 경계를
검증하기 위한 임시 구현이며, 첫 합류 PR에서 Progression 서비스로 이동한다.

## 정산 권한과 복구
1. App이 고유 runId와 수치 버전을 발급하고 저장한 뒤 전투를 시작한다. 런 중 특성 구매는 허용하지 않는다.
2. Combat은 피해·처치 사실을 집계한다. VFX나 UI에서 재화·프로필을 변경하지 않는다.
3. 일반 처치 즉시 런 누적 보상을 증가시키고 Progress를 전달한다. App/Progression은 sequence로 중복을 제외하고 복구용 누적 기록을 보관한다. 시각 흡수 애니메이션과 지급 사실은 독립적이다.
4. 영구 지갑 반영은 정산 트랜잭션 한 번으로 수행한다. 런 중 보유 표시는 영구 지갑+해당 런 미정산 금액이다. 중도 종료/복구도 같은 runId로 처리한다.
5. 보스는 성공 시에만 보상. 실패/포기는 0이며 파훼 보너스도 확정하지 않는다.
6. 최초 보스 처치 플래그·보상·처리한 runId를 하나의 저장 단위로 확정한다. 젬스톤
   해금은 Stat 구매 트랜잭션으로 별도 확정하며, 보스 정산이 젬스톤 슬롯을 자동으로
   추가하지 않는다.
7. 저장 실패에서는 성공 영수증을 내보내지 않고 같은 runId로 재시도한다. 저장 성공을 확인한 App만 다음 런으로 이동한다.
8. 저장 큐는 순차 실행. 취소·앱 종료·복구 중 중복 정산에 대해 검증한다. 미저장 구간의 복구 허용 범위는 실제 저장 구현 때 명시한다.

## 계산·입력 불변 조건
- HP·피해·재화는 Int64. v5 HP 2,700,000,000은 Int32 범위를 넘는다. 합계·곱셈에는 overflow 방어.
- 반경 단위는 1920×1080 기준 픽셀. 월드 좌표 변환은 Combat의 책임. UI 배율과 타격 판정을 분리한다.
- 다중 1→2→3→4타 후 자동 무한(별도 초당 분기 없음). 전투 내부 자동 타격 간격·스킬 쿨타임은 런타임 필드로 전달하지만 구매형 초당 Stat은 두지 않는다. 자동 중 수동 기본 피해 중복 없음. 병렬 타격은 동시 논리 타격이며 스레드 요구가 아니다.
- t=60 생성/타격 없음. 만료 이전 HP=0이면 성공, 만료 시각에 처음 HP=0이 되는 입력은 무효.
- 과잉 피해와 정지 시간은 DPS에서 제외. 0초 경과 DPS는 0. 스킬 피해 포함.
- 보스 파훼는 수동 입력. 스킬/자동 클릭이 대신 누르지 않는다.
- 앱 포커스·Overlay·ESC 시 전투 시계와 스킬/파훼 모두 정지. 재개는 새 입력.
- 이벤트 구독은 화면/런 수명과 함께 해제. 오래된 runId 콜백은 무시. Unity 오브젝트는 메인 스레드에서만 접근.
- 매 타격마다 LINQ·전체 프로필 복사·파일 저장 금지. Combat은 풀링과 누적 집계, UI는 별도 갱신 주기를 사용한다.
- 일반 종은 해금 이후 누적. 젬 UI는 해금된 것만. Main에 지갑 없음.
- 새 게임에는 가넷만 노출한다. 토파즈 → 자수정 → 사파이어 → 다이아몬드 → 드래곤 젬은 Stat
  해금 노드의 순서로 구매하며, 구매 전 이름·아이콘·수량을 HUD에 그리지 않는다.
- 해금 비용은 직전 단계 재화로 지불한다(토파즈←가넷, 자수정←토파즈,
  사파이어←자수정, 다이아몬드←사파이어, 드래곤 젬←다이아몬드). 해금 후 발견
  I·II의 비용은 해당 젬스톤이다. `CostGemstoneId`가 이 규칙을 단일 원천으로 가진다.

## 첫 합류에서 확인할 사례
가넷 1종 / 특성 구매 후 다음 런만 변경 / 일반 포기 처치 보상 / 보스 실패 0 / 같은 runId 2회 수신 / 저장 실패 재시도 / 종료 직전 마지막 타격 / 화면 이탈 후 이벤트 / v5 HP / 미해금 젬 숨김.
계약 변경은 생산자·소비자·테스트 더블을 같은 PR에서 갱신한다. 저장 schemaVersion 변경에는 구버전 복구/마이그레이션을 포함한다.

## 최신 확정 진행 스냅샷 확장

이번 개정에서 `TraitNodeDefinition.CostGemstoneId`가 추가됐다. Progression은
런 시작 시 구매 결과를 다음 읽기 전용 구조로 변환해 Combat에 전달한다.

| 구조 | 필드 | 의미 |
|---|---|---|
| `SkillSnapshot` | `skillId`, `unlocked`, `damageMultiplier`, `radiusMultiplier`, `cooldownMultiplier`, `currencyId` | 6종 스킬과 3단계 강화 결과. `dragonBreath`는 v4 해금이며 최종 피해·범위 연출을 사용 |
| `MonsterSnapshot` | `monsterId`, `unlocked`, `hp`, `spawnInterval`, `productionMultiplier`, `featureFlags` | Monster1~20의 HP·젠 주기·생산량 3단계·이동/방패/등장 특징 |
| `LootSnapshot` | `lootId`, `sourceId`, `dropWeight`, `effectType`, `effectValue`, `owned` | 일반 20종·보스 5종의 확률 전리품. Combat은 `lootId`를 결과에 기록하고 지갑을 직접 변경하지 않음 |
| `PetSnapshot` | `petId`, `count`, `radiusMultiplier`, `damageMultiplier`, `cooldownMultiplier`, `enabled` | 1마리 자동 공격 펫의 개별 커서 영역과 강화 결과 |
| `GemstoneBalance` | `gemstoneId`, `amount`, `unlocked`, `dropWeight`, `chancePercent` | 가넷·토파즈·자수정·사파이어·다이아몬드·드래곤 젬. 잠긴 종류는 Combat 후보에서 제외 |

`hitsPerBundle`는 1, 2, 3, 4로 증가하며 마지막 노드는 `autoEnabled=true`를
허용한다. 자동 무한 이후 초당 횟수를 Stat에서 다시 분기하지 않는다. 반복 입력은
동일 입력 묶음 안에서 모든 타격을 병렬 처리하고, 한 타격으로 사망한 적에게 남은
타격을 옮기지 않는다.

## Monster20 초안 밸런스(Combat 소비용)

아래 값은 첫 전투 플레이테스트용 시작값이다. HP는 Int64로 저장하고, `productionMultiplier`
는 해금 후 I/II/III에서 1.25/1.50/1.75로 곱한다. 낮은 Monster도 모든 후반 런에
계속 등장한다.

| ID | HP | 기본 젠 주기 | 특징 |
|---|---:|---:|---|
| Monster1 | 20 | 1.2s | 기본형 |
| Monster2 | 45 | 1.4s | 빠른 이동 |
| Monster3 | 70 | 1.6s | 나타남/사라짐 |
| Monster4 | 140 | 1.8s | 높은 체력 |
| Monster5 | 220 | 2.0s | 방패 |
| Monster6 | 500 | 2.2s | 점프 |
| Monster7 | 350 | 1.7s | 작은 크기 |
| Monster8 | 900 | 2.5s | 큰 크기·느림 |
| Monster9 | 1,200 | 2.4s | 지그재그 |
| Monster10 | 4,000 | 2.8s | 짧은 순간이동 |
| Monster11 | 6,000 | 2.6s | 방향 급변 |
| Monster12 | 8,000 | 3.0s | 원형 이동 |
| Monster13 | 12,000 | 3.2s | 처치 시 분리 |
| Monster14 | 50,000 | 3.5s | 잔상 |
| Monster15 | 70,000 | 3.2s | 크기 변화 |
| Monster16 | 90,000 | 3.8s | 가장자리 재등장 |
| Monster17 | 400,000 | 4.0s | 여러 겹 방패 |
| Monster18 | 650,000 | 3.6s | 구간 급가속 |
| Monster19 | 900,000 | 4.2s | 무리 이동 |
| Monster20 | 2,200,000 | 4.8s | 특징 무작위 조합 |

이 수치는 현재 UI 카탈로그의 표시값과 분리된 Combat 초안이다. v1~v5 보스 HP,
플레이어 실측 DPS, 한 필드의 생존 상한을 첫 60초 런 로그로 검증하고 `balanceVersion`을
올린다.

보스 HP의 첫 기준값은 v1 1,300 / v2 12,000 / v3 180,000 / v4 18,000,000 /
v5 2,700,000,000이다. Progression은 이 값을 UI에 복제하지 않으며, 승기는 보스
런 스냅샷의 `bossId`에 따라 전투 전용 밸런스 테이블에서 읽는다. 스킬 첫 값은
파이어볼 4A, 라이트닝 8A, 프리즈닝 12A, 허리케인 6A×6틱, 메테오 40A,
드래곤 브레스 250A이며, 각 스킬의 강화 배율은 피해 +35%·범위 +30%·쿨타임 -20%다.
