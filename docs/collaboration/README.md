# 두 사람 개발 인수인계

현재 게임 버전: **0.0.5**. 전투 JSON의 schemaVersion=3과 저장 version=3은 게임 버전과 별개다.

커서 핵심 스탯의 공통 기본값은 Main 씬에서 `PlayerCombatStatsRuntime`과 함께 둔
`PlayerCombatStatsDefaults` 컴포넌트가 소유한다. 나머지 게임 초기 데이터와 전체 정보
구조는 [영어 JSON](../../Assets/CursorHunter/Data/Resources/GameData/game-data.en.json)에 둔다.
강화 비용·선행 조건·명칭·커서 스탯 증가량은
[성장 화면 설정](../../Assets/CursorHunter/Data/Resources/GameData/progression-config.en.json)으로 분리한다.
플레이어의 구매·지갑은 별도 PlayerPrefs에 저장하며, 전투 시작 때 기본 스탯과 보유 특성의
증가량을 합산한다.
[키 바로 뒤 한국어 번역 JSON](game-data.ko.keys.json)은 원본의 모든 값을 유지하고 영문키(한국어)만 병기한다.
[상세 한국어 해설 JSON](game-data.ko.reference.json)은 원본값·단위·설명·ID의 뜻까지 제공한다.
두 한국어 파일은 사람이 읽는 참고서이며 게임에서 읽지 않는다.

