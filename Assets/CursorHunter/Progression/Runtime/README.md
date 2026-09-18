# Progression/Runtime
담당: 준영 (Contracts는 공동 검토).
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.

## Trait screen prototype

`TraitScreenController`는 특성 화면의 표시와 입력을 담당한다. `Main.unity`의
`TraitPanel_Root`에 연결되어 있으며, Stat·Skill·Monster·Loot·Pet 탭의 모든 노드를
`TraitCatalog` 데이터에서 런타임 체인 행으로 생성한다. 스탯은 카테고리마다 한 행,
스킬은 스킬마다 한 행, 몬스터는 해금 20종과 생산량 3단계 60개를 두 행으로 구성하고 인접 요소를 선으로 잇는다.
카테고리는 데이터 ID와 아이콘 색상을 위해 유지한다.

카테고리 수와 노드 수를 바꿀 때는 코드를 수정하지 않고 `Create > Cursor Hunter >
Progression > Trait Catalog`로 카탈로그 에셋을 만든 뒤 화면 컨트롤러의 `Catalog`에
할당한다. 각 노드는 고유 ID, 표시명, 설명, 값, 비용, 비용 젬스톤 ID, 필요한 보스 티어,
선행 노드, 시작 해금 여부를 가진다. 전리품은 `AcquisitionOnly`로 표시해 전투 중
확률 획득만 허용한다. 카탈로그를 아직 할당하지 않은 경우에만 첫 화면 확인을 위한 런타임
샘플이 자동으로 사용된다.

화면에는 공급 GUI의 패널·탭·리본·노드·아이콘 스프라이트와 몬스터 본체 스프라이트를
참조하고, 진입 페이드와 스케일 애니메이션, 노드 목록 전환 페이드, 버튼 호버·프레스
피드백을 적용했다. 한국어 표시를 위해 `Runtime/Fonts/NanumGothic.ttf`를 사용한다.
스킬·보스·생산량 구매는 노드의 `CostGemstoneId`로 가넷 외 잔액도 안전하게
차감·환불한다. 현재 적용된 노드 수와 전체 에셋 경로는
`docs/collaboration/trait-ui-assets.md`에서 관리한다. 구매 상태와 젬 잔액은 현재
프로토타입 로컬 상태이며, 실제 저장/전투 수치는 Contracts와 Progression 서비스가
정해진 뒤 연결한다.


## v0.0.5 런타임 데이터 기준

`TraitCatalog.CreateRuntimeDemo()`는 화면 검증용이지만 최종 ID 계약을 포함한다.
Stat은 60개, Skill은 24개, Monster는 80개, Loot는 25개, Pet은 5개다. Monster의
초기 HP·젠 주기·기본 묶음은 `TraitMonsterBalanceDefinition` 20개로 별도 보관한다. 실제 저장
서비스가 연결되면 이 메서드 대신 ScriptableObject 카탈로그를 주입한다.

- `TraitTab` 값은 `Stat=0`, `Skill=1`, `Monster=2`, `Loot=3`, `Pet=4`다.
- `TraitNodeDefinition.CostGemstoneId`는 `gem.garnet`을 기본값으로 하며, 스킬·보스·
  몬스터 생산량·펫은 단계별 젬스톤 ID를 명시한다. 젬스톤 해금은 직전 단계 재화,
  발견 I·II는 해금된 자기 재화를 사용한다.
- `TraitNodeDefinition.AcquisitionOnly`가 true인 전리품 노드는 UI에서 비용 구매를
  막고, Combat 결과의 `lootId`를 Progression이 소유권으로 확정한다.
- 6번째 재화 `gem.dragon`은 드래곤 브레스(v4)의 전용 재화다. 실제 원석 이름은
  아트 확정 시 ID를 유지한 채 표시명과 스프라이트만 교체한다.
- `Main.unity`의 `gemstoneIcons[5]`, `lootIcon/lootIcons`, `petIcon/petIcons`가
  현재 프로토타입 에셋 참조다. 배열이 짧은 카탈로그는 category icon으로 안전하게
  폴백한다.
