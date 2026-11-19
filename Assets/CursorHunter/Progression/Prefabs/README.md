# Progression/Prefabs
담당: 사용자 (Contracts는 공동 검토).
용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.
씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.

## 공통 성장 UI 배경

`ProgressionUiBackdrop.prefab`은 `Background_00.png`, 채도 조정 오버레이, `sparkle6_blue.png` 기반의 `FloatingSparkles`를 한 계층으로 묶는다. Main 연구 화면과 `TraitScreen_Runtime`에서 사용하며, `TraitScreen_Runtime`은 유물/특성 카드 섹션이 공유한다. 새로운 성장 UI는 이 프리팹을 화면 배경으로 첫 자식에 배치하고, 전용 색조나 입자 연출은 프리팹 Variant 또는 해당 인스턴스 설정으로 조정한다.
