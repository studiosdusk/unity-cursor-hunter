# IsometricGrassland

Main 씬의 `Map_6_Neolithic Age/field_a`를 기준으로 만든 초원 맵이다.
현재 Main에 배치된 인스턴스를 `CombatRunController.Battlefield`가 직접 참조하며, 생성·이동·공격의 공통 경계로 사용한다.
Main의 맵과 하늘 배경 인스턴스는 기본 비활성 상태다. `AppUiRootController`의 Combat 바인딩에 함께 연결되어 일반·보스 전투 입장부터 결과창까지 표시되며, 메인·특성·설정·필드/보스 선택 화면에서는 비활성화된다. 바인딩의 `Additional Roots`가 월드 오브젝트를 제어하므로 맵을 UI Transform 아래에 넣지 않는다.

## 배치

- `IsometricGrassland.prefab`을 씬에 끌어 넣고 Position `(0, 0, 0)`, Rotation `(0, 0, 0)`, Scale `(1, 1, 1)`로 비교한다.
- 기존 맵을 잠시 비활성화하면 마름모 바깥과 얇은 흙 단면을 확인하기 쉽다.
- XY 평면용 2D 프리팹이다. 현재 정면 Orthographic 카메라를 그대로 사용한다.
- 크기는 루트의 X/Y Scale을 같은 비율로 조절한다. 바닥 면 안쪽의 투영용 Transform은 그대로 둔다.

## 크기 기준

- Main의 원본 바닥은 `field_a.png` 202 × 111 px, 100 PPU, Local Scale `(26, 27, 1)`이다. SpriteRenderer는 Simple 모드다.
- 원본 스프라이트 경계는 **52.52 × 29.97 유닛**이다.
- 이 비율을 유지하며 70%로 맞춘 마름모 윗면은 **36.764 × 20.979 유닛**이다. 흙 단면의 높이는 **0.6 유닛**이다.
- Main 카메라의 Orthographic Size 12와 프로젝트 기본 1920 × 1080 화면을 기준으로 전체 외곽이 들어오도록 맞췄다. 다른 화면비에서는 별도 배치를 확인한다.
- 루트 원점은 마름모 윗면의 중심이다. 외곽 크기가 같아도 마름모의 내부 면적은 직사각형과 다르므로, 루트의 `BattlefieldArea`가 마름모 윗면을 생성·이동 영역으로 제공한다.

## 구성과 원본

- `Ground`: 마름모 윗면, 좌우 흙 단면, 얇은 잔디 테두리.
- `Ground_Color_Accents`: 사용하지 않는다. 마름모 색상 패치 두 개는 비활성화해 바닥을 균일한 초록색으로 표시한다.
- `Flowers`: 현재 Main과 같은 `a_flower2.png`, `a_flower3.png`를 사용한 꽃 18개.
- `Small_Stones`: `f_rock.png`를 사용한 작은 돌 13개.
- 초록색은 현재 Main의 `a/field_a.png`, 흙색의 기반은 같은 패키지의 `o/field_o.png`다.
- 바닥용 `Grass_Color.asset`과 `Soil_Color.asset`은 원본 PNG를 참조하면서 투명 가장자리 1 px를 제외한 사각 스프라이트다. 마름모와 단면의 모양은 저장 가능한 Transform 조합으로 구성했다.
- `IsometricGrassland_Unlit.mat`은 URP 2D Sprite Unlit 재질이다. 별도 조명이 필요 없다.
- 루트 SortingGroup은 `Default / -10`, 전체 오브젝트 레이어는 `Ignore Raycast`다. 꽃과 돌도 몬스터보다 아래에 표시된다.
- Collider, Rigidbody, Animator가 없다. `BattlefieldArea`는 경계와 공간 배율만 제공하며 장식은 통행이나 커서 판정을 막지 않는다.

## 전투 공간 설정

루트의 `BattlefieldArea` Inspector에서 조정한다. Half Size는 `(18.382, 10.4895)`로 윗면에 맞춘 값이며 흙 단면은 포함하지 않는다. 맵 루트의 이동·회전·Scale은 경계에 반영된다. 카메라는 기존 정면 Orthographic을 유지한다.

| 항목 | 기존 대비 배율 |
|---|---:|
| 일반 몬스터 / 보스 | 0.62 |
| 나무형 골렘 monster.08 | 0.50 |
| 작은 벌 monster.02 | 0.80 |
| 커서 / 일반 스킬 범위 | 0.65 |
| 마력 구체 | 0.55 |
| 펫 | 0.58 |
| 이동 속도 | 0.70 |
| 피해 숫자 | 0.85 |

