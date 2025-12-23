using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    [Header("武器配置")]
    public GameObject weaponPrefab;
    public WeaponData currentWeaponData;

    [Header("挂载点")]
    public Transform weaponHand;

    // 状态
    private GameObject currentWeaponInstance;
    private WeaponObject weaponObject;
    private Vector3 weaponLocalOffset; // 武器本地偏移

    void Start()
    {
        // 确保有weaponHand
        if (weaponHand == null)
        {
            CreateWeaponHand();
        }

        if (weaponPrefab != null)
        {
            EquipWeapon(weaponPrefab, currentWeaponData);
        }
    }

    void CreateWeaponHand()
    {
        GameObject hand = new GameObject("WeaponHand");
        hand.transform.parent = transform;
        hand.transform.localPosition = new Vector3(0.5f, 0, 0);
        weaponHand = hand.transform;
    }

    void Update()
    {
        HandleInput();
        UpdateWeaponAim();

        // 关键：每帧确保武器在正确位置
        if (currentWeaponInstance != null)
        {
            UpdateWeaponFollow();
        }
    }

    void UpdateWeaponFollow()
    {
        // 计算武器应该在世界空间中的位置
        Vector3 worldOffset = weaponHand.TransformVector(weaponLocalOffset);
        currentWeaponInstance.transform.position = weaponHand.position + worldOffset;
        currentWeaponInstance.transform.rotation = weaponHand.rotation;
    }

    public void EquipWeapon(GameObject prefab, WeaponData customData = null)
    {
        // 移除旧武器
        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
        }

        // 实例化新武器（不设父物体，我们自己控制位置）
        currentWeaponInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        weaponObject = currentWeaponInstance.GetComponent<WeaponObject>();

        if (weaponObject == null)
        {
            Debug.LogError("武器预制体缺少WeaponObject组件");
            return;
        }

        // 应用武器数据
        if (customData != null)
        {
            weaponObject.weaponData = customData;
            currentWeaponData = customData;
        }
        else if (currentWeaponData != null)
        {
            weaponObject.weaponData = currentWeaponData;
        }

        // 计算武器的本地偏移
        CalculateWeaponOffset();

        Debug.Log($"已装备武器: {weaponObject.GetWeaponInfo()}");
    }

    void CalculateWeaponOffset()
    {
        if (weaponObject == null) return;

        // 获取武器的手柄点（相对位置）
        Vector3 handleLocalPos = weaponObject.GetHandleLocalPosition();

        // 计算反向偏移（让手柄点对齐到weaponHand）
        weaponLocalOffset = -handleLocalPos;

        // 应用武器数据中的额外偏移
        if (weaponObject.weaponData != null)
        {
            weaponLocalOffset += (Vector3)weaponObject.weaponData.gripOffset;
        }
    }

    void UpdateWeaponAim()
    {
        if (weaponHand == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0; // 确保Z轴一致

        Vector2 direction = (mousePos - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        weaponHand.rotation = Quaternion.Euler(0, 0, angle);
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            TryAttack();
        }
    }

    void TryAttack()
    {
        if (weaponObject == null) return;

        if (weaponObject.CanAttack())
        {
            weaponObject.StartAttack();
        }
    }
}