// IGunDataProvider.cs
// ─────────────────────────────────────────────────────────────────────────────
// The SINGLE hand-off contract between the Gun Assembly Tool (the producer) and
// any consumer (the minigame demo, an AI agent, a save-game loader, a PCG
// spawner, ...).
//
// Consumers depend ONLY on this interface — never on the concrete component that
// implements it — so the producer can be swapped without changing a single line
// of consumer code:
//
//     • a weapon prefab baked by the Workbench   -> GunRuntimeData
//     • a runtime PCG spawner                     -> (future) RandomGunSpawner
//     • a loadout loaded from disk                -> (future) SaveGameGunProvider
//
// Lives in the GunAssemblyTool.Runtime assembly so BOTH the tool and the minigame
// can reference it.
// ─────────────────────────────────────────────────────────────────────────────

namespace GunAssemblyTool
{
    public interface IGunDataProvider
    {
        /// <summary>
        /// Returns the fully-assembled weapon data:
        /// gun body + attachments + computed <see cref="GunStats"/> + resolved media.
        /// Implementations may build lazily and cache the result.
        /// Should not return null once the gun has been assembled.
        /// </summary>
        GunInstanceData GetGunData();
    }
}