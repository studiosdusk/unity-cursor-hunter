# 특성 카드 데이터

특성 카드는 `Assets/CursorHunter/Data/Resources/TraitCardDefinitions`에 한 카드당 하나의 `TraitCardDefinition` SO로 관리한다. `stableId`는 저장과 풀 선택에 사용할 고정 ID다. `presentation`은 엑셀의 문자열 키, 아이콘, 표시 순서를 가진다. `gameplay`는 효과 종류와 해당 효과의 수치만 관리한다. Inspector에는 선택한 효과 종류의 수치만 표시한다.

아이콘은 `Assets/DownLoadAssets/IconPack/2D Minimal-IconPack/Icons/512`의 Sprite를 참조한다. 공급사 원본 파일은 수정하지 않는다.

## 텍스트 수정

`Assets/CursorHunter/Data/Authoring/TraitCardText.xlsx`의 `특성 카드 표시 정보` 시트가 표시 문구의 원본이다. 첫 열은 고정 키, 나머지 열은 `ko`, `en` 등 언어 코드다. 각 카드의 `presentation.textKey`에 `.title`과 `.description`을 붙인 행을 사용한다. 한국어와 영어는 필수이며, 다른 언어는 빈 번역을 한국어로 대체할 수 있다.

설명의 `{chance}`, `{multiplier}`, `{count}`, `{interval}`, `{duration}`, `{bonus}`는 게임 효과 SO의 수치를 참조한다. 보물상자의 `{chance}`는 해당 SO의 `게임 효과 > Treasure Chest > Chance Percent`를, `{count}`는 `Spawn Count`를 참조한다. 확률은 0~100 사이의 백분율이며 기존 확정 생성에 맞춘 초기값은 100이다. 언어별 문구에서 해당 효과에 필요한 자리표시자는 유지해야 한다. 백분율은 소수 둘째 자리까지 표시한다. 엑셀을 저장하면 Unity가 현지화 테이블을 갱신하며 `Tools > Cursor Hunter > Trait Card Text > Apply Excel`로도 적용할 수 있다. `TraitCardSlot`이 번역된 원문과 SO 게임 수치를 `TraitCardTextFormatter.Format`에 전달해 설명을 표시한다.

## 현재 범위

네 카드의 표시·게임 효과 데이터, 엑셀 가져오기, 카드 공개·선택·저장·보유 목록과 전투 효과를 연결했다. 카드의 게임 수치는 전투 시작에 복사되어 해당 전투 동안 고정된다. 보물상자의 체력·외형·재화는 [TreasureChestMonster.asset](TreasureChestMonster.asset)의 `MonsterDefinition`에서 관리하며 기본값은 체력 10·가넷 100·경험치 0이다. 생성 확률과 수는 카드 SO에서 관리한다. 전투 종료 보상 배율은 젬스톤 6종에 적용하고, 커서 증가는 반경 기준이며, 일반 몬스터 경험치의 소수점은 전투 안에서 누적한다.
