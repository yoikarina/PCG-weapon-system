// PCG engine that randomly selects a gun body and fills slots using weighted random selection.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    public class GunGenerator : MonoBehaviour
    {
        [Header("Data Source")]
        public AttachmentRegistry registry;
        public List<GunBodyData>  allBodies = new List<GunBodyData>();

        [Header("Generate Settings")]
        [Tooltip("When true, every available slot is filled. " +
                 "When false, each slot is filled with slotFillChance probability.")]
        public bool fillAllSlots = false;

        [Range(0f, 1f)]
        [Tooltip("Probability that each slot receives an attachment when fillAllSlots is false.")]
        public float slotFillChance = 0.7f;

        // Instance entry point. Delegates to the static Generate method
        // using this component's serialised field values.
        public GunConfiguration Generate() =>
            Generate(registry, allBodies, fillAllSlots, slotFillChance);

        // ── Static API ────────────────────────────────────────────────────────

        // Selects a gun body at random (uniform distribution), then iterates every
        // slot exposed by that body. For each slot, compatible attachments with
        // spawnWeight > 0 are collected and one is chosen via WeightedRandom.
        // Returns a GunConfiguration snapshot, or null if inputs are invalid.
        public static GunConfiguration Generate(
            AttachmentRegistry registry,
            IList<GunBodyData> bodies,
            bool  fillAll    = false,
            float fillChance = 0.7f)
        {
            if (registry == null || bodies == null || bodies.Count == 0)
            {
                Debug.LogWarning("[GunGenerator] Registry or bodies list is missing.");
                return null;
            }

            var body  = bodies[Random.Range(0, bodies.Count)];
            var state = new GunAssemblyState(registry);
            state.SetBody(body);

            foreach (var slotType in body.AvailableSlotTypes)
            {
                // Skip this slot based on the fill probability setting
                if (!fillAll && Random.value > fillChance) continue;

                var candidates = registry.GetCompatible(body, slotType)
                    .Where(a => a.spawnWeight > 0f).ToList();

                if (candidates.Count == 0) continue;
                state.Equip(slotType, WeightedRandom(candidates));
            }

            return state.ToConfiguration();
        }

        // Generates a configuration with specific slots pre-assigned to given attachments.
        // Only gun bodies that are compatible with every forced attachment are considered.
        // Remaining slots are filled randomly (subject to fillAll).
        // Useful for scripted encounters, e.g. "the boss always has a legendary scope".
        public static GunConfiguration GenerateWithConstraints(
            AttachmentRegistry registry,
            IList<GunBodyData> bodies,
            Dictionary<AttachmentType, AttachmentData> forcedSlots,
            bool fillAll = true)
        {
            if (registry == null || bodies == null || bodies.Count == 0) return null;

            // Filter to bodies that are compatible with all forced attachments
            var compatibleBodies = bodies.Where(b =>
                forcedSlots.All(kv =>
                    kv.Value == null || CompatibilityResolver.IsCompatible(b, kv.Value))
            ).ToList();

            if (compatibleBodies.Count == 0)
            {
                Debug.LogWarning("[GunGenerator] No body matches all constraints. Falling back to random.");
                compatibleBodies = bodies.ToList();
            }

            var body  = compatibleBodies[Random.Range(0, compatibleBodies.Count)];
            var state = new GunAssemblyState(registry);
            state.SetBody(body);

            // Apply forced attachments first
            foreach (var kv in forcedSlots)
                if (kv.Value != null) state.Equip(kv.Key, kv.Value);

            // Fill remaining slots randomly
            foreach (var slotType in body.AvailableSlotTypes)
            {
                if (forcedSlots.ContainsKey(slotType)) continue;
                if (!fillAll && Random.value < 0.5f) continue;

                var candidates = registry.GetCompatible(body, slotType)
                    .Where(a => a.spawnWeight > 0f).ToList();
                if (candidates.Count == 0) continue;
                state.Equip(slotType, WeightedRandom(candidates));
            }

            return state.ToConfiguration();
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        // Selects one attachment from the list using weighted random selection.
        // The probability of selecting an attachment is proportional to its spawnWeight
        // relative to the sum of all weights in the list.
        // Example: weights [60, 30, 10] yield probabilities 60 %, 30 %, 10 %.
        private static AttachmentData WeightedRandom(List<AttachmentData> candidates)
        {
            float total = candidates.Sum(c => c.spawnWeight);
            float roll  = Random.Range(0f, total);
            float cum   = 0f;
            foreach (var c in candidates)
            {
                cum += c.spawnWeight;
                if (roll <= cum) return c;
            }
            // Fallback handles floating-point edge cases where roll == total exactly
            return candidates[candidates.Count - 1];
        }
    }
}
