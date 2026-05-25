// Static utility class — single source of truth for compatibility checks and stat computation.

using System;
using System.Collections.Generic;
using System.Text;

namespace GunAssemblyTool
{
    public static class CompatibilityResolver
    {
        // ── Compatibility ─────────────────────────────────────────────────────

        // Returns true only when all three steps pass.
        // Step 1 — Required tags: body must carry every tag in attachment.requiredTags.
        // Step 2 — Forbidden tags: body must carry none of attachment.forbiddenTags.
        // Step 3 — Slot support: body must expose a slot of attachment.attachType.
        public static bool IsCompatible(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return false;

            foreach (var req in attachment.RequiredTagSet)
                if (!body.HasTag(req)) return false;

            foreach (var forb in attachment.ForbiddenTagSet)
                if (body.HasTag(forb)) return false;

            if (body.GetSlot(attachment.attachType) == null) return false;

            return true;
        }

        // Returns a human-readable reason why the attachment is incompatible.
        // Returns empty string when compatible.
        public static string GetIncompatibleReason(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return "Missing data";

            var sb = new StringBuilder();

            var missing = new List<string>();
            foreach (var req in attachment.RequiredTagSet)
                if (!body.HasTag(req)) missing.Add(req);
            if (missing.Count > 0)
                sb.AppendLine($"Missing tags: {string.Join(", ", missing)}");

            var blocked = new List<string>();
            foreach (var forb in attachment.ForbiddenTagSet)
                if (body.HasTag(forb)) blocked.Add(forb);
            if (blocked.Count > 0)
                sb.AppendLine($"Blocked by tags: {string.Join(", ", blocked)}");

            if (body.GetSlot(attachment.attachType) == null)
                sb.AppendLine($"Slot not supported: {attachment.attachType}");

            return sb.ToString().TrimEnd();
        }

        // ── Stats ─────────────────────────────────────────────────────────────

        // Builds final GunStats from the body's base stat list, then stacks
        // each equipped attachment's stat bonuses on top.
        // MagSize is treated as an override (last equipped magazine wins),
        // all other stats are additive.
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
                        case StatKeys.MagSize:
                            stats.ammoCapacity = (int)entry.value;
                            break;
                        default:
                            if (!stats.custom.ContainsKey(entry.key))
                                stats.custom[entry.key] = 0f;
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

        // Returns the total HP bonus from all equipped BarrelData attachments.
        public static int ComputeBarrelHpBonus(IEnumerable<AttachmentData> equipped)
        {
            int total = 0;
            foreach (var a in equipped)
                if (a is BarrelData barrel) total += barrel.hpBonus;
            return total;
        }

        // Builds a custom stat dictionary from all non-preset entries in a stat list.
        private static Dictionary<string, float> BuildCustomStats(List<StatEntry> entries)
        {
            var result = new Dictionary<string, float>();
            foreach (var e in entries)
            {
                bool isPreset = System.Array.IndexOf(StatKeys.Presets, e.key) >= 0;
                if (!isPreset) result[e.key] = e.value;
            }
            return result;
        }
    }

    // ── GunStats ──────────────────────────────────────────────────────────────

    // Final computed stats for one gun + attachment configuration.
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