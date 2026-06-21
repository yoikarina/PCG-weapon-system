// ScriptableObject that stores optional animation, audio and VFX references for a gun part.
// All entries are added manually via the Weapon Workbench — nothing is required.

using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewGunMedia", menuName = "GunAssemblyTool/Gun Media Data")]
    public class GunMediaData : ScriptableObject
    {
        // All lists start empty. Add only what your project needs.
        public List<AnimationClip> animations = new List<AnimationClip>();
        public List<AudioClip>     sfx        = new List<AudioClip>();
        public List<GameObject>    vfx        = new List<GameObject>();
    }
}
