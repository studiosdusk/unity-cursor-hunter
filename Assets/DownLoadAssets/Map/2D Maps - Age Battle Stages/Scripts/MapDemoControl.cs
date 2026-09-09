using UnityEngine;

namespace LayerLab
{
/// <summary>
/// Age Battle Stages 데모에서 맵 prefab을 전환하고 간단한 IMGUI 버튼을 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MapDemoControl : MonoBehaviour
{
    private const float ReferenceUiWidth = 1280f;
    private const float ReferenceUiHeight = 720f;
    private const KeyCode ToggleUiKey = KeyCode.H;
    private const string ToggleUiHintVisible = "H: UI 숨김";
    private const string ToggleUiHintHidden = "H: UI 표시";
    private const string MapSwitchHint = "좌클릭 / 우클릭 또는 A/D: 맵 전환";

    [Header("Maps")]
    [Tooltip("하단 버튼 순서대로 전환할 맵 prefab 목록입니다.")]
    [SerializeField] private GameObject[] mapPrefabs;
    [Tooltip("새 맵 prefab을 생성할 위치입니다. 씬에 이미 맵이 있으면 그 위치를 자동으로 사용합니다.")]
    [SerializeField] private Vector3 mapPosition = Vector3.zero;
    [Tooltip("데모 시작 시 첫 번째 맵을 자동 생성합니다. 씬에 이미 맵이 있으면 생성하지 않습니다.")]
    [SerializeField] private bool loadFirstMapOnStart = true;
    [Tooltip("씬에 이미 배치된 시작 맵입니다. 비워두면 이름으로 자동 탐색합니다.")]
    [SerializeField] private GameObject sceneInitialMap;

    [Header("Input")]
    [Tooltip("마우스 좌클릭은 다음 맵, 우클릭은 이전 맵으로 전환합니다.")]
    [SerializeField] private bool enableMouseClickSwitch = true;

    [HideInInspector, SerializeField] private Vector2 buttonSize = new Vector2(48f, 28f);
    [HideInInspector, SerializeField] private float buttonGap = 6f;
    [HideInInspector, SerializeField] private float bottomBarHeight = 74f;
    [HideInInspector, SerializeField] private Color panelColor = new Color(0.03f, 0.04f, 0.04f, 0.58f);
    [HideInInspector, SerializeField] private Color buttonColor = new Color(0.15f, 0.18f, 0.19f, 0.78f);
    [HideInInspector, SerializeField] private Color selectedButtonColor = new Color(0.05f, 0.07f, 0.08f, 0.92f);
    [HideInInspector, SerializeField] private bool showUi = true;

    private GameObject currentMap;
    private int currentMapIndex = -1;
    private GUIStyle panelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;
    private GUIStyle hintStyle;
    private GUIStyle hintBoxStyle;
    private float currentUiScale = 1f;

    private void Start()
    {
        CaptureSceneInitialMap();

        if (currentMap == null && loadFirstMapOnStart && HasMapPrefabs())
        {
            LoadMap(0);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleUiKey))
        {
            showUi = !showUi;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            LoadAdjacentMap(-1);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            LoadAdjacentMap(1);
        }

        if (enableMouseClickSwitch && !IsPointerOverBottomBar())
        {
            if (Input.GetMouseButtonDown(0))
            {
                LoadAdjacentMap(1);
            }
            else if (Input.GetMouseButtonDown(1))
            {
                LoadAdjacentMap(-1);
            }
        }
    }

    private void OnGUI()
    {
        if (!showUi || !HasMapPrefabs())
        {
            if (!showUi)
            {
                DrawHiddenUiHint();
            }

            return;
        }

        EnsureGuiStyles();

        Matrix4x4 previousMatrix = GUI.matrix;
        currentUiScale = GetUiScale();
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(currentUiScale, currentUiScale, 1f));

