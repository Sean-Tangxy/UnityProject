using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class BossWeaponData
{
    public string weaponName;
    public int weaponID;
    public Transform weaponTransform;
    public float attackDamage;
    public float moveSpeed;
    public float rotationSpeed;
    public SpriteRenderer weaponRenderer;
    public CircleCollider2D weaponCollider; // 改为CircleCollider更适合旋转
    public Vector3 originalLocalPosition;
    public Quaternion originalLocalRotation;
    public bool canDamagePlayer = true; // 是否可以对玩家造成伤害
    public float damageCooldown = 0.5f; // 伤害冷却时间
    public float lastDamageTime; // 上次造成伤害的时间
}

public enum BossState
{
    ShieldPhase,
    WeaponPhase,
    VulnerablePhase,
    Stunned
}

public enum WeaponMode
{
    State1,     // 绕身体旋转
    Mode1,      // 固定方向冲刺
    Mode2,      // 并排冲刺
    Mode3,      // 等差距离旋转
    Mode4       // 武器消失（输出窗口）
}

public class BossEnemyController : MonoBehaviour
{
    [Header("===== BOSS基础设置 =====")]
    [SerializeField] private int maxHealth = 5000;
    [SerializeField] private BossState currentBossState = BossState.ShieldPhase;

    [Header("===== 统一健康系统 =====")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemyHealthBar healthBar;

    [Header("===== 盾牌视觉 =====")]
    [SerializeField] private Transform shieldBody;
    [SerializeField] private SpriteRenderer shieldRenderer;
    [SerializeField] private BoxCollider2D shieldCollider;
    [SerializeField] private Color shieldNormalColor = Color.cyan;
    [SerializeField] private Color shieldDamagedColor = Color.blue;
    [SerializeField] private Color vulnerableColor = Color.red;

    [Header("===== 武器系统 =====")]
    [SerializeField] private List<BossWeaponData> weapons = new List<BossWeaponData>();
    [SerializeField] private WeaponMode currentWeaponMode = WeaponMode.State1;
    [SerializeField] private float weaponOrbitRadius = 2f;
    [SerializeField] private float weaponOrbitSpeed = 90f;
    [SerializeField] private float weaponSpacingAngle = 120f;

    [Header("===== 攻击模式设置 =====")]
    [Header("Mode1: 固定方向冲刺")]
    [SerializeField] private float mode1DashSpeed = 15f;
    [SerializeField] private float mode1DashDistance = 8f;
    [SerializeField] private float mode1ReturnSpeed = 8f;
    [SerializeField] private float mode1Cooldown = 3f;

    [Header("Mode2: 并排冲刺")]
    [SerializeField] private float mode2LineWidth = 4f;
    [SerializeField] private float mode2DashSpeed = 12f;
    [SerializeField] private float mode2DashDistance = 10f;
    [SerializeField] private float mode2Cooldown = 4f;

    [Header("Mode3: 等差距离旋转")]
    [SerializeField] private float[] mode3OrbitRadii = { 1.5f, 2.5f, 3.5f };
    [SerializeField] private float mode3RotationSpeed = 180f;
    [SerializeField] private float mode3Duration = 10f;
    [SerializeField] private float mode3Cooldown = 8f;

    [Header("Mode4: 输出窗口")]
    [SerializeField] private float mode4Duration = 5f;
    [SerializeField] private float mode4Cooldown = 15f;

    [Header("===== 玩家检测 =====")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float playerDetectionRange = 20f;
    [SerializeField] private LayerMask playerLayer;

    [Header("===== 状态管理 =====")]
    [SerializeField] private float stateChangeInterval = 30f;
    [SerializeField] private float phaseTransitionTime = 2f;
    private float stateTimer;
    private float weaponModeTimer;
    private bool isInAttackPattern = false;
    private Coroutine currentAttackCoroutine;

    [Header("===== 视觉反馈 =====")]
    [SerializeField] private ParticleSystem shieldHitEffect;
    [SerializeField] private ParticleSystem weaponSpawnEffect;
    [SerializeField] private AudioClip shieldHitSound;
    [SerializeField] private AudioClip weaponAttackSound;
    [SerializeField] private AudioClip phaseChangeSound;

    [Header("===== 追踪系统 =====")]
    [SerializeField] private BossTracker bossTracker;

    [Header("===== 调试 =====")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private Color debugOrbitColor = Color.green;
    [SerializeField] private Color debugAttackColor = Color.red;
    [SerializeField] private bool enableRotationDamage = true; // 是否启用旋转伤害


    public BossState CurrentBossState => currentBossState;

    // 添加一个方法让BossTracker可以获取状态
    public BossState GetCurrentBossState()
    {
        return currentBossState;
    }

    // ================ 补充缺失的方法 ================

    private IEnumerator HandleVulnerablePhase()
    {
        Debug.Log("BOSS进入脆弱阶段！");

        // 盾牌无效
        if (enemyHealth != null)
        {
            enemyHealth.SetShieldActive(false);
        }

        // 武器消失
        SetWeaponMode(WeaponMode.Mode4);

        // 显示脆弱状态
        if (shieldRenderer != null)
            shieldRenderer.color = vulnerableColor;

        // 脆弱持续时间
        yield return new WaitForSeconds(mode4Duration);

        // 恢复盾牌
        if (enemyHealth != null)
        {
            enemyHealth.RestoreShield();
        }

        if (shieldRenderer != null)
            shieldRenderer.color = shieldNormalColor;

        // 回到武器阶段
        yield return TransitionToState(BossState.WeaponPhase);
    }

    private IEnumerator HandleStunnedState()
    {
        Debug.Log("BOSS被眩晕！");

        // 眩晕状态，停止所有动作
        SetWeaponsCollision(false);
        SetWeaponsCanDamage(false);

        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(false);
        }

        // 眩晕持续时间
        yield return new WaitForSeconds(3f);

        // 恢复动作
        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(true);
        }

        // 回到盾牌阶段
        yield return TransitionToState(BossState.ShieldPhase);
    }

    private IEnumerator ExecuteWeaponMode2()
    {
        isInAttackPattern = true;

        // 暂停追踪
        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(false);
        }

        Debug.Log("武器模式2：并排冲刺");

        // 计算并排位置
        Vector3 lineStart = transform.position + Vector3.left * mode2LineWidth;
        Vector3 lineEnd = transform.position + Vector3.right * mode2LineWidth;

        // 将武器排成一条线
        for (int i = 0; i < weapons.Count; i++)
        {
            float t = i / (float)(weapons.Count - 1);
            Vector3 linePosition = Vector3.Lerp(lineStart, lineEnd, t);
            weapons[i].weaponTransform.position = linePosition;
            weapons[i].weaponTransform.rotation = Quaternion.Euler(0, 0, 90);
        }

        // 一起向右侧冲刺
        Vector3 dashTarget = transform.position + Vector3.right * mode2DashDistance;

        foreach (var weapon in weapons)
        {
            StartCoroutine(WeaponLineDash(weapon, dashTarget));
        }

        yield return new WaitForSeconds(mode2Cooldown);

        // 恢复追踪
        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(true);
        }

