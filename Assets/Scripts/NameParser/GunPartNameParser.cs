using UnityEngine;
using System;

[ExecuteAlways]
public class GunPart : MonoBehaviour
{
    [Header("Automation")]
    public bool autoParse = true;

    [Header("Parsed Data")]
    public WeaponFamily weaponFamily = WeaponFamily.Unknown;

    public GunPartType partType = GunPartType.Unknown;

    private void OnValidate()
    {
        if (!autoParse)
            return;

        ParseName();
    }

    public void ParseName()
    {
        weaponFamily = WeaponFamily.Unknown;
        partType = GunPartType.Unknown;

        string[] tokens = gameObject.name.Split('_');

        foreach (string token in tokens)
        {
            if (Enum.TryParse(token, true, out WeaponFamily family))
            {
                weaponFamily = family;
            }

            if (Enum.TryParse(token, true, out GunPartType type))
            {
                partType = type;
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}