        try
        {
            Rect bottomBarRect = GetBottomBarRect();
            DrawHintAbove(bottomBarRect);
            DrawBottomBar(bottomBarRect);
        }
        finally
        {
            GUI.matrix = previousMatrix;
            currentUiScale = 1f;
        }
    }

    /// <summary>
    /// 현재 맵을 제거하고 지정한 index의 prefab을 생성합니다.
    /// </summary>
    public void LoadMap(int index)
    {
        if (!IsValidMapIndex(index))
        {
            return;
        }

        if (currentMap != null)
        {
            Destroy(currentMap);
        }

        GameObject prefab = mapPrefabs[index];
        currentMap = Instantiate(prefab, mapPosition, prefab.transform.rotation);
        currentMap.name = prefab.name;
        currentMapIndex = index;
    }

    private void LoadAdjacentMap(int direction)
    {
        if (!HasMapPrefabs())
        {
            return;
        }

        int index = currentMapIndex >= 0 ? currentMapIndex : 0;
        index = (index + direction + mapPrefabs.Length) % mapPrefabs.Length;
        LoadMap(index);
    }

    private void CaptureSceneInitialMap()
    {
        if (currentMap != null)
        {
            return;
        }

        if (sceneInitialMap != null)
        {
            ConfigureExistingMap(sceneInitialMap);
            return;
        }

        GameObject sceneMap = FindSceneMapByPrefabName();
        if (sceneMap != null)
        {
            ConfigureExistingMap(sceneMap);
        }
    }

    private void ConfigureExistingMap(GameObject map)
    {
        currentMap = map;
        mapPosition = map.transform.position;
        currentMapIndex = FindMapPrefabIndex(map.name);
    }

    private GameObject FindSceneMapByPrefabName()
    {
        if (!HasMapPrefabs())
        {
            return null;
        }

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        GameObject exactMatch = FindSceneMap(transforms, true);
        return exactMatch != null ? exactMatch : FindSceneMap(transforms, false);
    }

    private GameObject FindSceneMap(Transform[] transforms, bool exactNameOnly)
    {
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform target = transforms[i];
            if (target == transform || target.GetComponentInParent<MapDemoControl>() != null)
            {
                continue;
            }

            if (target.parent != null)
            {
                continue;
            }

            int index = exactNameOnly ? FindExactMapPrefabIndex(target.name) : FindMapPrefabIndex(target.name);
            if (index >= 0)
            {
                return target.gameObject;
            }
        }

        return null;
    }

    private int FindExactMapPrefabIndex(string mapName)
    {
        if (!HasMapPrefabs())
        {
            return -1;
        }

        string normalizedName = RemoveCloneSuffix(mapName);
        for (int i = 0; i < mapPrefabs.Length; i++)
        {
            if (mapPrefabs[i] != null && mapPrefabs[i].name == normalizedName)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindMapPrefabIndex(string mapName)
    {
        if (!HasMapPrefabs())
        {
            return -1;
        }

        string normalizedName = NormalizeMapName(mapName);
        for (int i = 0; i < mapPrefabs.Length; i++)
        {
            if (mapPrefabs[i] == null)
            {
                continue;
            }

            string prefabName = NormalizeMapName(mapPrefabs[i].name);
            if (prefabName == normalizedName || normalizedName.StartsWith(prefabName + " ("))
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsPointerOverBottomBar()
    {
        if (!showUi || !HasMapPrefabs())
        {
            return false;
        }

        return Input.mousePosition.y <= GetBottomBarHeight() * GetUiScale();
    }

    private void DrawHiddenUiHint()
    {
        EnsureGuiStyles();
        currentUiScale = GetUiScale();

        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(currentUiScale, currentUiScale, 1f));

        try
        {
            Rect rect = new Rect(12f, 12f, 116f, 28f);
            GUI.Box(rect, GUIContent.none, hintBoxStyle);
            GUI.Label(rect, ToggleUiHintHidden, hintStyle);
        }
        finally
        {
            GUI.matrix = previousMatrix;
            currentUiScale = 1f;
        }
    }

    private void DrawBottomBar(Rect bottomBarRect)
    {
        GUI.Box(bottomBarRect, GUIContent.none, panelStyle);
        DrawMapButtons(bottomBarRect);
    }

    private void DrawMapButtons(Rect bottomBarRect)
    {
        int buttonCount = mapPrefabs.Length;
        float margin = 12f;
        float panelWidth = Mathf.Max(1f, GetScaledScreenWidth() - margin * 2f);
        int columns = GetMapButtonColumnCount(panelWidth, buttonCount);
        float availableWidth = panelWidth - buttonGap * (columns + 1);
        float width = Mathf.Max(1f, availableWidth / columns);
        float startX = margin + buttonGap;
        float startY = bottomBarRect.y + 12f;

        for (int i = 0; i < buttonCount; i++)
        {
            int column = i % columns;
            int row = i / columns;
            Rect rect = new Rect(
                startX + column * (width + buttonGap),
                startY + row * (buttonSize.y + buttonGap),
                width,
                buttonSize.y);

            GUIStyle style = i == currentMapIndex ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(rect, (i + 1).ToString(), style))
            {
                LoadMap(i);
            }
        }
    }

    private void DrawHintAbove(Rect bottomBarRect)
    {
        GUIContent toggleContent = new GUIContent(ToggleUiHintVisible);
        GUIContent mapContent = new GUIContent(MapSwitchHint);
        Vector2 toggleSize = hintStyle.CalcSize(toggleContent);
        Vector2 mapSize = hintStyle.CalcSize(mapContent);
        float width = Mathf.Max(toggleSize.x, mapSize.x) + 22f;
        float height = toggleSize.y + mapSize.y + 10f;
        Rect rect = new Rect(12f, Mathf.Max(8f, bottomBarRect.y - height - 8f), width, height);
        Rect toggleRect = new Rect(rect.x, rect.y + 3f, rect.width, toggleSize.y);
        Rect mapRect = new Rect(rect.x, toggleRect.yMax + 1f, rect.width, mapSize.y);

        GUI.Box(rect, GUIContent.none, hintBoxStyle);
        GUI.Label(toggleRect, toggleContent, hintStyle);
        GUI.Label(mapRect, mapContent, hintStyle);
    }

    private Rect GetBottomBarRect()
    {
        float height = GetBottomBarHeight();
        return new Rect(0f, Mathf.Max(0f, GetScaledScreenHeight() - height), GetScaledScreenWidth(), height);
    }

    private float GetBottomBarHeight()
    {
        int buttonCount = HasMapPrefabs() ? mapPrefabs.Length : 0;
        if (buttonCount <= 0)
        {
            return bottomBarHeight;
        }

        int columns = GetMapButtonColumnCount(GetScaledScreenWidth() - 24f, buttonCount);
        int rows = Mathf.CeilToInt(buttonCount / (float)columns);
        return 12f + rows * buttonSize.y + Mathf.Max(0, rows - 1) * buttonGap + 12f;
    }

    private int GetMapButtonColumnCount(float availableWidth, int buttonCount)
    {
        int columns = Mathf.FloorToInt((availableWidth - buttonGap) / (buttonSize.x + buttonGap));
        return Mathf.Clamp(columns, 1, Mathf.Max(1, buttonCount));
    }

    private float GetScaledScreenWidth()
    {
        return Screen.width / currentUiScale;
    }

    private float GetScaledScreenHeight()
    {
        return Screen.height / currentUiScale;
    }

    private bool HasMapPrefabs()
    {
        return mapPrefabs != null && mapPrefabs.Length > 0;
    }

    private bool IsValidMapIndex(int index)
    {
        return HasMapPrefabs() && index >= 0 && index < mapPrefabs.Length && mapPrefabs[index] != null;
    }

    private void EnsureGuiStyles()
    {
        if (panelStyle != null)
        {
            return;
        }

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = CreateTexture(panelColor);

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.normal.background = CreateTexture(buttonColor);
        buttonStyle.hover.background = CreateTexture(new Color(0.22f, 0.26f, 0.28f, 0.86f));
        buttonStyle.active.background = CreateTexture(selectedButtonColor);
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.fontSize = 12;

        selectedButtonStyle = new GUIStyle(buttonStyle);
        selectedButtonStyle.normal.background = CreateTexture(selectedButtonColor);

        hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.88f);
        hintStyle.fontStyle = FontStyle.Bold;
        hintStyle.fontSize = 11;
        hintStyle.alignment = TextAnchor.MiddleLeft;
        hintStyle.padding = new RectOffset(10, 10, 0, 0);

        hintBoxStyle = new GUIStyle(GUI.skin.textField);
        hintBoxStyle.normal.background = panelStyle.normal.background;
        hintBoxStyle.hover.background = hintBoxStyle.normal.background;
        hintBoxStyle.active.background = hintBoxStyle.normal.background;
        hintBoxStyle.focused.background = hintBoxStyle.normal.background;
    }

    private static float GetUiScale()
    {
        float widthScale = Screen.width / ReferenceUiWidth;
        float heightScale = Screen.height / ReferenceUiHeight;
        return Mathf.Max(1f, Mathf.Min(widthScale, heightScale));
    }

    private static string NormalizeMapName(string mapName)
    {
        string normalizedName = RemoveCloneSuffix(mapName);
        if (normalizedName.StartsWith("Camp_"))
        {
            return normalizedName.Substring("Camp_".Length);
        }

        if (normalizedName.StartsWith("Map_"))
        {
            return normalizedName.Substring("Map_".Length);
        }

        return normalizedName;
    }

    private static string RemoveCloneSuffix(string mapName)
    {
        return string.IsNullOrEmpty(mapName) ? string.Empty : mapName.Replace("(Clone)", string.Empty).Trim();
    }

    private static Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
}
