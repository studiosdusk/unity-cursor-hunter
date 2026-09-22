> **역사적 편집 원본 안내 (2026-09-21)**: 이 폴더는 초기 v0.0.4 화면 카탈로그 스냅샷이다.
> 최신 기준은 [`../collaboration/current-state-handoff-v0.0.4.md`](../collaboration/current-state-handoff-v0.0.4.md)다.
> 상세 수치와 화면 원문은 [`../cursor-hunter-game-design-v0.0.4.md`](../cursor-hunter-game-design-v0.0.4.md)에서 확인한다.
> 일반 Field 15→60초, 보스 60초 고정, Monster1~20, 젬스톤 해금, 전리품 조각 원장은
> 최신 문서를 우선하며 이 폴더의 과거 수치·이름은 현재 구현 기준으로 사용하지 않는다.

# v0.0.4 기획서 재생성

이 폴더의 `plan_core.md`와 `build_screen_catalog.py`가 v0.0.4 편집 원본이다. `render_design_pdf.py`를 실행하면 기획 본문과 48개 화면 상태를 합쳐 PDF를 만든다.

위 명령은 상세 화면 카탈로그를 보존하기 위한 레거시 재생성 경로다. 팀에 전달하는 최신
요약 PDF는 저장소 루트에서 `python3 tools/render_current_design_pdf.py`를 실행해
협업 인수인계 핵심본으로 생성한다.

```sh
python3 docs/v0.0.4/build_screen_catalog.py
python3 docs/v0.0.4/render_design_pdf.py
python3 docs/v0.0.4/validate_design.py
```

기존 생성물은 `docs/cursor-hunter-game-design-v0.0.4.md`, `docs/v0.0.4/screens.json`, `docs/v0.0.4/traits.json`이다. 최신 요약 PDF는 협업 인수인계 핵심본을 기준으로 `tools/render_current_design_pdf.py`가 생성한다.

PDF는 게임 규칙·화면·수치·전환만 담는다. 작업 검토 메모와 외부 자료 목록은 `docs/cursor-hunter-v0.0.4-review-notes.txt`에 둔다.

렌더링 결과의 페이지 수·핵심 젬스톤 상태·화면 잘림 검수 기록은 `docs/v0.0.4/visual-qa.md`에 둔다.
