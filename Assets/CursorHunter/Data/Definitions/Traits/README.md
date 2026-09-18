# Data/Definitions/Traits
담당: 사용자 (Contracts는 공동 검토).
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.

특성 화면 데이터는 `CursorHunter.Progression`의 `TraitCatalog` 에셋으로 관리한다.
이 폴더는 카탈로그 에셋과 향후 밸런스 버전을 보관하는 위치이며, 전투 모듈이 직접
화면 오브젝트를 수정하지 않도록 한다. 카탈로그의 노드 ID는 저장 데이터와 연결될
키이므로 출시 후에는 재사용하거나 임의로 변경하지 않는다.
