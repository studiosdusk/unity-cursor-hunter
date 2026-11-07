# IsometricGrassland

Main 씬의 `Map_6_Neolithic Age/field_a`를 기준으로 만든 외형 검토용 초원 맵이다.
씬 배치, 카메라, 몬스터 생성·이동·공격 코드에 연결하는 동작은 포함하지 않는다.

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
- 루트 원점은 마름모 윗면의 중심이다. 외곽 크기가 같아도 마름모의 내부 면적은 직사각형과 다르므로, 최종 채택 시 스폰·이동 영역을 별도로 연결해야 한다.

## 구성과 원본

- `Ground`: 마름모 윗면, 좌우 흙 단면, 얇은 잔디 테두리.
- `Ground_Color_Accents`: 바닥의 약한 색 변화. 필요하면 이 자식 그룹을 끈다.
- `Flowers`: 현재 Main과 같은 `a_flower2.png`, `a_flower3.png`를 사용한 꽃 18개.
- `Small_Stones`: `f_rock.png`를 사용한 작은 돌 13개.
- 초록색은 현재 Main의 `a/field_a.png`, 흙색의 기반은 같은 패키지의 `o/field_o.png`다.
- 바닥용 `Grass_Color.asset`과 `Soil_Color.asset`은 원본 PNG를 참조하면서 투명 가장자리 1 px를 제외한 사각 스프라이트다. 마름모와 단면의 모양은 저장 가능한 Transform 조합으로 구성했다.
- `IsometricGrassland_Unlit.mat`은 URP 2D Sprite Unlit 재질이다. 별도 조명이 필요 없다.
- 루트 SortingGroup은 `Default / -10`, 전체 오브젝트 레이어는 `Ignore Raycast`다. 꽃과 돌도 몬스터보다 아래에 표시된다.
- Collider, Rigidbody, Animator, 런타임 스크립트가 없다. 장식은 통행이나 커서 판정을 막지 않는다.

## 검증

Unity 6000.3.13f1의 격리된 프로젝트에서 현재 프로젝트의 URP 2D 렌더러와 Linear 색공간으로 생성했다.
저장 후 Unity를 다시 실행해 프리팹 참조, 윗면 크기, 38개 SpriteRenderer, 스크립트·Collider 부재와 렌더링을 확인했다.
실제 Main 씬의 전투 실행이나 이동 경계 연결은 이번 검증 범위에 포함하지 않는다.
