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
- 런타임 씬 전략은 **One Scene + 개발용 Sandbox 씬**으로 고정한다. 실제 런타임·빌드 진입점은 `App/Scenes/Main.unity` 단일 씬이며, Main·전투·정산·특성·설정 화면은 이 씬 안에서 App가 화면 수명주기와 활성 상태를 관리한다.
- `Combat/Scenes/CombatSandbox.unity`와 `Progression/Scenes/ProgressionSandbox.unity`는 각 모듈의 단독 개발·검증용 씬이다. 런타임에서 `LoadSceneMode.Additive`로 함께 로드하지 않으며, 통합 씬의 빌드 목록에도 넣지 않는다.
- 사용자: Progression/Scenes/ProgressionSandbox.unity를 Unity에서 생성.
- 친구: Combat/Scenes/CombatSandbox.unity를 Unity에서 생성.
- 통합 담당: App/Scenes/Main.unity를 Unity에서 생성하고 빌드 목록 등록.
- 전투 루트와 특성 루트는 각 담당의 프리팹. 통합 씬에는 프리팹 참조만 배치하고 내부 override를 누적하지 않는다.
- 런타임 통합에서는 App만 입력 라우팅, EventSystem, 화면 전환 수명주기를 소유한다. Sandbox는 독립 실행용 카메라/EventSystem을 자체 보유하되 실제 통합에는 가져오지 않는다.
- VFX 원본을 수정하지 않고 Combat/Prefabs/Vfx에 Variant 또는 외형 래퍼를 둔다. UI도 같은 방식으로 소유 영역에 둔다.
- 일반 몬스터는 공용 `Combat/Prefabs/Monsters/MonsterRoot.prefab`을 사용한다. 종별
  `MonsterDefinition`이 Base 스탯·드랍·외형 참조를 가지고, 재사용 가능한
  `MonsterBehaviorProfile` 목록을 Base 위에 합성해 런 스냅샷으로 복사한다.
- HP/보상은 전투 프리팹에 저장하지 않는다. 전투는 App이 만든 `SpawnSnapshot`만 사용한다.
- 데이터는 하나의 거대한 GameData.asset에 몰지 않는다. ID는 파일명/표시명/enum 순서와 독립적으로 고정한다.
- Addressables, DI 프레임워크, 공용 전역 이벤트 버스는 현 단계에 추가하지 않는다.

이 구조는 텍스트 코드 충돌과 씬 동시 편집을 줄인다. 계약/통합 작업이 사용자에게 몰리는 비용이 있으며, 공통 파일 충돌을 완전히 없애지는 못한다.

## 현재 통합 경로 — 전투 정보 v3 (2026-10-01)

GameDataDocument.Load는 두 영어 리소스를 읽는다. Main 씬의
`PlayerCombatStatsDefaults` 컴포넌트는 커서 핵심 스탯의 공통 기본값을 별도로 소유한다.

- GameData/game-data.en: GameInformation 구조와 기타 초기값; Progression Sandbox/검증용 커서 기본값 포함
- GameData/progression-config.en: 성장 화면 비용/선행 조건/명칭/외형 연결용 설정

App이 기본 스탯 스냅샷을 Progression에 제공한다. GameInformationBuilder가 이 기본값과
구매한 커서 스탯 증가량을 합쳐 전체 정보 JSON을 만든다. Progression은 별도의
`CursorCombatStatBonusesSnapshot`도 App에 반환한다. App은 이를 런 시작 시 Combat에 전달하고,
`PlayerCombatStatsRuntime`은 매 런 기본 컴포넌트에서 초기화한 뒤 증가량을 적용한다.
전체 정보 팝업은 같은 최종 SourceJson을 표시한다. Combat은 UI/PlayerPrefs/성장 설정을 직접 읽지 않는다.
진행도 원본 상태는 아직 TraitScreenController가 소유하며 독립 상태 서비스를 새로 만든 것은 아니다.

게임 정보 파일과 한국어 참고본에는 유물, progression, entities, fieldHelp, implementation이 없다.
성장 설정에는 유물을 제외한 23분류/111노드 및 명칭/외형이 남는다. fieldHelp/implementation은 실행 문서에서 제거했다.
JSON의 `behaviorType`은 이전 호환을 위해 `None=0`으로 보존한다. 신규 종별 특성은
Data의 조합형 프로필로 구성하고, 이동 모드·이동 속도·시각 크기·명중 영역 크기를
`SpawnSnapshot`에 복사해 공용 MonsterRoot가 적용한다. 일반 몬스터 10종, 스킬 7종,
젬 6종, 일반 필드 최대 30초를 유지한다.

유물 제거로 조각 드롭·지갑·구매·능력치 보정이 제거됐다. 진행도 저장 version은 4이며, 최초 v4 저장 전 기존 원문을 별도 PlayerPrefs 키에 백업한다.
일반 강화·젬은 유지한다. Combat은 지급 후보만 반환하며 저장소를 수정하지 않는다.

[최신 인수인계](current-state-handoff-v0.0.4.md)와 [계약·조회 API](contracts.md)를 함께 읽는다.
