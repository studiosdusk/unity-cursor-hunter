using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.App.Editor
{
    /// <summary>Explicit authoring saves, separate from transient combat overrides.</summary>
    public static class CombatBalanceBaseStore
    {
        public const string InformationPath =
            "Assets/CursorHunter/Data/Resources/GameData/game-data.en.json";
        private static readonly CultureInfo Numbers = CultureInfo.InvariantCulture;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static bool TryCapture(HuntManager target, out CombatBalanceSettings settings,
            out string error)
        {
            settings = null;
            if (!TryGetScenePath(target, out string scenePath, out error) ||
                !CombatBalanceSceneStore.TryCapture(scenePath, out settings, out error)) return false;
            try
            {
                if (!GameInformationJson.TryDeserialize(File.ReadAllText(InformationPath),
                    out GameInformation information, out error)) return false;
                settings.DurationSeconds = information.stats.normalFieldDurationSeconds;
                settings.GlobalAliveLimit = information.rules.globalAliveLimit;
                settings.PerMonsterAliveLimit = information.rules.perMonsterAliveLimit;
                settings.Monsters.Clear();
                foreach (MonsterDefinition definition in Definitions())
                {
                    bool enabled = Array.Exists(information.monsters,
                        value => value.id == definition.MonsterId && value.enabled);
                    settings.Monsters.Add(MonsterBalanceSettings.FromSnapshot(
                        definition.CreateSnapshot(settings.PerMonsterAliveLimit), enabled));
                }
                foreach (BossDefinition definition in target.GetBalanceBossDefinitions().OrderBy(value => value.Stage))
                    settings.Bosses.Add(BossBalanceSettings.FromSnapshot(definition.CreateSnapshot()));
                return true;
            }
            catch (Exception exception) { error = exception.Message; settings = null; return false; }
        }

        public static bool TrySave(HuntManager target, CombatBalanceSettings settings,
            out string error)
        {
            if (!TryGetScenePath(target, out string scenePath, out error) ||
                !TryCapture(target, out CombatBalanceSettings previous, out error) ||
                !TryBuildFiles(scenePath, InformationPath, Definitions(), settings,
                    out Dictionary<string, string> originals,
                    out Dictionary<string, string> updated, out error)) return false;
            // Prepare boss SOs in the same transaction as common stats and normal monsters.
            // Legacy drafts without boss inputs leave the boss assets unchanged.
            if (settings.Bosses != null && settings.Bosses.Count > 0)
            {
                if (!TryBuildBossFiles(target.GetBalanceBossDefinitions(), settings.Bosses,
                    out Dictionary<string, string> bossOriginals,
                    out Dictionary<string, string> bossUpdated, out error)) return false;
                foreach (var pair in bossOriginals) originals.Add(pair.Key, pair.Value);
                foreach (var pair in bossUpdated) updated.Add(pair.Key, pair.Value);
            }

            AssetDatabase.DisallowAutoRefresh();
            try
            {
                if (!TryCommitFiles(originals, updated, out error)) return false;
                try
                {
                    ImportFiles(updated.Keys);
                    CombatBalanceSceneStore.SynchronizeLoadedScene(scenePath, settings);
                    if (!target.RefreshCombatBalanceBaseSources(out error))
                        throw new InvalidOperationException(error);
                    CombatBalanceSceneStore.RememberSavedScene(scenePath, settings);
                    return true;
                }
                catch (Exception exception)
                {
                    string rollbackError = string.Empty;
                    TryCommitFiles(updated, originals, out rollbackError);
                    ImportFiles(originals.Keys);
                    CombatBalanceSceneStore.SynchronizeLoadedScene(scenePath, previous);
                    target.RefreshCombatBalanceBaseSources(out _);
                    error = "기본값 저장을 완료하지 못했습니다: " + exception.Message;
                    if (!string.IsNullOrEmpty(rollbackError)) error += " / 복구: " + rollbackError;
                    return false;
                }
            }
            finally { AssetDatabase.AllowAutoRefresh(); }
        }

        // Public for fixture-based authoring tests; this method never writes files.
        public static bool TryBuildFiles(string scenePath, string informationPath,
            IReadOnlyList<MonsterDefinition> definitions, CombatBalanceSettings settings,
            out Dictionary<string, string> originals, out Dictionary<string, string> updated,
            out string error)
        {
            originals = new Dictionary<string, string>(StringComparer.Ordinal);
            updated = new Dictionary<string, string>(StringComparer.Ordinal);
            error = string.Empty;
            try
            {
                if (settings == null || !settings.TryValidate(out error)) return false;
                if (!CombatBalanceSceneStore.TryBuildSceneText(scenePath, settings,
                    out string sceneOriginal, out string sceneUpdated, out error)) return false;
                var monsterValues = new Dictionary<string, MonsterBalanceSettings>(StringComparer.Ordinal);
                foreach (MonsterBalanceSettings value in settings.Monsters)
                {
                    if (value == null || string.IsNullOrEmpty(value.MonsterId) ||
                        monsterValues.ContainsKey(value.MonsterId))
                    { error = "몬스터 설정에 중복되거나 없는 ID가 있습니다."; return false; }
                    monsterValues.Add(value.MonsterId, value);
                }
                if (definitions == null || definitions.Count != monsterValues.Count)
                { error = "모든 몬스터의 기본값이 필요합니다."; return false; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var staged = new Dictionary<string, string>(StringComparer.Ordinal);
                var before = new Dictionary<string, string>(StringComparer.Ordinal);
                before.Add(scenePath, sceneOriginal);
                staged.Add(scenePath, sceneUpdated);
                foreach (MonsterDefinition definition in definitions)
                {
                    if (definition == null || !seen.Add(definition.MonsterId) ||
                        !monsterValues.TryGetValue(definition.MonsterId, out MonsterBalanceSettings value))
                    { error = "몬스터 원본과 최종 설정이 일치하지 않습니다."; return false; }
                    if (!value.TryCreateSnapshot(definition.CreateSnapshot(settings.PerMonsterAliveLimit),
                        settings.PerMonsterAliveLimit, out _, out error)) return false;
                    if (!string.IsNullOrEmpty(value.BonusDropCurrencyId) &&
                        Array.IndexOf(CurrencyIds, value.BonusDropCurrencyId) < 0)
                    { error = value.MonsterId + ": 추가 보상 재화 ID가 올바르지 않습니다."; return false; }
                    if (value.SpawnIntervalSeconds < 0.1f)
                    { error = value.MonsterId + ": 생성 간격은 0.1초 이상이어야 합니다."; return false; }
                    string path = AssetDatabase.GetAssetPath(definition);
                    if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                        !path.EndsWith(".asset", StringComparison.Ordinal))
                    { error = "프로젝트의 몬스터 에셋만 저장할 수 있습니다."; return false; }
                    string original = File.ReadAllText(path);
                    before.Add(path, original);
                    staged.Add(path, BuildMonsterText(original, value));
                }
                if (!CombatBalanceInformationStore.TryBuildFiles(informationPath, settings,
                    out Dictionary<string, string> informationOriginals,
                    out Dictionary<string, string> informationUpdated, out error)) return false;
                foreach (var pair in informationOriginals) before.Add(ProjectPath(pair.Key), pair.Value);
                foreach (var pair in informationUpdated) staged.Add(ProjectPath(pair.Key), pair.Value);
                originals = before;
                updated = staged;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public static bool TryBuildBossFiles(IReadOnlyList<BossDefinition> definitions,
            IReadOnlyList<BossBalanceSettings> settings, out Dictionary<string, string> originals,
            out Dictionary<string, string> updated, out string error)
        {
            originals = new Dictionary<string, string>(StringComparer.Ordinal);
            updated = new Dictionary<string, string>(StringComparer.Ordinal);
            error = string.Empty;
            try
            {
                if (definitions == null || settings == null || definitions.Count != settings.Count)
                { error = "모든 보스 단계의 기본값이 필요합니다."; return false; }
                var values = new Dictionary<string, BossBalanceSettings>(StringComparer.Ordinal);
                foreach (BossBalanceSettings value in settings)
                {
                    if (value == null || string.IsNullOrEmpty(value.BossId) || values.ContainsKey(value.BossId))
                    { error = "중복되거나 없는 보스 설정입니다."; return false; }
                    values.Add(value.BossId, value);
                }
                var before = new Dictionary<string, string>(StringComparer.Ordinal);
                var staged = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (BossDefinition definition in definitions)
                {
                    if (definition == null || !values.TryGetValue(definition.BossId, out BossBalanceSettings value))
                    { error = "보스 원본과 최종 설정이 일치하지 않습니다."; return false; }
                    if (!value.TryCreateSnapshot(definition, out _, out error)) return false;
                    string path = AssetDatabase.GetAssetPath(definition);
                    if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.Ordinal))
                    { error = "프로젝트의 보스 에셋만 저장할 수 있습니다."; return false; }
                    string original = File.ReadAllText(path);
                    string next = original;
                    var fields = new Dictionary<string, string>
                    {
                        { "maxHealth", value.MaxHealth.ToString(Numbers) },
                        { "moveSpeed", Number(value.MoveSpeed) },
                        { "visualScale", Number(value.VisualScale) },
                        { "hitArea", "{x: " + Number(value.HitAreaWidth) + ", y: " + Number(value.HitAreaHeight) + "}" }
                    };
                    foreach (var pair in fields)
                    {
                        var pattern = new Regex("(?m)^(  " + pair.Key + ":)[^\\r\\n]*");
                        if (pattern.Matches(next).Count != 1)
                            throw new InvalidOperationException("보스 기본 필드를 찾을 수 없습니다: " + pair.Key);
                        next = pattern.Replace(next, match => match.Groups[1].Value + " " + pair.Value);
                    }
                    before.Add(path, original); staged.Add(path, next);
                }
                originals = before; updated = staged;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        /// <summary>Prepares every file before replacing any; rejects concurrent edits.</summary>
        public static bool TryCommitFiles(IReadOnlyDictionary<string, string> originals,
            IReadOnlyDictionary<string, string> updated, out string error)
        {
            error = string.Empty;
            var temporary = new Dictionary<string, string>(StringComparer.Ordinal);
            var replaced = new List<string>();
            try
            {
                if (originals == null || updated == null || originals.Count != updated.Count)
                    throw new InvalidOperationException("저장 파일 목록이 일치하지 않습니다.");
                foreach (var pair in updated)
                {
                    if (!originals.TryGetValue(pair.Key, out string original) ||
                        !File.Exists(pair.Key) || File.ReadAllText(pair.Key) != original)
                        throw new InvalidOperationException("원본이 변경되었습니다. 기본값을 다시 불러오세요: " + pair.Key);
                    if ((File.GetAttributes(pair.Key) & FileAttributes.ReadOnly) != 0)
                        throw new IOException("읽기 전용 파일입니다: " + pair.Key);
                    if (pair.Value == original) continue;
                    string temp = pair.Key + ".balance-" + Guid.NewGuid().ToString("N") + ".tmp";
                    temporary.Add(pair.Key, temp);
                    File.WriteAllText(temp, pair.Value, Utf8);
                }
                // Check again after staging, before committing the first file.
                foreach (var pair in originals)
                    if (File.ReadAllText(pair.Key) != pair.Value)
                        throw new IOException("저장 준비 중 원본이 변경되었습니다: " + pair.Key);
                foreach (var pair in temporary)
                {
                    File.Replace(pair.Value, pair.Key, null);
                    replaced.Add(pair.Key);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                foreach (string path in replaced)
                {
                    try
                    {
                        string restore = path + ".balance-restore-" + Guid.NewGuid().ToString("N") + ".tmp";
                        temporary[path] = restore;
                        File.WriteAllText(restore, originals[path], Utf8);
                        File.Replace(restore, path, null);
                    }
                    catch (Exception restoreException) { error += " / 복구 실패 " + path + ": " + restoreException.Message; }
                }
                return false;
            }
            finally
            {
                foreach (string path in temporary.Values)
                    try { if (File.Exists(path)) File.Delete(path); }
                    catch (IOException) { /* Keep the original save result if temporary cleanup fails. */ }
            }
        }

        private static string BuildMonsterText(string text, MonsterBalanceSettings value)
        {
            var fields = new Dictionary<string, string>
            {
                { "maxHealth", value.MaxHealth.ToString(Numbers) },
                { "spawnIntervalSeconds", Number(value.SpawnIntervalSeconds) },
                { "packSize", value.PackSize.ToString(Numbers) },
                { "moveSpeed", Number(value.MoveSpeed) },
                { "visualScale", Number(value.VisualScale) },
                { "hitAreaWidth", Number(value.HitAreaWidth) },
                { "hitAreaHeight", Number(value.HitAreaHeight) },
                { "garnetReward", value.GarnetReward.ToString(Numbers) },
                { "bonusDropCurrencyId", "\"" + (value.BonusDropCurrencyId ?? "") + "\"" },
                { "bonusDropAmount", value.BonusDropAmount.ToString(Numbers) },
                { "bonusDropChancePercent", Number(value.BonusDropChancePercent) }
            };
            foreach (var pair in fields)
            {
                var pattern = new Regex("(?m)^(    " + pair.Key + ":)[^\\r\\n]*");
                if (pattern.Matches(text).Count != 1)
                    throw new InvalidOperationException("몬스터 기본 필드를 찾을 수 없습니다: " + pair.Key);
                text = pattern.Replace(text, match => match.Groups[1].Value + " " + pair.Value);
            }
            var finalFlag = new Regex("(?m)^  baseStatsAreFinalValues:[^\\r\\n]*");
            if (finalFlag.IsMatch(text)) return finalFlag.Replace(text, "  baseStatsAreFinalValues: 1");
            int position = text.IndexOf("  baseStats:", StringComparison.Ordinal);
            if (position < 0) throw new InvalidOperationException("몬스터 BaseStats가 없습니다.");
            return text.Insert(position, "  baseStatsAreFinalValues: 1" + (text.Contains("\r\n") ? "\r\n" : "\n"));
        }

        private static bool TryGetScenePath(HuntManager target, out string path, out string error)
        {
            path = target == null ? null : target.gameObject.scene.path;
            error = string.Empty;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".unity", StringComparison.Ordinal) || !File.Exists(path))
            { error = "저장된 프로젝트 씬의 HuntManager를 선택하세요."; return false; }
            return true;
        }

        private static MonsterDefinition[] Definitions() => Resources
            .LoadAll<MonsterDefinition>("MonsterDefinitions")
            .OrderBy(definition => definition.MonsterId, StringComparer.Ordinal).ToArray();
        private static void ImportFiles(IEnumerable<string> paths)
        {
            string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            foreach (string path in paths)
            {
                string full = Path.GetFullPath(path);
                if (full.StartsWith(assetsRoot, StringComparison.Ordinal))
                    AssetDatabase.ImportAsset("Assets/" + full.Substring(assetsRoot.Length)
                        .Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            }
        }
        private static string Number(float value) => value.ToString("R", Numbers);
        private static string ProjectPath(string path)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")) +
                Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            return full.StartsWith(project, StringComparison.Ordinal)
                ? full.Substring(project.Length).Replace('\\', '/') : full;
        }
        private static readonly string[] CurrencyIds =
            { "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };
    }
}
