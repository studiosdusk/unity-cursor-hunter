# Progression/Scenes
담당: 사용자 (Contracts는 공동 검토).
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
역할: `ProgressionSandbox.unity` 단독 개발·검증 씬. 런타임은 **One Scene + 개발용 Sandbox 씬** 정책을 따르므로 이 씬을 `Main.unity`와 함께 Additive 로드하거나 통합 빌드 씬으로 등록하지 않는다.
Sandbox는 독립 실행을 위해 자체 카메라와 EventSystem을 가질 수 있다. 실제 런타임 특성 화면은 Progression 루트 프리팹으로 만들어 `App/Scenes/Main.unity`에 참조한다. 현재 `ProgressionSandbox.unity`는 아직 생성되지 않았다.

## 초급 연구 테스트 씬

`ResearchPrototype.unity`는 Main과 별개인 실험 씬이다. `ResearchPrototype_Canvas/ResearchBoard/EditableNodes`의 시작 노드 6개와 후행 노드 6개, `EditableLinks`의 연결선은 Hierarchy에서 직접 선택하여 편집한다. 각 후행 노드의 `prerequisites`, `requiredPrerequisites`, `incomingLine`에서 선행과 연결선을 지정한다. 편집 중에는 전체 배치가 보이고 Play에서는 조건 미충족 노드와 연결선이 숨겨진다. 최초에는 중앙의 공격력 노드 하나만 표시되며, 이를 1랭크 구매하면 주변 연구가 해금된다. `Tooltip_Dark_Prefab/Popup`의 제목·수치·재화 표시도 씬 오브젝트다. Dark Tooltip 공급사 프리팹 인스턴스의 씬 오버라이드를 사용한다.

노드별 `ResearchTestNode.definition`이 `Progression/ResearchPrototype`의 개별 연구 SO를 참조한다. SO에서 표시명, 아이콘과 누적 효과, 가넷 비용을 수정한다. 맵 편집기나 본게임 연구 시스템은 아직 연결하지 않았다. Play에서는 노드를 가리키면 우측 비교를 표시하고 좌클릭하면 테스트 가넷으로 다음 랭크를 산다. 미강화는 파랑, 강화 중은 초록, 최대 강화는 노랑이다. 각 노드에서 1랭크를 구매하면 연결된 후행 노드가 해금된다. 툴팁은 노드에 마우스를 올린 동안 표시된다. 구매 노드는 팝 애니메이션, 새 노드는 등장 애니메이션을 재생한다. 효과음은 설정하지 않았다. 테스트용 후행 노드는 같은 SO를 재사용하며 동일 스탯 효과를 합산한다. 재화와 랭크는 임시 메모리 값이며 종료 시 사라진다. Main의 저장·전투에는 영향을 주지 않는다. 기본값은 현재 Main 기준을 테스트 코드에 고정했으므로 Main 값이 변경되면 미리보기와 차이 날 수 있다.

씬과 SO가 이미 있으면 `Tools > Cursor Hunter > Research Prototype > Create Test Scene`은 덮어쓰기를 거부한다. 새 씬을 만들 때만 사용하는 도구다. 화면의 실제 해상도와 한글 렌더링은 Unity Game 뷰에서 확인해야 한다.

최대 랭크 노드는 `MaxRankSparkles` 자식의 UI 스프라이트를 순서대로 깜빡인다. 각 별의 위치·크기·색은 씬에서 조절할 수 있으며, 입력을 가로채지 않는다. 사용한 `Image_Effect_Sparkle.png`는 GUI Pro-SimpleCasual의 원본 스프라이트다.

툴팁의 `EffectDescription`은 연구 SO의 텍스트 키를 이용해 엑셀 원본에서 가져온 Unity Localization 문자열 테이블을 표시한다. 최대 강화 시 비교 화살표·다음 수치·비용 아이콘을 숨기고 최종 수치와 완료 문구를 각각 중앙에 배치한다. 제목·설명 영역과 비용 오브젝트는 씬의 `Tooltip_Dark_Prefab/Popup`에서 조정할 수 있다.

연구 툴팁 수치는 언어에 종속되는 단위 문구를 붙이지 않는다. 배율과 공격 간격은 숫자만, 치명타 확률은 숫자와 `%` 기호로 표시한다. 제목 뒤 프레임은 숨기고 효과 영역은 네 모서리가 둥근 Dark 프레임을 사용한다.

연구 보드는 왼쪽 버튼 드래그로 이동하고 휠로 마우스 위치를 중심으로 확대·축소한다. 드래그는 구매로 처리하지 않으며, 배율과 이동 범위는 Canvas의 `ResearchMapNavigation`에서 조정할 수 있다.

## Main 씬 배치

`App/Scenes/Main.unity`의 `UICanvas/ResearchPrototype_Canvas`에도 같은 오브젝트를 편집 가능한 씬 계층으로 배치했다. 기본 비활성 상태이며 별도 Canvas와 EventSystem 없이 Main의 것을 사용한다. Inspector에서 임시로 활성화해 시험할 수 있다. 프로토타입은 테스트 가넷과 메모리 랭크를 사용하며 실제 연구 화면 전환·저장·전투에 연결되지 않는다.

각 노드의 `NodeBorder`는 GUI Pro-SimpleCasual 프레임 에셋이고, `UpgradeAvailable`은 IconPack의 초록색 + 아이콘이다. 활성 연구가 다음 랭크를 살 수 있는 재화를 보유할 때만 +를 표시하며, 재화 소진·최대 랭크·선행 잠금 상태에서는 숨긴다. 두 이미지는 입력을 가로채지 않는다.

모든 연구 노드는 `Progression/Prefabs/ResearchNode.prefab` 인스턴스다. 공통 외형은 프리팹에서 편집하고, 위치·연구 정의·선행 조건·연결선은 씬 인스턴스에서 설정한다. 새 노드는 `ResearchBoard/EditableNodes`에 프리팹을 놓고 고유 `placementId`, `definition`, `map`, 선행 조건과 `incomingLine`을 지정한 뒤 `ResearchTestMap.nodes`에 등록한다. 아이콘은 Play에서 지정된 연구 SO를 따른다. 기존 인스턴스의 각 아이콘은 씬에서도 확인할 수 있다.

연구 툴팁은 플레이어 기본 스탯과 구분해 선택한 노드의 누적 효과를 표시한다. 0랭크는 효과 0이며, 보스 피해와 치명타 확률의 퍼센트는 소수 둘째 자리까지 표시한다. 초급 보스 피해 1랭크는 +1%다.
