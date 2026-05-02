using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    // Simple scene helper:
    // Spawns a random gun and applies attachments visually
    public class RandomGunSpawner : MonoBehaviour
    {
        [Header("Data source")]

        public AttachmentRegistry registry;
        public List<GunBodyData> allBodies = new List<GunBodyData>();

        [Header("Generate settings")]

        public bool fillAllSlots = false;

        [Range(0f, 1f)]
        public float slotFillChance = 0.8f;

        // Currently spawned gun instance
        private GameObject _currentBodyInstance;

        private void Start() => Randomize();

        private void Update()
        {
            // Left click to generate a new random gun
            if (Input.GetMouseButtonDown(0))
                Randomize();
        }

        // Generate and spawn a new gun
        public void Randomize()
        {
            if (registry == null)
            {
                Debug.LogWarning("[RandomGunSpawner] Registry is not assigned.");
                return;
            }

            if (allBodies == null || allBodies.Count == 0)
            {
                Debug.LogWarning("[RandomGunSpawner] No gun bodies available.");
                return;
            }

            var config = GunGenerator.Generate(registry, allBodies, fillAllSlots, slotFillChance);
            if (config == null) return;

            // Find selected body data
            var bodyData = allBodies.FirstOrDefault(b => b.bodyId == config.bodyId);
            if (bodyData == null) return;

            // Remove previous instance
            if (_currentBodyInstance != null)
                Destroy(_currentBodyInstance);

            if (bodyData.bodyPrefab == null)
            {
                Debug.LogWarning($"[RandomGunSpawner] bodyPrefab is missing for {bodyData.bodyId}.");
                return;
            }

            // Instantiate gun body
            _currentBodyInstance = Instantiate(bodyData.bodyPrefab, transform);
            _currentBodyInstance.transform.localPosition = Vector3.zero;
            _currentBodyInstance.transform.localRotation = Quaternion.identity;

            // Apply attachments on next frame (wait for prefab init)
            StartCoroutine(ApplyNextFrame(config, bodyData));
        }

        private IEnumerator ApplyNextFrame(GunConfiguration config, GunBodyData bodyData)
        {
            yield return null;

            if (_currentBodyInstance == null) yield break;

            var controller = _currentBodyInstance.GetComponent<GunAssemblyController>();

            if (controller == null)
            {
                Debug.LogWarning("[RandomGunSpawner] Prefab does not have GunAssemblyController.");
                yield break;
            }

            // Pass registry so controller can rebuild its internal state correctly
            controller.ApplyConfiguration(config, allBodies, registry);
        }
    }
}