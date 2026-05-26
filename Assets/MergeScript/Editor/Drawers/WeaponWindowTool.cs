// Weapon Workbench — drag Prefabs to register them; data assets are created automatically in Assets/Data/.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using GunAssemblyTool;

public class WeaponWindowTool : EditorWindow
{
    // ── Folder constants ──────────────────────────────────────────────────────
    private const string GUNBODY_PATH = "Assets/Data/Gunbody";
    private const string ATTACHMENT_PATH = "Assets/Data/Attachment";
    private const string REGISTRY_PATH = "Assets/Data/Registry";
    private const string TAGS_PATH = "Assets/Data/Tags";

    // ── Original UI state (unchanged from WeaponAlignmentTool) ────────────────
    private int selectedTab = 0;
    private string[] tabNames = { "Receiver", "Muzzle", "Optic", "Stock", "Magazine" };

    // Key: original prefab  Value: auto-generated ScriptableObject
    private List<GameObject>[] assetLibrary = new List<GameObject>[5];
    private Dictionary<GameObject, GunBodyData> bodyDataMap = new Dictionary<GameObject, GunBodyData>();
    private Dictionary<GameObject, AttachmentData> attachDataMap = new Dictionary<GameObject, AttachmentData>();
    private Dictionary<GameObject, bool> calibrationStatus = new Dictionary<GameObject, bool>();

    private Vector2 scrollPos;

    private enum WorkbenchMode { Idle, Calibration, Equip }
    private WorkbenchMode currentMode = WorkbenchMode.Idle;

    private bool showCalibrationAssist = true;
    private float splitterPosPercent = 0.4f;
    private bool isResizing = false;
    private const float splitterWidth = 5f;
    private float safeRightWidth = 400f;

    // Calibration mode
    private GameObject currentTargetObject;
    private GameObject currentPrefabAsset;
    private List<GameObject> currentDummies = new List<GameObject>();
    private Dictionary<GameObject, string> dummyToSocketMap = new Dictionary<GameObject, string>();

    // Equip mode
    private GameObject[] equipLoadout = new GameObject[5];
    private GameObject equipAssemblyRoot;

    // ── Detail panel state ────────────────────────────────────────────────────
    private GameObject _selectedPrefab;
    private Vector2 _detailScroll;

    // ── PCG settings ──────────────────────────────────────────────────────────
    // ── Shared data ───────────────────────────────────────────────────────────
    private TagDefinitions _tagDefs;
    private AttachmentRegistry _registry;

    // ── Keyword whitelist per tab ─────────────────────────────────────────────
    // Prefab names must contain at least one keyword (case-insensitive) to be accepted.
    // Tab 0 (Receiver) has no restriction.
    private static readonly string[][] TabKeywords = new string[][]
    {
        null,                                                                          // 0 Receiver — no filter
        new[]{ "muzzle","suppressor","silencer","brake","compensator" },               // 1 Muzzle
        new[]{ "scope","optic","sight","eotech","holographic","red dot","acog","reflex" }, // 2 Optic
        new[]{ "stock","butt","buffer" },                                              // 3 Stock
        new[]{ "mag","magazine","drum","clip","ammo" }                                 // 4 Magazine
    };

