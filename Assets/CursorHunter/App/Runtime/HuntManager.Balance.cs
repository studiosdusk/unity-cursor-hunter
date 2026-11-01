#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using UnityEngine;

namespace CursorHunter.App
{
    public sealed partial class HuntManager
    {
        private CombatBalanceSettings _combatBalanceSettings;
        private RunId _balanceRunId;
        private Dictionary<string, SpawnPlanEntry> _balanceTemplates;

        public bool HasCombatBalanceSettings => _combatBalanceSettings != null;
        public bool IsCombatRunActive => combatRunController != null && combatRunController.IsRunActive;
        public bool IsBossCombatActive => combatRunController != null &&
            combatRunController.IsRunActive && combatRunController.IsBossRun;
        public int ActiveBossBalanceStage => IsBossCombatActive ? _lastBossStage : 0;
        public bool IsCombatBalanceTestActive => combatRunController != null &&
            combatRunController.IsRunActive && _balanceRunId.IsValid &&
            combatRunController.CurrentRunId == _balanceRunId;
        public CombatBalanceSettings AppliedCombatBalanceSettings =>
            IsCombatBalanceTestActive ? CaptureCombatBalanceSettings() : null;

        public CombatBalanceSettings CaptureCombatBalanceSettings()
        {
            ResolveReferences();
            if (combatRunController == null) return null;
            if (!combatRunController.IsRunActive && _combatBalanceSettings != null)
            {
                CombatBalanceSettings saved = _combatBalanceSettings.Clone();
                EnsureBossBalanceSettings(saved);
                return saved;
            }
            var result = new CombatBalanceSettings();
            result.UsePlayerProgression = _combatBalanceSettings?.UsePlayerProgression ?? connectTraitScreenToCombat;
            PlayerCombatStatsRuntime stats = combatRunController.PlayerCombatStatsRuntime;
            CombatSnapshot cursor = combatRunController.IsRunActive
                ? stats.Snapshot : stats.DefaultsCombatSnapshot;
            result.AttackPower = cursor.AttackPower;
            result.AttackRadiusWorldUnits = cursor.RangeMultiplier * GameInformation.BaseAttackRadiusWorldUnits;
            result.AttackCooldownSeconds = cursor.AttackCooldownSeconds;
            result.AutoAttackEnabled = cursor.AutoAttackEnabled;
            result.CriticalChancePercent = cursor.CriticalChancePercent;
            result.CriticalDamageMultiplier = cursor.CriticalDamageMultiplier;
            result.BossDamageMultiplier = cursor.BossDamageMultiplier;
            result.DurationSeconds = combatRunController.IsRunActive
                ? combatRunController.DurationSeconds : Mathf.Clamp(durationSeconds, 15f, 30f);
            result.Skills = combatRunController.CaptureSkillBalanceSettings();
            EnsureBalanceTemplates();

            GameInformation initial = null;
            TextAsset data = Resources.Load<TextAsset>("GameData/game-data.en");
            if (data != null && GameInformationJson.TryDeserialize(data.text, out initial, out _) &&
                defaultStage != null) defaultStage.TryApplyToInformation(initial, out _);
            SpawnPlan current = combatRunController.IsRunActive && !IsBossCombatActive
                ? monsterSpawner?.CurrentSpawnPlan : null;
            result.GlobalAliveLimit = current?.GlobalAliveLimit ?? initial?.rules?.globalAliveLimit ?? 80;
            result.PerMonsterAliveLimit = current != null && current.HasEntries
                ? current.Entries[0].Snapshot.AliveLimit : initial?.rules?.perMonsterAliveLimit ?? 80;
            if (current == null && initial?.stats != null)
                result.DurationSeconds = initial.stats.normalFieldDurationSeconds;
            var activeEntries = new Dictionary<string, SpawnPlanEntry>();
            if (current != null)
                foreach (SpawnPlanEntry entry in current.Entries)
                    activeEntries[entry.Snapshot.MonsterId] = entry;
            foreach (var pair in _balanceTemplates)
            {
                SpawnSnapshot snapshot = activeEntries.TryGetValue(pair.Key, out SpawnPlanEntry active)
                    ? active.Snapshot : pair.Value.Snapshot;
                bool enabled = current != null ? monsterSpawner.IsSpeciesEnabled(pair.Key) :
                    initial?.monsters != null && Array.Exists(initial.monsters,
                        value => value.id == pair.Key && value.enabled);
                MonsterBalanceSettings monster = MonsterBalanceSettings.FromSnapshot(snapshot, enabled);
                if (!activeEntries.ContainsKey(pair.Key) && initial?.monsters != null)
                {
                    MonsterInformation authored = Array.Find(initial.monsters, value => value.id == pair.Key);
                    if (authored != null) monster.PackSize += Math.Max(0, authored.productionCount - 1);
                }
                result.Monsters.Add(monster);
            }
            result.Monsters.Sort((first, second) => string.CompareOrdinal(first.MonsterId, second.MonsterId));
            CombatBalanceSettings retained = _combatBalanceSettings?.Clone();
            if (IsBossCombatActive && retained != null)
            {
                // A one-boss plan must never overwrite the retained normal-field roster or limits.
                result.Monsters = retained.Monsters;
                result.DurationSeconds = retained.DurationSeconds;
                result.GlobalAliveLimit = retained.GlobalAliveLimit;
                result.PerMonsterAliveLimit = retained.PerMonsterAliveLimit;
            }
            result.Bosses = retained?.Bosses;
            EnsureBossBalanceSettings(result);
            if (IsBossCombatActive && monsterSpawner?.CurrentSpawnPlan != null)
            {
                SpawnSnapshot activeBoss = monsterSpawner.CurrentSpawnPlan.Entries[0].Snapshot;
                int index = result.Bosses.FindIndex(value => value.BossId == activeBoss.MonsterId);
                if (index >= 0) result.Bosses[index] = BossBalanceSettings.FromSnapshot(activeBoss);
            }
            return result;
        }

