---
name: cursor-hunter-combat
description: Cursor Hunter의 일반·보스 전투, 커서 판정, 스킬, HUD 및 VFX 개발에 사용하는 담당별 작업 지침.
---
프로젝트 루트의 docs/collaboration/README.md, architecture.md, contracts.md를 읽고 해당 역할의 변경 경로를 확인한다.
Combat을 소유하고 Contracts만 참조한다. 영구 지갑이나 저장 파일을 변경하지 않는다. 고정 런 스냅샷으로 Sandbox부터 개발한다. 시간 만료 경계, 수동 파훼, 자동/수동 중복 방지, 누적 몬스터를 유지한다. 풀 반환, 매 타격 할당, 구독 해제와 오래된 runId 콜백을 검토한다. 검기·휘두르기 이펙트는 사용하지 않는다.
기존 타입을 먼저 확인한다. 문서의 계약 이름은 구현 예정 명세이며 존재한다고 가정하지 않는다.
공통 계약 변경 시 양쪽 생산자·소비자와 테스트 더블을 함께 검토한다.
docs/collaboration/assets.md에서 확인한 에셋을 담당 폴더의 Variant/래퍼로 사용한다.
python3 tools/validate_collaboration.py를 실행하고 Unity 검증 여부를 구분해 보고한다.
