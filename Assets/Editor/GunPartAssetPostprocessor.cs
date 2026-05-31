#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System;

public class GunPartAssetPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string assetPath in importedAssets)
        {
            // Only process prefabs
            if (!assetPath.EndsWith(".prefab"))
                continue;

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab == null)
                continue;

            ProcessPrefab(assetPath);
        }
    }

    private static void ProcessPrefab(string assetPath)
    {
        GameObject prefabRoot =
            PrefabUtility.LoadPrefabContents(assetPath);

        GunPart gunPart =
            prefabRoot.GetComponent<GunPart>();

        if (gunPart == null)
        {
            gunPart =
                prefabRoot.AddComponent<GunPart>();
        }

        ParseName(prefabRoot.name, gunPart);

        PrefabUtility.SaveAsPrefabAsset(
            prefabRoot,
            assetPath);

        PrefabUtility.UnloadPrefabContents(
            prefabRoot);

        Debug.Log(
            $"Processed Gun Part: {prefabRoot.name}");
    }

    private static void ParseName(
        string objectName,
        GunPart gunPart)
    {
        gunPart.weaponFamily =
            WeaponFamily.Unknown;

        gunPart.partType =
            GunPartType.Unknown;

        string[] tokens =
            objectName.Split('_');

        foreach (string token in tokens)
        {
            if (Enum.TryParse(
                token,
                true,
                out WeaponFamily family))
            {
                gunPart.weaponFamily = family;
            }

            if (Enum.TryParse(
                token,
                true,
                out GunPartType type))
            {
                gunPart.partType = type;
            }
        }
    }
}

#endif