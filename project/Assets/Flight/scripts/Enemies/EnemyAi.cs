using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum EnemyState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Hurt,
    Stunned,
    Dead
}

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("基本设置")]
    public string enemyName = "敌人";
    public float moveSpeed = 3f;
    public float chaseSpeed = 4f;
    public float rotationSpeed = 5f;

    [Header("视觉设置")]
    public float detectionRange = 5f;
    public float attackRange = 1.5f;
    public float fieldOfView = 90f;
    public LayerMask detectionLayers;
    public LayerMask obstacleLayers;

    [Header("巡逻设置")]
    public List<Transform> patrolPoints = new List<Transform>();
    public float waitTimeAtPoint = 2f;
    public float patrolPointReachedDistance = 0.2f;

    [Header("攻击设置")]
    public float attackCooldown = 1f;
    public int attackDamage = 10;
    public float attackWindupTime = 0.3f; // 攻击前摇
    public float attackRecoveryTime = 0.5f; // 攻击后摇
    public bool useWeaponSystem = true; // 是否使用武器系统

    [Header("武器系统")]
    public EnemyWeaponData weaponData;
    public Transform weaponHand;
    private EnemyWeapon currentWeapon;

    [Header("状态")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;
    [SerializeField] private Transform currentTarget;
    private int currentPatrolIndex = 0;
    private float stateTimer = 0f;
    private bool isAttacking = false;
    private bool canAttack = true;
    private float attackCooldownTimer = 0f;
    private float originalMoveSpeed;
    private float originalChaseSpeed;

    [Header("组件引用")]
    private Rigidbody2D rb;
    private EnemyHealth enemyHealth;
    private EnemyAttack enemyAttack;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    [Header("视觉效果")]
    public Color chaseColor = Color.red;
    public Color alertColor = Color.yellow;
    public Color stunColor = Color.blue;
    private Color originalColor;

    [Header("音效")]
    public AudioClip detectionSound;
    public AudioClip attackSound;
    private AudioSource audioSource;

    [Header("调试")]
    public bool showDebugInfo = false;
    public bool drawGizmos = true;

    // 添加初始化标志防止递归
    private bool isInitializing = false;

    void Awake()
    {
        // 防止递归调用
        if (isInitializing) return;
        isInitializing = true;

        InitializeComponents();
        InitializeState();

        // 延迟初始化武器，避免递归
        StartCoroutine(DelayedInitializeWeapon());

        isInitializing = false;
    }

    void InitializeComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyHealth = GetComponent<EnemyHealth>();
        enemyAttack = GetComponent<EnemyAttack>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 获取或添加音效组件
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
        }

        // 配置Rigidbody
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // 保存原始颜色
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        // 保存原始速度
        originalMoveSpeed = moveSpeed;
        originalChaseSpeed = chaseSpeed;

        // 验证必要组件
        if (enemyHealth == null)
        {
            Debug.LogWarning($"{gameObject.name} 缺乏EnemyHealth组件");
        }

        if (enemyAttack == null && !useWeaponSystem)
        {
            Debug.LogWarning($"{gameObject.name} 缺乏EnemyAttack组件，且未启用武器系统");
        }
    }

    void InitializeState()
    {
        if (patrolPoints.Count > 0)
        {
            ChangeState(EnemyState.Patrol);
        }
        else
        {
            ChangeState(EnemyState.Idle);
        }

        // 订阅生命值事件
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += OnDeath;
        }
    }

    IEnumerator DelayedInitializeWeapon()
    {
        // 等待一帧，确保所有组件都已初始化
        yield return null;

        InitializeWeapon();
    }

    void InitializeWeapon()
    {
        if (!useWeaponSystem || weaponData == null || weaponData.weaponPrefab == null)
        {
            return;
        }

        // 创建武器手（如果没有）
        if (weaponHand == null)
        {
            GameObject handObj = new GameObject("WeaponHand");
            handObj.transform.SetParent(transform, false);
            handObj.transform.localPosition = new Vector3(0.5f, 0.1f, 0);
            weaponHand = handObj.transform;
        }

        // 实例化武器
        GameObject weaponObj = Instantiate(weaponData.weaponPrefab,
            weaponHand.position, weaponHand.rotation, weaponHand);

        // 立即设置父对象，避免武器组件的Awake中可能存在的问题
        weaponObj.transform.SetParent(weaponHand, true);

        currentWeapon = weaponObj.GetComponent<EnemyWeapon>();

        if (currentWeapon != null)
        {
            currentWeapon.SetOwner(this);

            // 应用武器数据
            currentWeapon.SetDamage(weaponData.baseDamage);
            currentWeapon.SetAttackRange(weaponData.attackRange);

            // 调整位置和旋转
            weaponObj.transform.localPosition = weaponData.gripOffset;
            weaponObj.transform.localRotation = Quaternion.Euler(0, 0, weaponData.gripRotation);

            if (showDebugInfo)
            {
                Debug.Log($"{enemyName} 装备了 {weaponData.weaponName}");
            }
        }
        else
        {
            Debug.LogError($"武器预制体 {weaponData.weaponPrefab.name} 缺少 EnemyWeapon 组件");
        }
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;

        UpdateStateTimers();
        CheckForPlayer();
        UpdateState();
        UpdateAnimations();
        UpdateDebugInfo();
    }

    void FixedUpdate()
    {
        if (currentState == EnemyState.Dead || currentState == EnemyState.Stunned) return;

        HandleMovement();
    }

    #region 状态管理
    void ChangeState(EnemyState newState)
    {
        // 退出当前状态
        ExitState(currentState);

        // 进入新状态
        EnemyState previousState = currentState;
        currentState = newState;
        stateTimer = 0f;

        // 状态进入逻辑
        switch (newState)
        {
            case EnemyState.Idle:
                rb.linearVelocity = Vector2.zero;
                if (showDebugInfo) Debug.Log($"{enemyName} 进入空闲状态");
                break;

            case EnemyState.Patrol:
                if (patrolPoints.Count > 0)
                {
                    currentPatrolIndex = 0;
                }
                if (showDebugInfo) Debug.Log($"{enemyName} 进入巡逻状态");
                break;

            case EnemyState.Chase:
                UpdateVisualColor(chaseColor);
                if (previousState != EnemyState.Chase && detectionSound != null)
                {
                    audioSource.PlayOneShot(detectionSound);
                }
                if (showDebugInfo) Debug.Log($"{enemyName} 进入追逐状态");
                break;

            case EnemyState.Attack:
                rb.linearVelocity = Vector2.zero;
                if (showDebugInfo) Debug.Log($"{enemyName} 进入攻击状态");
                break;

            case EnemyState.Hurt:
                rb.linearVelocity = Vector2.zero;
                stateTimer = 0.5f; // 受伤硬直时间
                FlashColor(Color.white, 0.1f);
                if (showDebugInfo) Debug.Log($"{enemyName} 进入受伤状态");
                break;

            case EnemyState.Stunned:
                rb.linearVelocity = Vector2.zero;
                UpdateVisualColor(stunColor);
                if (showDebugInfo) Debug.Log($"{enemyName} 进入眩晕状态");
                break;
        }
    }

    void ExitState(EnemyState oldState)
    {
        switch (oldState)
        {
            case EnemyState.Chase:
            case EnemyState.Stunned:
                UpdateVisualColor(originalColor);
                break;
        }
    }

    void UpdateState()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;

            case EnemyState.Patrol:
                UpdatePatrolState();
                break;

            case EnemyState.Chase:
                UpdateChaseState();
                break;

            case EnemyState.Attack:
                UpdateAttackState();
                break;

            case EnemyState.Hurt:
                UpdateHurtState();
                break;

            case EnemyState.Stunned:
                UpdateStunnedState();
                break;
        }
    }

    void UpdateIdleState()
    {
        // 可以添加随机移动或等待
        if (stateTimer > 3f) // 空闲3秒后巡逻
        {
            if (patrolPoints.Count > 0)
            {
                ChangeState(EnemyState.Patrol);
            }
        }

        // 随机看向不同方向
        if (Random.value < 0.01f) // 1%概率
        {
            float randomRotation = Random.Range(-45f, 45f);
            transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + randomRotation);
        }
    }

    void UpdatePatrolState()
    {
        if (patrolPoints.Count == 0)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        Transform targetPoint = patrolPoints[currentPatrolIndex];
        if (targetPoint == null)
        {
            // 巡逻点被销毁，移除它
            patrolPoints.RemoveAt(currentPatrolIndex);
            if (patrolPoints.Count == 0)
            {
                ChangeState(EnemyState.Idle);
            }
            return;
        }

        float distanceToPoint = Vector2.Distance(transform.position, targetPoint.position);

        if (distanceToPoint <= patrolPointReachedDistance)
        {
            // 到达巡逻点，等待
            rb.linearVelocity = Vector2.zero;

            if (stateTimer >= waitTimeAtPoint)
            {
                // 前往下一个巡逻点
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
                stateTimer = 0f;

                if (showDebugInfo)
                {
                    Debug.Log($"{enemyName} 前往下一个巡逻点: {currentPatrolIndex}");
                }
            }
        }
    }

    void UpdateChaseState()
    {
        if (currentTarget == null)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);

        if (distanceToTarget <= attackRange)
        {
            if (canAttack)
            {
                ChangeState(EnemyState.Attack);
            }
            else
            {
                // 在攻击范围内但还在冷却，保持距离
                Vector2 directionAway = (transform.position - currentTarget.position).normalized;
                rb.linearVelocity = directionAway * moveSpeed * 0.5f;
            }
        }
        else if (distanceToTarget > detectionRange * 1.5f)
        {
            // 目标超出追逐范围
            currentTarget = null;
            if (patrolPoints.Count > 0)
            {
                ChangeState(EnemyState.Patrol);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    void UpdateAttackState()
    {
        if (!isAttacking)
        {
            // 开始攻击
            StartCoroutine(AttackRoutine());
        }

        // 检查目标是否仍在攻击范围内
        if (currentTarget != null)
        {
            float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
            if (distanceToTarget > attackRange * 1.2f)
            {
                ChangeState(EnemyState.Chase);
            }
        }
        else
        {
            ChangeState(EnemyState.Idle);
        }
    }

    void UpdateHurtState()
    {
        if (stateTimer <= 0)
        {
            if (currentTarget != null)
            {
                ChangeState(EnemyState.Chase);
            }
            else if (patrolPoints.Count > 0)
            {
                ChangeState(EnemyState.Patrol);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    void UpdateStunnedState()
    {
        if (stateTimer <= 0)
        {
            if (currentTarget != null)
            {
                ChangeState(EnemyState.Chase);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    void UpdateStateTimers()
    {
        if (stateTimer > 0)
        {
            stateTimer -= Time.deltaTime;
        }

        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
            if (attackCooldownTimer <= 0)
            {
                canAttack = true;
            }
        }
    }
    #endregion

    #region 玩家检测
    void CheckForPlayer()
    {
        if (currentTarget != null)
        {
            // 检查当前目标是否仍然有效
            if (!currentTarget.gameObject.activeInHierarchy ||
                Vector2.Distance(transform.position, currentTarget.position) > detectionRange * 2f)
            {
                currentTarget = null;
                if (currentState == EnemyState.Chase || currentState == EnemyState.Attack)
                {
                    ChangeState(patrolPoints.Count > 0 ? EnemyState.Patrol : EnemyState.Idle);
                }
            }
            return;
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);

        foreach (Collider2D collider in colliders)
        {
            if (collider.CompareTag("Player"))
            {
                if (IsTargetInSight(collider.transform))
                {
                    currentTarget = collider.transform;
                    ChangeState(EnemyState.Chase);
                    UpdateVisualColor(alertColor);
                    return;
                }
            }
        }
    }

    bool IsTargetInSight(Transform target)
    {
        if (target == null) return false;

        // 检查距离
        float distance = Vector2.Distance(transform.position, target.position);
        if (distance > detectionRange) return false;

        // 检查是否在视野角度内
        Vector2 directionToTarget = (target.position - transform.position).normalized;
        Vector2 forward = transform.right; // 假设敌人面朝右边

        float angle = Vector2.Angle(forward, directionToTarget);

        if (angle > fieldOfView * 0.5f)
        {
            return false;
        }

        // 检查是否有障碍物遮挡
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            directionToTarget,
            detectionRange,
            obstacleLayers
        );

        if (hit.collider != null && !hit.collider.CompareTag("Player"))
        {
            return false;
        }

        return true;
    }

    public void SetTarget(Transform target)
    {
        if (target == null) return;

        currentTarget = target;
        if (currentState != EnemyState.Hurt && currentState != EnemyState.Stunned && currentState != EnemyState.Dead)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    public void ClearTarget()
    {
        currentTarget = null;
        if (currentState == EnemyState.Chase || currentState == EnemyState.Attack)
        {
            ChangeState(patrolPoints.Count > 0 ? EnemyState.Patrol : EnemyState.Idle);
        }
    }
    #endregion

    #region 移动控制
    void HandleMovement()
    {
        if (currentState == EnemyState.Attack ||
            currentState == EnemyState.Hurt ||
            currentState == EnemyState.Stunned)
        {
            return;
        }

        Vector2 targetVelocity = Vector2.zero;

        switch (currentState)
        {
            case EnemyState.Patrol:
                if (patrolPoints.Count > 0 && currentPatrolIndex < patrolPoints.Count)
                {
                    Transform targetPoint = patrolPoints[currentPatrolIndex];
                    if (targetPoint != null)
                    {
                        targetVelocity = MoveTowards(targetPoint.position, moveSpeed);
                    }
                }
                break;

            case EnemyState.Chase:
                if (currentTarget != null)
                {
                    targetVelocity = MoveTowards(currentTarget.position, chaseSpeed);

                    // 避免与目标碰撞
                    float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
                    if (distanceToTarget < attackRange * 0.8f)
                    {
                        Vector2 directionAway = (transform.position - currentTarget.position).normalized;
                        targetVelocity += directionAway * moveSpeed * 0.3f;
                    }
                }
                break;
        }

        // 应用速度限制
        if (targetVelocity.magnitude > moveSpeed)
        {
            targetVelocity = targetVelocity.normalized * moveSpeed;
        }

        rb.linearVelocity = targetVelocity;

        // 更新面向方向
        if (targetVelocity.magnitude > 0.1f)
        {
            UpdateRotation(targetVelocity);
        }
        else if (currentTarget != null)
        {
            // 即使不移动，也要面向目标
            Vector2 directionToTarget = (currentTarget.position - transform.position).normalized;
            UpdateRotation(directionToTarget);
        }
    }

    Vector2 MoveTowards(Vector3 targetPosition, float speed)
    {
        Vector2 direction = (targetPosition - transform.position).normalized;
        return direction * speed;
    }

    void UpdateRotation(Vector2 movementDirection)
    {
        if (movementDirection.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
    #endregion

    #region 攻击系统
    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        canAttack = false;

        if (showDebugInfo)
        {
            Debug.Log($"{enemyName} 开始攻击!");
        }

        // 1. 攻击前摇（面向目标）
        if (currentTarget != null)
        {
            Vector2 direction = (currentTarget.position - transform.position).normalized;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 快速转向目标
            float rotationTime = attackWindupTime * 0.5f;
            float elapsed = 0f;
            Quaternion startRotation = transform.rotation;
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);

            while (elapsed < rotationTime)
            {
                elapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsed / rotationTime);
                yield return null;
            }
        }

        // 2. 执行攻击
        if (useWeaponSystem && currentWeapon != null)
        {
            // 使用武器攻击
            currentWeapon.StartAttack();

            // 等待武器攻击完成
            yield return new WaitForSeconds(weaponData.attackWindupTime +
                                          weaponData.attackSwingTime);
        }
        else if (enemyAttack != null)
        {
            // 使用旧的 EnemyAttack 系统
            enemyAttack.ExecuteAttack(currentTarget, attackDamage);

            // 播放攻击音效
            if (attackSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(attackSound);
            }

            // 等待攻击动画
            yield return new WaitForSeconds(attackWindupTime);
        }
        else
        {
            // 没有攻击系统，直接造成伤害
            if (currentTarget != null)
            {
                PlayerHealth playerHealth = currentTarget.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage, transform.position);
                }
            }
            yield return new WaitForSeconds(attackWindupTime);
        }

        // 3. 攻击后摇
        yield return new WaitForSeconds(attackRecoveryTime);

        // 4. 开始冷却
        attackCooldownTimer = attackCooldown;

        isAttacking = false;

        // 5. 返回适当状态
        if (currentTarget != null)
        {
            float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
            if (distanceToTarget > attackRange)
            {
                ChangeState(EnemyState.Chase);
            }
            else if (canAttack)
            {
                // 如果仍在范围内并且可以攻击，继续攻击
                ChangeState(EnemyState.Attack);
            }
        }
        else
        {
            ChangeState(patrolPoints.Count > 0 ? EnemyState.Patrol : EnemyState.Idle);
        }
    }

    public void OnAttackBlocked()
    {
        if (showDebugInfo)
        {
            Debug.Log($"{enemyName} 的攻击被格挡！");
        }

        // 攻击被格挡时的反应
        if (currentState == EnemyState.Attack)
        {
            rb.linearVelocity = Vector2.zero;

            // 轻微后撤
            if (currentTarget != null)
            {
                Vector2 knockbackDirection = (transform.position - currentTarget.position).normalized;
                rb.AddForce(knockbackDirection * 3f, ForceMode2D.Impulse);
            }

            // 短暂硬直
            StartCoroutine(BlockStunRoutine());
        }
    }

    IEnumerator BlockStunRoutine()
    {
        ChangeState(EnemyState.Hurt);
        yield return new WaitForSeconds(0.3f);

        if (currentTarget != null)
        {
            ChangeState(EnemyState.Chase);
        }
    }
    #endregion

    #region 受伤与状态效果
    public void OnHit(Vector2 hitDirection, float knockbackForce = 5f)
    {
        // 应用击退
        if (rb != null)
        {
            rb.AddForce(hitDirection * knockbackForce, ForceMode2D.Impulse);
        }

        // 进入受伤状态
        if (currentState != EnemyState.Dead && currentState != EnemyState.Stunned)
        {
            ChangeState(EnemyState.Hurt);
        }
    }

    public void Stun(float duration)
    {
        if (currentState == EnemyState.Dead) return;

        ChangeState(EnemyState.Stunned);
        stateTimer = duration;

        if (showDebugInfo)
        {
            Debug.Log($"{enemyName} 被眩晕 {duration} 秒");
        }
    }

    public void Slow(float slowPercent, float duration)
    {
        if (currentState == EnemyState.Dead) return;

        StartCoroutine(SlowRoutine(slowPercent, duration));
    }

    IEnumerator SlowRoutine(float slowPercent, float duration)
    {
        float originalMove = moveSpeed;
        float originalChase = chaseSpeed;

        moveSpeed = originalMove * (1f - slowPercent);
        chaseSpeed = originalChase * (1f - slowPercent);

        if (showDebugInfo)
        {
            Debug.Log($"{enemyName} 被减速 {slowPercent:P0}，持续 {duration} 秒");
        }

        yield return new WaitForSeconds(duration);

        moveSpeed = originalMove;
        chaseSpeed = originalChase;

        if (showDebugInfo)
        {
            Debug.Log($"{enemyName} 减速效果结束");
        }
    }

    public void ApplyFear(float duration)
    {
        StartCoroutine(FearRoutine(duration));
    }

    IEnumerator FearRoutine(float duration)
    {
        if (currentTarget != null)
        {
            Transform fearedTarget = currentTarget;
            ClearTarget();

            // 逃离目标
            float fearTimer = 0f;
            while (fearTimer < duration && currentState != EnemyState.Dead)
            {
                if (fearedTarget != null)
                {
                    Vector2 fleeDirection = (transform.position - fearedTarget.position).normalized;
                    rb.linearVelocity = fleeDirection * chaseSpeed;
                    UpdateRotation(fleeDirection);
                }

                fearTimer += Time.deltaTime;
                yield return null;
            }

            // 恢复巡逻
            if (currentState != EnemyState.Dead)
            {
                ChangeState(patrolPoints.Count > 0 ? EnemyState.Patrol : EnemyState.Idle);
            }
        }
    }

    void OnDeath()
    {
        ChangeState(EnemyState.Dead);

        // 禁用所有组件
        if (rb != null) rb.simulated = false;
        if (enemyAttack != null) enemyAttack.enabled = false;

        // 禁用武器
        if (currentWeapon != null)
        {
            currentWeapon.StopAttack();
            currentWeapon.enabled = false;
        }

        // 禁用碰撞体
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;

        // 停止所有协程
        StopAllCoroutines();

        // 禁用AI
        enabled = false;

        if (showDebugInfo)
        {
            Debug.Log($"{enemyName} AI已禁用");
        }
    }
    #endregion

    #region 武器系统
    public EnemyWeapon GetCurrentWeapon()
    {
        return currentWeapon;
    }

    public void EquipWeapon(EnemyWeaponData newWeaponData)
    {
        if (newWeaponData == null || newWeaponData.weaponPrefab == null) return;

        // 移除旧武器
        if (currentWeapon != null)
        {
            Destroy(currentWeapon.gameObject);
            currentWeapon = null;
        }

        weaponData = newWeaponData;

        // 初始化新武器
        if (weaponHand == null)
        {
            GameObject handObj = new GameObject("WeaponHand");
            handObj.transform.SetParent(transform, false);
            handObj.transform.localPosition = new Vector3(0.5f, 0.1f, 0);
            weaponHand = handObj.transform;
        }

        GameObject weaponObj = Instantiate(weaponData.weaponPrefab,
            weaponHand.position, weaponHand.rotation, weaponHand);
        currentWeapon = weaponObj.GetComponent<EnemyWeapon>();

        if (currentWeapon != null)
        {
            currentWeapon.SetOwner(this);
            currentWeapon.SetDamage(weaponData.baseDamage);
            currentWeapon.SetAttackRange(weaponData.attackRange);

            weaponObj.transform.localPosition = weaponData.gripOffset;
            weaponObj.transform.localRotation = Quaternion.Euler(0, 0, weaponData.gripRotation);

            if (showDebugInfo)
            {
                Debug.Log($"{enemyName} 装备了新武器: {weaponData.weaponName}");
            }
        }
    }

    public void DropWeapon()
    {
        if (currentWeapon != null)
        {
            // 解除父子关系
            currentWeapon.transform.parent = null;

            // 添加物理效果
            Rigidbody2D weaponRb = currentWeapon.gameObject.AddComponent<Rigidbody2D>();
            weaponRb.gravityScale = 1f;

            // 随机弹跳
            Vector2 randomForce = new Vector2(Random.Range(-2f, 2f), Random.Range(3f, 6f));
            weaponRb.AddForce(randomForce, ForceMode2D.Impulse);

            // 旋转
            weaponRb.AddTorque(Random.Range(-50f, 50f));

            // 设置可拾取标签
            currentWeapon.gameObject.tag = "DroppedWeapon";

            currentWeapon = null;
            weaponData = null;
        }
    }
    #endregion

    #region 动画与视觉效果
    void UpdateAnimations()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", rb.linearVelocity.magnitude);
        animator.SetBool("IsAttacking", isAttacking);
        animator.SetBool("IsChasing", currentState == EnemyState.Chase);
        animator.SetBool("IsHurt", currentState == EnemyState.Hurt);
        animator.SetBool("IsStunned", currentState == EnemyState.Stunned);
        animator.SetBool("IsDead", currentState == EnemyState.Dead);
    }

    void UpdateVisualColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    public void FlashColor(Color flashColor, float duration = 0.1f)
    {
        StartCoroutine(FlashColorRoutine(flashColor, duration));
    }

    IEnumerator FlashColorRoutine(Color flashColor, float duration)
    {
        if (spriteRenderer == null) yield break;

        Color original = spriteRenderer.color;
        spriteRenderer.color = flashColor;

        yield return new WaitForSeconds(duration);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = original;
        }
    }
    #endregion

    #region 调试信息
    void UpdateDebugInfo()
    {
        if (!showDebugInfo) return;

        if (Time.frameCount % 60 == 0) // 每秒更新一次
        {
            Debug.Log($"[{enemyName}] 状态: {currentState}, 目标: {(currentTarget != null ? currentTarget.name : "无")}, 速度: {rb.linearVelocity.magnitude:F2}");
        }
    }
    #endregion

    #region 公共方法
    public EnemyState GetCurrentState()
    {
        return currentState;
    }

    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }

    public bool IsAlive()
    {
        return currentState != EnemyState.Dead;
    }

    public void AddPatrolPoint(Transform point)
    {
        if (!patrolPoints.Contains(point))
        {
            patrolPoints.Add(point);
        }
    }

    public void RemovePatrolPoint(Transform point)
    {
        if (patrolPoints.Contains(point))
        {
            patrolPoints.Remove(point);
        }
    }

    public void ClearPatrolPoints()
    {
        patrolPoints.Clear();
    }

    public void SetMovementSpeed(float newSpeed, float newChaseSpeed = -1)
    {
        moveSpeed = newSpeed;
        if (newChaseSpeed >= 0)
        {
            chaseSpeed = newChaseSpeed;
        }
    }

    public void ResetMovementSpeed()
    {
        moveSpeed = originalMoveSpeed;
        chaseSpeed = originalChaseSpeed;
    }
    #endregion

    #region 编辑器工具
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // 绘制检测范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 绘制攻击范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 绘制视野角度
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        float halfFOV = fieldOfView * 0.5f;

        Quaternion leftRayRotation = Quaternion.AngleAxis(-halfFOV, Vector3.forward);
        Quaternion rightRayRotation = Quaternion.AngleAxis(halfFOV, Vector3.forward);

        Vector3 leftRayDirection = leftRayRotation * transform.right;
        Vector3 rightRayDirection = rightRayRotation * transform.right;

        Gizmos.DrawRay(transform.position, leftRayDirection * detectionRange);
        Gizmos.DrawRay(transform.position, rightRayDirection * detectionRange);

        // 绘制扇形区域
        DrawAngleSector(transform.position, transform.right, fieldOfView, detectionRange);

        // 绘制当前目标
        if (currentTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }

        // 绘制巡逻路径
        Gizmos.color = Color.blue;
        for (int i = 0; i < patrolPoints.Count; i++)
        {
            if (patrolPoints[i] != null)
            {
                Gizmos.DrawWireSphere(patrolPoints[i].position, 0.2f);
                if (i > 0 && patrolPoints[i - 1] != null)
                {
                    Gizmos.DrawLine(patrolPoints[i - 1].position, patrolPoints[i].position);
                }
            }
        }

        // 连接最后一个点和第一个点形成环路
        if (patrolPoints.Count > 1 && patrolPoints[0] != null && patrolPoints[patrolPoints.Count - 1] != null)
        {
            Gizmos.DrawLine(patrolPoints[patrolPoints.Count - 1].position, patrolPoints[0].position);
        }
    }

    void DrawAngleSector(Vector3 center, Vector3 direction, float angle, float radius)
    {
        int segments = 20;
        float step = angle / segments;
        float halfAngle = angle * 0.5f;

        Vector3 prevPoint = center + Quaternion.Euler(0, 0, -halfAngle) * direction * radius;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + step * i;
            Vector3 currentPoint = center + Quaternion.Euler(0, 0, currentAngle) * direction * radius;

            Gizmos.DrawLine(center, currentPoint);
            Gizmos.DrawLine(prevPoint, currentPoint);

            prevPoint = currentPoint;
        }
    }

    void OnValidate()
    {
        // 确保检测范围不小于攻击范围
        if (detectionRange < attackRange)
        {
            detectionRange = attackRange + 1f;
        }

        // 确保追逐速度不小于移动速度
        if (chaseSpeed < moveSpeed)
        {
            chaseSpeed = moveSpeed * 1.5f;
        }
    }
    #endregion

    void OnDestroy()
    {
        // 清理事件订阅
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= OnDeath;
        }

        // 停止所有协程
        StopAllCoroutines();
    }
}