using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AttachmentsSO", menuName = "Scriptable Objects/AttachmentsSO")]
public class Attachments : PartData
{
    public List<WeaponData> weaponDatas;    // Parts can be equipped to multiple weaponData, a magazine can be put on an AK-47 or a M16
}
