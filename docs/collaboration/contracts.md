# 런과 플레이어 상태 연결 계약 — JSON v4 (2026-10-22)

## 전투에 필요한 현재 정보만 전달한다

[game-data.en.json](../../Assets/CursorHunter/Data/Resources/GameData/game-data.en.json)은 GameInformation 구조와 비전투 초기값이다.
Main 씬의 `PlayerCombatStatsDefaults` 컴포넌트가 커서 핵심 스탯의 공통 기본값이다.
성장 화면은 [progression-config.en.json](../../Assets/CursorHunter/Data/Resources/GameData/progression-config.en.json)의 비용/선행 조건/증가량과 플레이어 구매/지갑을 사용한다.
GameInformationBuilder는 기본값에 구매한 증가량을 합산해 최종 전체 정보를 만든다. Progression은
커서 증가량 스냅샷도 App에 반환하며, 런타임은 매 런 기본값을 다시 읽고 증가량을 적용한다.

```text
PlayerCombatStatsDefaults + progression-config.en.json + 구매/지갑
 → GameInformationBuilder → GameInformation + CursorCombatStatBonusesSnapshot
 → 메모리 JSON → 검증 → ProgressionCombatSnapshot
 → App.HuntManager / RunCoordinator
 → PlayerCombatStatsRuntime resets from defaults and adds cursor bonuses
 → Combat
```

영문 GameInformation의 현재 루트는 schemaVersion=4, balanceVersion, stats, rules, gemstones, monsters, skills, relics, relicDropRules다.
information 래퍼, progression, entities, fieldHelp, implementation은 포함하지 않는다.
한국어 두 참고본은 docs 아래에만 있으며 게임에서 읽지 않는다.

## 필드와 단위

| 영역 | 필드 |
|---|---|
| stats | attackPower, attackRadiusWorldUnits, attackCooldownSeconds, criticalChancePercent, bossDamageMultiplier, normalFieldDurationSeconds |
| rules | criticalDamageMultiplier, globalAliveLimit, perMonsterAliveLimit, bossFieldDurationSeconds |
| gemstones[6] | id, enabled, amount |
| monsters[10] | id, enabled, productionCount, hitPoints, spawnIntervalSeconds, garnetReward, gemstoneId, gemstoneAmount, gemstoneChancePercent, behaviorType |
| skills[7] | `skill.tornado`, `skill.headshot`, `skill.thunder`, `skill.flame_exhaust`, `skill.magic_ball`, `skill.laser`, `skill.pet_baby_dragon`의 id, enabled, upgrades[statId, delta] |
| relics[20] | id, owned, sourceMonsterId, dropChancePercent, effectId, effectValue, effectValue2 |
| relicDropRules | maxPerKill=1, candidatePolicy=unownedOnly, resolution=sumFixedChanceThenWeightedPick, preserveBaseChanceAfterAcquisition=true |

HP/공격력/피해/재화는 Int64. 시간은 초, 반경은 월드 단위, 확률은 0~100이다.
productionCount는 1회 생성 마릿수다. gems.enabled는 공급 몬스터 활성 여부이며 잔액과 독립적이다.
false인 항목도 삭제하지 않고 ID로 구분한다. 배열 인덱스를 ID처럼 하드코딩하지 않는다.
일반 필드는 15~30초, 최대 동시 몬스터 80이다. 보스 기본 시간 60초는 일반 필드 상한과 별개다.

유물 효과 수치는 effectId가 단위를 정의한다. 퍼센트 효과의 5는 5%이며, 초·배수 값은 해당 단위를 그대로 쓴다. 크라켄의 눈만 effectValue2에 체력 기준 30%를 둔다. 몬스터별 두 유물의 기본 확률 합은 14%이며, 소유 유물을 후보에서 제외해도 나머지 확률은 변하지 않는다.

정식 [v4 스키마](game-information-v4.schema.json)와 [전체 예제](game-information-v4.example.json)를 따른다.
이전 v2/v3 예제·스키마는 과거 기록이며 v4 런 입력으로 사용할 수 없다.

## 몬스터 특성과 행동 프로필

JSON `behaviorType`과 `MonsterCombatSnapshot.BehaviorType`은 이전 데이터 호환을 위해
`Contracts.MonsterBehaviorType.None=0`으로 유지한다. 신규 특성은 Data의
`MonsterBehaviorProfile` 에셋으로 구성한다. 프로필의 체력·이동속도·비주얼 크기·명중
영역 배율은 곱해지며, 이동 모드와 특수 능력은 프로필 목록에서 마지막으로 지정한 값이
선택된다. 최종 수치는 `SpawnSnapshot`의 이동·크기·명중 영역·특수 능력 필드로 Combat에
전달된다.

