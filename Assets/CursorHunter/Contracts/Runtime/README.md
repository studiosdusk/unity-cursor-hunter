# Contracts/Runtime
담당: 사용자 (Contracts는 공동 검토).
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.

Progression은 `GameInformation`을 검증한 뒤 불변 `ProgressionCombatSnapshot`으로 전달한다. App은 Main 전투에 최종 `CombatSnapshot`, 해금된 스킬, 몬스터 생산 수, 유물 보유 상태를 런 시작 때 복사한다. Combat은 Progression이나 플레이어 저장소를 직접 참조하지 않는다.
