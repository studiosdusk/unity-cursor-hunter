# 두 사람 개발 인수인계
기준 기획: [v0.0.4 최신 확정 반영본](../cursor-hunter-game-design-v0.0.4.md). Unity **6000.3.13f1**.
macOS 설치 확인: `python3 tools/open_unity.py --check`. 확인 후 같은 명령에서 `--check`를 빼면 지정 버전으로 프로젝트를 연다. 커스텀 경로는 `--editor /설치경로/Unity.app`으로 지정한다. Windows에서는 Hub에서 같은 버전을 선택한다.
런타임 씬 전략: **One Scene + 개발용 Sandbox 씬**. `App/Scenes/Bootstrap.unity`가 실제 런타임 단일 진입점이며, `CombatSandbox.unity`와 `ProgressionSandbox.unity`는 통합 런타임에 로드하지 않는 모듈별 개발·검증 씬이다.
이 폴더는 개발 협업 문서다. 기획 PDF는 수정하지 않는다.

## 누적 기록 읽기

변경이 생길 때마다 [누적 인수인계 로그](handoff-log.md)에 날짜·담당·영향 파일·검증 결과를
추가한다. 이 README는 현재 합류 방법과 소유권을 설명하고, 로그는 이전 버전의 결정을
보존한다. 최신 특성 UI의 실제 아이콘·노드·확률 매핑은
[특성 UI 에셋 매핑](trait-ui-assets.md)을 기준으로 한다.

## 승기에게 전달할 순서
1. 이 문서와 [소유권·디렉토리](architecture.md)를 읽는다.
2. [연결 계약](contracts.md)을 두 사람이 확인한다. 계약은 구현 예정 명세이며 현재 런타임 API가 존재한다는 뜻이 아니다.
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

## 첫 번째 합류 목표
- 준영: 고정 테스트 프로필 → 공격 수치 스냅샷, 시작 시 가넷만 보이는 특성 UI, Stat 젬 해금·발견 확률 갱신, 구매 전후 수치, 같은 정산 ID 두 번 지급 방지.
- 승기: 임시 원형 적으로 60초 일반 필드, 겹침 클릭 피해, 처치 집계, 만료 시 살아 있는 적 무보상 제거.
- 함께: Main → 일반 필드 → 정산 저장 → 특성 구매 → 다음 런에서 새 공격력 적용.
- 다음 합류: v1 보스 파훼·실패 무보상·최초 처치 해금 → 중첩 몬스터 → 자동/스킬 → 후반 물량 → 엔딩.
모든 화면을 먼저 한 Scene에 조립하지 않는다. 첫 루프를 합친 후 모듈별 확장한다.

## 현재 준비된 것과 개발할 것
폴더·asmdef·소유권 지침·문서·에셋 목록과 특성 화면 런타임 프로토타입을 준비했다. 전투 서비스, 저장 서비스, 테스트 씬, ScriptableObject 인스턴스는 아직 구현하지 않았다.
asmdef는 모듈 경계를 위한 설정이며 빈 폴더만 있을 때 Unity에서 빈 어셈블리 알림이 나올 수 있다.
각자 Codex에서는 `$cursor-hunter-progression`, `$cursor-hunter-combat`을 사용할 수 있다.
스킬은 `.agents/skills`에 등록했고 공유용 사본은 `docs/collaboration/skills`에 있다. 저장소를 받은 승기도 같은 스킬을 사용할 수 있다. 목록에 안 보이면 프로젝트를 다시 열거나 해당 SKILL.md를 직접 읽도록 요청한다. 수정 시 두 위치를 동기화하며 검증 스크립트가 불일치를 잡는다.

## 젬스톤 현재 규칙

- 시작에는 가넷만 보인다. 토파즈·자수정·사파이어·다이아몬드·드래곤 젬은 보스 처치 보상이
  아니라 `Stat → 젬 수집` 행의 해금 노드를 구매했을 때 보유 목록과 랜덤 드롭 표에 추가한다.
- 해금 이후에는 `기본 가중치 + 발견 강화 횟수 × 강화당 가중치`를 사용하고, 해금된
  종류의 가중치 합으로 100%를 정규화한다. 현재 값과 노드 ID는
  [trait-ui-assets.md](trait-ui-assets.md)의 젬스톤 표에 고정한다.
- UI 프로토타입은 `TraitScreenController.IsGemstoneUnlocked`,
  `GetGemstoneDropChance`, `TryRollGemstoneDrop`으로 이 규칙을 보여준다. 전투 모듈은
  UI를 참조하지 않고, 합류 시 Progression이 제공하는 읽기 전용 드롭 스냅샷을 사용한다.
