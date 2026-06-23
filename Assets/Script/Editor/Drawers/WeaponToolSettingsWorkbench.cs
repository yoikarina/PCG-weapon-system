using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class WeaponToolSettingsWorkbench : EditorWindow
{
    // ── Default slot dummies ──────────────────────────────────────────────────
    public static GameObject dummyBody, dummyMuzzle, dummyScope, dummyStock, dummyMag;

    // ── Custom tab dummies ────────────────────────────────────────────────────
    private static Dictionary<string, GameObject> _customDummies
        = new Dictionary<string, GameObject>();

    // ── Tag settings ──────────────────────────────────────────────────────────
    public static bool autoAssignTags = true;
    private const string PREF_AUTO_ASSIGN = "WT_AutoAssignTags";
    private const string PREF_CUSTOM_DUMMY = "WT_CustomDummy_";

    // ── Prefab component injection ────────────────────────────────────────────
    public static bool addRigidbody = true;
    public static bool addTriggerCollider = true;
    public static bool addSolidCollider = true;
    public static List<MonoScript> scriptsToAdd = new List<MonoScript>();

    private const string PREF_ADD_RB = "WT_AddRigidbody";
    private const string PREF_ADD_TRIGGER_COL = "WT_AddTriggerCollider";
    private const string PREF_ADD_SOLID_COL = "WT_AddSolidCollider";
    private const string PREF_SCRIPTS = "WT_Scripts";

    // ── UI state ──────────────────────────────────────────────────────────────
    public static string highlightSocket = null;
    private Vector2 _scroll;
    private bool _componentsFoldout = true;
    private bool _scriptsFoldout = true;

    // ── Theme ─────────────────────────────────────────────────────────────────
    private static readonly Color C_HIGHLIGHT = new Color(1f, 0.85f, 0.2f, 0.25f);
    private static readonly Color C_ACCENT = new Color(0.44f, 0.75f, 0.75f, 1f);
    private static readonly Color C_DANGER = new Color(0.75f, 0.22f, 0.17f, 1f);

    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Weapon Workbench/⚙️ Global Dummy Settings", priority = 2)]
    public static void ShowWindow()
    {
        GetWindow<WeaponToolSettingsWorkbench>("Global Settings").minSize = new Vector2(340, 300);
    }

    public static void ShowForNewTab(string socketName)
    {
        highlightSocket = socketName;
        var win = GetWindow<WeaponToolSettingsWorkbench>("Global Settings");
        win.minSize = new Vector2(340, 300);
        win.Focus();
    }

    private void OnEnable() => LoadSettings();

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Space(10);
        GUILayout.Label("Global Base Dummy Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Configure this once. Settings are saved locally and do not " +
            "need to be reconfigured between sessions.",
            MessageType.Info);

        GUILayout.Space(10);

        // ── Default slot dummies ──────────────────────────────────────────────
        GUILayout.Label("Default Slots", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        dummyBody = (GameObject)EditorGUILayout.ObjectField("Standard Receiver", dummyBody, typeof(GameObject), false);
        dummyMuzzle = (GameObject)EditorGUILayout.ObjectField("Standard Muzzle", dummyMuzzle, typeof(GameObject), false);
        dummyScope = (GameObject)EditorGUILayout.ObjectField("Standard Optic", dummyScope, typeof(GameObject), false);
        dummyStock = (GameObject)EditorGUILayout.ObjectField("Standard Stock", dummyStock, typeof(GameObject), false);
        dummyMag = (GameObject)EditorGUILayout.ObjectField("Standard Magazine", dummyMag, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck()) SaveSettings();

        // ── Custom tab dummies ────────────────────────────────────────────────
        if (_customDummies.Count > 0)
        {
            GUILayout.Space(12);
            GUILayout.Label("Custom Tab Sockets", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Each custom tab needs a dummy prefab that marks where that " +
                "socket sits during receiver calibration. Any simple prefab works " +
                "(e.g. a small sphere or the Standard Muzzle prefab).",
                MessageType.None);

            GUILayout.Space(4);

            var keys = _customDummies.Keys.ToList();
            foreach (string socketName in keys)
            {
                bool isHighlighted = (socketName == highlightSocket);

                Rect rowRect = EditorGUILayout.BeginVertical();
                if (isHighlighted)
                    EditorGUI.DrawRect(rowRect, C_HIGHLIGHT);

                GUILayout.Space(4);

                string label = isHighlighted
                    ? $"★  {socketName}  ← assign a prefab here"
                    : socketName;

                GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
                if (isHighlighted) labelStyle.normal.textColor = new Color(0.7f, 0.5f, 0f);
                GUILayout.Label(label, labelStyle);

                if (isHighlighted)
                {
                    EditorGUILayout.HelpBox(
                        $"You just added a new tab for '{socketName}'.\n" +
                        "Assign a dummy prefab below — it will appear in the " +
                        "scene during Receiver calibration so you can position " +
                        "where this socket sits on the gun.\n\n" +
                        "Tip: you can reuse the Standard Muzzle prefab if you " +
                        "don't have a dedicated dummy yet.",
                        MessageType.Warning);
                }

                EditorGUI.BeginChangeCheck();
                _customDummies[socketName] = (GameObject)EditorGUILayout.ObjectField(
                    "Dummy Prefab", _customDummies[socketName], typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck())
                {
                    SaveSettings();
                    if (_customDummies[socketName] != null && isHighlighted)
                        highlightSocket = null;
                }

                GUILayout.Space(4);
                EditorGUILayout.EndVertical();

                Rect sep = GUILayoutUtility.GetRect(0, 1);
                EditorGUI.DrawRect(sep, new Color(0.44f, 0.75f, 0.75f, 0.3f));
            }
        }

        // ── Tag settings ──────────────────────────────────────────────────────
        GUILayout.Space(12);
        GUILayout.Label("Tag Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        autoAssignTags = EditorGUILayout.ToggleLeft(
            "Auto-assign tags on prefab drop", autoAssignTags);
        if (EditorGUI.EndChangeCheck()) SaveSettings();

        // ── Prefab component injection ────────────────────────────────────────
        GUILayout.Space(12);
        Rect sepRect = GUILayoutUtility.GetRect(0, 1);
        EditorGUI.DrawRect(sepRect, new Color(0.44f, 0.75f, 0.75f, 0.3f));
        GUILayout.Space(6);

        _componentsFoldout = EditorGUILayout.Foldout(
            _componentsFoldout, "Saved Prefab Components", true, EditorStyles.foldoutHeader);

        if (_componentsFoldout)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox(
                "These components are added to every prefab created by Save Full Assembly.",
                MessageType.None);
            GUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            addRigidbody = EditorGUILayout.ToggleLeft("Rigidbody", addRigidbody);
            addTriggerCollider = EditorGUILayout.ToggleLeft("BoxCollider  (trigger — pickup range)", addTriggerCollider);
            addSolidCollider = EditorGUILayout.ToggleLeft("BoxCollider  (solid — physics)", addSolidCollider);
            if (EditorGUI.EndChangeCheck()) SaveSettings();

            GUILayout.Space(8);

            // ── Script list ───────────────────────────────────────────────────
            _scriptsFoldout = EditorGUILayout.Foldout(
                _scriptsFoldout, "Scripts to Attach", true);

            if (_scriptsFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Drag MonoScript assets here from the Project window. " +
                    "Each script's component is added to the prefab when saving.",
                    MessageType.None);
                GUILayout.Space(2);

                EditorGUI.BeginChangeCheck();
                for (int i = 0; i < scriptsToAdd.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        scriptsToAdd[i] = (MonoScript)EditorGUILayout.ObjectField(
                            $"Script {i + 1}", scriptsToAdd[i], typeof(MonoScript), false);

                        GUI.backgroundColor = C_DANGER;
                        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                        {
                            scriptsToAdd.RemoveAt(i);
                            GUI.backgroundColor = Color.white;
                            GUI.changed = true;
                            break;
                        }
                        GUI.backgroundColor = Color.white;
                    }
                }
                if (EditorGUI.EndChangeCheck()) SaveSettings();

                GUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+ Add Script Slot", EditorStyles.miniButton,
                            GUILayout.Width(120)))
                    {
                        scriptsToAdd.Add(null);
                        SaveSettings();
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        GUILayout.Space(10);
        EditorGUILayout.EndScrollView();

        if (highlightSocket != null) Repaint();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public static GameObject GetCustomDummy(string socketName)
    {
        if (_customDummies.TryGetValue(socketName, out var prefab) && prefab != null)
            return prefab;
        return dummyMuzzle;
    }

    public static void RegisterCustomSocket(string socketName)
    {
        if (_customDummies.ContainsKey(socketName)) return;
        _customDummies[socketName] = LoadPrefab(PREF_CUSTOM_DUMMY + socketName);
        SaveSettings();
    }

    public static void UnregisterCustomSocket(string socketName)
    {
        if (!_customDummies.ContainsKey(socketName)) return;
        _customDummies.Remove(socketName);
        EditorPrefs.DeleteKey(PREF_CUSTOM_DUMMY + socketName);
        SaveCustomSocketList();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public static void LoadSettings()
    {
        dummyBody = LoadPrefab("WT_DummyBody");
        dummyMuzzle = LoadPrefab("WT_DummyMuzzle");
        dummyScope = LoadPrefab("WT_DummyScope");
        dummyStock = LoadPrefab("WT_DummyStock");
        dummyMag = LoadPrefab("WT_DummyMag");
        autoAssignTags = EditorPrefs.GetBool(PREF_AUTO_ASSIGN, true);
        addRigidbody = EditorPrefs.GetBool(PREF_ADD_RB, true);
        addTriggerCollider = EditorPrefs.GetBool(PREF_ADD_TRIGGER_COL, true);
        addSolidCollider = EditorPrefs.GetBool(PREF_ADD_SOLID_COL, true);
        LoadScripts();

        _customDummies.Clear();
        string raw = EditorPrefs.GetString("WT_CustomSocketList", "");
        if (!string.IsNullOrEmpty(raw))
            foreach (var sn in raw.Split(','))
            {
                if (string.IsNullOrEmpty(sn)) continue;
                _customDummies[sn] = LoadPrefab(PREF_CUSTOM_DUMMY + sn);
            }
    }

    private static void SaveSettings()
    {
        SavePrefab("WT_DummyBody", dummyBody);
        SavePrefab("WT_DummyMuzzle", dummyMuzzle);
        SavePrefab("WT_DummyScope", dummyScope);
        SavePrefab("WT_DummyStock", dummyStock);
        SavePrefab("WT_DummyMag", dummyMag);
        EditorPrefs.SetBool(PREF_AUTO_ASSIGN, autoAssignTags);
        EditorPrefs.SetBool(PREF_ADD_RB, addRigidbody);
        EditorPrefs.SetBool(PREF_ADD_TRIGGER_COL, addTriggerCollider);
        EditorPrefs.SetBool(PREF_ADD_SOLID_COL, addSolidCollider);
        SaveScripts();
        foreach (var kv in _customDummies)
            SavePrefab(PREF_CUSTOM_DUMMY + kv.Key, kv.Value);
        SaveCustomSocketList();
    }

    private static void LoadScripts()
    {
        scriptsToAdd.Clear();
        string raw = EditorPrefs.GetString(PREF_SCRIPTS, "");
        if (string.IsNullOrEmpty(raw)) return;
        foreach (var guid in raw.Split(';'))
        {
            if (string.IsNullOrEmpty(guid)) continue;
            scriptsToAdd.Add(AssetDatabase.LoadAssetAtPath<MonoScript>(
                AssetDatabase.GUIDToAssetPath(guid)));
        }
    }

    private static void SaveScripts()
    {
        var guids = scriptsToAdd.Select(s =>
        {
            if (s == null) return "";
            string path = AssetDatabase.GetAssetPath(s);
            return string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
        });
        EditorPrefs.SetString(PREF_SCRIPTS, string.Join(";", guids));
    }

    private static void SaveCustomSocketList()
    {
        EditorPrefs.SetString("WT_CustomSocketList",
            string.Join(",", _customDummies.Keys));
    }

    private static void SavePrefab(string key, GameObject obj)
    {
        if (obj == null) EditorPrefs.DeleteKey(key);
        else EditorPrefs.SetString(key,
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));
    }

    private static GameObject LoadPrefab(string key)
    {
        string guid = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(guid)) return null;
        return AssetDatabase.LoadAssetAtPath<GameObject>(
            AssetDatabase.GUIDToAssetPath(guid));
    }
}