        public bool TrySetCombatBalanceSettings(CombatBalanceSettings settings, out string error)
        {
            ResolveReferences();
            EnsureRunCoordinator();
            error = string.Empty;
            if (settings == null || combatRunController == null || monsterSpawner == null ||
                !settings.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error)) error = "Main의 전투 시스템을 찾을 수 없습니다.";
                return false;
            }
            CombatBalanceSettings next = settings.Clone();
            EnsureBossBalanceSettings(next);
            if (next.UsePlayerProgression && (!connectTraitScreenToCombat || progressionController == null))
            {
                error = "구매한 특성을 사용하려면 HuntManager의 특성 연동을 켜 주세요.";
                return false;
            }
            if (next.UsePlayerProgression && combatRunController.IsRunActive &&
                (!_runUsesTraitScreen || IsCombatBalanceTestActive && _combatBalanceSettings?.UsePlayerProgression != true))
            {
                error = "직접 입력값으로 시작한 전투가 끝난 뒤 구매한 특성 사용으로 전환하세요.";
                return false;
            }
            if ((!next.UsePlayerProgression && !combatRunController.ValidateSkillBalanceSettings(next.Skills, out error)) ||
                !TryValidateBossBalanceSettings(next, out error) ||
                !TryBuildBalancePlan(next, out SpawnPlan plan, out List<string> enabled, out error))
                return false;
            if (Application.isPlaying && combatRunController.IsRunActive)
            {
                if (IsBossCombatActive && !TryBuildBossBalancePlan(next, FindBalanceBoss(_lastBossStage),
                    out plan, out enabled, out error)) return false;
                if (!monsterSpawner.CanApplyBalance(plan, enabled, out error)) return false;
                CombatBalanceSettings previous = CaptureCombatBalanceSettings();
                // Mark first: even a duration shortening must never settle a tuned run.
                _runCoordinator.MarkAsBalanceTest();
                _balanceRunId = combatRunController.CurrentRunId;
                if (!next.UsePlayerProgression && !combatRunController.TryApplyLiveBalance(next.ToCombatSnapshot(),
                    IsBossCombatActive ? BossDefinition.DurationSeconds : next.DurationSeconds,
                    next.Skills, out error)) return false;
                if (!monsterSpawner.TryApplyBalance(plan, enabled, out error))
                {
                    if (!next.UsePlayerProgression) combatRunController.TryApplyLiveBalance(previous.ToCombatSnapshot(),
                        IsBossCombatActive ? BossDefinition.DurationSeconds : previous.DurationSeconds,
                        previous.Skills, out _);
                    return false;
                }
            }
            _combatBalanceSettings = next;
            return true;
        }

        public BossDefinition[] GetBalanceBossDefinitions()
        {
            if (bosses == null || bosses.Length == 0) bosses = Resources.LoadAll<BossDefinition>("BossDefinitions");
            return (BossDefinition[])bosses.Clone();
        }

        private BossDefinition FindBalanceBoss(int stage) =>
            Array.Find(GetBalanceBossDefinitions(), value => value != null && value.Stage == stage);

        private void EnsureBossBalanceSettings(CombatBalanceSettings settings)
        {
            if (settings.Bosses != null && settings.Bosses.Count > 0) return;
            settings.Bosses = new List<BossBalanceSettings>();
            BossDefinition[] definitions = GetBalanceBossDefinitions();
            Array.Sort(definitions, (a, b) => (a != null ? a.Stage : 0).CompareTo(b != null ? b.Stage : 0));
            foreach (BossDefinition definition in definitions)
                if (definition != null) settings.Bosses.Add(BossBalanceSettings.FromSnapshot(definition.CreateSnapshot()));
        }

        private bool TryValidateBossBalanceSettings(CombatBalanceSettings settings, out string error)
        {
            error = string.Empty;
            BossDefinition[] definitions = GetBalanceBossDefinitions();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (settings.Bosses.Count != definitions.Length)
            { error = "모든 보스 단계의 설정이 필요합니다."; return false; }
            foreach (BossBalanceSettings value in settings.Bosses)
            {
                if (value == null || string.IsNullOrEmpty(value.BossId) || !seen.Add(value.BossId))
                { error = "중복되거나 없는 보스 설정입니다."; return false; }
                BossDefinition definition = Array.Find(definitions, candidate => candidate != null && candidate.BossId == value.BossId);
                if (!value.TryCreateSnapshot(definition, out _, out error)) return false;
            }
            return true;
        }

        private bool TryBuildBossBalancePlan(CombatBalanceSettings settings, BossDefinition boss,
            out SpawnPlan plan, out List<string> enabled, out string error)
        {
            plan = null; enabled = null; error = string.Empty;
            BossBalanceSettings value = boss == null ? null : settings.Bosses.Find(candidate => candidate.BossId == boss.BossId);
            if (value == null) { error = "도전할 보스의 밸런스 설정이 없습니다."; return false; }
            if (!value.TryCreateSnapshot(boss, out SpawnSnapshot snapshot, out error)) return false;
            plan = new SpawnPlan(new[] { new SpawnPlanEntry(snapshot, boss.VisualPrefab) }, 1);
            enabled = new List<string> { boss.BossId };
            return true;
        }

        public void ClearCombatBalanceSettings()
        {
            if (IsCombatBalanceTestActive) ReturnToMainMenu();
            _combatBalanceSettings = null;
            _balanceRunId = default;
            combatRunController?.ClearSkillBalanceOverrides();
        }

        public bool RefreshCombatBalanceBaseSources(out string error)
        {
            ResolveReferences();
            _balanceTemplates = null;
            error = string.Empty;
            return !connectTraitScreenToCombat || progressionController == null ||
                progressionController.ReloadAuthoredCombatDefaults(out error);
        }

        private void EnsureBalanceTemplates()
        {
            if (_balanceTemplates != null) return;
            _balanceTemplates = new Dictionary<string, SpawnPlanEntry>(StringComparer.Ordinal);
            foreach (MonsterDefinition definition in Resources.LoadAll<MonsterDefinition>("MonsterDefinitions"))
                if (definition != null && definition.VisualPrefab != null)
                    _balanceTemplates[definition.MonsterId] = new SpawnPlanEntry(
                        definition.CreateSnapshot(80), definition.VisualPrefab);
            if (monsterSpawner?.CurrentSpawnPlan != null && (combatRunController == null || !combatRunController.IsBossRun))
                foreach (SpawnPlanEntry entry in monsterSpawner.CurrentSpawnPlan.Entries)
                    if (!_balanceTemplates.ContainsKey(entry.Snapshot.MonsterId))
                        _balanceTemplates.Add(entry.Snapshot.MonsterId, entry);
        }

        private bool TryBuildBalancePlan(CombatBalanceSettings settings, out SpawnPlan plan,
            out List<string> enabled, out string error)
        {
            EnsureBalanceTemplates();
            plan = null;
            enabled = new List<string>();
            var entries = new List<SpawnPlanEntry>();
            var seen = new HashSet<string>();
            foreach (MonsterBalanceSettings value in settings.Monsters)
            {
                if (value == null || string.IsNullOrEmpty(value.MonsterId) || !seen.Add(value.MonsterId) ||
                    !_balanceTemplates.TryGetValue(value.MonsterId, out SpawnPlanEntry reference))
                {
                    error = "중복되거나 없는 몬스터 설정입니다.";
                    return false;
                }
                if (value.SpawnIntervalSeconds < 0.1f)
                {
                    error = value.MonsterId + ": 생성 간격은 0.1초 이상이어야 합니다.";
                    return false;
                }
                if (value.BonusDropAmount > 0 && Array.IndexOf(BalanceCurrencyIds, value.BonusDropCurrencyId) < 0)
                {
                    error = value.MonsterId + ": 추가 보상의 젬 종류를 확인하세요.";
                    return false;
                }
                if (!value.TryCreateSnapshot(reference.Snapshot, settings.PerMonsterAliveLimit,
                    out SpawnSnapshot snapshot, out error)) return false;
                entries.Add(new SpawnPlanEntry(snapshot, reference.VisualPrefab));
                if (value.Enabled) enabled.Add(value.MonsterId);
            }
            if (entries.Count != _balanceTemplates.Count || entries.Count == 0)
            {
                error = "모든 몬스터 종류의 설정이 필요합니다.";
                return false;
            }
            plan = new SpawnPlan(entries, settings.GlobalAliveLimit);
            error = string.Empty;
            return true;
        }

        private static readonly string[] BalanceCurrencyIds =
            { "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };

        private void BeginBalanceRun(BossDefinition boss = null)
        {
            CombatBalanceSettings settings = _combatBalanceSettings;
            string error = string.Empty;
            if (settings == null || !TrySetCombatBalanceSettings(settings, out error))
            {
                Debug.LogWarning("전투 밸런스 설정을 확인하세요: " + error, this);
                return;
            }
            settings = _combatBalanceSettings;
            ProgressionCombatSnapshot player = null;
            if (settings.UsePlayerProgression)
            {
                try
                {
                    progressionController.SetCursorCombatStatDefaults(combatRunController.PlayerCombatStatsRuntime.Defaults);
                    player = progressionController.CreateCombatSnapshot();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("플레이어 특성 정보를 준비하지 못했습니다: " + exception.Message, this);
                    return;
                }
                combatRunController.ClearSkillBalanceOverrides();
            }
            SpawnPlan plan;
            List<string> enabled;
            bool built = boss != null
                ? TryBuildBossBalancePlan(settings, boss, out plan, out enabled, out error)
                : TryBuildBalancePlan(settings, out plan, out enabled, out error);
            if (!built || !settings.UsePlayerProgression &&
                !combatRunController.SetSkillBalanceOverrides(settings.Skills, out error))
            {
                Debug.LogWarning(error, this);
                return;
            }
            float testDuration = boss != null ? BossDefinition.DurationSeconds : player != null
                ? GetRunDuration(RunMode.NormalField, player, durationSeconds, RelicCombatModifiers.From(player.Relics))
                : settings.DurationSeconds;
            var request = new RunRequest(RunId.Create(), Mathf.Max(1, schemaVersion), Mathf.Max(1, balanceVersion),
                boss == null ? RunMode.NormalField : RunMode.Boss,
                boss == null ? string.Empty : boss.BossId, CreateRunSeed(),
                testDuration,
                boss == null ? 0 : boss.Stage, boss == null ? null : boss.CreateClearRewards());
            _balanceRunId = request.RunId;
            bool started = player != null
                ? _runCoordinator.StartBalanceTest(request, player, plan, enabled, out error)
                : _runCoordinator.StartBalanceTest(request, settings.ToCombatSnapshot(), plan, enabled, out error);
            if (!started)
            {
                _balanceRunId = default;
                combatRunController.ClearSkillBalanceOverrides();
                Debug.LogWarning("전투 밸런스 시작 실패: " + error, this);
                return;
            }
            _lastRunWasBoss = boss != null;
            _lastBossStage = boss == null ? 0 : boss.Stage;
            _lastBossSucceeded = false;
            if (boss != null) bossHealth?.Show(combatRunController, monsterSpawner.BossTarget, boss);
            else bossHealth?.Hide();
            _runUsesTraitScreen = player != null;
            if (player != null) progressionController.SetActiveRunSnapshot(player);
            else progressionController?.ClearActiveRunSnapshot();
            uiRootController?.EnterCombat();
            cursorController.SetRangeMultiplier(combatRunController.PlayerCombatStatsRuntime.AttackRangeMultiplier);
            cursorController.ShowCursorImage();
            testPanelToggleController?.HideTestPanel();
            if (runHud != null)
            {
                runHud.Initialize();
                runHud.ShowRunning(combatRunController, monsterSpawner);
            }
        }

        private bool IsBalanceResult(RunResult result) => _balanceRunId.IsValid && result.RunId == _balanceRunId;

        private void FinishBalanceRun(RunResult result)
        {
            if (!IsBalanceResult(result)) return;
            // The window owns the settings across attempts. Only clear this
            // run's bindings; the next run either reads fresh purchases or
            // reapplies the retained manual values, according to the selected source.
            _balanceRunId = default;
            combatRunController.ClearSkillBalanceOverrides();
        }
    }
}
#endif
