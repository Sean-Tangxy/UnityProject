using UnityEngine;
using System.Collections;

public class WeaponObject : MonoBehaviour
{
    [Header("武器数据配置")]
    public WeaponData weaponData;

    [Header("组件引用")]
    private SpriteRenderer spriteRenderer;
    private Collider2D weaponCollider;
    private AudioSource audioSource;

    [Header("状态")]
    private bool isAttacking = false;
    private bool canDamage = false; // 伤害帧控制
    private float currentCooldown = 0f;
    private Transform handlePoint;

    #region 初始化
    void Awake()
    {
        InitializeComponents();
        FindHandlePoint();
        ApplyWeaponData();
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
            audioSource.spatialBlend = 0.7f; // 3D音效混合
        }

        // 配置碰撞体
        if (weaponCollider != null)
        {
            weaponCollider.isTrigger = true;
            weaponCollider.enabled = false; // 默认关闭
        }
    }

    void FindHandlePoint()
    {
        handlePoint = transform.Find("HandlePoint");
        if (handlePoint == null)
        {
            Debug.LogWarning($"{gameObject.name} 缺少HandlePoint，将使用自身作为参考点");
            handlePoint = transform;
        }
    }

    // 应用武器数据到各个组件
    void ApplyWeaponData()
    {
        if (weaponData == null)
        {
            Debug.LogError("WeaponData未分配！请在Inspector中配置");
            return;
        }

        // 1. 应用外观
        if (spriteRenderer != null)
        {
            spriteRenderer.color = weaponData.weaponColor;
            if (weaponData.weaponIcon != null)
            {
                spriteRenderer.sprite = weaponData.weaponIcon;
            }
        }

        // 2. 应用碰撞体大小
        if (weaponCollider != null)
        {
            UpdateColliderSize();
        }

        // 3. 初始化冷却时间
        currentCooldown = weaponData.GetActualCooldown();

        Debug.Log($"武器初始化: {weaponData.weaponName}, 伤害: {weaponData.GetActualDamage()}, 冷却: {currentCooldown}s");
    }

    void UpdateColliderSize()
    {
        if (weaponCollider is BoxCollider2D boxCollider)
        {
            boxCollider.size = new Vector2(weaponData.attackRange, 0.3f);
            boxCollider.offset = new Vector2(weaponData.attackRange * 0.5f, 0);
        }
        else if (weaponCollider is CircleCollider2D circleCollider)
        {
            circleCollider.radius = weaponData.attackRange * 0.5f;
        }
    }
    #endregion

    #region 攻击系统
    public bool CanAttack()
    {
        return !isAttacking;
    }

    public void StartAttack()
    {
        if (isAttacking || weaponData == null) return;

        StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        isAttacking = true;
        canDamage = false;

        // 阶段1: 攻击前摇（可选）
        yield return null;

        // 阶段2: 挥动动画
        yield return StartCoroutine(SwingAnimation());

        // 阶段3: 冷却恢复
        yield return StartCoroutine(CooldownRecovery());

        isAttacking = false;
    }

    private IEnumerator SwingAnimation()
    {
        // 播放挥动音效
        PlaySwingSound();

        // 启用碰撞体（在攻击的有效帧）
        canDamage = true;
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }

        // 挥动动画
        float elapsed = 0f;
        float swingDuration = weaponData.swingDuration;
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = Quaternion.Euler(0, 0, weaponData.swingAngle);

        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / swingDuration;
            t = weaponData.swingCurve.Evaluate(t); // 使用配置的曲线

            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        // 短暂停顿在最大角度
        yield return new WaitForSeconds(0.05f);

        // 禁用伤害
        canDamage = false;
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }

        // 快速收回
        elapsed = 0f;
        float returnDuration = swingDuration * 0.3f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(endRot, startRot, elapsed / returnDuration);
            yield return null;
        }

        // 确保完全复位
        transform.localRotation = startRot;
    }

    private IEnumerator CooldownRecovery()
    {
        float cooldownTime = weaponData.GetActualCooldown();
        float elapsed = 0f;

        while (elapsed < cooldownTime)
        {
            elapsed += Time.deltaTime;
            // 这里可以更新UI显示冷却进度
            // float progress = elapsed / cooldownTime;
            yield return null;
        }
    }

    private void PlaySwingSound()
    {
        if (weaponData.swingSound != null && audioSource != null)
        {
            audioSource.clip = weaponData.swingSound;
            audioSource.Play();
        }
    }
    #endregion

    #region 碰撞检测与伤害
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canDamage || weaponData == null) return;

        if (other.CompareTag("Enemy"))
        {
            ApplyDamage(other);
        }
    }

    void ApplyDamage(Collider2D enemy)
    {
        // 计算最终伤害
        float finalDamage = weaponData.GetActualDamage();

        // 获取命中点
        Vector2 hitPoint = enemy.ClosestPoint(transform.position);

        // 获取敌人生命组件
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(finalDamage, hitPoint);
            Debug.Log($"{weaponData.weaponName} 对 {enemy.name} 造成 {finalDamage} 点伤害");
        }
        else
        {
            Debug.LogWarning($"敌人 {enemy.name} 没有EnemyHealth组件");
        }

        // 播放命中音效
        PlayHitSound(hitPoint);

        // 可选：触发命中特效
        SpawnHitEffect(hitPoint);

        // 可选：应用击退
        ApplyKnockback(enemy, hitPoint);
    }

    private void PlayHitSound(Vector2 position)
    {
        if (weaponData.hitSound != null)
        {
            AudioSource.PlayClipAtPoint(weaponData.hitSound, position, 0.5f);
        }
    }

    private void SpawnHitEffect(Vector2 position)
    {
        // 这里可以实例化命中特效
        // GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        // Destroy(effect, 1f);
    }

    private void ApplyKnockback(Collider2D enemy, Vector2 hitPoint)
    {
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
            float knockbackForce = weaponData.damage * 0.5f; // 根据伤害计算击退力
            rb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
        }
    }
    #endregion

    #region 公共方法
    public float GetDamage()
    {
        return weaponData != null ? weaponData.GetActualDamage() : 0f;
    }

    public float GetCooldown()
    {
        return weaponData != null ? weaponData.GetActualCooldown() : 1f;
    }

    public float GetAttackRange()
    {
        return weaponData != null ? weaponData.attackRange : 1f;
    }

    public Vector3 GetHandlePosition()
    {
        return handlePoint.position;
    }

    public Vector3 GetHandleLocalPosition()
    {
        return handlePoint.localPosition;
    }

    // 动态更新武器数据（用于装备升级等）
    public void UpdateWeaponData(WeaponData newData)
    {
        weaponData = newData;
        ApplyWeaponData();
    }

    // 获取武器信息（用于UI显示）
    public string GetWeaponInfo()
    {
        if (weaponData == null) return "无武器";

        return $"{weaponData.weaponName}\n" +
               $"伤害: {weaponData.GetActualDamage()}\n" +
               $"攻击速度: {weaponData.attackSpeed}\n" +
               $"冷却: {weaponData.GetActualCooldown():F1}s";
    }
    #endregion

    #region 编辑器工具
    void OnValidate()
    {
        // 在编辑器中实时预览武器数据效果
        if (Application.isPlaying) return;

        if (weaponData != null && spriteRenderer != null)
        {
            spriteRenderer.color = weaponData.weaponColor;
        }
    }

    // 在Scene视图中显示攻击范围
    void OnDrawGizmosSelected()
    {
        if (weaponData == null) return;

        Gizmos.color = Color.yellow;

        // 绘制攻击范围
        Vector3 rangeEnd = transform.position + transform.right * weaponData.attackRange;
        Gizmos.DrawLine(transform.position, rangeEnd);
        Gizmos.DrawWireSphere(rangeEnd, 0.1f);

        // 绘制HandlePoint位置
        if (handlePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(handlePoint.position, 0.05f);
            Gizmos.DrawLine(handlePoint.position, transform.position);
        }
    }
    #endregion
}