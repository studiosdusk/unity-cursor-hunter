# 디렉토리와 소유권
기존 Assets/DownLoadAssets, Scenes/SampleScene, Scripts/Test.cs, Settings는 이동하지 않는다. 신규 게임 구현은 Assets/CursorHunter 아래 둔다.

| 경로 (Assets/CursorHunter 기준) | 담당 | 내용 |
|---|---|---|
| Contracts/Runtime | 공동 검토, 사용자 반영 | ID, 값 객체, 런 입출력 계약. UnityEngine 비의존 |
| Data/Runtime | 사용자 | 정적 데이터 정의·로드·검증 |
| Data/Definitions | 사용자 | Monsters/Bosses/Skills/Traits/Economy별 한 항목 한 파일 |
| Progression/Runtime | 사용자 | 특성, 프로필, 구매, 해금, 저장/복구 |
| Progression/Prefabs, Art, Scenes | 사용자 | 특성 화면, 재화 표시, 아이콘, 개인 테스트 씬 |
| Combat/Runtime | 친구 | 일반/보스/입력/스킬/타이머/판정/풀 |
| Combat/Prefabs, Art, Scenes | 친구 | 적·보스·필드·HUD·VFX·개인 테스트 씬 |
| App/Runtime, Prefabs, Scenes | 사용자 | 진입점, 연결, Main·정산·설정·엔딩 |
| 각 모듈/Tests | 해당 담당 | 자신의 테스트 코드 (테스트 asmdef는 첫 테스트 작성 시 생성) |

## 참조 방향
```text
Contracts ← Data
Contracts ← Progression
Data      ← Progression
Contracts ← Combat
Contracts + Data + Progression + Combat ← App
```
화살표는 오른쪽 모듈이 왼쪽을 참조한다. Combat은 Progression/Data 구현에 직접 접근하지 않는다.
App이 Data/Progression에서 런 입력을 만들어 Combat에 전달한다. Progression도 Combat 구현을 참조하지 않는다.
테스트 더블은 각자 Tests 아래에 둔다. 친구는 사용자 저장 구현이 끝날 때까지 기다리지 않고 Contracts 형식의 고정 스냅샷으로 개발한다.
공급사 스크립트는 기존 기본 어셈블리에 남긴다. 신규 asmdef에서 Assembly-CSharp 타입을 직접 참조하지 않는다. 외형 자식 오브젝트를 감싸는 게임 전용 프리팹을 사용한다.

## 씬·프리팹 경계
- 런타임 씬 전략은 **One Scene + 개발용 Sandbox 씬**으로 고정한다. 실제 런타임·빌드 진입점은 `App/Scenes/Bootstrap.unity` 단일 씬이며, Main·전투·정산·특성·설정 화면은 이 씬 안에서 App가 화면 수명주기와 활성 상태를 관리한다.
- `Combat/Scenes/CombatSandbox.unity`와 `Progression/Scenes/ProgressionSandbox.unity`는 각 모듈의 단독 개발·검증용 씬이다. 런타임에서 `LoadSceneMode.Additive`로 함께 로드하지 않으며, 통합 씬의 빌드 목록에도 넣지 않는다.
- 사용자: Progression/Scenes/ProgressionSandbox.unity를 Unity에서 생성.
- 친구: Combat/Scenes/CombatSandbox.unity를 Unity에서 생성.
- 통합 담당: App/Scenes/Bootstrap.unity를 Unity에서 생성하고 빌드 목록 등록.
- 전투 루트와 특성 루트는 각 담당의 프리팹. 통합 씬에는 프리팹 참조만 배치하고 내부 override를 누적하지 않는다.
- 런타임 통합에서는 App만 입력 라우팅, EventSystem, 화면 전환 수명주기를 소유한다. Sandbox는 독립 실행용 카메라/EventSystem을 자체 보유하되 실제 통합에는 가져오지 않는다.
- VFX 원본을 수정하지 않고 Combat/Prefabs/Vfx에 Variant 또는 외형 래퍼를 둔다. UI도 같은 방식으로 소유 영역에 둔다.
- 적 프리팹에 HP/보상을 복제하지 않는다. monster ID와 시각 참조만 두고 런 스냅샷 수치를 사용한다.
- 데이터는 하나의 거대한 GameData.asset에 몰지 않는다. ID는 파일명/표시명/enum 순서와 독립적으로 고정한다.
- Addressables, DI 프레임워크, 공용 전역 이벤트 버스는 현 단계에 추가하지 않는다.

이 구조는 텍스트 코드 충돌과 씬 동시 편집을 줄인다. 계약/통합 작업이 사용자에게 몰리는 비용이 있으며, 공통 파일 충돌을 완전히 없애지는 못한다.

## 현재 통합 경로 추가 규칙

- `Progression/TraitScreenController`가 `CreateCombatSnapshot()`으로 런 시작 시 불변
  전투 값을 만든다. App은 이 스냅샷을 `HuntManager`를 통해 Combat에 넘기며, 전투 중
  특성 UI·PlayerPrefs를 다시 읽지 않는다.
- `TraitScreenController.ShowSummaryPopup()`은 활성 Canvas 하위 전역 오버레이를 사용한다.
  `App/PrototypeRunHud`의 일반·보스 Field `전체 정보` 버튼은 이 공개 메서드만 호출하고,
  Progression의 내부 구매 딕셔너리에 접근하지 않는다. 런 중에는 App이 시작 시 복사한
  `ProgressionCombatSnapshot`을 `SetActiveRunSnapshot`으로 고정하고, 결과 화면까지 같은
  값을 보여준다.
