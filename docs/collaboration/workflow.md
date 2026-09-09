# 브랜치·병합 절차
## 시작
Unity Hub에서 6000.3.13f1로 열고 ProjectSettings/ProjectVersion.txt와 일치하는지 확인한다.
ProjectSettings는 이미 Force Text, Visible Meta Files이다. 다른 버전으로 열어 자동 변환한 변경은 섞지 않는다.
이 설계가 반영된 공통 기준 커밋에서 각자 브랜치를 만든다. 예: codex/progression/purchase, codex/combat/normal-field.
각자 별도 clone/worktree를 사용한다. 두 Unity 에디터로 같은 디렉토리를 동시에 열지 않는다.

## 충돌이 생기기 쉬운 지점
| 우선순위 | 원인·근거 | 확인·예방 |
|---|---|---|
| 높음 | 같은 씬·프리팹 YAML 동시 수정 | git diff --name-only로 겹친 경로 확인, 전용 Sandbox·루트 Prefab |
| 높음 | 공통 데이터/ID와 소비 코드 동시 변경 | 계약 PR 먼저 합치고 양쪽 rebase/merge |
| 높음 | .meta 유실·재생성으로 GUID 변경 | 에셋과 .meta 함께 커밋, Unity 내 이동, Missing Script/참조 검사 |
| 중간 | 버전/패키지/렌더러 자동 갱신 | 설정 변경은 통합 담당 단독 PR |
| 중간 | 대형 카탈로그·아틀라스 동시 편집 | 한 데이터 항목 한 파일, 모듈별 아틀라스 |
우선순위는 구조에서 추정한 위험이며 실제 충돌 빈도 측정값은 아니다.

## PR 한 번의 흐름
1. 작업 시작 전에 상대와 변경 폴더·공통 계약 변경 여부 공유.
2. 한 PR에 한 기능. Assets 변경은 .meta까지 포함. 원본 공급사 에셋 수정은 별도 합의.
3. python3 tools/validate_collaboration.py 실행.
4. 해당 Sandbox에서 확인하고 결과·재현 절차를 PR에 기록.
5. 공통 계약 변경은 상대 검토 후 먼저 병합. 후속 기능 브랜치는 최신 기준을 반영.
6. 통합 담당이 Bootstrap에 두 프리팹을 연결해 첫 루프 확인 후 병합.
7. PDF 재생성은 사용자만 담당. 생성 PDF/JSON 충돌은 원본을 병합하고 다시 생성한다.

## 충돌 시
.scene/.prefab/.asset YAML을 무조건 ours/theirs로 덮지 않는다. 변경 의도를 확인하고 원 담당이 Unity에서 다시 적용한다.
GUID가 달라졌다면 유효한 기존 .meta를 보존하고 참조 누락을 확인한다. GUID 전체 재생성 금지.
텍스트 계약은 양쪽 호출부와 함께 해결한다. 개인 미커밋 변경을 reset/clean으로 지우지 않는다.
UnityYAMLMerge는 각 PC의 동일 버전 Tools 위치를 확인한 뒤 선택적으로 설정한다. 이 작업에서는 전역 Git 설정을 변경하지 않는다.
원격에 올라간 공유 브랜치의 force push는 사용하지 않는다.

## 역할별 완료 기준
사용자: 재화 부족/선행 조건/독립 분기/MAX/저장 실패/중복 지급/잠긴 젬 UI.
친구: 시간 경계/중도 종료/파훼/자동 중복/풀 반환/프레임 할당/1,200마리.
공동: 씬 왕복 시 구독 누수, 오래된 비동기 참조, 일시정지, 실제 빌드, 기존 SampleScene 영향.
Github 계정을 알 수 없어 CODEOWNERS에는 가짜 계정을 넣지 않았다. 팀 계정 확정 후 이 소유권 표를 경로 규칙으로 등록한다.
