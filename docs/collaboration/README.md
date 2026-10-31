# 두 사람 개발 인수인계

현재 게임 버전: **0.0.5**. 전투 JSON의 schemaVersion=4와 진행도 저장 version=6은 게임 버전과 별개다.

커서 핵심 스탯의 공통 기본값은 Main 씬에서 `PlayerCombatStatsRuntime`과 함께 둔
`PlayerCombatStatsDefaults` 컴포넌트가 소유한다. 나머지 게임 초기 데이터와 전체 정보
구조는 [영어 JSON](../../Assets/CursorHunter/Data/Resources/GameData/game-data.en.json)에 둔다.
강화 비용·선행 조건·명칭·커서 스탯 증가량은
[성장 화면 설정](../../Assets/CursorHunter/Data/Resources/GameData/progression-config.en.json)으로 분리한다.
플레이어의 구매·지갑은 별도 PlayerPrefs에 저장한다. Main의 `HuntManager.Connect Trait Screen To Combat`는 켜져 있으며, 기본 전투 시작은 특성 화면의 구매·해금 상태를 런 시작 스냅샷으로 사용한다. 수동 전투 밸런스 창은 별도의 개발 테스트 경로다. 특성 연구 화면에는 최종 전투 수치를 수동 편집하는 패널을 두지 않는다.
[키 바로 뒤 한국어 번역 JSON](game-data.ko.keys.json)은 원본의 모든 값을 유지하고 영문키(한국어)만 병기한다.
[상세 한국어 해설 JSON](game-data.ko.reference.json)은 원본값·단위·설명·ID의 뜻까지 제공한다.
두 한국어 파일은 사람이 읽는 참고서이며 게임에서 읽지 않는다.

