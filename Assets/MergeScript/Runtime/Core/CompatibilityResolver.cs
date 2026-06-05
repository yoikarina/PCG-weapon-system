// Static utility class — single source of truth for compatibility checks and stat computation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GunAssemblyTool
{
    public static class CompatibilityResolver
    {
        // Compatibility check — three steps:
        // Step 1 — Required tags (OR): gun body must have AT LEAST ONE required tag.
        //           If requiredTags is empty, any gun body passes this step.
        // Step 2 — Forbidden tags: gun body must have NONE of the forbidden tags.
        // Step 3 — Slot support: gun body must expose a slot of attachment.attachType.
        public static bool IsCompatible(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return false;

            // Step 1: OR logic — compatibility rules:
            //   Both empty  → default on both sides → always compatible
            //   Body empty, attachment has tags → body is default → compatible
            //   Body has tags, attachment empty  → attachment is default → compatible
            //   Both have tags → body must have at least one attachment tag (OR)
            if (attachment.RequiredTagSet.Count > 0 && body.tags.Count > 0)
            {
                bool anyMatch = attachment.RequiredTagSet.Any(req => body.HasTag(req));
                if (!anyMatch) return false;
            }

            // Step 2: body must have none of the forbidden tags
            foreach (var forb in attachment.ForbiddenTagSet)
                if (body.HasTag(forb)) return false;

            // Step 3: slot must be supported
            if (body.GetSlot(attachment.attachType) == null) return false;

            return true;
        }

        // Returns a human-readable reason why the attachment is incompatible.
        // Returns the first reason why IsCompatible returned false.
        // Mirrors IsCompatible step-by-step so the reason matches exactly.
        public static string GetIncompatibleReason(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return "Missing data";

            // Step 1: required tags (OR) — same condition as IsCompatible
            if (attachment.RequiredTagSet.Count > 0 && body.tags.Count > 0)
            {
                bool anyMatch = attachment.RequiredTagSet.Any(req => body.HasTag(req));
                if (!anyMatch)
                    return $"Body has none of the required tags: [{string.Join(", ", attachment.RequiredTagSet)}]";
            }

            // Step 2: forbidden tags
            var blocked = attachment.ForbiddenTagSet.Where(body.HasTag).ToList();
            if (blocked.Count > 0)
                return $"Body has forbidden tag(s): {string.Join(", ", blocked)}";

            // Step 3: slot
            if (body.GetSlot(attachment.attachType) == null)
                return $"Slot not supported on this gun body: {attachment.attachType}";

            return "";
        }

        // Stacks attachment stat bonuses on top of the body's base stats.
        // MagSize overrides instead of adding; all other stats are additive.
        public static GunStats ComputeStats(GunBodyData body, IEnumerable<AttachmentData> equipped)
        {
            if (body == null) return default;

            var stats = new GunStats
            {
                damage = body.GetStat(StatKeys.Damage),
                fireRate = body.GetStat(StatKeys.FireRate),
                accuracy = body.GetStat(StatKeys.Accuracy),
                reloadTime = body.GetStat(StatKeys.ReloadTime),
                fireRange = body.GetStat(StatKeys.FireRange),
                ammoCapacity = (int)body.GetStat(StatKeys.MagSize),
                custom = BuildCustomStats(body.stats)
            };

            foreach (var a in equipped)
            {
                if (a == null) continue;
                foreach (var entry in a.stats)
                {
                    switch (entry.key)
                    {
                        case StatKeys.Damage: stats.damage += entry.value; break;
                        case StatKeys.FireRate: stats.fireRate += entry.value; break;
                        case StatKeys.Accuracy: stats.accuracy += entry.value; break;
                        case StatKeys.ReloadTime: stats.reloadTime += entry.value; break;
                        case StatKeys.FireRange: stats.fireRange += entry.value; break;
                        case StatKeys.MagSize: stats.ammoCapacity = (int)entry.value; break;
                        default:
                            if (!stats.custom.ContainsKey(entry.key)) stats.custom[entry.key] = 0f;
                            stats.custom[entry.key] += entry.value;
                            break;
                    }
                }
            }

            stats.damage = Math.Max(0f, stats.damage);
            stats.fireRate = Math.Max(0f, stats.fireRate);
            stats.accuracy = Math.Min(1f, Math.Max(0f, stats.accuracy));
            stats.reloadTime = Math.Max(0f, stats.reloadTime);
            stats.fireRange = Math.Max(0f, stats.fireRange);
            stats.ammoCapacity = Math.Max(0, stats.ammoCapacity);

            return stats;
        }

        public static int ComputeBarrelHpBonus(IEnumerable<AttachmentData> equipped)
        {
            int total = 0;
            foreach (var a in equipped)
                if (a is BarrelData barrel) total += barrel.hpBonus;
            return total;
        }

        private static Dictionary<string, float> BuildCustomStats(List<StatEntry> entries)
        {
            var result = new Dictionary<string, float>();
            foreach (var e in entries)
            {
                bool isPreset = Array.IndexOf(StatKeys.Presets, e.key) >= 0;
                if (!isPreset) result[e.key] = e.value;
            }
            return result;
        }
    }

    [Serializable]
    public struct GunStats
    {
        public float damage;
        public float fireRate;
        public float accuracy;
        public float reloadTime;
        public float fireRange;
        public int ammoCapacity;
        public Dictionary<string, float> custom;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Damage:{damage:F1} | FireRate:{fireRate:F0} | " +
                      $"Accuracy:{accuracy:P0} | Reload:{reloadTime:F2}s | " +
                      $"Range:{fireRange:F0} | Ammo:{ammoCapacity}");
            if (custom != null)
                foreach (var kv in custom)
                    sb.Append($" | {kv.Key}:{kv.Value:F2}");
            return sb.ToString();
        }
    }
}