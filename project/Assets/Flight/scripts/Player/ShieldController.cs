using UnityEngine;
using System.Collections;

[RequireComponent(typeof(DirectionalMovement))]
public class ShieldController : MonoBehaviour
{
    [Header("盾牌配置")]
    public GameObject shieldPrefab;

    [Header("盾牌挂载点命名（Player子物体）")]
    public string shieldUpName = "ShieldHand_up";
    public string shieldDownName = "ShieldHand_down";
    public string shieldLeftName = "ShieldHand_left";
    public string shieldRightName = "ShieldHand_right";

    [Header("玩家属性")]
    public float playerStamina = 100f;
    public float staminaRecoveryRate = 10f;

    [Header("渲染层级（地板=0，人物=2）")]
    public int floorOrder = 0;
    public int playerOrder = 2;
    public int behindPlayerOrder = 1; // 人物后
    public int frontPlayerOrder = 3;  // 人物前

    private Transform shieldHand;

    private Transform handUp, handDown, handLeft, handRight;

    private DirectionalMovement move;

    private GameObject currentShield;
    private ShieldObject shieldObject;

    private bool isHoldingBlock = false;
    private bool isBlocking = false;

    // 🔒 防御锁定
    private bool lockFacing = false;
    private Quaternion lockedRotation;
    private Vector2 lockedFacing4 = Vector2.down;

    void Start()
    {
        move = GetComponent<DirectionalMovement>();
        CacheShieldHands();

        // ✅ 一次性确认是否都找到了
        Debug.Log($"ShieldHands found: up={handUp != null}, down={handDown != null}, left={handLeft != null}, right={handRight != null}");

        shieldHand = handDown != null ? handDown :
                     handLeft != null ? handLeft :
                     handRight != null ? handRight :
                     handUp;

        if (shieldHand == null)
        {
            Debug.LogError("未找到 ShieldHand_up/down/left/right（检查名字与层级是否在Player子物体下）");
            enabled = false;
            return;
        }

        if (shieldPrefab != null)
        {
            EquipShield(shieldPrefab);
        }
    }

    void Update()
    {
        HandleInput();
        UpdateStamina();

        isBlocking = shieldObject != null && shieldObject.IsBlocking();

        // ✅ 强制压成四向，避免 float/斜向导致 == 失败
        Vector2 facing4 = ToFacing4(move.GetFacing4Dir());

        if (!lockFacing)
        {
            UpdateShieldHandByFacing(facing4);
        }

        UpdateShieldRenderOrder(lockFacing ? lockedFacing4 : facing4);
        UpdateShieldFollow();
    }

    #region 挂载点
    void CacheShieldHands()
    {
        handUp = FindChildByNameIgnoreCase(transform, shieldUpName);
        handDown = FindChildByNameIgnoreCase(transform, shieldDownName);
        handLeft = FindChildByNameIgnoreCase(transform, shieldLeftName);
        handRight = FindChildByNameIgnoreCase(transform, shieldRightName);
    }

    Transform FindChildByNameIgnoreCase(Transform root, string targetName)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (string.Equals(all[i].name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return all[i];
        }
        return null;
    }

    // ✅ 不管输入是(0.7,0.7)还是(0,0.999)都压成上下左右
    Vector2 ToFacing4(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return Vector2.down; // 兜底：无朝向时给down
        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);
        if (ax > ay) return dir.x >= 0 ? Vector2.right : Vector2.left;
        return dir.y >= 0 ? Vector2.up : Vector2.down;
    }

    void UpdateShieldHandByFacing(Vector2 facing4)
    {
        Transform target = shieldHand;

        if (facing4 == Vector2.up) target = handUp ?? target;
        else if (facing4 == Vector2.down) target = handDown ?? target;
        else if (facing4 == Vector2.left) target = handLeft ?? target;
        else if (facing4 == Vector2.right) target = handRight ?? target;

        if (target != null && target != shieldHand)
        {
            shieldHand = target;
            if (shieldObject != null)
                shieldObject.SendMessage("SetPivot", shieldHand, SendMessageOptions.DontRequireReceiver);
        }
    }

    void UpdateShieldFollow()
    {
        if (currentShield == null || shieldHand == null) return;

        if (lockFacing)
        {
            currentShield.transform.position = shieldHand.position;
            currentShield.transform.rotation = lockedRotation;
        }
        else
        {
            currentShield.transform.position = shieldHand.position;
            currentShield.transform.rotation = shieldHand.rotation;
        }
    }
    #endregion

    #region 输入（仅右键）
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(1)) StartBlocking();
        if (Input.GetMouseButtonUp(1)) StopBlocking();

        if (isBlocking && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
            TryParry();
    }

    void StartBlocking()
    {
        if (shieldObject == null) return;
        if (!shieldObject.CanBlock()) return;
        if (playerStamina < shieldObject.shieldData.blockStaminaCost) return;

        shieldObject.StartBlock();
        isHoldingBlock = true;

        lockFacing = true;
        lockedFacing4 = ToFacing4(move.GetFacing4Dir());
        lockedRotation = shieldHand.rotation;

        playerStamina -= shieldObject.shieldData.blockStaminaCost * 0.5f;
    }

    void StopBlocking()
    {
        if (shieldObject == null) return;

        shieldObject.StopBlock();
        isHoldingBlock = false;
        lockFacing = false;
    }

    void TryParry()
    {
        if (shieldObject != null) StartCoroutine(ParryAttemptRoutine());
    }

    IEnumerator ParryAttemptRoutine()
    {
        yield return new WaitForSeconds(0.1f);
    }
    #endregion

    #region 耐力
    void UpdateStamina()
    {
        if (isHoldingBlock && isBlocking)
        {
            float cost = shieldObject.shieldData.blockStaminaCost * Time.deltaTime;
            playerStamina = Mathf.Max(0, playerStamina - cost);
            if (playerStamina <= 0) StopBlocking();
        }
        else if (!isHoldingBlock && playerStamina < 100f)
        {
            playerStamina = Mathf.Min(100f, playerStamina + staminaRecoveryRate * Time.deltaTime);
        }
    }
    #endregion

    #region 渲染层级
    void UpdateShieldRenderOrder(Vector2 facing4)
    {
        if (currentShield == null) return;

        int order = (facing4 == Vector2.up) ? behindPlayerOrder : frontPlayerOrder;

        var srs = currentShield.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            srs[i].sortingOrder = order;
    }
    #endregion

    #region 装备
    public void EquipShield(GameObject prefab)
    {
        if (currentShield != null) Destroy(currentShield);

        currentShield = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        shieldObject = currentShield.GetComponent<ShieldObject>();

        if (shieldObject == null)
        {
            Destroy(currentShield);
            currentShield = null;
            return;
        }

        shieldObject.SetPlayerTransform(transform);
        shieldObject.SendMessage("SetPivot", shieldHand, SendMessageOptions.DontRequireReceiver);

        UpdateShieldFollow();
        UpdateShieldRenderOrder(ToFacing4(move.GetFacing4Dir()));
    }
    #endregion

    public bool IsBlocking() => isBlocking;
    public ShieldObject GetShieldObject() => shieldObject;
}