- 일반 몬스터 체력바는 `Combat/Runtime/MonsterHealthBarView`가 소유한다. `WalkerStumpTarget`
  이 체력바를 생성·갱신하지만 체력바 SpriteRenderer는 콜라이더 bounds 계산에서 제외한다.
  친구가 몬스터 프리팹을 교체해도 HP/보상은 `SpawnSnapshot`에서 주입한다.
- UI 액션 이름은 `강화/해금`, `노드 초기화`, `전체 초기화`를 사용한다. `CancelSelected` 같은
  취소 명칭을 새 코드에 추가하지 않는다. 전체 초기화는 Progression 소유이고, Field HUD에는
  초기화 버튼을 두지 않는다.
- 보스 HP 초안은 기획서 B04의 `1,300 / 12,000 / 90,000 / 240,000 / 1,800,000`을
  기준으로 한다. 승기는 Combat 밸런스 구현 시 이 값을 독립된 보스 스냅샷으로 소비하고,
  TraitCatalog의 몬스터 HP와 섞지 않는다.

## 일반·보스 필드의 진행 데이터 읽기 계약

런 시작 시점의 값만 읽는다. 전투 중 특성 화면이나 PlayerPrefs를 다시 읽으면 사용자가
전투 중 구매한 값이 현재 런에 섞이므로 금지한다.

```csharp
TraitScreenController progression =
    FindFirstObjectByType<TraitScreenController>(FindObjectsInactive.Include);
ProgressionCombatSnapshot snapshot =
    progression == null ? null : progression.CreateCombatSnapshot();

float normalSeconds = HuntManager.GetRunDuration(
    RunMode.NormalField, snapshot, 15f);
RunRequest normal = new RunRequest(
    RunId.Create(), schemaVersion, balanceVersion,
    RunMode.NormalField, string.Empty, seed, normalSeconds);

// 보스는 저장된 일반 필드 시간과 관계없이 항상 60초다.
float bossSeconds = HuntManager.GetRunDuration(
    RunMode.Boss, snapshot, 15f); // 60f
RunRequest boss = new RunRequest(
    RunId.Create(), schemaVersion, balanceVersion,
    RunMode.Boss, bossId, seed, bossSeconds);
```

`snapshot.Combat`에는 공격력·기본 반경 배율·다중 클릭·치명타 확률·보스 피해 배율·
자동 공격 플래그가, `snapshot.NormalFieldDurationSeconds`에는 15~60초의 일반 필드 시간이
들어 있다. `snapshot.Skills`는 스킬별 해금·피해·범위·쿨타임 배율, `snapshot.Monsters`는
해금·HP·젠 주기·생산량·확정 가넷·보너스 젬스톤 드롭·전리품 조각 확률을 가진다.
치명타는 Combat이 2배 피해를 계산하고 `CombatRunController.CriticalHit` 이벤트를 한 번
발행한다. HUD는 이 이벤트를 `치명타! ×2` 피드백으로 표현한다.

필드의 `전체 정보` 버튼은 `PrototypeRunHud.ShowProgressionInfo()`만 호출한다. 이 메서드는
동일한 Progression 인스턴스의 `ShowSummaryPopup()`을 열며, `HuntManager`가 런 시작 직후
`SetActiveRunSnapshot(snapshot)`을 호출했기 때문에 일반·보스 필드와 결과 화면에서 같은
Key | Value 스냅샷을 볼 수 있다. Combat이 전체 표를 만들거나 UI 노드에 접근하지 않는다.

몬스터 처치 결과는 `RunResult.Rewards`의 `ResourceRewardSnapshot` 목록으로 전달한다.
가넷은 기존 `GarnetEarned`와 목록의 `gem.garnet`에 함께 기록하고, 보너스 젬스톤과
`fragment.loot.monster.XX` 조각은 확률이 성공한 경우에만 추가한다. 현재 App 프로토타입은
Eligible 결과에서 아래 한 경로로 정산한다.

```csharp
if (result.SettlementPolicy == RunSettlementPolicy.Eligible)
{
    progression.GrantResourceRewards(result.Rewards);
}
```

`HuntManager`가 `RunId` 집합으로 프로세스 내 중복을 막고, 출시 전에는 이 검사를
저장 가능한 `RunId + settlementEventId` 트랜잭션으로 교체한다. `GrantResourceRewards`는
가넷·해금된 젬스톤·전리품 조각만 프로필에 반영하며, 잠긴 젬스톤은 무시한다. Combat은
이 메서드를 호출하거나 Progression 구현을 참조하지 않는다.

일반 필드의 기본 반경은 `Main.unity`의 `cursor_image` 스케일 `(2,2,1)`, 원형 콜라이더
반경 `0.86269045`, `rangeMultiplier=1`이다. 승기는 이 값을 Combat에서 다시 계산하지
않고 App이 전달한 `CombatSnapshot.RangeMultiplier`만 곱한다. 보스 시작 시에는 같은
스냅샷을 쓰되 `RunRequest.Mode == Boss`일 때 `BossDamageMultiplier`를 피해 계산에 적용한다.
