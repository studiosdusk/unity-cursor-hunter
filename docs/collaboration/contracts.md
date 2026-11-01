# 런과 플레이어 상태 연결 계약 — JSON v4 (2026-10-22)

## 전투에 필요한 현재 정보만 전달한다

현재 Main은 `HuntManager.Connect Trait Screen To Combat`가 켜져 있다. 기본 전투 시작은 특성 화면의 실제 구매·해금 상태를 `GameInformation`과 `ProgressionCombatSnapshot`으로 복사해 사용한다. 커서 스탯은 기본값과 구매 증가량을 포함한 최종 `CombatSnapshot`으로 한 번만 전달한다. 스킬은 실제 해금 및 지원되는 업그레이드를, 몬스터는 실제 해금과 생산 수를 소비한다. Main 전투의 기본 버튼은 기본 Stage 로스터나 수동 테스트값으로 이 데이터를 덮어쓰지 않는다.

[game-data.en.json](../../Assets/CursorHunter/Data/Resources/GameData/game-data.en.json)은 GameInformation 구조와 비전투 초기값이다.
Main 씬의 `PlayerCombatStatsDefaults` 컴포넌트가 커서 핵심 스탯의 공통 기본값이다.
성장 화면은 [progression-config.en.json](../../Assets/CursorHunter/Data/Resources/GameData/progression-config.en.json)의 비용/선행 조건/증가량과 플레이어 구매/지갑을 사용한다.
GameInformationBuilder는 기본값에 구매한 증가량을 합산해 최종 전체 정보를 만든다. Progression은
조회용 커서 증가량 스냅샷도 App에 반환하지만, Main 전투는 전체 정보에서 만든 최종
CombatSnapshot만 PlayerCombatStatsRuntime에 전달한다. Combat에서 기본값을 다시 더하지 않아
구매 증가량이 중복 적용되지 않는다.

```text
PlayerCombatStatsDefaults + progression-config.en.json + 실제 구매/지갑
 → GameInformationBuilder → 최종 GameInformation JSON
 → 검증 → ProgressionCombatSnapshot
 → App.HuntManager / RunCoordinator
 → PlayerCombatStatsRuntime captures final CombatSnapshot
 → Combat
```

현재 연구 데이터에서 스탯 노드는 공격력·반경·공격 간격·치명타 확률·필드 시간·보스 피해를 바꾼다. 베이비 드래곤과 토네이도·벼락·화염 배기·마력 구체·레이저는 단계별 해금 및 피해·범위 강화 노드를 갖는다. 스킬 피해는 각 스킬의 고유 기본 피해와 구매한 스킬 피해 증가량으로 정하며, 플레이어 공격력이나 커서 공격 반경에서 파생하지 않는다. 펫의 동시 타격 수도 별도 연구로 증가한다. 몬스터 노드는 출현 해금과 1회 생성 수를 바꾼다. 적의 HP·이동·행동·외형 등 기초 전투값은 각 `MonsterDefinition`과 행동 프로필에서 읽으며, 구매 가능한 특성 노드만 그 스냅샷에 더한다.

## 2026-10-29 · 온보딩과 초반 진행도

새 진행도는 중앙 `stat.attack.01` 연구 코어부터 시작한다. 신규/전체 초기화 프로필은 메인 Trait 버튼 → 코어와 기본 스탯 → 첫 공격력·반경 강화 → 펫 → 그루터기의 9단계 연구를 마쳐야 첫 전투에 진입한다. 기존 저장에는 이 온보딩을 강제하지 않으며 코어와 `monster.01`만 있으면 전투를 시작할 수 있다. 전투 결과의 강화 버튼은 실제 플레이 경로에서 특성 화면을 직접 연다. 수동 밸런스 테스트 경로는 로비로 돌아간다.

