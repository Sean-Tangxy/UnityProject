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

    [Header("检测设置（改成：只要在范围内就追）")]
    public float detectionRange = 5f;
    public float attackRange = 1.5f;
    public LayerMask detectionLayers;   // ✅ 必须包含玩家所在 Layer

    [Header("（可留空）巡逻设置：现在不用也能追")]
    public List<Transform> patrolPoints = new List<Transform>();
    public float waitTimeAtPoint = 2f;
    public float patrolPointReachedDistance = 0.2f;

    [Header("攻击设置")]
    public float attackCooldown = 1f;
    public int attackDamage = 10;
    public float attackWindupTime = 0.3f;
    public float attackRecoveryTime = 0.5f;
    public bool useWeaponSystem = true;

    [Header("武器系统")]
    public EnemyWeaponData weaponData;
    public Transform weaponHand;
    private EnemyWeapon currentWeapon;

    // ========================= 盾牌/脆弱窗口（可选） =========================
    [Header("盾牌/脆弱窗口（可选）")]
    public bool hasShield = false;
    public GameObject shieldVisual;
    public float shieldUpDuration = 4f;
    public float shieldDownDuration = 1.2f;
    public float vulnerableDamageMultiplier = 1.5f;
    public bool dropShieldOnHitDuringDownWindow = true;
    public float extraVulnerableTimeOnDrop = 0.5f;

    [Header("放下盾时闪烁")]
    public float shieldBlinkFrequency = 6f;
    [Range(0f, 1f)]
    public float shieldBlinkMinAlpha = 0.25f;

    private bool shieldUp = true;
    private bool shieldDropped = false;
    private float shieldCycleTimer = 0f;
    private float vulnerableTimer = 0f;
    private Coroutine shieldBlinkCo;

    // ========================= 状态 =========================
    [Header("状态")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;
    [SerializeField] private Transform currentTarget;

    [SerializeField] private float stateTimer = 0f;

    private bool isAttacking = false;
    private bool canAttack = true;
    private float attackCooldownTimer = 0f;

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

    private bool isInitializing = false;

    void Awake()
    {
        if (isInitializing) return;
        isInitializing = true;

        InitializeComponents();
        InitializeState();
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

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
        }

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        if (enemyHealth != null)
            enemyHealth.OnDeath += OnDeath;
    }

    // ✅ 改成：初始就 Idle，等待检测玩家
    void InitializeState()
    {
        ChangeState(EnemyState.Idle);
    }

    IEnumerator DelayedInitializeWeapon()
    {
        yield return null;
        InitializeWeapon();
    }

    void InitializeWeapon()
    {
        if (!useWeaponSystem || weaponData == null || weaponData.weaponPrefab == null)
            return;

        if (weaponHand == null)
        {
            GameObject handObj = new GameObject("WeaponHand");
            handObj.transform.SetParent(transform, false);
            handObj.transform.localPosition = new Vector3(0.5f, 0.1f, 0);
            weaponHand = handObj.transform;
        }

        GameObject weaponObj = Instantiate(weaponData.weaponPrefab, weaponHand.position, weaponHand.rotation, weaponHand);
        weaponObj.transform.SetParent(weaponHand, true);

        currentWeapon = weaponObj.GetComponent<EnemyWeapon>();
        if (currentWeapon != null)
        {
            currentWeapon.SetOwner(this);
            currentWeapon.SetDamage(weaponData.baseDamage);
            currentWeapon.SetAttackRange(weaponData.attackRange);

            weaponObj.transform.localPosition = weaponData.gripOffset;
            weaponObj.transform.localRotation = Quaternion.Euler(0, 0, weaponData.gripRotation);
        }
        else
        {
            Debug.LogError($"武器预制体 {weaponData.weaponPrefab.name} 缺少 EnemyWeapon 组件");
        }
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;

        UpdateTimers();
        UpdateShieldBehavior();

        // ✅ 每帧检测玩家：进入范围就追
        CheckForPlayerSimple();

        UpdateState();
        UpdateAnimations();
    }

    void FixedUpdate()
    {
        if (currentState == EnemyState.Dead || currentState == EnemyState.Stunned) return;
        HandleMovement();
    }

    // ========================= ✅ 检测玩家（简化版） =========================
    void CheckForPlayerSimple()
    {
        // 如果已有目标，超出范围就丢失目标回 Idle
        if (currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.position);
            if (!currentTarget.gameObject.activeInHierarchy || dist > detectionRange * 1.5f)
            {
                currentTarget = null;
                ChangeState(EnemyState.Idle);
            }
            return;
        }

        // 在范围内找玩家（LayerMask + Tag 双保险）
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);
        for (int i = 0; i < cols.Length; i++)
        {
            if (!cols[i].CompareTag("Player")) continue;

            currentTarget = cols[i].transform;
            ChangeState(EnemyState.Chase);
            UpdateVisualColor(alertColor);

            if (detectionSound != null && audioSource != null)
                audioSource.PlayOneShot(detectionSound);

            return;
        }
    }

    // ========================= 外部依赖接口（盾牌/受伤） =========================
    public void OnAttackBlocked()
    {
        if (currentState == EnemyState.Dead || currentState == EnemyState.Stunned) return;

        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (currentTarget != null && rb != null)
        {
            Vector2 knockDir = (transform.position - currentTarget.position).normalized;
            rb.AddForce(knockDir * 3f, ForceMode2D.Impulse);
        }

        ChangeState(EnemyState.Hurt);
        stateTimer = Mathf.Max(stateTimer, 0.25f);
    }

    public void Stun(float duration)
    {
        if (currentState == EnemyState.Dead) return;

        ChangeState(EnemyState.Stunned);
        stateTimer = Mathf.Max(duration, 0.01f);

        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    public void NotifyDamagedByPlayer()
    {
        if (!hasShield || shieldDropped) return;

        bool inDownWindow = (!shieldUp) && (vulnerableTimer > 0f);
        if (inDownWindow && dropShieldOnHitDuringDownWindow)
        {
            DropShield();
        }
    }

    public float GetIncomingDamageMultiplier()
    {
        if (!hasShield) return 1f;
        if (shieldDropped) return Mathf.Max(1f, vulnerableDamageMultiplier);
        if (!shieldUp && vulnerableTimer > 0f) return Mathf.Max(1f, vulnerableDamageMultiplier);
        return 1f;
    }

    // ========================= 计时器 =========================
    void UpdateTimers()
    {
        if (stateTimer > 0f) stateTimer -= Time.deltaTime;

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
            if (attackCooldownTimer <= 0f) canAttack = true;
        }
    }

    // ========================= 盾牌周期/闪烁 =========================
    void UpdateShieldBehavior()
    {
        if (!hasShield || shieldDropped) return;
        if (currentState == EnemyState.Dead) return;

        shieldCycleTimer += Time.deltaTime;

        if (shieldUp)
        {
            if (shieldCycleTimer >= Mathf.Max(0.01f, shieldUpDuration))
                ForceShieldDown(Mathf.Max(0.01f, shieldDownDuration));
        }
        else
        {
            if (vulnerableTimer > 0f)
                vulnerableTimer -= Time.deltaTime;

            if (shieldCycleTimer >= Mathf.Max(0.01f, shieldDownDuration))
                RaiseShield();
        }
    }

    void ForceShieldDown(float duration)
    {
        shieldUp = false;
        shieldCycleTimer = 0f;
        vulnerableTimer = duration;
        StartShieldBlink();
    }

    void RaiseShield()
    {
        if (shieldDropped) return;
        shieldUp = true;
        shieldCycleTimer = 0f;
        vulnerableTimer = 0f;
        StopShieldBlink(true);
    }

    void DropShield()
    {
        shieldDropped = true;
        shieldUp = false;

        if (extraVulnerableTimeOnDrop > 0f)
            vulnerableTimer = Mathf.Max(vulnerableTimer, extraVulnerableTimeOnDrop);

        StopShieldBlink(true);

        if (shieldVisual != null)
            shieldVisual.SetActive(false);
    }

    void StartShieldBlink()
    {
        if (shieldVisual == null) return;

        if (shieldBlinkCo != null) StopCoroutine(shieldBlinkCo);
        shieldBlinkCo = StartCoroutine(ShieldBlinkRoutine());
    }

    void StopShieldBlink(bool restoreAlpha)
    {
        if (shieldBlinkCo != null)
        {
            StopCoroutine(shieldBlinkCo);
            shieldBlinkCo = null;
        }

        if (restoreAlpha && shieldVisual != null)
            SetShieldAlpha(1f);
    }

    IEnumerator ShieldBlinkRoutine()
    {
        float freq = Mathf.Max(0.1f, shieldBlinkFrequency);
        float minA = Mathf.Clamp01(shieldBlinkMinAlpha);

        while (!shieldUp && !shieldDropped && currentState != EnemyState.Dead)
        {
            float t = Mathf.PingPong(Time.time * freq, 1f);
            float a = Mathf.Lerp(minA, 1f, t);
            SetShieldAlpha(a);
            yield return null;
        }

        SetShieldAlpha(1f);
        shieldBlinkCo = null;
    }

    void SetShieldAlpha(float a)
    {
        if (shieldVisual == null) return;
        var srs = shieldVisual.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            var c = srs[i].color;
            c.a = a;
            srs[i].color = c;
        }
    }

    // ========================= 状态机 =========================
    void ChangeState(EnemyState newState)
    {
        ExitState(currentState);
        currentState = newState;

        switch (newState)
        {
            case EnemyState.Idle:
                if (rb != null) rb.linearVelocity = Vector2.zero;
                break;

            case EnemyState.Chase:
                UpdateVisualColor(chaseColor);
                break;

            case EnemyState.Attack:
                if (rb != null) rb.linearVelocity = Vector2.zero;
                break;

            case EnemyState.Hurt:
                if (rb != null) rb.linearVelocity = Vector2.zero;
                stateTimer = 0.5f;
                FlashColor(Color.white, 0.08f);
                break;

            case EnemyState.Stunned:
                if (rb != null) rb.linearVelocity = Vector2.zero;
                UpdateVisualColor(stunColor);
                break;

            case EnemyState.Dead:
                if (rb != null) rb.linearVelocity = Vector2.zero;
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
            case EnemyState.Idle: UpdateIdleState(); break;
            case EnemyState.Chase: UpdateChaseState(); break;
            case EnemyState.Attack: UpdateAttackState(); break;
            case EnemyState.Hurt: UpdateHurtState(); break;
            case EnemyState.Stunned: UpdateStunnedState(); break;
        }
    }

    void UpdateIdleState()
    {
        // ✅ Idle 不再切 Patrol，只等待检测玩家
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void UpdateChaseState()
    {
        if (currentTarget == null)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        float dist = Vector2.Distance(transform.position, currentTarget.position);

        if (dist <= attackRange)
        {
            if (canAttack && !isAttacking)
                ChangeState(EnemyState.Attack);
        }
        else if (dist > detectionRange * 1.5f)
        {
            currentTarget = null;
            ChangeState(EnemyState.Idle);
        }
    }

    void UpdateAttackState()
    {
        if (!isAttacking)
            StartCoroutine(AttackRoutine());

        if (currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.position);
            if (dist > attackRange * 1.2f)
                ChangeState(EnemyState.Chase);
        }
        else
        {
            ChangeState(EnemyState.Idle);
        }
    }

    void UpdateHurtState()
    {
        if (stateTimer <= 0f)
            ChangeState(currentTarget != null ? EnemyState.Chase : EnemyState.Idle);
    }

    void UpdateStunnedState()
    {
        if (stateTimer <= 0f)
        {
            UpdateVisualColor(originalColor);
            ChangeState(currentTarget != null ? EnemyState.Chase : EnemyState.Idle);
        }
    }

    // ========================= 移动 =========================
    void HandleMovement()
    {
        if (currentState == EnemyState.Attack || currentState == EnemyState.Hurt || currentState == EnemyState.Stunned)
            return;

        Vector2 v = Vector2.zero;

        if (currentState == EnemyState.Chase && currentTarget != null)
        {
            v = ((Vector2)(currentTarget.position - transform.position)).normalized * chaseSpeed;
        }

        if (rb != null) rb.linearVelocity = v;

        if (v.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            Quaternion targetRot = Quaternion.Euler(0, 0, targetAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    // ========================= 攻击 =========================
    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        canAttack = false;

        // 前摇：面向目标
        if (currentTarget != null)
        {
            Vector2 dir = (currentTarget.position - transform.position).normalized;
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion startRot = transform.rotation;
            Quaternion targetRot = Quaternion.Euler(0, 0, targetAngle);

            float rotTime = Mathf.Max(0.01f, attackWindupTime * 0.5f);
            float t = 0f;
            while (t < rotTime)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t / rotTime);
                yield return null;
            }
        }

        // 执行攻击
        if (useWeaponSystem && currentWeapon != null && weaponData != null)
        {
            currentWeapon.StartAttack();
            yield return new WaitForSeconds(weaponData.attackWindupTime + weaponData.attackSwingTime);
        }
        else if (enemyAttack != null)
        {
            enemyAttack.ExecuteAttack(currentTarget, attackDamage);

            if (attackSound != null && audioSource != null)
                audioSource.PlayOneShot(attackSound);

            yield return new WaitForSeconds(Mathf.Max(0.01f, attackWindupTime));
        }
        else
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, attackWindupTime));
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, attackRecoveryTime));

        attackCooldownTimer = Mathf.Max(0.01f, attackCooldown);
        isAttacking = false;

        ChangeState(currentTarget != null ? EnemyState.Chase : EnemyState.Idle);
    }

    // ========================= 死亡 =========================
    void OnDeath()
    {
        ChangeState(EnemyState.Dead);
        StopShieldBlink(true);

        if (rb != null) rb.simulated = false;

        StopAllCoroutines();
        enabled = false;
    }

    // ========================= 动画/颜色 =========================
    void UpdateAnimations()
    {
        if (animator == null || rb == null) return;

        animator.SetFloat("Speed", rb.linearVelocity.magnitude);
        animator.SetBool("IsAttacking", isAttacking);
        animator.SetBool("IsChasing", currentState == EnemyState.Chase);
        animator.SetBool("IsHurt", currentState == EnemyState.Hurt);
        animator.SetBool("IsStunned", currentState == EnemyState.Stunned);
        animator.SetBool("IsDead", currentState == EnemyState.Dead);
    }

    void UpdateVisualColor(Color c)
    {
        if (spriteRenderer != null)
            spriteRenderer.color = c;
    }

    public void FlashColor(Color flashColor, float duration = 0.1f)
    {
        StartCoroutine(FlashColorRoutine(flashColor, duration));
    }

    IEnumerator FlashColorRoutine(Color flashColor, float duration)
    {
        if (spriteRenderer == null) yield break;

        Color old = spriteRenderer.color;
        spriteRenderer.color = flashColor;

        yield return new WaitForSeconds(duration);

        if (spriteRenderer != null)
            spriteRenderer.color = old;
    }

    void OnValidate()
    {
        if (detectionRange < attackRange) detectionRange = attackRange + 1f;
        if (chaseSpeed < moveSpeed) chaseSpeed = moveSpeed * 1.5f;

        shieldUpDuration = Mathf.Max(0.01f, shieldUpDuration);
        shieldDownDuration = Mathf.Max(0.01f, shieldDownDuration);
        vulnerableDamageMultiplier = Mathf.Max(1f, vulnerableDamageMultiplier);
        shieldBlinkFrequency = Mathf.Max(0.1f, shieldBlinkFrequency);
        shieldBlinkMinAlpha = Mathf.Clamp01(shieldBlinkMinAlpha);
    }

    void OnDestroy()
    {
        if (enemyHealth != null)
            enemyHealth.OnDeath -= OnDeath;

        StopAllCoroutines();
    }

    public EnemyState GetCurrentState() => currentState;
    public Transform GetCurrentTarget() => currentTarget;
    public bool IsAlive() => currentState != EnemyState.Dead;
}
