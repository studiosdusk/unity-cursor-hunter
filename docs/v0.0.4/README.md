# v0.0.4 기획서 재생성

이 폴더의 `plan_core.md`와 `build_screen_catalog.py`가 v0.0.4 편집 원본이다. `render_design_pdf.py`를 실행하면 기획 본문과 48개 화면 상태를 합쳐 PDF를 만든다.

```sh
python3 docs/v0.0.4/build_screen_catalog.py
python3 docs/v0.0.4/render_design_pdf.py
python3 docs/v0.0.4/validate_design.py
```

생성물은 `docs/cursor-hunter-game-design-v0.0.4.md`, `docs/v0.0.4/screens.json`, `docs/v0.0.4/traits.json`, `output/pdf/cursor-hunter-game-design-v0.0.4.pdf`이다. 화면과 수치를 바꿀 때는 `plan_core.md`와 화면 카탈로그를 함께 변경하고, PDF를 다시 렌더링한다.

PDF는 게임 규칙·화면·수치·전환만 담는다. 작업 검토 메모와 외부 자료 목록은 `docs/cursor-hunter-v0.0.4-review-notes.txt`에 둔다.

렌더링 결과의 페이지 수·핵심 젬스톤 상태·화면 잘림 검수 기록은 `docs/v0.0.4/visual-qa.md`에 둔다.
