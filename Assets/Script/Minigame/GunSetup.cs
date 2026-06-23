// GunSetup.cs
// Runs once on Awake to wire scene references into the weapon prefab's
// components, then destroys itself. All wiring is best-effort — GunData
// will self-heal any remaining nulls on first use.
//
// Scene requirements (all optional — warnings logged if missing):
//   • "Player"          tag  → PickUpController.player
//   • "WeaponContainer" tag  → PickUpController.gunContainer
//   • "MainCamera"      tag  → PickUpController.Camera (Unity default)
//   • DamageCalculation component anywhere in the scene

using UnityEngine;

public class GunSetup : MonoBehaviour
{
    private void Awake()
    {
        var gunData = GetComponent<GunData>();
        var pickUp = GetComponent<PickUpController>();
        var weaponCol = GetComponent<WeaponCollection>();

        // ── Trigger GunData initialisation first ──────────────────────────────
        // runtimeData is already wired by the workbench wiring pass, so this
        // should succeed. If it fails, GunData will retry on first shot.
        gunData?.Initialise();

        // ── Resolve GunData scene refs ────────────────────────────────────────
        gunData?.ResolveSceneRefs();

        // ── Wire PickUpController ─────────────────────────────────────────────
        if (pickUp != null)
        {
            pickUp.gun = gunData;
            pickUp.rb = GetComponent<Rigidbody>();

            foreach (var col in GetComponents<BoxCollider>())
                if (col.isTrigger) { pickUp.coll = col; break; }

            var playerGO = GameObject.FindWithTag("Player");
            var containerGO = GameObject.FindWithTag("WeaponContainer");

            if (playerGO != null)
                pickUp.player = playerGO.transform;
            else
                Debug.LogWarning($"[GunSetup] No 'Player' tag found — " +
                                 $"assign PickUpController.player manually on '{name}'.");

            if (containerGO != null)
                pickUp.gunContainer = containerGO.transform;
            else
                Debug.LogWarning($"[GunSetup] No 'WeaponContainer' tag found — " +
                                 $"assign PickUpController.gunContainer manually on '{name}'.");

            if (Camera.main != null)
                pickUp.Camera = Camera.main.transform;
            else
                Debug.LogWarning($"[GunSetup] No MainCamera found — " +
                                 $"assign PickUpController.Camera manually on '{name}'.");
        }

        // ── Wire WeaponCollection ─────────────────────────────────────────────
        if (weaponCol != null)
        {
            weaponCol.gunData = gunData;
            weaponCol.pickUpController = pickUp;
        }

        Destroy(this);
    }
}