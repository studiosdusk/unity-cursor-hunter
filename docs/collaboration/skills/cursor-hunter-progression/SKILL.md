---
name: cursor-hunter-progression
description: Cursor Hunter의 특성 업그레이드, 밸런스, 재화 및 저장 개발에 사용하는 담당별 작업 지침.
---
프로젝트 루트의 docs/collaboration/README.md, architecture.md, contracts.md를 읽고 해당 역할의 변경 경로를 확인한다.
Data와 Progression을 소유한다. App은 초기 통합 담당 영역이다. Combat 구현에 의존하지 않는다. 독립 분기, 가넷부터 시작하는 젬 노출, Int64, 중복 정산 방지와 저장 실패 재시도를 유지한다. 데이터는 항목별로 나누고 표시명과 ID를 분리한다. 구매/지급 결과를 테스트한다.
기존 타입을 먼저 확인한다. 문서의 계약 이름은 구현 예정 명세이며 존재한다고 가정하지 않는다.
공통 계약 변경 시 양쪽 생산자·소비자와 테스트 더블을 함께 검토한다.
docs/collaboration/assets.md에서 확인한 에셋을 담당 폴더의 Variant/래퍼로 사용한다.
python3 tools/validate_collaboration.py를 실행하고 Unity 검증 여부를 구분해 보고한다.