    // Returns true if the prefab name matches at least one keyword for the given tab.
    private bool MatchesTabKeywords(string prefabName, int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= TabKeywords.Length) return true;
        var keywords = TabKeywords[tabIndex];
        if (keywords == null) return true;
        string lower = prefabName.ToLowerInvariant();
        return keywords.Any(k => lower.Contains(k));
    }

    // ── Stat list UI state ────────────────────────────────────────────────────
    private string _customStatKey = "";
    private int _presetStatIdx = 0;
    private bool _statFoldout = true;

    // Persistent dropdown indices for tag editors.
    // Must be fields so the selection survives OnGUI repaints.
    private int _bodyTagIdx = 0;
    private int _reqTagIdx = 0;
    private int _forbTagIdx = 0;

    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Weapon Workbench/🛠️ Main Workbench", priority = 1)]
    public static void ShowWindow()
    {
        GetWindow<WeaponWindowTool>("Weapon Workbench").minSize = new Vector2(850, 550);
    }

    private void OnEnable()
    {
        for (int i = 0; i < assetLibrary.Length; i++)
            if (assetLibrary[i] == null) assetLibrary[i] = new List<GameObject>();

        EnsureDataFolders();
        LoadOrCreateSharedData();
        WeaponToolSettingsWorkbench.LoadSettings();
        LoadLibrary();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main GUI
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        float minLeftPx = 300f;
        float minRightPx = 380f;
        float targetLeftWidth = position.width * splitterPosPercent;
        float maxLeftPx = Mathf.Max(minLeftPx, position.width - minRightPx);
        float actualLeftWidth = Mathf.Clamp(targetLeftWidth, minLeftPx, maxLeftPx);
        splitterPosPercent = actualLeftWidth / position.width;

        GUILayout.BeginHorizontal();
        DrawLeftWorkbench(actualLeftWidth);

        Rect splitterRect = new Rect(actualLeftWidth, 0, splitterWidth, position.height);
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && splitterRect.Contains(e.mousePosition)) isResizing = true;
        if (isResizing) { splitterPosPercent = Mathf.Clamp(e.mousePosition.x / position.width, 0.2f, 0.8f); Repaint(); }
        if (e.type == EventType.MouseUp) isResizing = false;

        EditorGUI.DrawRect(new Rect(actualLeftWidth, 0, 2, position.height), new Color(0.4f, 0.4f, 0.4f, 1f));

        float actualRightWidth = position.width - actualLeftWidth - 2f;
        if (Event.current.type == EventType.Layout) safeRightWidth = actualRightWidth;

        Rect rightPanelRect = new Rect(actualLeftWidth + 2f, 0, actualRightWidth, position.height);
        HandleDragAndDrop(rightPanelRect);
        DrawRightPanel(safeRightWidth);

        GUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Left panel
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawLeftWorkbench(float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width));
        GUILayout.Label("Workbench", EditorStyles.largeLabel);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        if (currentMode == WorkbenchMode.Idle) DrawIdleUI();
        else if (currentMode == WorkbenchMode.Calibration) DrawCalibrationUI();
        else if (currentMode == WorkbenchMode.Equip) DrawEquipUI();

        // Detail panel lives in the left column so the right icon grid
        // can use full height regardless of how many prefabs are loaded.
        if (_selectedPrefab != null)
        {
            GUILayout.Space(6);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(width - 8, 1), new Color(0.4f, 0.4f, 0.4f));
            GUILayout.Space(4);
            _detailScroll = GUILayout.BeginScrollView(_detailScroll);
            DrawDetailPanel(width - 8);
            GUILayout.EndScrollView();
        }

        GUILayout.EndVertical();
    }

    private void DrawIdleUI()
    {
        GUILayout.Space(30);
        GUILayout.Label(
            "Double-click an asset on the right to enter operation mode:\n\n" +
            "⏳ Uncalibrated -> [Calibration Mode]\n" +
            "✅ Calibrated   -> [Equip Mode]\n\n" +
            "Single-click to view and edit part data.",
            EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawCalibrationUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            $"Mode: Calibrating\nTarget: {currentTargetObject?.name}\n" +
            "Align it with the dummy using Move(W) / Rotate(E).",
            MessageType.Info);

        GUILayout.Space(5);
        bool newState = EditorGUILayout.ToggleLeft("👁 Show 3D Visual Assist", showCalibrationAssist, EditorStyles.boldLabel);
        if (newState != showCalibrationAssist) { showCalibrationAssist = newState; SceneView.RepaintAll(); }

        GUILayout.Space(20);
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("✔ Complete Alignment & Generate Prefab", GUILayout.Height(50)))
        { CompleteCalibration(); GUIUtility.ExitGUI(); }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        if (GUILayout.Button("Cancel")) { ClearWorkbench(); GUIUtility.ExitGUI(); }
    }

    private void DrawEquipUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Mode: Real-time Assembly.\nDouble-click calibrated parts on the right to snap them in.",
            MessageType.Info);

        GUILayout.Space(10);
        EditorGUI.BeginDisabledGroup(true);
        string[] labels = { "Receiver", "Muzzle", "Optic", "Stock", "Magazine" };
        for (int i = 0; i < 5; i++)
            EditorGUILayout.ObjectField(labels[i], equipLoadout[i], typeof(GameObject), false);
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);
        GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);
        if (GUILayout.Button("🎲 Randomize Full Weapon", GUILayout.Height(35)))
            RandomizeEquipAssembly();

        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("💾 Save Full Assembly", GUILayout.Height(50)))
        { SaveEquipAssembly(); GUIUtility.ExitGUI(); }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        if (GUILayout.Button("Clear Workbench")) { ClearWorkbench(); GUIUtility.ExitGUI(); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Right panel
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawRightPanel(float width)
    {
        // Right panel always shows the full-height icon library.
        // The detail panel has moved to the left column below the workbench UI.
        DrawRightLibrary(width);
    }

    private void DrawRightLibrary(float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width));
        GUILayout.Label("Asset Library", EditorStyles.largeLabel);

        GUILayout.BeginHorizontal();
        float cur = 0, max = width - 15f;
        for (int i = 0; i < tabNames.Length; i++)
        {
            float bw = EditorStyles.toolbarButton.CalcSize(new GUIContent(tabNames[i])).x;
            if (cur + bw > max) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); cur = 0; }
            if (GUILayout.Toggle(selectedTab == i, tabNames[i], EditorStyles.toolbarButton, GUILayout.Width(bw)))
                selectedTab = i;
            cur += bw + 4f;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        DrawAssetList(width);
        GUILayout.EndVertical();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Detail panel
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawDetailPanel(float width)
    {
        if (_selectedPrefab == null) return;

        // No BeginScrollView here — the scroll is handled by the caller
        // (DrawLeftWorkbench) to avoid nested scroll views which break mouse wheel.
        if (bodyDataMap.TryGetValue(_selectedPrefab, out var bodyData))
            DrawBodyDataDetail(bodyData);
        else if (attachDataMap.TryGetValue(_selectedPrefab, out var attData))
            DrawAttachmentDataDetail(attData);
        else
            GUILayout.Label("No data asset found for this prefab.", EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawBodyDataDetail(GunBodyData data)
    {
        GUILayout.Label($"📦  {data.displayName}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        GUILayout.Label("Compatibility Tags", EditorStyles.miniBoldLabel);

        if (data.tags.Count == 0)
            EditorGUILayout.HelpBox(
                "No tags — this gun body is treated as default and accepts any attachment.",
                MessageType.None);

        DrawTagEditor(data);

        GUILayout.Space(6);

        GUILayout.Label("Supported Slots", EditorStyles.miniBoldLabel);
        bool slotsChanged = false;
        foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
        {
            bool has = data.slots.Any(s => s.slotType == type);
            bool newVal = EditorGUILayout.Toggle(type.ToString(), has);
            if (newVal != has)
            {
                if (newVal) data.slots.Add(new SlotData { slotType = type });
                else data.slots.RemoveAll(s => s.slotType == type);
                slotsChanged = true;
            }
        }
        if (slotsChanged) EditorUtility.SetDirty(data);

        GUILayout.Space(6);

        GUILayout.Label("Stats", EditorStyles.miniBoldLabel);
        DrawStatList(data.stats, data);
    }

    private void DrawAttachmentDataDetail(AttachmentData data)
    {
        GUILayout.Label($"🔧  {data.displayName}  [{data.attachType}]", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        var allTags = _tagDefs != null ? _tagDefs.AllTags.ToArray() : new string[0];

        // Required tags use OR logic: body needs at least one of these tags.
        // Leave empty to match any gun body regardless of its tags.
        if (data.requiredTags.Count == 0)
            EditorGUILayout.HelpBox(
                "No required tags — this attachment matches any gun body.",
                MessageType.None);
        else
            EditorGUILayout.HelpBox(
                "OR logic: gun body needs at least one of these tags to be compatible.",
                MessageType.None);

        GUILayout.Label("Required Tags", EditorStyles.miniBoldLabel);
        DrawTagList(data.requiredTags, allTags, new Color(0.3f, 0.7f, 0.4f),
                    ref _reqTagIdx, data);

        GUILayout.Space(4);

        GUILayout.Label("Forbidden Tags", EditorStyles.miniBoldLabel);
        DrawTagList(data.forbiddenTags, allTags, new Color(0.9f, 0.3f, 0.3f),
                    ref _forbTagIdx, data);

        GUILayout.Space(6);

        GUILayout.Label("Stat Bonuses", EditorStyles.miniBoldLabel);
        DrawStatList(data.stats, data);

        GUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        if (data is BarrelData bar)
            bar.hpBonus = EditorGUILayout.IntField("HP Bonus (Player)", bar.hpBonus);
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(data);
    }

    private void DrawTagList(List<string> tagList, string[] allTags,
                             Color pillColor, ref int idxRef,
                             UnityEngine.Object owner = null)
    {
        var toRemove = new List<string>();
        GUILayout.BeginHorizontal();
        foreach (var t in tagList)
        {
            GUI.backgroundColor = pillColor;
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(t, EditorStyles.miniLabel);
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("×", EditorStyles.miniLabel, GUILayout.Width(14)))
                toRemove.Add(t);
            GUILayout.EndHorizontal();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        if (toRemove.Count > 0)
        {
            tagList.RemoveAll(t => toRemove.Contains(t));
            if (owner != null) EditorUtility.SetDirty(owner);
        }

        var available = allTags.Where(t => !tagList.Contains(t)).ToArray();
        if (available.Length > 0)
        {
            GUILayout.BeginHorizontal();
            idxRef = Mathf.Clamp(idxRef, 0, available.Length - 1);
            idxRef = EditorGUILayout.Popup(idxRef, available, GUILayout.Width(120));
            if (GUILayout.Button("+ Add", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                tagList.Add(available[idxRef]);
                if (owner != null) EditorUtility.SetDirty(owner);
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("All tags added.", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void DrawTagEditor(GunBodyData data)
    {
        var allTags = _tagDefs != null ? _tagDefs.AllTags.ToArray() : new string[0];
        bool changed = false;

        var toRemove = new List<string>();
        GUILayout.BeginHorizontal();
        foreach (var tag in data.tags)
        {
            GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f);
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(tag, EditorStyles.miniLabel);
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("×", EditorStyles.miniLabel, GUILayout.Width(14))) toRemove.Add(tag);
            GUILayout.EndHorizontal();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
        if (toRemove.Count > 0) { data.tags.RemoveAll(t => toRemove.Contains(t)); changed = true; }

        var available = allTags.Where(t => !data.tags.Contains(t)).ToArray();
        if (available.Length > 0)
        {
            GUILayout.BeginHorizontal();
            _bodyTagIdx = Mathf.Clamp(_bodyTagIdx, 0, available.Length - 1);
            _bodyTagIdx = EditorGUILayout.Popup(_bodyTagIdx, available, GUILayout.Width(120));
            if (GUILayout.Button("+ Add", EditorStyles.miniButton, GUILayout.Width(50)))
            { data.tags.Add(available[_bodyTagIdx]); changed = true; }
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        var key = "WT_CustomTag_Body";
        string custom = EditorGUILayout.TextField(EditorPrefs.GetString(key, ""), GUILayout.Width(100));
        EditorPrefs.SetString(key, custom);
        if (GUILayout.Button("+ Custom", EditorStyles.miniButton, GUILayout.Width(60)) &&
            !string.IsNullOrWhiteSpace(custom))
        {
            string norm = custom.Trim().ToLowerInvariant();
            if (!data.tags.Contains(norm))
            {
                data.tags.Add(norm);
                if (_tagDefs != null && !_tagDefs.IsValid(norm))
                { _tagDefs.tags.Add(norm); EditorUtility.SetDirty(_tagDefs); }
                changed = true;
            }
            EditorPrefs.SetString(key, "");
        }
        GUILayout.EndHorizontal();

        if (changed) EditorUtility.SetDirty(data);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stat list editor
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawStatList(List<GunAssemblyTool.StatEntry> statList, UnityEngine.Object owner)
    {
        _statFoldout = EditorGUILayout.Foldout(
            _statFoldout,
            $"Stats  ({statList.Count})",
            true,
            EditorStyles.foldoutHeader);

        if (!_statFoldout) return;

        EditorGUI.indentLevel++;

        if (statList.Count == 0)
        {
            EditorGUILayout.LabelField("No stats yet. Add one below.", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            int removeIdx = -1;
            for (int i = 0; i < statList.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(statList[i].key, GUILayout.Width(100));

                EditorGUI.BeginChangeCheck();
                float newVal = EditorGUILayout.FloatField(statList[i].value);
                if (EditorGUI.EndChangeCheck())
                {
                    statList[i].value = newVal;
                    EditorUtility.SetDirty(owner);
                }

                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("✕", GUILayout.Width(22))) removeIdx = i;
                GUI.color = Color.white;

                GUILayout.EndHorizontal();
            }
            if (removeIdx >= 0)
            {
                statList.RemoveAt(removeIdx);
                EditorUtility.SetDirty(owner);
            }
        }

        GUILayout.Space(4);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Add preset stat — only shows presets not already added
        var existingKeys = new HashSet<string>(statList.Select(s => s.key));
        var available = GunAssemblyTool.StatKeys.Presets
                               .Where(p => !existingKeys.Contains(p))
                               .ToArray();

        if (available.Length > 0)
        {
            GUILayout.BeginHorizontal();
            _presetStatIdx = Mathf.Clamp(_presetStatIdx, 0, available.Length - 1);
            _presetStatIdx = EditorGUILayout.Popup(_presetStatIdx, available, GUILayout.Width(110));
            if (GUILayout.Button("+ Add Stat", EditorStyles.miniButton, GUILayout.Width(72)))
            {
                statList.Add(new GunAssemblyTool.StatEntry(available[_presetStatIdx], 0f));
                _presetStatIdx = 0;
                EditorUtility.SetDirty(owner);
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("All preset stats added.", EditorStyles.centeredGreyMiniLabel);
        }

        // Add custom stat
        GUILayout.BeginHorizontal();
        _customStatKey = EditorGUILayout.TextField(_customStatKey, GUILayout.Width(110));
        if (GUILayout.Button("+ Custom", EditorStyles.miniButton, GUILayout.Width(72)) &&
            !string.IsNullOrWhiteSpace(_customStatKey))
        {
            string norm = _customStatKey.Trim();
            if (!existingKeys.Contains(norm))
            {
                statList.Add(new GunAssemblyTool.StatEntry(norm, 0f));
                EditorUtility.SetDirty(owner);
            }
            _customStatKey = "";
        }
        GUILayout.EndHorizontal();

        EditorGUI.indentLevel--;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Asset list
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawAssetList(float width)
    {
        List<GameObject> currentList = assetLibrary[selectedTab];

        bool removedAny = false;
        for (int i = currentList.Count - 1; i >= 0; i--)
            if (currentList[i] == null) { currentList.RemoveAt(i); removedAny = true; }
        if (removedAny) SaveLibrary();

        if (currentList.Count == 0)
        {
            GUILayout.Space(20);
            GUILayout.Label("Drag Prefabs here to add them.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);


        GUILayout.Space(8);

        float iconSize = 75f;
        float itemWidth = 85f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width - 30f) / itemWidth));

        GUILayout.BeginHorizontal();
        for (int i = 0; i < currentList.Count; i++)
        {
            GameObject obj = currentList[i];
            if (obj == null) { currentList.RemoveAt(i); i--; SaveLibrary(); continue; }

            if (i > 0 && i % columns == 0)
            { GUILayout.EndHorizontal(); GUILayout.Space(10); GUILayout.BeginHorizontal(); }

            bool isCalibrated = calibrationStatus.ContainsKey(obj) && calibrationStatus[obj];
            bool isSelected = _selectedPrefab == obj;

            GUILayout.Space(6);
            GUILayout.BeginVertical(
            GUILayout.Width(itemWidth + 12));
            GUILayout.Space(4);
            Texture2D preview = AssetPreview.GetAssetPreview(obj) ?? AssetPreview.GetMiniThumbnail(obj);
            float padding = 6f;

            Rect cardRect = GUILayoutUtility.GetRect(
                iconSize + padding * 2,
                iconSize + padding * 2);

            Rect buttonRect = new Rect(
                cardRect.x + padding,
                cardRect.y + padding,
                iconSize,
                iconSize);
            bool isHover = buttonRect.Contains(Event.current.mousePosition);
            Event e = Event.current;

            if (isSelected)
            {
                Handles.BeginGUI();

                Handles.color = new Color(0.9f, 0.9f, 0.9f, 1f);

                Handles.DrawAAPolyLine(
                    4f,
                    new Vector3(cardRect.x, cardRect.y),
                    new Vector3(cardRect.xMax, cardRect.y),
                    new Vector3(cardRect.xMax, cardRect.yMax),
                    new Vector3(cardRect.x, cardRect.yMax),
                    new Vector3(cardRect.x, cardRect.y)
                );

                Handles.EndGUI();
            }
            if (isHover && !isSelected)
            {
                Handles.BeginGUI();

                Handles.color = new Color(1f, 1f, 1f, 0.15f);

                Handles.DrawAAPolyLine(
                    2f,
                    new Vector3(buttonRect.x - 2, buttonRect.y - 2),
                    new Vector3(buttonRect.xMax + 2, buttonRect.y - 2),
                    new Vector3(buttonRect.xMax + 2, buttonRect.yMax + 2),
                    new Vector3(buttonRect.x - 2, buttonRect.yMax + 2),
                    new Vector3(buttonRect.x - 2, buttonRect.y - 2)
                );

                Handles.EndGUI();

                //Repaint();
            }
            // Right-click context menu
            if (e.type == EventType.MouseDown && e.button == 1 && buttonRect.Contains(e.mousePosition))
            {
                GameObject menuObj = obj;
                int menuTab = selectedTab;
                GenericMenu menu = new GenericMenu();
                if (isCalibrated)
                    menu.AddItem(new GUIContent("🔧 Equip Part"), false, () => EnterEquipMode(menuObj, menuTab));
                menu.AddItem(new GUIContent("📐 (Re)Calibrate"), false, () => EnterCalibrationMode(menuObj, menuTab));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("🗑️ Remove"), false, () =>
                {
                    // Delete the auto-generated data asset so it does not accumulate
                    if (menuTab == 0 && bodyDataMap.TryGetValue(menuObj, out var bd))
                    {
                        string bdPath = AssetDatabase.GetAssetPath(bd);
                        if (!string.IsNullOrEmpty(bdPath)) AssetDatabase.DeleteAsset(bdPath);
                    }
                    else if (menuTab > 0 && attachDataMap.TryGetValue(menuObj, out var ad))
                    {
                        string adPath = AssetDatabase.GetAssetPath(ad);
                        if (!string.IsNullOrEmpty(adPath)) AssetDatabase.DeleteAsset(adPath);
                    }

                    assetLibrary[menuTab].Remove(menuObj);
                    calibrationStatus.Remove(menuObj);
                    bodyDataMap.Remove(menuObj);
                    attachDataMap.Remove(menuObj);
                    if (_selectedPrefab == menuObj) _selectedPrefab = null;
                    SaveLibrary();
                    RefreshRegistry();
                    AssetDatabase.Refresh();
                });
                menu.ShowAsContext();
                e.Use();
            }
            // Double-click — enter calibrate or equip mode, keep detail panel visible
            else if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && buttonRect.Contains(e.mousePosition))
            {
                if (isCalibrated) EnterEquipMode(obj, selectedTab);
                else EnterCalibrationMode(obj, selectedTab);
                // Do NOT clear _selectedPrefab so the detail panel stays visible
                e.Use();
            }
            // Single-click — toggle detail panel
            else if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 1 && buttonRect.Contains(e.mousePosition))
            {
                _selectedPrefab = (_selectedPrefab == obj) ? null : obj;
                e.Use();
                Repaint();
            }

            GUI.Box(buttonRect, preview);

            Rect statusRect = new Rect(
            cardRect.xMax - 24,
            cardRect.yMax - 24,
            20,
            20);
            GUIStyle statusSt = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            if (isCalibrated) GUI.Label(statusRect, "✅", statusSt);
            else { GUI.color = Color.yellow; GUI.Label(statusRect, "⏳", statusSt); GUI.color = Color.white; }

            GUIStyle labelSt = new GUIStyle(EditorStyles.miniLabel)
            { alignment = TextAnchor.UpperCenter, clipping = TextClipping.Clip };
            GUILayout.Label(obj.name, labelSt, GUILayout.Width(iconSize));

            GUILayout.Space(6);
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drag and drop — with keyword filtering per tab
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
        if (!dropArea.Contains(evt.mousePosition)) return;

        // Check whether any dragged object passes the keyword filter for this tab
        bool anyValid = DragAndDrop.objectReferences.Any(o =>
        {
            if (!(o is GameObject go)) return false;
            string path = AssetDatabase.GetAssetPath(go).ToLower();
            bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(go) ||
                            path.EndsWith(".fbx") || path.EndsWith(".obj");
            return isPrefab && MatchesTabKeywords(go.name, selectedTab);
        });

        DragAndDrop.visualMode = anyValid
            ? DragAndDropVisualMode.Copy
            : DragAndDropVisualMode.Rejected;

        if (evt.type == EventType.DragPerform && anyValid)
        {
            DragAndDrop.AcceptDrag();
            bool changed = false;
            bool rejected = false;

            foreach (Object draggedObj in DragAndDrop.objectReferences)
            {
                if (!(draggedObj is GameObject go)) continue;
                string path = AssetDatabase.GetAssetPath(go).ToLower();
                bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(go) ||
                                path.EndsWith(".fbx") || path.EndsWith(".obj");
                if (!isPrefab) continue;

                // Keyword check
                if (!MatchesTabKeywords(go.name, selectedTab))
                {
                    string expected = TabKeywords[selectedTab] != null
                        ? string.Join(", ", TabKeywords[selectedTab])
                        : "any";
                    Debug.LogWarning(
                        $"[WeaponWorkbench] '{go.name}' rejected on tab '{tabNames[selectedTab]}'. " +
                        $"Name must contain one of: {expected}");
                    rejected = true;
                    continue;
                }

                if (assetLibrary[selectedTab].Contains(go)) continue;

                assetLibrary[selectedTab].Add(go);
                calibrationStatus[go] = CheckIfCalibrated(go, selectedTab);

                if (selectedTab == 0) AutoCreateGunBodyData(go);
                else AutoCreateAttachmentData(go, selectedTab);

                changed = true;
            }

            if (rejected && !changed)
                EditorUtility.DisplayDialog(
                    "Wrong Tab",
                    $"The dragged prefab does not match the '{tabNames[selectedTab]}' tab.\n" +
                    $"Expected name keywords: {string.Join(", ", TabKeywords[selectedTab] ?? new[] { "any" })}",
                    "OK");

            if (changed) { SaveLibrary(); RefreshRegistry(); }
        }
        evt.Use();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auto-create data assets
    // ─────────────────────────────────────────────────────────────────────────

    private void AutoCreateGunBodyData(GameObject prefab)
    {
        if (bodyDataMap.ContainsKey(prefab)) return;

        var data = ScriptableObject.CreateInstance<GunBodyData>();
        data.bodyId = prefab.name.ToLowerInvariant().Replace(" ", "_");
        data.displayName = prefab.name;
        data.bodyPrefab = prefab;
        data.partObject = prefab;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{GUNBODY_PATH}/{prefab.name}_Body.asset");
        AssetDatabase.CreateAsset(data, assetPath);
        AssetDatabase.SaveAssets();

        bodyDataMap[prefab] = data;
        Debug.Log($"[WeaponWorkbench] Created GunBodyData → {assetPath}");
    }

    // Checks whether a linked data asset has fallen out of sync with its
    // prefab's name (e.g. after a rename) and updates displayName / bodyId /
    // attachmentId and the .asset filename to match.
    private void SyncDataNames()
    {
        bool dirty = false;

        foreach (var kv in bodyDataMap)
        {
            if (kv.Key == null || kv.Value == null) continue;
            string prefabName = kv.Key.name;
            var data = kv.Value;

            if (data.displayName != prefabName)
            {
                data.displayName = prefabName;
                data.bodyId = prefabName.ToLowerInvariant().Replace(" ", "_");
                EditorUtility.SetDirty(data);

                // Rename the .asset file to match
                string oldPath = AssetDatabase.GetAssetPath(data);
                string newPath = System.IO.Path.GetDirectoryName(oldPath).Replace('\\', '/') +
                                 "/" + prefabName + "_Body.asset";
                newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
                AssetDatabase.MoveAsset(oldPath, newPath);
                dirty = true;
                Debug.Log($"[WeaponWorkbench] Renamed data asset: {oldPath} → {newPath}");
            }
        }

        foreach (var kv in attachDataMap)
        {
            if (kv.Key == null || kv.Value == null) continue;
            string prefabName = kv.Key.name;
            var data = kv.Value;

            if (data.displayName != prefabName)
            {
                data.displayName = prefabName;
                data.attachmentId = prefabName.ToLowerInvariant().Replace(" ", "_");
                EditorUtility.SetDirty(data);

                string oldPath = AssetDatabase.GetAssetPath(data);
                string newPath = System.IO.Path.GetDirectoryName(oldPath).Replace('\\', '/') +
                                 "/" + prefabName + "_" + data.attachType + ".asset";
                newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
                AssetDatabase.MoveAsset(oldPath, newPath);
                dirty = true;
                Debug.Log($"[WeaponWorkbench] Renamed data asset: {oldPath} → {newPath}");
            }
        }

        if (dirty) AssetDatabase.SaveAssets();
    }

    private void AutoCreateAttachmentData(GameObject prefab, int tabIndex)
    {
        if (attachDataMap.ContainsKey(prefab)) return;

        AttachmentType type = TabIndexToAttachmentType(tabIndex);
        AttachmentData data = CreateAttachmentDataOfType(type);

        data.attachmentId = prefab.name.ToLowerInvariant().Replace(" ", "_");
        data.displayName = prefab.name;
        data.attachType = type;
        data.attachmentPrefab = prefab;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{ATTACHMENT_PATH}/{prefab.name}_{type}.asset");
        AssetDatabase.CreateAsset(data, assetPath);
        AssetDatabase.SaveAssets();

        attachDataMap[prefab] = data;
        Debug.Log($"[WeaponWorkbench] Created {type}Data → {assetPath}");
    }

    private AttachmentType TabIndexToAttachmentType(int tabIndex)
    {
        switch (tabIndex)
        {
            case 1: return AttachmentType.Muzzle;
            case 2: return AttachmentType.Scope;
            case 3: return AttachmentType.Stock;
            case 4: return AttachmentType.Magazine;
            default: return AttachmentType.Barrel;
        }
    }

    private AttachmentData CreateAttachmentDataOfType(AttachmentType type)
    {
        switch (type)
        {
            case AttachmentType.Magazine: return ScriptableObject.CreateInstance<MagazineData>();
            case AttachmentType.Muzzle: return ScriptableObject.CreateInstance<MuzzleData>();
            case AttachmentType.Scope: return ScriptableObject.CreateInstance<ScopeData>();
            case AttachmentType.Stock: return ScriptableObject.CreateInstance<StockData>();
            case AttachmentType.Grip: return ScriptableObject.CreateInstance<GripData>();
            case AttachmentType.Underbarrel: return ScriptableObject.CreateInstance<UnderbarrelData>();
            case AttachmentType.Skin: return ScriptableObject.CreateInstance<SkinData>();
            default: return ScriptableObject.CreateInstance<BarrelData>();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Calibration
    // ─────────────────────────────────────────────────────────────────────────

    private bool CheckIfCalibrated(GameObject go, int tabIndex)
    {
        if (tabIndex == 0)
            return go.transform.Find("Socket_Muzzle") != null &&
                   go.transform.Find("Socket_Optic") != null &&
                   go.transform.Find("Socket_Stock") != null &&
                   go.transform.Find("Socket_Magazine") != null;
        return go.GetComponent<MeshRenderer>() == null && go.transform.childCount > 0;
    }

    private void EnterCalibrationMode(GameObject targetPrefab, int tabIndex)
    {
        if (WeaponToolSettingsWorkbench.dummyBody == null || WeaponToolSettingsWorkbench.dummyMuzzle == null)
        {
            if (EditorUtility.DisplayDialog("Missing Dummy",
                "Please configure base dummies first.\nGo to settings now?", "Settings", "Cancel"))
                WeaponToolSettingsWorkbench.ShowWindow();
            return;
        }

        ClearWorkbench();
        currentMode = WorkbenchMode.Calibration;
        currentPrefabAsset = targetPrefab;
        currentTargetObject = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab);
        PrefabUtility.UnpackPrefabInstance(currentTargetObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        if (tabIndex == 0)
        {
            currentTargetObject.transform.position = Vector3.zero;
            GameObject mD = SpawnDummy(WeaponToolSettingsWorkbench.dummyMuzzle, "Socket_Muzzle");
            GameObject oD = SpawnDummy(WeaponToolSettingsWorkbench.dummyScope, "Socket_Optic");
            GameObject sD = SpawnDummy(WeaponToolSettingsWorkbench.dummyStock, "Socket_Stock");
            GameObject mgD = SpawnDummy(WeaponToolSettingsWorkbench.dummyMag, "Socket_Magazine");
            SnapDummy(mD, currentTargetObject, "Socket_Muzzle");
            SnapDummy(oD, currentTargetObject, "Socket_Optic");
            SnapDummy(sD, currentTargetObject, "Socket_Stock");
            SnapDummy(mgD, currentTargetObject, "Socket_Magazine");
        }
        else
        {
            SpawnDummy(WeaponToolSettingsWorkbench.dummyBody, "Body");
            string socketName = GetSocketNameForEquip(tabIndex);
            Transform targetSocket = currentDummies[0].transform.Find(socketName);
            currentTargetObject.transform.position = targetSocket != null ? targetSocket.position : Vector3.zero;
            if (targetSocket != null) currentTargetObject.transform.rotation = targetSocket.rotation;
        }

        Selection.activeGameObject = currentTargetObject;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private void SnapDummy(GameObject dummy, GameObject body, string socketName)
    {
        if (dummy == null) return;
        Transform s = body.transform.Find(socketName);
        if (s != null) { dummy.transform.position = s.position; dummy.transform.rotation = s.rotation; }
    }

    private GameObject SpawnDummy(GameObject dummyPrefab, string socketName)
    {
        if (dummyPrefab == null) return null;
        GameObject dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
        dummy.transform.position = Vector3.zero;
        dummy.name = "[Dummy] " + dummyPrefab.name;
        currentDummies.Add(dummy);
        dummyToSocketMap[dummy] = socketName;
        return dummy;
    }

    private void CompleteCalibration()
    {
        bool success = (selectedTab == 0) ? CalibrateGunBody() : CalibrateAttachment(selectedTab);
        if (!success) return;

        string originalPath = AssetDatabase.GetAssetPath(currentPrefabAsset);
        string savePath = originalPath;
        bool isRawModel = originalPath.ToLower().EndsWith(".fbx") || originalPath.ToLower().EndsWith(".obj");
        if (isRawModel) savePath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(originalPath, ".prefab"));

        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(currentTargetObject, savePath, InteractionMode.UserAction);
        if (saved != null)
        {
            int index = assetLibrary[selectedTab].IndexOf(currentPrefabAsset);
            if (index >= 0) assetLibrary[selectedTab][index] = saved;

            // 👇 【核心修复】：必须先 Remove，再赋值 true！
            // 否则覆盖同一个 Prefab 时，会导致刚标记的绿灯瞬间被删除。
            calibrationStatus.Remove(currentPrefabAsset);
            calibrationStatus[saved] = true;

            if (selectedTab == 0 && bodyDataMap.TryGetValue(currentPrefabAsset, out var bd))
            {
                bd.bodyPrefab = saved; bd.partObject = saved;
                bodyDataMap.Remove(currentPrefabAsset);
                bodyDataMap[saved] = bd;
                EditorUtility.SetDirty(bd);
            }
            else if (selectedTab > 0 && attachDataMap.TryGetValue(currentPrefabAsset, out var ad))
            {
                ad.attachmentPrefab = saved;
                attachDataMap.Remove(currentPrefabAsset);
                attachDataMap[saved] = ad;
                EditorUtility.SetDirty(ad);
            }

            SaveLibrary();
            RefreshRegistry();
            Debug.Log($"[WeaponWorkbench] Calibrated: {savePath}");
        }
        ClearWorkbench();
    }

    private bool CalibrateGunBody()
    {
        foreach (var kvp in dummyToSocketMap)
        {
            Transform socket = currentTargetObject.transform.Find(kvp.Value);
            if (socket == null)
            {
                var sObj = new GameObject(kvp.Value);
                socket = sObj.transform;
                // 这里恢复为 true，让 Unity 帮我们处理初始的层级关系
                socket.SetParent(currentTargetObject.transform, true);
            }

            // 1. 对齐世界位置和旋转
            socket.position = kvp.Key.transform.position;
            socket.rotation = kvp.Key.transform.rotation;

            // 2. 👇【核心修复】：动态计算并抵消枪身的缩放！
            // 获取枪身当前的绝对世界缩放 (lossyScale)
            Vector3 parentScale = currentTargetObject.transform.lossyScale;

            // 赋予插槽“反向比例”，确保它不管在多大/多小的枪身下，它的世界大小永远是 1
            socket.localScale = new Vector3(
                1f / parentScale.x,
                1f / parentScale.y,
                1f / parentScale.z
            );
        }
        return true;
    }

    private bool CalibrateAttachment(int tabIndex)
    {
        string targetSocketName = GetSocketNameForEquip(tabIndex);
        Transform targetSocket = currentDummies.Count > 0 ? currentDummies[0].transform.Find(targetSocketName) : null;
        if (targetSocket == null) return false;

        if (currentTargetObject.GetComponent<MeshRenderer>() != null)
        {
            GameObject newRoot = new GameObject(currentTargetObject.name + "_Prefab");
            newRoot.transform.position = targetSocket.position;
            newRoot.transform.rotation = targetSocket.rotation;
            currentTargetObject.transform.SetParent(newRoot.transform, true);
            currentTargetObject = newRoot;
        }
        else
        {
            var children = new List<Transform>();
            foreach (Transform child in currentTargetObject.transform) children.Add(child);
            foreach (Transform child in children) child.SetParent(null, true);
            currentTargetObject.transform.position = targetSocket.position;
            currentTargetObject.transform.rotation = targetSocket.rotation;
            foreach (Transform child in children) child.SetParent(currentTargetObject.transform, true);
        }
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Equip mode
    // ─────────────────────────────────────────────────────────────────────────

    private void EnterEquipMode(GameObject obj, int tabIndex)
    {
        if (currentMode == WorkbenchMode.Calibration) ClearWorkbench();
        currentMode = WorkbenchMode.Equip;
        equipLoadout[tabIndex] = obj;
        // Pass whether this was triggered by selecting a gun body (tab 0)
        // so RefreshEquipAssembly knows whether to show incompatibility dialogs.
        RefreshEquipAssembly(tabIndex == 0);
    }

    // silentClear: when true (switching gun bodies), incompatible parts are
    // removed silently. When false (manually picking an attachment), a dialog
    // tells the user why the part was rejected.
    private void RefreshEquipAssembly(bool silentClear = false)
    {
        if (equipAssemblyRoot != null) DestroyImmediate(equipAssemblyRoot);
        equipAssemblyRoot = new GameObject("[Assembling] New Weapon");
        equipAssemblyRoot.transform.position = Vector3.zero;

        GameObject bodyInstance = null;
        if (equipLoadout[0] != null)
        {
            bodyInstance = (GameObject)PrefabUtility.InstantiatePrefab(equipLoadout[0], equipAssemblyRoot.transform);
            bodyInstance.transform.localPosition = Vector3.zero;
            bodyInstance.transform.localRotation = Quaternion.identity;
        }

        for (int i = 1; i <= 4; i++)
        {
            if (equipLoadout[i] == null) continue;

            if (equipLoadout[0] != null &&
                bodyDataMap.TryGetValue(equipLoadout[0], out var bd) &&
                attachDataMap.TryGetValue(equipLoadout[i], out var ad))
            {
                if (!CompatibilityResolver.IsCompatible(bd, ad))
                {
                    string reason = CompatibilityResolver.GetIncompatibleReason(bd, ad);
                    if (!silentClear)
                    {
                        // User manually picked this attachment — tell them why it was rejected
                        Debug.LogWarning($"[WeaponWorkbench] '{equipLoadout[i].name}' rejected: {reason}");
                        EditorUtility.DisplayDialog(
                            "Incompatible Part",
                            $"'{equipLoadout[i].name}' cannot be attached to '{equipLoadout[0].name}'.\n\n{reason}",
                            "OK");
                    }
                    else
                    {
                        // Gun body was switched — silently clear incompatible parts
                        Debug.Log($"[WeaponWorkbench] Cleared '{equipLoadout[i].name}' (incompatible with new body).");
                    }
                    equipLoadout[i] = null;
                    continue;
                }
            }

            GameObject acc = (GameObject)PrefabUtility.InstantiatePrefab(equipLoadout[i]);
            if (bodyInstance != null)
            {
                string socketName = GetSocketNameForEquip(i);
                Transform socket = bodyInstance.transform.Find(socketName);
                if (socket != null)
                {
                    acc.transform.SetParent(socket, false);
                    acc.transform.localPosition = Vector3.zero;
                    acc.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    acc.transform.SetParent(equipAssemblyRoot.transform, false);
                    Debug.LogWarning($"[WeaponWorkbench] Missing '{socketName}' on receiver.");
                }
            }
            else acc.transform.SetParent(equipAssemblyRoot.transform, false);
        }

        Selection.activeGameObject = equipAssemblyRoot;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    // ─────────────────────────────────────────────────────────────────────────


    // ─────────────────────────────────────────────────────────────────────────
    // Randomize — uniform random across calibrated assets (original logic)
    // ─────────────────────────────────────────────────────────────────────────

    private void RandomizeEquipAssembly()
    {
        bool hasValidBody = false;

        for (int i = 0; i < 5; i++)
        {
            // Collect calibrated assets for this slot that are compatible
            var validAssets = new List<GameObject>();
            foreach (var go in assetLibrary[i])
            {
                if (go == null) continue;
                if (!calibrationStatus.ContainsKey(go) || !calibrationStatus[go]) continue;

                // For slots 1-4 check compatibility against the current receiver
                if (i > 0 && equipLoadout[0] != null)
                {
                    if (bodyDataMap.TryGetValue(equipLoadout[0], out var bd) &&
                        attachDataMap.TryGetValue(go, out var ad))
                    {
                        if (!CompatibilityResolver.IsCompatible(bd, ad)) continue;
                    }
                }
                validAssets.Add(go);
            }

            if (validAssets.Count > 0)
            {
                equipLoadout[i] = validAssets[Random.Range(0, validAssets.Count)];
                if (i == 0) hasValidBody = true;
            }
            else
            {
                equipLoadout[i] = null;
            }
        }

        if (!hasValidBody)
        {
            EditorUtility.DisplayDialog("Notice",
                "Missing a calibrated Receiver in your library. Cannot randomize!", "OK");
            return;
        }

        RefreshEquipAssembly(silentClear: true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Save / load
    // ─────────────────────────────────────────────────────────────────────────

    private void SaveEquipAssembly()
    {
        if (equipAssemblyRoot == null || equipLoadout[0] == null)
        {
            EditorUtility.DisplayDialog("Cannot Save", "A Receiver must be included to save.", "OK");
            return;
        }
        string path = EditorUtility.SaveFilePanelInProject("Save Full Weapon", "NewWeaponLoadout", "prefab", "Select save path");
        if (string.IsNullOrEmpty(path)) return;
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(equipAssemblyRoot, path, InteractionMode.UserAction);
        if (saved != null) { Debug.Log($"[WeaponWorkbench] Saved: {path}"); EditorGUIUtility.PingObject(saved); }
    }

    private void ClearWorkbench()
    {
        if (currentTargetObject != null) DestroyImmediate(currentTargetObject);
        foreach (var d in currentDummies) if (d != null) DestroyImmediate(d);
        currentDummies.Clear(); dummyToSocketMap.Clear();
        currentTargetObject = null; currentPrefabAsset = null;

        if (equipAssemblyRoot != null) DestroyImmediate(equipAssemblyRoot);
        for (int i = 0; i < equipLoadout.Length; i++) equipLoadout[i] = null;
        currentMode = WorkbenchMode.Idle;
    }

    private void SaveLibrary()
    {
        for (int i = 0; i < assetLibrary.Length; i++)
        {
            var guids = assetLibrary[i]
                .Where(go => go != null)
                .Select(go => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(go)));
            EditorPrefs.SetString("WT_AssetLib_Tab_" + i, string.Join(",", guids));
        }

        var calibrated = calibrationStatus
            .Where(kvp => kvp.Key != null && kvp.Value)
            .Select(kvp => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Key)));
        EditorPrefs.SetString("WT_AssetLib_Calibrated", string.Join(",", calibrated));

        var bodyMap = bodyDataMap
            .Where(kvp => kvp.Key != null && kvp.Value != null)
            .Select(kvp =>
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Key)) + ":" +
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Value)));
        EditorPrefs.SetString("WT_BodyDataMap", string.Join(",", bodyMap));

        var attMap = attachDataMap
            .Where(kvp => kvp.Key != null && kvp.Value != null)
            .Select(kvp =>
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Key)) + ":" +
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Value)));
        EditorPrefs.SetString("WT_AttachDataMap", string.Join(",", attMap));
    }

    private void LoadLibrary()
    {
        calibrationStatus.Clear();
        var calibratedSet = new HashSet<string>(
            EditorPrefs.GetString("WT_AssetLib_Calibrated", "")
                       .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

        for (int i = 0; i < assetLibrary.Length; i++)
        {
            assetLibrary[i] = new List<GameObject>();
            string raw = EditorPrefs.GetString("WT_AssetLib_Tab_" + i, "");
            foreach (var guid in raw.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null) continue;
                assetLibrary[i].Add(go);
                calibrationStatus[go] = calibratedSet.Contains(guid) || CheckIfCalibrated(go, i);
            }
        }

        bodyDataMap.Clear();
        foreach (var pair in EditorPrefs.GetString("WT_BodyDataMap", "")
                                         .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(':');
            if (parts.Length != 2) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(parts[0]));
            var data = AssetDatabase.LoadAssetAtPath<GunBodyData>(AssetDatabase.GUIDToAssetPath(parts[1]));
            if (go != null && data != null) bodyDataMap[go] = data;
        }

        attachDataMap.Clear();
        foreach (var pair in EditorPrefs.GetString("WT_AttachDataMap", "")
                                         .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(':');
            if (parts.Length != 2) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(parts[0]));
            var data = AssetDatabase.LoadAssetAtPath<AttachmentData>(AssetDatabase.GUIDToAssetPath(parts[1]));
            if (go != null && data != null) attachDataMap[go] = data;
        }

        // Sync data asset names with their linked prefab names in case any
        // prefabs were renamed while the workbench was closed.
        SyncDataNames();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scene view gizmos
    // ─────────────────────────────────────────────────────────────────────────

    private void OnSceneGUI(SceneView sv)
    {
        if (currentMode != WorkbenchMode.Calibration || currentTargetObject == null || !showCalibrationAssist) return;

        if (selectedTab == 0)
        {
            foreach (var dummy in currentDummies)
                if (dummy != null) DrawSocketVisual(dummy.transform.position, dummy.transform.rotation, dummy.name.Replace("[Dummy] ", ""));
        }
        else if (currentDummies.Count > 0 && currentDummies[0] != null)
        {
            Transform s = currentDummies[0].transform.Find(GetSocketNameForEquip(selectedTab));
            if (s != null) DrawSocketVisual(s.position, s.rotation, GetSocketNameForEquip(selectedTab));
        }
    }

    private void DrawSocketVisual(Vector3 pos, Quaternion rot, string label)
    {
        Handles.color = new Color(0.2f, 1f, 0.2f, 0.3f);
        Handles.SphereHandleCap(0, pos, rot, 0.04f, EventType.Repaint);
        Handles.color = new Color(0.2f, 1f, 0.2f, 0.8f);
        Handles.DrawWireDisc(pos, rot * Vector3.up, 0.04f);
        Handles.DrawWireDisc(pos, rot * Vector3.right, 0.04f);
        Handles.DrawWireDisc(pos, rot * Vector3.forward, 0.04f);
        Handles.color = new Color(0.2f, 0.6f, 1f, 1f);
        Handles.ArrowHandleCap(0, pos, rot, 0.15f, EventType.Repaint);
        GUIStyle s = new GUIStyle { normal = { textColor = Color.green }, fontSize = 12, fontStyle = FontStyle.Bold };
        Handles.Label(pos + Vector3.up * 0.06f, label, s);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string GetSocketNameForEquip(int tabIndex)
    {
        switch (tabIndex)
        {
            case 1: return "Socket_Muzzle";
            case 2: return "Socket_Optic";
            case 3: return "Socket_Stock";
            case 4: return "Socket_Magazine";
            default: return "";
        }
    }

    private void LoadOrCreateSharedData()
    {
        EnsureDataFolders();

        string tagsPath = $"{TAGS_PATH}/TagDefinitions.asset";
        _tagDefs = AssetDatabase.LoadAssetAtPath<TagDefinitions>(tagsPath);
        if (_tagDefs == null)
        {
            _tagDefs = ScriptableObject.CreateInstance<TagDefinitions>();
            AssetDatabase.CreateAsset(_tagDefs, tagsPath);
            AssetDatabase.SaveAssets();
        }

        string regPath = $"{REGISTRY_PATH}/AttachmentRegistry.asset";
        _registry = AssetDatabase.LoadAssetAtPath<AttachmentRegistry>(regPath);
        if (_registry == null)
        {
            _registry = ScriptableObject.CreateInstance<AttachmentRegistry>();
            AssetDatabase.CreateAsset(_registry, regPath);
            AssetDatabase.SaveAssets();
        }
    }

    private void RefreshRegistry()
    {
        if (_registry == null) return;
        _registry.allAttachments = attachDataMap.Values.Where(d => d != null).ToList();
        EditorUtility.SetDirty(_registry);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureDataFolders()
    {
        string[] folders = { "Assets/Data", GUNBODY_PATH, ATTACHMENT_PATH, REGISTRY_PATH, TAGS_PATH };
        foreach (var folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                string child = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}