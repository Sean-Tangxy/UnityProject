using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ShieldObject : MonoBehaviour
{
    [Header("盾牌配置")]
    public ShieldData shieldData;

    [Header("组件引用")]
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D shieldCollider;
    private AudioSource audioSource;

    [Header("状态")]
    private bool isBlocking = false;
    private bool canBlock = true;
    private float currentDurability;
    private float blockCooldownTimer = 0f;
    private Transform playerTransform;
    private List<Collider2D> blockedAttacks = new List<Collider2D>(); // 防止重复格挡

    #region 初始化
    void Awake()
    {
        InitializeComponents();
        ApplyShieldData();
    }

    void InitializeComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        shieldCollider = GetComponent<BoxCollider2D>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
        }

        // 配置碰撞体
        if (shieldCollider != null)
        {
            shieldCollider.isTrigger = true;
            shieldCollider.enabled = false; // 默认不启用，只在防御时启用
        }

        // 初始化耐久度
        if (shieldData != null)
        {
            currentDurability = shieldData.maxDurability;
        }
    }

    void ApplyShieldData()
    {
        if (shieldData == null)
        {
            Debug.LogError("ShieldData未分配！");
            return;
        }

        // 应用外观
        if (spriteRenderer != null)
        {
            if (shieldData.shieldSprite != null)
            {
                spriteRenderer.sprite = shieldData.shieldSprite;
            }
            spriteRenderer.color = shieldData.shieldColor;
        }

        // 应用碰撞体大小
        if (shieldCollider != null)
        {
            shieldCollider.size = shieldData.shieldSize;
            shieldCollider.offset = shieldData.shieldOffset;
        }

        Debug.Log($"盾牌初始化: {shieldData.shieldName}, 耐久: {currentDurability}/{shieldData.maxDurability}");
    }
    #endregion

    #region 防御系统
    public void StartBlock()
    {
        if (!canBlock || shieldData == null) return;
        if (currentDurability <= 0) return;

        isBlocking = true;
        blockedAttacks.Clear(); // 清空已格挡列表

        // 启用防御碰撞体
        if (shieldCollider != null)
        {
            shieldCollider.enabled = true;
        }

        // 播放防御动画
        StartCoroutine(BlockAnimation());

        Debug.Log("开始防御");
    }

    public void StopBlock()
    {
        if (!isBlocking) return;

        isBlocking = false;

        // 禁用防御碰撞体
        if (shieldCollider != null)
        {
            shieldCollider.enabled = false;
        }

        // 清空已格挡列表
        blockedAttacks.Clear();

        // 进入冷却
        StartCoroutine(BlockCooldown());

        Debug.Log("停止防御");
    }

    private IEnumerator BlockAnimation()
    {
        // 防御时的视觉效果
        Vector3 originalScale = transform.localScale;
        Vector3 blockScale = originalScale * 1.1f;

        float elapsed = 0f;
        float duration = 0.1f;

        // 放大效果
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(originalScale, blockScale, t);
            yield return null;
        }

        // 保持防御状态
        while (isBlocking && currentDurability > 0)
        {
            // 持续防御时轻微呼吸效果
            float breathScale = 1f + Mathf.Sin(Time.time * 3f) * 0.02f;
            transform.localScale = blockScale * breathScale;

            // 更新盾牌颜色表示耐久度
            UpdateShieldAppearance();

            yield return null;
        }

        // 恢复原状
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(blockScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    private IEnumerator BlockCooldown()
    {
        canBlock = false;
        blockCooldownTimer = shieldData.blockCooldown;

        while (blockCooldownTimer > 0)
        {
            blockCooldownTimer -= Time.deltaTime;
            // 这里可以更新UI显示冷却
            yield return null;
        }

        canBlock = true;
    }

    private void UpdateShieldAppearance()
    {
        if (spriteRenderer == null) return;

        // 根据耐久度改变颜色和透明度
        float durabilityPercent = currentDurability / shieldData.maxDurability;

        Color targetColor = shieldData.shieldColor;

        if (durabilityPercent > 0.7f)
        {
            targetColor = shieldData.shieldColor;
        }
        else if (durabilityPercent > 0.3f)
        {
            // 黄色警告
            targetColor = Color.Lerp(shieldData.shieldColor, Color.yellow, 0.5f);
        }
        else
        {
            // 红色危险
            targetColor = Color.Lerp(Color.yellow, Color.red, 1f - durabilityPercent);

            // 低耐久时闪烁
            float blink = Mathf.PingPong(Time.time * 5f, 1f);
            targetColor.a = 0.5f + blink * 0.5f;
        }

        spriteRenderer.color = targetColor;
    }
    #endregion

    #region 碰撞检测与防御
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isBlocking || shieldData == null) return;

        // 防止重复格挡同一攻击
        if (blockedAttacks.Contains(other)) return;

        // 检查是否为敌人攻击
        if (other.CompareTag("EnemyWeapon") || other.CompareTag("Enemy"))
        {
            HandleBlock(other);
        }
        else if (other.CompareTag("Projectile")) // 远程攻击
        {
            HandleProjectileBlock(other);
        }
    }

    private void HandleBlock(Collider2D attacker)
    {
        // 检查是否在防御角度内
        if (!IsWithinBlockAngle(attacker.transform.position)) return;

        // 标记已格挡
        blockedAttacks.Add(attacker);

        // 消耗耐久
        ConsumeDurability(shieldData.durabilityPerHit);

        // 播放格挡音效
        if (shieldData.blockSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shieldData.blockSound);
        }

        // 触发格挡特效
        SpawnBlockEffect(attacker.ClosestPoint(transform.position));

        // 屏幕震动
        CameraShake.Instance?.Shake(shieldData.blockShakeDuration, shieldData.blockShakeIntensity);

        // 通知攻击者被格挡
        NotifyAttackBlocked(attacker);

        Debug.Log($"成功格挡攻击！当前耐久: {currentDurability}/{shieldData.maxDurability}");
    }

    private void HandleProjectileBlock(Collider2D projectile)
    {
        if (blockedAttacks.Contains(projectile)) return;
        blockedAttacks.Add(projectile);

        // 反弹或销毁弹射物
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // 反弹弹射物
            Vector2 reflectDirection = Vector2.Reflect(rb.linearVelocity.normalized, transform.right);
            rb.linearVelocity = reflectDirection * rb.linearVelocity.magnitude * 1.2f; // 反弹更快

            // 修改标签避免二次触发
            projectile.tag = "ReflectedProjectile";

            // 改变颜色表示被反弹
            SpriteRenderer projRenderer = projectile.GetComponent<SpriteRenderer>();
            if (projRenderer != null)
            {
                projRenderer.color = Color.cyan;
            }
        }
        else
        {
            // 直接销毁弹射物
            Destroy(projectile.gameObject);
        }

        // 消耗耐久（远程攻击消耗较少）
        ConsumeDurability(shieldData.durabilityPerHit * 0.5f);

        // 播放格挡音效
        if (shieldData.blockSound != null)
        {
            AudioSource.PlayClipAtPoint(shieldData.blockSound, transform.position, 0.5f);
        }

        // 生成反弹特效
        if (shieldData.blockEffectPrefab != null)
        {
            GameObject effect = Instantiate(shieldData.blockEffectPrefab,
                projectile.transform.position, Quaternion.identity);
            Destroy(effect, 0.3f);
        }
    }

    private bool IsWithinBlockAngle(Vector3 attackerPosition)
    {
        if (playerTransform == null) return true;

        Vector3 directionToAttacker = attackerPosition - playerTransform.position;
        Vector3 facingDirection = transform.right; // 盾牌面对的方向

        float angle = Vector3.Angle(facingDirection, directionToAttacker);

        return angle <= shieldData.blockAngle * 0.5f;
    }

    private void ConsumeDurability(float amount)
    {
        if (shieldData == null) return;

        currentDurability = Mathf.Max(0, currentDurability - amount);

        // 更新外观
        UpdateShieldAppearance();

        // 检查是否损坏
        if (currentDurability <= 0)
        {
            ShieldBroken();
        }
    }

    private void ShieldBroken()
    {
        Debug.Log($"盾牌 {shieldData.shieldName} 已损坏！");

        // 禁用防御功能
        isBlocking = false;
        canBlock = false;

        // 禁用碰撞体
        if (shieldCollider != null)
        {
            shieldCollider.enabled = false;
        }

        // 改变外观
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.gray;
            // 可以添加破损贴图
        }

        // 播放损坏音效
        if (shieldData.blockSound != null)
        {
            AudioSource.PlayClipAtPoint(shieldData.blockSound, transform.position, 1f);
        }

        // 生成破损特效
        if (shieldData.blockEffectPrefab != null)
        {
            GameObject brokenEffect = Instantiate(shieldData.blockEffectPrefab,
                transform.position, Quaternion.identity);

            // 改变颜色为灰色表示破损
            ParticleSystem ps = brokenEffect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startColor = Color.gray;
            }

            Destroy(brokenEffect, 2f);
        }

        // 自动掉落（可选）
        StartCoroutine(DropShield());
    }

    private IEnumerator DropShield()
    {
        // 解除父子关系
        transform.parent = null;

        // 添加物理效果
        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 2f;

        // 随机弹跳
        Vector2 randomForce = new Vector2(Random.Range(-3f, 3f), Random.Range(5f, 8f));
        rb.AddForce(randomForce, ForceMode2D.Impulse);

        // 旋转
        float randomTorque = Random.Range(-100f, 100f);
        rb.AddTorque(randomTorque);

        // 一段时间后消失
        yield return new WaitForSeconds(5f);

        // 闪烁消失
        for (int i = 0; i < 3; i++)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            yield return new WaitForSeconds(0.1f);
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            yield return new WaitForSeconds(0.1f);
        }

        Destroy(gameObject);
    }

    private void SpawnBlockEffect(Vector2 position)
    {
        if (shieldData.blockEffectPrefab != null)
        {
            GameObject effect = Instantiate(shieldData.blockEffectPrefab, position, Quaternion.identity);

            // 根据格挡方向旋转特效
            Vector3 direction = (Vector3)position - transform.position;
            if (direction.magnitude > 0.1f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                effect.transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            Destroy(effect, 1f);
        }
    }

    private void NotifyAttackBlocked(Collider2D attacker)
    {
        // 通知敌人攻击被格挡
        EnemyAttack enemyAttack = attacker.GetComponent<EnemyAttack>();
        if (enemyAttack != null)
        {
            enemyAttack.OnAttackBlocked();
        }

        // 如果是弹反时机
        if (shieldData.canParry && IsParryWindow())
        {
            PerformParry(attacker);
        }
    }

    private bool IsParryWindow()
    {
        // 这里可以实现弹反时机检测
        // 例如：在防御开始的前几帧
        return Random.value < 0.3f; // 30%几率弹反（示例）
    }

    private void PerformParry(Collider2D attacker)
    {
        Debug.Log("弹反成功！");

        // 使敌人僵直 - 使用 EnemyAI 组件
        EnemyAI enemyAI = attacker.GetComponentInParent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.Stun(shieldData.parryStunDuration);
            Debug.Log($"敌人被眩晕 {shieldData.parryStunDuration} 秒");
        }
        else
        {
            // 备用方案：如果有 EnemyHealth 组件
            EnemyHealth enemyHealth = attacker.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                // 弹反造成额外伤害
                float parryDamage = 15f; // 弹反伤害
                Vector2 hitPoint = attacker.ClosestPoint(transform.position);
                enemyHealth.TakeDamage(parryDamage, hitPoint);
                Debug.Log($"弹反造成 {parryDamage} 点额外伤害");
            }
        }

        // 触发弹反特效
        if (shieldData.blockEffectPrefab != null)
        {
            Vector2 hitPoint = attacker.ClosestPoint(transform.position);
            GameObject effect = Instantiate(shieldData.blockEffectPrefab, hitPoint, Quaternion.identity);
            effect.transform.localScale = Vector3.one * 1.5f; // 放大特效

            // 弹反特效使用不同颜色
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startColor = Color.cyan;
            }

            Destroy(effect, 0.5f);
        }

        // 弹反成功恢复少量耐久
        float durabilityRecovery = shieldData.durabilityPerHit * 0.5f;
        currentDurability = Mathf.Min(shieldData.maxDurability, currentDurability + durabilityRecovery);
        UpdateShieldAppearance();

        Debug.Log($"弹反恢复 {durabilityRecovery} 点耐久");
    }
    #endregion

    #region 公共方法
    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
    }

    public bool IsBlocking()
    {
        return isBlocking;
    }

    public bool CanBlock()
    {
        return canBlock && currentDurability > 0;
    }

    public float GetCurrentDurability()
    {
        return currentDurability;
    }

    public float GetDurabilityPercentage()
    {
        return shieldData != null ? currentDurability / shieldData.maxDurability : 0f;
    }

    public void RepairShield(float amount)
    {
        if (shieldData == null) return;

        currentDurability = Mathf.Min(shieldData.maxDurability, currentDurability + amount);
        UpdateShieldAppearance();

        // 如果从损坏状态修复，重新启用功能
        if (currentDurability > 0 && !canBlock)
        {
            canBlock = true;

            // 重新启用碰撞体
            if (shieldCollider != null)
            {
                shieldCollider.enabled = false; // 默认关闭
            }

            Debug.Log($"盾牌修复 {amount} 点耐久，当前耐久: {currentDurability}");
        }
    }

    public void UpgradeShield(float durabilityBonus = 0f, float reductionBonus = 0f)
    {
        if (shieldData == null) return;

        shieldData.maxDurability += durabilityBonus;
        shieldData.damageReduction = Mathf.Min(0.95f, shieldData.damageReduction + reductionBonus);

        // 恢复耐久度
        RepairShield(durabilityBonus);

        Debug.Log($"盾牌升级！最大耐久: {shieldData.maxDurability}, 伤害减免: {shieldData.damageReduction:P0}");
    }

    // 重置已格挡列表（每帧或每次攻击后调用）
    public void ResetBlockedAttacks()
    {
        blockedAttacks.Clear();
    }

    // 获取防御方向（用于计算防御角度）
    public Vector3 GetBlockDirection()
    {
        return transform.right;
    }
    #endregion

    #region 编辑器工具
    void OnDrawGizmosSelected()
    {
        if (shieldData == null || playerTransform == null) return;

        // 绘制防御角度
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        float halfAngle = shieldData.blockAngle * 0.5f;

        Vector3 leftDir = Quaternion.Euler(0, 0, halfAngle) * transform.right;
        Vector3 rightDir = Quaternion.Euler(0, 0, -halfAngle) * transform.right;

        Gizmos.DrawLine(playerTransform.position, playerTransform.position + leftDir * 2f);
        Gizmos.DrawLine(playerTransform.position, playerTransform.position + rightDir * 2f);

        // 绘制扇形区域
        DrawAngleSector(playerTransform.position, transform.right, shieldData.blockAngle, 2f);

        // 绘制盾牌碰撞体
        if (shieldCollider != null)
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Vector3 center = transform.position + (Vector3)shieldCollider.offset;
            Vector3 size = shieldCollider.size;
            Gizmos.DrawWireCube(center, size);
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
        // 在编辑器中更新碰撞体设置
        if (Application.isPlaying) return;

        if (shieldData != null && spriteRenderer != null)
        {
            spriteRenderer.color = shieldData.shieldColor;
        }
    }
    #endregion

    void OnDestroy()
    {
        // 清理资源
        blockedAttacks.Clear();
    }
}