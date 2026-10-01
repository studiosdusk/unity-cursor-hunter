# 과거 JSON v2 개편 기록

> 아래 내용은 유물 제거·전투 정보 v3 분리 이전의 기록이다. 현재 기준은
> [최신 인수인계](current-state-handoff-v0.0.4.md)와 [연결 계약](contracts.md)이다.
> 현재 game-data.en.json에는 progression/entities/fieldHelp/implementation/유물 정보가 없으며,
> 성장 설정은 progression-config.en.json으로 분리했다. 몬스터 behaviorType은 None=0만 준비했다.

# 2026-10-01 게임 정보 JSON / 성장 구조 개편

최신 확인: [진행도 저장·전투 조회·요청 9항목의 인수인계](current-state-handoff-v0.0.4.md).
[키 바로 뒤 한국어 번역본](game-data.ko.keys.json)은 "attackPower(공격력)": 1처럼
원본 값과 구조를 그대로 유지한다. 이 파일 역시 설명용이며 런타임에서는 읽지 않는다.
아래 구현 설명의 잔여 예외(젬 결제 조건, 전체 초기화 조각 보존, 저장 실패)는 최신 인수인계를 우선한다.

## 최종 정정: 영어 실행 원본 + 한국어 해설 문서

- [실제 전투·성장 입력: game-data.en.json](../../Assets/CursorHunter/Data/Resources/GameData/game-data.en.json)
- [사람이 읽는 한국어 해설: game-data.ko.reference.json](game-data.ko.reference.json)
- [실행 문서 스키마](game-data-document.schema.json)

게임은 영어 JSON만 읽는다. 한국어 해설은 번역판 실행 파일이 아니다.
이전 Resources의 game-data.ko.json / active-data.json 및 해당 meta는 제거했다.
기존 한국어 설명은 docs의 새 해설과 재생성 도구에 보존했다.
GameDataDocument.Load()는 GameData/game-data.en 경로만 사용하고 locale=en만 허용한다.
UI 표시 언어는 이 데이터 파일의 언어와 별개다.

### 한국어 해설 읽는 법

예를 들어 영어의 attackPower는 아래처럼 보인다.

```json
"attackPower (공격력)": {
  "원본값": 1,
  "설명": "기본 자동 공격 한 번의 피해입니다. 강화·유물 효과를 적용하기 전 파일 기준값입니다. 정수로 입력합니다."
}
```

영어 원본의 모든 필드를 원래 계층과 배열 순서대로 담았다.
키는 영문키와 한글명을 병기하고, 원본값은 정확한 값/자료형을 보존한다.
이름·설명·ID·enum은 한국어뜻도 함께 표시한다. 큰 Int64 값도 문자열로 바꾸지 않는다.
원본 전체 SHA-256 및 원본값 복원 검사로 누락과 오래된 수치를 찾는다.
한국어 파일을 수정해도 게임 수치는 바뀌지 않는다. 수치는 영어 원본에서만 수정한다.

information에는 스탯·전투 규칙·젬 6종·몬스터 10종·스킬 7종·유물 15종이 있다.
progression에는 38개 분류/186개 노드의 가격·재화·선행 조건·효과·유물 효과율과 보스 보상 5단계가 있다.
entities는 이름·콘셉트·외형 키, fieldHelp는 필드 설명, implementation은 실제 구현 상태다.
미구현 보스 HP/패턴을 임의의 완성 데이터로 꾸미지 않았다.

### 수치와 저장

- 기본 스탯은 information.stats, 스킬 기본 피해/반경/쿨타임은 information.skills에서 수정한다.
- 노드 operation: baseline=파일 기본값, set=절대값, multiply=배율, enable=활성화.
- 반경은 월드 단위이며 지름이나 픽셀이 아니다.
- 몬스터·스킬 최초 활성화, 생산량, 유물 시작 레벨은 information이 기준이다.
  젬 enabled는 활성 몬스터 공급원 기준으로 다시 계산한다.
