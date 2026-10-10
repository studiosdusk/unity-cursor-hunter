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

## 커서 스킨

Main 씬의 `Managers > CursorManager`에서 `Cursor Skin Controller > Selected Skin`을
선택한다. 현재 `cursor_image`(기존 마법진)와 `Joystick_Skill01_Fill_White`가 등록되어
있으며 기본값은 `Joystick_Skill01_Fill_White`다. 편집 모드와 Play 모드 모두 반영된다.
커서 오브젝트가 숨겨져 있으면 선택은 유지되며 커서를 켤 때 표시된다.

스킨 정의는 `App/Art/CursorSkins`의 `CursorSkinDefinition` 에셋이다.
`Create > Cursor Hunter > Cursor Skin`으로 새 에셋을 만든 뒤 고유 `Skin Id`, `Sprite`,
`Range Diameter Ratio`를 지정하고 컨트롤러의 `Available Skins`에 추가한다.
Ratio는 이미지 전체 긴 변에 대한 범위 원의 지름 비율이며, 투명 여백·원 밖의 장식은
제외한다. 기존 마법진은 원 밖의 장식을 보존하도록 약 0.674, 새 원은 1을 사용한다.

인게임 선택 화면은 `AvailableSkins`를 읽고 `TrySelectSkin(skinId)`를 호출하면 된다.
버튼 OnClick에는 문자열 인자를 받는 `SelectSkin(string)`을 연결할 수 있다.
ID는 `cursor.default`, `cursor.joystick-skill01-white`이며 목록 순서와 무관하다.
없는 ID, 중복 ID, 이미지가 없는 스킨을 요청하면 현재 스킨을 유지하고 false를 반환한다.
선택 UI·해금·영구 저장은 아직 구현하지 않았으며, 이후 선택한 ID를 저장/복원하면 된다.

`cursor_image`에는 기존 CircleCollider2D를 유지하고 SpriteRenderer는 그 자식
`SkinVisual`에 둔다. 스킨 컨트롤러는 자식 이미지만 범위 원에 맞추며 Collider의 반경,
오프셋, 루트 크기를 수정하지 않는다. 커서 이동·표시 여부·강화 배율은 기존
MainCursorController가 관리한다. 원본 공급사 이미지와 머티리얼은 수정하지 않는다.

`Breathing`은 Play 중 이미지 크기를 기본 0.95~1.05배로 부드럽게 왕복한다.
`Breathing Period Seconds`는 한 왕복에 걸리는 시간(기본 2초)이다.
`Breathing Enabled`로 끄거나 최소/최대 배율을 조절할 수 있다.
스킨 교체와 범위 강화에도 같은 연출을 적용하며, 공격 판정과 이미지 중심은 유지한다.
시간 배율과 무관하게 재생하고, 편집 모드 또는 컴포넌트 비활성화 시 기본 크기로 돌아온다.
