// Static utility class — single source of truth for compatibility checks and stat computation.

using System;
using System.Collections.Generic;
using System.Text;

namespace GunAssemblyTool
{
    public static class CompatibilityResolver
    {
        // ── Compatibility ─────────────────────────────────────────────────────

        // Returns true only when all four steps pass.
        // Step 1 — Required tags: the gun body must carry every tag in attachment.requiredTags.
        // Step 2 — Forbidden tags: the gun body must carry none of attachment.forbiddenTags.
        // Step 3 — Slot support: the gun body must expose a slot of attachment.attachType.
        // Step 4 — Physical interface: attachment.threadType must be in slot.allowedThreads
        //          (if the whitelist is non-empty), and attachment.magType must be in
        //          slot.allowedMags (if the whitelist is non-empty).
        // Called by AttachmentRegistry.GetCompatible, GunAssemblyState.Equip, and
        // GunAssemblyState.Validate.
        public static bool IsCompatible(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return false;

            // Step 1: required tags
            foreach (var req in attachment.RequiredTagSet)
                if (!body.HasTag(req)) return false;

            // Step 2: forbidden tags
            foreach (var forb in attachment.ForbiddenTagSet)
                if (body.HasTag(forb)) return false;

            // Step 3: slot support
            var slot = body.GetSlot(attachment.attachType);
            if (slot == null) return false;

            // Step 4: physical interface
            if (slot.allowedThreads.Count > 0 &&
                !slot.allowedThreads.Contains(attachment.threadType))
                return false;

            if (slot.allowedMags.Count > 0 &&
                !slot.allowedMags.Contains(attachment.magType))
                return false;

            return true;
        }

        // Returns a human-readable description of why the attachment is incompatible
        // with the given gun body. Returns an empty string when they are compatible.
        // Used for debug logging and future UI tooltip displays.
        public static string GetIncompatibleReason(GunBodyData body, AttachmentData attachment)
        {
            if (body == null || attachment == null) return "Missing data";

            var sb = new StringBuilder();

            // Report missing required tags
            var missing = new List<string>();
            foreach (var req in attachment.RequiredTagSet)
                if (!body.HasTag(req)) missing.Add(req);
            if (missing.Count > 0)
                sb.AppendLine($"Missing tags: {string.Join(", ", missing)}");

            // Report hit forbidden tags
            var blocked = new List<string>();
            foreach (var forb in attachment.ForbiddenTagSet)
                if (body.HasTag(forb)) blocked.Add(forb);
            if (blocked.Count > 0)
                sb.AppendLine($"Blocked by tags: {string.Join(", ", blocked)}");

            // Report slot or interface mismatch
            var slot = body.GetSlot(attachment.attachType);
            if (slot == null)
            {
                sb.AppendLine($"Slot not supported: {attachment.attachType}");
            }
            else
            {
                if (slot.allowedThreads.Count > 0 &&
                    !slot.allowedThreads.Contains(attachment.threadType))
                    sb.AppendLine(
                        $"Thread mismatch: attachment={attachment.threadType}, " +
                        $"allowed=[{string.Join(", ", slot.allowedThreads)}]");

                if (slot.allowedMags.Count > 0 &&
                    !slot.allowedMags.Contains(attachment.magType))
                    sb.AppendLine(
                        $"Magazine format mismatch: attachment={attachment.magType}, " +
                        $"allowed=[{string.Join(", ", slot.allowedMags)}]");
            }

            return sb.ToString().TrimEnd();
        }

        // ── Stats ─────────────────────────────────────────────────────────────

        // Stacks all equipped attachment bonuses on top of the gun body's base stats
        // and returns the final GunStats struct.
        // MagazineData.magSize overrides ammoCapacity entirely rather than adding to it.
        // All stat values are clamped to valid ranges before returning.
        public static GunStats ComputeStats(GunBodyData body, IEnumerable<AttachmentData> equipped)
        {
            if (body == null) return default;

            var stats = new GunStats
            {
                damage       = body.baseDamage,
                fireRate     = body.baseFireRate,
                accuracy     = body.baseAccuracy,
                reloadTime   = body.baseReloadTime,
                ammoCapacity = body.baseAmmoCapacity
            };

            foreach (var a in equipped)
            {
                if (a == null) continue;
                stats.damage     += a.damageBonus;
                stats.fireRate   += a.fireRateBonus;
                stats.accuracy   += a.accuracyBonus;
                stats.reloadTime += a.reloadTimeBonus;

                // Magazine overrides ammo capacity — does not stack additively.
                if (a is MagazineData mag)
                    stats.ammoCapacity = mag.magSize;
            }

            // Clamp to safe ranges
            stats.damage       = Math.Max(0f,   stats.damage);
            stats.fireRate     = Math.Max(1f,   stats.fireRate);
            stats.accuracy     = Math.Min(1f,   Math.Max(0f, stats.accuracy));
            stats.reloadTime   = Math.Max(0.1f, stats.reloadTime);
            stats.ammoCapacity = Math.Max(1,    stats.ammoCapacity);

            return stats;
        }

        // Sums the hpBonus field across all equipped BarrelData attachments.
        // Called by GunAssemblyState.ComputeBarrelHpBonus, which is in turn
        // invoked by GunAssemblyController whenever the gun configuration changes.
        // The result is broadcast to Player.BuffAttributes via onHpBonusChanged.
        public static int ComputeBarrelHpBonus(IEnumerable<AttachmentData> equipped)
        {
            int total = 0;
            foreach (var a in equipped)
                if (a is BarrelData barrel) total += barrel.hpBonus;
            return total;
        }
    }

    // ── GunStats ──────────────────────────────────────────────────────────────

    // Immutable value struct representing the final computed stats for one gun
    // configuration (body + all equipped attachments).
    [Serializable]
    public struct GunStats
    {
        public float damage;
        public float fireRate;      // Rounds per minute
        public float accuracy;      // 0 to 1
        public float reloadTime;    // Seconds
        public int   ammoCapacity;  // Rounds per magazine

        public override string ToString() =>
            $"Damage:{damage:F1} | FireRate:{fireRate:F0}RPM | " +
            $"Accuracy:{accuracy:P0} | Reload:{reloadTime:F2}s | Ammo:{ammoCapacity}";
    }
}