- 스킬 피해는 기본 피해에 강화 배율·공격력 성장 비율을 곱하고 한 번만 반올림한다.
  progression.scaleSkillDamageWithAttack=false이면 공격력 성장 연동을 끈다.
- savedProgressPolicy=overlay(기본)는 기존 지갑·구매 기록을 적용한다.
  저장된 강화가 기본 스탯을 덮어쓸 수 있다.
- fileOnly는 JSON 상태로 시작하며 기존 저장을 읽거나 쓰지 않는다.
  세션 구매·보상은 메모리에만 적용한다. 저장 삭제/초기화가 아니다.
- 실행 중 JSON 핫리로드나 원본 파일 덮어쓰기는 하지 않는다. 다음 초기화부터 변경을 반영한다.
- 노드는 선행 노드가 먼저여야 한다. ID 변경은 저장 이관이 필요하다.
- prefabKey는 등록된 MonsterDefinition과 일치해야 한다.
- 전체 생존 상한은 최대 80, 일반 필드는 최대 30초다.
- 이전 balance-v2.json / GameBalanceDefinition은 과거 참고이며 런타임 입력이 아니다.

### 해설 갱신 및 검증

- python3 tools/build_korean_game_data_reference.py: 해설이 영어 원본 전체와 일치하는지 검사.
- python3 tools/build_korean_game_data_reference.py --patch: 재생성용 apply_patch 패치를 출력.
  이를 apply_patch에 전달하면 docs의 해설만 갱신한다. 영어 원본은 건드리지 않는다.
- 새 키/표시 문구의 한국어 뜻은 위 도구의 LABELS / EXPLANATIONS / TRANSLATIONS에서 관리한다.
- python3 tools/validate_bilingual_game_data.py: 영어 실행 원본 + 한국어 설명서 + 스키마/참조 검사.
- 기존 게임 정보 검사와 모듈 경계/GUID/C# 구문 검사도 수행한다.
- Unity 컴파일·Editor 테스트·게임 실행은 하지 않는다.

전투·성장 지침에 따라 데이터 로드는 Data/Progression/App에 두고 Combat은 공통 스냅샷만 읽는다.

---

이 문서의 아래 본문은 최초 개편 기록이다. 실제 입력 파일은 위 영어 JSON 하나를 사용한다. 이전 v0.0.4 문서의
다중 클릭, 펫 탭, 젬 해금 트리, Monster20, 일반 필드 60초 규칙보다 우선한다.
게임 버전명을 새로 확정한 것은 아니며 JSON schemaVersion과 balanceVersion은 2다.

## 적용 항목

| 요청 | 구현 |
|---|---|
| 전체 정보 JSON | `GameInformation` DTO + 검증 + 직렬화/역직렬화. 전체 정보와 전투가 같은 JSON 사용 |
| 노드형 트리 | 실제 선행 조건 연결선, 상태별 테두리, 양방향 드래그, −/+ /1:1 확대·축소 |
| 펫 → 스킬 | 별도 펫 탭 제거, `skill.cursorAura`로 전환. 기존 펫 구매 이관 |
| 몬스터 10종 | 아래 10종 콘셉트·프리팹·아이콘 연결, 다중 종 스폰 |
| 전리품 → 유물 | 일반 10 + 보스 5 유물, 각 5레벨. 일반 유물 레벨당 공격력 2%, 보스 유물 레벨당 보스 피해 5% |
| Text → TextMeshPro | 프로젝트 소유 런타임 UI 전환, 한글 동적 SDF 공유, 다중 아틀라스, 로케일 키 연결 |
| 다중 클릭 제거 | 커서 영역 자동 1회 타격 + 쿨타임 강화. 마우스 클릭이 추가 기본 피해를 발생시키지 않음 |
| 젬 해금 제거 | 일반 몬스터를 해금하면 그 몬스터가 해당 젬을 공급. 별도 젬 구매/발견 가중치 없음 |
| 일반 필드 최대 30초 | 15/20/25/30초. JSON 검증·스냅샷·런 요청·App 경계 적용. 보스 60초 유지 |
| 해상도/화질 | 1920×1080 레이아웃 기준 + 네이티브 디스플레이 출력, Retina 유지, SDF 텍스트, 웹 기본 해상도 1920×1080 |

