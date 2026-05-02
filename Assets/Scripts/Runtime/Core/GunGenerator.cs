using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    // Procedural gun generator
    // Can be used as a MonoBehaviour or called as a static utility
    public class GunGenerator : MonoBehaviour
    {
        [Header("Data source")]

        // All available attachments
        public AttachmentRegistry registry;

        // All possible gun bodies
        public List<GunBodyData> allBodies = new List<GunBodyData>();

        [Header("Generate settings")]

        // If true → every slot will be filled
        // If false → each slot is filled based on probability
        [Tooltip("true = fill all slots; false = use slotFillChance per slot")]
        public bool fillAllSlots = false;

        // Chance for each slot to have an attachment (only used when fillAllSlots = false)
        [Range(0f, 1f)]
        [Tooltip("Chance for each slot to be filled (used when fillAllSlots = false)")]
        public float slotFillChance = 0.7f;

        /// <summary>
        /// Instance entry point
        /// Generate and return a configuration snapshot
        /// </summary>
        public GunConfiguration Generate() =>
            Generate(registry, allBodies, fillAllSlots, slotFillChance);

        // ── Static API ─────────────────────────────────────

        /// <summary>
        /// Pick a random gun body, then randomly fill its slots with attachments
        /// </summary>
        public static GunConfiguration Generate(
            AttachmentRegistry registry,
            IList<GunBodyData> bodies,
            bool fillAll = false,
            float fillChance = 0.7f)
        {
            if (registry == null || bodies == null || bodies.Count == 0)
            {
                Debug.LogWarning("[GunGenerator] Registry or bodies list is missing.");
                return null;
            }

            // Pick a random gun body
            var body = bodies[Random.Range(0, bodies.Count)];

            var state = new GunAssemblyState(registry);
            state.SetBody(body);

            // Fill each available slot
            foreach (var slotType in body.availableSlots)
            {
                // Skip based on probability
                if (!fillAll && Random.value > fillChance) continue;

                // Get valid attachments and filter by spawn weight
                var candidates = registry.GetCompatible(body, slotType)
                    .Where(a => a.spawnWeight > 0f).ToList();

                if (candidates.Count == 0) continue;

                // Pick one using weighted random
                state.Equip(slotType, WeightedRandom(candidates));
            }

            return state.ToConfiguration();
        }

        /// <summary>
        /// Generate with constraints:
        /// Some slots are forced to use specific attachments
        /// Other slots are filled randomly
        /// </summary>
        public static GunConfiguration GenerateWithConstraints(
            AttachmentRegistry registry,
            IList<GunBodyData> bodies,
            Dictionary<AttachmentType, AttachmentData> forcedSlots,
            bool fillAll = true)
        {
            if (registry == null || bodies == null || bodies.Count == 0) return null;

            // Find gun bodies that are compatible with ALL forced attachments
            var compatibleBodies = bodies.Where(b =>
                forcedSlots.All(kv =>
                    kv.Value == null || CompatibilityResolver.IsCompatible(b, kv.Value))
            ).ToList();

            // If none found → fallback to all bodies
            if (compatibleBodies.Count == 0)
            {
                Debug.LogWarning("[GunGenerator] No body matches all constraints. Falling back to random.");
                compatibleBodies = bodies.ToList();
            }

            var body = compatibleBodies[Random.Range(0, compatibleBodies.Count)];

            var state = new GunAssemblyState(registry);
            state.SetBody(body);

            // Apply forced attachments first
            foreach (var kv in forcedSlots)
                if (kv.Value != null)
                    state.Equip(kv.Key, kv.Value);

            // Fill remaining slots randomly
            foreach (var slotType in body.availableSlots)
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

        // ── Internal helper ────────────────────────────────

        // Weighted random selection based on spawnWeight
        private static AttachmentData WeightedRandom(List<AttachmentData> candidates)
        {
            float total = candidates.Sum(c => c.spawnWeight);

            float roll = Random.Range(0f, total);
            float cum = 0f;

            foreach (var c in candidates)
            {
                cum += c.spawnWeight;
                if (roll <= cum) return c;
            }

            // Fallback (should rarely happen)
            return candidates[candidates.Count - 1];
        }
    }
}