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
                return _combatBalanceSettings.Clone();
            var result = new CombatBalanceSettings();
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
            SpawnPlan current = combatRunController.IsRunActive ? monsterSpawner?.CurrentSpawnPlan : null;
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
            if (!combatRunController.ValidateSkillBalanceSettings(next.Skills, out error) ||
                !TryBuildBalancePlan(next, out SpawnPlan plan, out List<string> enabled, out error))
                return false;
            if (!combatRunController.IsRunActive && enabled.Count == 0)
            {
                error = "전투를 시작하려면 한 종류 이상의 몬스터를 사용하세요.";
                return false;
            }
            if (Application.isPlaying && combatRunController.IsRunActive)
            {
                if (!monsterSpawner.CanApplyBalance(plan, enabled, out error)) return false;
                CombatBalanceSettings previous = CaptureCombatBalanceSettings();
                // Mark first: even a duration shortening must never settle a tuned run.
                _runCoordinator.MarkAsBalanceTest();
                _balanceRunId = combatRunController.CurrentRunId;
                if (!combatRunController.TryApplyLiveBalance(next.ToCombatSnapshot(),
                    next.DurationSeconds, next.Skills, out error)) return false;
                if (!monsterSpawner.TryApplyBalance(plan, enabled, out error))
                {
                    combatRunController.TryApplyLiveBalance(previous.ToCombatSnapshot(),
                        previous.DurationSeconds, previous.Skills, out _);
                    return false;
                }
            }
            _combatBalanceSettings = next;
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
            if (monsterSpawner?.CurrentSpawnPlan != null)
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

        private void BeginBalanceRun()
        {
            CombatBalanceSettings settings = _combatBalanceSettings;
            string error = string.Empty;
            if (settings == null || !TrySetCombatBalanceSettings(settings, out error))
            {
                Debug.LogWarning("전투 밸런스 설정을 확인하세요: " + error, this);
                return;
            }
            settings = _combatBalanceSettings;
            if (!TryBuildBalancePlan(settings, out SpawnPlan plan, out List<string> enabled, out error) ||
                !combatRunController.SetSkillBalanceOverrides(settings.Skills, out error))
            {
                Debug.LogWarning(error, this);
                return;
            }
            var request = new RunRequest(RunId.Create(), 3, Mathf.Max(1, balanceVersion),
                RunMode.NormalField, string.Empty, CreateRunSeed(), settings.DurationSeconds);
            _balanceRunId = request.RunId;
            if (!_runCoordinator.StartBalanceTest(request, settings.ToCombatSnapshot(), plan,
                enabled, out error))
            {
                _balanceRunId = default;
                combatRunController.ClearSkillBalanceOverrides();
                Debug.LogWarning("전투 밸런스 시작 실패: " + error, this);
                return;
            }
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
            // run's bindings; the next run will compose them again from the
            // retained final values, without adding progression a second time.
            _balanceRunId = default;
            combatRunController.ClearSkillBalanceOverrides();
        }
    }
}
#endif