수치 조정 원본은 `Assets/CursorHunter/Data/Resources/GameData/game-data.en.json` 하나다.
공급사 에셋은 변경하지 않고 게임 전용 MonsterDefinition에서 참조한다.
씬/프리팹에 있는 기존 전투 수치 필드는 프로토타입 호환용이며,
JSON v2 일반 전투에서는 JSON에서 읽은 값이 우선한다.

## 데이터 흐름

1. Progression이 구매 기록·지갑·정적 밸런스를 합쳐 최종 `GameInformation`을 만든다.
2. `CreateGameInformationJson()`이 영문 키 JSON을 생성한다.
3. App/HuntManager가 `GameInformationJson.TryDeserialize`로 JSON을 읽고 유효성을 확인한다.
4. 검증된 DTO를 복사된 `ProgressionCombatSnapshot`으로 변환한다.
5. Combat은 스탯·몬스터·스킬 값을 이 스냅샷에서만 읽는다.
6. 전체 정보 팝업은 해당 런의 `SourceJson`을 다시 읽어 Key/Value를 표시한다.
7. JSON 복사 버튼으로 동일한 JSON을 클립보드에 복사할 수 있다.

전투 시작 이후 프로필이 바뀌어도 진행 중인 전투 값은 바뀌지 않는다.
런 중/결과창의 젬 수량은 시작 시점 스냅샷이며 팝업에 이를 표시한다.
전체 정보 팝업과 포커스 상실 중에는 자동 공격·스폰·시계·스킬 쿨타임을 정지한다.
매 프레임 JSON 파싱, 파일 저장, 전체 프로필 복사는 하지 않는다.

## JSON 구조와 단위

전체 예제: [game-information-v2.example.json](game-information-v2.example.json)
정식 스키마: [game-information-v2.schema.json](game-information-v2.schema.json)

```json
{
  "schemaVersion": 2,
  "balanceVersion": 2,
  "stats": {
    "attackPower": 10,
    "attackRadiusWorldUnits": 5,
    "attackCooldownSeconds": 0.5,
    "criticalChancePercent": 15,
    "bossDamageMultiplier": 1,
    "normalFieldDurationSeconds": 30
  },
  "gemstones": [
    { "id": "gem.garnet", "enabled": true, "amount": 123 },
    { "id": "gem.topaz", "enabled": false, "amount": 123 }
  ],
  "monsters": [
    {
      "id": "monster.01",
      "enabled": true,
      "productionCount": 2,
      "hitPoints": 8,
      "spawnIntervalSeconds": 1.2,
      "garnetReward": 3,
      "gemstoneId": "gem.garnet",
      "gemstoneAmount": 0,
      "gemstoneChancePercent": 0,
      "relicFragmentId": "fragment.relic.monster.01",
      "relicFragmentChancePercent": 3
    }
  ],
  "skills": [
    {
      "id": "skill.fireball",
      "enabled": true,
      "damage": 40,
      "radiusWorldUnits": 1.5,
      "cooldownSeconds": 4.5
    }
  ],
  "relics": [
    { "id": "relic.monster.01", "enabled": false, "level": 0, "fragments": 0 }
  ]
}
```

위 블록은 구조 설명을 위한 발췌다. 실제 입력은 보석 6개·몬스터 10개·스킬 7개를
모두 포함하며 고정 ID 순서를 사용한다. false인 항목도 삭제하지 않는다.
배열 순서가 ID를 대신하지 않으며 ID를 함께 검증한다.

