using UnityEngine;
using System.Collections;

[RequireComponent(typeof(DirectionalMovement))]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("武器配置")]
    public GameObject weaponPrefab;
    public WeaponData currentWeaponData;

    [Header("挂载点命名（Player子物体）")]
    public string handUpName = "WeaponHand_up";
    public string handDownName = "WeaponHand_down";
    public string handLeftName = "WeaponHand_left";
    public string handRightName = "WeaponHand_right";

    [Header("自动识别")]
    public bool autoFindHands = true;

    [Header("渲染层级（地板=0，人物=2）")]
    public int floorOrder = 0;
    public int playerOrder = 2;
    public int behindPlayerOrder = 1; // 人物后
    public int frontPlayerOrder = 3;  // 人物前

    // 当前挂载点
    private Transform weaponHand;

    // 4向挂载点
    private Transform handUp, handDown, handLeft, handRight;

    // 依赖：移动脚本
    private DirectionalMovement move;

    // 武器状态
    private GameObject currentWeaponInstance;
    private WeaponObject weaponObject;
    private Vector3 weaponLocalOffset;

    void Start()
    {
        move = GetComponent<DirectionalMovement>();

        if (autoFindHands) CacheHands();

        // 默认朝下
        weaponHand = handDown != null ? handDown :
                     handRight != null ? handRight :
                     handLeft != null ? handLeft :
                     handUp;

        if (weaponHand == null)
        {
            Debug.LogError("未找到 WeaponHand_up/down/left/right，请在Player子物体下创建同名空物体。");
            enabled = false;
            return;
        }

        if (weaponPrefab != null)
        {
            EquipWeapon(weaponPrefab, currentWeaponData);
        }
    }

    void CacheHands()
    {
        handUp = FindChildByName(transform, handUpName);
        handDown = FindChildByName(transform, handDownName);
        handLeft = FindChildByName(transform, handLeftName);
        handRight = FindChildByName(transform, handRightName);
    }

    Transform FindChildByName(Transform root, string targetName)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t.name == targetName) return t;
        }
        return null;
    }

    void Update()
    {
        HandleInput();

        Vector2 facing4 = move.GetFacing4Dir();
        bool attacking = (weaponObject != null && !weaponObject.CanAttack());

        // ✅ 攻击中不切挂载点、不更新瞄准（避免和挥砍/突刺打架）
        if (!attacking)
        {
            UpdateWeaponHandByFacing(facing4);
            UpdateWeaponAim();
        }

        // ✅ 渲染层级：每帧更新（不管是否攻击）
        UpdateWeaponRenderOrder(facing4);

        if (currentWeaponInstance != null && weaponHand != null)
        {
            UpdateWeaponFollow();
        }
    }

    #region 输入
    void HandleInput()
    {
        // F：卸下武器
        if (Input.GetKeyDown(KeyCode.F))
        {
            UnequipWeapon();
            return;
        }

        if (weaponObject == null) return;

        // 空格：突刺（有 StartThrust 就调用，没有就回退普通攻击）
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryThrust();
            return;
        }

        // 鼠标左键：普通攻击（挥砍）
        if (Input.GetMouseButtonDown(0))
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

    void TryThrust()
    {
        if (weaponObject == null) return;
        if (!weaponObject.CanAttack()) return;

        weaponObject.SendMessage("StartThrust", SendMessageOptions.DontRequireReceiver);
        StartCoroutine(ThrustFallbackNextFrame());
    }

    IEnumerator ThrustFallbackNextFrame()
    {
        yield return null;
        if (weaponObject != null && weaponObject.CanAttack())
        {
            weaponObject.StartAttack();
        }
    }
    #endregion

    #region 挂载点切换
    void UpdateWeaponHandByFacing(Vector2 facing4)
    {
        Transform target = weaponHand;

        if (facing4 == Vector2.up) target = handUp != null ? handUp : target;
        else if (facing4 == Vector2.down) target = handDown != null ? handDown : target;
        else if (facing4 == Vector2.left) target = handLeft != null ? handLeft : target;
        else if (facing4 == Vector2.right) target = handRight != null ? handRight : target;

        if (target != null && target != weaponHand)
        {
            weaponHand = target;

            // 切挂载点后重新绑定 pivot（让武器绕挂载点转）
            if (weaponObject != null)
                weaponObject.SetPivot(weaponHand);

            CalculateWeaponOffset();
            UpdateWeaponFollow();
        }
    }
    #endregion

    #region 跟随与偏移
    void UpdateWeaponFollow()
    {
        Vector3 worldOffset = weaponHand.TransformVector(weaponLocalOffset);
        currentWeaponInstance.transform.position = weaponHand.position + worldOffset;
        currentWeaponInstance.transform.rotation = weaponHand.rotation;
    }

    void CalculateWeaponOffset()
    {
        if (weaponObject == null) return;

        Vector3 handleLocalPos = weaponObject.GetHandleLocalPosition();
        weaponLocalOffset = -handleLocalPos;

        if (weaponObject.weaponData != null)
        {
            weaponLocalOffset += (Vector3)weaponObject.weaponData.gripOffset;
        }
    }
    #endregion

    #region 瞄准
    void UpdateWeaponAim()
    {
        if (weaponHand == null) return;
        if (Camera.main == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;

        Vector2 direction = (mousePos - transform.position);
        if (direction.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        weaponHand.rotation = Quaternion.Euler(0, 0, angle);
    }
    #endregion

    #region 渲染层级
    void UpdateWeaponRenderOrder(Vector2 facing4)
    {
        if (currentWeaponInstance == null) return;

        int order = (facing4 == Vector2.up) ? behindPlayerOrder : frontPlayerOrder;

        // 武器可能有多个 SpriteRenderer（例如特效/子物体），统一设置
        var srs = currentWeaponInstance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            srs[i].sortingOrder = order;
        }
    }
    #endregion

    #region 装备/卸下
    public void EquipWeapon(GameObject prefab, WeaponData customData = null)
    {
        if (weaponHand == null)
        {
            Debug.LogError("weaponHand 为空：没有可用的挂载点。");
            return;
        }

        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
        }

        currentWeaponInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        weaponObject = currentWeaponInstance.GetComponent<WeaponObject>();

        if (weaponObject == null)
        {
            Debug.LogError("武器预制体缺少 WeaponObject 组件");
            Destroy(currentWeaponInstance);
            currentWeaponInstance = null;
            return;
        }

        if (customData != null)
        {
            weaponObject.weaponData = customData;
            currentWeaponData = customData;
        }
        else if (currentWeaponData != null)
        {
            weaponObject.weaponData = currentWeaponData;
        }

        weaponObject.SetPivot(weaponHand);

        CalculateWeaponOffset();
        UpdateWeaponFollow();

        // 初次设置渲染层级
        UpdateWeaponRenderOrder(move.GetFacing4Dir());
    }

    public void UnequipWeapon()
    {
        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
        }

        currentWeaponInstance = null;
        weaponObject = null;
        weaponLocalOffset = Vector3.zero;

        Debug.Log("已卸下武器");
    }
    #endregion
}
