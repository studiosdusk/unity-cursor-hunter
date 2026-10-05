# Data/Runtime
담당: 사용자 (Contracts는 공동 검토).
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.

`MonsterDefinition`은 종별 `MonsterBaseStats`와 `MonsterBehaviorProfile` 목록을 보유한다.
각 프로필의 체력·이동속도·비주얼 크기·명중 영역 배율은 곱해지고, 이동 전략은 프로필
목록에서 마지막으로 지정한 항목이 선택된다. 런 시작 시 `CreateSnapshot`이 이를 최종
`SpawnSnapshot`으로 복사한다. Progression의 소환 수는 현재 기본값 1마리를 기준으로 한
증가분으로 해석하며, App이 이를 종별 `BaseStats.PackSize`에 더한다.
10종 Definition의 `visualPrefab`은 각자의 `prefabKey`에 해당하는 서로 다른 몬스터
프리팹을 참조한다.
