# Combat/Scenes
담당: 친구.
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
역할: `CombatSandbox.unity` 단독 개발·검증 씬. 런타임은 **One Scene + 개발용 Sandbox 씬** 정책을 따르므로 이 씬을 `Bootstrap.unity`와 함께 Additive 로드하거나 통합 빌드 씬으로 등록하지 않는다.
Sandbox는 독립 실행을 위해 자체 카메라와 EventSystem을 가질 수 있다. 실제 런타임 전투는 Combat 루트 프리팹으로 만들어 `App/Scenes/Bootstrap.unity`에 참조한다. 현재 `CombatSandbox.unity`는 아직 생성되지 않았다.
