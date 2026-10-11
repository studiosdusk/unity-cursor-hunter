# Combat/Art
담당: 친구.
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.

커서 히트는 사용자가 지정한 `MouseAttack_Prefab/MouseAttack_0~3.prefab`을 사용한다.
`Prefabs/Vfx/CursorHitEffects.prefab`이 네 원본과 `MouseAttack_Materials`의 URP Unlit
머티리얼을 참조한다. 원본 머티리얼/프리팹의 자동 반복 설정은 보존하고, 런타임 복제본의
자동 방출을 끈 뒤 종류별 공유 ParticleSystem에서 수동 방출한다. 각 타격은 설정된 종류 중
동일 확률로 하나를 재생하며 연속 중복도 허용한다. 이전 문서의 다른 히트 에셋 후보보다
2026-10-10 사용자가 지정한 이 네 에셋을 우선한다.

`CombatManager/CursorHitEffects`의 `Effect Variants` 배열 하나에서 추가·삭제·순서를
관리한다. 각 항목은 `Prefab`과 선택 사항인 `Material Override`를 함께 저장한다.
Override가 비어 있으면 프리팹 renderer의 sharedMaterial을 사용한다. 이 프로젝트에서는
URP 호환 재질이 필요하며, 기존 MouseAttack 원본에는 연결된 URP Override를 사용한다.
자식이 없는 단일 ParticleSystem 프리팹을 지원한다. 비어 있거나 재생할 수 없는 항목은
제외하고 유효한 항목 중 하나를 고른다. 항목 개수에 4개 제한은 없으며 전체 삭제 시 표시만 끈다.

Play 중 Inspector 설정을 바꾸면 다음 Update에서 현재 파티클을 정리하고 시스템을
다시 준비한다. 항목을 다시 추가하면 자동 재개하며, 정지 상태는 유지한다. 재구성 비용은
설정 변경 때만 발생하고 타격 경로에서는 생성·할당하지 않는다. 기존 두 배열을 사용하는
열린 씬은 역직렬화 때 한 번 이전한다. Main의 1·2·3번 선택과 크기 설정은 유지했다.
