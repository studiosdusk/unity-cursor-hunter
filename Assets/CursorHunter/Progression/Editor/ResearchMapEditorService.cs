using System;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.Progression.ResearchPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace CursorHunter.Progression.Editor
{
    public static class ResearchMapEditorService
    {
        public const string MainScene = "Assets/CursorHunter/App/Scenes/Main.unity";
        public const string PrefabPath = "Assets/CursorHunter/Progression/Prefabs/ResearchNode.prefab";
        public static ResearchTestMap FindMap()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScene);
            if (!scene.IsValid() || !scene.isLoaded) return null;
            return scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).FirstOrDefault();
        }
        public static ResearchMapDefinition Capture(ResearchTestMap map, string version)
        {
            if (map == null || map.nodes == null) throw new InvalidOperationException("Main 씬의 연구 맵을 찾을 수 없습니다.");
            var asset = ScriptableObject.CreateInstance<ResearchMapDefinition>();
            asset.versionId = version;
            foreach (var node in map.nodes)
            {
                if (node == null || node.definition == null) throw new InvalidOperationException("씬에 SO가 없는 노드가 있습니다.");
                var costs = new ResearchCost[node.definition.ranks.Length];
                for (int i = 0; i < costs.Length; i++)
                    if (!node.TryGetCost(i, out costs[i])) throw new InvalidOperationException(node.name + ": 비용이 올바르지 않습니다.");
                asset.nodes.Add(new ResearchMapPlacement {
                    id = node.placementId, definition = node.definition,
                    position = ((RectTransform)node.transform).anchoredPosition, costs = costs,
                    prerequisites = node.prerequisites == null ? new List<string>() : node.prerequisites.Select(x => x != null ? x.placementId : "").ToList(),
                    requiredPrerequisites = node.requiredPrerequisites
                });
            }
            return asset;
        }
        public static string Fingerprint(ResearchTestMap map)
        {
            var temp = Capture(map, "scene");
            try { return FingerprintDefinition(temp); }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
        }
        public static string FingerprintDefinition(ResearchMapDefinition definition)
        {
                var text = new System.Text.StringBuilder();
                void Add(string value) { value ??= ""; text.Append(value.Length).Append(':').Append(value); }
                foreach (var node in definition.nodes)
                {
                    Add(node.id);
                    Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node.definition)));
                    Add(node.position.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Add(node.position.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Add(node.requiredPrerequisites.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Add(node.prerequisites.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    foreach (var prerequisite in node.prerequisites) Add(prerequisite);
                    Add(node.costs.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    foreach (var cost in node.costs) { Add(cost.currencyId); Add(cost.amount.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
                }
                return Hash128.Compute(text.ToString()).ToString();
        }
        public static string CheckApply(ResearchMapDefinition asset, ResearchTestMap map)
        {
            var errors = GetApplyErrors(asset, map);
            return errors.Count == 0 ? null : string.Join("\n", errors);
        }
        public static List<string> GetApplyErrors(ResearchMapDefinition asset, ResearchTestMap map)
        {
            var errors = Validate(asset);
            if (AssetDatabase.Contains(asset) && AssetDatabase.FindAssets("t:ResearchMapDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ResearchMapDefinition>)
                .Any(other => other != null && other != asset && other.versionId == asset.versionId))
                errors.Add("버전 이름 '" + asset.versionId + "'이 이미 사용 중입니다. 오른쪽에서 다른 이름을 입력해 주세요.");
            if (map == null || map.nodes == null || map.nodes.Length == 0 || map.navigation == null || map.navigation.board == null)
            {
                errors.Add("Main 씬의 연구 맵을 찾을 수 없습니다. Main 씬을 열고 연구 맵과 노드가 있는지 확인해 주세요.");
                return errors;
            }
            try
            {
                bool preservingBeforeTest = asset.isolatedTest && (map.appliedVersion == null || !map.appliedVersion.isolatedTest);
                if (!preservingBeforeTest && !string.IsNullOrEmpty(map.appliedSceneFingerprint) && map.appliedSceneFingerprint != Fingerprint(map))
                    errors.Add("씬에서 연구 맵이 변경되었습니다. 먼저 '씬에서 가져오기'로 씬의 변경사항을 새 버전에 보관해 주세요.");
            }
            catch (Exception e) { errors.Add("씬의 기존 연구 맵을 읽을 수 없습니다. 씬 노드 설정을 확인해 주세요: " + e.Message); }
            if (errors.Count > 0) return errors;
            var definitions = asset.nodes.ToDictionary(x => x.id);
            bool switchingIsolated = asset.isolatedTest != (map.appliedVersion != null && map.appliedVersion.isolatedTest);
            if (switchingIsolated && map.nodes.Any(x => x != null && definitions.ContainsKey(x.placementId)))
                errors.Add("테스트 맵과 기존 맵의 배치 ID가 겹칩니다. 구매 기록 보호를 위해 서로 다른 ID를 사용해 주세요.");
            foreach (var old in map.nodes)
            {
                if (old == null || !definitions.TryGetValue(old.placementId, out var next))
                { if (!switchingIsolated || old == null) errors.Add("기존 노드 '" + (old != null ? old.placementId : "알 수 없음") + "'가 없어졌습니다. 구매 기록 이전 정책이 정해질 때까지 기존 노드를 유지해 주세요."); continue; }
                if (old.definition != next.definition)
                    errors.Add("기존 노드 '" + old.placementId + "'의 연구 종류가 바뀌었습니다. 구매 기록 이전 정책이 정해질 때까지 원래 연구를 유지해 주세요.");
                var before = (old.prerequisites ?? Array.Empty<ResearchTestNode>()).Where(x => x != null).Select(x => x.placementId).OrderBy(x => x).ToArray();
                var after = (next.prerequisites ?? new List<string>()).OrderBy(x => x).ToArray();
                // Isolated test maps have their own placement IDs and may be rearranged freely.
                // Production maps keep the migration guard for existing purchases.
                if (!asset.isolatedTest && (!before.SequenceEqual(after) || old.requiredPrerequisites != next.requiredPrerequisites))
                    errors.Add("기존 노드 '" + old.placementId + "'의 선행 조건이 바뀌었습니다. 구매 기록 이전 정책이 정해질 때까지 원래 연결과 필요 개수를 유지해 주세요.");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) errors.Add("연구 노드 프리팹을 찾을 수 없습니다. " + PrefabPath + " 경로를 확인해 주세요.");
            if (map.navigation.board.Find("EditableLinks") == null) errors.Add("씬에서 연결선을 배치할 EditableLinks를 찾을 수 없습니다. 연구 맵 오브젝트 구성을 확인해 주세요.");
            return errors;
        }
        public static List<string> Validate(ResearchMapDefinition asset)
        {
            var errors = asset.Validate();
            var table = LocalizationEditorSettings.GetStringTableCollection("Research")?.GetTable("ko") as StringTable;
            foreach (var node in (asset.nodes ?? new List<ResearchMapPlacement>()).Where(x => x != null && x.definition != null && x.definition.presentation != null))
            {
                string key = node.definition.presentation.textKey;
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(table?.GetEntry(key + ".title")?.Value) ||
                    string.IsNullOrWhiteSpace(table?.GetEntry(key + ".description")?.Value))
                    errors.Add(node.id + ": 엑셀에 연구 제목 또는 효과 설명이 없습니다.");
            }
            var nodeLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var node in (asset.nodes ?? new List<ResearchMapPlacement>()).Where(x => x != null && !string.IsNullOrWhiteSpace(x.id)))
            {
                string key = node.definition?.presentation?.textKey;
                string label = !string.IsNullOrWhiteSpace(key) ? table?.GetEntry(key + ".title")?.Value : null;
                if (!nodeLabels.ContainsKey(node.id))
                    nodeLabels.Add(node.id, string.IsNullOrWhiteSpace(label) ? node.definition != null ? node.definition.name : "빈 노드" : label);
            }
            for (int i = 0; i < errors.Count; i++)
            {
                int separator = errors[i].IndexOf(':');
                if (separator > 0 && nodeLabels.TryGetValue(errors[i].Substring(0, separator), out string label))
                    errors[i] = label + " [" + errors[i].Substring(0, separator) + "]" + errors[i].Substring(separator);
            }
            return errors;
        }
        public static List<string> SaveAndApply(ResearchMapDefinition asset, ResearchTestMap map)
        {
            var errors = GetApplyErrors(asset, map);
            if (errors.Count != 0) return errors;
            if (!AssetDatabase.Contains(asset))
            {
                errors.Add("맵 버전 파일이 없습니다. 새 버전을 만들고 다시 저장해 주세요.");
                return errors;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            try
            {
                bool switchingToTest = asset.isolatedTest && (map.appliedVersion == null || !map.appliedVersion.isolatedTest);
                if (switchingToTest && (map.appliedVersion == null ||
                    FingerprintDefinition(map.appliedVersion) != Fingerprint(map)))
                {
                    var backup = Capture(map, "기존 맵 보관 " + Guid.NewGuid().ToString("N").Substring(0, 8));
                    string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset)).Replace('\\', '/');
                    string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/ResearchMap_BeforeTest.asset");
                    AssetDatabase.CreateAsset(backup, path);
                    AssetDatabase.SaveAssetIfDirty(backup);
                    Undo.RecordObject(map, "기존 연구 맵 보관");
                    map.appliedVersion = backup;
                    map.appliedSceneFingerprint = Fingerprint(map);
                }
                Apply(asset, map);
                AssetDatabase.SaveAssetIfDirty(asset);
                if (!EditorSceneManager.SaveScene(map.gameObject.scene))
                    throw new InvalidOperationException("Main 씬 파일을 저장하지 못했습니다. 파일 권한과 디스크 상태를 확인해 주세요.");
                return errors;
            }
            catch (Exception e)
            {
                Undo.RevertAllDownToGroup(group);
                errors.Add("저장 또는 씬 적용에 실패해 이번 씬 적용을 되돌렸습니다. 맵 버전 파일은 이미 저장되었을 수 있습니다.\n원인: " + e.Message);
                return errors;
            }
        }
        public static List<string> SaveToScene(ResearchMapDefinition asset, string scenePath)
        {
            var errors = Validate(asset);
            if (errors.Count != 0) return errors;
            var scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                        return new List<string> { "Main 씬 파일을 찾을 수 없습니다. 경로를 확인해 주세요: " + scenePath };
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    openedHere = true;
                }
                var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).FirstOrDefault();
                return SaveAndApply(asset, map);
            }
            catch (Exception e) { return new List<string> { "Main 씬을 열거나 저장할 수 없습니다.\n원인: " + e.Message }; }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
        public static void Apply(ResearchMapDefinition asset, ResearchTestMap map)
        {
            string error = CheckApply(asset, map);
            if (error != null) throw new InvalidOperationException(error);
            var nodeParent = map.nodes[0].transform.parent;
            var lineParent = (RectTransform)map.navigation.board.Find("EditableLinks");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("연구 맵 씬에 적용");
            try
            {
                bool switchingIsolated = asset.isolatedTest != (map.appliedVersion != null && map.appliedVersion.isolatedTest);
                Transform archive = map.navigation.board.Find("ResearchMapArchive");
                if (switchingIsolated && archive == null)
                {
                    var archived = new GameObject("ResearchMapArchive", typeof(RectTransform));
                    Undo.RegisterCreatedObjectUndo(archived, "연구 맵 보관 영역");
                    archived.transform.SetParent(map.navigation.board, false);
                    archived.SetActive(false);
                    archive = archived.transform;
                }
                var oldNodes = map.nodes.ToDictionary(x => x.placementId);
                if (archive != null)
                    foreach (var old in archive.GetComponentsInChildren<ResearchTestNode>(true))
                        if (!string.IsNullOrEmpty(old.placementId) && !oldNodes.ContainsKey(old.placementId)) oldNodes.Add(old.placementId, old);
                var result = new Dictionary<string, ResearchTestNode>();
                foreach (var placement in asset.nodes)
                {
                    if (!oldNodes.TryGetValue(placement.id, out var node))
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, nodeParent);
                        Undo.RegisterCreatedObjectUndo(go, "연구 노드 생성");
                        go.name = "Node_" + placement.id;
                        node = go.GetComponent<ResearchTestNode>();
                    }
                    else if (archive != null && node.transform.IsChildOf(archive))
                        Undo.SetTransformParent(node.transform, nodeParent, "연구 노드 복원");
                    Undo.RecordObject(node, "연구 노드 설정");
                    Undo.RecordObject(node.transform, "연구 노드 위치");
                    node.placementId = placement.id; node.definition = placement.definition; node.map = map;
                    node.usePlacementCosts = true; node.placementCosts = (ResearchCost[])placement.costs.Clone();
                    ((RectTransform)node.transform).anchoredPosition = placement.position;
                    if (node.icon != null) { Undo.RecordObject(node.icon, "연구 아이콘"); node.icon.sprite = placement.definition.presentation.icon; PrefabUtility.RecordPrefabInstancePropertyModifications(node.icon); }
                    result.Add(placement.id, node);
                }
                if (asset.isolatedTest)
                {
                    var board = map.navigation.board;
                    float width = Math.Max(board.sizeDelta.x, asset.nodes.Max(x => Math.Abs(x.position.x)) * 2f + 240f);
                    float height = Math.Max(board.sizeDelta.y, asset.nodes.Max(x => Math.Abs(x.position.y)) * 2f + 240f);
                    Undo.RecordObject(board, "테스트 맵 영역 확장");
                    board.sizeDelta = new Vector2(width, height);
                }
                var oldLines = new HashSet<GameObject>();
                var reusableLines = new Dictionary<(string, string), GameObject>();
                foreach (var node in map.nodes.Concat(result.Values.Where(x => archive != null && oldNodes.ContainsKey(x.placementId))).Distinct())
                {
                    if (node.incomingLine != null) oldLines.Add(node.incomingLine);
                    if (node.incomingLines != null) foreach (var line in node.incomingLines) if (line != null) oldLines.Add(line);
                    var prerequisites = node.prerequisites ?? Array.Empty<ResearchTestNode>();
                    for (int i = 0; i < prerequisites.Length; i++)
                    {
                        if (prerequisites[i] == null) continue;
                        GameObject line = node.incomingLines != null && node.incomingLines.Length == prerequisites.Length
                            ? node.incomingLines[i] : prerequisites.Length == 1 ? node.incomingLine : null;
                        if (line != null && (line.transform.parent == lineParent || (archive != null && line.transform.IsChildOf(archive))) && line.transform is RectTransform)
                            reusableLines[(prerequisites[i].placementId, node.placementId)] = line;
                    }
                }
                foreach (var placement in asset.nodes)
                {
                    var node = result[placement.id];
                    node.prerequisites = placement.prerequisites.Select(x => result[x]).ToArray();
                    node.requiredPrerequisites = placement.requiredPrerequisites;
                    node.incomingLine = null;
                    var lines = new List<GameObject>();
                    foreach (var previous in node.prerequisites)
                    {
                        if (!reusableLines.TryGetValue((previous.placementId, node.placementId), out var go))
                        {
                            go = new GameObject("Link_" + previous.placementId + "_to_" + node.placementId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                            Undo.RegisterCreatedObjectUndo(go, "연구 연결 생성");
                            go.transform.SetParent(lineParent, false);
                            ((RectTransform)go.transform).sizeDelta = new Vector2(7f, 100f);
                            var image = go.GetComponent<Image>(); image.color = new Color(.13f, .65f, .95f); image.raycastTarget = false;
                        }
                        else
                        {
                            oldLines.Remove(go);
                            if (go.transform.parent != lineParent) Undo.SetTransformParent(go.transform, lineParent, "연구 연결 복원");
                        }
                        var rect = (RectTransform)go.transform;
                        Undo.RecordObject(rect, "연구 연결 위치");
                        Vector2 a = lineParent.InverseTransformPoint(previous.transform.position);
                        Vector2 b = lineParent.InverseTransformPoint(node.transform.position);
                        var delta = b - a;
                        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                        rect.anchoredPosition = (a + b) * .5f;
                        rect.sizeDelta = new Vector2(rect.sizeDelta.x > 0 ? rect.sizeDelta.x : 7f, Mathf.Max(1f, delta.magnitude - 92f));
                        rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg);
                        lines.Add(go);
                    }
                    node.incomingLines = lines.ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(node);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(node.transform);
                }
                foreach (var line in oldLines)
                {
                    if (switchingIsolated) Undo.SetTransformParent(line.transform, archive, "연구 연결 보관");
                    else Undo.DestroyObjectImmediate(line);
                }
                foreach (var old in map.nodes)
                    if (old != null && !result.ContainsKey(old.placementId))
                    {
                        if (switchingIsolated) Undo.SetTransformParent(old.transform, archive, "연구 노드 보관");
                        else Undo.DestroyObjectImmediate(old.gameObject);
                    }
                Undo.RecordObject(map, "연구 맵 적용");
                map.nodes = asset.nodes.Select(x => result[x.id]).ToArray();
                map.appliedVersion = asset;
                map.appliedSceneFingerprint = Fingerprint(map);
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
    }
}
