using System;
using System.Collections.Generic;
using System.Text;

namespace GunAssemblyTool
{
    // Pure logic class:
    // Handles compatibility checks and final stat calculation
    // No Unity dependency → can be tested easily
    public static class CompatibilityResolver
    {
        /// <summary>
        /// Check if an attachment can be used on a gun body
        ///
        /// Rules:
        /// 1. Gun must contain ALL required tags
        /// 2. Gun must NOT contain any forbidden tags
        /// 3. Gun must support this attachment slot type
        /// </summary>
        public static bool IsCompatible(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return false;

            // Check required tags
            foreach (var req in attachment.RequiredTagSet)
                if (!body.HasTag(req)) return false;

            // Check forbidden tags
            foreach (var forb in attachment.ForbiddenTagSet)
                if (body.HasTag(forb)) return false;

            // Check slot support (e.g. gun has a scope slot)
            if (!body.HasSlot(attachment.attachType)) return false;

            return true;
        }

        /// <summary>
        /// Return a human-readable reason why it is NOT compatible
        /// Used for UI tooltip or debugging
        /// Returns empty string if compatible
        /// </summary>
        public static string GetIncompatibleReason(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return "Missing data";

            var sb = new StringBuilder();

            // Missing required tags
            var missing = new List<string>();
            foreach (var req in attachment.RequiredTagSet)
                if (!body.HasTag(req)) missing.Add(req);

            if (missing.Count > 0)
                sb.AppendLine($"Missing tags: {string.Join(", ", missing)}");

            // Has forbidden tags
            var blocked = new List<string>();
            foreach (var forb in attachment.ForbiddenTagSet)
                if (body.HasTag(forb)) blocked.Add(forb);

            if (blocked.Count > 0)
                sb.AppendLine($"Blocked by tags: {string.Join(", ", blocked)}");

            // Slot not supported
            if (!body.HasSlot(attachment.attachType))
                sb.AppendLine($"Slot not supported: {attachment.attachType}");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Combine base gun stats with all equipped attachments
        /// Returns final stats
        /// </summary>
        public static GunStats ComputeStats(GunBodyData body, IEnumerable<AttachmentData> equipped)
        {
            if (body == null) return default;

            // Start from base stats
            var stats = new GunStats
            {
                damage = body.baseDamage,
                fireRate = body.baseFireRate,
                accuracy = body.baseAccuracy,
                reloadTime = body.baseReloadTime
            };

            // Add all attachment bonuses
            foreach (var a in equipped)
            {
                if (a == null) continue;

                stats.damage += a.damageBonus;
                stats.fireRate += a.fireRateBonus;
                stats.accuracy += a.accuracyBonus;
                stats.reloadTime += a.reloadTimeBonus;
            }

            // Clamp values to safe ranges
            stats.damage = Math.Max(0f, stats.damage);
            stats.fireRate = Math.Max(1f, stats.fireRate);
            stats.accuracy = Math.Min(1f, Math.Max(0f, stats.accuracy));
            stats.reloadTime = Math.Max(0.1f, stats.reloadTime);

            return stats;
        }
    }

    [Serializable]
    public struct GunStats
    {
        // Final computed stats
        public float damage;
        public float fireRate;
        public float accuracy;
        public float reloadTime;

        // Convert stats to readable string (for debug/UI)
        public override string ToString() =>
            $"Damage:{damage:F1} | FireRate:{fireRate:F0}RPM | " +
            $"Accuracy:{accuracy:P0} | Reload:{reloadTime:F2}s";
    }
}