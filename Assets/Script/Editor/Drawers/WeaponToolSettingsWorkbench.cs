using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class WeaponToolSettingsWorkbench : EditorWindow
{
    // ── Default slot dummies ──────────────────────────────────────────────────
    public static GameObject dummyBody, dummyMuzzle, dummyScope, dummyStock, dummyMag;

    // ── Custom tab dummies ────────────────────────────────────────────────────
    // Key: socketName (e.g. "Socket_Grip")  Value: dummy prefab
    private static Dictionary<string, GameObject> _customDummies = new Dictionary<string, GameObject>();

    // ── Tag settings ──────────────────────────────────────────────────────────
    public static bool autoAssignTags = true;
    private const string PREF_AUTO_ASSIGN = "WT_AutoAssignTags";
    private const string PREF_CUSTOM_DUMMY = "WT_CustomDummy_";

    // ── UI state ──────────────────────────────────────────────────────────────
    // When set, the settings window scrolls to and highlights that socket entry.
    public static string highlightSocket = null;
    private Vector2 _scroll;

    // ── Theme (matches main workbench) ────────────────────────────────────────
    private static readonly Color C_HIGHLIGHT = new Color(1f, 0.85f, 0.2f, 0.25f);  // amber tint
    private static readonly Color C_ACCENT = new Color(0.44f, 0.75f, 0.75f, 1f);

    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Weapon Workbench/⚙️ Global Dummy Settings", priority = 2)]
    public static void ShowWindow()
    {
        GetWindow<WeaponToolSettingsWorkbench>("Global Settings").minSize = new Vector2(340, 300);
    }

    // Called by WeaponWindowTool after the user adds a new tab, so the window
    // opens focused on the new socket that needs a dummy prefab assigned.
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

                // Draw amber background for the newly added socket
                Rect rowRect = EditorGUILayout.BeginVertical();
                if (isHighlighted)
                    EditorGUI.DrawRect(rowRect, C_HIGHLIGHT);

                GUILayout.Space(4);

                // Label with a ★ marker if this is the newly added one
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
                    // Clear highlight once the user assigns a prefab
                    if (_customDummies[socketName] != null && isHighlighted)
                        highlightSocket = null;
                }

                GUILayout.Space(4);
                EditorGUILayout.EndVertical();

                // Thin separator between entries
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

        GUILayout.Space(10);
        EditorGUILayout.EndScrollView();

        // Keep repainting while a socket is highlighted so the amber tint is visible
        if (highlightSocket != null) Repaint();
    }

    // ── Public API for WeaponWindowTool ───────────────────────────────────────

    /// Returns the dummy prefab for a custom socket name, falling back to
    /// dummyMuzzle if none has been assigned yet.
    public static GameObject GetCustomDummy(string socketName)
    {
        if (_customDummies.TryGetValue(socketName, out var prefab) && prefab != null)
            return prefab;
        return dummyMuzzle; // fallback so calibration still works
    }

    /// Register a new custom socket. Call this when a new tab is added.
    /// If the socket already has a saved dummy it will be restored.
    public static void RegisterCustomSocket(string socketName)
    {
        if (_customDummies.ContainsKey(socketName)) return;
        // Try to restore a previously saved dummy for this socket
        _customDummies[socketName] = LoadPrefab(PREF_CUSTOM_DUMMY + socketName);
        SaveSettings();
    }

    /// Unregister a custom socket when its tab is removed.
    public static void UnregisterCustomSocket(string socketName)
    {
        if (!_customDummies.ContainsKey(socketName)) return;
        _customDummies.Remove(socketName);
        EditorPrefs.DeleteKey(PREF_CUSTOM_DUMMY + socketName);
        // Also remove from the saved list
        SaveCustomSocketList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Persistence
    // ─────────────────────────────────────────────────────────────────────────

    public static void LoadSettings()
    {
        dummyBody = LoadPrefab("WT_DummyBody");
        dummyMuzzle = LoadPrefab("WT_DummyMuzzle");
        dummyScope = LoadPrefab("WT_DummyScope");
        dummyStock = LoadPrefab("WT_DummyStock");
        dummyMag = LoadPrefab("WT_DummyMag");
        autoAssignTags = EditorPrefs.GetBool(PREF_AUTO_ASSIGN, true);

        // Restore custom socket list
        _customDummies.Clear();
        string raw = EditorPrefs.GetString("WT_CustomSocketList", "");
        if (!string.IsNullOrEmpty(raw))
        {
            foreach (var sn in raw.Split(','))
            {
                if (string.IsNullOrEmpty(sn)) continue;
                _customDummies[sn] = LoadPrefab(PREF_CUSTOM_DUMMY + sn);
            }
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

        // Save each custom dummy
        foreach (var kv in _customDummies)
            SavePrefab(PREF_CUSTOM_DUMMY + kv.Key, kv.Value);

        SaveCustomSocketList();
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