        isInAttackPattern = false;
    }

    private IEnumerator ExecuteWeaponMode3()
    {
        isInAttackPattern = true;

        // 暂停追踪
        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(false);
        }

        Debug.Log("武器模式3：等差距离旋转");

        SetWeaponMode(WeaponMode.Mode3);
        SetWeaponsCollision(true);
        SetWeaponsCanDamage(true);

        yield return new WaitForSeconds(mode3Duration);

        SetWeaponsCollision(false);
        SetWeaponsCanDamage(false);

        yield return new WaitForSeconds(mode3Cooldown);

        // 恢复追踪
        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(true);
        }

        isInAttackPattern = false;
    }

    private IEnumerator ExecuteWeaponMode4()
    {
        isInAttackPattern = true;

        // 暂停追踪
        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(false);
        }

        Debug.Log("武器模式4：输出窗口");

        SetWeaponMode(WeaponMode.Mode4);
        SetWeaponsVisible(false);
        SetWeaponsCollision(false);
        SetWeaponsCanDamage(false);

        // 进入脆弱阶段
        yield return TransitionToState(BossState.VulnerablePhase);

        yield return new WaitForSeconds(mode4Cooldown);

        SetWeaponsVisible(true);

        // 恢复追踪
        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(true);
        }

        isInAttackPattern = false;
    }

    private IEnumerator WeaponLineDash(BossWeaponData weapon, Vector3 target)
    {
        if (weapon.weaponTransform == null) yield break;

        // 冲刺阶段设置
        weapon.canDamagePlayer = true;
        weapon.damageCooldown = 0.2f;

        Vector3 startPos = weapon.weaponTransform.position;
        float dashTimer = 0f;

        while (dashTimer < 1f)
        {
            dashTimer += Time.deltaTime * mode2DashSpeed;
            weapon.weaponTransform.position = Vector3.Lerp(startPos, target, dashTimer);
            yield return null;
        }

        // 返回原位
        dashTimer = 0f;
        startPos = weapon.weaponTransform.position;

        while (dashTimer < 1f)
        {
            dashTimer += Time.deltaTime * mode2DashSpeed;
            weapon.weaponTransform.position = Vector3.Lerp(startPos, transform.position, dashTimer);
            yield return null;
        }

        // 恢复旋转状态设置
        weapon.damageCooldown = 0.5f;
    }

    private void Start()
    {
        InitializeBoss();
        StartCoroutine(BossAIBehavior());
    }

    private void InitializeBoss()
    {
        // 确保有EnemyHealth组件
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth == null)
        {
            enemyHealth = gameObject.AddComponent<EnemyHealth>();
            enemyHealth.maxHealth = maxHealth;
            enemyHealth.isBoss = true;
            enemyHealth.shieldHealth = 1000f;
        }

        // 确保有EnemyHealthBar组件
        healthBar = GetComponent<EnemyHealthBar>();
        if (healthBar == null)
        {
            healthBar = gameObject.AddComponent<EnemyHealthBar>();
        }

        // 获取或添加BossTracker组件
        bossTracker = GetComponent<BossTracker>();
        if (bossTracker == null)
        {
            bossTracker = gameObject.AddComponent<BossTracker>();
        }

        // 初始化武器
        InitializeWeapons();

        // 设置盾牌视觉
        if (shieldRenderer != null)
            shieldRenderer.color = shieldNormalColor;

        // 订阅健康系统事件
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += OnBossDeath;
            enemyHealth.OnShieldHealthChanged += OnShieldHealthChanged;
        }

        Debug.Log("BOSS初始化完成，使用统一健康系统");
    }

    private void OnShieldHealthChanged(float shieldPercentage)
    {
        // 盾牌血量变化时的视觉反馈
        if (shieldRenderer != null)
        {
            if (shieldPercentage <= 0)
            {
                shieldRenderer.color = vulnerableColor;
            }
            else if (shieldPercentage < 0.3f)
            {
                shieldRenderer.color = Color.Lerp(vulnerableColor, shieldDamagedColor, shieldPercentage / 0.3f);
            }
            else
            {
                shieldRenderer.color = Color.Lerp(shieldDamagedColor, shieldNormalColor, (shieldPercentage - 0.3f) / 0.7f);
            }
        }
    }

    private void InitializeWeapons()
    {
        // 查找现有的武器
        Transform[] weaponTransforms = GetComponentsInChildren<Transform>();
        int weaponIndex = 0;

        foreach (Transform child in weaponTransforms)
        {
            if (child.CompareTag("BossWeapon") && weapons.Count < 3)
            {
                BossWeaponData weaponData = new BossWeaponData
                {
                    weaponName = $"Weapon_{weaponIndex + 1}",
                    weaponID = weaponIndex,
                    weaponTransform = child,
                    attackDamage = 30 + weaponIndex * 10,
                    moveSpeed = 5f + weaponIndex * 1f,
                    rotationSpeed = weaponOrbitSpeed,
                    originalLocalPosition = child.localPosition,
                    originalLocalRotation = child.localRotation,
                    canDamagePlayer = true
                };

                weaponData.weaponRenderer = child.GetComponent<SpriteRenderer>();

                // 使用CircleCollider2D更适合旋转武器
                weaponData.weaponCollider = child.GetComponent<CircleCollider2D>();
                if (weaponData.weaponCollider == null)
                {
                    weaponData.weaponCollider = child.gameObject.AddComponent<CircleCollider2D>();
                }

                weaponData.weaponCollider.isTrigger = true;
                weaponData.weaponCollider.radius = 0.5f; // 设置合适的碰撞半径
                weaponData.weaponCollider.enabled = enableRotationDamage; // 默认启用旋转伤害

                weapons.Add(weaponData);
                weaponIndex++;
            }
        }

        // 如果没有找到武器，创建默认武器
        if (weapons.Count == 0)
        {
            CreateWeapons();
        }

        SetWeaponMode(WeaponMode.State1);
    }

    private void CreateWeapons()
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject weaponObj = new GameObject($"BossWeapon_{i}");
            weaponObj.tag = "BossWeapon";
            weaponObj.layer = LayerMask.NameToLayer("EnemyWeapon"); // 建议创建专门层级
            weaponObj.transform.parent = transform;
            weaponObj.transform.localPosition = Vector3.zero;

            // 添加SpriteRenderer
            SpriteRenderer sr = weaponObj.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("DefaultWeapon"); // 需要替换为实际sprite

            // 添加CircleCollider2D
            CircleCollider2D col = weaponObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.enabled = enableRotationDamage;
            col.radius = 0.5f;

            // 添加武器碰撞检测脚本
            BossWeaponCollision weaponCollision = weaponObj.AddComponent<BossWeaponCollision>();
            weaponCollision.Initialize(this, i);

            BossWeaponData weaponData = new BossWeaponData
            {
                weaponName = $"Weapon_{i + 1}",
                weaponID = i,
                weaponTransform = weaponObj.transform,
                attackDamage = 30 + i * 10,
                moveSpeed = 5f + i * 1f,
                rotationSpeed = weaponOrbitSpeed,
                weaponRenderer = sr,
                weaponCollider = col,
                originalLocalPosition = Vector3.zero,
                originalLocalRotation = Quaternion.identity,
                canDamagePlayer = true
            };

            weapons.Add(weaponData);
        }
    }

    private void Update()
    {
        if (enemyHealth == null || enemyHealth.CurrentHealth <= 0) return;

        // 更新武器状态
        UpdateWeapons();

        // 更新状态计时器
        stateTimer += Time.deltaTime;
        if (stateTimer >= stateChangeInterval)
        {
            CycleBossState();
            stateTimer = 0f;
        }
    }

    private void UpdateWeapons()
    {
        switch (currentWeaponMode)
        {
            case WeaponMode.State1:
                UpdateWeaponOrbit();
                break;
            case WeaponMode.Mode3:
                UpdateWeaponMode3();
                break;
        }
    }

    private void UpdateWeaponOrbit()
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].weaponTransform == null) continue;

            float angle = (Time.time * weaponOrbitSpeed + i * weaponSpacingAngle) * Mathf.Deg2Rad;
            Vector3 orbitPosition = new Vector3(
                Mathf.Cos(angle) * weaponOrbitRadius,
                Mathf.Sin(angle) * weaponOrbitRadius,
                0
            );

            weapons[i].weaponTransform.localPosition = orbitPosition;
            weapons[i].weaponTransform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90);
        }
    }

    private void UpdateWeaponMode3()
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].weaponTransform == null) continue;

            float angle = Time.time * mode3RotationSpeed * Mathf.Deg2Rad;
            Vector3 orbitPosition = new Vector3(
                Mathf.Cos(angle) * mode3OrbitRadii[i],
                Mathf.Sin(angle) * mode3OrbitRadii[i],
                0
            );

            weapons[i].weaponTransform.localPosition = orbitPosition;
            weapons[i].weaponTransform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90);
        }
    }

    // ================ 新增：武器伤害管理 ================

    /// <summary>
    /// 武器尝试对玩家造成伤害（由武器碰撞器调用）
    /// </summary>
    public void WeaponTryDamagePlayer(int weaponID, Collider2D playerCollider)
    {
        if (weaponID < 0 || weaponID >= weapons.Count) return;

        var weapon = weapons[weaponID];

        // 检查冷却时间
        if (Time.time - weapon.lastDamageTime < weapon.damageCooldown) return;

        // 检查是否可以对玩家造成伤害
        if (!weapon.canDamagePlayer) return;

        // 对玩家造成伤害
        PlayerHealth playerHealth = playerCollider.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            Vector3 hitPosition = playerCollider.ClosestPoint(weapon.weaponTransform.position);
            DamagePlayer(playerHealth, weapon.attackDamage * 0.5f, hitPosition); // 旋转状态伤害减半

            // 更新上次伤害时间
            weapon.lastDamageTime = Time.time;

            Debug.Log($"旋转武器 {weapon.weaponName} 对玩家造成 {weapon.attackDamage * 0.5f} 点伤害");
        }
    }

    /// <summary>
    /// 冲刺攻击对玩家造成伤害（冲刺状态伤害更高）
    /// </summary>
    public void WeaponDashDamagePlayer(int weaponID, Collider2D playerCollider)
    {
        if (weaponID < 0 || weaponID >= weapons.Count) return;

        var weapon = weapons[weaponID];

        // 检查冷却时间
        if (Time.time - weapon.lastDamageTime < weapon.damageCooldown) return;

        PlayerHealth playerHealth = playerCollider.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            Vector3 hitPosition = playerCollider.ClosestPoint(weapon.weaponTransform.position);
            DamagePlayer(playerHealth, weapon.attackDamage, hitPosition);

            weapon.lastDamageTime = Time.time;

            Debug.Log($"冲刺武器 {weapon.weaponName} 对玩家造成 {weapon.attackDamage} 点伤害");
        }
    }

    /// <summary>
    /// 通用的玩家伤害方法
    /// </summary>
    private void DamagePlayer(PlayerHealth playerHealth, float damage, Vector3 hitPosition)
    {
        if (playerHealth == null) return;

        try
        {
            // 尝试调用不同的TakeDamage方法签名
            System.Type type = playerHealth.GetType();
            var methodTwoParams = type.GetMethod("TakeDamage", new System.Type[] { typeof(float), typeof(Vector3) });
            var methodOneParam = type.GetMethod("TakeDamage", new System.Type[] { typeof(float) });

            if (methodTwoParams != null)
            {
                methodTwoParams.Invoke(playerHealth, new object[] { damage, hitPosition });
            }
            else if (methodOneParam != null)
            {
                methodOneParam.Invoke(playerHealth, new object[] { damage });
            }
            else
            {
                Debug.LogWarning($"PlayerHealth.TakeDamage方法未找到合适重载，使用默认方式");
                // 尝试直接访问属性或字段
                var healthProperty = type.GetProperty("CurrentHealth");
                if (healthProperty != null)
                {
                    float currentHealth = (float)healthProperty.GetValue(playerHealth);
                    healthProperty.SetValue(playerHealth, currentHealth - damage);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"调用PlayerHealth.TakeDamage失败: {e.Message}");
        }
    }

    // ================ 原有的BOSS状态管理 ================

    private IEnumerator BossAIBehavior()
    {
        while (enemyHealth != null && enemyHealth.CurrentHealth > 0)
        {
            switch (currentBossState)
            {
                case BossState.ShieldPhase:
                    yield return HandleShieldPhase();
                    break;
                case BossState.WeaponPhase:
                    yield return HandleWeaponPhase();
                    break;
                case BossState.VulnerablePhase:
                    yield return HandleVulnerablePhase();
                    break;
                case BossState.Stunned:
                    yield return HandleStunnedState();
                    break;
            }

            yield return null;
        }
    }

    private IEnumerator HandleShieldPhase()
    {
        SetWeaponMode(WeaponMode.State1);

        if (enemyHealth != null && !enemyHealth.IsShieldActive)
        {
            enemyHealth.RestoreShield();
        }

        yield return new WaitForSeconds(10f);

        if (enemyHealth != null && enemyHealth.ShieldPercentage < 0.5f)
        {
            yield return TransitionToState(BossState.WeaponPhase);
        }
    }

    private IEnumerator HandleWeaponPhase()
    {
        int attackPattern = Random.Range(1, 5);

        switch (attackPattern)
        {
            case 1:
                yield return ExecuteWeaponMode1();
                break;
            case 2:
                yield return ExecuteWeaponMode2();
                break;
            case 3:
                yield return ExecuteWeaponMode3();
                break;
            case 4:
                yield return ExecuteWeaponMode4();
                break;
        }

        SetWeaponMode(WeaponMode.State1);
        yield return new WaitForSeconds(2f);

        if (enemyHealth != null && enemyHealth.ShieldPercentage <= 0)
        {
            yield return TransitionToState(BossState.VulnerablePhase);
        }
    }

    private IEnumerator ExecuteWeaponMode1()
    {
        isInAttackPattern = true;

        // 暂停追踪
        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(false);
        }

        Debug.Log("武器模式1：固定方向冲刺");
        Vector3 targetDirection = GetAttackDirection();

        foreach (var weapon in weapons)
        {
            StartCoroutine(WeaponDashAttack(weapon, targetDirection));
            yield return new WaitForSeconds(0.3f);
        }

        yield return new WaitForSeconds(mode1Cooldown);

        // 恢复追踪
        if (bossTracker != null)
        {
            bossTracker.SetTrackingActive(true);
        }

        isInAttackPattern = false;
    }

    private IEnumerator WeaponDashAttack(BossWeaponData weapon, Vector3 direction)
    {
        if (weapon.weaponTransform == null) yield break;

        // 冲刺阶段伤害更高
        weapon.canDamagePlayer = true;
        weapon.damageCooldown = 0.2f; // 冲刺时冷却更短

        float dashTimer = 0f;
        Vector3 startPos = weapon.weaponTransform.position;
        Vector3 targetPos = startPos + direction.normalized * mode1DashDistance;

        while (dashTimer < 1f)
        {
            dashTimer += Time.deltaTime * mode1DashSpeed;
            weapon.weaponTransform.position = Vector3.Lerp(startPos, targetPos, dashTimer);
            yield return null;
        }

        // 返回
        dashTimer = 0f;
        startPos = weapon.weaponTransform.position;
        Vector3 returnPos = transform.position;

        while (dashTimer < 1f)
        {
            dashTimer += Time.deltaTime * mode1ReturnSpeed;
            weapon.weaponTransform.position = Vector3.Lerp(startPos, returnPos, dashTimer);
            yield return null;
        }

        // 恢复旋转状态的设置
        weapon.damageCooldown = 0.5f;
    }

    // ================ 其他原有方法 ================

    private void SetWeaponMode(WeaponMode mode)
    {
        currentWeaponMode = mode;
        weaponModeTimer = 0f;

        switch (mode)
        {
            case WeaponMode.State1:
                SetWeaponsVisible(true);
                SetWeaponsCollision(enableRotationDamage); // 旋转状态启用碰撞
                SetWeaponsCanDamage(true);
                break;
            case WeaponMode.Mode3:
                SetWeaponsVisible(true);
                SetWeaponsCollision(true); // Mode3也启用碰撞
                SetWeaponsCanDamage(true);
                break;
            case WeaponMode.Mode4:
                SetWeaponsVisible(false);
                SetWeaponsCollision(false);
                SetWeaponsCanDamage(false);
                break;
            default:
                SetWeaponsVisible(true);
                SetWeaponsCollision(true);
                SetWeaponsCanDamage(true);
                break;
        }
    }

    private void SetWeaponsCanDamage(bool canDamage)
    {
        foreach (var weapon in weapons)
        {
            weapon.canDamagePlayer = canDamage;
        }
    }

    private void SetWeaponsVisible(bool visible)
    {
        foreach (var weapon in weapons)
        {
            if (weapon.weaponRenderer != null)
                weapon.weaponRenderer.enabled = visible;
        }
    }

    private void SetWeaponsCollision(bool active)
    {
        foreach (var weapon in weapons)
        {
            if (weapon.weaponCollider != null)
            {
                weapon.weaponCollider.enabled = active;
                if (active)
                {
                    Debug.Log($"武器 {weapon.weaponName} 碰撞器已启用");
                }
            }
        }
    }

    private Vector3 GetAttackDirection()
    {
        if (playerTarget != null)
        {
            return (playerTarget.position - transform.position).normalized;
        }

        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle), 0);
    }

    private IEnumerator TransitionToState(BossState newState)
    {
        if (phaseChangeSound != null)
            AudioSource.PlayClipAtPoint(phaseChangeSound, transform.position);

        yield return new WaitForSeconds(phaseTransitionTime);

        currentBossState = newState;
        stateTimer = 0f;

        Debug.Log($"BOSS状态切换为: {newState}");
    }

    private void CycleBossState()
    {
        switch (currentBossState)
        {
            case BossState.ShieldPhase:
                currentBossState = BossState.WeaponPhase;
                break;
            case BossState.WeaponPhase:
                if (enemyHealth != null && enemyHealth.ShieldPercentage <= 0)
                    currentBossState = BossState.VulnerablePhase;
                else
                    currentBossState = BossState.ShieldPhase;
                break;
            case BossState.VulnerablePhase:
                currentBossState = BossState.ShieldPhase;
                break;
            case BossState.Stunned:
                currentBossState = BossState.ShieldPhase;
                break;
        }

        Debug.Log($"BOSS状态循环切换为: {currentBossState}");
    }

    private void OnBossDeath()
    {
        Debug.Log("BOSS已被击败！");

        if (currentAttackCoroutine != null)
            StopCoroutine(currentAttackCoroutine);

        SetWeaponsCollision(false);
        SetWeaponsVisible(false);

        if (bossTracker != null)
        {
            bossTracker.OnBossDeath();
        }

        if (phaseChangeSound != null)
            AudioSource.PlayClipAtPoint(phaseChangeSound, transform.position, 2f);

        Destroy(gameObject, 3f);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugInfo) return;

        Gizmos.color = debugAttackColor;
        Gizmos.DrawWireSphere(transform.position, playerDetectionRange);

        Gizmos.color = debugOrbitColor;
        Gizmos.DrawWireSphere(transform.position, weaponOrbitRadius);

        if (Application.isPlaying)
        {
            foreach (var weapon in weapons)
            {
                if (weapon.weaponTransform != null)
                {
                    Gizmos.color = weapon.weaponCollider.enabled ? Color.red : Color.gray;
                    Gizmos.DrawSphere(weapon.weaponTransform.position, 0.3f);
                }
            }
        }
    }
}