// Static utility — single source of truth for compatibility checks and stat computation.

using System;
using System.Collections.Generic;
using System.Linq;

namespace GunAssemblyTool
{
    public static class CompatibilityResolver
    {
        // ── Compatibility ─────────────────────────────────────────────────────

        // Three-step check:
        // 1. Required tags (OR): body must have at least one required tag.
        //    Empty tags on either side = default = always passes.
        // 2. Forbidden tags: body must have none of the forbidden tags.
        // 3. Slot support: body must expose a slot matching attachment.attachType.
        public static bool IsCompatible(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return false;

            // Step 1: OR logic — both empty = default = pass
            if (attachment.RequiredTagSet.Count > 0 && body.tags.Count > 0)
            {
                bool anyMatch = attachment.RequiredTagSet.Any(req => body.HasTag(req));
                if (!anyMatch) return false;
            }

            // Step 2: forbidden tags
            foreach (var forb in attachment.ForbiddenTagSet)
                if (body.HasTag(forb)) return false;

            // Step 3: slot
            if (body.GetSlot(attachment.attachType) == null) return false;

            return true;
        }

        // Returns the first reason why IsCompatible returned false.
        public static string GetIncompatibleReason(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return "Missing data";

            if (attachment.RequiredTagSet.Count > 0 && body.tags.Count > 0)
            {
                bool anyMatch = attachment.RequiredTagSet.Any(req => body.HasTag(req));
                if (!anyMatch)
                    return $"Body has none of the required tags: [{string.Join(", ", attachment.RequiredTagSet)}]";
            }

            var blocked = attachment.ForbiddenTagSet.Where(body.HasTag).ToList();
            if (blocked.Count > 0)
                return $"Body has forbidden tag(s): {string.Join(", ", blocked)}";

            if (body.GetSlot(attachment.attachType) == null)
                return $"Slot not supported on this gun body: {attachment.attachType}";

            return "";
        }

        // ── Stat computation ──────────────────────────────────────────────────

        // Computes the final GunStats for a body + attachment list.
        // MagSize overrides (last equipped wins); all other stats are additive.
        // Weight is additive across body and all attachments.
        public static GunStats ComputeStats(GunBodyData body, IEnumerable<AttachmentData> equipped)
        {
            if (body == null) return new GunStats();

            var stats = new GunStats
            {
                damage = body.GetStat(StatKeys.Damage),
                fireRate = body.GetStat(StatKeys.FireRate),
                accuracy = body.GetStat(StatKeys.Accuracy),
                reloadTime = body.GetStat(StatKeys.ReloadTime),
                fireRange = body.GetStat(StatKeys.FireRange),
                ammoCapacity = (int)body.GetStat(StatKeys.MagSize),
                weight = body.GetStat(StatKeys.Weight),
            };
            // Apply body custom stats (non-additive for body — it sets the base)
            ApplyCustomStats(stats, body.stats, additive: false);

            foreach (var a in equipped)
            {
                if (a == null) continue;
                foreach (var entry in a.stats)
                {
                    switch (entry.key)
                    {
                        case StatKeys.Damage: stats.damage += entry.floatValue; break;
                        case StatKeys.FireRate: stats.fireRate += entry.floatValue; break;
                        case StatKeys.Accuracy: stats.accuracy += entry.floatValue; break;
                        case StatKeys.ReloadTime: stats.reloadTime += entry.floatValue; break;
                        case StatKeys.FireRange: stats.fireRange += entry.floatValue; break;
                        case StatKeys.MagSize: stats.ammoCapacity = (int)entry.floatValue; break;
                        case StatKeys.Weight: stats.weight += entry.floatValue; break;
                        default: break; // handled by ApplyCustomStats below
                    }
                }
                ApplyCustomStats(stats, a.stats, additive: true);
            }

            // Clamp to sane ranges
            stats.damage = Math.Max(0f, stats.damage);
            stats.fireRate = Math.Max(0f, stats.fireRate);
            stats.accuracy = Math.Min(1f, Math.Max(0f, stats.accuracy));
            stats.reloadTime = Math.Max(0f, stats.reloadTime);
            stats.fireRange = Math.Max(0f, stats.fireRange);
            stats.ammoCapacity = Math.Max(0, stats.ammoCapacity);
            stats.weight = Math.Max(0f, stats.weight);

            return stats;
        }

        // Applies custom (non-preset) stats from one entry list into the GunStats dictionaries.
        private static void ApplyCustomStats(GunStats stats, List<StatEntry> entries, bool additive)
        {
            foreach (var e in entries)
            {
                if (Array.IndexOf(StatKeys.Presets, e.key) >= 0) continue; // preset handled above

                switch (e.valueType)
                {
                    case StatValueType.Float:
                        if (!stats.customFloat.ContainsKey(e.key)) stats.customFloat[e.key] = 0f;
                        stats.customFloat[e.key] = additive
                            ? stats.customFloat[e.key] + e.floatValue
                            : e.floatValue;
                        break;

                    case StatValueType.Int:
                        if (!stats.customInt.ContainsKey(e.key)) stats.customInt[e.key] = 0;
                        stats.customInt[e.key] = additive
                            ? stats.customInt[e.key] + e.intValue
                            : e.intValue;
                        break;

                    case StatValueType.Bool:
                        // OR logic: true if body or any attachment sets it true
                        if (!stats.customBool.ContainsKey(e.key)) stats.customBool[e.key] = false;
                        stats.customBool[e.key] = stats.customBool[e.key] || e.boolValue;
                        break;
                }
            }
        }
    }
}