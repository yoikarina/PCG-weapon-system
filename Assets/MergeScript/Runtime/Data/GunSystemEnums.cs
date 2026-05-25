// Defines all shared enums used across the gun assembly system.

namespace GunAssemblyTool
{
    // Identifies which slot on a gun body an attachment occupies.
    // Each gun body can have at most one attachment per type at a time.
    public enum AttachmentType
    {
        Magazine,
        Barrel,
        Muzzle,
        Stock,
        Scope,
        Grip,
        Underbarrel,
        Skin
    }
}
