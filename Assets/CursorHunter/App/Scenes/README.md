# App/Scenes
담당: 사용자 (Contracts는 공동 검토).
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
런타임 정책: **One Scene + 개발용 Sandbox 씬**. `Bootstrap.unity`가 실제 런타임·빌드 진입점이며, Main·전투·정산·특성·설정 화면은 이 씬 안에서 전환한다. 현재 `Bootstrap.unity`는 아직 생성되지 않았고 빌드 목록에는 기존 `Assets/Scenes/SampleScene.unity`가 남아 있다.
