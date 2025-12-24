using System.Linq;
using UnityEngine;

public class PlayerEquipRegenerator : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject weaponPrefab;
    public GameObject shieldPrefab;

    [Header("四向挂点名字（按你层级真实名字填）")]
    public string weaponUp = "WeaponHand_up";
    public string weaponDown = "WeaponHand_down";
    public string weaponLeft = "WeaponHand_left";
    public string weaponRight = "WeaponHand_right";

    public string shieldUp = "ShieldHand_up";
    public string shieldDown = "ShieldHand_down";
    public string shieldLeft = "ShieldHand_left";
    public string shieldRight = "ShieldHand_right";

    [Header("实例命名关键字（用于清理旧实例）")]
    public string weaponNameKey = "Weapon";
    public string shieldNameKey = "Shield";

    private DirectionalMovement move;

    void Awake()
    {
        move = GetComponent<DirectionalMovement>();
    }

    /// <summary>
    /// 场景切换后调用：清理旧实例 -> 重建到当前朝向挂点
    /// </summary>
    public void Regenerate()
    {
        // 1) 清理旧的武器/盾牌实例（只清理挂点子物体，不动角色本体）
        CleanupChildrenByKey(weaponNameKey);
        CleanupChildrenByKey(shieldNameKey);

        // 2) 按朝向选择挂点并生成
        var face = move != null ? move.GetFacing4Dir() : Vector2.down;

        Transform weaponMount = FindMount(GetMountName(face, weaponUp, weaponDown, weaponLeft, weaponRight));
        Transform shieldMount = FindMount(GetMountName(face, shieldUp, shieldDown, shieldLeft, shieldRight));

        if (weaponPrefab != null && weaponMount != null)
            SpawnTo(weaponPrefab, weaponMount);

        if (shieldPrefab != null && shieldMount != null)
            SpawnTo(shieldPrefab, shieldMount);
    }

    // ---------- helpers ----------

    private void CleanupChildrenByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            // 只删“挂点下面的实例”，避免误删Player本体
            if (t == transform) continue;
            if (t.name.Contains(key) && t.parent != null && t.parent != transform)
            {
                // 只删除子物体，不删挂点本身
                Destroy(t.gameObject);
            }
        }
    }

    private static string GetMountName(Vector2 face, string up, string down, string left, string right)
    {
        if (face == Vector2.up) return up;
        if (face == Vector2.down) return down;
        if (face == Vector2.left) return left;
        return right; // Vector2.right
    }

    private Transform FindMount(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }

    private void SpawnTo(GameObject prefab, Transform mount)
    {
        // 如果你希望“每次只存在一个实例”，这里先确保挂点空
        foreach (Transform child in mount) Destroy(child.gameObject);

        var go = Instantiate(prefab, mount.position, mount.rotation, mount);
        go.name = prefab.name;   // 方便调试
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.SetActive(true);
    }
}