```text
MonsterDefinition.BaseStats
 + MonsterDefinition.BehaviorProfiles[]
 + (MonsterCombatSnapshot.ProductionCount - initial count 1)
 → App SpawnPlan
 → SpawnSnapshot
 → MonsterRoot/MonsterBehaviorController
```

각 몬스터의 특성은 실제 전투에서 아래 설정으로 실행된다.

| ID | 이름 | 전투 특성 |
|---|---|---|
| monster.01 | 그루터기 | 기본 몬스터 |
| monster.02 | 박쥐 | 시각·피격 영역 0.62배 |
| monster.03 | 버섯 | 이동 속도 1.8배 |
| monster.04 | 가면유령 | 4초마다 1.2초간 사라져 피격 회피 |
| monster.05 | 전갈 | 4.5초마다 1.7초간 피해 65% 감소 |
| monster.06 | 눈요정 | 3.5초마다 이동 영역 안의 무작위 위치로 순간이동 |
| monster.07 | 거미 | 체력 2.5배 |
| monster.08 | 골렘 | 시각 크기 2배, 피격 영역 1.8배 |
| monster.09 | 봉오리 | 3.5초마다 반경 2.75 내 아군 체력을 최대 체력의 20% 회복 |
| monster.10 | 해골군주 | 4초마다 반경 3.5 내 쓰러진 아군을 최대 체력의 30%로 부활 |

시체는 부활 가능 시간을 위해 6초 동안 유지한다. 부활한 개체는 처치 보상을 한 번만
정산하므로 반복 부활로 재화를 복제하지 않는다. 능력 시계는 런 진행 시간을 사용해
일시정지 중에는 멈춘다. 종별 외형은 `MonsterDefinition.VisualPrefab`으로 유지한다.
기존 `behaviorType` JSON 필드는 계속 0만 허용한다.

## 전투에서 값을 읽는 실제 API

아래 controller는 현재 CombatRunController, snapshot은 App이 캡처한 ProgressionCombatSnapshot이다.
전역 변수나 새로 추가한 전체 데이터 getter를 뜻하지 않는다.
HuntManager.BeginPrototypeRun이 전체 정보와 커서 증가량 스냅샷을 만들고,
RunCoordinator.Start에 CombatSnapshot/커서 증가량/스킬 목록/SpawnPlan을 전달한다.
CombatRunController는 PrepareRun 이후 CommitStart 이전에 일곱 스킬의 BaseStat과
UpgradeStat을 합쳐 이번 런의 RuntimeStat으로 고정한다.
매직볼은 단일 오브젝트가 런 종료까지 유지되어 쿨타임 증가량을 받지 않는다.
허용된 증가량 ID는 `moveSpeedWorldUnitsPerSecond`, `sizeMultiplier`, `damageMultiplier`다.

| 정보 | 실제 프로퍼티 |
|---|---|
| 공격력 | controller.CombatSnapshot.AttackPower |
| 반경 배율 / 월드 반경 | controller.CombatSnapshot.RangeMultiplier / GameInformation.BaseAttackRadiusWorldUnits * RangeMultiplier |
| 기본 공격 쿨타임 | controller.CombatSnapshot.AttackCooldownSeconds |
| 치명타 확률 / 피해 배율 | controller.CombatSnapshot.CriticalChancePercent / CriticalDamageMultiplier |
| 보스 피해 배율 | controller.CombatSnapshot.BossDamageMultiplier |
| 자동 공격 여부 / 주기 | controller.CombatSnapshot.AutoAttackEnabled / AutoAttackIntervalSeconds |
| 스킬 정보 | SkillCombatSnapshot의 SkillId, Unlocked, Upgrades와 각 스킬 프리팹/Main override의 BaseStat을 합친 RuntimeStat. Editor Play Mode에서는 `Skill Acquired` 토글로 저장 구매 없이 시험 가능 |
| 몬스터 생성 | SpawnPlan.Entries[i].Snapshot의 MonsterId, MaxHealth, PackSize, SpawnIntervalSeconds, AliveLimit |
| 몬스터 특성 | SpawnPlan.Entries[i].Snapshot의 MovementMode, MoveSpeed, VisualScale, SpecialAbility 및 능력 수치 |
| 가넷 / 추가 젬 보상 | SpawnSnapshot.GarnetReward / BonusDropCurrencyId, BonusDropAmount, BonusDropChancePercent |
| 유물 정의·획득 상태 | `ProgressionCombatSnapshot.Relics[i]` → `CombatRunController.RelicSnapshots[i]`: RelicId, Owned, SourceMonsterId, DropChancePercent, EffectId, EffectValue, EffectValue2 |
| 유물 드롭 정책 | `ProgressionCombatSnapshot.RelicDropRules` → `CombatRunController.RelicDropRules`: MaxPerKill, CandidatePolicy, Resolution, PreserveBaseChanceAfterAcquisition |
| 개체 현재 체력 | MonsterCombatTarget.CurrentHealth (MaxHealth는 시작/최대 체력) |
| 런 진행 | controller.ElapsedSeconds, RemainingSeconds, DefeatedCount, GarnetEarned, EffectiveDamage |
| 일반/보스 제한 시간 | snapshot.NormalFieldDurationSeconds / BossFieldDurationSeconds |
| 생성 상한 | snapshot.GlobalAliveLimit / PerMonsterAliveLimit |
| 전체 시작 정보 | snapshot.SourceJson → GameInformationJson.TryDeserialize (App에서만) |

