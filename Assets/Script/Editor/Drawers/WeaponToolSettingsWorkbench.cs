using UnityEngine;
using UnityEditor;

public class WeaponToolSettingsWorkbench : EditorWindow
{
    // Public static variables for direct access from the main workbench window
    public static GameObject dummyBody, dummyMuzzle, dummyScope, dummyStock, dummyMag;
    public static bool autoAssignTags = true;

    private const string PREF_AUTO_ASSIGN = "WT_AutoAssignTags";

    [MenuItem("Tools/Weapon Workbench/⚙️ Global Dummy Settings", priority = 2)]
    public static void ShowWindow()
    {
        GetWindow<WeaponToolSettingsWorkbench>("Global Settings").minSize = new Vector2(300, 240);
    }

    private void OnEnable() => LoadSettings();

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Global Base Dummy Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Configure this once. Settings are automatically saved locally " +
            "and do not need to be reconfigured.",
            MessageType.Info);
        GUILayout.Space(10);

        EditorGUI.BeginChangeCheck();

        dummyBody = (GameObject)EditorGUILayout.ObjectField("Standard Receiver", dummyBody, typeof(GameObject), false);
        dummyMuzzle = (GameObject)EditorGUILayout.ObjectField("Standard Muzzle", dummyMuzzle, typeof(GameObject), false);
        dummyScope = (GameObject)EditorGUILayout.ObjectField("Standard Optic", dummyScope, typeof(GameObject), false);
        dummyStock = (GameObject)EditorGUILayout.ObjectField("Standard Stock", dummyStock, typeof(GameObject), false);
        dummyMag = (GameObject)EditorGUILayout.ObjectField("Standard Magazine", dummyMag, typeof(GameObject), false);

        GUILayout.Space(10);
        GUILayout.Label("Tag Settings", EditorStyles.boldLabel);
        autoAssignTags = EditorGUILayout.ToggleLeft("Auto-assign tags on prefab drop", autoAssignTags);

        if (EditorGUI.EndChangeCheck())
            SaveSettings();
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
    }

    private static void SaveSettings()
    {
        SavePrefab("WT_DummyBody", dummyBody);
        SavePrefab("WT_DummyMuzzle", dummyMuzzle);
        SavePrefab("WT_DummyScope", dummyScope);
        SavePrefab("WT_DummyStock", dummyStock);
        SavePrefab("WT_DummyMag", dummyMag);
        EditorPrefs.SetBool(PREF_AUTO_ASSIGN, autoAssignTags);
    }

    private static void SavePrefab(string key, GameObject obj)
    {
        if (obj == null) EditorPrefs.DeleteKey(key);
        else EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));
    }

    private static GameObject LoadPrefab(string key)
    {
        string guid = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(guid)) return null;
        return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
    }
}