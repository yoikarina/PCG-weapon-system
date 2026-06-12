// Watches for deleted data assets and removes the linked prefab from the Workbench list.

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class WeaponDataWatcher : AssetPostprocessor
{
    // Maps data asset GUID -> prefab asset GUID.
    // Built by WeaponWindowTool.SaveDataGuidMap() every time the library is saved.
    // Since this is a static dictionary it survives domain reloads within the editor session.
    public static Dictionary<string, string> dataGuidToPrefabGuid
        = new Dictionary<string, string>();

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (var deletedPath in deletedAssets)
        {
            if (!deletedPath.EndsWith(".asset")) continue;
            if (!deletedPath.StartsWith("Assets/Data/Gunbody/") &&
                !deletedPath.StartsWith("Assets/Data/Attachment/")) continue;

            // The asset is already gone so AssetPathToGUID may return empty.
            // We stored the GUID in EditorPrefs before deletion via SaveDataGuidMap.
            string dataGuid = EditorPrefs.GetString("WT_DataGuid_" + deletedPath, "");
            if (string.IsNullOrEmpty(dataGuid)) continue;

            string prefabGuid = EditorPrefs.GetString("WT_DataToPrefab_" + dataGuid, "");
            if (string.IsNullOrEmpty(prefabGuid)) continue;

            // Clean up stored prefs
            EditorPrefs.DeleteKey("WT_DataGuid_" + deletedPath);
            EditorPrefs.DeleteKey("WT_DataToPrefab_" + dataGuid);

            // Remove from Workbench
            var window = EditorWindow.GetWindow<WeaponWindowTool>(false, null, false);
            if (window == null) continue;

            if (window.RemoveEntryByPrefabGuid(prefabGuid))
            {
                Debug.Log($"[WeaponDataWatcher] Removed Workbench entry for: {deletedPath}");
                EditorApplication.delayCall += () =>
                    EditorWindow.GetWindow<WeaponWindowTool>(false, null, false)?.Repaint();
            }
        }
    }
}
