// A single named stat entry. Supports Float, Int and Bool value types.
// MagSize overrides the body base value; all other numeric stats are additive.
// Bool stats are OR'd together (if any attachment sets it true, result is true).

using System;
using UnityEngine;

namespace GunAssemblyTool
{
    public enum StatValueType { Float, Int, Bool }

    [Serializable]
    public class StatEntry
    {
        public string key;
        public StatValueType valueType = StatValueType.Float;

        // Only one of these is used depending on valueType.
        public float floatValue;
        public int intValue;
        public bool boolValue;

        public StatEntry() { }

        // Float constructor (backwards compatible)
        public StatEntry(string key, float value)
        {
            this.key = key;
            this.valueType = StatValueType.Float;
            this.floatValue = value;
        }

        public StatEntry(string key, int value)
        {
            this.key = key;
            this.valueType = StatValueType.Int;
            this.intValue = value;
        }

        public StatEntry(string key, bool value)
        {
            this.key = key;
            this.valueType = StatValueType.Bool;
            this.boolValue = value;
        }

        // Returns the value as float regardless of type (Bool: 1/0, Int: cast).
        // Useful for additive computation in CompatibilityResolver.
        public float AsFloat()
        {
            switch (valueType)
            {
                case StatValueType.Int: return intValue;
                case StatValueType.Bool: return boolValue ? 1f : 0f;
                default: return floatValue;
            }
        }

        public override string ToString()
        {
            switch (valueType)
            {
                case StatValueType.Int: return $"{key}: {intValue}";
                case StatValueType.Bool: return $"{key}: {boolValue}";
                default: return $"{key}: {floatValue:F2}";
            }
        }
    }

    public static class StatKeys
    {
        public const string Damage = "Damage";
        public const string FireRate = "FireRate";
        public const string FireRange = "FireRange";
        public const string ReloadTime = "ReloadTime";
        public const string MagSize = "MagSize";
        public const string Accuracy = "Accuracy";
        public const string Weight = "Weight";

        public static readonly string[] Presets =
        {
            Damage, FireRate, FireRange, ReloadTime, MagSize, Accuracy, Weight
        };
    }
}