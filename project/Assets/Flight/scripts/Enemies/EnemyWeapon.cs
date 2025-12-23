using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class EnemyWeapon : MonoBehaviour
{
    [Header("武器属性")]
    public string weaponName = "敌人武器";
    public float baseDamage = 15f;
    public float attackKnockback = 8f;
    public float attackRange = 1.2f;
    public Vector2 attackSize = new Vector2(1.2f, 0.5f);

    [Header("攻击效果")]
    public LayerMask attackableLayers = 1 << 8; // 默认攻击 Player 层 (第8层)
    public GameObject attackEffectPrefab;
    public AudioClip attackSound;
    public Color attackFlashColor = Color.red;
    public float attackFlashDuration = 0.1f;

    [Header("攻击动画")]
    public float attackWindupTime = 0.2f; // 攻击前摇
    public float attackSwingTime = 0.3f;  // 攻击挥动时间
    public float attackRecoveryTime = 0.2f; // 攻击恢复时间
    public AnimationCurve swingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("组件引用")]
    private SpriteRenderer spriteRenderer;
    private Collider2D weaponCollider;
    private AudioSource audioSource;
    private EnemyAI enemyOwner; // 持有此武器的敌人

    [Header("状态")]
    private bool isAttacking = false;
    private bool canDamage = false;
    private Coroutine attackCoroutine;

    #region 初始化
    void Awake()
    {
        InitializeComponents();
        SetupWeapon();
    }

    void InitializeComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        weaponCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
        }

        // 配置碰撞体
        if (weaponCollider != null)
        {
            weaponCollider.isTrigger = true;
            weaponCollider.enabled = false; // 默认关闭
        }

        // 设置武器图层和标签
        gameObject.layer = LayerMask.NameToLayer("EnemyWeapon");
        gameObject.tag = "EnemyWeapon";
    }

    void SetupWeapon()
    {
        // 配置碰撞体大小
        if (weaponCollider is BoxCollider2D boxCollider)
        {
            boxCollider.size = attackSize;
            boxCollider.offset = new Vector2(attackRange * 0.5f, 0);
        }

        Debug.Log($"{weaponName} 初始化完成，攻击范围: {attackRange}");
    }

    // 设置武器的所有者
    public void SetOwner(EnemyAI owner)
    {
        enemyOwner = owner;

        if (owner != null)
        {
            // 将武器附加到敌人身上
            Transform weaponHand = owner.transform.Find("WeaponHand");
            if (weaponHand != null)
            {
                transform.parent = weaponHand;
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                // 如果没有武器手，直接附加到敌人
                transform.parent = owner.transform;
                transform.localPosition = new Vector3(0.5f, 0.1f, 0);
            }
        }
    }
    #endregion

    #region 攻击系统
    public void StartAttack()
    {
        if (isAttacking) return;

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    public void StopAttack()
    {
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        isAttacking = false;
        canDamage = false;

        // 禁用碰撞体
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }

        // 恢复原状
        transform.localRotation = Quaternion.identity;
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        canDamage = false;

        // 阶段1: 攻击前摇
        yield return new WaitForSeconds(attackWindupTime);

        // 阶段2: 攻击挥动
        yield return StartCoroutine(SwingAttack());

        // 阶段3: 攻击恢复
        yield return new WaitForSeconds(attackRecoveryTime);

        isAttacking = false;
    }

    private IEnumerator SwingAttack()
    {
        // 播放攻击音效
        if (attackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(attackSound);
        }

        // 攻击闪光效果
        if (spriteRenderer != null)
        {
            StartCoroutine(FlashWeapon(attackFlashColor, attackFlashDuration));
        }

        // 启用伤害碰撞体
        canDamage = true;
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }

        // 挥动动画
        float elapsed = 0f;
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = Quaternion.Euler(0, 0, 90f); // 挥动90度

        while (elapsed < attackSwingTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / attackSwingTime;
            t = swingCurve.Evaluate(t);

            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        // 短暂停顿
        yield return new WaitForSeconds(0.05f);

        // 禁用伤害
        canDamage = false;
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }

        // 快速收回
        elapsed = 0f;
        float returnTime = attackSwingTime * 0.2f;
        while (elapsed < returnTime)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(endRot, startRot, elapsed / returnTime);
            yield return null;
        }

        transform.localRotation = startRot;
    }

    private IEnumerator FlashWeapon(Color flashColor, float duration)
    {
        if (spriteRenderer == null) yield break;

        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = flashColor;

        yield return new WaitForSeconds(duration);

        spriteRenderer.color = originalColor;
    }

    public bool IsAttacking()
    {
        return isAttacking;
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
        // 方法1：检查图层
        if ((attackableLayers.value & (1 << target.gameObject.layer)) != 0)
            return true;

        // 方法2：检查标签
        if (target.CompareTag("Player") || target.CompareTag("Ally"))
            return true;

        // 方法3：检查是否有 PlayerHealth 组件
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            return true;

        return false;
    }

    void HandleHit(Collider2D target)
    {
        // 计算击退方向
        Vector2 hitDirection = (target.transform.position - transform.position).normalized;

        // 应用伤害
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            // 检查是否被格挡
            bool isBlocked = CheckIfBlocked(target);

            if (isBlocked)
            {
                // 攻击被格挡
                OnAttackBlocked();

                // 造成少量伤害（穿透格挡）
                float blockedDamage = baseDamage * 0.3f;
                playerHealth.TakeDamage(blockedDamage, transform.position);
            }
            else
            {
                // 正常伤害
                playerHealth.TakeDamage(baseDamage, transform.position);
            }
        }
        else
        {
            // 尝试从父对象找
            playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(baseDamage, target.ClosestPoint(transform.position));
            }
        }

        // 应用击退
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb == null)
        {
            targetRb = target.GetComponentInParent<Rigidbody2D>();
        }

        if (targetRb != null)
        {
            targetRb.AddForce(hitDirection * attackKnockback, ForceMode2D.Impulse);
        }

        // 生成攻击特效
        SpawnAttackEffect(target.ClosestPoint(transform.position));

        // 一次攻击只伤害一个目标
        canDamage = false;
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }

        Debug.Log($"{weaponName} 命中 {target.name}，造成 {baseDamage} 点伤害");
    }

    bool CheckIfBlocked(Collider2D target)
    {
        // 检查目标是否有盾牌
        ShieldObject shield = target.GetComponentInChildren<ShieldObject>();
        if (shield != null && shield.IsBlocking())
        {
            // 检查是否在防御角度内
            Vector3 directionToTarget = (transform.position - shield.transform.position).normalized;
            Vector3 shieldForward = shield.GetBlockDirection();
            float angle = Vector3.Angle(shieldForward, directionToTarget);

            return angle <= 90f; // 假设盾牌前方90度内可以格挡
        }

        return false;
    }

    public void OnAttackBlocked()
    {
        Debug.Log($"{weaponName} 的攻击被格挡！");

        // 播放被格挡音效
        if (attackSound != null)
        {
            AudioSource.PlayClipAtPoint(attackSound, transform.position, 0.5f);
        }

        // 触发视觉效果
        if (spriteRenderer != null)
        {
            StartCoroutine(FlashWeapon(Color.blue, 0.2f));
        }

        // 通知所有者攻击被格挡
        if (enemyOwner != null)
        {
            // 这里可以调用敌人的反应方法
        }

        // 轻微击退武器
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
    public float GetDamage()
    {
        return baseDamage;
    }

    public void SetDamage(float damage)
    {
        baseDamage = damage;
    }

    public float GetAttackRange()
    {
        return attackRange;
    }

    public void SetAttackRange(float range)
    {
        attackRange = range;
        UpdateColliderSize();
    }

    public void SetAttackableLayers(LayerMask layers)
    {
        attackableLayers = layers;
    }

    void UpdateColliderSize()
    {
        if (weaponCollider is BoxCollider2D boxCollider)
        {
            boxCollider.size = new Vector2(attackRange, attackSize.y);
            boxCollider.offset = new Vector2(attackRange * 0.5f, 0);
        }
    }

    // 武器升级
    public void UpgradeWeapon(float damageBonus = 0f, float rangeBonus = 0f)
    {
        baseDamage += damageBonus;
        attackRange += rangeBonus;
        UpdateColliderSize();

        Debug.Log($"{weaponName} 升级！伤害: {baseDamage}, 范围: {attackRange}");
    }
    #endregion

    #region 编辑器工具
    void OnDrawGizmosSelected()
    {
        // 绘制攻击范围
        Gizmos.color = Color.red;
        Vector3 rangeEnd = transform.position + transform.right * attackRange;
        Gizmos.DrawLine(transform.position, rangeEnd);
        Gizmos.DrawWireSphere(rangeEnd, 0.1f);

        // 绘制碰撞体
        if (weaponCollider != null)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);

            if (weaponCollider is BoxCollider2D boxCollider)
            {
                Vector3 center = transform.position + (Vector3)boxCollider.offset;
                Vector3 size = boxCollider.size;
                Gizmos.DrawWireCube(center, size);
            }
        }
    }

    void OnValidate()
    {
        // 在编辑器中实时更新
        if (Application.isPlaying) return;

        UpdateColliderSize();
    }
    #endregion
}