//// Scene entry point; replaces the active gun prefab with a newly generated random one on left mouse click.

//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//namespace GunAssemblyTool
//{
//    // Attach this to an empty parent GameObject in the scene — NOT on the gun prefab itself.
//    // On Start it spawns one gun immediately; each left mouse click replaces it with a new one.
//    public class RandomGunSpawner : MonoBehaviour
//    {
//        [Header("Data Source")]
//        public AttachmentRegistry registry;
//        public List<GunBodyData>  allBodies = new List<GunBodyData>();

//        [Header("Generate Settings")]
//        [Tooltip("When true every available slot is filled; " +
//                 "when false each slot is filled with slotFillChance probability.")]
//        public bool fillAllSlots = false;

//        [Range(0f, 1f)]
//        [Tooltip("Probability that each slot receives an attachment when fillAllSlots is false.")]
//        public float slotFillChance = 0.8f;

//        // The gun GameObject currently instantiated in the scene.
//        private GameObject _currentBodyInstance;

//        // Generates the first gun as soon as the scene starts.
//        private void Start()  => Randomize();

//        // Listens for left mouse button clicks and triggers a new generation on each press.
//        private void Update() { if (Input.GetMouseButtonDown(0)) Randomize(); }

//        // Generates a random GunConfiguration, destroys the current gun instance,
//        // instantiates the new gun body prefab, and starts the ApplyNextFrame coroutine.
//        public void Randomize()
//        {
//            if (registry == null)
//            {
//                Debug.LogWarning("[RandomGunSpawner] Registry is not assigned.");
//                return;
//            }
//            if (allBodies == null || allBodies.Count == 0)
//            {
//                Debug.LogWarning("[RandomGunSpawner] No gun bodies in allBodies list.");
//                return;
//            }

//            var config = GunGenerator.Generate(registry, allBodies, fillAllSlots, slotFillChance);
//            if (config == null) return;

//            var bodyData = allBodies.FirstOrDefault(b => b.bodyId == config.bodyId);
//            if (bodyData == null) return;

//            if (_currentBodyInstance != null) Destroy(_currentBodyInstance);

//            if (bodyData.bodyPrefab == null)
//            {
//                Debug.LogWarning($"[RandomGunSpawner] bodyPrefab not set on {bodyData.bodyId}.");
//                return;
//            }

//            // Instantiate the new gun body as a child of this GameObject so it
//            // inherits the spawner's world transform.
//            _currentBodyInstance = Instantiate(bodyData.bodyPrefab, transform);
//            _currentBodyInstance.transform.localPosition = Vector3.zero;
//            _currentBodyInstance.transform.localRotation = Quaternion.identity;

//            StartCoroutine(ApplyNextFrame(config, bodyData));
//        }

//        // Waits one frame after instantiation before calling ApplyConfiguration.
//        // This ensures all GunAttachmentPoint Awake() calls on the new prefab's
//        // child nodes have completed before the controller attempts to look them up.
//        // Without this delay the controller's _points dictionary is empty and
//        // attachment visuals will not appear in the scene.
//        private IEnumerator ApplyNextFrame(GunConfiguration config, GunBodyData bodyData)
//        {
//            yield return null;

//            if (_currentBodyInstance == null) yield break;

//            var controller = _currentBodyInstance.GetComponent<GunAssemblyController>();
//            if (controller == null)
//            {
//                Debug.LogWarning("[RandomGunSpawner] Gun prefab is missing GunAssemblyController.");
//                yield break;
//            }

//            // Pass the registry explicitly because the controller's own registry field
//            // may still be null immediately after instantiation.
//            controller.ApplyConfiguration(config, allBodies, registry);

//            Debug.Log(
//                $"[RandomGunSpawner] Body={bodyData.bodyId} | " +
//                $"Parts=[{string.Join(", ", config.slots.Values)}]");
//        }
//    }
//}
