# Combat/Scenes
담당: 친구.
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
역할: `CombatSandbox.unity` 단독 개발·검증 씬. 런타임은 **One Scene + 개발용 Sandbox 씬** 정책을 따르므로 이 씬을 `Main.unity`와 함께 Additive 로드하거나 통합 빌드 씬으로 등록하지 않는다.
Sandbox는 독립 실행을 위해 자체 카메라와 EventSystem을 가질 수 있다. 실제 런타임 전투는 Combat 루트 프리팹으로 만들어 `App/Scenes/Main.unity`에 참조한다.

`CombatSandbox.unity`를 열고 Play하면 고정 스냅샷의 몬스터 몸체 8개에 히트 이펙트를 재생한다.
상단 패널에서 `80 targets / 0.05s`로 부하를 높이고, Auto hit/Hit once/Pause/Resume/Restart로
표시 상한과 수명주기를 확인한다. 30초가 지나면 Restart한다. 스폰·피해는 Combat 코드를
사용하며 Data/Progression/App이나 저장에 접근하지 않는다. 패널의 IMGUI 문자열 할당은
개발용 표시 비용이므로 Profiler의 타격 할당은 `CursorHitEffectManager` 경로로 확인한다.

Main의 `CombatManager/CursorHitEffects` 프리팹에서 수명(0.5초), 크기 배율(1.2),
크기 범위(0.5~2.5), 표시 순서(200), 전체 표시 상한(256)을 조정한다.
상한은 초기화 시 고정된다. 인스펙터 하단에는 활성·최고 활성·방출·생략 수를 표시한다.
