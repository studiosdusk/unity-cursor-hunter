# 런과 영구 데이터의 연결 계약 v0
구현 전에 두 담당자가 이 명세를 기준으로 Contracts/Runtime에 C# 타입을 추가한다. 아래 이름은 새로 구현할 계약 이름이다.

## 데이터 경계
| 계약 | 필수 데이터 | 생산 → 소비 |
|---|---|---|
| RunRequest | runId, schemaVersion, balanceVersion, 모드, bossId(일반은 없음), seed, durationSeconds=60 | App → Combat |
| CombatSnapshot | attack(long), radiusPixels, hitsPerBundle, autoBundlesPerSecond, 자동 ON, 스킬별 계수/주기 | Progression → App → Combat |
| SpawnSnapshot | monsterId, HP(long), 주기, 무리 수, 종별 상한/전체 상한, 보상표 | Data+Progression → App → Combat |
| RunProgress | runId, sequence, 경과시간, 종별 누적 처치, 유효 피해(long), 파훼 성공/실패, 남은 시간 | Combat → App |
| RunResult | runId, 종료 사유, snapshot 버전, 종별 처치, 경과시간, 피해, 보스 성공, 파훼 수 | Combat → App → Progression |
| SettlementReceipt | runId, 지급 재화, 첫 처치 여부, 새 해금, 저장 성공/오류 | Progression → App |
배열/컬렉션은 복사한 읽기 전용 스냅샷으로 넘긴다. ScriptableObject, GameObject, 저장 파일 경로를 계약에 넣지 않는다.
초기 공개 동작은 Start(request, snapshot), Pause, Resume, Abort와 Progress/Completed 알림으로 제한한다. 실제 메서드 이름/이벤트 형태는 첫 계약 PR에서 확정한다.

## 정산 권한과 복구
1. App이 고유 runId와 수치 버전을 발급하고 저장한 뒤 전투를 시작한다. 런 중 특성 구매는 허용하지 않는다.
2. Combat은 피해·처치 사실을 집계한다. VFX나 UI에서 재화·프로필을 변경하지 않는다.
3. 일반 처치 즉시 런 누적 보상을 증가시키고 Progress를 전달한다. App/Progression은 sequence로 중복을 제외하고 복구용 누적 기록을 보관한다. 시각 흡수 애니메이션과 지급 사실은 독립적이다.
4. 영구 지갑 반영은 정산 트랜잭션 한 번으로 수행한다. 런 중 보유 표시는 영구 지갑+해당 런 미정산 금액이다. 중도 종료/복구도 같은 runId로 처리한다.
5. 보스는 성공 시에만 보상. 실패/포기는 0이며 파훼 보너스도 확정하지 않는다.
6. 최초 보스 처치 플래그·젬 해금·보상·처리한 runId를 하나의 저장 단위로 확정한다. 새 젬 슬롯을 만든 뒤 같은 처치의 기본/파훼 보상을 적용한다.
7. 저장 실패에서는 성공 영수증을 내보내지 않고 같은 runId로 재시도한다. 저장 성공을 확인한 App만 다음 런으로 이동한다.
8. 저장 큐는 순차 실행. 취소·앱 종료·복구 중 중복 정산에 대해 검증한다. 미저장 구간의 복구 허용 범위는 실제 저장 구현 때 명시한다.

## 계산·입력 불변 조건
- HP·피해·재화는 Int64. v5 HP 2,700,000,000은 Int32 범위를 넘는다. 합계·곱셈에는 overflow 방어.
- 반경 단위는 1920×1080 기준 픽셀. 월드 좌표 변환은 Combat의 책임. UI 배율과 타격 판정을 분리한다.
- 자동 10~30묶음/초, 다중 1~3타. 자동 중 수동 기본 피해 중복 없음. 병렬 타격은 동시 논리 타격이며 스레드 요구가 아니다.
- t=60 생성/타격 없음. 만료 이전 HP=0이면 성공, 만료 시각에 처음 HP=0이 되는 입력은 무효.
- 과잉 피해와 정지 시간은 DPS에서 제외. 0초 경과 DPS는 0. 스킬 피해 포함.
- 보스 파훼는 수동 입력. 스킬/자동 클릭이 대신 누르지 않는다.
- 앱 포커스·Overlay·ESC 시 전투 시계와 스킬/파훼 모두 정지. 재개는 새 입력.
- 이벤트 구독은 화면/런 수명과 함께 해제. 오래된 runId 콜백은 무시. Unity 오브젝트는 메인 스레드에서만 접근.
- 매 타격마다 LINQ·전체 프로필 복사·파일 저장 금지. Combat은 풀링과 누적 집계, UI는 별도 갱신 주기를 사용한다.
- 일반 종은 해금 이후 누적. 젬 UI는 해금된 것만. Main에 지갑 없음.
- 새 게임 가넷 → v1 최초 처치 토파즈 → v2 자수정 → v3 사파이어 → v4 다이아몬드.

## 첫 합류에서 확인할 사례
가넷 1종 / 특성 구매 후 다음 런만 변경 / 일반 포기 처치 보상 / 보스 실패 0 / 같은 runId 2회 수신 / 저장 실패 재시도 / 종료 직전 마지막 타격 / 화면 이탈 후 이벤트 / v5 HP / 미해금 젬 숨김.
계약 변경은 생산자·소비자·테스트 더블을 같은 PR에서 갱신한다. 저장 schemaVersion 변경에는 구버전 복구/마이그레이션을 포함한다.
