# Combat/Runtime
담당: 친구.
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.

`MonsterCombatTarget`은 `MonsterHealthBarView`를 통해 일반 몬스터의 월드 HP 바를
생성·갱신한다. HP·가넷·보너스 젬스톤·전리품 조각 확률은 `SpawnSnapshot`에서만
주입하고, 체력바 렌더러는 콜라이더 bounds 계산에서 제외한다. `CombatRunController`는
`CombatSnapshot.CriticalChancePercent`를 타격마다 시드 기반으로 판정하고, 치명타에는
2배 피해를 적용한 뒤 `CriticalHit` 이벤트를 발행한다. 보스 모드에서는
`BossDamageMultiplier`를 피해에 곱한다. Field HUD의 `전체 정보`는 App이 Progression
전역 오버레이를 호출하는 방식으로 연결하며 Combat은 Progression 구현을 직접 참조하지
않는다. `RunResult.Rewards`는 가넷·젬스톤·전리품 조각을 settlement에 전달하는 계약이다.

`PlayerCombatStatsDefaults`는 모든 새 런의 공통 커서 기본값을 Inspector에서 소유한다.
런 시작마다 `PlayerCombatStatsRuntime`은 이 값으로 초기화한 뒤 App이 전달한
`CursorCombatStatBonusesSnapshot`을 더한다. 특성 구매 상태나 성장 설정은 Combat에서 직접 읽지 않는다.

신규 런타임 프리팹은 `MonsterRoot.prefab`을 공통 루트로 사용한다. 종별 외형은
`SpawnPlanEntry.VisualPrefab`으로 전달해 `VisualRoot` 아래에 붙인다. `MonsterCombatTarget`
은 시각 계층을 조립한 뒤 Animator와 SpriteRenderer bounds를 확인한다.
`WalkerStumpTarget`은 기존 테스트/씬 코드와의 호환 alias다.

`MonsterBehaviorController`는 `SpawnSnapshot`에 이미 합성된 이동 모드·이동 속도·크기·
명중 영역 값을 적용한다. 일반 이동은 시드로 정한 방향으로 이동하고 화면 안쪽 경계에서
반사하며, 원형 이동은 스폰 지점을 중심으로 돈다. 이동 시간은 `CombatRunController`의 런
시계를 사용해 일시정지·런 종료·오래된 RunId 콜백에 따라 움직임을 멈춘다.
