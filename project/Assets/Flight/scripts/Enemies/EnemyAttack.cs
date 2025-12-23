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
    private EnemyAI enemyAI;

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
        // 获取或创建攻击碰撞体
        attackCollider = GetComponent<Collider2D>();
        if (attackCollider == null)
        {
            attackCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        // 配置碰撞体
        if (attackCollider is BoxCollider2D boxCollider)
        {
            boxCollider.size = attackSize;
            boxCollider.offset = attackOffset;
            boxCollider.isTrigger = true;
            boxCollider.enabled = false;
            originalColliderSize = boxCollider.size.x;
        }

        // 获取其他组件
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyAI = GetComponent<EnemyAI>();

        // 创建音效组件
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

        // 1. 攻击前摇（面向目标）
        if (target != null)
        {
            Vector2 direction = (target.position - transform.position).normalized;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 快速转向目标
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

        // 2. 攻击动画
        yield return StartCoroutine(AttackAnimation());

        // 3. 攻击恢复
        canDamage = false;
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }

        // 4. 通知攻击完成
        isAttacking = false;
    }

    IEnumerator AttackAnimation()
    {
        // 播放攻击音效
        if (attackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(attackSound);
        }

        // 攻击闪光效果
        if (spriteRenderer != null)
        {
            StartCoroutine(FlashSprite(attackFlashColor, attackFlashDuration));
        }

        // 启用碰撞体（伤害帧）
        canDamage = true;
        if (attackCollider != null)
        {
            attackCollider.enabled = true;
        }

        // 攻击动画（武器挥动效果）
        if (spriteRenderer != null)
        {
            yield return StartCoroutine(SwingAnimation());
        }
        else
        {
            // 简单的时间延迟
            yield return new WaitForSeconds(attackDuration * 0.3f);
        }

        // 结束攻击动画
        canDamage = false;
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }
    }

    IEnumerator SwingAnimation()
    {
        float elapsed = 0f;
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0, 0, 90f); // 挥动90度

        while (elapsed < attackDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / attackDuration;
            t = attackCurve.Evaluate(t);

            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        // 快速收回
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

        // 检查是否是可攻击的目标
        if (IsValidTarget(other))
        {
            HandleHit(other);
        }
    }

    bool IsValidTarget(Collider2D target)
    {
        // 检查图层
        if (((1 << target.gameObject.layer) & attackLayers.value) == 0)
            return false;

        // 检查标签
        if (target.CompareTag("Player") || target.CompareTag("Ally"))
            return true;

        return false;
    }

    void HandleHit(Collider2D target)
    {
        // 1. 计算击退方向
        Vector2 hitDirection = (target.transform.position - transform.position).normalized;

        // 2. 应用伤害
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            // 检查玩家是否正在格挡
            ShieldController shieldController = target.GetComponentInChildren<ShieldController>();
            bool isBlocking = shieldController != null && shieldController.IsBlocking();

            if (isBlocking)
            {
                // 攻击被格挡
                OnAttackBlocked();

                // 仍然可能造成少量伤害
                float blockedDamage = attackDamage * 0.3f; // 30%的伤害
                playerHealth.TakeDamage(blockedDamage, transform.position);
            }
            else
            {
                // 正常伤害
                playerHealth.TakeDamage(attackDamage, transform.position);
            }
        }
        else
        {
            // 其他类型的生命组件
            EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(attackDamage, target.ClosestPoint(transform.position));
            }
        }

        // 3. 应用击退
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            targetRb.AddForce(hitDirection * attackKnockback, ForceMode2D.Impulse);
        }

        // 4. 生成攻击特效
        SpawnAttackEffect(target.ClosestPoint(transform.position));

        // 5. 一次攻击只伤害一个目标（可选）
        // canDamage = false;
        // if (attackCollider != null) attackCollider.enabled = false;

        Debug.Log($"{gameObject.name} 对 {target.name} 造成 {attackDamage} 点伤害");
    }

    public void OnAttackBlocked()
    {
        Debug.Log($"{gameObject.name} 的攻击被格挡！");

        // 播放被格挡音效
        if (attackSound != null)
        {
            AudioSource.PlayClipAtPoint(attackSound, transform.position, 0.5f);
        }

        // 触发视觉效果
        if (spriteRenderer != null)
        {
            StartCoroutine(FlashSprite(Color.blue, 0.2f));
        }

        // 通知AI攻击被格挡
        if (enemyAI != null)
        {
            enemyAI.OnAttackBlocked();
        }

        // 轻微击退自己
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 knockbackDirection = (transform.position - GameObject.FindGameObjectWithTag("Player").transform.position).normalized;
            rb.AddForce(knockbackDirection * 3f, ForceMode2D.Impulse);
        }
    }

    void SpawnAttackEffect(Vector2 position)
    {
        if (attackEffectPrefab != null)
        {
            GameObject effect = Instantiate(attackEffectPrefab, position, Quaternion.identity);

            // 根据攻击方向旋转特效
            Vector3 direction = (Vector3)position - transform.position;
            if (direction.magnitude > 0.1f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                effect.transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            Destroy(effect, 1f);
        }
    }
    #endregion

    #region 公共方法
    public bool IsAttacking()
    {
        return isAttacking;
    }

    public void SetAttackDamage(float damage)
    {
        attackDamage = damage;
    }

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

    public void SetAttackLayers(LayerMask layers)
    {
        attackLayers = layers;
    }
    #endregion

    #region 编辑器工具
    void OnDrawGizmosSelected()
    {
        if (attackCollider == null) return;

        Gizmos.color = new Color(1, 0, 0, 0.3f);

        if (attackCollider is BoxCollider2D boxCollider)
        {
            Vector2 size = boxCollider.size;
            Vector2 offset = boxCollider.offset;

            Vector3 worldCenter = transform.position + (Vector3)offset;
            Vector3 worldSize = new Vector3(size.x * transform.lossyScale.x,
                                           size.y * transform.lossyScale.y,
                                           1);

            Gizmos.DrawWireCube(worldCenter, worldSize);
        }
    }

    void OnValidate()
    {
        // 在编辑器中更新碰撞体设置
        if (Application.isPlaying) return;

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            boxCollider.size = attackSize;
            boxCollider.offset = attackOffset;
            boxCollider.isTrigger = true;
        }
    }
    #endregion
}