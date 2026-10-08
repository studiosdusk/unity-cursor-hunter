# App/Runtime
담당: 사용자 (Contracts는 공동 검토).
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.

## 전투 스테이지 설정

`App/Stages/Stage01.asset`이 Main 씬 `HuntManager`의 `Default Stage`에 연결돼 있다.
기존 전투 진입 버튼은 `BeginPrototypeRun()`을 호출하며 이 에셋의 설정으로 진입한다.
스테이지를 추가하려면 `Cursor Hunter/Stages/Combat Stage` 에셋을 만들고
`HuntManager.BeginStageRun(CombatStageDefinition)`에 해당 에셋을 전달한다.

`Monsters`의 각 행에서 `Monster` Definition, `Allow Spawn`, `Production`을 지정한다.
허용된 행만 이번 런에 출현한다. `Allow Spawn`은 플레이어의 해금 여부와 관계없이
이번 스테이지에 해당 종을 허용하며, 구매/저장 상태는 변경하지 않는다.
`Production`의 `Base`, `Production2`, `Production3`, `Production4`는 몬스터 특성의
생산 노드와 같은 1·2·3·4마리 단계다. `UsePlayerUpgrade`는 구매한 생산 단계를
사용한다. 현재 모든 MonsterDefinition의 기본 PackSize는 1이다.
스테이지 설정은 런 시작 때 복사돼서 진행 중 인스펙터 변경에는 영향받지 않는다.