- 몬스터는 발 위치와 몸체 폭에 따른 여백을 기준으로 생성·이동·순환·순간이동한다. 그림은 세워 두고 발의 Y 위치로 정렬한다.
- 커서의 표시와 실제 Collider가 함께 줄어든다. 맵 밖·UI 위에서도 커서 그림을 HUD보다 앞에 계속 표시한다. 공격 경로는 맵 밖·UI 위에서 끊는다.
- 토네이도와 마력 구체는 크기 여백을 두고 경사진 변에서 반사한다. 구체는 반사 전후 구간별로 적 충돌을 검사한다.
- 레이저는 맵 변을 따르는 두 대각선 중 하나로 몬스터를 관통한다. 경로를 예고한 뒤 발사하고 잔광으로 사라지며, 판정 폭과 광륜이 맵 밖으로 나가지 않게 양 끝을 자른다. 기본 폭은 `1.6 × 스킬 크기 × 유물 범위 × Skill Scale`, 기본 1초 연출은 예고 0.18초·발사 0.32초·잔광 0.5초다. 피해 11·쿨타임 5초와 강화 계산은 유지한다. 화염 배기는 이미 축소한 커서 크기를 상속하고 맵을 벗어날 때 풀로 반환한다.
- 펫의 위치·추적은 맵 내부로 제한하고, 탐지·공격 거리와 이동 속도에도 공간 배율을 적용한다.
- 기본 스탯·구매 수치·밸런스 저장값은 논리 값 그대로 유지한다. 소비 시 한 번만 배율을 적용하며 재시작·풀 재사용 때 누적하지 않는다.
- `Battlefield` 참조가 없는 Sandbox는 기존 카메라 직사각형 경계를 사용한다.

## 하늘 배경

Main에는 별도의 `IsometricSkyBackdrop.prefab` 인스턴스를 배치했다. `GUI Pro-SimpleCasual`의 `ResourcesData/Sprites/Demo/Demo_Background/Background_01.png`를 어두운 청록색으로 표시하고, 같은 패키지 `Demo_Image/Image_Cloud_00.png`와 `Image_Cloud_01.png`로 움직이는 구름 4개를 구성한다. 원본 이미지·임포터는 변경하지 않는다.

- `IsometricSkyBackdrop` Inspector의 **Sky Tint**는 `(0.13, 0.28, 0.32, 1)`, **Cloud Tint**는 `(0.4, 0.62, 0.66, 0.18)`이다. 색은 전투 맵·몬스터·HUD에 적용되지 않는다.
- 카메라에 맞춰 종횡비를 유지하며 화면 전체를 덮고, `Overscan 1.03`으로 가장자리 여유를 둔다. 화면 비율·Orthographic Size·카메라 위치가 바뀌어도 배경을 다시 맞춘다.
- 구름은 각기 화면 폭의 13~18% 크기이며 초당 화면 폭의 0.18~0.32%만큼 서로 다른 방향으로 이동한다. 21~29초 주기의 작은 상하 움직임도 더했다. 구름 전체가 화면 밖으로 나간 뒤 반대쪽에서 순환한다.
- `Clouds`에서 구름별 **Viewport Center / Viewport Width / Drift Speed / Bob Height / Bob Period**를 조정한다. Main의 `worldCamera`와 `combat` 참조는 씬 인스턴스에 연결했다. 전투 일시정지·입장 대기에는 구름도 멈춘다.
- Sky는 `Default / -1000`, 구름은 `Default / -990~-987`로 맵(`Default / -10`)보다 뒤에 그린다. 전체 레이어는 `Ignore Raycast`이고 Collider·Canvas·입력 컴포넌트가 없다. 스프라이트 5개를 재사용하며 매 프레임 새 오브젝트를 생성하지 않는다.

## 검증

`BattlefieldAreaTests`는 변환된 맵, 크기 여백, 긴 프레임 반사, 레이저 단면과 커서 재진입을 검사한다. `IsometricMainPlayModeTests`는 Main의 20·40·80마리, 라이브 밸런스, 스킬·펫 재시작과 1~20단계 보스 경계를 검사한다. 실제 실행 결과는 `docs/collaboration/handoff-log.md`의 아이소메트릭 전투 적용 항목을 참고한다.
