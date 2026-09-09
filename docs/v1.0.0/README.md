# v1.0.0 기획 편집과 재생성

저장소 루트에서 실행한다. Python에 reportlab, pypdf가 필요하며 기존 `docs/render_plan_pdf.py`의 한글 폰트 설정을 사용한다.

```sh
python3 docs/v1.0.0/build_screen_catalog.py
python3 docs/v1.0.0/render_release_pdf.py
python3 docs/v1.0.0/validate_release.py
pdftoppm -scale-to 1200 -png output/pdf/cursor-hunter-game-design-v1.0.0.pdf /tmp/cursor-hunter-v1
```

편집 원본:

- `core.md`: 출시 검토·공통 규칙·부분 강화 제안
- `build_screen_catalog.py`: 화면48개별 정보·진입·전환·예외
- `render_release_pdf.py`: 벡터 화면, 노드 원장 생성, 통합 문서 구성
- `../cursor-hunter-game-design-v0.0.3.md`: B01~B07에 가져오는 기존 밸런스 기준. 이전 버전 보존을 위해 기준표 수정이 필요하면 v1 전용 원장으로 분리하고 생성기를 변경한다.

생성물 `screens.json`, `traits.json`, 통합 Markdown 및 PDF는 직접 편집하지 않는다. 화면 수치가 변하면 벡터 UI 예시와 명세를 함께 변경한다. 검산 통과 뒤 모든 PDF 페이지를 렌더링해 넘침·연결선·상태 일치를 확인한다.

수학 검산은 플레이 시뮬레이션이 아니다. 2시간 엔딩, 수입60%, 성능 목표는 실제 빌드 검증 전까지 가정이다. PDF의 이동 링크는 문서 탐색이며 게임 동작을 실행하지 않는다.
