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

    // Specifies the thread size on a barrel or muzzle attachment.
    // A slot's allowedThreads whitelist is checked against this value
    // during compatibility resolution.
    public enum ThreadType
    {
        None,       // No thread — muzzle attachments cannot be fitted
        Standard,   // Most common thread; fits most suppressors and brakes
        Large,      // Heavy barrel thread for high-calibre weapons
        Shotgun     // Shotgun-specific thread size
    }

    // Specifies the physical magazine format of a magazine attachment.
    // A slot's allowedMags whitelist is checked against this value
    // during compatibility resolution.
    public enum MagType
    {
        None,       // No magazine well (e.g. fixed internal magazine)
        AR,         // AR / M4 / M16 double-stack format
        AK,         // AK-pattern curved magazine format
        Shotgun,    // Shotgun tube or box magazine format
        Pistol      // Single-stack pistol magazine format
    }
}
