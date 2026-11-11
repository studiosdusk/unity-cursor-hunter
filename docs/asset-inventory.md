# Unity 에셋 전체 인벤토리

- 조사 기준 브랜치: `v0.0.8(dong)`
- 조사일: 2026-11-11
- 대상: `Assets/` 아래에서 Unity가 실제 콘텐츠로 읽는 비메타 파일 **21,724개**
- 총 파일 크기: **2.40 GiB** (2,581,794,325 bytes)
- 제외: Unity 식별자 보조 파일 `.meta`와 `rg`가 무시하는 macOS `.DS_Store` 7개. CSV에는 경로, 그룹, 유형, 확장자, 크기를 함께 넣었다.
- 전체 경로 목록: [`docs/asset-inventory.csv`](asset-inventory.csv)

## 전체 자산 유형

| 유형 | 파일 수 |
|---|---:|
| 이미지·텍스처 | 15,049 |
| 프리팹 | 2,695 |
| 머티리얼 | 2,653 |
| 코드·셰이더 | 554 |
| 데이터·설정 | 286 |
| 애니메이션·리깅 | 178 |
| 문서 | 107 |
| 3D 모델 | 99 |
| 씬 | 89 |
| 오디오 | 7 |
| 플러그인·바이너리 | 5 |
| 글꼴 원본 | 2 |

## 폴더·에셋 패키지별 수량

| 경로 그룹 | 파일 수 |
|---|---:|
| `Assets/DownLoadAssets/118 sprite effects bundle` | 6,033 |
| `Assets/DownLoadAssets/Casual_GUI` | 4,196 |
| `Assets/DownLoadAssets/Eric VFX Studio` | 3,556 |
| `Assets/DownLoadAssets/IconPack` | 2,705 |
| `Assets/DownLoadAssets/2D Characters-PetPack4` | 1,230 |
| `Assets/DownLoadAssets/GUI-CasualFantasy` | 931 |
| `Assets/DownLoadAssets/Casual_VFX` | 720 |
| `Assets/DownLoadAssets/Map` | 605 |
| `Assets/DownLoadAssets/MonsterAsset` | 597 |
| `Assets/CursorHunter` | 377 |
| `Assets/Spine` | 360 |
| `Assets/Spine Examples` | 343 |
| `Assets/TextMesh Pro` | 37 |
| `Assets/Plugins` | 19 |
| `Assets/Editor` | 5 |
| `Assets/Settings` | 4 |
| `Assets/DefaultVolumeProfile.asset` | 1 |
| `Assets/InputSystem_Actions.inputactions` | 1 |
| `Assets/Resources` | 1 |
| `Assets/Scenes` | 1 |
| `Assets/Scripts` | 1 |
| `Assets/UniversalRenderPipelineGlobalSettings.asset` | 1 |

## 확장자별 전체 수량

| 확장자 | 파일 수 |
|---|---:|
| `.png` | 14,948 |
| `.prefab` | 2,695 |
| `.mat` | 2,653 |
| `.cs` | 459 |
| `.asset` | 224 |
| `.anim` | 121 |
| `.fbx` | 96 |
| `.unity` | 89 |
| `.txt` | 71 |
| `.shader` | 66 |
| `.tga` | 52 |
| `.psd` | 43 |
| `.json` | 38 |
| `.controller` | 35 |
| `.md` | 34 |
| `.cginc` | 23 |
| `.overridecontroller` | 20 |
| `.asmdef` | 18 |
| `.shadergraph` | 5 |
| `.dds` | 4 |
| `.ogg` | 4 |
| `.wav` | 3 |
| `.obj` | 3 |
| `.bytes` | 3 |
| `.ttf` | 2 |
| `.xml` | 2 |
| `.dll` | 2 |
| `.preset` | 2 |
| `.xlsx` | 1 |
| `.asmref` | 1 |
| `.spine` | 1 |
| `.playable` | 1 |
| `.tif` | 1 |
| `.inputactions` | 1 |
| `.jpg` | 1 |
| `.scenetemplate` | 1 |
| `.hlsl` | 1 |

## 스탯 화면 UI에 쓸 만한 보유 리소스

### 캐주얼 판타지 UI 팩
- 경로: `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/`
- PNG 수량(컴포넌트 하위): **674장**
- 프레임 **98**, 버튼 **55**, 라벨 **50**, 팝업 **9**, 슬라이더 **73**, 기타 UI **31**, 장식 아이콘 **96**, 깃발 아이콘 **62**.
- `Icon_ItemIcons`는 50종 아이콘을 `Original/128/256/512` 크기로 제공한다.
- 프레임·버튼·탭 에셋 후보: `Frame/BasicFrame_SquareOutline03_White.png`, `Frame/CardFrame01_White1.png`, `Frame/SpeechFrame01_White1.png`, `Button/Button_SquareSolid03_White.png`, `Button/Button_SquareGradient156_White1.png`, `UI_Etc/TabMenu_Top_White_Bg.png`, `UI_Etc/TabMenu_Top_White_Focus.png`.

