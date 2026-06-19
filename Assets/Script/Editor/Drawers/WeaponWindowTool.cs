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
    private static readonly Color C_ACCENT = new Color(0.44f, 0.75f, 0.75f, 1f); // #6FBFBF ice teal
    private static readonly Color C_ACCENT_DIM = new Color(0.18f, 0.42f, 0.42f, 1f); // #2E6B6B dark teal
    private static readonly Color C_BG_BLOCK = new Color(0.09f, 0.13f, 0.13f, 1f); // #172020 dark teal-black
    private static readonly Color C_BORDER = new Color(0.44f, 0.75f, 0.75f, 0.40f); // teal border
    private static readonly Color C_TEXT_MAIN = new Color(0.88f, 0.96f, 0.96f, 1f); // #E0F5F5 pale teal-white
    private static readonly Color C_TEXT_DIM = new Color(0.38f, 0.58f, 0.58f, 1f); // #619494 muted teal
    private static readonly Color C_DANGER = new Color(0.75f, 0.22f, 0.17f, 1f); // #C0392B red
    private static readonly Color C_SUCCESS = new Color(0.27f, 0.60f, 0.32f, 1f); // #45993A green — confirm actions

    // ── Original UI state (unchanged from WeaponAlignmentTool) ────────────────
    private int selectedTab = 0;
    private string[] tabNames = { "Receiver", "Muzzle", "Optic", "Stock", "Magazine" };

    // Key: original prefab  Value: auto-generated ScriptableObject
    private List<GameObject>[] assetLibrary = new List<GameObject>[5];
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

    // ── Shared data ───────────────────────────────────────────────────────────
    private TagDefinitions _tagDefs;
    private AttachmentRegistry _registry;

    // ── Keyword whitelist per tab ─────────────────────────────────────────────
    private static readonly string[][] TabKeywords = new string[][]
    {
        null,
        new[]{ "muzzle","suppressor","silencer","brake","compensator" },
        new[]{ "scope","optic","sight","eotech","holographic","red dot","acog","reflex" },
        new[]{ "stock","butt","buffer" },
        new[]{ "mag","magazine","drum","clip","ammo" }
    };

    private bool MatchesTabKeywords(string prefabName, int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= TabKeywords.Length) return true;
        var keywords = TabKeywords[tabIndex];
        if (keywords == null) return true;
        string lower = prefabName.ToLowerInvariant();
        return keywords.Any(k => lower.Contains(k));
    }

    // ── Tag auto-assignment ───────────────────────────────────────────────────
    private bool _autoAssignTags = true;
    private const string PREF_AUTO_ASSIGN_TAGS = "WT_AutoAssignTags";

    // ── Stat list UI state ────────────────────────────────────────────────────
    private string _customStatKey = "";
    private GunAssemblyTool.StatValueType _customStatType = GunAssemblyTool.StatValueType.Float;
    private int _presetStatIdx = 0;
    private bool _statFoldout = true;
    private bool _manageCustomKeys = false;
    private bool _showBodyMedia = false;
    private bool _showAttMedia = false;
    private bool _statSaved = true;

    // Persistent dropdown indices for tag editors.
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
        _autoAssignTags = EditorPrefs.GetBool(PREF_AUTO_ASSIGN_TAGS, true); //TEMP implementation
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
        WB_LabelDim("○  Uncalibrated  →  double-click to calibrate");
        WB_LabelDim("●  Calibrated    →  double-click to assemble");
        GUILayout.Space(4);
        WB_LabelDim("Right-click any asset to view its data.");
        WB_EndBlock();

        GUILayout.Space(10);
        DrawSectionHeader("Auto Tag Assignment");
        WB_BeginBlock();

        bool newVal = EditorGUILayout.ToggleLeft("  Auto-assign tags on drop", _autoAssignTags);
        if (newVal != _autoAssignTags)
        {
            _autoAssignTags = newVal;
            EditorPrefs.SetBool(PREF_AUTO_ASSIGN_TAGS, _autoAssignTags);
        }

        GUILayout.Space(4);
        WB_LabelDim(
            "When enabled, prefab names are split on underscores and each token " +
            "is checked against the Tag Definitions list. Any match is automatically " +
            "assigned on drop — no manual tagging needed.\n\n" +
            "Naming convention:  Type_Part_Variant  e.g.  Pistol_Muzzle_FlashHider_A\n" +
            "Use  _  as the word divider between tokens.");

        WB_EndBlock();
    }

    private void DrawCalibrationUI()
    {
        // ── Mode title ────────────────────────────────────────────────────────
        WB_ModeTitle("CALIBRATION MODE");

        // ── Info block ────────────────────────────────────────────────────────
        WB_BeginBlock();
        WB_LabelDim($"Target:  {currentTargetObject?.name}");
        WB_LabelDim("Move (W) / Rotate (E) to align with the socket dummies.");
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
        // ── Mode title ────────────────────────────────────────────────────────
        WB_ModeTitle("ASSEMBLY MODE");

        // ── Current loadout ───────────────────────────────────────────────────
        WB_SectionLabel("Current Loadout");
        WB_BeginBlock();
        EditorGUI.BeginDisabledGroup(true);
        string[] labels = { "Receiver", "Muzzle", "Optic", "Stock", "Magazine" };
        GUIStyle lbl = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = C_TEXT_DIM }, fixedWidth = 72 };
        for (int i = 0; i < 5; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(labels[i], lbl);
            EditorGUILayout.ObjectField(equipLoadout[i], typeof(GameObject), false);
            GUILayout.EndHorizontal();
        }
        EditorGUI.EndDisabledGroup();
        WB_EndBlock();

        // ── Combined stats ────────────────────────────────────────────────────
        DrawCombinedStats();

        GUILayout.Space(10);
        WB_ButtonPrimary("Randomize Full Weapon", 34, RandomizeEquipAssembly);
        GUILayout.Space(4);
        WB_ButtonSuccess("Save Full Assembly", 44, () => { SaveEquipAssembly(); GUIUtility.ExitGUI(); });
        GUILayout.Space(4);
        WB_ButtonSecondary("Clear Workbench", 26, () => { ClearWorkbench(); GUIUtility.ExitGUI(); });
    }

    // Realtime combined stats: gun body base + all attachment bonuses.
    // Only shown when at least one stat is defined. Only shows stats that
    // exist on the body or any attachment — total values only, no breakdown.
    private void DrawCombinedStats()
    {
        if (equipLoadout[0] == null) return;
        if (!bodyDataMap.TryGetValue(equipLoadout[0], out var bodyData)) return;

        var equippedData = new List<AttachmentData>();
        for (int i = 1; i < equipLoadout.Length; i++)
            if (equipLoadout[i] != null && attachDataMap.TryGetValue(equipLoadout[i], out var ad))
                equippedData.Add(ad);

        // Only show when at least one stat is defined anywhere in the loadout
        if (bodyData.stats.Count == 0 && equippedData.All(a => a.stats.Count == 0)) return;

        var stats = CompatibilityResolver.ComputeStats(bodyData, equippedData);

        GUILayout.Space(6);
        WB_SectionLabel("Stats");
        WB_BeginBlock();

        // Show only stats that are defined by the body or any attachment
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

        if (stats.customFloat != null)
            foreach (var kv in stats.customFloat)
                Row(kv.Key, kv.Value.ToString("F2"));

        if (stats.customInt != null)
            foreach (var kv in stats.customInt)
                Row(kv.Key, kv.Value.ToString());

        if (stats.customBool != null)
            foreach (var kv in stats.customBool)
                Row(kv.Key, kv.Value ? "True" : "False");

        WB_EndBlock();
    }

    private void DrawViewDataUI()
    {
        if (_selectedPrefab == null)
        {
            // Must end the scroll view that DrawLeftWorkbench opened before
            // switching mode, otherwise layout Begin/End counts will mismatch.
            GUILayout.EndScrollView();
            currentMode = WorkbenchMode.Idle;
            GUILayout.EndVertical(); // close DrawLeftWorkbench's BeginVertical
            GUIUtility.ExitGUI();
            return;
        }

        // ── Mode title ────────────────────────────────────────────────────────
        WB_ModeTitle("DATA VIEW");

        // ── Asset name ────────────────────────────────────────────────────────
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

    private void DrawRightPanel(float width)
    {
        DrawRightLibrary(width);
    }

    private void DrawRightLibrary(float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width), GUILayout.ExpandHeight(false));
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
        if (data.tags.Count == 0)
            WB_InfoBox("No tags — default body, accepts any attachment.");
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

            // Active slots: cyber green bg + dark text. Inactive: default.
            if (has) GUI.backgroundColor = C_ACCENT;
            GUIStyle slotStyle = new GUIStyle(EditorStyles.miniButton);
            if (has) slotStyle.normal.textColor = C_BG_BLOCK;
            slotStyle.fontStyle = has ? FontStyle.Bold : FontStyle.Normal;

            bool newVal = GUILayout.Toggle(has, type.ToString(), slotStyle,
                              GUILayout.Width(120), GUILayout.Height(22));
            GUI.backgroundColor = Color.white;

            if (newVal != has)
            {
                if (newVal) data.slots.Add(new SlotData { slotType = type });
                else data.slots.RemoveAll(s => s.slotType == type);
                slotsChanged = true;
            }
            col++;
        }
        GUILayout.EndHorizontal();
        if (slotsChanged) EditorUtility.SetDirty(data);
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
                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        $"{GUNBODY_PATH}/{data.displayName}_Media.asset");
                    AssetDatabase.CreateAsset(data.mediaData, path);
                    AssetDatabase.SaveAssets();
                    EditorUtility.SetDirty(data);
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
        if (data.requiredTags.Count == 0)
            WB_InfoBox("No required tags — matches any gun body.");
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
        if (data is BarrelData bar)
        {
            WB_BeginBlock();
            bar.hpBonus = EditorGUILayout.IntField("HP Bonus (Player)", bar.hpBonus);
            WB_EndBlock();
        }
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(data);

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
                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        $"{ATTACHMENT_PATH}/{data.displayName}_Media.asset");
                    AssetDatabase.CreateAsset(data.mediaOverride, path);
                    AssetDatabase.SaveAssets();
                    EditorUtility.SetDirty(data);
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

    private void DrawSimpleAssetList<T>(string label,
        System.Collections.Generic.List<T> list) where T : UnityEngine.Object
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
        GUIStyle s = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = C_ACCENT },
            margin = new RectOffset(0, 0, 2, 2)
        };
        GUILayout.Label(title, s);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1), C_BORDER);
        GUILayout.Space(2);
    }

    // ── Workbench UI helpers ──────────────────────────────────────────────────

    // Gold mode title bar
    private void WB_ModeTitle(string text)
    {
        Rect r = GUILayoutUtility.GetRect(0, 28);
        EditorGUI.DrawRect(r, C_ACCENT_DIM);
        GUIStyle s = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(r.x + 10, r.y, r.width, r.height), text, s);
        GUILayout.Space(6);
    }

    // Section sub-label (white, smaller)
    private void WB_SectionLabel(string text)
    {
        GUIStyle s = new GUIStyle(EditorStyles.boldLabel)
        { normal = { textColor = C_TEXT_MAIN }, fontSize = 11 };
        GUILayout.Label(text, s);
    }

    // Dim label (grey)
    private void WB_LabelDim(string text)
    {
        GUIStyle s = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = C_TEXT_DIM }, wordWrap = true };
        GUILayout.Label(text, s);
    }

    // Info hint box (no icon, thin border)
    private void WB_InfoBox(string text)
    {
        GUILayout.Space(2);
        GUIStyle s = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            normal = { textColor = C_TEXT_DIM },
            padding = new RectOffset(6, 6, 4, 4)
        };
        float h = s.CalcHeight(new GUIContent(text), EditorGUIUtility.currentViewWidth - 20) + 8;
        Rect r = GUILayoutUtility.GetRect(0, h);
        EditorGUI.DrawRect(r, C_BG_BLOCK);
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), C_BORDER);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), C_BORDER);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), C_BORDER);
        EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), C_BORDER);
        GUI.Label(r, text, s);
        GUILayout.Space(2);
    }

    // Asset title header: name (white large) + type badge (gold small)
    private void WB_AssetTitle(string name, string badge)
    {
        Rect r = GUILayoutUtility.GetRect(0, 38);
        EditorGUI.DrawRect(r, C_BG_BLOCK);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), C_BORDER);

        GUIStyle ts = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = C_TEXT_MAIN },
            alignment = TextAnchor.MiddleLeft
        };
        GUIStyle bs = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = C_ACCENT }, alignment = TextAnchor.MiddleRight };

        GUI.Label(new Rect(r.x + 8, r.y, r.width * 0.7f, r.height), name, ts);
        GUI.Label(new Rect(r.x, r.y, r.width - 8, r.height), badge, bs);
        GUILayout.Space(2);
    }

    // Thin-border content block
    private void WB_BeginBlock()
    {
        GUILayout.Space(2);
        EditorGUILayout.BeginVertical();
        GUILayout.Space(4);
    }

    private void WB_EndBlock()
    {
        GUILayout.Space(4);
        EditorGUILayout.EndVertical();
        Rect r = GUILayoutUtility.GetLastRect();
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), C_BORDER);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax, r.width, 1), C_BORDER);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height + 1), C_BORDER);
        EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height + 1), C_BORDER);
        GUILayout.Space(2);
    }

    // Gold primary button
    private void WB_ButtonPrimary(string label, float height, System.Action onClick)
    {
        GUI.backgroundColor = C_ACCENT_DIM;
        GUIStyle s = new GUIStyle(GUI.skin.button)
        { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        if (GUILayout.Button(label, s, GUILayout.Height(height))) onClick?.Invoke();
        GUI.backgroundColor = Color.white;
    }

    // Green success button (Complete / Save)
    private void WB_ButtonSuccess(string label, float height, System.Action onClick)
    {
        GUI.backgroundColor = C_SUCCESS;
        GUIStyle s = new GUIStyle(GUI.skin.button)
        { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        if (GUILayout.Button(label, s, GUILayout.Height(height))) onClick?.Invoke();
        GUI.backgroundColor = Color.white;
    }

    // Dark secondary button
    private void WB_ButtonSecondary(string label, float height, System.Action onClick)
    {
        GUI.backgroundColor = new Color(0.28f, 0.28f, 0.28f);
        if (GUILayout.Button(label, GUILayout.Height(height))) onClick?.Invoke();
        GUI.backgroundColor = Color.white;
    }

    // Small accent button (inline, e.g. + Add Media)
    private void WB_ButtonAccentSmall(string label, System.Action onClick)
    {
        GUI.backgroundColor = C_ACCENT_DIM;
        GUIStyle s = new GUIStyle(EditorStyles.miniButton)
        { normal = { textColor = Color.white }, fontStyle = FontStyle.Bold };
        if (GUILayout.Button(label, s, GUILayout.Height(22))) onClick?.Invoke();
        GUI.backgroundColor = Color.white;
    }

    // Red danger button (- Remove)
    private void WB_ButtonDanger(string label, System.Action onClick)
    {
        GUI.backgroundColor = C_DANGER;
        GUIStyle s = new GUIStyle(EditorStyles.miniButton)
        { normal = { textColor = new Color(1f, 0.7f, 0.7f) } };
        if (GUILayout.Button(label, s, GUILayout.Height(22))) onClick?.Invoke();
        GUI.backgroundColor = Color.white;
    }

    private Color SlotColor(AttachmentType type)
    {
        switch (type)
        {
            case AttachmentType.Barrel: return new Color(1f, 0.6f, 0.2f);
            case AttachmentType.Magazine: return new Color(0.3f, 0.8f, 0.4f);
            case AttachmentType.Muzzle: return new Color(0.4f, 0.7f, 1f);
            case AttachmentType.Scope: return new Color(0.8f, 0.4f, 1f);
            case AttachmentType.Stock: return new Color(1f, 0.8f, 0.2f);
            case AttachmentType.Grip: return new Color(1f, 0.4f, 0.4f);
            case AttachmentType.Underbarrel: return new Color(0.4f, 1f, 0.8f);
            default: return new Color(0.7f, 0.7f, 0.7f);
        }
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

    private const string CUSTOM_STATS_PREF = "WT_CustomStatKeys";

    private List<string> LoadCustomStatKeys()
    {
        string raw = EditorPrefs.GetString(CUSTOM_STATS_PREF, "");
        if (string.IsNullOrEmpty(raw)) return new List<string>();
        return new List<string>(raw.Split(','));
    }

    private void SaveCustomStatKey(string key)
    {
        var keys = LoadCustomStatKeys();
        if (!keys.Contains(key))
        {
            keys.Add(key);
            EditorPrefs.SetString(CUSTOM_STATS_PREF, string.Join(",", keys));
        }
    }

    private void RemoveCustomStatKey(string key)
    {
        var keys = LoadCustomStatKeys();
        if (keys.Remove(key))
            EditorPrefs.SetString(CUSTOM_STATS_PREF, string.Join(",", keys));
    }

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
                var entry = statList[i];
                GUILayout.BeginHorizontal();

                // Key label — fixed width so columns stay aligned
                GUILayout.Label(entry.key, GUILayout.Width(90));

                // Type selector
                EditorGUI.BeginChangeCheck();
                var newType = (GunAssemblyTool.StatValueType)EditorGUILayout.EnumPopup(
                    entry.valueType, GUILayout.Width(64));
                if (EditorGUI.EndChangeCheck())
                {
                    entry.valueType = newType;
                    EditorUtility.SetDirty(owner);
                    _statSaved = false;
                }

                // Value field — all cases use fixed width so the ✕ button
                // always stays visible regardless of window size.
                EditorGUI.BeginChangeCheck();
                switch (entry.valueType)
                {
                    case GunAssemblyTool.StatValueType.Float:
                        entry.floatValue = EditorGUILayout.FloatField(
                            entry.floatValue, GUILayout.Width(80));
                        break;
                    case GunAssemblyTool.StatValueType.Int:
                        entry.intValue = EditorGUILayout.IntField(
                            entry.intValue, GUILayout.Width(80));
                        break;
                    case GunAssemblyTool.StatValueType.Bool:
                        GUIStyle boolLbl = new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = C_TEXT_DIM } };
                        GUILayout.Space(14);
                        GUILayout.Label(
                            entry.boolValue ? "True" : "False",
                            boolLbl, GUILayout.Width(30));
                        entry.boolValue = EditorGUILayout.Toggle(
                            entry.boolValue, GUILayout.Width(33));
                        break;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(owner);
                    _statSaved = false;
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
                _statSaved = false;
            }
        }

        GUILayout.Space(4);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        var existingKeys = new HashSet<string>(statList.Select(s => s.key));
        var customKeys = LoadCustomStatKeys();
        var allDropdown = GunAssemblyTool.StatKeys.Presets
            .Concat(customKeys)
            .Distinct()
            .Where(k => !existingKeys.Contains(k))
            .ToArray();

        // ── Add from dropdown ────────────────────────────────────────────────
        if (allDropdown.Length > 0)
        {
            GUILayout.BeginHorizontal();
            _presetStatIdx = Mathf.Clamp(_presetStatIdx, 0, allDropdown.Length - 1);
            _presetStatIdx = EditorGUILayout.Popup(_presetStatIdx, allDropdown, GUILayout.Width(110));
            if (GUILayout.Button("+ Add", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                // Use Float as default for preset stats
                statList.Add(new GunAssemblyTool.StatEntry(allDropdown[_presetStatIdx], 0f));
                _presetStatIdx = 0;
                EditorUtility.SetDirty(owner);
                _statSaved = false;
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("All stats added.", EditorStyles.centeredGreyMiniLabel);
        }

        GUILayout.Space(4);

        // ── Add custom key — user picks type BEFORE clicking + Custom ────────
        GUILayout.BeginHorizontal();
        _customStatKey = EditorGUILayout.TextField(_customStatKey, GUILayout.Width(100));
        _customStatType = (GunAssemblyTool.StatValueType)EditorGUILayout.EnumPopup(
            _customStatType, GUILayout.Width(64));
        if (GUILayout.Button("+ Custom", EditorStyles.miniButton, GUILayout.Width(66)) &&
            !string.IsNullOrWhiteSpace(_customStatKey))
        {
            string norm = _customStatKey.Trim();
            if (!existingKeys.Contains(norm))
            {
                // Create entry with the selected type
                var newEntry = new GunAssemblyTool.StatEntry();
                newEntry.key = norm;
                newEntry.valueType = _customStatType;
                statList.Add(newEntry);
                SaveCustomStatKey(norm);
                EditorUtility.SetDirty(owner);
                _statSaved = false;
            }
            _customStatKey = "";
        }
        GUILayout.EndHorizontal();

        // ── Manage saved custom keys ─────────────────────────────────────────
        if (customKeys.Count > 0)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            _manageCustomKeys = EditorGUILayout.Foldout(
                _manageCustomKeys,
                $"Manage custom keys  ({customKeys.Count})",
                true);

            if (_manageCustomKeys)
            {
                string toRemove = null;
                foreach (var ck in customKeys)
                {
                    // Use GUILayout.Space to indent manually — indent level
                    // does not affect horizontal groups reliably.
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(16);
                    GUILayout.Label(ck, EditorStyles.miniLabel, GUILayout.Width(120));
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
                        toRemove = ck;
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
                if (toRemove != null)
                {
                    RemoveCustomStatKey(toRemove);
                    _presetStatIdx = 0;
                    if (statList.RemoveAll(s => s.key == toRemove) > 0)
                        EditorUtility.SetDirty(owner);
                }
            }
        }

        // Save button — only shown when there are stats to save
        if (statList.Count > 0)
        {
            GUILayout.Space(4);
            GUI.backgroundColor = _statSaved
                ? new Color(0.3f, 0.6f, 0.3f)
                : new Color(0.2f, 0.5f, 0.9f);
            if (GUILayout.Button(_statSaved ? "✔ Saved" : "💾 Save Stats", GUILayout.Height(24)))
            {
                EditorUtility.SetDirty(owner);
                AssetDatabase.SaveAssets();
                _statSaved = true;
            }
            GUI.backgroundColor = Color.white;
        }

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
            GUILayout.BeginVertical(GUILayout.Width(itemWidth + 12));
            GUILayout.Space(4);

            Texture2D preview = AssetPreview.GetAssetPreview(obj) ?? AssetPreview.GetMiniThumbnail(obj);
            float padding = 6f;
            Rect cardRect = GUILayoutUtility.GetRect(iconSize + padding * 2, iconSize + padding * 2);
            Rect buttonRect = new Rect(cardRect.x + padding, cardRect.y + padding, iconSize, iconSize);
            bool isHover = buttonRect.Contains(Event.current.mousePosition);
            Event e = Event.current;

            if (isSelected)
            {
                Handles.BeginGUI();
                Handles.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                Handles.DrawAAPolyLine(4f,
                    new Vector3(cardRect.x, cardRect.y),
                    new Vector3(cardRect.xMax, cardRect.y),
                    new Vector3(cardRect.xMax, cardRect.yMax),
                    new Vector3(cardRect.x, cardRect.yMax),
                    new Vector3(cardRect.x, cardRect.y));
                Handles.EndGUI();
            }
            if (isHover && !isSelected)
            {
                Handles.BeginGUI();
                Handles.color = new Color(1f, 1f, 1f, 0.15f);
                Handles.DrawAAPolyLine(2f,
                    new Vector3(buttonRect.x - 2, buttonRect.y - 2),
                    new Vector3(buttonRect.xMax + 2, buttonRect.y - 2),
                    new Vector3(buttonRect.xMax + 2, buttonRect.yMax + 2),
                    new Vector3(buttonRect.x - 2, buttonRect.yMax + 2),
                    new Vector3(buttonRect.x - 2, buttonRect.y - 2));
                Handles.EndGUI();
            }

            // Right-click context menu
            if (e.type == EventType.MouseDown && e.button == 1 && buttonRect.Contains(e.mousePosition))
            {
                GameObject menuObj = obj;
                int menuTab = selectedTab;
                GenericMenu menu = new GenericMenu();
                if (isCalibrated)
                    menu.AddItem(new GUIContent("🔧 Equip Part"), false, () => EnterEquipMode(menuObj, menuTab));
                menu.AddItem(new GUIContent("📋 View Data"), false, () =>
                {
                    _selectedPrefab = menuObj;
                    currentMode = WorkbenchMode.ViewData;
                });
                menu.AddItem(new GUIContent("📐 (Re)Calibrate"), false, () => EnterCalibrationMode(menuObj, menuTab));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("🗑️ Remove"), false, () =>
                {
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

                    // If the removed prefab was being viewed in ViewData mode,
                    // null _selectedPrefab and switch mode BEFORE saving so the
                    // next repaint does not try to draw data for a deleted object.
                    bool wasViewing = (_selectedPrefab == menuObj &&
                                       currentMode == WorkbenchMode.ViewData);
                    if (_selectedPrefab == menuObj)
                    {
                        _selectedPrefab = null;
                        currentMode = WorkbenchMode.Idle;
                    }

                    SaveLibrary();
                    RefreshRegistry();
                    AssetDatabase.Refresh();

                    // ExitGUI stops the current frame immediately so the layout
                    // state from the ScrollView in ViewData mode is not re-entered
                    // in the same frame with a different mode already set.
                    if (wasViewing) GUIUtility.ExitGUI();
                });
                menu.ShowAsContext();
                e.Use();
            }
            // Double-click
            else if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && buttonRect.Contains(e.mousePosition))
            {
                if (isCalibrated) EnterEquipMode(obj, selectedTab);
                else EnterCalibrationMode(obj, selectedTab);
                e.Use();
            }
            // Single-click
            else if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 1 && buttonRect.Contains(e.mousePosition))
            {
                if (currentMode == WorkbenchMode.ViewData)
                    _selectedPrefab = obj;
                else
                    _selectedPrefab = (_selectedPrefab == obj) ? null : obj;
                e.Use();
                Repaint();
            }

            GUI.Box(buttonRect, preview);

            Rect statusRect = new Rect(cardRect.xMax - 24, cardRect.yMax - 24, 20, 20);
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
    // Drag and drop
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
        if (!dropArea.Contains(evt.mousePosition)) return;

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

                if (!MatchesTabKeywords(go.name, selectedTab))
                {
                    rejected = true;
                    continue;
                }

                if (assetLibrary[selectedTab].Contains(go)) continue;

                assetLibrary[selectedTab].Add(go);
                calibrationStatus[go] = CheckIfCalibrated(go, selectedTab);

                if (selectedTab == 0)
                {
                    AutoCreateGunBodyData(go);
                    if (_autoAssignTags && bodyDataMap.TryGetValue(go, out var bodyData))
                        AutoAssignTagsFromName(go, bodyData.tags, bodyData);
                }
                else
                {
                    AutoCreateAttachmentData(go, selectedTab);
                    if (_autoAssignTags && attachDataMap.TryGetValue(go, out var attData))
                        AutoAssignTagsFromName(go, attData.requiredTags, attData);
                }

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
    // Tag auto-assignment
    // ─────────────────────────────────────────────────────────────────────────

    private void AutoAssignTagsFromName(GameObject prefab, List<string> tagList, Object owner)
    {
        if (_tagDefs == null || prefab == null || tagList == null || owner == null) return;

        // Split on underscores, hyphens, spaces, and dots — covers most
        // naming conventions (pistol_silencer_a, pistol-grip.v2, etc.)
        string[] tokens = prefab.name.ToLowerInvariant()
            .Split(new[] { '_', '-', ' ', '.' }, System.StringSplitOptions.RemoveEmptyEntries);

        bool added = false;
        foreach (string token in tokens)
        {
            if (_tagDefs.IsValid(token) && !tagList.Contains(token))
            {
                tagList.Add(token);
                added = true;
            }
        }

        if (added)
        {
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WeaponWorkbench] Auto-assigned tags to '{prefab.name}': " +
                      string.Join(", ", tagList));
        }
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
        Debug.Log($"[WeaponWorkbench] Created GunBodyData -> {assetPath}");
    }

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

                string oldPath = AssetDatabase.GetAssetPath(data);
                string newPath = System.IO.Path.GetDirectoryName(oldPath).Replace('\\', '/') +
                                 "/" + prefabName + "_Body.asset";
                newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
                AssetDatabase.MoveAsset(oldPath, newPath);
                dirty = true;
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
        Debug.Log($"[WeaponWorkbench] Created {type}Data -> {assetPath}");
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
                socket.SetParent(currentTargetObject.transform, true);
            }

            socket.position = kvp.Key.transform.position;
            socket.rotation = kvp.Key.transform.rotation;

            Vector3 parentScale = currentTargetObject.transform.lossyScale;
            socket.localScale = new Vector3(
                1f / parentScale.x,
                1f / parentScale.y,
                1f / parentScale.z);
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
        RefreshEquipAssembly(tabIndex == 0);
    }

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
                        Debug.LogWarning($"[WeaponWorkbench] '{equipLoadout[i].name}' rejected: {reason}");
                        EditorUtility.DisplayDialog(
                            "Incompatible Part",
                            $"'{equipLoadout[i].name}' cannot be attached to '{equipLoadout[0].name}'.\n\n{reason}",
                            "OK");
                    }
                    else
                    {
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
    // Randomize
    // ─────────────────────────────────────────────────────────────────────────

    private void RandomizeEquipAssembly()
    {
        bool hasValidBody = false;

        for (int i = 0; i < 5; i++)
        {
            var validAssets = new List<GameObject>();
            foreach (var go in assetLibrary[i])
            {
                if (go == null) continue;
                if (!calibrationStatus.ContainsKey(go) || !calibrationStatus[go]) continue;

                if (i > 0 && equipLoadout[0] != null)
                {
                    if (bodyDataMap.TryGetValue(equipLoadout[0], out var bd) &&
                        attachDataMap.TryGetValue(go, out var ad))
                        if (!CompatibilityResolver.IsCompatible(bd, ad)) continue;
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

        // INSERT: bake the data contract onto the prefab before saving 
        var runtimeData = equipAssemblyRoot.GetComponent<GunAssemblyTool.GunRuntimeData>();
        if (runtimeData == null)
            runtimeData = equipAssemblyRoot.AddComponent<GunAssemblyTool.GunRuntimeData>();

        bodyDataMap.TryGetValue(equipLoadout[0], out runtimeData.body);   // [0] = receiver/body

        runtimeData.attachments.Clear();
        for (int i = 1; i < equipLoadout.Length; i++)                     // [1..] = attachments
            if (equipLoadout[i] != null &&
                attachDataMap.TryGetValue(equipLoadout[i], out var attachData))
                runtimeData.attachments.Add(attachData);
        // END INSERT 

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

        SaveDataGuidMap();
    }

    private void SaveDataGuidMap()
    {
        foreach (var kv in bodyDataMap)
        {
            if (kv.Key == null || kv.Value == null) continue;
            string dataPath = AssetDatabase.GetAssetPath(kv.Value);
            string dataGuid = AssetDatabase.AssetPathToGUID(dataPath);
            string prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kv.Key));
            if (string.IsNullOrEmpty(dataGuid) || string.IsNullOrEmpty(prefabGuid)) continue;
            EditorPrefs.SetString("WT_DataGuid_" + dataPath, dataGuid);
            EditorPrefs.SetString("WT_DataToPrefab_" + dataGuid, prefabGuid);
        }
        foreach (var kv in attachDataMap)
        {
            if (kv.Key == null || kv.Value == null) continue;
            string dataPath = AssetDatabase.GetAssetPath(kv.Value);
            string dataGuid = AssetDatabase.AssetPathToGUID(dataPath);
            string prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kv.Key));
            if (string.IsNullOrEmpty(dataGuid) || string.IsNullOrEmpty(prefabGuid)) continue;
            EditorPrefs.SetString("WT_DataGuid_" + dataPath, dataGuid);
            EditorPrefs.SetString("WT_DataToPrefab_" + dataGuid, prefabGuid);
        }
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
        string[] folders = { "Assets/PCG Weapon Workbench", "Assets/PCG Weapon Workbench/Data", GUNBODY_PATH, ATTACHMENT_PATH, REGISTRY_PATH, TAGS_PATH };
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

    public bool RemoveEntryByPrefabGuid(string prefabGuid)
    {
        GameObject match = null;

        for (int tab = 0; tab < assetLibrary.Length; tab++)
        {
            foreach (var go in assetLibrary[tab])
            {
                if (go == null) continue;
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(go));
                if (guid == prefabGuid) { match = go; break; }
            }
            if (match != null) break;
        }

        if (match == null) return false;

        for (int i = 0; i < assetLibrary.Length; i++) assetLibrary[i].Remove(match);
        calibrationStatus.Remove(match);
        bodyDataMap.Remove(match);
        attachDataMap.Remove(match);
        if (_selectedPrefab == match) _selectedPrefab = null;
        SaveLibrary();
        RefreshRegistry();
        return true;
    }
}