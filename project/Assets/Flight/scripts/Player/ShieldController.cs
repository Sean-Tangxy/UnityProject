using UnityEngine;
using System.Collections;

public class ShieldController : MonoBehaviour
{
    [Header("盾牌配置")]
    public GameObject shieldPrefab;
    public Transform shieldHand; // 左手位置

    [Header("玩家属性")]
    public float playerStamina = 100f;
    public float staminaRecoveryRate = 10f;

    // 状态
    private GameObject currentShield;
    private ShieldObject shieldObject;
    private bool isHoldingBlock = false;
    private bool isBlocking = false; // 添加这个状态变量

    void Start()
    {
        InitializeShieldHand();

        if (shieldPrefab != null)
        {
            EquipShield(shieldPrefab);
        }
    }

    void Update()
    {
        HandleInput();
        UpdateStamina();

        // 更新防御状态（基于盾牌对象的状态）
        if (shieldObject != null)
        {
            isBlocking = shieldObject.IsBlocking();
        }
        else
        {
            isBlocking = false;
        }
    }

    void InitializeShieldHand()
    {
        if (shieldHand == null)
        {
            GameObject hand = new GameObject("ShieldHand");
            hand.transform.parent = transform;
            hand.transform.localPosition = new Vector3(-0.3f, 0.2f, 0); // 左手位置
            shieldHand = hand.transform;
        }
    }

    void HandleInput()
    {
        // 防御输入（鼠标右键或左Shift）
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.LeftShift))
        {
            StartBlocking();
        }

        if (Input.GetMouseButtonUp(1) || Input.GetKeyUp(KeyCode.LeftShift))
        {
            StopBlocking();
        }

        // 弹反输入（在防御时按攻击键）
        if (isBlocking && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {
            TryParry();
        }
    }

    void StartBlocking()
    {
        if (shieldObject == null) return;
        if (!shieldObject.CanBlock()) return;
        if (playerStamina < shieldObject.shieldData.blockStaminaCost) return;

        shieldObject.StartBlock();
        isHoldingBlock = true;

        // 消耗初始耐力
        playerStamina -= shieldObject.shieldData.blockStaminaCost * 0.5f;
    }

    void StopBlocking()
    {
        if (shieldObject == null) return;

        shieldObject.StopBlock();
        isHoldingBlock = false;
    }

    void TryParry()
    {
        if (shieldObject != null)
        {
            // 可以在这里添加弹反的特殊逻辑
            Debug.Log("尝试弹反");

            // 比如：快速按两次防御键触发弹反
            StartCoroutine(ParryAttemptRoutine());
        }
    }

    IEnumerator ParryAttemptRoutine()
    {
        // 弹反逻辑示例
        yield return new WaitForSeconds(0.1f);
        // 弹反成功或失败的处理
    }

    void UpdateStamina()
    {
        // 持续防御消耗耐力
        if (isHoldingBlock && shieldObject != null && isBlocking)
        {
            float staminaCost = shieldObject.shieldData.blockStaminaCost * Time.deltaTime;
            playerStamina = Mathf.Max(0, playerStamina - staminaCost);

            // 耐力耗尽自动停止防御
            if (playerStamina <= 0)
            {
                StopBlocking();
            }
        }
        else if (!isHoldingBlock && playerStamina < 100f)
        {
            // 恢复耐力
            playerStamina = Mathf.Min(100f, playerStamina + staminaRecoveryRate * Time.deltaTime);
        }
    }

    public void EquipShield(GameObject prefab)
    {
        // 移除旧盾牌
        if (currentShield != null)
        {
            Destroy(currentShield);
        }

        // 实例化新盾牌
        currentShield = Instantiate(prefab, shieldHand.position, shieldHand.rotation, shieldHand);
        shieldObject = currentShield.GetComponent<ShieldObject>();

        if (shieldObject == null)
        {
            Debug.LogError("盾牌预制体缺少ShieldObject组件");
            return;
        }

        // 设置玩家引用
        shieldObject.SetPlayerTransform(transform);

        // 调整位置
        AdjustShieldPosition();

        Debug.Log($"已装备盾牌: {shieldObject.shieldData.shieldName}");
    }

    void AdjustShieldPosition()
    {
        if (currentShield == null || shieldObject == null) return;

        // 可以根据盾牌数据调整位置
        if (shieldObject.shieldData != null)
        {
            currentShield.transform.localPosition = shieldObject.shieldData.shieldOffset;
        }
    }

    // 添加 IsBlocking 公共方法
    public bool IsBlocking()
    {
        return isBlocking;
    }

    // 添加获取盾牌对象的方法（供其他脚本调用）
    public ShieldObject GetShieldObject()
    {
        return shieldObject;
    }

    // 获取盾牌信息（用于UI显示）
    public string GetShieldInfo()
    {
        if (shieldObject == null || shieldObject.shieldData == null)
            return "未装备盾牌";

        return $"{shieldObject.shieldData.shieldName}\n" +
               $"耐久: {shieldObject.GetCurrentDurability():F0}/{shieldObject.shieldData.maxDurability}\n" +
               $"伤害减免: {shieldObject.shieldData.damageReduction:P0}\n" +
               $"防御角度: {shieldObject.shieldData.blockAngle}°";
    }
}