// Weapon Workbench — drag Prefabs to register them; data assets are created automatically in Assets/PCG Weapon Workbench/Data/.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using GunAssemblyTool;

public class WeaponWindowTool : EditorWindow
{
    // ── Folder constants ──────────────────────────────────────────────────────
    private const string GUNBODY_PATH = "Assets/PCG Weapon Workbench/Data/Gunbody";
    private const string ATTACHMENT_PATH = "Assets/PCG Weapon Workbench/Data/Attachment";
    private const string REGISTRY_PATH = "Assets/PCG Weapon Workbench/Data/Registry";
    private const string TAGS_PATH = "Assets/PCG Weapon Workbench/Data/Tags";

    // ── Theme ─────────────────────────────────────────────────────────────────
    private static readonly Color C_ACCENT = new Color(0.44f, 0.75f, 0.75f, 1f);
    private static readonly Color C_ACCENT_DIM = new Color(0.18f, 0.42f, 0.42f, 1f);
    private static readonly Color C_BG_BLOCK = new Color(0.09f, 0.13f, 0.13f, 1f);
    private static readonly Color C_BORDER = new Color(0.44f, 0.75f, 0.75f, 0.40f);
    private static readonly Color C_TEXT_MAIN = new Color(0.88f, 0.96f, 0.96f, 1f);
    private static readonly Color C_TEXT_DIM = new Color(0.38f, 0.58f, 0.58f, 1f);
    private static readonly Color C_DANGER = new Color(0.75f, 0.22f, 0.17f, 1f);
    private static readonly Color C_SUCCESS = new Color(0.27f, 0.60f, 0.32f, 1f);

    // ── Dynamic tab system ────────────────────────────────────────────────────
    private class TabEntry
    {
        public string displayName;
        public bool isReceiver;
        public AttachmentType type;
        public string socketName;
        public string[] keywords;
    }

    private List<TabEntry> _tabs = new List<TabEntry>();

    // ── UI state ──────────────────────────────────────────────────────────────
    private int selectedTab = 0;

    private List<List<GameObject>> assetLibrary = new List<List<GameObject>>();
    private Dictionary<GameObject, GunBodyData> bodyDataMap = new Dictionary<GameObject, GunBodyData>();
    private Dictionary<GameObject, AttachmentData> attachDataMap = new Dictionary<GameObject, AttachmentData>();
    private Dictionary<GameObject, bool> calibrationStatus = new Dictionary<GameObject, bool>();

    private Vector2 scrollPos;

    private enum WorkbenchMode { Idle, Calibration, Equip, ViewData }
    private WorkbenchMode currentMode = WorkbenchMode.Idle;

    private bool showCalibrationAssist = true;
    private float splitterPosPercent = 0.4f;
    private bool isResizing = false;
    private const float splitterWidth = 5f;
    private float safeRightWidth = 400f;

    // Deferred ExitGUI — avoids ExitGUIException in GenericMenu callbacks
    private bool _pendingExitGUI = false;

    // Calibration mode
    private GameObject currentTargetObject;
    private GameObject currentPrefabAsset;
    private List<GameObject> currentDummies = new List<GameObject>();
    private Dictionary<GameObject, string> dummyToSocketMap = new Dictionary<GameObject, string>();

    // Equip mode
    private List<GameObject> equipLoadout = new List<GameObject>();
    private GameObject equipAssemblyRoot;

    // Detail panel state
    private GameObject _selectedPrefab;
    private Vector2 _detailScroll;

    // Shared data
    private TagDefinitions _tagDefs;
    private AttachmentRegistry _registry;

    // Stat list UI state
    private string _customStatKey = "";
    private GunAssemblyTool.StatValueType _customStatType = GunAssemblyTool.StatValueType.Float;
    private int _presetStatIdx = 0;
    private bool _statFoldout = true;
    private bool _manageCustomKeys = false;
    private bool _showBodyMedia = false;
    private bool _showAttMedia = false;
    private bool _statSaved = true;

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
        EnsureDataFolders();
        LoadOrCreateSharedData();
        WeaponToolSettingsWorkbench.LoadSettings();
        InitTabs();
        LoadLibrary();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // ── Tab initialisation ────────────────────────────────────────────────────

    private void InitTabs()
    {
        _tabs.Clear();
        // Default 5 tabs
        _tabs.Add(new TabEntry { displayName = "Receiver", isReceiver = true, socketName = "", keywords = null });
        _tabs.Add(new TabEntry { displayName = "Muzzle", isReceiver = false, type = AttachmentType.Muzzle, socketName = "Socket_Muzzle", keywords = new[] { "muzzle", "suppressor", "silencer", "brake", "compensator" } });
        _tabs.Add(new TabEntry { displayName = "Optic", isReceiver = false, type = AttachmentType.Scope, socketName = "Socket_Optic", keywords = new[] { "scope", "optic", "sight", "eotech", "holographic", "red dot", "acog", "reflex" } });
        _tabs.Add(new TabEntry { displayName = "Stock", isReceiver = false, type = AttachmentType.Stock, socketName = "Socket_Stock", keywords = new[] { "stock", "butt", "buffer" } });
        _tabs.Add(new TabEntry { displayName = "Magazine", isReceiver = false, type = AttachmentType.Magazine, socketName = "Socket_Magazine", keywords = new[] { "mag", "magazine", "drum", "clip", "ammo" } });

        // Restore custom tabs
        string raw = EditorPrefs.GetString("WT_CustomTabs", "");
        if (!string.IsNullOrEmpty(raw))
        {
            foreach (var entry in raw.Split(','))
            {
                var parts = entry.Split('|');
                if (parts.Length < 3) continue;
                if (!System.Enum.TryParse(parts[1], out AttachmentType aType)) continue;
                if (_tabs.Any(t => !t.isReceiver && t.type == aType)) continue;
                _tabs.Add(new TabEntry { displayName = parts[0], isReceiver = false, type = aType, socketName = parts[2], keywords = new[] { parts[0].ToLowerInvariant() } });
            }
        }

        // Sync custom sockets with Settings so their dummy fields appear there
        for (int t = 5; t < _tabs.Count; t++)
            WeaponToolSettingsWorkbench.RegisterCustomSocket(_tabs[t].socketName);

        while (assetLibrary.Count < _tabs.Count) assetLibrary.Add(new List<GameObject>());
        while (equipLoadout.Count < _tabs.Count) equipLoadout.Add(null);
    }

    private void SaveCustomTabs()
    {
        var entries = _tabs.Skip(5).Select(t => $"{t.displayName}|{(int)t.type}|{t.socketName}");
        EditorPrefs.SetString("WT_CustomTabs", string.Join(",", entries));
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

        // Deferred ExitGUI — must be at end of OnGUI outside all layout groups
        if (_pendingExitGUI)
        {
            _pendingExitGUI = false;
            GUIUtility.ExitGUI();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Left panel
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawLeftWorkbench(float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width), GUILayout.ExpandHeight(false));
        GUILayout.Label("Workbench", EditorStyles.largeLabel);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        if (currentMode == WorkbenchMode.Idle) DrawIdleUI();
        else if (currentMode == WorkbenchMode.Calibration) DrawCalibrationUI();
        else if (currentMode == WorkbenchMode.Equip) DrawEquipUI();
        else if (currentMode == WorkbenchMode.ViewData)
        {
            _detailScroll = GUILayout.BeginScrollView(_detailScroll);
            DrawViewDataUI();
            GUILayout.EndScrollView();
        }

        GUILayout.EndVertical();
    }