그루터기는 기본 생성 수 2마리, 생성 간격 0.75초다. 생산 연구는 3→4→5마리로 올린다. 이 값은 `productionCount`와 `spawnIntervalSeconds`만 변경한다. 체력은 `hitPoints` 및 `MonsterDefinition`/행동 프로필에서 따로 결정되며 생산량을 바꿔도 자동 증가하지 않는다. 현재 게임 정보의 종별 HP는 그루터기 8, 여왕벌 20, 독버섯 37, 가면유령 32, 갑옷병사 48, 눈요정 60, 벌집 90, 골렘 125, 봉오리 107, 판도라 상자 140이다. 몬스터 체력은 새 진행 단계의 평균 타격 수를 낮추기 위해 별도 조정했으며, 목표 타격 수와 장기 플레이 시간은 Unity 실전 검증 전이다.

베이비 드래곤은 보스 0단계에서 처음 배우며, 이후 회오리 1, 번개 2, 불꽃 투사체 3, 마력 구체 4, 레이저 5로 스킬을 하나씩 연다. 헤드샷 해금 노드는 없다. 진행도 저장은 최고 클리어 단계 0~20과 연구 해금 단계 0~5를 함께 보존한다. 적격 보스 승리의 `GrantBossClear` 정산이 성공할 때 클리어한 단계까지의 연구를 열고, 5단계 이후에는 연구 해금 단계를 5로 유지한다. 같은 단계 재도전·중복 결과·저장 실패·Combat Balance 테스트로 연구 해금 단계가 추가 상승하지 않는다. Main의 실제 보스 도전/대상/결과 흐름은 아래 보스 계약을 따른다.

Main의 PlayerCombatStatsDefaults는 공격력 10, 치명타 확률 5%, 치명타 피해 배율 2배다. 각 보스 해금 단계에서 공격력·치명타·몬스터 체력이 함께 성장한다. `game-data.en.json`과 MonsterDefinition의 체력은 같은 값을 사용한다. 첫 필드에서 그루터기 체력 8, 이후 몬스터는 여왕벌 20·독버섯 37·가면유령 32·갑옷병사 48·눈요정 60·벌집 90·골렘 125·봉오리 107·판도라 상자 140이다. 수치가 목표 타격 수에 맞는지는 런타임 타격 분포로 확인해야 한다.

Editor Play Mode에서는 `PrototypeRunHud`가 전투 남은 시간과 별도로 전체 누적 시간 및 현재 보스 준비 단계 시간을 표시한다. 타이머는 `Time.unscaledDeltaTime`으로 계산하고 앱 포커스 상실, 앱 일시정지, `timeScale=0`, 전투 일시정지 중에는 멈춘다. 테스트 타이머는 빌드에 포함되지 않는다. 특성 노드 비용은 하단 강조 영역에 젬 아이콘과 함께 표시하고, 현재 재화로 구매할 수 있는 노드만 좌상단 바깥에 배지를 표시한다. 노드 툴팁은 몬스터 특성과 실제 처치 보상만 간결히 표시하고 생산 간격 문구는 숨긴다. 보드 휠 확대 민감도는 낮췄다. 100:1 상위 젬스톤 교환은 누르고 있는 동안 반복된다.

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
일반 필드는 15~30초, 최대 동시 몬스터 80이다. 보스는 1~20단계 모두 30초이며 일반 시간 강화의 영향을 받지 않는다.

## 보스 도전 (2026-10-30)

- Editor의 `Combat Balance`는 Main에서 `구매한 플레이어 특성 사용`(`UsePlayerProgression=true`)이 기본이다. 일반·보스 모두 매 전투 시작에 최신 구매 스냅샷의 공격력·커서·7종 스킬·유물을 적용하고, 적 수치만 편집한다. 적의 라이브 변경은 현재 플레이어와 활성 스킬을 재설정하지 않는다. 옵션을 끈 직접 입력 모드에서만 플레이어 최종값을 교체한다. 직접 입력으로 시작한 전투는 종료 후 구매 모드로 전환한다. 일반 몬스터 편성과 단계별 보스 수치는 별도로 보존한다. 보스 탭에서 1~20단계 HP·속도·시각 크기·피격 영역을 편집하고 Play 중 선택 단계를 테스트할 수 있다. 해금 제한은 테스트 설정이 있을 때만 우회하며 테스트 토벌은 지갑·저장 최고 단계·세션 최고 단계를 변경하지 않는다.
- 보스 실시간 편집은 RunId·경과 시간·남은 HP 비율·동일 개체를 유지한다. 보스는 30초·한 마리를 유지하며 일반 스폰 편성을 끼워 넣지 않는다. `최종 설정 스탯 > Save`는 보스의 다섯 수치를 해당 SO에 다른 기본값과 함께 저장한다. 이름·번역·외형·특성·보상은 보존한다. 테스트 설정 해제 시 실제 진행도와 저장된 기본값을 다시 사용한다.

