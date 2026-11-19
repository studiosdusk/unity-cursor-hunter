using System;
using System.IO;
using System.Linq;
using CursorHunter.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CursorHunter.Progression.Editor
{
    [InitializeOnLoad]
    public sealed class WorldStageTextWorkbook : AssetPostprocessor
    {
        public const string Path = "Assets/CursorHunter/Data/Authoring/StageText.xlsx";
        private const string StageFolder = "/Resources/WorldStages/";
        private static bool pending;

        static WorldStageTextWorkbook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && pending) Queue();
            };
            Queue();
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(path => path == Path || path.Contains(StageFolder))) Queue();
        }

        private static void Queue()
        {
            pending = true;
            EditorApplication.delayCall -= WhenReady;
            EditorApplication.delayCall += WhenReady;
        }

        private static void WhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Queue(); return; }
            pending = false;
            try { Apply(); }
            catch (Exception e) { Debug.LogError("스테이지 엑셀 적용 실패: " + e.Message); }
        }

        [MenuItem("Tools/Cursor Hunter/World Stages/Apply Excel")]
        public static void Apply() => TraitCardTextWorkbook.WriteTables("WorldStage", ReadValidated(Path));

        public static TraitCardTextWorkbook.Row[] ReadValidated(string path)
        {
            var rows = TraitCardTextWorkbook.ReadRows(path, "스테이지 표시 정보", false);
            var keys = rows.ToDictionary(row => row.Key, StringComparer.Ordinal);
            void Require(string key, bool formatted = false)
            {
                if (!keys.TryGetValue(key, out var row)) throw new InvalidDataException("Missing stage text: " + key);
                foreach (string value in row.Values.Values.Where(s => s.Length > 0))
                {
                    // Each formatted label takes exactly one already formatted name, balance or multiplier.
                    string rest = value.Replace("{0}", "");
                    if (rest.IndexOfAny(new[] { '{', '}' }) >= 0 || (value.Length - rest.Length != (formatted ? 3 : 0)))
                        throw new InvalidDataException("Invalid stage text placeholder: " + key);
                }
            }
            foreach (var stage in WorldStageDefinition.LoadCatalog())
            {
                Require(stage.textKey + ".name");
                Require(stage.textKey + ".description");
            }
            foreach (var key in new[] { "title", "locked_title", "insufficient_points",
                "hunt", "boss", "home", "help", "instructions", "save_failed" })
                Require("stage.ui." + key);
            foreach (var key in new[] { "selected", "points", "monster_xp", "monster_hp", "raid_stage" })
                Require("stage.ui." + key, true);
            return rows;
        }
    }

    internal sealed class WorldStageTextBuildCheck : BuildPlayerProcessor
    {
        public override int callbackOrder => -1000;
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            try { WorldStageTextWorkbook.Apply(); }
            catch (Exception e) { throw new BuildFailedException("WorldStage: " + e.Message); }
        }
    }
}
