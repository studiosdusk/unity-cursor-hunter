using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CursorHunter.Progression.ResearchPrototype;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace CursorHunter.Progression.Editor
{
    public sealed class ResearchMapEditorWindow : EditorWindow
    {
        const string Folder = "Assets/CursorHunter/Progression/ResearchPrototype/Maps";
        static readonly string[] CurrencyIds = { "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };
        static readonly string[] CurrencyNames = { "가넷", "토파즈", "자수정", "사파이어", "다이아몬드", "드래곤 스톤" };
        static readonly string[] CurrencyChoices = new[] { "선택해 주세요" }.Concat(CurrencyNames).ToArray();
        static readonly string[] AllCurrencyChoices = new[] { "재화 선택" }.Concat(CurrencyNames).ToArray();
        ResearchMapDefinition map;
        string selectedId, connectingId, status = "버전을 선택하거나 씬에서 현재 맵을 가져오세요.";
        Vector2 pan, scroll;
        float zoom = 1f;
        bool draggingNode, panning;
        bool dragUndoRecorded;
        bool clearNodeInputFocus;
        bool snap = true;
        bool hasCopiedCost;
        ResearchCost copiedCost;
        Rect canvas;
        ResearchMapDefinition[] versions;
        string[] versionLabels;
        ResearchDefinition[] definitions;
        string[] definitionLabels;
        readonly Dictionary<ResearchDefinition, string> names = new Dictionary<ResearchDefinition, string>();
        bool cacheInvalid = true;
        const float PanelWidth = 310f;
        [MenuItem("Tools/Cursor Hunter/연구 맵 에디터")]
        public static void Open() { var window = GetWindow<ResearchMapEditorWindow>(); window.titleContent = new GUIContent("연구 맵 에디터"); window.minSize = new Vector2(800, 500); }
        void OnEnable()
        {
            EditorApplication.projectChanged += InvalidateCache;
            Undo.undoRedoPerformed += InvalidateCache;
            cacheInvalid = true;
            if (map == null) map = AssetDatabase.LoadAssetAtPath<ResearchMapDefinition>(Folder + "/ResearchMap_v1.asset");
            if (map != null) status = "기존 맵 v1을 불러왔습니다. 노드 우클릭: 연결·복제·삭제 / 빈 공간 우클릭: 생성";
        }
        void OnDisable()
        {
            EditorApplication.projectChanged -= InvalidateCache;
            Undo.undoRedoPerformed -= InvalidateCache;
        }
        void OnFocus() => InvalidateCache();
        void InvalidateCache() { cacheInvalid = true; Repaint(); }
        internal static void InvalidateOpenWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<ResearchMapEditorWindow>()) window.InvalidateCache();
        }
        void EnsureCache()
        {
            if (!cacheInvalid && versions != null && definitions != null) return;
            cacheInvalid = false;
            names.Clear();
            versions = AssetDatabase.FindAssets("t:ResearchMapDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ResearchMapDefinition>).Where(x => x != null).OrderBy(x => x.versionId).ToArray();
            RefreshVersionLabels();
            var table = LocalizationEditorSettings.GetStringTableCollection("Research")?.GetTable("ko") as StringTable;
            definitions = AssetDatabase.FindAssets("t:ResearchDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ResearchDefinition>).Where(x => x != null).ToArray();
            foreach (var definition in definitions)
            {
                string key = definition.presentation?.textKey;
                names[definition] = string.IsNullOrWhiteSpace(key) ? definition.name :
                    table?.GetEntry(key + ".title")?.Value ?? definition.name;
            }
            definitions = definitions.OrderBy(NameOf).ThenBy(x => x.stableId).ToArray();
            definitionLabels = new[] { "선택해 주세요" }.Concat(definitions.Select(NameOf)).ToArray();
        }
        void RefreshVersionLabels()
        {
            versionLabels = new[] { "버전 선택" }.Concat(versions.Select(x => x.versionId + "  (" + x.name + ")")).ToArray();
        }

        void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("연구 맵을 편집하려면 플레이를 종료해 주세요.", MessageType.Info);
                return;
            }
            if ((cacheInvalid && Event.current.type == EventType.Layout) || versions == null || definitions == null) EnsureCache();
            GUILayout.BeginArea(new Rect(0, 0, position.width, 20));
            DrawToolbar();
            GUILayout.EndArea();
            if (map == null)
            {
                EditorGUI.HelpBox(new Rect(4, 24, position.width - 8, 85), "1. '씬에서 가져오기'로 현재 연구 맵을 버전으로 저장하거나 '새 버전'을 만드세요.\n2. 빈 노드에 연구 SO와 레벨별 비용을 설정하세요.\n3. 노드를 우클릭해 다른 노드에 연결하세요.", MessageType.Info);
                return;
            }
            EditorGUI.LabelField(new Rect(4, 20, Mathf.Max(0, position.width - 8), 19), status, EditorStyles.miniLabel);
            // GUILayoutUtility.GetRect returns a placeholder during Layout. The
            // scroll view needs the same, real bounds during Layout and Repaint.
            Rect area = new Rect(0, 40, position.width, Mathf.Max(0, position.height - 40));
            canvas = new Rect(area.x, area.y, Mathf.Max(100, area.width - PanelWidth), area.height);
            Rect inspector = new Rect(canvas.xMax + 1, area.y, PanelWidth - 1, area.height);
            DrawCanvas();
            if (clearNodeInputFocus)
            {
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                EditorGUIUtility.editingTextField = false;
                clearNodeInputFocus = false;
            }
            DrawInspector(inspector);
        }
        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int current = Array.IndexOf(versions, map);
            int next = EditorGUILayout.Popup(current + 1, versionLabels, EditorStyles.toolbarPopup, GUILayout.Width(220)) - 1;
            if (next != current) { map = next >= 0 ? versions[next] : null; SelectNode(null); clearNodeInputFocus = true; connectingId = null; }
            if (GUILayout.Button("씬에서 가져오기", EditorStyles.toolbarButton)) CaptureScene();
            if (GUILayout.Button("새 버전", EditorStyles.toolbarButton)) NewVersion(false);
            using (new EditorGUI.DisabledScope(map == null))
            {
                if (GUILayout.Button("버전 복제", EditorStyles.toolbarButton)) NewVersion(true);
                if (GUILayout.Button("저장", EditorStyles.toolbarButton)) SaveAndApply();
                if (GUILayout.Button("검증", EditorStyles.toolbarButton)) ValidateMap();
                if (GUILayout.Button("씬과 비교", EditorStyles.toolbarButton)) CompareScene();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        void CaptureScene()
        {
            try
            {
                var sceneMap = ResearchMapEditorService.FindMap();
                if (sceneMap == null) throw new InvalidOperationException("Main 씬을 먼저 열어 주세요.");
                var captured = ResearchMapEditorService.Capture(sceneMap, NextVersion());
                SaveNew(captured);
                Undo.RecordObject(sceneMap, "씬 연구 맵 변경사항 보관");
                sceneMap.appliedVersion = captured;
                sceneMap.appliedSceneFingerprint = ResearchMapEditorService.Fingerprint(sceneMap);
                EditorSceneManager.MarkSceneDirty(sceneMap.gameObject.scene);
                status = "씬의 현재 구성으로 새 버전을 만들었습니다.";
            }
            catch (Exception ex) { EditorUtility.DisplayDialog("가져오기 실패", ex.Message, "확인"); }
        }
        string NextVersion()
        {
            var versionNames = versions.Where(x => x != null).Select(x => x.versionId).ToHashSet();
            int number = 1; while (versionNames.Contains("v" + number)) number++;
            return "v" + number;
        }
        void NewVersion(bool copy)
        {
            var created = copy ? Instantiate(map) : CreateInstance<ResearchMapDefinition>();
            created.versionId = NextVersion();
            if (!copy) created.nodes = new List<ResearchMapPlacement>();
            SaveNew(created); status = copy ? "기존 버전을 복제했습니다. 노드 ID는 유지됩니다." : "빈 버전을 만들었습니다. 캔버스를 우클릭해 노드를 추가하세요.";
        }
        void SaveNew(ResearchMapDefinition created)
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/ResearchMap_" + created.versionId + ".asset");
            AssetDatabase.CreateAsset(created, path); AssetDatabase.SaveAssetIfDirty(created);
            map = created; SelectNode(null); clearNodeInputFocus = true; connectingId = null; InvalidateCache();
        }
        void ValidateMap()
        {
            var target = ResearchMapEditorService.FindMap();
            var errors = target != null ? ResearchMapEditorService.GetApplyErrors(map, target) : ResearchMapEditorService.Validate(map);
            if (errors.Count == 0) EditorUtility.DisplayDialog("검증 완료", target != null
                ? "오류가 없습니다. 저장하면 씬에 바로 적용됩니다."
                : "맵 데이터에 오류가 없습니다. 저장할 때 Main 씬도 자동으로 확인합니다.", "확인");
            else ResearchMapErrorsWindow.Open(errors);
        }
        void CompareScene()
        {
            var sceneMap = ResearchMapEditorService.FindMap();
            if (sceneMap == null) { EditorUtility.DisplayDialog("씬을 찾을 수 없습니다", "Main 씬을 먼저 열어 주세요.", "확인"); return; }
            ResearchMapDefinition scene = null;
            try
            {
                scene = ResearchMapEditorService.Capture(sceneMap, "scene");
                var before = scene.nodes.ToDictionary(x => x.id);
                var after = map.nodes.Where(x => x != null && !string.IsNullOrEmpty(x.id)).GroupBy(x => x.id).ToDictionary(x => x.Key, x => x.First());
                var lines = new List<string>();
                foreach (var id in before.Keys.Except(after.Keys)) lines.Add("삭제: " + id);
                foreach (var id in after.Keys.Except(before.Keys)) lines.Add("추가: " + id);
                foreach (var id in before.Keys.Intersect(after.Keys))
                {
                    var a = before[id]; var b = after[id];
                    var changes = new List<string>();
                    if (a.definition != b.definition) changes.Add("연구 종류");
                    if (a.position != b.position) changes.Add("위치");
                    if (a.requiredPrerequisites != b.requiredPrerequisites || !a.prerequisites.OrderBy(x => x).SequenceEqual(b.prerequisites.OrderBy(x => x))) changes.Add("선행 조건");
                    if (a.costs == null || b.costs == null || a.costs.Length != b.costs.Length ||
                        a.costs.Where((x, i) => i >= b.costs.Length || x.currencyId != b.costs[i].currencyId || x.amount != b.costs[i].amount).Any()) changes.Add("비용");
                    if (changes.Count > 0) lines.Add(id + ": " + string.Join(", ", changes));
                }
                EditorUtility.DisplayDialog("씬과 버전 비교", lines.Count == 0 ? "차이가 없습니다." : string.Join("\n", lines), "확인");
            }
            catch (Exception ex) { EditorUtility.DisplayDialog("비교 실패", ex.Message, "확인"); }
            finally { if (scene != null) DestroyImmediate(scene); }
        }
        void SaveAndApply()
        {
            try
            {
                var errors = ResearchMapEditorService.SaveToScene(map, ResearchMapEditorService.MainScene);
                if (errors.Count > 0) { status = "저장하지 못했습니다. 오류 목록을 확인해 주세요."; ResearchMapErrorsWindow.Open(errors); return; }
                status = "검증을 마쳤습니다. 맵 버전과 Main 씬을 저장했습니다.";
            }
            catch (Exception ex) { status = "저장하지 못했습니다."; ResearchMapErrorsWindow.Open(new[] { "저장 중 오류가 발생했습니다.\n원인: " + ex.Message }); }
        }
        Vector2 ToScreen(Vector2 point) => canvas.center + pan + new Vector2(point.x, -point.y) * zoom;
        Vector2 ToMap(Vector2 point) { Vector2 p = (point - canvas.center - pan) / zoom; return new Vector2(p.x, -p.y); }
        Rect NodeRect(ResearchMapPlacement n) { Vector2 p = ToScreen(n.position); return new Rect(p.x - 55 * zoom, p.y - 31 * zoom, 110 * zoom, 62 * zoom); }
        void DrawCanvas()
        {
            if (Event.current.type != EventType.Repaint) { HandleCanvasEvents(); return; }
            EditorGUI.DrawRect(canvas, new Color(.12f, .15f, .2f));
            GUI.BeginClip(canvas);
            Rect local = new Rect(0, 0, canvas.width, canvas.height);
            EditorGUI.DrawRect(local, new Color(.12f, .15f, .2f));
            float grid = 40 * zoom;
            Handles.BeginGUI(); Handles.color = new Color(1, 1, 1, .06f);
            for (float x = (canvas.width * .5f + pan.x) % grid; x < canvas.width; x += grid) Handles.DrawLine(new Vector3(x, 0), new Vector3(x, canvas.height));
            for (float y = (canvas.height * .5f + pan.y) % grid; y < canvas.height; y += grid) Handles.DrawLine(new Vector3(0, y), new Vector3(canvas.width, y));
            var lookup = map.nodes.Where(x => x != null && !string.IsNullOrEmpty(x.id)).GroupBy(x => x.id).ToDictionary(x => x.Key, x => x.First());
            foreach (var node in map.nodes.Where(x => x != null))
                foreach (var id in node.prerequisites ?? new List<string>())
                    if (id != null && lookup.TryGetValue(id, out var source))
                    {
                        Vector2 a = ToScreen(source.position) - canvas.position, b = ToScreen(node.position) - canvas.position;
                        if (Mathf.Max(a.x, b.x) < 0 || Mathf.Min(a.x, b.x) > canvas.width ||
                            Mathf.Max(a.y, b.y) < 0 || Mathf.Min(a.y, b.y) > canvas.height) continue;
                        Vector2 dir = (b - a).normalized; Vector2 start = a + dir * (35 * zoom), end = b - dir * (35 * zoom);
                        Handles.color = new Color(.25f, .75f, 1f);
                        Handles.DrawAAPolyLine(3f, start, end);
                        Vector2 side = new Vector2(-dir.y, dir.x) * 5f * zoom;
                        Handles.DrawAAConvexPolygon(end, end - dir * (10 * zoom) + side, end - dir * (10 * zoom) - side);
                    }
            Handles.EndGUI();
            foreach (var node in map.nodes.Where(x => x != null))
            {
                Rect r = NodeRect(node); r.position -= canvas.position;
                if (!r.Overlaps(local)) continue;
                EditorGUI.DrawRect(r, node.id == selectedId ? new Color(.3f, .55f, .7f) : new Color(.25f, .32f, .39f));
                if (node.definition != null && node.definition.icon != null)
                {
                    var sprite = node.definition.icon; Rect texture = sprite.textureRect;
                    GUI.DrawTextureWithTexCoords(new Rect(r.x + 4, r.y + 5, 28 * zoom, 28 * zoom), sprite.texture,
                        new Rect(texture.x / sprite.texture.width, texture.y / sprite.texture.height, texture.width / sprite.texture.width, texture.height / sprite.texture.height));
                }
                GUI.Label(new Rect(r.x + 4, r.y + 34 * zoom, r.width - 8, 23 * zoom), node.definition == null ? "빈 노드" : NameOf(node.definition), EditorStyles.whiteMiniLabel);
            }
            GUI.EndClip();
            HandleCanvasEvents();
        }
        void HandleCanvasEvents()
        {
            Event e = Event.current;
            if (e.type == EventType.MouseUp) { draggingNode = panning = dragUndoRecorded = false; return; }
            if (!canvas.Contains(e.mousePosition) && !draggingNode && !panning) return;
            if (e.type == EventType.ScrollWheel)
            {
                Vector2 before = ToMap(e.mousePosition); zoom = Mathf.Clamp(zoom * (e.delta.y < 0 ? 1.1f : 1f / 1.1f), .35f, 2f);
                Vector2 after = ToScreen(before); pan += e.mousePosition - after; e.Use(); Repaint(); return;
            }
            if (e.type == EventType.MouseDown)
            {
                var hit = map.nodes.LastOrDefault(x => x != null && NodeRect(x).Contains(e.mousePosition));
                if (e.button == 2 || (e.button == 0 && hit == null && string.IsNullOrEmpty(connectingId)))
                { panning = true; e.Use(); return; }
                if (e.button == 0 && hit != null)
                {
                    if (!string.IsNullOrEmpty(connectingId))
                    {
                        var source = connectingId; connectingId = null; Connect(source, hit.id);
                    }
                    SelectNode(hit.id); draggingNode = true; dragUndoRecorded = false; e.Use(); Repaint(); return;
                }
                if (e.button == 1)
                {
                    var menu = new GenericMenu(); Vector2 position = ToMap(e.mousePosition);
                    if (hit == null) menu.AddItem(new GUIContent("빈 노드 생성"), false, () => AddNode(position));
                    else
                    {
                        SelectNode(hit.id);
                        if (definitions.Length == 0)
                            menu.AddDisabledItem(new GUIContent("연구 주입/사용 가능한 연구가 없습니다"));
                        foreach (var definition in definitions)
                        {
                            var selectedDefinition = definition;
                            menu.AddItem(new GUIContent("연구 주입/" + NameOf(definition) + " (" + definition.stableId + ")"),
                                definition == hit.definition, () => AssignDefinition(hit, selectedDefinition));
                        }
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("다른 노드에 연결"), false, () => { connectingId = hit.id; status = "연결할 후행 노드를 클릭하세요. Esc로 취소합니다."; });
                        menu.AddItem(new GUIContent("노드 복제"), false, () => Duplicate(hit));
                        menu.AddItem(new GUIContent("노드 삭제"), false, () => Delete(hit));
                    }
                    menu.ShowAsContext(); e.Use(); Repaint(); return;
                }
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { connectingId = null; status = "연결을 취소했습니다."; e.Use(); Repaint(); return; }
            if (e.type == EventType.MouseDrag && panning) { pan += e.delta; e.Use(); Repaint(); }
            else if (e.type == EventType.MouseDrag && draggingNode)
            {
                var node = Selected(); if (node == null) return;
                if (!dragUndoRecorded) { Undo.RegisterCompleteObjectUndo(map, "연구 노드 이동"); dragUndoRecorded = true; }
                node.position += new Vector2(e.delta.x, -e.delta.y) / zoom;
                EditorUtility.SetDirty(map); e.Use(); Repaint();
            }
        }
        ResearchMapPlacement Selected() => map.nodes.FirstOrDefault(x => x != null && x.id == selectedId);
        void SelectNode(string id)
        {
            if (selectedId == id) return;
            selectedId = id;
            // IMGUI reuses numeric field control IDs between nodes. Release the
            // previous field before drawing another node's values.
            clearNodeInputFocus = true;
            Repaint();
        }
        void AddNode(Vector2 position)
        {
            Undo.RecordObject(map, "빈 연구 노드 생성");
            if (snap) position = new Vector2(Mathf.Round(position.x / 20) * 20, Mathf.Round(position.y / 20) * 20);
            var node = new ResearchMapPlacement { id = "node." + Guid.NewGuid().ToString("N"), position = position };
            map.nodes.Add(node); SelectNode(node.id); EditorUtility.SetDirty(map); Repaint();
        }
        void Duplicate(ResearchMapPlacement original)
        {
            Undo.RecordObject(map, "연구 노드 복제");
            var copy = new ResearchMapPlacement { id = "node." + Guid.NewGuid().ToString("N"), definition = original.definition,
                position = original.position + new Vector2(140, 0), costs = (ResearchCost[])original.costs.Clone() };
            map.nodes.Add(copy); SelectNode(copy.id); EditorUtility.SetDirty(map);
        }
        void Delete(ResearchMapPlacement node)
        {
            if (!EditorUtility.DisplayDialog("노드 삭제", "이 버전에서 노드와 연결을 삭제합니다. 기존 구매 기록에 영향을 주는 삭제는 씬에 적용할 수 없습니다.", "삭제", "취소")) return;
            Undo.RecordObject(map, "연구 노드 삭제");
            map.nodes.Remove(node);
            foreach (var n in map.nodes) if (n.prerequisites.Remove(node.id)) n.requiredPrerequisites = Mathf.Min(n.requiredPrerequisites, n.prerequisites.Count);
            SelectNode(null); EditorUtility.SetDirty(map);
        }
        void Connect(string from, string to)
        {
            var target = map.nodes.First(x => x.id == to);
            if (from == to || target.prerequisites.Contains(from)) { status = "같은 노드나 이미 연결된 노드는 연결할 수 없습니다."; return; }
            Undo.RecordObject(map, "연구 노드 연결");
            target.prerequisites.Add(from); target.requiredPrerequisites = Mathf.Max(1, target.requiredPrerequisites);
            if (map.Validate().Any(x => x.Contains("순환")))
            { target.prerequisites.Remove(from); if (target.prerequisites.Count == 0) target.requiredPrerequisites = 0; status = "순환 연결은 만들 수 없습니다."; }
            else status = "선행 노드를 연결했습니다.";
            EditorUtility.SetDirty(map);
        }
        string NameOf(ResearchDefinition definition)
        {
            if (definition == null) return "";
            return names.TryGetValue(definition, out var name) ? name : definition.name;
        }

        void AssignDefinition(ResearchMapPlacement node, ResearchDefinition definition)
        {
            if (node.definition == definition) return;
            int newCount = definition == null ? 0 : definition.ranks.Length;
            if (node.costs != null && node.costs.Length > newCount &&
                !EditorUtility.DisplayDialog("비용 행 변경", "새 SO의 최대 레벨이 더 작습니다. 초과하는 비용 행이 삭제됩니다.", "변경", "취소")) return;
            Undo.RecordObject(map, "연구 종류 선택");
            node.definition = definition;
            Array.Resize(ref node.costs, newCount);
            EditorUtility.SetDirty(map);
            status = definition == null ? "연구 종류를 해제했습니다." : NameOf(definition) + "을(를) 주입했습니다. 레벨별 비용을 확인해 주세요.";
            Repaint();
        }
        void DrawInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("연구 맵 설정", EditorStyles.boldLabel);
            if (map.isolatedTest)
                EditorGUILayout.HelpBox("전체 노드 테스트 맵입니다. 구매는 무료이며 기존 맵의 구매 기록과 구분됩니다. 저장 시 기존 맵은 보관됩니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            string version = EditorGUILayout.TextField("버전 이름", map.versionId);
            string description = EditorGUILayout.TextField("설명", map.description);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(map, "버전 정보 수정"); map.versionId = version; map.description = description; EditorUtility.SetDirty(map); RefreshVersionLabels(); }
            snap = EditorGUILayout.Toggle("생성 시 격자 맞춤", snap);
            if (GUILayout.Button("맵 중앙 보기")) { pan = Vector2.zero; zoom = 1f; }
            EditorGUILayout.Space();
            var n = Selected();
            if (n == null) { EditorGUILayout.HelpBox("노드를 선택하세요. 빈 공간을 우클릭하면 노드를 만들 수 있습니다.", MessageType.Info); }
            else
            {
                EditorGUILayout.LabelField("선택한 노드", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(n.id, EditorStyles.miniLabel, GUILayout.Height(18));
                EditorGUI.BeginChangeCheck();
                Vector2 position = EditorGUILayout.Vector2Field("위치", n.position);
                if (EditorGUI.EndChangeCheck() &&
                    !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
                    !float.IsNaN(position.y) && !float.IsInfinity(position.y))
                {
                    Undo.RecordObject(map, "연구 노드 좌표 수정");
                    n.position = position;
                    EditorUtility.SetDirty(map);
                    Repaint();
                }
                int chosen = Array.IndexOf(definitions, n.definition);
                EditorGUI.BeginChangeCheck();
                int picked = EditorGUILayout.Popup("연구 종류", chosen + 1, definitionLabels) - 1;
                var definition = picked < 0 ? null : definitions[picked];
                if (EditorGUI.EndChangeCheck())
                    AssignDefinition(n, definition);
                if (n.definition != null)
                {
                    EditorGUILayout.LabelField(NameOf(n.definition), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("최대 레벨", n.definition.ranks.Length.ToString());
                    if (n.costs == null || n.costs.Length != n.definition.ranks.Length)
                        EditorGUILayout.HelpBox("비용 행 수가 최대 레벨과 다릅니다. SO를 다시 선택해 주세요.", MessageType.Warning);
                    else for (int i = 0; i < n.costs.Length; i++)
                    {
                        EditorGUILayout.Space(3);
                        EditorGUILayout.LabelField((i + 1) + "레벨  |  증가: " + n.definition.ranks[i].levelDelta + "  |  적용: " + n.definition.GetEffectValue(i + 1), EditorStyles.miniBoldLabel);
                        EditorGUI.BeginChangeCheck();
                        int index = Array.IndexOf(CurrencyIds, n.costs[i].currencyId);
                        int selected = EditorGUILayout.Popup("젬스톤", index + 1, CurrencyChoices) - 1;
                        long amount = EditorGUILayout.LongField("비용", n.costs[i].amount);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(map, "연구 비용 수정"); var c = n.costs[i];
                            c.currencyId = selected < 0 ? "" : CurrencyIds[selected]; c.amount = amount; n.costs[i] = c; EditorUtility.SetDirty(map);
                        }
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("이 비용 복사")) { copiedCost = n.costs[i]; hasCopiedCost = true; status = "비용을 복사했습니다."; }
                        using (new EditorGUI.DisabledScope(!hasCopiedCost))
                            if (GUILayout.Button("복사한 비용 붙여넣기"))
                            { Undo.RecordObject(map, "연구 비용 붙여넣기"); n.costs[i] = copiedCost; EditorUtility.SetDirty(map); }
                        EditorGUILayout.EndHorizontal();
                    }
                    if (n.costs != null && n.costs.Length == n.definition.ranks.Length)
                    {
                        int all = EditorGUILayout.Popup("전체 레벨 재화 변경", 0, AllCurrencyChoices);
                        if (all > 0)
                        {
                            Undo.RecordObject(map, "전체 레벨 재화 변경");
                            for (int i = 0; i < n.costs.Length; i++) { var c = n.costs[i]; c.currencyId = CurrencyIds[all - 1]; n.costs[i] = c; }
                            EditorUtility.SetDirty(map);
                        }
                    }
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("선행 노드 (1레벨 이상)", EditorStyles.boldLabel);
                foreach (var id in n.prerequisites.ToArray())
                {
                    EditorGUILayout.BeginHorizontal();
                    var p = map.nodes.FirstOrDefault(x => x.id == id);
                    EditorGUILayout.LabelField(p?.definition != null ? NameOf(p.definition) : id);
                    if (GUILayout.Button("연결 삭제", GUILayout.Width(70)))
                    { Undo.RecordObject(map, "연구 연결 삭제"); n.prerequisites.Remove(id); n.requiredPrerequisites = Mathf.Min(n.requiredPrerequisites, n.prerequisites.Count); EditorUtility.SetDirty(map); }
                    EditorGUILayout.EndHorizontal();
                }
                if (n.prerequisites.Count > 0)
                {
                    int count = EditorGUILayout.IntSlider("필요한 선행 수", n.requiredPrerequisites, 1, n.prerequisites.Count);
                    if (count != n.requiredPrerequisites) { Undo.RecordObject(map, "선행 조건 수정"); n.requiredPrerequisites = count; EditorUtility.SetDirty(map); }
                }
                else EditorGUILayout.HelpBox("선행 노드가 없어 처음부터 해금됩니다.", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
