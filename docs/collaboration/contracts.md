# 런과 플레이어 상태 연결 계약 — JSON v3 (2026-10-01)

## 전투에 필요한 현재 정보만 전달한다

[game-data.en.json](../../Assets/CursorHunter/Data/Resources/GameData/game-data.en.json)은 GameInformation과 같은 구조의 초기값이다.
성장 화면은 [progression-config.en.json](../../Assets/CursorHunter/Data/Resources/GameData/progression-config.en.json)의 비용/선행 조건/효과와 플레이어 구매/지갑을 사용한다.
GameInformationBuilder가 최종 정보를 만든 뒤 App이 Combat에 전달한다. 강화 설정 자체는 전투 입력이 아니다.

```text
game-data.en.json + progression-config.en.json + 구매/지갑
 → GameInformationBuilder → GameInformation
 → 메모리 JSON → 검증 → ProgressionCombatSnapshot
 → App.HuntManager / RunCoordinator → Combat
```

영문 GameInformation의 루트는 schemaVersion=3, balanceVersion, stats, rules, gemstones, monsters, skills다.
information 래퍼, progression, entities, fieldHelp, implementation, relics는 포함하지 않는다.
한국어 두 참고본은 docs 아래에만 있으며 게임에서 읽지 않는다.

## 필드와 단위

| 영역 | 필드 |
|---|---|
| stats | attackPower, attackRadiusWorldUnits, attackCooldownSeconds, criticalChancePercent, bossDamageMultiplier, normalFieldDurationSeconds |
| rules | criticalDamageMultiplier, globalAliveLimit, perMonsterAliveLimit, bossFieldDurationSeconds |
| gemstones[6] | id, enabled, amount |
| monsters[10] | id, enabled, productionCount, hitPoints, spawnIntervalSeconds, garnetReward, gemstoneId, gemstoneAmount, gemstoneChancePercent, behaviorType |
| skills[7] | id, enabled, damage, radiusWorldUnits, cooldownSeconds |

HP/공격력/피해/재화는 Int64. 시간은 초, 반경은 월드 단위, 확률은 0~100이다.
productionCount는 1회 생성 마릿수다. gems.enabled는 공급 몬스터 활성 여부이며 잔액과 독립적이다.
false인 항목도 삭제하지 않고 ID로 구분한다. 배열 인덱스를 ID처럼 하드코딩하지 않는다.
일반 필드는 15~30초, 최대 동시 몬스터 80이다. 보스 기본 시간 60초는 일반 필드 상한과 별개다.

정식 [v3 스키마](game-information-v3.schema.json)와 [전체 예제](game-information-v3.example.json)를 따른다.
이전 v2 예제·스키마는 과거 기록이며 v3 런 입력으로 사용할 수 없다.

## 몬스터 특성 enum: 자리만 준비

Contracts.MonsterBehaviorType에는 현재 None=0만 정의한다.
JSON의 behaviorType은 숫자 0이며, 미정 특성을 임의 배정하지 않았다.
지그재그/은신/주기적 무적은 아직 enum 멤버나 행동 로직으로 구현하지 않았다.
향후 추가할 때 숫자 코드를 고정하고 계약·스키마·테스트·Combat 행동을 함께 확장한다.

```text
MonsterInformation.behaviorType
 → MonsterCombatSnapshot.BehaviorType
 → SpawnSnapshot.BehaviorType
 → WalkerStumpTarget.BehaviorType
```

잘못된 enum 코드는 유효성 검사에서 거절한다. 이 필드는 런 중 바꾸지 않는다.

## 전투에서 값을 읽는 실제 API

아래 controller는 현재 CombatRunController, snapshot은 App이 캡처한 ProgressionCombatSnapshot이다.
전역 변수나 새로 추가한 전체 데이터 getter를 뜻하지 않는다.
HuntManager.BeginPrototypeRun이 전체 스냅샷을 만들고,
RunCoordinator.Start에 CombatSnapshot/SpawnPlan, ConfigureSkills에 스킬 목록을 전달한다.

| 정보 | 실제 프로퍼티 |
|---|---|
| 공격력 | controller.CombatSnapshot.AttackPower |
| 반경 배율 / 월드 반경 | controller.CombatSnapshot.RangeMultiplier / GameInformation.BaseAttackRadiusWorldUnits * RangeMultiplier |
| 기본 공격 쿨타임 | controller.CombatSnapshot.AttackCooldownSeconds |
| 치명타 확률 / 피해 배율 | controller.CombatSnapshot.CriticalChancePercent / CriticalDamageMultiplier |
| 보스 피해 배율 | controller.CombatSnapshot.BossDamageMultiplier |
| 자동 공격 여부 / 주기 | controller.CombatSnapshot.AutoAttackEnabled / AutoAttackIntervalSeconds |
| 스킬 정보 | ConfigureSkills로 받은 SkillCombatSnapshot의 SkillId, Unlocked, Damage, RadiusWorldUnits, CooldownSeconds |
| 몬스터 생성 | SpawnPlan.Entries[i].Snapshot의 MonsterId, MaxHealth, PackSize, SpawnIntervalSeconds, AliveLimit |
| 몬스터 특성 | SpawnPlan.Entries[i].Snapshot.BehaviorType 또는 개체 WalkerStumpTarget.BehaviorType |
| 가넷 / 추가 젬 보상 | SpawnSnapshot.GarnetReward / BonusDropCurrencyId, BonusDropAmount, BonusDropChancePercent |
| 개체 현재 체력 | WalkerStumpTarget.CurrentHealth (MaxHealth는 시작/최대 체력) |
| 런 진행 | controller.ElapsedSeconds, RemainingSeconds, DefeatedCount, GarnetEarned, EffectiveDamage |
| 일반/보스 제한 시간 | snapshot.NormalFieldDurationSeconds / BossFieldDurationSeconds |
| 생성 상한 | snapshot.GlobalAliveLimit / PerMonsterAliveLimit |
| 전체 시작 정보 | snapshot.SourceJson → GameInformationJson.TryDeserialize (App에서만) |

CombatRunController 내부에서는 _combatSnapshot과 _skills를 읽는다.
외부에 Skills getter나 전체 ProgressionCombatSnapshot getter가 현재 존재하지 않는다.
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

## 유물 제거와 저장 호환

유물 배열·조각 필드·노드·효과·탭·몬스터/보스 조각 보상은 현재 구현에서 제거했다.
다음 일반 유물 시스템은 미정이므로 대체 효과를 추가하지 않았다.
저장 version은 3이며 첫 v3 저장 직전에 이전 원문을 cursor_hunter.progression.v1.before-v3에 한 번 백업한다.
옛 유물 구매/조각은 복원·지급·환불하지 않고 백업에만 남긴다. 일반 강화·젬은 유지한다.
유물 보정이 사라져 기존 최종 공격력·보스 배율·연동 스킬 피해가 낮아질 수 있다.

## 검증·아직 없는 기능

Python 정적 검사와 12개 데이터 테스트, C# 구문 검사. Unity 컴파일·게임·Editor 테스트는 실행하지 않았다.
스킬 고유 투사체/빙결/틱/브레스, 보스 패턴/패링, 전체 Localization, 영속 RunId journal은 미완료다.
프로세스 내 중복 정산 방지와 PlayerPrefs 저장의 기존 한계는 남아 있다.
메인 스레드 흐름을 유지하며 읽기 전용 배열만으로 전체 시스템의 멀티스레드 안전성을 주장하지 않는다.