전투 개발자: [실제 조회 API와 값 대응표](contracts.md#전투에서-값을-읽는-실제-api).
앞으로 구현할 자동 스킬의 공통 설계 기준은 [자동 스킬 기획서](../skill-design.md)를 따른다.
전투 데이터의 기본 계약: [전투 정보 v4 인수인계](current-state-handoff-v0.0.4.md), 현재 스킬·유물 상태: 아래 `현재 합류 기준`과 [유물 드롭 설계](../relic-drop-design.md).
기획자는 Main 씬의 `Tools > Cursor Hunter > Combat Balance`에서 적 수치를 실시간 조정한다.
기본인 `구매한 플레이어 특성 사용`은 구매한 공격력·커서·스킬·유물을 유지하며, 이 옵션을 끄면 플레이어 최종 스탯도 직접 시험할 수 있다. [전투 밸런스 창](../combat-balance-editor-design.md)은
베이스·커서 공격·7종 스킬·몬스터 및 스폰·1~20단계 보스를 다루며, 테스트 보상은 지급하지 않는다.
창 최상단에서 `실시간 테스트` 또는 `최종 설정 스탯` 모드를 선택하고, 그 아래 베이스·커서 공격·스킬·몬스터·스폰 분류에서 수치를 편집한다.
커서 범위는 기존 기본 크기를 `1`로 보는 배율로 입력하며, `−`/`+` 버튼으로 0.1배씩 조절할 수 있다.
`최종 설정 스탯`에서는 테스트값을 가져와 편집한 뒤 Save로 실제 기본값을 저장할 수 있다.
`실시간 테스트`는 원본을 수정하지 않으며, Save도 구매·해금·지갑·Stage 편성을 바꾸지 않는다.
유물 제거·몬스터 특성 자리·전투 데이터 분리 및 미완료 범위: [최신 인수인계](current-state-handoff-v0.0.4.md).

현재 구현 기준: [전투 정보 v4 인수인계](current-state-handoff-v0.0.4.md). 이전 [v2 개편 기록](game-information-refactor-2026-10-01.md)은 과거 자료다. Unity **6000.3.13f1**.
macOS 설치 확인: `python3 tools/open_unity.py --check`. 확인 후 같은 명령에서 `--check`를 빼면 지정 버전으로 프로젝트를 연다. 커스텀 경로는 `--editor /설치경로/Unity.app`으로 지정한다. Windows에서는 Hub에서 같은 버전을 선택한다.
런타임 씬 전략: **One Scene + 개발용 Sandbox 씬**. `App/Scenes/Main.unity`가 실제 런타임 단일 진입점이며, `CombatSandbox.unity`와 `ProgressionSandbox.unity`는 통합 런타임에 로드하지 않는 모듈별 개발·검증 씬이다.
이 폴더는 개발 협업 문서다. 요약 PDF는 이 폴더의 핵심본을 기준으로 재생성하고,
상세 기획 원문은 `docs/cursor-hunter-game-design-v0.0.4.md`에서 관리한다.

현재 상태를 처음 읽는 사람은 이 README의 `현재 합류 기준`과 [전투 계약](contracts.md)을 먼저 읽는다.
`current-state-handoff-v0.0.4.md`와 기존 PDF·캡처는 해당 시점의 과거 자료이며
이번에 재생성하지 않았다. 세부 화면·수치·에셋은 아래 상세 문서에서 확인한다.

## 누적 기록 읽기

변경이 생길 때마다 [누적 인수인계 로그](handoff-log.md)에 날짜·담당·영향 파일·검증 결과를
추가한다. 이 README는 현재 합류 방법과 소유권을 설명하고, 로그는 이전 버전의 결정을
보존한다. 최신 특성 UI의 실제 아이콘·노드·확률 매핑은
[특성 UI 에셋 매핑](trait-ui-assets.md)을 기준으로 한다.

## 승기에게 전달할 순서
1. 이 문서와 [소유권·디렉토리](architecture.md)를 읽는다.
2. [연결 계약](contracts.md)을 두 사람이 확인한다. 특성 성장의 구매 상태와 스킬·몬스터 전투 스냅샷 연결은 구현되어 있다. 독립 스킬/펫 강화, 몬스터별 표현 매핑, 화면별 성장 UX와 장기 밸런스는 `plans.md` 및 `checkList.md`에서 단계별 진행 상태를 확인한다. Main 보스 런은 1~20단계 도전·보상 정산까지 연결되어 있다. BabyDragon 펫은 [펫 스킬 기획·구현](../pet-skill-design.md)의 일곱 번째 스킬로 실행 데이터와 Main 씬에 연결됐다.
3. [병합 절차](workflow.md)에 따라 각자 작업 브랜치와 별도 체크아웃을 사용한다.
4. [에셋 매핑](assets.md)에서 자신의 원본 에셋을 찾아 전용 프리팹을 만든다.
5. 특성 화면의 노드 수·아이콘·폰트 연결은 [특성 UI 에셋 매핑](trait-ui-assets.md)을 기준으로 확인한다.
6. [Stat](screenshots/trait-stat.png) · [Skill](screenshots/trait-skill.png) · [Monster](screenshots/trait-monster.png) 캡처는 이전 탭 레이아웃의 기록이다. 현재 화면 기준은 전체 화면 단일 연결 보드이며, 최신 배치·잠금·비용 표시와 실제 아이콘 매핑은 [특성 UI 에셋 매핑](trait-ui-assets.md)을 읽는다.

| 담당 | 주 작업 | 소유하는 결과 |
|---|---|---|
| 준영 | 특성, 경제, 밸런스, 저장 | 업그레이드 UI·구매, 영구 프로필, 수치 카탈로그, 지급 확정 |
| 승기 | 일반/보스 전투 | 커서 판정, 생성, 타이머, 파훼, 스킬 실행, 전투 HUD·VFX |
| 준영(초기 통합 담당) | App, 공통 계약 | Main·화면 전환·정산 UI·설정·엔딩, 연결 및 패키지/빌드 설정 |
공통 변경은 양쪽 검토 대상으로 둔다. 담당은 작업 소유권이며 서로의 코드 열람을 제한하지 않는다.

## 현재 합류 기준

- 보스 도전은 1~20단계, 단계마다 30초다. Main의 BossSlider를 사용하며, 토벌 성공 시 3~6종 보석 동시 분출 후 공통 결과 UI에서 다음 단계/특성 강화를 선택한다. 실패는 재도전/특성 강화다. 단계·보상은 저장되며 보스별 체력·행동·보상 목록은 `BossDefinitions`의 초기 밸런스 데이터에서 조정한다. 이름은 `Data/Authoring/BossNames.xlsx`의 `ko`·`en` 등 언어 열을 수정·저장하면 Unity에서 SO에 자동 반영한다. 언어 코드 열 추가로 확장하며 미번역 이름은 한국어로 대체한다. [보스 계약](contracts.md#보스-도전-2026-10-30)을 참고한다.
- 커서 기본 스탯 컴포넌트와 보유 커서 특성의 증가량을 런 시작 때 합산한다. 전투 JSON에는 7종 스킬 정의가 있으며, 성장 트리는 베이비 드래곤을 첫 스킬로 제공하고 보스 단계 1~5에 토네이도·벼락·화염 배기·마력 구체·레이저를 하나씩 배정한다. 해금된 스킬은 Main의 BaseStat과 스킬별 UpgradeStat 스냅샷을 전투 진입 때 합산해 런 동안 고정한다. 스킬·몬스터·재화·유물 정의는 GameInformation v4 스냅샷을 사용한다.
- 일반 필드 15~30초, 자동 반경 공격·쿨타임, 일반 몬스터 10종 누적 생성.
- 전투 데이터에는 토네이도·헤드샷·벼락·화염 배기·마력 구체·레이저·베이비 드래곤 7종이 있다. 베이비 드래곤은 보스 0단계 첫 스킬이고, 이후 토네이도(1)·벼락(2)·화염 배기(3)·마력 구체(4)·레이저(5) 한 종류씩 열린다. 헤드샷은 전투 정의만 유지하며 현재 성장 트리에는 해금 노드가 없다. 실제 보스 클리어 정산이 해당 단계의 연구를 열며, 같은 단계 재도전은 더 높은 단계의 연구를 열지 않는다.
- 유물 20종의 고정 ID·출처 몬스터·개별 확률·효과 ID/수치와 단일 드롭 규칙이 JSON에 있다. 처치 때 미보유 유물 확률 합산 후 한 번 굴리고, 성공하면 고정 개별 확률에 비례해 최대 하나를 선택한다. 전투 결과의 유물 ID는 재화와 함께 정산되며 PlayerPrefs 진행도 version 6에 영구 저장된다. 테스트 도구의 보유 상태도 다음 전투에 적용된다. Unity에서 실제 드롭·정산·재실행 확인은 남아 있다. 기획은 [유물 드롭 설계](../relic-drop-design.md)를 기준으로 한다.
- JSON의 기존 몬스터 behaviorType은 None=0으로 유지한다. 신규 종별 행동·스탯 프로필은
  MonsterDefinition과 MonsterBehaviorProfile 에셋에서 구성한다.
- 젬 별도 해금 트리는 제거. 일반 몬스터가 해당 젬을 공급한다.
- 노드 화면은 실제 선행 조건의 연결선, 드래그, 확대/축소를 지원한다.
- 성장 팝업은 몬스터 해금과 생산량 증가를 구분하고, 한국어 특성·처치 보상을 표시한다. 젬스톤은 각 행에서 바로 다음 등급으로 100:1 교환할 수 있으며 누르고 있으면 반복 교환한다.
- Editor Play Mode 테스트 시계는 전체 누적 시간과 현재 보스 진행 단계 시간을 나누어 표시한다. 앱/전투가 일시정지되면 멈춘다. 화면의 실제 개행, 음량, 성장 연출과 110분 장기 시간은 Unity에서 수동 확인해야 한다.
- 전체 정보와 전투는 같은 최종 정보를 사용한다. JSON 복사 버튼으로 전투 시작값을 확인한다. 원본 파일 기본값과 구분한다.
- 런타임 UI는 TMP, 공용 한글 동적 SDF 폰트, 기본 ko/en UI 키를 사용한다.
- 실제 런타임·빌드 진입점은 Main.unity다.

## 데이터와 검증

- 커서 핵심 스탯 기본값: Main.unity의 PlayerCombatStatsDefaults 컴포넌트
- 그 외 초기 정보와 전체 GameInformation 구조: Assets/CursorHunter/Data/Resources/GameData/game-data.en.json
- 성장 화면 설정: Assets/CursorHunter/Data/Resources/GameData/progression-config.en.json (Combat에는 전달하지 않음)
- 한국어 키 번역: [game-data.ko.keys.json](game-data.ko.keys.json) (키만 번역 병기, 구조/값 유지)
- 한국어 상세 해설: [game-data.ko.reference.json](game-data.ko.reference.json) (비실행 문서)
- 원본 수정 후 python3 tools/build_korean_game_data_reference.py --patch 출력을 apply_patch로 적용해 두 참고본을 갱신한다.
- python3 tools/test_korean_game_data_reference.py
- python3 tools/test_combat_information_v4.py
- 문서 스키마: [game-data-document.schema.json](game-data-document.schema.json)
- 이전 balance-v2.json과 GameBalanceDefinition은 과거 호환 참고이며 런타임에서 사용하지 않는다.
- python3 tools/validate_bilingual_game_data.py
- 구조 예제: [game-information-v4.example.json](game-information-v4.example.json)
- 스키마: [game-information-v4.schema.json](game-information-v4.schema.json)
- 코드 경계: [contracts.md](contracts.md)
- python3 tools/validate_collaboration.py
- python3 tools/validate_game_information.py

현재 브랜치의 Unity 6000.3.13f1 컴파일·기능·실제 화면 검증은 [2026-10-30 QA](../qa/2026-10-30-playtest.md)를 참조한다. 장기 밸런스와 최종 청취 평가는 별도 검수 범위다.
저장은 PlayerPrefs 프로토타입이며 영속 정산 journal과 파일/클라우드 저장은 후속 작업이다.
보스 패턴·패링, 스킬별 투사체/빙결/고유 연출도 완성된 것으로 간주하지 않는다.

테스트 플래그 testModeUnlockAll과 testModeFreeUpgrades는 Main에서 껐다.
현재 저장 version은 6다. 기존 version 5 이하의 저장은 첫 v6 저장 직전에 .before-v6 키에 원문 백업한다. v5 저장의 통화·구매·단계 값은 그대로 읽고, 신규 유물 소유 목록은 빈 목록으로 시작한다. 기존 저장에는 신규 온보딩을 강제하지 않으며, 코어·그루터기가 있으면 새 튜토리얼의 추가 구매를 요구하지 않고 전투를 시작할 수 있다. 신규/명시적 초기화 프로필은 메인 Trait 안내부터 첫 연구를 진행한다. 일반 강화·젬 잔액·보스 단계·유물 소유 ID를 저장한다.
이전 캡처·v0.0.4 숫자 표·역할 스킬의 옛 규칙은 위 개편 기준과 충돌할 때 과거 기록으로 취급한다.
