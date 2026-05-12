using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem; // 使用新版输入系统

public class SimpleWeaponRandomizer : MonoBehaviour
{
    [Header("枪身资源池")]
    public List<GameObject> bodyPrefabs;

    [Header("配件资源池")]
    public List<GameObject> muzzlePrefabs;   // 枪口池
    public List<GameObject> magazinePrefabs; // 弹匣池 (新加入)

    private GameObject currentGun;

    void Update()
    {
        // 新版 Input System 检测空格键
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            GenerateRandomWeapon();
        }
    }

    void GenerateRandomWeapon()
    {
        if (currentGun != null) Destroy(currentGun);

        // 1. 生成随机枪身
        GameObject randomBody = bodyPrefabs[Random.Range(0, bodyPrefabs.Count)];
        currentGun = Instantiate(randomBody, Vector3.zero, Quaternion.identity);

        // 2. 挂载枪口 (Muzzle)
        AttachPart("Socket_Muzzle", muzzlePrefabs);

        // 3. 挂载弹匣 (Magazine) - 逻辑完全相同
        AttachPart("Socket_Magazine", magazinePrefabs);
    }

    // 为了避免重复写代码，我们写一个通用的挂载函数
    private void AttachPart(string socketName, List<GameObject> partPool)
    {
        if (partPool == null || partPool.Count == 0) return;

        // 在生成的枪身中寻找对应的插槽
        Transform socket = currentGun.transform.Find(socketName);

        if (socket != null)
        {
            // 随机选一个配件
            GameObject prefab = partPool[Random.Range(0, partPool.Count)];
            
            // 生成并对齐
            GameObject instance = Instantiate(prefab, socket.position, socket.rotation);
            instance.transform.SetParent(socket);
        }
        else
        {
            Debug.LogWarning($"找不到插槽: {socketName}，请检查枪身 Prefab 层级。");
        }
    }
}