    private void DrawIdleUI()
    {
        GUILayout.Space(16);
        WB_BeginBlock();
        WB_LabelDim("Drag prefabs into the right panel to register them.");
        GUILayout.Space(4);
        WB_LabelDim("○  Receiver  →  double-click to calibrate (set sockets)");
        WB_LabelDim("●  Calibrated  →  double-click to assemble");
        WB_LabelDim("✨  Attachments are auto-ready — position set by Receiver sockets");
        GUILayout.Space(4);
        WB_LabelDim("Right-click any asset to view its data.");
        WB_EndBlock();

        GUILayout.Space(10);
        DrawSectionHeader("Auto Tag Assignment");
        WB_BeginBlock();

        bool newVal = EditorGUILayout.ToggleLeft("  Auto-assign tags on drop", WeaponToolSettingsWorkbench.autoAssignTags);
        if (newVal != WeaponToolSettingsWorkbench.autoAssignTags)
        {
            WeaponToolSettingsWorkbench.autoAssignTags = newVal;
            EditorPrefs.SetBool("WT_AutoAssignTags", newVal);
        }

        GUILayout.Space(4);
        WB_LabelDim(
            "When enabled, prefab names are split on underscores and each token " +
            "is checked against the Tag Definitions list. Any match is automatically " +
            "assigned on drop.\n\n" +
            "Naming convention:  Type_Part_Variant  e.g.  Pistol_Muzzle_FlashHider_A");

        GUILayout.Space(8);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1), C_BORDER);
        GUILayout.Space(6);

        WB_LabelDim("Use the button below to retroactively assign missing tags to all loaded prefabs.");
        GUILayout.Space(4);
        if (GUILayout.Button("Assign Missing Tags to All Loaded Prefabs", EditorStyles.miniButton))
            BulkAssignMissingTags();

        WB_EndBlock();

        GUILayout.Space(10);
        DrawSectionHeader("Saved Prefab Components");
        WB_BeginBlock();
        WB_LabelDim(
            "Configure which components and scripts are added to every prefab " +
            "created by Save Full Assembly.\n\n" +
            "Rigidbody, colliders, and custom scripts can all be toggled " +
            "in Global Settings.");
        GUILayout.Space(4);
        if (GUILayout.Button("Open Global Settings →", EditorStyles.miniButton))
            WeaponToolSettingsWorkbench.ShowWindow();
        WB_EndBlock();
    }

    private void DrawCalibrationUI()
    {
        WB_ModeTitle("CALIBRATION MODE");
        WB_BeginBlock();
        WB_LabelDim($"Target:  {currentTargetObject?.name}");
        WB_LabelDim("Move (W) / Rotate (E) to align with the socket dummies.");
        WB_LabelDim("Custom sockets appear as '[Socket] Socket_XXX' in the Hierarchy — move them to position each slot on the gun.");
        WB_EndBlock();
        GUILayout.Space(6);
        bool newState = EditorGUILayout.ToggleLeft("  Show 3D Visual Assist", showCalibrationAssist);
        if (newState != showCalibrationAssist) { showCalibrationAssist = newState; SceneView.RepaintAll(); }
        GUILayout.Space(12);
        WB_ButtonSuccess("Complete Alignment", 44, () => { CompleteCalibration(); GUIUtility.ExitGUI(); });
        GUILayout.Space(4);
        WB_ButtonSecondary("Cancel", 26, () => { ClearWorkbench(); GUIUtility.ExitGUI(); });
    }

    private void DrawEquipUI()
    {
        WB_ModeTitle("ASSEMBLY MODE");
        WB_SectionLabel("Current Loadout");
        WB_BeginBlock();
        EditorGUI.BeginDisabledGroup(true);
        GUIStyle lbl = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TEXT_DIM }, fixedWidth = 72 };
        for (int i = 0; i < _tabs.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(_tabs[i].displayName, lbl);
            EditorGUILayout.ObjectField(equipLoadout[i], typeof(GameObject), false);
            GUILayout.EndHorizontal();
        }
        EditorGUI.EndDisabledGroup();
        WB_EndBlock();
        DrawCombinedStats();
        GUILayout.Space(10);
        WB_ButtonPrimary("Randomize Full Weapon", 34, RandomizeEquipAssembly);
        GUILayout.Space(4);
        WB_ButtonSuccess("Save Full Assembly", 44, () => { SaveEquipAssembly(); GUIUtility.ExitGUI(); });
        GUILayout.Space(4);
        WB_ButtonSecondary("Clear Workbench", 26, () => { ClearWorkbench(); GUIUtility.ExitGUI(); });
    }

    private void DrawCombinedStats()
    {
        if (equipLoadout[0] == null) return;
        if (!bodyDataMap.TryGetValue(equipLoadout[0], out var bodyData)) return;

        var equippedData = new List<AttachmentData>();
        for (int i = 1; i < _tabs.Count; i++)
            if (equipLoadout[i] != null && attachDataMap.TryGetValue(equipLoadout[i], out var ad))
                equippedData.Add(ad);

        if (bodyData.stats.Count == 0 && equippedData.All(a => a.stats.Count == 0)) return;

        var stats = CompatibilityResolver.ComputeStats(bodyData, equippedData);
        GUILayout.Space(6);
        WB_SectionLabel("Stats");
        WB_BeginBlock();

        bool Has(string k) => bodyData.HasStat(k) || equippedData.Any(a => a.HasStat(k));
        void Row(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(110));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        if (Has(StatKeys.Damage)) Row("Damage", stats.damage.ToString("F2"));
        if (Has(StatKeys.FireRate)) Row("Fire Rate", stats.fireRate.ToString("F2"));
        if (Has(StatKeys.Accuracy)) Row("Accuracy", stats.accuracy.ToString("F2"));
        if (Has(StatKeys.ReloadTime)) Row("Reload Time", stats.reloadTime.ToString("F2"));
        if (Has(StatKeys.FireRange)) Row("Fire Range", stats.fireRange.ToString("F2"));
        if (Has(StatKeys.MagSize)) Row("Ammo Capacity", stats.ammoCapacity.ToString());
        if (Has(StatKeys.Weight)) Row("Weight (kg)", stats.weight.ToString("F2"));
        if (stats.customFloat != null) foreach (var kv in stats.customFloat) Row(kv.Key, kv.Value.ToString("F2"));
        if (stats.customInt != null) foreach (var kv in stats.customInt) Row(kv.Key, kv.Value.ToString());
        if (stats.customBool != null) foreach (var kv in stats.customBool) Row(kv.Key, kv.Value ? "True" : "False");

        WB_EndBlock();
    }

    private void DrawViewDataUI()
    {
        if (_selectedPrefab == null)
        {
            GUILayout.EndScrollView();
            currentMode = WorkbenchMode.Idle;
            GUILayout.EndVertical();
            GUIUtility.ExitGUI();
            return;
        }
        WB_ModeTitle("DATA VIEW");
        WB_BeginBlock();
        WB_LabelDim($"Asset:  {_selectedPrefab.name}");
        WB_EndBlock();
        GUILayout.Space(6);
        WB_ButtonSecondary("← Back", 26, () =>
        {
            bool wasAssembling = equipLoadout[0] != null;
            currentMode = wasAssembling ? WorkbenchMode.Equip : WorkbenchMode.Idle;
            _selectedPrefab = null;
            GUIUtility.ExitGUI();
        });
        GUILayout.Space(8);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1), C_BORDER);
        GUILayout.Space(6);
        DrawDetailPanel(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Right panel
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawRightPanel(float width) { DrawRightLibrary(width); }

    private void DrawRightLibrary(float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width), GUILayout.ExpandHeight(false));
        GUILayout.Label("Asset Library", EditorStyles.largeLabel);

        GUILayout.BeginHorizontal();
        float cur = 0, max = width - 52f;
        for (int i = 0; i < _tabs.Count; i++)
        {
            float bw = EditorStyles.toolbarButton.CalcSize(new GUIContent(_tabs[i].displayName)).x;
            if (cur + bw > max) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); cur = 0; }
            if (GUILayout.Toggle(selectedTab == i, _tabs[i].displayName, EditorStyles.toolbarButton, GUILayout.Width(bw)))
                selectedTab = i;
            cur += bw + 4f;
        }
        // Remove custom tab (red − button, only for tabs beyond default 5)
        if (selectedTab >= 5)
        {
            GUI.backgroundColor = C_DANGER;
            if (GUILayout.Button("−", EditorStyles.toolbarButton, GUILayout.Width(22)))
                RemoveCustomTab(selectedTab);
            GUI.backgroundColor = Color.white;
        }
        // Add tab
        GUI.backgroundColor = C_ACCENT_DIM;
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(22)))
            ShowAddTabDialog();
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        DrawAssetList(width);
        GUILayout.EndVertical();
    }

    // ── Add / Remove custom tab ───────────────────────────────────────────────

    private void ShowAddTabDialog()
    {
        var usedTypes = new HashSet<AttachmentType>(_tabs.Where(t => !t.isReceiver).Select(t => t.type));
        var available = System.Enum.GetValues(typeof(AttachmentType))
            .Cast<AttachmentType>().Where(t => !usedTypes.Contains(t)).ToArray();

        if (available.Length == 0)
        {
            EditorUtility.DisplayDialog("No Types Left", "All available attachment types already have a tab.", "OK");
            return;
        }

        AddTabWindow.Show(available, (chosenType) =>
        {
            string socketName = "Socket_" + chosenType.ToString();
            _tabs.Add(new TabEntry
            {
                displayName = chosenType.ToString(),
                isReceiver = false,
                type = chosenType,
                socketName = socketName,
                keywords = new[] { chosenType.ToString().ToLowerInvariant() }
            });
            assetLibrary.Add(new List<GameObject>());
            equipLoadout.Add(null);
            selectedTab = _tabs.Count - 1;
            SaveCustomTabs();

            // Register the new socket in Settings and open the Settings window
            // so the user can assign a dummy prefab for it immediately.
            WeaponToolSettingsWorkbench.RegisterCustomSocket(socketName);
            WeaponToolSettingsWorkbench.ShowForNewTab(socketName);

            Repaint();
        });
    }

    private void RemoveCustomTab(int tabIndex)
    {
        if (tabIndex < 5) return;
        if (!EditorUtility.DisplayDialog("Remove Tab",
            $"Remove the '{_tabs[tabIndex].displayName}' tab?\nPrefabs will be unregistered (data assets are kept).",
            "Remove", "Cancel")) return;

        string removedSocketName = _tabs[tabIndex].socketName;
        foreach (var go in assetLibrary[tabIndex])
        {
            if (go == null) continue;
            calibrationStatus.Remove(go);
            attachDataMap.Remove(go);
        }
        // Unregister from settings so the dummy slot disappears there too
        WeaponToolSettingsWorkbench.UnregisterCustomSocket(removedSocketName);

        _tabs.RemoveAt(tabIndex);
        assetLibrary.RemoveAt(tabIndex);
        equipLoadout.RemoveAt(tabIndex);
        if (selectedTab >= _tabs.Count) selectedTab = _tabs.Count - 1;
        SaveCustomTabs();
        SaveLibrary();
        Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Detail panel
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawDetailPanel(float width)
    {
        if (_selectedPrefab == null) return;
        if (bodyDataMap.TryGetValue(_selectedPrefab, out var bodyData))
            DrawBodyDataDetail(bodyData);
        else if (attachDataMap.TryGetValue(_selectedPrefab, out var attData))
            DrawAttachmentDataDetail(attData);
        else
            GUILayout.Label("No data asset found for this prefab.", EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawBodyDataDetail(GunBodyData data)
    {
        WB_AssetTitle(data.displayName, "GUN BODY");
        GUILayout.Space(6);
        DrawSectionHeader("Compatibility Tags");
        WB_BeginBlock();
        if (data.tags.Count == 0) WB_InfoBox("No tags — default body, accepts any attachment.");
        DrawTagEditor(data);
        WB_EndBlock();
        GUILayout.Space(6);
        DrawSectionHeader("Supported Slots");
        WB_BeginBlock();
        bool slotsChanged = false;
        var allTypes = System.Enum.GetValues(typeof(AttachmentType));
        int col = 0;
        GUILayout.BeginHorizontal();
        foreach (AttachmentType type in allTypes)
        {
            if (col == 2) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); col = 0; }
            bool has = data.slots.Any(s => s.slotType == type);
            if (has) GUI.backgroundColor = C_ACCENT;
            GUIStyle slotStyle = new GUIStyle(EditorStyles.miniButton);
            if (has) slotStyle.normal.textColor = C_BG_BLOCK;
            slotStyle.fontStyle = has ? FontStyle.Bold : FontStyle.Normal;
            bool newVal = GUILayout.Toggle(has, type.ToString(), slotStyle, GUILayout.Width(120), GUILayout.Height(22));
            GUI.backgroundColor = Color.white;
            if (newVal != has) { if (newVal) data.slots.Add(new SlotData { slotType = type }); else data.slots.RemoveAll(s => s.slotType == type); slotsChanged = true; }
            col++;
        }
        GUILayout.EndHorizontal();
        if (slotsChanged)
        {
            EditorUtility.SetDirty(data);
            // Invalidate the runtime slot cache so IsCompatible immediately
            // reflects the change without needing to reload the asset.
            data.InvalidateCache();
        }
        WB_EndBlock();
        GUILayout.Space(4);

        // ── Save Data button ─────────────────────────────────────────────────
        // Manually saving ensures slot/tag/stat changes are written to disk
        // immediately, without needing to click elsewhere or close the window.
        WB_BeginBlock();
        GUI.backgroundColor = new Color(0.2f, 0.5f, 0.9f);
        if (GUILayout.Button("💾  Save Gun Body Data", GUILayout.Height(28)))
        {
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            // Rebuild the slot/tag cache so IsCompatible picks up changes
            // immediately without a domain reload.
            data.InvalidateCache();
            Debug.Log($"[WeaponWorkbench] Saved GunBodyData: {data.displayName}");
        }
        GUI.backgroundColor = Color.white;
        WB_EndBlock();

        GUILayout.Space(6);
        DrawSectionHeader("Stats");
        WB_BeginBlock();
        DrawStatList(data.stats, data);
        WB_EndBlock();
        GUILayout.Space(6);
        DrawSectionHeader("Media");
        WB_BeginBlock();
        if (!_showBodyMedia)
        {
            WB_ButtonAccentSmall("+ Add Media", () =>
            {
                if (data.mediaData == null)
                {
                    data.mediaData = ScriptableObject.CreateInstance<GunAssemblyTool.GunMediaData>();
                    string path = AssetDatabase.GenerateUniqueAssetPath($"{GUNBODY_PATH}/{data.displayName}_Media.asset");
                    AssetDatabase.CreateAsset(data.mediaData, path); AssetDatabase.SaveAssets(); EditorUtility.SetDirty(data);
                }
                _showBodyMedia = true;
            });
        }
        else
        {
            if (data.mediaData != null) DrawMediaEditor(data.mediaData);
            GUILayout.Space(4);
            WB_ButtonDanger("- Remove Media", () => _showBodyMedia = false);
        }
        WB_EndBlock();
    }

    private void DrawAttachmentDataDetail(AttachmentData data)
    {
        WB_AssetTitle(data.displayName, data.attachType.ToString().ToUpper());
        GUILayout.Space(6);
        var allTags = _tagDefs != null ? _tagDefs.AllTags.ToArray() : new string[0];
        DrawSectionHeader("Required Tags  (OR)");
        WB_BeginBlock();
        if (data.requiredTags.Count == 0) WB_InfoBox("No required tags — matches any gun body.");
        DrawTagList(data.requiredTags, allTags, new Color(0.3f, 0.8f, 0.4f), ref _reqTagIdx, data);
        WB_EndBlock();
        GUILayout.Space(6);
        DrawSectionHeader("Forbidden Tags");
        WB_BeginBlock();
        DrawTagList(data.forbiddenTags, allTags, new Color(0.9f, 0.3f, 0.3f), ref _forbTagIdx, data);
        WB_EndBlock();
        GUILayout.Space(6);
        DrawSectionHeader("Stat Bonuses");
        WB_BeginBlock();
        DrawStatList(data.stats, data);
        WB_EndBlock();
        GUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        if (data is BarrelData bar) { WB_BeginBlock(); bar.hpBonus = EditorGUILayout.IntField("HP Bonus (Player)", bar.hpBonus); WB_EndBlock(); }
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(data);
        GUILayout.Space(4);

        // ── Save Data button ─────────────────────────────────────────────────
        WB_BeginBlock();
        GUI.backgroundColor = new Color(0.2f, 0.5f, 0.9f);
        if (GUILayout.Button("💾  Save Attachment Data", GUILayout.Height(28)))
        {
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WeaponWorkbench] Saved AttachmentData: {data.displayName}");
        }
        GUI.backgroundColor = Color.white;
        WB_EndBlock();

        GUILayout.Space(6);
        DrawSectionHeader("Media Override");
        WB_BeginBlock();
        if (!_showAttMedia)
        {
            WB_ButtonAccentSmall("+ Add Media Override", () =>
            {
                if (data.mediaOverride == null)
                {
                    data.mediaOverride = ScriptableObject.CreateInstance<GunAssemblyTool.GunMediaData>();
                    string path = AssetDatabase.GenerateUniqueAssetPath($"{ATTACHMENT_PATH}/{data.displayName}_Media.asset");
                    AssetDatabase.CreateAsset(data.mediaOverride, path); AssetDatabase.SaveAssets(); EditorUtility.SetDirty(data);
                }
                _showAttMedia = true;
            });
        }
        else
        {
            if (data.mediaOverride != null) DrawMediaEditor(data.mediaOverride);
            GUILayout.Space(4);
            WB_ButtonDanger("- Remove Media Override", () => _showAttMedia = false);
        }
        WB_EndBlock();
    }

    private void DrawMediaEditor(GunAssemblyTool.GunMediaData media)
    {
        EditorGUI.BeginChangeCheck();
        DrawSimpleAssetList<AnimationClip>("Animations", media.animations);
        GUILayout.Space(4);
        DrawSimpleAssetList<AudioClip>("SFX", media.sfx);
        GUILayout.Space(4);
        DrawSimpleAssetList<GameObject>("VFX", media.vfx);
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(media);
    }

    private void DrawSimpleAssetList<T>(string label, List<T> list) where T : UnityEngine.Object
    {
        GUILayout.Label(label, EditorStyles.miniBoldLabel);
        int removeIdx = -1;
        for (int i = 0; i < list.Count; i++)
        {
            GUILayout.BeginHorizontal();
            list[i] = (T)EditorGUILayout.ObjectField(list[i], typeof(T), false);
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("✕", GUILayout.Width(22))) removeIdx = i;
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }
        if (removeIdx >= 0) list.RemoveAt(removeIdx);
        GUI.backgroundColor = new Color(0.6f, 0.8f, 0.6f);
        if (GUILayout.Button($"+ {label}", EditorStyles.miniButton)) list.Add(null);
        GUI.backgroundColor = Color.white;
    }

    private void DrawSectionHeader(string title)
    {
        GUILayout.Space(4);
        GUIStyle s = new GUIStyle(EditorStyles.miniBoldLabel) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = C_ACCENT }, margin = new RectOffset(0, 0, 2, 2) };
        GUILayout.Label(title, s);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1), C_BORDER);
        GUILayout.Space(2);
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void WB_ModeTitle(string text)
    {
        Rect r = GUILayoutUtility.GetRect(0, 28);
        EditorGUI.DrawRect(r, C_ACCENT_DIM);
        GUIStyle s = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
        GUI.Label(new Rect(r.x + 10, r.y, r.width, r.height), text, s);
        GUILayout.Space(6);
    }
    private void WB_SectionLabel(string text) { GUIStyle s = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = C_TEXT_MAIN }, fontSize = 11 }; GUILayout.Label(text, s); }
    private void WB_LabelDim(string text) { GUIStyle s = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TEXT_DIM }, wordWrap = true }; GUILayout.Label(text, s); }
    private void WB_InfoBox(string text)
    {
        GUILayout.Space(2);
        GUIStyle s = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = C_TEXT_DIM }, padding = new RectOffset(6, 6, 4, 4) };
        float h = s.CalcHeight(new GUIContent(text), EditorGUIUtility.currentViewWidth - 20) + 8;
        Rect r = GUILayoutUtility.GetRect(0, h);
        EditorGUI.DrawRect(r, C_BG_BLOCK);
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), C_BORDER); EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), C_BORDER);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), C_BORDER); EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), C_BORDER);
        GUI.Label(r, text, s); GUILayout.Space(2);
    }
    private void WB_AssetTitle(string name, string badge)
    {
        Rect r = GUILayoutUtility.GetRect(0, 38);
        EditorGUI.DrawRect(r, C_BG_BLOCK); EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), C_BORDER);
        GUIStyle ts = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = C_TEXT_MAIN }, alignment = TextAnchor.MiddleLeft };
        GUIStyle bs = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_ACCENT }, alignment = TextAnchor.MiddleRight };
        GUI.Label(new Rect(r.x + 8, r.y, r.width * 0.7f, r.height), name, ts);
        GUI.Label(new Rect(r.x, r.y, r.width - 8, r.height), badge, bs); GUILayout.Space(2);
    }
    private void WB_BeginBlock() { GUILayout.Space(2); EditorGUILayout.BeginVertical(); GUILayout.Space(4); }
    private void WB_EndBlock()
    {
        GUILayout.Space(4); EditorGUILayout.EndVertical();
        Rect r = GUILayoutUtility.GetLastRect();
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), C_BORDER); EditorGUI.DrawRect(new Rect(r.x, r.yMax, r.width, 1), C_BORDER);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height + 1), C_BORDER); EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height + 1), C_BORDER);
        GUILayout.Space(2);
    }
    private void WB_ButtonPrimary(string label, float height, System.Action onClick) { GUI.backgroundColor = C_ACCENT_DIM; GUIStyle s = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } }; if (GUILayout.Button(label, s, GUILayout.Height(height))) onClick?.Invoke(); GUI.backgroundColor = Color.white; }
    private void WB_ButtonSuccess(string label, float height, System.Action onClick) { GUI.backgroundColor = C_SUCCESS; GUIStyle s = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } }; if (GUILayout.Button(label, s, GUILayout.Height(height))) onClick?.Invoke(); GUI.backgroundColor = Color.white; }
    private void WB_ButtonSecondary(string label, float height, System.Action onClick) { GUI.backgroundColor = new Color(0.28f, 0.28f, 0.28f); if (GUILayout.Button(label, GUILayout.Height(height))) onClick?.Invoke(); GUI.backgroundColor = Color.white; }
    private void WB_ButtonAccentSmall(string label, System.Action onClick) { GUI.backgroundColor = C_ACCENT_DIM; GUIStyle s = new GUIStyle(EditorStyles.miniButton) { normal = { textColor = Color.white }, fontStyle = FontStyle.Bold }; if (GUILayout.Button(label, s, GUILayout.Height(22))) onClick?.Invoke(); GUI.backgroundColor = Color.white; }
    private void WB_ButtonDanger(string label, System.Action onClick) { GUI.backgroundColor = C_DANGER; GUIStyle s = new GUIStyle(EditorStyles.miniButton) { normal = { textColor = new Color(1f, 0.7f, 0.7f) } }; if (GUILayout.Button(label, s, GUILayout.Height(22))) onClick?.Invoke(); GUI.backgroundColor = Color.white; }

    private void DrawTagList(List<string> tagList, string[] allTags, Color pillColor, ref int idxRef, UnityEngine.Object owner = null)
    {
        var toRemove = new List<string>();
        GUILayout.BeginHorizontal();
        foreach (var t in tagList) { GUI.backgroundColor = pillColor; GUILayout.BeginHorizontal(EditorStyles.helpBox); GUILayout.Label(t, EditorStyles.miniLabel); GUI.backgroundColor = Color.white; if (GUILayout.Button("×", EditorStyles.miniLabel, GUILayout.Width(14))) toRemove.Add(t); GUILayout.EndHorizontal(); }
        GUI.backgroundColor = Color.white; GUILayout.EndHorizontal();
        if (toRemove.Count > 0) { tagList.RemoveAll(t => toRemove.Contains(t)); if (owner != null) EditorUtility.SetDirty(owner); }
        var available = allTags.Where(t => !tagList.Contains(t)).ToArray();
        if (available.Length > 0) { GUILayout.BeginHorizontal(); idxRef = Mathf.Clamp(idxRef, 0, available.Length - 1); idxRef = EditorGUILayout.Popup(idxRef, available, GUILayout.Width(120)); if (GUILayout.Button("+ Add", EditorStyles.miniButton, GUILayout.Width(50))) { tagList.Add(available[idxRef]); if (owner != null) EditorUtility.SetDirty(owner); } GUILayout.EndHorizontal(); }
        else EditorGUILayout.LabelField("All tags added.", EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawTagEditor(GunBodyData data)
    {
        var allTags = _tagDefs != null ? _tagDefs.AllTags.ToArray() : new string[0];
        bool changed = false;
        var toRemove = new List<string>();
        GUILayout.BeginHorizontal();
        foreach (var tag in data.tags) { GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f); GUILayout.BeginHorizontal(EditorStyles.helpBox); GUILayout.Label(tag, EditorStyles.miniLabel); GUI.backgroundColor = Color.white; if (GUILayout.Button("×", EditorStyles.miniLabel, GUILayout.Width(14))) toRemove.Add(tag); GUILayout.EndHorizontal(); }
        GUI.backgroundColor = Color.white; GUILayout.EndHorizontal();
        if (toRemove.Count > 0) { data.tags.RemoveAll(t => toRemove.Contains(t)); changed = true; }
        var available = allTags.Where(t => !data.tags.Contains(t)).ToArray();
        if (available.Length > 0) { GUILayout.BeginHorizontal(); _bodyTagIdx = Mathf.Clamp(_bodyTagIdx, 0, available.Length - 1); _bodyTagIdx = EditorGUILayout.Popup(_bodyTagIdx, available, GUILayout.Width(120)); if (GUILayout.Button("+ Add", EditorStyles.miniButton, GUILayout.Width(50))) { data.tags.Add(available[_bodyTagIdx]); changed = true; } GUILayout.EndHorizontal(); }
        GUILayout.BeginHorizontal();
        var key = "WT_CustomTag_Body"; string custom = EditorGUILayout.TextField(EditorPrefs.GetString(key, ""), GUILayout.Width(100)); EditorPrefs.SetString(key, custom);
        if (GUILayout.Button("+ Custom", EditorStyles.miniButton, GUILayout.Width(60)) && !string.IsNullOrWhiteSpace(custom)) { string norm = custom.Trim().ToLowerInvariant(); if (!data.tags.Contains(norm)) { data.tags.Add(norm); if (_tagDefs != null && !_tagDefs.IsValid(norm)) { _tagDefs.tags.Add(norm); EditorUtility.SetDirty(_tagDefs); } changed = true; } EditorPrefs.SetString(key, ""); }
        GUILayout.EndHorizontal();
        if (changed) EditorUtility.SetDirty(data);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stat list editor
    // ─────────────────────────────────────────────────────────────────────────

    private const string CUSTOM_STATS_PREF = "WT_CustomStatKeys";
    private List<string> LoadCustomStatKeys() { string raw = EditorPrefs.GetString(CUSTOM_STATS_PREF, ""); if (string.IsNullOrEmpty(raw)) return new List<string>(); return new List<string>(raw.Split(',')); }
    private void SaveCustomStatKey(string key) { var keys = LoadCustomStatKeys(); if (!keys.Contains(key)) { keys.Add(key); EditorPrefs.SetString(CUSTOM_STATS_PREF, string.Join(",", keys)); } }
    private void RemoveCustomStatKey(string key) { var keys = LoadCustomStatKeys(); if (keys.Remove(key)) EditorPrefs.SetString(CUSTOM_STATS_PREF, string.Join(",", keys)); }

    private void DrawStatList(List<GunAssemblyTool.StatEntry> statList, UnityEngine.Object owner)
    {
        _statFoldout = EditorGUILayout.Foldout(_statFoldout, $"Stats  ({statList.Count})", true, EditorStyles.foldoutHeader);
        if (!_statFoldout) return;
        EditorGUI.indentLevel++;
        if (statList.Count == 0) EditorGUILayout.LabelField("No stats yet. Add one below.", EditorStyles.centeredGreyMiniLabel);
        else
        {
            int removeIdx = -1;
            for (int i = 0; i < statList.Count; i++)
            {
                var entry = statList[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry.key, GUILayout.Width(90));
                EditorGUI.BeginChangeCheck();
                var newType = (GunAssemblyTool.StatValueType)EditorGUILayout.EnumPopup(entry.valueType, GUILayout.Width(64));
                if (EditorGUI.EndChangeCheck()) { entry.valueType = newType; EditorUtility.SetDirty(owner); _statSaved = false; }
                EditorGUI.BeginChangeCheck();
                switch (entry.valueType)
                {
                    case GunAssemblyTool.StatValueType.Float: entry.floatValue = EditorGUILayout.FloatField(entry.floatValue, GUILayout.Width(80)); break;
                    case GunAssemblyTool.StatValueType.Int: entry.intValue = EditorGUILayout.IntField(entry.intValue, GUILayout.Width(80)); break;
                    case GunAssemblyTool.StatValueType.Bool:
                        GUIStyle bl = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TEXT_DIM } };
                        GUILayout.Space(14); GUILayout.Label(entry.boolValue ? "True" : "False", bl, GUILayout.Width(30));
                        entry.boolValue = EditorGUILayout.Toggle(entry.boolValue, GUILayout.Width(33)); break;
                }
                if (EditorGUI.EndChangeCheck()) { EditorUtility.SetDirty(owner); _statSaved = false; }
                GUI.color = new Color(1f, 0.5f, 0.5f); if (GUILayout.Button("✕", GUILayout.Width(22))) removeIdx = i; GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }
            if (removeIdx >= 0) { statList.RemoveAt(removeIdx); EditorUtility.SetDirty(owner); _statSaved = false; }
        }
        GUILayout.Space(4); EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        var existingKeys = new HashSet<string>(statList.Select(s => s.key));
        var customKeys = LoadCustomStatKeys();
        var allDropdown = GunAssemblyTool.StatKeys.Presets.Concat(customKeys).Distinct().Where(k => !existingKeys.Contains(k)).ToArray();
        if (allDropdown.Length > 0) { GUILayout.BeginHorizontal(); _presetStatIdx = Mathf.Clamp(_presetStatIdx, 0, allDropdown.Length - 1); _presetStatIdx = EditorGUILayout.Popup(_presetStatIdx, allDropdown, GUILayout.Width(110)); if (GUILayout.Button("+ Add", EditorStyles.miniButton, GUILayout.Width(50))) { statList.Add(new GunAssemblyTool.StatEntry(allDropdown[_presetStatIdx], 0f)); _presetStatIdx = 0; EditorUtility.SetDirty(owner); _statSaved = false; } GUILayout.EndHorizontal(); }
        else EditorGUILayout.LabelField("All stats added.", EditorStyles.centeredGreyMiniLabel);
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        _customStatKey = EditorGUILayout.TextField(_customStatKey, GUILayout.Width(100));
        _customStatType = (GunAssemblyTool.StatValueType)EditorGUILayout.EnumPopup(_customStatType, GUILayout.Width(64));
        if (GUILayout.Button("+ Custom", EditorStyles.miniButton, GUILayout.Width(66)) && !string.IsNullOrWhiteSpace(_customStatKey)) { string norm = _customStatKey.Trim(); if (!existingKeys.Contains(norm)) { var ne = new GunAssemblyTool.StatEntry { key = norm, valueType = _customStatType }; statList.Add(ne); SaveCustomStatKey(norm); EditorUtility.SetDirty(owner); _statSaved = false; } _customStatKey = ""; }
        GUILayout.EndHorizontal();
        if (customKeys.Count > 0)
        {
            GUILayout.Space(4); EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            _manageCustomKeys = EditorGUILayout.Foldout(_manageCustomKeys, $"Manage custom keys  ({customKeys.Count})", true);
            if (_manageCustomKeys) { string toRemove = null; foreach (var ck in customKeys) { GUILayout.BeginHorizontal(); GUILayout.Space(16); GUILayout.Label(ck, EditorStyles.miniLabel, GUILayout.Width(120)); GUI.color = new Color(1f, 0.5f, 0.5f); if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20))) toRemove = ck; GUI.color = Color.white; GUILayout.EndHorizontal(); } if (toRemove != null) { RemoveCustomStatKey(toRemove); _presetStatIdx = 0; if (statList.RemoveAll(s => s.key == toRemove) > 0) EditorUtility.SetDirty(owner); } }
        }
        if (statList.Count > 0) { GUILayout.Space(4); GUI.backgroundColor = _statSaved ? new Color(0.3f, 0.6f, 0.3f) : new Color(0.2f, 0.5f, 0.9f); if (GUILayout.Button(_statSaved ? "✔ Saved" : "💾 Save Stats", GUILayout.Height(24))) { EditorUtility.SetDirty(owner); AssetDatabase.SaveAssets(); _statSaved = true; } GUI.backgroundColor = Color.white; }
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

        if (currentList.Count == 0) { GUILayout.Space(20); GUILayout.Label("Drag Prefabs here to add them.", EditorStyles.centeredGreyMiniLabel); return; }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        GUILayout.Space(8);
        float iconSize = 75f, itemWidth = 85f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width - 30f) / itemWidth));

        GUILayout.BeginHorizontal();
        for (int i = 0; i < currentList.Count; i++)
        {
            GameObject obj = currentList[i];
            if (obj == null) { currentList.RemoveAt(i); i--; SaveLibrary(); continue; }
            if (i > 0 && i % columns == 0) { GUILayout.EndHorizontal(); GUILayout.Space(10); GUILayout.BeginHorizontal(); }

            bool isCalibrated = calibrationStatus.ContainsKey(obj) && calibrationStatus[obj];
            bool isSelected = _selectedPrefab == obj;

            GUILayout.Space(6);
            GUILayout.BeginVertical(GUILayout.Width(itemWidth + 12));
            GUILayout.Space(4);

            Texture2D preview = AssetPreview.GetAssetPreview(obj) ?? AssetPreview.GetMiniThumbnail(obj);
            float padding = 6f;
            Rect cardRect = GUILayoutUtility.GetRect(iconSize + padding * 2, iconSize + padding * 2);
            Rect buttonRect = new Rect(cardRect.x + padding, cardRect.y + padding, iconSize, iconSize);
            bool isHover = buttonRect.Contains(Event.current.mousePosition);
            Event e = Event.current;

            if (isSelected) { Handles.BeginGUI(); Handles.color = new Color(0.9f, 0.9f, 0.9f, 1f); Handles.DrawAAPolyLine(4f, new Vector3(cardRect.x, cardRect.y), new Vector3(cardRect.xMax, cardRect.y), new Vector3(cardRect.xMax, cardRect.yMax), new Vector3(cardRect.x, cardRect.yMax), new Vector3(cardRect.x, cardRect.y)); Handles.EndGUI(); }
            if (isHover && !isSelected) { Handles.BeginGUI(); Handles.color = new Color(1f, 1f, 1f, 0.15f); Handles.DrawAAPolyLine(2f, new Vector3(buttonRect.x - 2, buttonRect.y - 2), new Vector3(buttonRect.xMax + 2, buttonRect.y - 2), new Vector3(buttonRect.xMax + 2, buttonRect.yMax + 2), new Vector3(buttonRect.x - 2, buttonRect.yMax + 2), new Vector3(buttonRect.x - 2, buttonRect.y - 2)); Handles.EndGUI(); }

            // Right-click
            if (e.type == EventType.MouseDown && e.button == 1 && buttonRect.Contains(e.mousePosition))
            {
                GameObject menuObj = obj; int menuTab = selectedTab;
                GenericMenu menu = new GenericMenu();
                if (isCalibrated) menu.AddItem(new GUIContent("🔧 Equip Part"), false, () => EnterEquipMode(menuObj, menuTab));
                menu.AddItem(new GUIContent("📋 View Data"), false, () => { _selectedPrefab = menuObj; currentMode = WorkbenchMode.ViewData; });
                menu.AddItem(new GUIContent("📐 (Re)Calibrate"), false, () => EnterCalibrationMode(menuObj, menuTab));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("🗑️ Remove"), false, () =>
                {
                    if (menuTab == 0 && bodyDataMap.TryGetValue(menuObj, out var bd)) { string p = AssetDatabase.GetAssetPath(bd); if (!string.IsNullOrEmpty(p)) AssetDatabase.DeleteAsset(p); }
                    else if (menuTab > 0 && attachDataMap.TryGetValue(menuObj, out var ad)) { string p = AssetDatabase.GetAssetPath(ad); if (!string.IsNullOrEmpty(p)) AssetDatabase.DeleteAsset(p); }
                    assetLibrary[menuTab].Remove(menuObj);
                    calibrationStatus.Remove(menuObj); bodyDataMap.Remove(menuObj); attachDataMap.Remove(menuObj);
                    bool wasViewing = (_selectedPrefab == menuObj && currentMode == WorkbenchMode.ViewData);
                    if (_selectedPrefab == menuObj) { _selectedPrefab = null; currentMode = WorkbenchMode.Idle; }
                    SaveLibrary(); RefreshRegistry(); AssetDatabase.Refresh();
                    if (wasViewing) _pendingExitGUI = true; // deferred — avoids ExitGUIException in callbacks
                });
                menu.ShowAsContext(); e.Use();
            }
            // Double-click
            else if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && buttonRect.Contains(e.mousePosition))
            {
                if (selectedTab == 0) { if (isCalibrated) EnterEquipMode(obj, selectedTab); else EnterCalibrationMode(obj, selectedTab); }
                else { if (!isCalibrated) MarkAttachmentReady(obj, selectedTab); EnterEquipMode(obj, selectedTab); }
                e.Use();
            }
            // Single-click
            else if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 1 && buttonRect.Contains(e.mousePosition))
            {
                if (currentMode == WorkbenchMode.ViewData) _selectedPrefab = obj;
                else _selectedPrefab = (_selectedPrefab == obj) ? null : obj;
                e.Use(); Repaint();
            }

            GUI.Box(buttonRect, preview);
            Rect statusRect = new Rect(cardRect.xMax - 24, cardRect.yMax - 24, 20, 20);
            GUIStyle statusSt = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            if (isCalibrated) GUI.Label(statusRect, "✅", statusSt);
            else { GUI.color = Color.yellow; GUI.Label(statusRect, "⏳", statusSt); GUI.color = Color.white; }
            GUIStyle labelSt = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, clipping = TextClipping.Clip };
            GUILayout.Label(obj.name, labelSt, GUILayout.Width(iconSize));
            GUILayout.Space(6); GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drag and drop
    // ─────────────────────────────────────────────────────────────────────────

    private bool MatchesTabKeywords(string prefabName, int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabs.Count) return true;
        var kws = _tabs[tabIndex].keywords;
        if (kws == null || kws.Length == 0) return true;
        string lower = prefabName.ToLowerInvariant();
        return kws.Any(k => lower.Contains(k));
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
        if (!dropArea.Contains(evt.mousePosition)) return;

        bool anyValid = DragAndDrop.objectReferences.Any(o =>
        {
            if (!(o is GameObject go)) return false;
            string path = AssetDatabase.GetAssetPath(go).ToLower();
            return (PrefabUtility.IsPartOfPrefabAsset(go) || path.EndsWith(".fbx") || path.EndsWith(".obj"))
                   && MatchesTabKeywords(go.name, selectedTab);
        });
        DragAndDrop.visualMode = anyValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

        if (evt.type == EventType.DragPerform && anyValid)
        {
            DragAndDrop.AcceptDrag();
            bool changed = false, rejected = false;
            foreach (Object draggedObj in DragAndDrop.objectReferences)
            {
                if (!(draggedObj is GameObject go)) continue;
                string path = AssetDatabase.GetAssetPath(go).ToLower();
                if (!(PrefabUtility.IsPartOfPrefabAsset(go) || path.EndsWith(".fbx") || path.EndsWith(".obj"))) continue;
                if (!MatchesTabKeywords(go.name, selectedTab)) { rejected = true; continue; }
                if (assetLibrary[selectedTab].Contains(go)) continue;
                assetLibrary[selectedTab].Add(go);
                if (selectedTab == 0)
                {
                    AutoCreateGunBodyData(go);
                    calibrationStatus[go] = CheckIfReceiverCalibrated(go);
                    if (WeaponToolSettingsWorkbench.autoAssignTags && bodyDataMap.TryGetValue(go, out var bd)) AutoAssignTagsFromName(go, bd.tags, bd);
                }
                else
                {
                    AutoCreateAttachmentData(go, selectedTab);
                    MarkAttachmentReady(go, selectedTab);
                    if (WeaponToolSettingsWorkbench.autoAssignTags && attachDataMap.TryGetValue(go, out var ad)) AutoAssignTagsFromName(go, ad.requiredTags, ad);
                }
                changed = true;
            }
            if (rejected && !changed)
            {
                var kws = _tabs[selectedTab].keywords;
                EditorUtility.DisplayDialog("Wrong Tab", $"The dragged prefab does not match the '{_tabs[selectedTab].displayName}' tab.\nExpected name keywords: {string.Join(", ", kws ?? new[] { "any" })}", "OK");
            }
            if (changed) { SaveLibrary(); RefreshRegistry(); Repaint(); }
        }
        evt.Use();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MarkAttachmentReady
    // ─────────────────────────────────────────────────────────────────────────

    private void MarkAttachmentReady(GameObject go, int tabIndex)
    {
        calibrationStatus[go] = true;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(go));
        if (!string.IsNullOrEmpty(guid))
        {
            var existing = new HashSet<string>(EditorPrefs.GetString("WT_AssetLib_Calibrated", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            existing.Add(guid);
            EditorPrefs.SetString("WT_AssetLib_Calibrated", string.Join(",", existing));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tag auto-assignment
    // ─────────────────────────────────────────────────────────────────────────

    private int AutoAssignTagsFromName(GameObject prefab, List<string> tagList, Object owner)
    {
        if (_tagDefs == null || prefab == null || tagList == null || owner == null) return 0;
        string[] tokens = prefab.name.ToLowerInvariant().Split(new[] { ' ', '_', '-', '.' }, System.StringSplitOptions.RemoveEmptyEntries);
        int added = 0;
        foreach (string token in tokens) if (_tagDefs.IsValid(token) && !tagList.Contains(token)) { tagList.Add(token); added++; }
        if (added > 0) { EditorUtility.SetDirty(owner); AssetDatabase.SaveAssets(); }
        return added;
    }

    private void BulkAssignMissingTags()
    {
        int total = 0;
        foreach (var go in assetLibrary[0]) if (go != null && bodyDataMap.TryGetValue(go, out var bd)) total += AutoAssignTagsFromName(go, bd.tags, bd);
        for (int tab = 1; tab < assetLibrary.Count; tab++) foreach (var go in assetLibrary[tab]) if (go != null && attachDataMap.TryGetValue(go, out var ad)) total += AutoAssignTagsFromName(go, ad.requiredTags, ad);
        Debug.Log(total > 0 ? $"[WeaponWorkbench] Bulk tag assignment — {total} tag(s) added." : "[WeaponWorkbench] Bulk tag assignment: nothing to add.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auto-create data assets
    // ─────────────────────────────────────────────────────────────────────────

    private void AutoCreateGunBodyData(GameObject prefab)
    {
        if (bodyDataMap.ContainsKey(prefab)) return;
        var data = ScriptableObject.CreateInstance<GunBodyData>();
        data.bodyId = prefab.name.ToLowerInvariant().Replace(" ", "_"); data.displayName = prefab.name; data.bodyPrefab = prefab; data.partObject = prefab;
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{GUNBODY_PATH}/{prefab.name}_Body.asset");
        AssetDatabase.CreateAsset(data, assetPath); AssetDatabase.SaveAssets();
        bodyDataMap[prefab] = data;
        Debug.Log($"[WeaponWorkbench] Created GunBodyData -> {assetPath}");
    }

    private void SyncDataNames()
    {
        bool dirty = false;
        foreach (var kv in bodyDataMap) { if (kv.Key == null || kv.Value == null) continue; string n = kv.Key.name; var d = kv.Value; if (d.displayName != n) { d.displayName = n; d.bodyId = n.ToLowerInvariant().Replace(" ", "_"); EditorUtility.SetDirty(d); string op = AssetDatabase.GetAssetPath(d); AssetDatabase.MoveAsset(op, AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.GetDirectoryName(op).Replace('\\', '/') + "/" + n + "_Body.asset")); dirty = true; } }
        foreach (var kv in attachDataMap) { if (kv.Key == null || kv.Value == null) continue; string n = kv.Key.name; var d = kv.Value; if (d.displayName != n) { d.displayName = n; d.attachmentId = n.ToLowerInvariant().Replace(" ", "_"); EditorUtility.SetDirty(d); string op = AssetDatabase.GetAssetPath(d); AssetDatabase.MoveAsset(op, AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.GetDirectoryName(op).Replace('\\', '/') + "/" + n + "_" + d.attachType + ".asset")); dirty = true; } }
        if (dirty) AssetDatabase.SaveAssets();
    }

    private void AutoCreateAttachmentData(GameObject prefab, int tabIndex)
    {
        if (attachDataMap.ContainsKey(prefab)) return;
        AttachmentType type = _tabs[tabIndex].type;
        AttachmentData data = CreateAttachmentDataOfType(type);
        data.attachmentId = prefab.name.ToLowerInvariant().Replace(" ", "_"); data.displayName = prefab.name; data.attachType = type; data.attachmentPrefab = prefab;
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{ATTACHMENT_PATH}/{prefab.name}_{type}.asset");
        AssetDatabase.CreateAsset(data, assetPath); AssetDatabase.SaveAssets();
        attachDataMap[prefab] = data;
        Debug.Log($"[WeaponWorkbench] Created {type}Data -> {assetPath}");
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
    // Calibration — Receiver only
    // ─────────────────────────────────────────────────────────────────────────

    private bool CheckIfReceiverCalibrated(GameObject go)
    {
        return go.transform.Find("Socket_Muzzle") != null && go.transform.Find("Socket_Optic") != null &&
               go.transform.Find("Socket_Stock") != null && go.transform.Find("Socket_Magazine") != null;
    }

    private void EnterCalibrationMode(GameObject targetPrefab, int tabIndex)
    {
        if (WeaponToolSettingsWorkbench.dummyBody == null || WeaponToolSettingsWorkbench.dummyMuzzle == null)
        {
            if (EditorUtility.DisplayDialog("Missing Dummy", "Please configure base dummies first.\nGo to settings now?", "Settings", "Cancel"))
                WeaponToolSettingsWorkbench.ShowWindow();
            return;
        }
        ClearWorkbench();
        currentMode = WorkbenchMode.Calibration; currentPrefabAsset = targetPrefab;
        currentTargetObject = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab);
        PrefabUtility.UnpackPrefabInstance(currentTargetObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        if (tabIndex == 0)
        {
            currentTargetObject.transform.position = Vector3.zero;

            // Default 4 sockets
            var mD = SpawnDummy(WeaponToolSettingsWorkbench.dummyMuzzle, "Socket_Muzzle");
            var oD = SpawnDummy(WeaponToolSettingsWorkbench.dummyScope, "Socket_Optic");
            var sD = SpawnDummy(WeaponToolSettingsWorkbench.dummyStock, "Socket_Stock");
            var mgD = SpawnDummy(WeaponToolSettingsWorkbench.dummyMag, "Socket_Magazine");
            SnapDummy(mD, currentTargetObject, "Socket_Muzzle");
            SnapDummy(oD, currentTargetObject, "Socket_Optic");
            SnapDummy(sD, currentTargetObject, "Socket_Stock");
            SnapDummy(mgD, currentTargetObject, "Socket_Magazine");

            // Custom tab sockets (e.g. Socket_Grip, Socket_Underbarrel, Socket_Skin).
            // Each gets its own distinctly-named scene object so the user can tell
            // them apart from the default dummies in the hierarchy and scene view.
            for (int t = 5; t < _tabs.Count; t++)
            {
                string customSocket = _tabs[t].socketName;
                if (string.IsNullOrEmpty(customSocket)) continue;
                if (WeaponToolSettingsWorkbench.dummyMuzzle == null) continue;

                // Use the assigned custom dummy prefab if available,
                // otherwise fall back to dummyMuzzle (so calibration works
                // even before the user assigns a dedicated prefab in Settings).
                GameObject customDummyPrefab =
                    WeaponToolSettingsWorkbench.GetCustomDummy(customSocket);
                if (customDummyPrefab == null) continue;
                GameObject customDummy = (GameObject)PrefabUtility.InstantiatePrefab(
                    customDummyPrefab);
                customDummy.transform.position = Vector3.zero;
                // Use the socket name as the scene label so the user knows exactly
                // which socket they are positioning (e.g. "[Socket] Socket_Grip").
                customDummy.name = "[Socket] " + customSocket;
                currentDummies.Add(customDummy);
                dummyToSocketMap[customDummy] = customSocket;

                // If this socket already exists on the prefab, snap the dummy there
                // so the user can review or fine-tune the existing position.
                SnapDummy(customDummy, currentTargetObject, customSocket);
            }
        }
        else
        {
            SpawnDummy(WeaponToolSettingsWorkbench.dummyBody, "Body");
            string sn = _tabs[tabIndex].socketName;
            Transform socket = currentDummies[0].transform.Find(sn);
            currentTargetObject.transform.position = socket != null ? socket.position : Vector3.zero;
            if (socket != null) currentTargetObject.transform.rotation = socket.rotation;
        }
        Selection.activeGameObject = currentTargetObject;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private void SnapDummy(GameObject dummy, GameObject body, string socketName) { if (dummy == null) return; Transform s = body.transform.Find(socketName); if (s != null) { dummy.transform.position = s.position; dummy.transform.rotation = s.rotation; } }
    private GameObject SpawnDummy(GameObject dummyPrefab, string socketName) { if (dummyPrefab == null) return null; GameObject dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab); dummy.transform.position = Vector3.zero; dummy.name = "[Dummy] " + dummyPrefab.name; currentDummies.Add(dummy); dummyToSocketMap[dummy] = socketName; return dummy; }

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
            calibrationStatus.Remove(currentPrefabAsset); calibrationStatus[saved] = true;
            if (selectedTab == 0 && bodyDataMap.TryGetValue(currentPrefabAsset, out var bd)) { bd.bodyPrefab = saved; bd.partObject = saved; bodyDataMap.Remove(currentPrefabAsset); bodyDataMap[saved] = bd; EditorUtility.SetDirty(bd); }
            else if (selectedTab > 0 && attachDataMap.TryGetValue(currentPrefabAsset, out var ad)) { ad.attachmentPrefab = saved; attachDataMap.Remove(currentPrefabAsset); attachDataMap[saved] = ad; EditorUtility.SetDirty(ad); }
            SaveLibrary(); RefreshRegistry();
            Debug.Log($"[WeaponWorkbench] Calibrated: {savePath}");
        }
        ClearWorkbench();
    }

    private bool CalibrateGunBody()
    {
        foreach (var kvp in dummyToSocketMap)
        {
            Transform socket = currentTargetObject.transform.Find(kvp.Value);
            if (socket == null) { var sObj = new GameObject(kvp.Value); socket = sObj.transform; socket.SetParent(currentTargetObject.transform, true); }
            socket.position = kvp.Key.transform.position; socket.rotation = kvp.Key.transform.rotation;
            Vector3 ps = currentTargetObject.transform.lossyScale;
            socket.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);
        }
        return true;
    }

    private bool CalibrateAttachment(int tabIndex)
    {
        string socketName = _tabs[tabIndex].socketName;
        Transform targetSocket = currentDummies.Count > 0 ? currentDummies[0].transform.Find(socketName) : null;
        if (targetSocket == null) return false;
        if (currentTargetObject.GetComponent<MeshRenderer>() != null)
        {
            GameObject newRoot = new GameObject(currentTargetObject.name + "_Prefab");
            newRoot.transform.position = targetSocket.position; newRoot.transform.rotation = targetSocket.rotation;
            currentTargetObject.transform.SetParent(newRoot.transform, true); currentTargetObject = newRoot;
        }
        else
        {
            var children = new List<Transform>(); foreach (Transform child in currentTargetObject.transform) children.Add(child);
            foreach (Transform child in children) child.SetParent(null, true);
            currentTargetObject.transform.position = targetSocket.position; currentTargetObject.transform.rotation = targetSocket.rotation;
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
        currentMode = WorkbenchMode.Equip; equipLoadout[tabIndex] = obj;
        RefreshEquipAssembly(tabIndex == 0);
    }

    private void RefreshEquipAssembly(bool silentClear = false)
    {
        if (equipAssemblyRoot != null) DestroyImmediate(equipAssemblyRoot);
        equipAssemblyRoot = new GameObject("[Assembling] New Weapon");
        equipAssemblyRoot.transform.position = Vector3.zero;

        GameObject bodyInstance = null;
        if (equipLoadout[0] != null) { bodyInstance = (GameObject)PrefabUtility.InstantiatePrefab(equipLoadout[0], equipAssemblyRoot.transform); bodyInstance.transform.localPosition = Vector3.zero; bodyInstance.transform.localRotation = Quaternion.identity; }

        for (int i = 1; i < _tabs.Count; i++)
        {
            if (equipLoadout[i] == null) continue;
            if (equipLoadout[0] != null && bodyDataMap.TryGetValue(equipLoadout[0], out var bd) && attachDataMap.TryGetValue(equipLoadout[i], out var ad))
            {
                if (!CompatibilityResolver.IsCompatible(bd, ad))
                {
                    string reason = CompatibilityResolver.GetIncompatibleReason(bd, ad);
                    if (!silentClear) { Debug.LogWarning($"[WeaponWorkbench] '{equipLoadout[i].name}' rejected: {reason}"); EditorUtility.DisplayDialog("Incompatible Part", $"'{equipLoadout[i].name}' cannot be attached to '{equipLoadout[0].name}'.\n\n{reason}", "OK"); }
                    else Debug.Log($"[WeaponWorkbench] Cleared '{equipLoadout[i].name}' (incompatible).");
                    equipLoadout[i] = null; continue;
                }
            }
            GameObject acc = (GameObject)PrefabUtility.InstantiatePrefab(equipLoadout[i]);
            if (bodyInstance != null)
            {
                string sn = _tabs[i].socketName; Transform socket = bodyInstance.transform.Find(sn);
                if (socket != null) { acc.transform.SetParent(socket, false); acc.transform.localPosition = Vector3.zero; acc.transform.localRotation = Quaternion.identity; }
                else { acc.transform.SetParent(equipAssemblyRoot.transform, false); Debug.LogWarning($"[WeaponWorkbench] Missing '{sn}' on receiver."); }
            }
            else acc.transform.SetParent(equipAssemblyRoot.transform, false);
        }
        Selection.activeGameObject = equipAssemblyRoot;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Randomize
    // ─────────────────────────────────────────────────────────────────────────

    private void RandomizeEquipAssembly()
    {
        bool hasValidBody = false;
        for (int i = 0; i < _tabs.Count; i++)
        {
            var validAssets = new List<GameObject>();
            foreach (var go in assetLibrary[i])
            {
                if (go == null || !calibrationStatus.ContainsKey(go) || !calibrationStatus[go]) continue;
                if (i > 0 && equipLoadout[0] != null && bodyDataMap.TryGetValue(equipLoadout[0], out var bd) && attachDataMap.TryGetValue(go, out var ad))
                    if (!CompatibilityResolver.IsCompatible(bd, ad)) continue;
                validAssets.Add(go);
            }
            if (validAssets.Count > 0) { equipLoadout[i] = validAssets[Random.Range(0, validAssets.Count)]; if (i == 0) hasValidBody = true; }
            else equipLoadout[i] = null;
        }
        if (!hasValidBody) { EditorUtility.DisplayDialog("Notice", "Missing a calibrated Receiver. Cannot randomize!", "OK"); return; }
        RefreshEquipAssembly(silentClear: true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Save / Load
    // ─────────────────────────────────────────────────────────────────────────

    private void SaveEquipAssembly()
    {
        if (equipAssemblyRoot == null || equipLoadout[0] == null)
        {
            EditorUtility.DisplayDialog("Cannot Save", "A Receiver must be included to save.", "OK");
            return;
        }
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Full Weapon", "NewWeaponLoadout", "prefab", "Select save path");
        if (string.IsNullOrEmpty(path)) return;

        // ── 1. GunRuntimeData — data contract read by the minigame ───────────
        var runtimeData = equipAssemblyRoot.GetComponent<GunAssemblyTool.GunRuntimeData>()
                       ?? equipAssemblyRoot.AddComponent<GunAssemblyTool.GunRuntimeData>();
        bodyDataMap.TryGetValue(equipLoadout[0], out runtimeData.body);
        runtimeData.attachments.Clear();
        for (int i = 1; i < _tabs.Count; i++)
            if (equipLoadout[i] != null && attachDataMap.TryGetValue(equipLoadout[i], out var ad))
                runtimeData.attachments.Add(ad);

        // ── 2. Physics — Editor can reference these types directly ────────────
        InjectUserComponents(equipAssemblyRoot);

        // ── 3. BulletSpawnPoint — child GO placed at the muzzle tip ──────────
        Transform spawnPoint = equipAssemblyRoot.transform.Find("BulletSpawnPoint");
        if (spawnPoint == null)
        {
            var spawnGO = new GameObject("BulletSpawnPoint");
            spawnGO.transform.SetParent(equipAssemblyRoot.transform, false);
            spawnGO.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            spawnPoint = spawnGO.transform;
        }

        // ── 4. Minigame scripts — added via reflection ───────────────────────
        System.Type FindType(string name)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null) return t;
            }
            Debug.LogWarning($"[WeaponWorkbench] Type '{name}' not found. " +
                             "Make sure all minigame scripts are compiled.");
            return null;
        }

        Component GetOrAdd(System.Type type)
        {
            if (type == null) return null;
            return equipAssemblyRoot.GetComponent(type)
                ?? equipAssemblyRoot.AddComponent(type);
        }

        GetOrAdd(FindType("GunData"));
        GetOrAdd(FindType("PickUpController"));
        GetOrAdd(FindType("WeaponCollection"));
        GetOrAdd(FindType("WeaponDetection"));
        GetOrAdd(FindType("GunSetup"));

        // ── 5. Save prefab first ──────────────────────────────────────────────
        // Save the scene GO to disk first, then reopen via LoadPrefabContents
        // to wire cross-component references — the only reliable way to persist
        // component refs in a prefab across asmdef boundaries.
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
            equipAssemblyRoot, path, InteractionMode.UserAction);

        if (saved == null)
        {
            Debug.LogError("[WeaponWorkbench] SaveAsPrefabAssetAndConnect failed.");
            return;
        }

        // ── 6. Wire references on the saved prefab asset ──────────────────────
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);

        System.Type FindType2(string name)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null) return t;
            }
            return null;
        }

        void SetProp(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null) prop.objectReferenceValue = value;
        }

        var prefabRb = prefabRoot.GetComponent<Rigidbody>();
        var prefabTrigger = System.Array.Find(
            prefabRoot.GetComponents<BoxCollider>(), c => c.isTrigger);
        var prefabSpawn = prefabRoot.transform.Find("BulletSpawnPoint");

        var tGunData = FindType2("GunData");
        var tPickUp = FindType2("PickUpController");
        var tWeaponCol = FindType2("WeaponCollection");

        var prefabRuntimeData = prefabRoot.GetComponent<GunAssemblyTool.GunRuntimeData>();
        var prefabGunData = tGunData != null ? prefabRoot.GetComponent(tGunData) : null;
        var prefabPickUp = tPickUp != null ? prefabRoot.GetComponent(tPickUp) : null;
        var prefabWeaponCol = tWeaponCol != null ? prefabRoot.GetComponent(tWeaponCol) : null;

        if (prefabGunData != null)
        {
            var so = new SerializedObject(prefabGunData);
            SetProp(so, "runtimeData", prefabRuntimeData);
            SetProp(so, "bulletSpawnLocation", prefabSpawn != null ? prefabSpawn.gameObject : null);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (prefabPickUp != null)
        {
            var so = new SerializedObject(prefabPickUp);
            SetProp(so, "gun", prefabGunData);
            SetProp(so, "rb", prefabRb);
            SetProp(so, "coll", prefabTrigger);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (prefabWeaponCol != null)
        {
            var so = new SerializedObject(prefabWeaponCol);
            SetProp(so, "gunData", prefabGunData);
            SetProp(so, "pickUpController", prefabPickUp);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Write back to disk and unload the prefab editing context
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log($"[WeaponWorkbench] Saved and wired: {path}");
        EditorGUIUtility.PingObject(saved);
    }

    private static Rigidbody InjectUserComponents(GameObject go)
    {
        // ── Rigidbody ─────────────────────────────────────────────────────────
        Rigidbody rb = null;
        if (WeaponToolSettingsWorkbench.addRigidbody)
            rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();

        // ── Trigger BoxCollider (pickup range / WeaponDetection) ──────────────
        if (WeaponToolSettingsWorkbench.addTriggerCollider)
        {
            BoxCollider trigger = null;
            foreach (var c in go.GetComponents<BoxCollider>())
                if (c.isTrigger) { trigger = c; break; }
            if (trigger == null)
            {
                trigger = go.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(1.5f, 0.5f, 1.5f);
                trigger.center = new Vector3(0f, 0.25f, 0f);
            }
        }

        // ── Solid BoxCollider (physics when gun lies on the ground) ───────────
        if (WeaponToolSettingsWorkbench.addSolidCollider)
        {
            BoxCollider solid = null;
            foreach (var c in go.GetComponents<BoxCollider>())
                if (!c.isTrigger) { solid = c; break; }
            if (solid == null)
            {
                solid = go.AddComponent<BoxCollider>();
                solid.isTrigger = false;
                solid.size = new Vector3(0.8f, 0.2f, 0.3f);
                solid.center = Vector3.zero;
            }
        }

        // ── User scripts ──────────────────────────────────────────────────────
        foreach (var monoScript in WeaponToolSettingsWorkbench.scriptsToAdd)
        {
            if (monoScript == null) continue;

            System.Type type = monoScript.GetClass();
            if (type == null)
            {
                Debug.LogWarning($"[WeaponWorkbench] Could not resolve type for " +
                                 $"'{monoScript.name}' — is it compiled?");
                continue;
            }
            if (!type.IsSubclassOf(typeof(Component)))
            {
                Debug.LogWarning($"[WeaponWorkbench] '{type.Name}' is not a " +
                                 $"Component — skipping.");
                continue;
            }
            if (go.GetComponent(type) == null)
                go.AddComponent(type);
        }

        return rb;
    }


    private void ClearWorkbench()
    {
        if (currentTargetObject != null) DestroyImmediate(currentTargetObject);
        foreach (var d in currentDummies) if (d != null) DestroyImmediate(d);
        currentDummies.Clear(); dummyToSocketMap.Clear(); currentTargetObject = null; currentPrefabAsset = null;
        if (equipAssemblyRoot != null) DestroyImmediate(equipAssemblyRoot);
        for (int i = 0; i < equipLoadout.Count; i++) equipLoadout[i] = null;
        currentMode = WorkbenchMode.Idle;
    }

    private void SaveLibrary()
    {
        EditorPrefs.SetInt("WT_TabCount", assetLibrary.Count);
        for (int i = 0; i < assetLibrary.Count; i++)
        {
            var guids = assetLibrary[i].Where(go => go != null).Select(go => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(go)));
            EditorPrefs.SetString("WT_AssetLib_Tab_" + i, string.Join(",", guids));
        }
        var calibrated = calibrationStatus.Where(kvp => kvp.Key != null && kvp.Value).Select(kvp => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Key))).Where(g => !string.IsNullOrEmpty(g));
        EditorPrefs.SetString("WT_AssetLib_Calibrated", string.Join(",", calibrated));
        var bodyMap = bodyDataMap.Where(kvp => kvp.Key != null && kvp.Value != null).Select(kvp => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Key)) + ":" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Value)));
        EditorPrefs.SetString("WT_BodyDataMap", string.Join(",", bodyMap));
        var attMap = attachDataMap.Where(kvp => kvp.Key != null && kvp.Value != null).Select(kvp => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Key)) + ":" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Value)));
        EditorPrefs.SetString("WT_AttachDataMap", string.Join(",", attMap));
        SaveDataGuidMap();
    }

    private void SaveDataGuidMap()
    {
        foreach (var kv in bodyDataMap) { if (kv.Key == null || kv.Value == null) continue; string dp = AssetDatabase.GetAssetPath(kv.Value); string dg = AssetDatabase.AssetPathToGUID(dp); string pg = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kv.Key)); if (string.IsNullOrEmpty(dg) || string.IsNullOrEmpty(pg)) continue; EditorPrefs.SetString("WT_DataGuid_" + dp, dg); EditorPrefs.SetString("WT_DataToPrefab_" + dg, pg); }
        foreach (var kv in attachDataMap) { if (kv.Key == null || kv.Value == null) continue; string dp = AssetDatabase.GetAssetPath(kv.Value); string dg = AssetDatabase.AssetPathToGUID(dp); string pg = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kv.Key)); if (string.IsNullOrEmpty(dg) || string.IsNullOrEmpty(pg)) continue; EditorPrefs.SetString("WT_DataGuid_" + dp, dg); EditorPrefs.SetString("WT_DataToPrefab_" + dg, pg); }
    }

    private void LoadLibrary()
    {
        while (assetLibrary.Count < _tabs.Count) assetLibrary.Add(new List<GameObject>());
        while (equipLoadout.Count < _tabs.Count) equipLoadout.Add(null);
        calibrationStatus.Clear();
        var calibratedSet = new HashSet<string>(EditorPrefs.GetString("WT_AssetLib_Calibrated", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        int savedTabCount = EditorPrefs.GetInt("WT_TabCount", _tabs.Count);
        for (int i = 0; i < savedTabCount && i < assetLibrary.Count; i++)
        {
            assetLibrary[i] = new List<GameObject>();
            string raw = EditorPrefs.GetString("WT_AssetLib_Tab_" + i, "");
            foreach (var guid in raw.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null) continue;
                assetLibrary[i].Add(go);
                calibrationStatus[go] = (i == 0) ? calibratedSet.Contains(guid) || CheckIfReceiverCalibrated(go) : true;
            }
        }
        bodyDataMap.Clear();
        foreach (var pair in EditorPrefs.GetString("WT_BodyDataMap", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)) { var p = pair.Split(':'); if (p.Length != 2) continue; var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(p[0])); var data = AssetDatabase.LoadAssetAtPath<GunBodyData>(AssetDatabase.GUIDToAssetPath(p[1])); if (go != null && data != null) bodyDataMap[go] = data; }
        attachDataMap.Clear();
        foreach (var pair in EditorPrefs.GetString("WT_AttachDataMap", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)) { var p = pair.Split(':'); if (p.Length != 2) continue; var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(p[0])); var data = AssetDatabase.LoadAssetAtPath<AttachmentData>(AssetDatabase.GUIDToAssetPath(p[1])); if (go != null && data != null) attachDataMap[go] = data; }
        SyncDataNames();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scene view gizmos
    // ─────────────────────────────────────────────────────────────────────────

    private void OnSceneGUI(SceneView sv)
    {
        if (currentMode != WorkbenchMode.Calibration || currentTargetObject == null || !showCalibrationAssist) return;
        if (selectedTab == 0) { foreach (var dummy in currentDummies) if (dummy != null) DrawSocketVisual(dummy.transform.position, dummy.transform.rotation, dummy.name.Replace("[Dummy] ", "")); }
        else if (currentDummies.Count > 0 && currentDummies[0] != null) { string sn = _tabs[selectedTab].socketName; Transform s = currentDummies[0].transform.Find(sn); if (s != null) DrawSocketVisual(s.position, s.rotation, sn); }
    }

    private void DrawSocketVisual(Vector3 pos, Quaternion rot, string label)
    {
        Handles.color = new Color(0.2f, 1f, 0.2f, 0.3f); Handles.SphereHandleCap(0, pos, rot, 0.04f, EventType.Repaint);
        Handles.color = new Color(0.2f, 1f, 0.2f, 0.8f); Handles.DrawWireDisc(pos, rot * Vector3.up, 0.04f); Handles.DrawWireDisc(pos, rot * Vector3.right, 0.04f); Handles.DrawWireDisc(pos, rot * Vector3.forward, 0.04f);
        Handles.color = new Color(0.2f, 0.6f, 1f, 1f); Handles.ArrowHandleCap(0, pos, rot, 0.15f, EventType.Repaint);
        GUIStyle s = new GUIStyle { normal = { textColor = Color.green }, fontSize = 12, fontStyle = FontStyle.Bold };
        Handles.Label(pos + Vector3.up * 0.06f, label, s);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadOrCreateSharedData()
    {
        EnsureDataFolders();
        string tagsPath = $"{TAGS_PATH}/TagDefinitions.asset";
        _tagDefs = AssetDatabase.LoadAssetAtPath<TagDefinitions>(tagsPath);
        if (_tagDefs == null) { _tagDefs = ScriptableObject.CreateInstance<TagDefinitions>(); AssetDatabase.CreateAsset(_tagDefs, tagsPath); AssetDatabase.SaveAssets(); }
        string regPath = $"{REGISTRY_PATH}/AttachmentRegistry.asset";
        _registry = AssetDatabase.LoadAssetAtPath<AttachmentRegistry>(regPath);
        if (_registry == null) { _registry = ScriptableObject.CreateInstance<AttachmentRegistry>(); AssetDatabase.CreateAsset(_registry, regPath); AssetDatabase.SaveAssets(); }
    }

    private void RefreshRegistry()
    {
        if (_registry == null) return;
        _registry.allAttachments = attachDataMap.Values.Where(d => d != null).ToList();
        EditorUtility.SetDirty(_registry); AssetDatabase.SaveAssets();
    }

    private static void EnsureDataFolders()
    {
        string[] folders = { "Assets/PCG Weapon Workbench", "Assets/PCG Weapon Workbench/Data", GUNBODY_PATH, ATTACHMENT_PATH, REGISTRY_PATH, TAGS_PATH };
        foreach (var folder in folders) { if (!AssetDatabase.IsValidFolder(folder)) { string parent = Path.GetDirectoryName(folder).Replace('\\', '/'); string child = Path.GetFileName(folder); AssetDatabase.CreateFolder(parent, child); } }
    }

    public bool RemoveEntryByPrefabGuid(string prefabGuid)
    {
        GameObject match = null;
        for (int tab = 0; tab < assetLibrary.Count; tab++) { foreach (var go in assetLibrary[tab]) { if (go == null) continue; if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(go)) == prefabGuid) { match = go; break; } } if (match != null) break; }
        if (match == null) return false;
        for (int i = 0; i < assetLibrary.Count; i++) assetLibrary[i].Remove(match);
        calibrationStatus.Remove(match); bodyDataMap.Remove(match); attachDataMap.Remove(match);
        if (_selectedPrefab == match) _selectedPrefab = null;
        SaveLibrary(); RefreshRegistry(); return true;
    }
}

// ── Add Tab popup window ──────────────────────────────────────────────────────

public class AddTabWindow : EditorWindow
{
    private AttachmentType[] _options;
    private int _selectedIdx = 0;
    private System.Action<AttachmentType> _onConfirm;

    public static void Show(AttachmentType[] options, System.Action<AttachmentType> onConfirm)
    {
        var win = GetWindow<AddTabWindow>(true, "Add New Tab", true);
        win._options = options; win._onConfirm = onConfirm; win._selectedIdx = 0;
        win.minSize = win.maxSize = new Vector2(280, 110);
        win.ShowUtility();
    }

    private void OnGUI()
    {
        GUILayout.Space(12);
        GUILayout.Label("Choose attachment type for new tab:", EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(6);
        _selectedIdx = EditorGUILayout.Popup(_selectedIdx, _options.Select(o => o.ToString()).ToArray());
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Tab", GUILayout.Height(28))) { _onConfirm?.Invoke(_options[_selectedIdx]); Close(); }
        if (GUILayout.Button("Cancel", GUILayout.Height(28))) Close();
        GUILayout.EndHorizontal();
    }
}