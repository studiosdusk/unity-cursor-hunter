# Combat/Runtime
담당: 친구.
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.

`MonsterCombatTarget`의 일반 몬스터 월드 HP 바는 기본적으로 숨긴다. 프리팹의
`showHealthBar`를 켠 경우에만 `MonsterHealthBarView`를 생성·갱신한다.
HP·가넷·보너스 젬스톤·전리품 조각 확률은 `SpawnSnapshot`에서만
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
명중 영역 값을 적용한다. 몬스터마다 생성 시와 이후 2초마다 정지·이동 중 하나를 같은
확률로 고른다. 이동을 고르면 시드 난수로 새 방향을 정하고, 일반 이동은 화면 안쪽
경계에서 반사하며 원형 이동은 스폰 지점을 중심으로 돈다. `Stationary` 모드는 계속
정지한다. 행동 타이머는 `CombatRunController`의 런 시계를 사용해 일시정지 중에는
진행하지 않고, 런 종료·오래된 RunId 콜백에서는 움직임을 멈춘다.
실제 위치가 변한 프레임에는 Animator에 `isMoving` Bool이 있으면 켜고, 정지·일시정지·런 종료 시
끈다. Idle/Walk 스테이트가 있으면 정지 행동에서 Walk 또는 Walk 진입 전이를 Idle로
바꾸고, Hit/Dead 재생 중에는 해당 스테이트를 덮어쓰지 않는다.
피격 시에는 즉시 이동 표시를 끄고 Hit 클립 길이 동안 위치 이동을 멈춘다. 연속 피격은
정지 시간을 연장하며, 2초 행동 선택 시계는 계속 진행된다.
사망 판정 뒤에는 Animator가 다음 프레임에 아직 Walk 상태를 보고하더라도 Idle을
강제 재생하지 않는다. 이 상태 전환이 예약된 Dead 재생을 덮지 않도록 한다.
17종 컨트롤러의 Hit→Idle 전이는 Exit Time 1에서 실행해 Hit 클립을 끝까지 재생한다.
사망 판정과 보상 집계는 체력이 0이 된 순간 처리한다. `MonsterRoot`는 Dead 상태의 실제
재생 진행률이 `deathDespawnNormalizedTime`에 도달하면 시각 오브젝트를 제거한다.
현재 17종 Dead 클립은 1.5초 중 1.167초(정규화 시간 0.7777778)에 모든 사망 파츠가
꺼지므로, 공용 프리팹의 제거 시점을 여기에 맞췄다. Animator 상태가 사라진 경우에만
클립 길이를 기준으로 정리한다.
실제 수평 이동 방향에 따라 `VisualRoot`의 X 배율 부호만 바꾼다. Walker 비주얼은
기본 X 배율에서 왼쪽을 바라보므로 오른쪽 이동에 음의 X 배율을 적용한다. 수평 이동이
없으면 마지막 방향을 유지한다. `HitArea`의 콜라이더 크기·위치는 뒤집지 않는다.

`EnemyMonster 2/Animations`의 17종 컨트롤러는 `hit` Trigger, `dead`·`isMoving`
Bool과 Idle↔Walk, Any State→Hit/Dead, Hit→Idle 전이를 공유한다. 각 종의
Idle/Walk/Hit/Dead/Attack 클립과 재생 속도는 유지하며, 50개 시각 프리팹은 각 종의
컨트롤러를 계속 참조한다.

커서 기본 공격의 주기는 `PlayerCombatStatsRuntime.AutoAttackIntervalSeconds`에서
공통으로 읽지만, 마지막 성공 타격 시각은 `CombatRunController`가 몬스터 인스턴스별로
저장한다. 빈 공간은 쿨타임을 소비하지 않는다. 원형 커서의 이전 위치와 현재 위치
사이를 CircleCast하고 현재 위치를 OverlapCircle로 검사해, 한 프레임의 빠른 이동에서
지나간 몬스터를 모두 후보로 모은다. 중복 콜라이더는 몬스터당 한 번만 처리하고,
사망·런 종료 때 해당 타격 기록을 정리한다. App의 `CursorAttackController`는
몬스터 이동 후 LateUpdate에서 커서를 샘플링하며, UI 위·화면 밖·포커스 상실·일시정지·
자동 공격 해제 시 이동 경로를 끊는다. 테스트 버튼의 공격은 현재 위치만 판정한다.
스킬은 기존의 독립 쿨타임과 현재 위치 판정을 사용한다.
