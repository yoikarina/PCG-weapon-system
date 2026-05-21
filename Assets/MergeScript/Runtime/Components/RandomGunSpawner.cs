// Scene entry point; replaces the active gun prefab with a newly generated random one on left mouse click.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    public class RandomGunSpawner : MonoBehaviour
    {
        [Header("Data Source")]
        public AttachmentRegistry registry;
        public List<GunBodyData> allBodies = new List<GunBodyData>();

        [Header("Generate Settings")]
        [Tooltip("When true every available slot is filled; " +
                 "when false each slot is filled with slotFillChance probability.")]
        public bool fillAllSlots = false;

        [Range(0f, 1f)]
        [Tooltip("Probability that each slot receives an attachment when fillAllSlots is false.")]
        public float slotFillChance = 0.8f;

        [Header("Player Integration")]
        [Tooltip("Drag the Player GameObject here so the spawner can wire up " +
                 "onHpBonusChanged and pass the active controller reference " +
                 "each time a new gun is spawned.")]
        public Player player;

        // The gun GameObject currently instantiated in the scene.
        private GameObject _currentBodyInstance;

        // Generates the first gun as soon as the scene starts.
        private void Start() => Randomize();

        // Listens for left mouse button clicks and triggers a new generation on each press.
        private void Update() { if (Input.GetMouseButtonDown(0)) Randomize(); }

        // Generates a random GunConfiguration, destroys the current gun instance,
        // instantiates the new gun body prefab, and starts the ApplyNextFrame coroutine.
        public void Randomize()
        {
            if (registry == null)
            {
                Debug.LogWarning("[RandomGunSpawner] Registry is not assigned.");
                return;
            }
            if (allBodies == null || allBodies.Count == 0)
            {
                Debug.LogWarning("[RandomGunSpawner] No gun bodies in allBodies list.");
                return;
            }

            var config = GunGenerator.Generate(registry, allBodies, fillAllSlots, slotFillChance);
            if (config == null) return;

            var bodyData = allBodies.FirstOrDefault(b => b.bodyId == config.bodyId);
            if (bodyData == null) return;

            if (_currentBodyInstance != null) Destroy(_currentBodyInstance);

            if (bodyData.bodyPrefab == null)
            {
                Debug.LogWarning($"[RandomGunSpawner] bodyPrefab not set on {bodyData.bodyId}.");
                return;
            }

            _currentBodyInstance = Instantiate(bodyData.bodyPrefab, transform);
            _currentBodyInstance.transform.localPosition = Vector3.zero;
            _currentBodyInstance.transform.localRotation = Quaternion.identity;

            StartCoroutine(ApplyNextFrame(config, bodyData));
        }

        // Waits one frame then applies the configuration and wires up Player events.
        private IEnumerator ApplyNextFrame(GunConfiguration config, GunBodyData bodyData)
        {
            yield return null;

            if (_currentBodyInstance == null) yield break;

            var controller = _currentBodyInstance.GetComponent<GunAssemblyController>();
            if (controller == null)
            {
                Debug.LogWarning("[RandomGunSpawner] Gun prefab is missing GunAssemblyController.");
                yield break;
            }

            // Inject the registry into the freshly instantiated controller.
            controller.ApplyConfiguration(config, allBodies, registry);

            // Wire the controller to the Player so HP bonus and ammo updates flow through.
            if (player != null)
            {
                // Update the player's active controller reference so OnReload can
                // read CurrentAmmoCapacity from the new gun.
                player.activeGunController = controller;

                // Clear any listeners from the previous gun then bind to the new one.
                // This ensures only the current gun's events drive the player stats.
                controller.onHpBonusChanged.RemoveAllListeners();
                controller.onHpBonusChanged.AddListener(player.BuffAttributes);

                // Immediately push the new gun's barrel HP bonus and ammo capacity
                // to the UI so the display is correct before the player fires or reloads.
                player.BuffAttributes(controller.State.ComputeBarrelHpBonus());
                player.stats?.UIMaxAmmo(controller.CurrentAmmoCapacity);
                player.stats?.UIAmmo(controller.CurrentAmmoCapacity);
            }

            Debug.Log(
                $"[RandomGunSpawner] Body={bodyData.bodyId} | " +
                $"Parts=[{string.Join(", ", config.slots.Values)}]");
        }
    }
}