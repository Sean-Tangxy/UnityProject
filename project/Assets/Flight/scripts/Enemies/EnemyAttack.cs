using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class EnemyAttack : MonoBehaviour
{
    [Header("攻击设置")]
    public float attackDamage = 10f;
    public float attackKnockback = 5f;
    public Vector2 attackOffset = new Vector2(0.5f, 0);
    public Vector2 attackSize = new Vector2(1f, 0.5f);

    [Header("击退控制（新增/修复）")]
    public bool enableKnockback = true;
    public bool knockbackWhenBlocking = false;

    [Tooltip("敌人本体物理关闭（Collider disabled 或 Rigidbody2D.simulated=false）时，禁止对玩家施加击退")]
    public bool disableKnockbackWhenEnemyPhysicsOff = true;

    [Header("攻击效果")]
    public LayerMask attackLayers;
    public GameObject attackEffectPrefab;
    public AudioClip attackSound;
    public Color attackFlashColor = Color.white;
    public float attackFlashDuration = 0.1f;

    [Header("攻击动画")]
    public float attackDuration = 0.5f;
    public AnimationCurve attackCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("组件引用")]
    private Collider2D attackCollider;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;

    // ✅ 改为：从父级拿到 EnemyAI/本体刚体/本体碰撞体
    private EnemyAI enemyAI;
    private Rigidbody2D enemyRootRb;
    private Collider2D enemyRootCol;

    [Header("状态")]
    private bool isAttacking = false;
    private bool canDamage = false;
    private float originalColliderSize;

    void Awake()
    {
        InitializeComponents();
    }

    void InitializeComponents()
    {
        attackCollider = GetComponent<Collider2D>();
        if (attackCollider == null)
            attackCollider = gameObject.AddComponent<BoxCollider2D>();

        if (attackCollider is BoxCollider2D boxCollider)
        {
            boxCollider.size = attackSize;
            boxCollider.offset = attackOffset;
            boxCollider.isTrigger = true;
            boxCollider.enabled = false;
            originalColliderSize = boxCollider.size.x;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();

        // ✅ 关键：拿父级（本体）
        enemyAI = GetComponentInParent<EnemyAI>();
        enemyRootRb = enemyAI != null ? enemyAI.GetComponent<Rigidbody2D>() : GetComponentInParent<Rigidbody2D>();
        enemyRootCol = enemyAI != null ? enemyAI.GetComponent<Collider2D>() : GetComponentInParent<Collider2D>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
        }
    }

    #region 攻击执行
    public void ExecuteAttack(Transform target, float damage = 0)
    {
        if (isAttacking) return;

        float actualDamage = damage > 0 ? damage : attackDamage;
        StartCoroutine(AttackRoutine(target, actualDamage));
    }

    IEnumerator AttackRoutine(Transform target, float damage)
    {
        isAttacking = true;
        canDamage = false;

        if (target != null)
        {
            Vector2 direction = (target.position - transform.position).normalized;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            float rotationTime = 0.1f;
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

        yield return StartCoroutine(AttackAnimation());

        canDamage = false;
        if (attackCollider != null) attackCollider.enabled = false;

        isAttacking = false;
    }

    IEnumerator AttackAnimation()
    {
        if (attackSound != null && audioSource != null)
            audioSource.PlayOneShot(attackSound);

        if (spriteRenderer != null)
            StartCoroutine(FlashSprite(attackFlashColor, attackFlashDuration));

        canDamage = true;
        if (attackCollider != null) attackCollider.enabled = true;

        if (spriteRenderer != null)
            yield return StartCoroutine(SwingAnimation());
        else
            yield return new WaitForSeconds(attackDuration * 0.3f);

        canDamage = false;
        if (attackCollider != null) attackCollider.enabled = false;
    }

    IEnumerator SwingAnimation()
    {
        float elapsed = 0f;
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0, 0, 90f);

        while (elapsed < attackDuration)
        {
            elapsed += Time.deltaTime;
            float t = attackCurve.Evaluate(elapsed / attackDuration);
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        elapsed = 0f;
        float returnDuration = attackDuration * 0.2f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(endRot, startRot, elapsed / returnDuration);
            yield return null;
        }

        transform.localRotation = startRot;
    }

    IEnumerator FlashSprite(Color flashColor, float duration)
    {
        if (spriteRenderer == null) yield break;
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(duration);
        spriteRenderer.color = originalColor;
    }
    #endregion

    #region 碰撞检测
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canDamage) return;
        if (IsValidTarget(other)) HandleHit(other);
    }

    bool IsValidTarget(Collider2D target)
    {
        if (((1 << target.gameObject.layer) & attackLayers.value) == 0)
            return false;

        return target.CompareTag("Player") || target.CompareTag("Ally");
    }

    void HandleHit(Collider2D target)
    {
        Vector2 hitDirection = (target.transform.position - transform.position).normalized;

        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        bool isBlocking = false;

        if (playerHealth != null)
        {
            ShieldController shieldController = target.GetComponentInChildren<ShieldController>();
            isBlocking = shieldController != null && shieldController.IsBlocking();

            if (isBlocking)
            {
                OnAttackBlocked();
                playerHealth.TakeDamage(attackDamage * 0.3f, transform.position);
            }
            else
            {
                playerHealth.TakeDamage(attackDamage, transform.position);
            }
        }
        else
        {
            EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(attackDamage, target.ClosestPoint(transform.position));
        }

        // ✅ 只在允许时击退
        ApplyKnockbackIfAllowed(target, hitDirection, isBlocking);

        SpawnAttackEffect(target.ClosestPoint(transform.position));
    }

    void ApplyKnockbackIfAllowed(Collider2D target, Vector2 hitDirection, bool isBlocking)
    {
        if (!enableKnockback) return;

        if (isBlocking && !knockbackWhenBlocking) return;

        // ✅ 核心：用 EnemyAI/本体物理状态判断
        if (disableKnockbackWhenEnemyPhysicsOff)
        {
            if (enemyAI != null && !enemyAI.enabled) return;
            if (enemyAI != null && enemyAI.GetCurrentState() == EnemyState.Dead) return;

            if (enemyRootCol != null && !enemyRootCol.enabled) return;
            if (enemyRootRb != null && !enemyRootRb.simulated) return;
        }

        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            targetRb.AddForce(hitDirection * attackKnockback, ForceMode2D.Impulse);
        }
    }

    public void OnAttackBlocked()
    {
        if (spriteRenderer != null)
            StartCoroutine(FlashSprite(Color.blue, 0.2f));

        if (enemyAI != null)
            enemyAI.OnAttackBlocked();

        // （可选）敌人自己后退：同样尊重本体物理
        if (enemyRootRb != null && enemyRootRb.simulated)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Vector2 knockbackDirection = (transform.position - player.transform.position).normalized;
                enemyRootRb.AddForce(knockbackDirection * 3f, ForceMode2D.Impulse);
            }
        }
    }

    void SpawnAttackEffect(Vector2 position)
    {
        if (attackEffectPrefab == null) return;

        GameObject effect = Instantiate(attackEffectPrefab, position, Quaternion.identity);

        Vector3 direction = (Vector3)position - transform.position;
        if (direction.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            effect.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        Destroy(effect, 1f);
    }
    #endregion

    #region 公共方法
    public bool IsAttacking() => isAttacking;

    public void SetAttackDamage(float damage) => attackDamage = damage;

    public void IncreaseAttackRange(float multiplier)
    {
        if (attackCollider is BoxCollider2D boxCollider)
        {
            Vector2 newSize = boxCollider.size;
            newSize.x = originalColliderSize * multiplier;
            boxCollider.size = newSize;

            Vector2 newOffset = boxCollider.offset;
            newOffset.x = attackOffset.x * multiplier;
            boxCollider.offset = newOffset;
        }
    }

    public void SetAttackLayers(LayerMask layers) => attackLayers = layers;
    #endregion
}
