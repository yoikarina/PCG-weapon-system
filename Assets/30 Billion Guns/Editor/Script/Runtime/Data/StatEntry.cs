// Defines a single named stat value used by the dynamic stat system.

using System;
using UnityEngine;

namespace GunAssemblyTool
{
    // A key-value pair representing one stat on a gun body or attachment.
    // Stored in List<StatEntry> on GunBodyData and AttachmentData.
    [Serializable]
    public class StatEntry
    {
        // Stat name — either a preset key (see StatKeys) or a user-defined string.
        public string key;

        // The numeric value of this stat.
        public float value;

        public StatEntry() { }
        public StatEntry(string key, float value) { this.key = key; this.value = value; }
    }

    // Preset stat key constants shared across the entire system.
    // WeaponWindowTool uses these for the dropdown; ComputeStats uses them to
    // identify specific stats (e.g. MagSize overrides instead of adding).
    public static class StatKeys
    {
        public const string Damage      = "Damage";
        public const string FireRange   = "FireRange";
        public const string ReloadTime  = "ReloadTime";
        public const string MagSize     = "MagSize";
        public const string FireRate    = "FireRate";
        public const string Accuracy    = "Accuracy";

        // All preset keys shown in the "Add Stat" dropdown.
        public static readonly string[] Presets =
        {
            Damage, FireRange, ReloadTime, MagSize, FireRate, Accuracy
        };
    }
}
