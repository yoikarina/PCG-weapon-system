// Holds all computed stats for a fully assembled gun.
// Numeric stats (float/int) are additive across body + attachments.
// Bool stats use OR logic: true if body OR any attachment sets it true.
// MagSize overrides instead of adding.

using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    [System.Serializable]
    public class GunStats
    {
        [Header("Numeric Stats")]
        public float damage;
        public float fireRate;
        public float accuracy;
        public float reloadTime;
        public float fireRange;
        public int ammoCapacity;
        public float weight;

        // Custom float/int stats defined in the Workbench
        public Dictionary<string, float> customFloat = new Dictionary<string, float>();
        public Dictionary<string, int> customInt = new Dictionary<string, int>();

        [Header("Bool Stats")]
        // Custom bool stats defined in the Workbench ¡ª OR logic across all parts
        public Dictionary<string, bool> customBool = new Dictionary<string, bool>();

        // Convenience: get any custom float stat
        public float GetFloat(string key, float defaultValue = 0f) =>
            customFloat.TryGetValue(key, out float v) ? v : defaultValue;

        // Convenience: get any custom int stat
        public int GetInt(string key, int defaultValue = 0) =>
            customInt.TryGetValue(key, out int v) ? v : defaultValue;

        // Convenience: get any custom bool stat
        public bool GetBool(string key, bool defaultValue = false) =>
            customBool.TryGetValue(key, out bool v) ? v : defaultValue;

        public override string ToString() =>
            $"Damage:{damage:F1} | FireRate:{fireRate:F0} | Accuracy:{accuracy:P0} | " +
            $"Reload:{reloadTime:F2}s | Range:{fireRange:F0} | Ammo:{ammoCapacity} | Weight:{weight:F2}kg";
    }
}