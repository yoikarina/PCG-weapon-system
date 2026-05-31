#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GunPartAutoSetup
{
    static GunPartAutoSetup()
    {
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private static void OnHierarchyChanged()
    {
        GameObject[] allObjects =
            Object.FindObjectsByType<GameObject>(
                FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            // Skip if already has component
            if (obj.GetComponent<GunPart>() != null)
                continue;

            // Optional:
            // Only process objects following naming convention

            if (LooksLikeGunPart(obj.name))
            {
                Undo.AddComponent<GunPart>(obj);

                Debug.Log(
                    $"Auto-added GunPart to: {obj.name}");
            }
        }
    }

    private static bool LooksLikeGunPart(string objectName)
    {
        string[] tokens = objectName.Split('_');

        foreach (string token in tokens)
        {
            if (System.Enum.TryParse(
                token,
                true,
                out WeaponFamily family))
            {
                return true;
            }
        }

        return false;
    }
}

#endif