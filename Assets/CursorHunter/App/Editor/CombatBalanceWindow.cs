using System;
using System.Collections.Generic;
using System.Globalization;
using CursorHunter.Combat;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.App.Editor
{
    /// <summary>Edits transient final values through the App composition root.</summary>
    public sealed class CombatBalanceWindow : EditorWindow
    {
        private const string SessionPrefix = "CursorHunter.CombatBalanceWindow.";
        private static readonly string[] Modes = { "실시간 테스트", "최종 설정 스탯" };
        private static readonly string[] Sections = { "베이스", "커서 공격", "스킬", "몬스터·스폰" };
        private static readonly CultureInfo Numbers = CultureInfo.InvariantCulture;

        [SerializeField] private HuntManager target;
        [SerializeField] private int selectedMode;
        [SerializeField] private int selectedTab;
        [SerializeField] private int selectedSkill;
        [SerializeField] private int selectedMonster;
        [SerializeField] private Vector2 scroll;
        [SerializeField] private string draftJson;
        [SerializeField] private string finalDraftJson;
        [SerializeField] private string finalBaselineJson;
        [SerializeField] private string finalTargetKey;
        [SerializeField] private int finalSection;

        private CombatBalanceSettings _draft;
        private CombatBalanceSettings _actual;
        private CombatBalanceSettings _finalDraft;
        private CombatBalanceSettings _finalBaseline;
        private string _finalStatus;
        private bool _finalStatusIsError;
        private bool _drawingFinal;
        private bool _saveFinalRequested;
        private CombatBalanceSettings DrawingDraft => _drawingFinal ? _finalDraft : _draft;
        private CombatBalanceSettings DrawingActual => _drawingFinal ? _finalBaseline : _actual;
        private string InputScope => _drawingFinal ? "final." : "live.";
        private readonly Dictionary<string, string> _fieldErrors = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _longInputs = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _longErrors = new Dictionary<string, string>();
        private string _applyError;
        private bool _edited;
        private bool _ownsSettings;
        private bool _restoreOnPlay;
        private bool _playTransition;
        private bool _assemblyReloading;
        private double _nextRefresh;

        [MenuItem("Tools/Cursor Hunter/Combat Balance")]
        public static void Open()
        {
            GetWindow<CombatBalanceWindow>("전투 밸런스");
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("전투 밸런스");
            minSize = new Vector2(520f, 380f);
            // Preserve the previously selected final-settings tab after the layout upgrade.
            if (selectedTab == 4)
            {
                selectedMode = 1;
                selectedTab = 0;
            }
            _assemblyReloading = false;
            _draft = Deserialize(draftJson);
            _finalDraft = Deserialize(finalDraftJson);
            _finalBaseline = Deserialize(finalBaselineJson);
            _restoreOnPlay = SessionState.GetBool(SessionPrefix + "Armed", false) &&
                EditorApplication.isPlayingOrWillChangePlaymode;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            ResolveTarget();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            if (_assemblyReloading || _playTransition ||
                EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
                return;
            ClearOwnedSettings();
            ClearSession();
        }

        private void OnDestroy()
        {
            if (_assemblyReloading) return;
            ClearOwnedSettings();
            ClearSession();
        }

        private void OnBeforeAssemblyReload()
        {
            _assemblyReloading = true;
            // Script recompilation cannot safely retain a live transient run.
            // The normal Edit -> Play reload only carries the validated inputs.
            if (EditorApplication.isPlaying && !_playTransition)
            {
                ClearOwnedSettings();
                ClearSession();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _playTransition = true;
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _playTransition = false;
                _restoreOnPlay = SessionState.GetBool(SessionPrefix + "Armed", false);
                _nextRefresh = 0;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _playTransition = true;
                _restoreOnPlay = false;
                ClearOwnedSettings();
                ClearSession();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                _playTransition = false;
                _ownsSettings = false;
                _actual = null;
                _draft = null;
                ClearInputs("live.");
                draftJson = null;
                _applyError = null;
                ClearSession();
                ResolveTarget();
            }
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.isCompiling || _playTransition ||
                EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.25d;
            ResolveTarget();
            if (target == null) return;

            if (_restoreOnPlay && EditorApplication.isPlaying)
            {
                _restoreOnPlay = false;
                CombatBalanceSettings pending = Deserialize(SessionState.GetString(
                    SessionPrefix + "Settings", string.Empty));
                if (pending != null)
                {
                    _draft = pending;
                    ClearInputs("live.");
                    _fieldErrors.Clear();
                    ApplyDraft();
                }
            }
            _actual = target.IsCombatBalanceTestActive
                ? target.AppliedCombatBalanceSettings : null;
            Repaint();
        }

        private void ResolveTarget()
        {
            if (!IsSceneTarget(target))
            {
                // A newly opened scene must not inherit another scene's draft
                // or ownership. Only the normal Play transition carries the
                // last validated settings into the replacement scene object.
                bool preservePending = _restoreOnPlay &&
                    SessionState.GetBool(SessionPrefix + "Armed", false);
                ClearOwnedSettings();
                _draft = null;
                _actual = null;
                draftJson = null;
                ClearInputs("live.");
                _fieldErrors.Clear();
                _applyError = null;
                if (!preservePending)
                {
                    _restoreOnPlay = false;
                    ClearSession();
                }
                target = UnityEngine.Object.FindFirstObjectByType<HuntManager>(
                    FindObjectsInactive.Include);
            }
            if (!IsSceneTarget(target))
            {
                target = null;
                return;
            }
            ObserveFinalTarget();
            if (_draft == null)
            {
                _draft = target.CaptureCombatBalanceSettings();
                SaveDraft();
            }
        }

        private static bool IsSceneTarget(HuntManager value)
        {
            return value != null && !EditorUtility.IsPersistent(value) &&
                value.gameObject.scene.IsValid() && value.gameObject.scene.isLoaded;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            selectedMode = GUILayout.Toolbar(Mathf.Clamp(selectedMode, 0, Modes.Length - 1),
                Modes, GUILayout.Height(28f));
            EditorGUILayout.Space(6);
            HuntManager nextTarget = (HuntManager)EditorGUILayout.ObjectField(
                "전투 대상", target, typeof(HuntManager), true);
            if (nextTarget != target)
            {
                if (nextTarget != null && !IsSceneTarget(nextTarget))
                    _applyError = "열려 있는 씬의 HuntManager를 선택하세요.";
                else
                {
                    ClearOwnedSettings();
                    ClearSession();
                    target = nextTarget;
                    ObserveFinalTarget();
                    _draft = target != null ? target.CaptureCombatBalanceSettings() : null;
                    ClearInputs("live.");
                    _actual = null;
                    _applyError = null;
                    SaveDraft();
                    GUI.FocusControl(null);
                }
            }
            if (target == null || _draft == null)
            {
                EditorGUILayout.HelpBox("Main 씬을 열고 HuntManager를 선택하세요.", MessageType.Info);
                return;
            }

            int section = selectedMode == 1 ? finalSection : selectedTab;
            section = GUILayout.Toolbar(Mathf.Clamp(section, 0, Sections.Length - 1), Sections);
            EditorGUILayout.Space(6);
            if (selectedMode == 1)
            {
                finalSection = section;
                DrawFinalSettings();
                return;
            }
            selectedTab = section;
            _saveFinalRequested = false;

            DrawStatus();
            _drawingFinal = false;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            _fieldErrors.Clear();
            _edited = false;
            DrawColumns();
            DrawSection(selectedTab);
            EditorGUILayout.EndScrollView();

            if (_edited) ApplyDraft();
            if (!string.IsNullOrEmpty(_applyError))
                EditorGUILayout.HelpBox(_applyError, MessageType.Error);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("숫자는 Enter 또는 포커스 해제로 확정 · 토글은 즉시 반영", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("테스트 설정은 다음 전투에도 유지됩니다. 수동 해제·창 닫기·Play 종료·대상 교체·Play 중 스크립트 리로드 시 해제됩니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("실시간 테스트는 원본과 유저 저장을 수정하지 않음", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(!_ownsSettings))
            {
                if (GUILayout.Button(target.IsCombatBalanceTestActive ? "테스트 종료 및 설정 해제" : "테스트 설정 해제"))
                {
                    ClearOwnedSettings();
                    ClearSession();
                    _draft = target.CaptureCombatBalanceSettings();
                    ClearInputs("live.");
                    _actual = null;
                    _applyError = null;
                    SaveDraft();
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawSection(int section)
        {
            switch (section)
            {
                case 0: DrawBase(); break;
                case 1: DrawCursor(); break;
                case 2: DrawSkills(); break;
                case 3: DrawMonsters(); break;
            }
        }

        private void DrawFinalSettings()
        {
            if (_finalDraft == null)
            {
                _finalDraft = _draft.Clone();
                SaveFinalDraftJson();
            }
            if (_finalBaseline == null) CaptureSavedBaseline(false);
            EditorGUILayout.HelpBox("최종안은 전투에 실시간 적용되지 않습니다. 저장 버튼으로 게임 기본 수치를 변경합니다.", MessageType.Info);
            EditorGUILayout.LabelField("스킬·몬스터 사용 여부, 구매·해금, Stage 편성은 저장 대상에서 제외합니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(target.IsTraitScreenConnectedToCombat
                ? "저장한 값은 강화 전 기본값입니다. 일반 전투에는 기존 강화와 Stage의 생성 수 설정이 추가로 반영됩니다."
                : "특성 연동이 해제되어 있습니다. 일반 전투에는 저장한 기본값과 Stage의 생성 수 설정을 적용합니다.",
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 테스트값 가져오기"))
                {
                    CombatBalanceSettings current = target.IsCombatBalanceTestActive
                        ? target.AppliedCombatBalanceSettings : _draft;
                    if (current != null)
                    {
                        _finalDraft = current.Clone();
                        ClearInputs("final.");
                        SaveFinalDraftJson();
                        _finalStatus = "현재 테스트값을 최종안으로 가져왔습니다.";
                        _finalStatusIsError = false;
                        GUI.FocusControl(null);
                    }
                }
                if (GUILayout.Button("저장된 기본값 불러오기"))
                {
                    CaptureSavedBaseline(true);
                    GUI.FocusControl(null);
                }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            _drawingFinal = true;
            _fieldErrors.Clear();
            _edited = false;
            DrawColumns();
            DrawSection(finalSection);
            EditorGUILayout.EndScrollView();
            if (_edited)
            {
                SaveFinalDraftJson();
                _finalStatus = null;
            }
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("최종안은 Play 종료와 스크립트 리로드 후에도 유지됩니다. 다른 전투 대상으로 바꾸면 초기화됩니다.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Save · 게임 기본값 저장", GUILayout.Height(28f)))
            {
                // Give delayed numeric controls one complete GUI pass to commit.
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                _saveFinalRequested = true;
                Repaint();
            }
            if (_saveFinalRequested && Event.current.type == EventType.Repaint)
            {
                _saveFinalRequested = false;
                SaveFinalSettings();
            }
            if (!string.IsNullOrEmpty(_finalStatus))
                EditorGUILayout.HelpBox(_finalStatus, _finalStatusIsError ? MessageType.Error : MessageType.Info);
            _drawingFinal = false;
        }

        private bool CaptureSavedBaseline(bool replaceDraft)
        {
            if (!CombatBalanceBaseStore.TryCapture(target, out CombatBalanceSettings saved, out string error))
            {
                _finalStatus = error;
                _finalStatusIsError = true;
                return false;
            }
            _finalBaseline = saved.Clone();
            finalBaselineJson = JsonUtility.ToJson(_finalBaseline);
            if (replaceDraft)
            {
                _finalDraft = saved.Clone();
                ClearInputs("final.");
                SaveFinalDraftJson();
                _finalStatus = "저장된 기본값을 불러왔습니다.";
                _finalStatusIsError = false;
            }
            return true;
        }

        private void SaveFinalSettings()
        {
            SaveFinalDraftJson();
            _finalStatusIsError = true;
            foreach (var error in _longErrors)
                if (error.Key.StartsWith("final.", StringComparison.Ordinal))
                {
                    _finalStatus = error.Value;
                    return;
                }
            foreach (var error in _fieldErrors)
                if (error.Key.StartsWith("final.", StringComparison.Ordinal))
                {
                    _finalStatus = error.Value;
                    return;
                }
            string failure = string.Empty;
            if (_finalDraft == null || target == null ||
                !CombatBalanceBaseStore.TrySave(target, _finalDraft.Clone(), out failure))
            {
                _finalStatus = _finalDraft == null || target == null
                    ? "저장할 최종안과 전투 대상을 확인하세요." : failure;
                return;
            }
            if (!CaptureSavedBaseline(false)) return;
            _finalStatus = "게임 기본 수치를 저장했습니다. 테스트 설정을 해제하면 저장한 기본값을 사용합니다.";
            _finalStatusIsError = false;
        }

        private void SaveFinalDraftJson() =>
            finalDraftJson = _finalDraft == null ? null : JsonUtility.ToJson(_finalDraft);

        private void ObserveFinalTarget()
        {
            if (!IsSceneTarget(target)) return;
            string key = target.gameObject.scene.path;
            if (string.IsNullOrEmpty(key)) key = "unsaved:" + target.gameObject.scene.name;
            for (Transform node = target.transform; node != null; node = node.parent)
                key += "/" + node.name + "[" + node.GetSiblingIndex() + "]";
            if (!string.IsNullOrEmpty(finalTargetKey) && finalTargetKey != key)
            {
                _finalDraft = null;
                _finalBaseline = null;
                finalDraftJson = null;
                finalBaselineJson = null;
                _finalStatus = null;
                _saveFinalRequested = false;
                ClearInputs("final.");
            }
            finalTargetKey = key;
        }

        private void ClearInputs(string prefix)
        {
            var keys = new List<string>(_longInputs.Keys);
            foreach (string key in keys)
                if (key.StartsWith(prefix, StringComparison.Ordinal)) _longInputs.Remove(key);
            keys = new List<string>(_longErrors.Keys);
            foreach (string key in keys)
                if (key.StartsWith(prefix, StringComparison.Ordinal)) _longErrors.Remove(key);
        }

        private void DrawStatus()
        {
            string status = !string.IsNullOrEmpty(_applyError)
                ? "입력 오류 · 직전 값 유지"
                : target.IsCombatBalanceTestActive ? "현재 전투에 적용됨 · 다음 전투에도 적용"
                : target.HasCombatBalanceSettings ? "설정 유지 중 · 다음 전투에도 적용"
                : "값을 수정하면 현재·다음 전투에 적용";
            EditorGUILayout.HelpBox(status, !string.IsNullOrEmpty(_applyError)
                ? MessageType.Error : MessageType.Info);
        }

        private float NameWidth => Mathf.Clamp(position.width * 0.42f, 165f, 250f);
        private const float ActualWidth = 110f;

        private void DrawColumns()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("설정 항목", EditorStyles.boldLabel, GUILayout.Width(NameWidth));
            GUILayout.Label(_drawingFinal ? "최종안" : "입력값", EditorStyles.boldLabel);
            GUILayout.Label(_drawingFinal ? "저장된 기본값" : "실제 적용값", EditorStyles.boldLabel, GUILayout.Width(ActualWidth));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBase()
        {
            DrawingDraft.AttackPower = LongRow("공격력", DrawingDraft.AttackPower, DrawingActual?.AttackPower, 1L);
            DrawingDraft.CriticalChancePercent = FloatRow("치명타 확률 (%)", DrawingDraft.CriticalChancePercent, DrawingActual?.CriticalChancePercent, 0f, 100f);
            DrawingDraft.CriticalDamageMultiplier = FloatRow("치명타 피해 배율 (배)", DrawingDraft.CriticalDamageMultiplier, DrawingActual?.CriticalDamageMultiplier, 1f);
            DrawingDraft.BossDamageMultiplier = FloatRow("보스 피해 배율 (배)", DrawingDraft.BossDamageMultiplier, DrawingActual?.BossDamageMultiplier, float.Epsilon);
            DrawingDraft.DurationSeconds = FloatRow("일반 전투 시간 (초)", DrawingDraft.DurationSeconds, DrawingActual?.DurationSeconds, 15f, 30f);
        }

        private void DrawCursor()
        {
            const float baseRadius = CursorHunter.Contracts.GameInformation.BaseAttackRadiusWorldUnits;
            float rangeMultiplier = DrawingDraft.AttackRadiusWorldUnits / baseRadius;
            float? actualMultiplier = DrawingActual == null
                ? (float?)null : DrawingActual.AttackRadiusWorldUnits / baseRadius;
            float nextMultiplier = FloatRow("커서 범위 (배)", rangeMultiplier, actualMultiplier,
                0.01f, step: 0.1f);
            // Keep the fixed reference even after Save changes the authored defaults.
            // Avoid round-trip drift when only repainting or editing another field.
            if (!nextMultiplier.Equals(rangeMultiplier))
                DrawingDraft.AttackRadiusWorldUnits = nextMultiplier * baseRadius;
            EditorGUILayout.LabelField("1 = 기본 크기 · 2 = 2배 · − / + 버튼으로 0.1배씩 조절", EditorStyles.wordWrappedMiniLabel);
            DrawingDraft.AttackCooldownSeconds = FloatRow("같은 대상 재타격 간격 (초)", DrawingDraft.AttackCooldownSeconds, DrawingActual?.AttackCooldownSeconds, 0.05f);
            DrawingDraft.AutoAttackEnabled = BoolRow("자동 공격 사용", DrawingDraft.AutoAttackEnabled, DrawingActual?.AutoAttackEnabled);
        }

        private void DrawSkills()
        {
            if (DrawingDraft.Skills == null || DrawingDraft.Skills.Count == 0)
            {
                EditorGUILayout.HelpBox("선택한 전투에 연결된 스킬이 없습니다.", MessageType.Info);
                return;
            }
            string[] options = DrawingDraft.Skills.ConvertAll(skill => SkillName(skill.SkillId)).ToArray();
            selectedSkill = EditorGUILayout.Popup("스킬 선택", Mathf.Clamp(selectedSkill, 0, options.Length - 1), options);
            SkillBalanceSettings selected = DrawingDraft.Skills[selectedSkill];
            SkillBalanceSettings actual = DrawingActual?.Skills?.Find(skill => skill.SkillId == selected.SkillId);
            EditorGUILayout.LabelField(selected.SkillId, EditorStyles.miniLabel);
            if (!_drawingFinal) selected.Enabled = BoolRow("스킬 사용", selected.Enabled, actual?.Enabled);
            if (selected.Values == null) return;
            foreach (SkillBalanceValue value in selected.Values)
            {
                SkillBalanceValue applied = actual?.Values?.Find(candidate => candidate.Id == value.Id);
                string label = SkillValueName(value.Id);
                if (value.WholeNumber)
                    value.Value = IntRow(label, Mathf.RoundToInt(value.Value),
                        applied == null ? (int?)null : Mathf.RoundToInt(applied.Value),
                        Mathf.CeilToInt(value.Minimum), value.Maximum >= int.MaxValue ? int.MaxValue : Mathf.FloorToInt(value.Maximum));
                else
                    value.Value = FloatRow(label, value.Value, applied?.Value, value.Minimum, value.Maximum);
            }
            if (selected.SkillId == HeadshotSkillController.SkillId)
            {
                DamageRow("주변 기본 타격 피해", selected, actual, "splashDamageMultiplier");
                DamageRow("보스 대상 기본 피해", selected, actual, "bossDamageMultiplier");
                EditorGUILayout.LabelField("일반 몬스터 주 대상 피해는 해당 대상의 현재 HP를 기준으로 계산합니다.", EditorStyles.wordWrappedMiniLabel);
            }
            else DamageRow("기본 타격 피해", selected, actual, "damageMultiplier");
            EditorGUILayout.LabelField("파생 피해는 공격력 × 스킬 배율이며, 치명타와 공통 보스 피해 배율은 타격 시 추가 적용합니다.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawMonsters()
        {
            DrawingDraft.GlobalAliveLimit = IntRow("전체 생존 상한 (마리)", DrawingDraft.GlobalAliveLimit, DrawingActual?.GlobalAliveLimit, 1, 80);
            DrawingDraft.PerMonsterAliveLimit = IntRow("종별 공통 생존 상한 (마리)", DrawingDraft.PerMonsterAliveLimit, DrawingActual?.PerMonsterAliveLimit, 1, Math.Max(1, DrawingDraft.GlobalAliveLimit));
            EditorGUILayout.Space(6);
            if (DrawingDraft.Monsters == null || DrawingDraft.Monsters.Count == 0)
            {
                EditorGUILayout.HelpBox("선택한 전투에 설정할 몬스터가 없습니다.", MessageType.Info);
                return;
            }
            string[] options = DrawingDraft.Monsters.ConvertAll(monster => MonsterName(monster.MonsterId)).ToArray();
            selectedMonster = EditorGUILayout.Popup("몬스터 선택", Mathf.Clamp(selectedMonster, 0, options.Length - 1), options);
            MonsterBalanceSettings selected = DrawingDraft.Monsters[selectedMonster];
            MonsterBalanceSettings actual = DrawingActual?.Monsters?.Find(monster => monster.MonsterId == selected.MonsterId);
            EditorGUILayout.LabelField(selected.MonsterId, EditorStyles.miniLabel);
            if (!_drawingFinal) selected.Enabled = BoolRow("생성 사용", selected.Enabled, actual?.Enabled);
            selected.MaxHealth = LongRow("최대 HP", selected.MaxHealth, actual?.MaxHealth, 1L, selected.MonsterId);
            selected.SpawnIntervalSeconds = FloatRow("생성 간격 (초)", selected.SpawnIntervalSeconds, actual?.SpawnIntervalSeconds, 0.1f);
            selected.PackSize = IntRow("1회 생성 수 (마리)", selected.PackSize, actual?.PackSize, 1);
            selected.MoveSpeed = FloatRow("이동 속도 (월드 단위/초)", selected.MoveSpeed, actual?.MoveSpeed, float.Epsilon);
            selected.VisualScale = FloatRow("시각 크기 (배)", selected.VisualScale, actual?.VisualScale, float.Epsilon);
            selected.HitAreaWidth = FloatRow("피격 영역 너비 (월드 단위)", selected.HitAreaWidth, actual?.HitAreaWidth, float.Epsilon);
            selected.HitAreaHeight = FloatRow("피격 영역 높이 (월드 단위)", selected.HitAreaHeight, actual?.HitAreaHeight, float.Epsilon);
            selected.GarnetReward = LongRow("가넷 보상 (개)", selected.GarnetReward, actual?.GarnetReward, 0L, selected.MonsterId);
            selected.BonusDropCurrencyId = StringRow("추가 보상 재화 ID", selected.BonusDropCurrencyId, actual?.BonusDropCurrencyId);
            selected.BonusDropAmount = LongRow("추가 보상 수량 (개)", selected.BonusDropAmount, actual?.BonusDropAmount, 0L, selected.MonsterId);
            selected.BonusDropChancePercent = FloatRow("추가 보상 확률 (%)", selected.BonusDropChancePercent, actual?.BonusDropChancePercent, 0f, 100f);
            if (selected.BonusDropAmount > 0L && (string.IsNullOrWhiteSpace(selected.BonusDropCurrencyId) || selected.BonusDropChancePercent <= 0f))
                FieldError("추가 보상", "수량이 있으면 재화 ID와 0%보다 큰 확률을 입력하세요.");
        }

        private long LongRow(string name, long value, long? actual, long minimum, string scope = "base")
        {
            string key = InputScope + scope + "." + name;
            if (!_longInputs.TryGetValue(key, out string input)) input = value.ToString(Numbers);
            BeginRow(name);
            string nextInput = EditorGUILayout.DelayedTextField(input);
            EndRow(actual?.ToString(Numbers));
            _edited |= nextInput != input;
            _longInputs[key] = nextInput;
            if (!long.TryParse(nextInput, NumberStyles.Integer, Numbers, out long next) || next < minimum)
            {
                string message = minimum.ToString(Numbers) + " 이상, " + long.MaxValue.ToString(Numbers) + " 이하의 정수를 입력하세요.";
                _longErrors[key] = name + ": " + message;
                FieldError(name, message);
                return value;
            }
            _longErrors.Remove(key);
            return next;
        }

        private int IntRow(string name, int value, int? actual, int minimum, int maximum = int.MaxValue)
        {
            BeginRow(name);
            int next = EditorGUILayout.DelayedIntField(value);
            EndRow(actual?.ToString(Numbers));
            _edited |= next != value;
            if (next < minimum || next > maximum)
                FieldError(name, maximum == int.MaxValue ? minimum + " 이상을 입력하세요." : minimum + "~" + maximum + " 범위로 입력하세요.");
            return next;
        }

        private float FloatRow(string name, float value, float? actual, float minimum,
            float maximum = float.MaxValue, float step = 0f)
        {
            BeginRow(name);
            float next = EditorGUILayout.DelayedFloatField(value);
            if (step > 0f)
            {
                float delta = 0f;
                if (GUILayout.Button(new GUIContent("−", Format(step) + "만큼 줄이기"), GUILayout.Width(24f)))
                    delta = -step;
                if (GUILayout.Button(new GUIContent("+", Format(step) + "만큼 늘리기"), GUILayout.Width(24f)))
                    delta = step;
                if (delta != 0f)
                {
                    next = Mathf.Clamp((float)Math.Round((double)next + delta, 4), minimum, maximum);
                    GUI.FocusControl(null);
                    GUIUtility.keyboardControl = 0;
                }
            }
            EndRow(actual.HasValue ? Format(actual.Value) : null);
            _edited |= !next.Equals(value);
            if (float.IsNaN(next) || float.IsInfinity(next) || next < minimum || next > maximum)
                FieldError(name, maximum == float.MaxValue
                    ? (minimum == float.Epsilon ? "0보다 큰 유효한 값을 입력하세요." : Format(minimum) + " 이상의 유효한 값을 입력하세요.")
                    : Format(minimum) + "~" + Format(maximum) + " 범위로 입력하세요.");
            return next;
        }

        private bool BoolRow(string name, bool value, bool? actual)
        {
            BeginRow(name);
            bool next = EditorGUILayout.Toggle(value);
            EndRow(actual.HasValue ? actual.Value ? "사용" : "사용 안 함" : null);
            _edited |= next != value;
            return next;
        }

        private string StringRow(string name, string value, string actual)
        {
            BeginRow(name);
            string next = EditorGUILayout.DelayedTextField(value ?? string.Empty);
            EndRow(DrawingActual == null ? null : string.IsNullOrEmpty(actual) ? "없음" : actual);
            _edited |= next != (value ?? string.Empty);
            return next;
        }

        private void DamageRow(string name, SkillBalanceSettings draft, SkillBalanceSettings actual, string id)
        {
            SkillBalanceValue input = draft.Values.Find(value => value.Id == id);
            if (input == null) return;
            SkillBalanceValue applied = actual?.Values?.Find(value => value.Id == id);
            BeginRow(name + " (계산값)");
            GUILayout.Label(ScaleDamage(DrawingDraft.AttackPower, input.Value).ToString(Numbers), EditorStyles.miniLabel);
            EndRow(applied == null || DrawingActual == null ? null : ScaleDamage(DrawingActual.AttackPower, applied.Value).ToString(Numbers));
        }

        private void BeginRow(string name)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(name, name), GUILayout.Width(NameWidth));
        }

        private void EndRow(string actual)
        {
            GUILayout.Label(new GUIContent(actual ?? "—", actual == null ? (_drawingFinal ? "저장된 기본값을 불러오지 못했습니다." : "현재 적용 중인 테스트 전투가 없습니다.") : actual),
                EditorStyles.miniLabel, GUILayout.Width(ActualWidth));
            EditorGUILayout.EndHorizontal();
        }

        private void FieldError(string name, string message)
        {
            _fieldErrors[InputScope + name] = name + ": " + message;
            EditorGUILayout.HelpBox(name + ": " + message, MessageType.Error);
        }

        private void ApplyDraft()
        {
            SaveDraft();
            if (_draft == null || target == null) return;
            foreach (var error in _longErrors)
                if (error.Key.StartsWith("live.", StringComparison.Ordinal))
                {
                    _applyError = error.Value;
                    return;
                }
            foreach (var error in _fieldErrors)
            {
                if (error.Key.StartsWith("live.", StringComparison.Ordinal))
                {
                    _applyError = error.Value;
                    return;
                }
            }
            CombatBalanceSettings copy = Deserialize(draftJson);
            if (!target.TrySetCombatBalanceSettings(copy, out _applyError)) return;
            _applyError = null;
            _ownsSettings = true;
            SessionState.SetString(SessionPrefix + "Settings", draftJson);
            SessionState.SetBool(SessionPrefix + "Armed", true);
            _actual = target.IsCombatBalanceTestActive
                ? target.AppliedCombatBalanceSettings : null;
        }

        private void SaveDraft()
        {
            draftJson = _draft == null ? null : JsonUtility.ToJson(_draft);
        }

        private void ClearOwnedSettings()
        {
            if (_ownsSettings && target != null) target.ClearCombatBalanceSettings();
            _ownsSettings = false;
        }

        private static void ClearSession()
        {
            SessionState.EraseBool(SessionPrefix + "Armed");
            SessionState.EraseString(SessionPrefix + "Settings");
        }

        private static CombatBalanceSettings Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<CombatBalanceSettings>(json); }
            catch (ArgumentException) { return null; }
        }

        private static string Format(float value) => value.ToString("0.####", Numbers);

        private static long ScaleDamage(long attackPower, float multiplier)
        {
            double value = attackPower * (double)multiplier;
            if (double.IsNaN(value) || value <= 1d) return 1L;
            return value >= long.MaxValue ? long.MaxValue :
                (long)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static string SkillName(string id)
        {
            switch (id)
            {
                case "skill.tornado": return "토네이도";
                case "skill.headshot": return "헤드샷";
                case "skill.thunder": return "번개";
                case "skill.flame_exhaust": return "화염 방출";
                case "skill.magic_ball": return "마법구";
                case "skill.laser": return "레이저";
                case "skill.pet_baby_dragon": return "펫";
                default: return id;
            }
        }

        private static string MonsterName(string id)
        {
            switch (id)
            {
                case "monster.slime": return "슬라임";
                case "monster.01": return "그루터기";
                case "monster.02": return "여왕벌";
                case "monster.03": return "독버섯";
                case "monster.04": return "가면유령";
                case "monster.05": return "갑옷병사";
                case "monster.06": return "눈요정";
                case "monster.07": return "벌집";
                case "monster.08": return "골렘";
                case "monster.09": return "봉오리";
                case "monster.10": return "판도라 상자";
                default: return id;
            }
        }

        private static string SkillValueName(string id)
        {
            switch (id)
            {
                case "count": return "생성 수 (개)";
                case "durationSeconds": return "지속 시간 (초)";
                case "damageMultiplier": return "피해 배율 (배)";
                case "cooldownSeconds": return "발동 간격 (초)";
                case "firstCastDelaySeconds": return "첫 발동 대기 (초)";
                case "hitIntervalSeconds": return "타격 간격 (초)";
                case "moveSpeedWorldUnitsPerSecond": return "이동 속도 (월드 단위/초)";
                case "directionChangeSeconds": return "방향 전환 간격 (초)";
                case "stopChanceOnDirectionChange": return "방향 전환 시 정지 확률 (0~1)";
                case "stopDurationSeconds": return "정지 시간 (초)";
                case "shrinkOutSeconds": return "소멸 축소 시간 (초)";
                case "explosionDelaySeconds": return "폭발 대기 (초)";
                case "effectDurationSeconds": return "효과 지속 시간 (초)";
                case "splashDamageMultiplier": return "주변 피해 배율 (배)";
                case "bossDamageMultiplier": return "보스 대상 피해 배율 (배)";
                case "impactDelaySeconds": return "낙뢰 대기 (초)";
                case "projectileDurationSeconds": return "투사체 수명 (초)";
                case "speedWorldUnitsPerSecond": return "투사체 속도 (월드 단위/초)";
                case "cursorSizeRatio": return "커서 대비 크기 (배)";
                case "hitRadiusMultiplier": return "명중 반경 배율 (배)";
                case "hitEffectDurationSeconds": return "명중 효과 수명 (초)";
                case "sizeMultiplier": return "크기 배율 (배)";
                case "sameTargetHitCooldownSeconds": return "같은 대상 재타격 간격 (초)";
                case "attackIntervalSeconds": return "공격 간격 (초)";
                case "detectionRadiusWorldUnits": return "탐지 반경 (월드 단위)";
                case "attackRangeWorldUnits": return "공격 사거리 (월드 단위)";
                default: return ObjectNames.NicifyVariableName(id);
            }
        }
    }
}