- Main의 보스 도전 버튼은 `HuntManager.BeginBossRun`으로 저장된 최고 클리어 단계의 다음 단계에 진입한다. 20단계 완료 후에는 20단계를 다시 도전할 수 있다.
- 버튼용 `HuntManager.ResetBossProgress()`는 최고 클리어 단계를 0으로 저장하고 세션의 보스 단계·재도전 상태를 지워 다음 도전을 1단계부터 시작한다. 구매·지갑·기지급 RunId는 유지한다. 미정산 토벌 보상은 먼저 저장하며 저장 실패 시 초기화를 취소한다. 성공 시 진행 중인 보스전과 결과 연출을 정리하고 전투 화면에서는 메인으로 돌아간다. 연구·설정 화면 및 진행 중인 일반 전투는 유지한다.
- `Data/Resources/BossDefinitions/Boss01~20.asset`의 공통 `BossDefinition`이 체력·이동속도·외형·특성·보상을 소유한다. 수치는 초기 밸런스다. App이 전투 수치를 `SpawnSnapshot`으로 복사해 별도 `BossRoot`에 적용한다. 1~5단계는 일반 몬스터의 외형을 재사용하고 6~20단계는 서로 다른 15개 원본의 `Combat/Prefabs/Monsters/BossVisuals` Variant를 사용한다.
- 1~5단계 이름은 일반 몬스터와 같은 그루터기·독버섯·눈요정·골렘·판도라 상자다. 6~20단계는 촛불정령·폭탄꽃·가시선인장·일개미·사마귀·식충꽃·왕눈파리·기계잠자리·악마벌·맹독거미·돌전갈·나무골렘·해적유령·해골전사·흡혈귀 순서다. 체력바 제목에는 단계 없이 보스 이름만 표시한다. [외형 매핑](assets.md#보스-620단계-전용-외형-2026-10-30)을 참고한다.
- 6~20단계 특성은 `specialAbility=None`, `movementMode=BoundedWander`, 기본 이동속도 `0.6`으로 통일한다. 일반 이동·피격·사망은 재생하며 순간이동·은신·방어막·회복·부활은 사용하지 않는다. 새 외형 중 반복하는 사망 클립은 프로젝트 소유 복사본과 Override Controller로 한 번만 재생한다. 각 단계의 체력·보상·제한시간은 기존 초기값을 유지한다.
- 보상은 SO의 `clearRewards`에 젬 ID·수량 목록으로 저장한다. App이 `RunRequest.BossClearRewards`에 복사하고 Combat이 토벌 성공 때 전체 목록을 한 번 정산한다. 일반 몬스터 해금 여부로 보스 보상을 제외하지 않는다. 초기 편성은 1~4단계 가넷·토파즈·자수정, 5~8단계 사파이어 추가, 9~12단계 다이아몬드 추가, 13~20단계 드래곤 젬 추가다. 일반 몬스터의 단일 추가 드롭 계약은 유지한다.
- 보스 이름 원본은 `Data/Authoring/BossNames.xlsx`의 `보스 이름` 시트다. A/B열은 `단계`/`보스 ID`, C열부터는 `ko`, `en`, `ja`, `zh-Hans` 같은 언어 코드이며 4행이 제목, 5~24행이 이름이다. `ko`는 필수이고 다른 번역은 비워둘 수 있다. 오른쪽에 언어 열을 추가하는 것만으로 새 언어를 가져오며 언어 열·보스 행 순서는 바꿀 수 있다. 대소문자·밑줄은 정규화하고 중복 언어는 거절한다. 기존 한국어 이름은 유지했으며 `en`은 번역 입력용 빈 열이다.
- Excel 저장 후 Unity로 복귀하면 ID를 기준으로 SO의 `displayName`(기본 한국어)와 `localizedNames`(언어 코드·이름 목록)를 갱신한다. 번역 삭제/열 제거 시 SO의 이전 번역도 제거한다. 플레이 중 변경은 Edit Mode 복귀 시 반영한다. 잘못된 표는 전체 반영을 취소하고 Console에 이유를 표시한다. `Tools > Cursor Hunter > Boss Names > Open Excel / Apply Excel` 메뉴도 제공한다. 런타임은 SO만 읽으므로 빌드에 Excel 실행 환경이 필요하지 않다.
- 게임 언어는 `LocalizationCatalog.SetLanguage("ja")`처럼 선택한다. 보스 이름은 선택 언어 → 상위 언어 → 한국어 순으로 찾는다(예: `zh-Hans-CN` → `zh-Hans` → `zh` → `ko`). `BossHealthPresenter`는 언어 변경 이벤트에서 표시 이름과 폰트를 갱신한다. 언어 선택 UI와 전체 게임 번역은 별도 범위다. 공용 UI JSON의 기존 `ko`/`en` 필드는 유지하며 추가 언어는 `translations: [{"locale":"ja","text":"…"}]`로 확장할 수 있다. 새 문자권 폰트는 `GameData/TypographySettings`의 `localeFonts`에 등록한다(선택/상위 언어 폰트 → 기본 `sourceFont`).
- `RunRequest`/`RunResult`에 `BossStage`가 있고 결과에는 `BossId`도 복사된다. Boss 전투는 ID가 일치하는 단일 스폰만 허용하고 재생성하지 않는다.
- 30초 전에 처치하면 `BossDefeated`, 정확히 만료한 시각부터는 피해 무효다. 시간 초과·포기 시 보상은 0이며 `Discard`로 종료한다.
- Main에 사용자가 배치한 `BossSlider`는 `BossHealthPresenter`가 체력 비율·현재/최대 HP·보스 이름을 표시한다. `Text_Title`에는 단계 접두어를 붙이지 않는다. HP가 감소하면 `Text_Value`가 원래 크기의 1.2배로 커졌다가 0.18초 동안 원래 크기로 돌아온다(Inspector 조정 가능). 연속 타격은 고정 최대 배율에서 다시 시작하며 실제 시간으로 복귀한다. 첫 표시·동일 HP는 효과를 재생하지 않고 숨김·재도전 시 원래 크기로 초기화한다. 일반 전투와 결과 화면에서는 숨긴다.
- BossSlider 글꼴은 결과창을 기본 테마로 사용한다. 이름은 지역화 글꼴 Bold(최대 38), 체력은 `TypographySettings.numericFont`의 Rubik SemiBold Bold(최대 28)로 표시한다. 각 라벨 전용 SDF 재질에 얇은 어두운 테두리를 적용하고 공유 폰트/결과창 재질은 변경하지 않는다. 가로 영역 확장·한 줄 자동 크기 조절로 긴 이름과 Int64 체력 표시를 수용하며 기존 피격 확대를 유지한다.
- 성공 시 최대 128개(풀 예산 이내)의 젬을 동시에 분출하고 HUD로 흡수한다. 보스 보상은 한 시각 오브젝트가 여러 개의 보석을 대표할 수 있으며 Int64 합계는 보존한다. 일반 전투의 1/5/10 표현은 유지한다.
- 토벌 치명타와 동시에 사망 애니메이션과 젬 분출을 시작한다. 전투 종료·보상 정산은 즉시 처리하되, 죽은 보스는 공격 판정·등록·부활 대상에서 제외한 연출용 개체로 남기고 사망 클립이 끝나면 제거한다. 사망은 실제 시간으로 재생하며 일반 몬스터의 6초 부활 유예를 기다리지 않는다. 애니메이터가 없거나 멈춘 경우에도 제한된 대기 후 정리한다. 다음 전투·메뉴 복귀·진행도 초기화는 남은 사망 연출을 취소하고, 시간 초과·포기는 사망 연출 없이 기존대로 정리한다.
- 토벌 성공 연출은 `SettlementPolicy`와 분리한다. Combat Balance의 `Discard` 성공도 동일하게 다종 젬 분출 → HUD 흡수 → 결과창을 재생한다. 테스트 결과의 수량은 연출용이며 지갑·클리어 기록은 저장하지 않는다. 실패·포기에서는 보스 분출을 재생하지 않는다.
- 성공은 사망 애니메이션과 젬 흡수가 모두 완료된 뒤 성공 팝업 → 기존 결과 UI로 진행한다. 실패는 기존 실패 팝업 → 결과 UI를 사용한다. 성공은 다음 단계 도전, 실패는 같은 단계 재도전이며 공통 강화 버튼은 특성 연동 플레이에서 특성 화면으로, 수동 테스트에서는 로비로 이동한다. 20단계 성공의 다음 행동은 일반 전투다. 자동으로 다음 보스를 시작하지 않는다.
- App 정산이 `TraitScreenController.GrantBossClear`를 통해 보상·유물 소유·최고 단계·연구 해금 단계·정산 RunId를 같은 version 6 저장 문서에 기록한다. 보스 브랜치의 이전 저장은 최고 단계에서 연구 해금 단계(최대 5)를 복원하고, master의 기존 연구 해금 단계와 유물 소유도 유지한다. 이전 저장에 없는 최고 단계·정산 RunId는 0/빈 목록으로 시작한다. 저장 거절 시 메모리 지급을 되돌리고 결과 행동에서 같은 정산을 재시도한다. 연구 초기화는 보스 기록을 지우지 않는다.

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
| monster.02 | 여왕벌 | 시각·피격 영역 0.62배 |
| monster.03 | 독버섯 | 이동 속도 1.8배 |
| monster.04 | 가면유령 | 4초마다 1.2초간 사라져 피격 회피 |
| monster.05 | 갑옷병사 | 4.5초마다 1.7초간 피해 65% 감소 |
| monster.06 | 눈요정 | 3.5초마다 이동 영역 안의 무작위 위치로 순간이동 |
| monster.07 | 벌집 | 체력 2.5배 |
| monster.08 | 골렘 | 시각 크기 2배, 피격 영역 1.8배 |
| monster.09 | 봉오리 | 3.5초마다 반경 2.75 내 아군 체력을 최대 체력의 20% 회복 |
| monster.10 | 판도라 상자 | 4초마다 반경 3.5 내 쓰러진 아군을 최대 체력의 30%로 부활 |

시체는 부활 가능 시간을 위해 6초 동안 유지한다. 부활한 개체는 처치 보상을 한 번만
정산하므로 반복 부활로 재화를 복제하지 않는다. 능력 시계는 런 진행 시간을 사용해
일시정지 중에는 멈춘다. 종별 외형은 `MonsterDefinition.VisualPrefab`으로 유지한다.
기존 `behaviorType` JSON 필드는 계속 0만 허용한다.

## 전투에서 값을 읽는 실제 API

아래 controller는 현재 CombatRunController, snapshot은 App이 캡처한 ProgressionCombatSnapshot이다.
전역 변수나 새로 추가한 전체 데이터 getter를 뜻하지 않는다.
특성 연동이 켜진 Main에서 HuntManager.BeginPrototypeRun은 전체 정보와 최종 CombatSnapshot을 만들고,
RunCoordinator.Start에 CombatSnapshot/스킬 목록/유물 목록/SpawnPlan을 전달한다.
CombatRunController는 PrepareRun 이후 CommitStart 이전에 특성 스냅샷의 스킬 ID와 Main의
컨트롤러 ID를 정확히 대조한다. 해금 상태는 특성 스냅샷만 사용하고, 기본 수치와 존재하는
업그레이드를 합쳐 이번 런의 RuntimeStat으로 고정한다. 스냅샷·컨트롤러가 빠지거나 해금된
스킬 준비에 실패하면 Main 런을 거부한다.
매직볼은 단일 오브젝트가 런 종료까지 유지되어 쿨타임 증가량을 받지 않는다.
기존 저장/테스트 입력과 호환되도록 `moveSpeedWorldUnitsPerSecond`, `sizeMultiplier`, `damageMultiplier`, `cooldownSeconds`, 기타 기존 전투 튜닝 ID를 계속 허용한다. 신규 성장 노드는 스킬별 `damage`, `radius`, 펫의 `simultaneousHits`를 사용한다. 이 스킬 피해값은 플레이어 공격력과 독립적인 고정 피해이며, 보스 피해 배율·치명타·유물의 자동 스킬/펫 피해 효과는 전투의 공통 피해 처리 단계에서 별도로 적용된다. 스킬 범위는 각 스킬 RuntimeStat의 반경/크기에 적용하며 플레이어 커서 반경은 사용하지 않는다. `simultaneousHits`와 `count`는 정수 증가량이어야 한다.

| 정보 | 실제 프로퍼티 |
|---|---|
| 공격력 | controller.CombatSnapshot.AttackPower |
| 반경 배율 / 월드 반경 | controller.CombatSnapshot.RangeMultiplier / GameInformation.BaseAttackRadiusWorldUnits * RangeMultiplier |
| 기본 공격 쿨타임 | controller.CombatSnapshot.AttackCooldownSeconds |
| 치명타 확률 / 피해 배율 | controller.CombatSnapshot.CriticalChancePercent / CriticalDamageMultiplier |
| 보스 피해 배율 | controller.CombatSnapshot.BossDamageMultiplier |
| 자동 공격 여부 / 주기 | controller.CombatSnapshot.AutoAttackEnabled / AutoAttackIntervalSeconds |
| 스킬 정보 | SkillCombatSnapshot의 SkillId, Unlocked, Upgrades와 각 스킬 프리팹/Main override의 BaseStat을 합친 RuntimeStat. Main 일반 런은 특성 해금 상태를 그대로 사용하며, Editor Play Mode 강제 해금 토글은 이 경로에서 무시 |
| 몬스터 생성 | SpawnPlan.Entries[i].Snapshot의 MonsterId, MaxHealth, PackSize, SpawnIntervalSeconds, AliveLimit |
| 몬스터 특성 | SpawnPlan.Entries[i].Snapshot의 MovementMode, MoveSpeed, VisualScale, SpecialAbility 및 능력 수치 |
| 가넷 / 추가 젬 보상 | SpawnSnapshot.GarnetReward / BonusDropCurrencyId, BonusDropAmount, BonusDropChancePercent |
| 유물 정의·획득 상태 | `ProgressionCombatSnapshot.Relics[i]` → `CombatRunController.RelicSnapshots[i]`: RelicId, Owned, SourceMonsterId, DropChancePercent, EffectId, EffectValue, EffectValue2 |
| 유물 드롭 정책 | `ProgressionCombatSnapshot.RelicDropRules` → `CombatRunController.RelicDropRules`: MaxPerKill, CandidatePolicy, Resolution, PreserveBaseChanceAfterAcquisition |
| 개체 현재 체력 | MonsterCombatTarget.CurrentHealth (MaxHealth는 시작/최대 체력) |
| 런 진행 | controller.ElapsedSeconds, RemainingSeconds, DefeatedCount, GarnetEarned, EffectiveDamage |
| 이번 런의 종류별 젬 획득량 | controller.GetResourceEarned(currencyId), 미획득 종류는 0. 영구 잔액과 독립 |
| 처치 보상 표시 사건 | controller.RewardResolved(CombatRewardResolved): 원자적 기록 완료 후 RunId·런 내 순번·사망 전 몸 위치·Garnet·BonusCurrencyId/BonusAmount 전달. 같은 가넷 ID 보너스는 Garnet에 정규화. UI 완료가 실제 지급 조건이 아님 |
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

- SourceJson의 젬 amount는 시작 시 잔액. 진행 중 수익은 GetResourceEarned(currencyId)/GarnetEarned, 종료 수익은 RunResult.Rewards다.
- 전체 정보 팝업/JSON 복사는 활성 런의 SourceJson을 표시하고, 런 밖에서는 현재 프로필을 합산한다. 결과 화면에서는 마지막 런 정보가 유지될 수 있다.
- 매 타격/매 프레임 JSON 파싱이나 스냅샷 재생성을 하지 않는다.
- 배열은 런 시작 시 복사되어 읽기 전용으로 노출된다. 후속 UI 구매는 다음 런부터 반영된다.
- GameInformation DTO 자체는 생성/역직렬화용 가변 객체다. Combat에는 복사된 계약 값을 전달한다.
- Combat이 지갑을 수정하지 않는다. 보상은 RunResult.Rewards → App 정산 → Progression 저장 경로를 유지한다.

## 유물과 저장 호환

20개 유물의 효과·출처·개별 확률은 GameInformation v4에 있으며, `GameInformationBuilder`는 도감/테스트 도구의 획득 ID를 `owned` 값에 반영한다. `ToCombatSnapshot`은 정의·보유 상태를 불변 `RelicCombatSnapshot` 목록과 드롭 규칙으로 복사한다. 런 시작 시 `RelicCombatModifiers`가 소유 효과를 집계한다. 피해/반경/쿨타임/펫/필드 시간/생성 간격/처치 보상 효과는 전투 런에서 적용된다. 일반 몬스터 처치 시 미보유 유물의 고정 개별 확률을 합산해 드롭 여부를 굴린 뒤, 성공하면 해당 확률 비율로 최대 하나를 고른다. 새 유물은 `RunResult.RelicAwards`로 나와 App 정산에서 통화와 함께 한 번 저장되며 다음 런 시작부터 효과를 적용한다. 전체 드롭 알고리즘은 [유물 드롭 설계](../relic-drop-design.md)를 따른다.
저장 version은 6이며 첫 v6 저장 직전에 이전 원문을 cursor_hunter.progression.v1.before-v6에 한 번 백업한다. v1~v5의 화폐·구매·단계 상태는 보존하고 유물 소유 ID는 없는 것으로 시작한다.
옛 유물 구매/조각은 복원·지급·환불하지 않고 백업에만 남긴다. 일반 강화·젬은 유지한다.
유물 보정이 사라져 기존 최종 공격력·보스 배율·연동 스킬 피해가 낮아질 수 있다.

## 검증·아직 없는 기능

전투 정의는 7종이다. 성장 트리에서는 베이비 드래곤 펫을 첫 스킬로 배우고, 보스 단계 1~5에서 토네이도·벼락·화염 배기·마력 구체·레이저를 차례로 하나씩 해금한다. 헤드샷은 전투 정의만 있고 성장 해금 경로는 없다. 활성 스킬과 펫의 피해·공격 범위 연구, 펫 동시 타격 연구는 성장 스냅샷에 별도 수치로 전달된다. Main 씬의 Editor 테스트 토글은 실제 전투 이펙트 확인용이며 성장 해금 조건을 변경하지 않는다. 실제 보스 런 흐름은 연결되어 있으며, 장기 성장 밸런스와 스킬별 강화의 단계/화면 배치는 추가 검수 대상이다.
스킬 고유 투사체/빙결/틱/브레스, 보스 패턴/패링, 전체 Localization, 영속 RunId journal은 미완료다.
프로세스 내 중복 정산 방지와 PlayerPrefs 저장의 기존 한계는 남아 있다.
메인 스레드 흐름을 유지하며 읽기 전용 배열만으로 전체 시스템의 멀티스레드 안전성을 주장하지 않는다.