### Simple Casual GUI Pro 팩
- 경로: `Assets/DownLoadAssets/Casual_GUI/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/`
- 컴포넌트 PNG **2,947장**. 하위에 Custom/Demo Light/Demo Dark 프레임, 카드·스킬·다이아 프레임, 테이블·목록 프레임, 버튼·팝업·슬라이더·UI 표시물이 있다.
- `Frame/Frame_Custom/`에는 **126장**이 있고, 예: `SkillFrame01_White1.png`, `CardFrame01_White1.png`, `BasicFrame_Diamond01_White1.png`, `ListFrame00_White.png`, `TableFrame01_White1.png`, `MessageFrame_White1.png`.
- 별도 프리팹 **598개**, 프리뷰 화면 **119개**가 있다. 현재 Main 씬 전투 배경도 이 팩의 `ResourcesData/Sprites/Demo/Demo_Background/Background_00.png`를 사용한다.

### UI 에셋 시각 미리보기
- [UI 스프라이트 검색 갤러리](asset-inventory/ui-asset-gallery.html): 두 GUI 팩의 프레임·버튼·라벨·팝업·슬라이더 **1,293장**을 이름으로 검색하며 미리 볼 수 있다.
- 아래 연락처 시트는 스탯 화면에서 비교할 만한 91개 후보를 묶었다. 투명 스프라이트는 어두운 배경 위에 올려 표시했다. 흰색 계열 스프라이트는 게임 안에서 색상을 입혀 쓰는 원본이다.

  - [프레임·노드 후보](asset-inventory/ui-preview-frames/1.png)
  - [버튼·탭·팝업 후보](asset-inventory/ui-preview-actions/1.png)
  - [라벨·상태 UI 후보](asset-inventory/ui-preview-labels/1.png)

### 아이콘·게임 오브젝트 팩
- `Assets/DownLoadAssets/IconPack/2D Minimal-IconPack/Icons/`: 아이콘 PNG **2,700장**. 보상·재화·게임 메뉴·장비 등 범용 아이콘이 많다. 현재 캐릭터/몬스터의 카툰 스타일과 혼합할 때는 스타일을 확인하는 것이 좋다.
- `Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/`: 적 몬스터 스프라이트·프리팹 등 **597개**.
- `Assets/DownLoadAssets/2D Characters-PetPack4/`: 펫 캐릭터·애니메이션·Spine 리소스 등 **1,230개**.
- `Assets/DownLoadAssets/Map/2D Maps - Age Battle Stages/`: 맵·스테이지 관련 **605개**.

## Main 씬에서 스탯 UI가 실제 참조하는 에셋

| 용도 | 에셋 경로 |
|---|---|
| 현재 패널 배경 | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Frame/BasicFrame_SquareSolid01_Demo01.png` |
| 버튼 프레임 | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Button/Button_SquareSolid03_White.png` |
| 연구 카테고리 프레임 | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Frame/ItemFrame01_Demo_Gray.png` |
| 특성 카테고리 프레임 | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Frame/ItemFrame01_Demo_Green.png` |
| 유물 카테고리 프레임 | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Frame/ItemFrame01_Demo_Purple.png` |
| 연구 보드 내부 탭 기본/선택 프레임 | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/UI_Etc/TabMenu_Top_Demo_Bg.png` / `TabMenu_Top_Demo_Focus.png` |
| 연구 탭 아이콘 | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Book02_Brown.png` |
| 특성 탭 아이콘 | `Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Medalstand.png` |
| 화면/전투 UI 원본 글꼴 | `Assets/CursorHunter/Progression/Runtime/Fonts/NanumGothic.ttf` |
| UI 배경 | `Assets/DownLoadAssets/Casual_GUI/GUI Pro-SimpleCasual/ResourcesData/Sprites/Demo/Demo_Background/Background_00.png` |

Main의 연구·특성·유물 화면은 `GUI-CasualFantasy`의 Demo 계열 패널/탭 프레임을 일부 사용하고 있다. 같은 팩에 White 프레임이 있고, `GUI Pro-SimpleCasual`에는 전용 카드·스킬·목록·테이블 프레임이 많이 있으므로, 다음 UI 개선에서는 한 프레임 계열을 선택해 버튼·노드·정보 패널을 통일하는 것이 가장 효율적이다.

## 전체 목록 CSV 사용법

`경로`나 `그룹`으로 검색하면 모든 원본 파일을 찾을 수 있다. Unity `.meta`를 제외했으므로, Unity GUID는 원본 `.meta`에서 별도로 확인한다. CSV의 `번호`는 이 스냅샷에서 찾기 위한 인덱스이며 Unity 에셋 ID는 아니다.