- `amount`, `attackPower`, `hitPoints`, `damage`, `fragments`는 Int64.
- `enabled`는 boolean, 숫자는 문자열로 저장하지 않는다.
- `criticalChancePercent`는 0~100 백분율.
- `attackRadiusWorldUnits`와 `radiusWorldUnits`는 월드 반경이며 지름/픽셀/배율이 아니다.
- 현재 씬 기본 반경은 1.7253809 월드 단위. App은 기존 커서 크기와 호환되는 배율로 변환한다.
- `productionCount`는 스폰 기회 1회에 생성하는 정수 마릿수(현재 1~4).
- `gemstones.enabled`는 현재 등장 가능한 일반 몬스터 공급원 유무다.
  이전 보유 잔액과 독립적이므로 false이면서 amount가 양수일 수 있다.
  잔액은 보존·표시한다. 다만 현재 UpgradeSelected에는 currencyUnlocked 공급원 검사가
  남아 있어 비활성 젬 잔액의 소비를 막을 수 있다. 소비와 공급 조건의 완전 분리는 후속 수정 대상이다.
- 가넷은 기본 공급원으로 항상 사용 가능하다.
- 같은 값을 화면용 문자열과 전투용 숫자로 이중 관리하지 않는다.

## 몬스터 10종

| ID | 콘셉트 이름 | 공급 젬 | 프리팹 |
|---|---|---|---|
| monster.01 | 가넷 그루터기 | gem.garnet | Walker/Walker_Stump |
| monster.02 | 토파즈 동굴박쥐 | gem.topaz | Bat/Bat_Cave |
| monster.03 | 호박 버섯 | gem.topaz | Walker/Walker_Mushroom |
| monster.04 | 자수정 가면유령 | gem.amethyst | Ghost/Ghost_Mask |
| monster.05 | 수정 전갈 | gem.amethyst | Scorpion/Scorpion_Purple |
| monster.06 | 사파이어 눈요정 | gem.sapphire | Fairy/Fairy_Snow |
| monster.07 | 빙결 거미 | gem.sapphire | Spider/Spider_Frozen |
| monster.08 | 다이아 철골렘 | gem.diamond | Golem/Golem_Iron |
| monster.09 | 드래곤 불꽃봉오리 | gem.dragon | Bud/Bud_Fire |
| monster.10 | 고대 해골군주 | gem.dragon | Skeleton/Skeleton_Warlord |

일반 몬스터는 공격하지 않는 수확 대상이며, 위 콘셉트는 외형·재화 역할을 정의한다.
종별 이동/특수 공격을 새로 구현한 것은 아니다.
기본종은 무료, 다음 종은 앞 종에서 획득 가능한 재화로 순차 구매한다.
자기 자신이 드롭하는 젬으로 자기 해금을 결제하는 순환 잠금을 만들지 않는다.
모든 해금 종은 누적 등장하며 종별 주기와 생산량을 각각 적용한다.
전체 동시 생존 수는 80이며 프레임 누락분을 한꺼번에 스폰하지 않는다.
가넷은 모든 종의 기본 보상, 나머지 젬은 지정 몬스터 처치 시 현재 초안으로 1개 확정 지급이다.
일반 유물 조각은 종별 3% 확률이다. 수치는 실행 밸런스 검증 전 초안이다.

## 자동 공격과 스킬

기본 공격은 커서를 몬스터에 대면 반경 내 모든 유효 대상을 쿨타임마다 한 번 공격한다.
쿨타임은 0.8 → 0.65 → 0.5 → 0.35 → 0.2초다.
기존 `HitsPerBundle` 인자/프로퍼티는 호출 호환만 유지하고 실제 값은 항상 1이다.
수동 클릭에 의한 추가 기본 타격 경로는 없다.

스킬 7종은 파이어볼·라이트닝·프리즈닝·허리케인·메테오·드래곤 브레스·커서 오라다.
JSON에는 배율만이 아닌 최종 피해·반경·쿨타임이 들어간다.
현재 공통 실행기는 각 스킬 쿨타임에 커서 중심 원형 피해를 적용한다.
허리케인은 6×6의 합산 피해를 사용한다. 별도 투사체, 빙결 상태, 틱 연출, 브레스 방향 판정은
기존에도 미완성이었던 후속 Combat 항목이며 이번 공통 반경 실행기가 그 연출 완성을 뜻하지 않는다.
패링은 자동 공격/스킬에 연결하지 않는다.