전투 개발자: [실제 조회 API와 값 대응표](contracts.md#전투에서-값을-읽는-실제-api).
유물 제거·몬스터 특성 자리·전투 데이터 분리 및 미완료 범위: [최신 인수인계](current-state-handoff-v0.0.4.md).

현재 구현 기준: [전투 정보 v3 인수인계](current-state-handoff-v0.0.4.md). 이전 [v2 개편 기록](game-information-refactor-2026-10-01.md)은 과거 자료다. Unity **6000.3.13f1**.
macOS 설치 확인: `python3 tools/open_unity.py --check`. 확인 후 같은 명령에서 `--check`를 빼면 지정 버전으로 프로젝트를 연다. 커스텀 경로는 `--editor /설치경로/Unity.app`으로 지정한다. Windows에서는 Hub에서 같은 버전을 선택한다.
런타임 씬 전략: **One Scene + 개발용 Sandbox 씬**. `App/Scenes/Main.unity`가 실제 런타임 단일 진입점이며, `CombatSandbox.unity`와 `ProgressionSandbox.unity`는 통합 런타임에 로드하지 않는 모듈별 개발·검증 씬이다.
이 폴더는 개발 협업 문서다. 요약 PDF는 이 폴더의 핵심본을 기준으로 재생성하고,
상세 기획 원문은 `docs/cursor-hunter-game-design-v0.0.4.md`에서 관리한다.

현재 상태를 처음 읽는 사람은 [현재 상태·인수인계 핵심본](current-state-handoff-v0.0.4.md)을 먼저 읽는다.
최신 안내를 문서 앞에 두고 옛 v0.0.4 원장은 뒤에 보존한다. 기존 PDF·캡처는 과거 자료이며
이번에 재생성하지 않았다. 세부 화면·수치·에셋은 아래 상세 문서에서 확인한다.

## 누적 기록 읽기

변경이 생길 때마다 [누적 인수인계 로그](handoff-log.md)에 날짜·담당·영향 파일·검증 결과를
추가한다. 이 README는 현재 합류 방법과 소유권을 설명하고, 로그는 이전 버전의 결정을
보존한다. 최신 특성 UI의 실제 아이콘·노드·확률 매핑은
[특성 UI 에셋 매핑](trait-ui-assets.md)을 기준으로 한다.

## 승기에게 전달할 순서
1. 이 문서와 [소유권·디렉토리](architecture.md)를 읽는다.
2. [연결 계약](contracts.md)을 두 사람이 확인한다. 핵심 런타임 계약은 현재 구현되어 있고, 보스·스킬 고유 동작은 후속 작업이다. 펫은 별도 영역 없이 커서 오라 스킬로 통합했다.
3. [병합 절차](workflow.md)에 따라 각자 작업 브랜치와 별도 체크아웃을 사용한다.
4. [에셋 매핑](assets.md)에서 자신의 원본 에셋을 찾아 전용 프리팹을 만든다.
5. 특성 화면의 노드 수·아이콘·폰트 연결은 [특성 UI 에셋 매핑](trait-ui-assets.md)을 기준으로 확인한다.
6. 기본 레이아웃은 [Stat](screenshots/trait-stat.png) · [Skill](screenshots/trait-skill.png) · [Monster](screenshots/trait-monster.png) 캡처로 확인한다. 이 파일들은 이전 캡처이며, 최신 잠금·확률 표시는 Mac 잠금 해제 후 다시 캡처한다. 젬스톤 표시 규칙과 실제 매핑은 [특성 UI 에셋 매핑](trait-ui-assets.md)을 읽는다.

| 담당 | 주 작업 | 소유하는 결과 |
|---|---|---|
| 준영 | 특성, 경제, 밸런스, 저장 | 업그레이드 UI·구매, 영구 프로필, 수치 카탈로그, 지급 확정 |
| 승기 | 일반/보스 전투 | 커서 판정, 생성, 타이머, 파훼, 스킬 실행, 전투 HUD·VFX |
| 준영(초기 통합 담당) | App, 공통 계약 | Main·화면 전환·정산 UI·설정·엔딩, 연결 및 패키지/빌드 설정 |
공통 변경은 양쪽 검토 대상으로 둔다. 담당은 작업 소유권이며 서로의 코드 열람을 제한하지 않는다.

## 현재 합류 기준

- 커서 기본 스탯 컴포넌트와 보유 커서 특성의 증가량을 런 시작 때 합산한다. 스킬·몬스터·재화 정보는 GameInformation v3 스냅샷을 사용한다.
- 일반 필드 15~30초, 자동 반경 공격·쿨타임, 일반 몬스터 10종 누적 생성.
- 펫은 스킬의 커서 오라로 통합. 유물 관련 데이터·UI·드롭은 제거했으며 새 일반 유물 시스템은 추후 설계한다.
- 몬스터 behaviorType은 enum 자리만 준비했다. 현재 None=0만 지원하며 행동은 미구현이다.
- 젬 별도 해금 트리는 제거. 일반 몬스터가 해당 젬을 공급한다.
- 노드 화면은 실제 선행 조건의 연결선, 드래그, 확대/축소를 지원한다.
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
- python3 tools/test_combat_information_v3.py
- 문서 스키마: [game-data-document.schema.json](game-data-document.schema.json)
- 이전 balance-v2.json과 GameBalanceDefinition은 과거 호환 참고이며 런타임에서 사용하지 않는다.
- python3 tools/validate_bilingual_game_data.py
- 구조 예제: [game-information-v3.example.json](game-information-v3.example.json)
- 스키마: [game-information-v3.schema.json](game-information-v3.schema.json)
- 코드 경계: [contracts.md](contracts.md)
- python3 tools/validate_collaboration.py
- python3 tools/validate_game_information.py

이번 변경은 게임을 실행하지 않고 수정·정적 검증만 진행했다.
Unity 컴파일/Import/시각 검증은 수행하지 않았다.
저장은 PlayerPrefs 프로토타입이며 영속 정산 journal과 파일/클라우드 저장은 후속 작업이다.
보스 패턴·패링, 스킬별 투사체/빙결/고유 연출도 완성된 것으로 간주하지 않는다.

테스트 플래그 testModeUnlockAll과 testModeFreeUpgrades는 Main에서 껐다.
기존 저장은 정상 첫 v3 저장 직전에 .before-v3 키에 원문 백업한다. 유물은 현재 진행도에서 제외하고 일반 강화·젬 잔액은 유지한다.
이전 캡처·v0.0.4 숫자 표·역할 스킬의 옛 규칙은 위 개편 기준과 충돌할 때 과거 기록으로 취급한다.