CombatRunController는 현재 런의 CombatSnapshot과 RunId를 사용한다.
외부에 Skills getter나 전체 ProgressionCombatSnapshot getter는 없다.
신규 Combat 하위 시스템에는 초기화 경로에서 필요한 스냅샷을 명시적으로 전달한다.

App에서 전체 시작 정보를 검사하는 메서드 예시(기존 App 클래스 안에 넣는 예시이지 이미 존재하는 메서드는 아님):

```csharp
using CursorHunter.Contracts;
using CursorHunter.Data;

public static bool TryReadRunStartInformation(
    ProgressionCombatSnapshot snapshot,
    out GameInformation information)
{
    information = null;
    if (snapshot == null || !snapshot.IsValid ||
        string.IsNullOrWhiteSpace(snapshot.SourceJson))
        return false;

    return GameInformationJson.TryDeserialize(
        snapshot.SourceJson, out information, out _);
}
```

성공하면 information.stats.attackPower, information.monsters[i].behaviorType 등을 읽는다.
gemstones는 id == "gem.garnet" 같은 식으로 찾고 amount를 읽는다.
Data 참조가 없는 Combat 어셈블리에서 위 파싱 메서드를 호출하거나 Progression UI/PlayerPrefs를 직접 참조하지 않는다.

## 시작 정보와 현재 상태를 혼동하지 않는다

- SourceJson의 젬 amount는 시작 시 잔액. 진행 중 수익은 GarnetEarned/종료 RunResult.Rewards다.
- 전체 정보 팝업/JSON 복사는 활성 런의 SourceJson을 표시하고, 런 밖에서는 현재 프로필을 합산한다. 결과 화면에서는 마지막 런 정보가 유지될 수 있다.
- 매 타격/매 프레임 JSON 파싱이나 스냅샷 재생성을 하지 않는다.
- 배열은 런 시작 시 복사되어 읽기 전용으로 노출된다. 후속 UI 구매는 다음 런부터 반영된다.
- GameInformation DTO 자체는 생성/역직렬화용 가변 객체다. Combat에는 복사된 계약 값을 전달한다.
- Combat이 지갑을 수정하지 않는다. 보상은 RunResult.Rewards → App 정산 → Progression 저장 경로를 유지한다.

## 유물과 저장 호환

20개 유물의 효과·출처·개별 확률은 GameInformation v4에 있으며, `GameInformationBuilder`는 도감/테스트 도구의 획득 ID를 `owned` 값에 반영한다. `ToCombatSnapshot`은 정의·보유 상태를 불변 `RelicCombatSnapshot` 목록과 드롭 규칙으로 복사한다. Combat은 이 목록을 읽을 수 있지만 현재 실제 드롭 롤, 결과 정산/저장, 스탯·스킬 효과를 적용하는 소비자는 아직 구현되지 않았다. 전체 드롭 알고리즘은 [유물 드롭 설계](../relic-drop-design.md)를 따른다.
저장 version은 4이며 첫 v4 저장 직전에 이전 원문을 cursor_hunter.progression.v1.before-v4에 한 번 백업한다. v1~v3의 암묵적 시작 노드 표시는 구매 상태에서 제거하고, 젬 잔액과 유료 진행 상태는 보존한다.
옛 유물 구매/조각은 복원·지급·환불하지 않고 백업에만 남긴다. 일반 강화·젬은 유지한다.
유물 보정이 사라져 기존 최종 공격력·보스 배율·연동 스킬 피해가 낮아질 수 있다.

## 검증·아직 없는 기능

자동 스킬은 7종 모두 구매 해금 노드를 가지며, Main 씬의 Editor 테스트 토글로 실제 전투 이펙트를 볼 수 있다. 스킬별 강화 구매 단계는 아직 구성하지 않았다. Main 화면에서 실제 이펙트와 발동 타이밍을 수동으로 확인하는 작업은 남아 있다.
스킬 고유 투사체/빙결/틱/브레스, 보스 패턴/패링, 전체 Localization, 영속 RunId journal은 미완료다.
프로세스 내 중복 정산 방지와 PlayerPrefs 저장의 기존 한계는 남아 있다.
메인 스레드 흐름을 유지하며 읽기 전용 배열만으로 전체 시스템의 멀티스레드 안전성을 주장하지 않는다.