펫은 개체를 소환하지 않는 `skill.cursorAura` 스킬로 해석했다.
스킬의 피해/반경/쿨타임 강화는 해금 노드에서 각각 분기한다.
스킬·특성 수치는 JSON에서 바꿀 수 있지만 노드 개수/ID 변경 시 마이그레이션이 필요하다.

## 유물과 저장 이관

저장 키는 기존 `cursor_hunter.progression.v1`을 유지하고 내부 version은 2로 저장한다.

- 다중 클릭 구매 ID → 같은 단계 쿨타임 구매 ID.
- 펫 해금 → 커서 오라, 펫 공격/영역/주기 → 오라 피해/반경/쿨타임.
- 같은 오라 노드로 합쳐지는 추가 구매분은 원래 결제 재화로 환불한다.
- 전리품 ID/조각 → `relic.*` / `fragment.relic.*`.
- 11~20번 일반 전리품·조각은 10번 일반 유물에 합친다.
  같은 레벨 구매가 중복되면 추가 사용 조각을 환불한다.
- 삭제된 젬 해금/발견 노드·11~20번 몬스터·30초 초과 시간 노드는 기록된 사용 비용을 환불한다.
- 초기화 환불은 기록된 원래 비용/재화로 수행하며 재화 덧셈은 포화 연산을 쓴다.
- 게임을 실행하지 않았으므로 사용자 PlayerPrefs와 저장 데이터는 이번 작업에서 변경되지 않았다.

PlayerPrefs 저장소와 프로세스 내 RunId 정산 중복 방지는 기존 구조다.
영속 정산 journal·파일 백업·클라우드 저장을 구현했다고 해석하지 않는다.

## 텍스트와 해상도

프로젝트 소유 런타임 UI는 TextMeshProUGUI로 통일했다.
기존 씬의 TMP도 공용 한글 동적 SDF 폰트를 사용한다. 공급사 원본은 변경하지 않았다.
공유 FontAsset 캐시로 글자별 폰트 생성과 메모리 중복을 피하고,
동적 다중 아틀라스로 한글/영문 글리프를 공급한다.

`LocalizedTmpLabel`은 로케일 변경 구독을 활성 수명에 맞춰 해제한다.
`LocalizationCatalog.SetLanguage("ko" / "en")`로 기본 UI 문구를 바꿀 수 있다.
`GameData/ui-localization.json`은 기본 탭/버튼/제목 키를 제공한다.
노드 설명·조합형 문장 전체의 영문 번역 및 설정 화면 언어 선택은 이번에 완료했다고 주장하지 않는다.
미등록 문구는 기존 한국어로 표시한다.

빌드에서 네이티브 해상도의 테두리 없는 전체화면을 사용한다. Unity Editor 해상도는 강제로 바꾸지 않는다.
URP renderScale 1, MSAA 4, 전체 해상도 mip, Retina, 1920×1080 Canvas 기준을 유지한다.
필터링과 UI SDF 품질을 적용했으며 원본 저해상도 그림 자체를 새로 제작한 것은 아니다.

## 검증

- `python3 tools/validate_collaboration.py`
- `python3 tools/validate_game_information.py`
- 선택적 구문 검사: tree-sitter / tree-sitter-c-sharp 설치 후 위 명령에 `--syntax`
- `git diff --check`
- 추가한 Editor 테스트: `Assets/CursorHunter/App/Tests/GameInformationTests.cs`

정적 검사는 JSON 필드/ID, 10종·7종 개수, 재화 접근 경로, 프리팹 GUID/fileID,
TMP 전환, 씬 테스트 플래그, 렌더 설정과 C# 구문을 검사한다.
Unity Import, 실제 Unity C# 컴파일, Editor 테스트 실행, Play Mode, 게임 실행은 수행하지 않았다.
사용자가 요청한 실행 금지를 준수했다. 시각 배치·전투 난이도는 실제 실행 검증 전 상태다.
