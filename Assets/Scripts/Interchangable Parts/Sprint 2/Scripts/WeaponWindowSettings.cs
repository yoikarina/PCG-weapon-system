using UnityEngine;
using UnityEditor;

public class WeaponWindowSettings : EditorWindow
{
    // Public static variables for direct access from the main workbench window
    public static GameObject dummyBody, dummyMuzzle, dummyScope, dummyStock, dummyMag;

    // Creates a dedicated settings entry in the Unity menu bar
    [MenuItem("Tools/Weapon Workbench/ Global Dummy Settings", priority = 2)]
    public static void ShowWindow()
    {
        GetWindow<WeaponWindowSettings>("Global Settings").minSize = new Vector2(300, 220);
    }

    private void OnEnable()
    {
        // Automatically load saved settings when the window opens
        LoadSettings();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Global Base Dummy Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Configure this once. Settings are automatically saved locally and do not need to be reconfigured.", MessageType.Info);
        GUILayout.Space(10);

        // Begin listening for any changes in the inspector fields
        EditorGUI.BeginChangeCheck();

        dummyBody = (GameObject)EditorGUILayout.ObjectField("Standard Receiver", dummyBody, typeof(GameObject), false);
        dummyMuzzle = (GameObject)EditorGUILayout.ObjectField("Standard Muzzle", dummyMuzzle, typeof(GameObject), false);
        dummyScope = (GameObject)EditorGUILayout.ObjectField("Standard Optic", dummyScope, typeof(GameObject), false);
        dummyStock = (GameObject)EditorGUILayout.ObjectField("Standard Stock", dummyStock, typeof(GameObject), false);
        dummyMag = (GameObject)EditorGUILayout.ObjectField("Standard Magazine", dummyMag, typeof(GameObject), false);

        // Immediately save if the user assigns or removes a prefab
        if (EditorGUI.EndChangeCheck()) {
            SaveSettings();
        }
    }

    // ================= Core Persistence Logic =================
    public static void LoadSettings()
    {
        dummyBody = LoadPrefab("WT_DummyBody");
        dummyMuzzle = LoadPrefab("WT_DummyMuzzle");
        dummyScope = LoadPrefab("WT_DummyScope");
        dummyStock = LoadPrefab("WT_DummyStock");
        dummyMag = LoadPrefab("WT_DummyMag");
    }

    private static void SaveSettings()
    {
        SavePrefab("WT_DummyBody", dummyBody);
        SavePrefab("WT_DummyMuzzle", dummyMuzzle);
        SavePrefab("WT_DummyScope", dummyScope);
        SavePrefab("WT_DummyStock", dummyStock);
        SavePrefab("WT_DummyMag", dummyMag);
    }

    private static void SavePrefab(string key, GameObject obj)
    {
        if (obj == null) EditorPrefs.DeleteKey(key);
        // Save the unique identifier (GUID) of the Prefab to ensure reliable loading across sessions
        else EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));
    }

    private static GameObject LoadPrefab(string key)
    {
        string guid = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(guid)) return null;
        return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
    }
}