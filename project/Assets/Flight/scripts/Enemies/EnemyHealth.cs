using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("生命值设置")]
    public float maxHealth = 30f;                 // 最大生命值
    private float currentHealth;                  // 当前生命值

    [Header("受伤效果")]
    public Color hurtColor = Color.red;
    public float hurtFlashDuration = 0.1f;
    public GameObject bloodEffect;

    [Header("音效")]
    public AudioClip hurtSound;
    public AudioClip deathSound;

    [Header("BOSS特殊设置")]
    public bool isBoss = false;                   // 标记是否为BOSS
    public bool shieldActive = true;              // 盾牌是否激活
    public float shieldHealth = 1000f;            // 盾牌生命值
    private float currentShieldHealth;            // 当前盾牌生命值
    public float shieldDamageMultiplier = 0.1f;   // 盾牌状态伤害倍率

    // 组件引用
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Collider2D enemyCollider;

    // 缓存 EnemyAI（用于盾牌倍率/掉盾）
    private EnemyAI enemyAI;

    // 事件
    public System.Action<float> OnHealthChanged;
    public System.Action OnDeath;
    public System.Action<float> OnShieldHealthChanged; // 新增：盾牌血量变化事件

    void Awake()
    {
        // 获取组件引用
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyCollider = GetComponent<Collider2D>();
        enemyAI = GetComponent<EnemyAI>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    void Start()
    {
        // 初始化生命值
        currentHealth = maxHealth;
        currentShieldHealth = shieldHealth;

        Debug.Log($"{gameObject.name} 已初始化，生命值: {currentHealth}/{maxHealth}" +
                  (isBoss ? $", 盾牌: {currentShieldHealth}/{shieldHealth}" : ""));
    }

    // 受到伤害的主方法
    public void TakeDamage(float damageAmount, Vector3 hitPosition)
    {
        if (currentHealth <= 0f) return;
        if (damageAmount <= 0f) return;

        float finalDamage = damageAmount;

        // ==============================
        // BOSS盾牌逻辑
        // ==============================
        if (isBoss && shieldActive && currentShieldHealth > 0)
        {
            // 先计算盾牌伤害
            float shieldDamage = finalDamage;
            currentShieldHealth -= shieldDamage;

            // 触发盾牌血量变化事件
            OnShieldHealthChanged?.Invoke(currentShieldHealth / shieldHealth);

            Debug.Log($"{gameObject.name} 盾牌受到 {shieldDamage} 点伤害，剩余盾牌: {currentShieldHealth}");

            // 如果盾牌被击破
            if (currentShieldHealth <= 0)
            {
                shieldActive = false;
                Debug.Log($"{gameObject.name} 盾牌已被击破!");

                // 触发盾牌破碎事件
                OnShieldHealthChanged?.Invoke(0f);
            }

            // 盾牌状态下，对本体伤害大幅降低
            finalDamage *= shieldDamageMultiplier;
        }

        // ==============================
        // 原有伤害倍率逻辑
        // ==============================
        if (enemyAI != null)
        {
            // 1) 先通知被玩家命中
            enemyAI.NotifyDamagedByPlayer();

            // 2) 再应用易伤倍率
            float mul = enemyAI.GetIncomingDamageMultiplier();
            if (mul < 1f) mul = 1f;
            finalDamage *= mul;
        }

        // 1. 减少生命值
        currentHealth -= finalDamage;

        // 2. 触发生命值变化事件
        OnHealthChanged?.Invoke(currentHealth / maxHealth);

        // 3. 显示伤害数字
        ShowDamageNumber(finalDamage, hitPosition);

        // 4. 受伤效果
        StartCoroutine(HurtEffect());

        // 5. 播放受伤音效
        if (hurtSound != null)
            AudioSource.PlayClipAtPoint(hurtSound, transform.position, 0.5f);

        // 6. 生成血液特效
        if (bloodEffect != null)
            Instantiate(bloodEffect, hitPosition, Quaternion.identity);

        Debug.Log($"{gameObject.name} 受到 {finalDamage} 点伤害，剩余生命: {currentHealth}");

        // 7. 检查死亡
        if (currentHealth <= 0f)
            Die();
    }

    // BOSS特殊方法：恢复盾牌
    public void RestoreShield()
    {
        if (isBoss)
        {
            shieldActive = true;
            currentShieldHealth = shieldHealth;
            OnShieldHealthChanged?.Invoke(1f);
            Debug.Log($"{gameObject.name} 盾牌已恢复");
        }
    }

    // BOSS特殊方法：设置盾牌状态
    public void SetShieldActive(bool active)
    {
        if (isBoss)
        {
            shieldActive = active;
            if (active && currentShieldHealth <= 0)
            {
                currentShieldHealth = shieldHealth;
                OnShieldHealthChanged?.Invoke(1f);
            }
        }
    }

    void ShowDamageNumber(float damage, Vector3 position)
    {
        Debug.Log($"<color=red>伤害: {damage}</color>");
    }

    IEnumerator HurtEffect()
    {
        if (spriteRenderer == null) yield break;

        spriteRenderer.color = hurtColor;
        yield return new WaitForSeconds(hurtFlashDuration);
        spriteRenderer.color = originalColor;
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} 已死亡");

        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, transform.position, 0.7f);

        OnDeath?.Invoke();

        if (enemyCollider != null)
            enemyCollider.enabled = false;

        Destroy(gameObject, 0.5f);
    }

    public void Heal(float healAmount)
    {
        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth / maxHealth);
        Debug.Log($"{gameObject.name} 恢复 {healAmount} 生命，当前: {currentHealth}");
    }

    // 属性
    public float CurrentHealth => currentHealth;
    public float HealthPercentage => maxHealth <= 0f ? 0f : (currentHealth / maxHealth);
    public float CurrentShieldHealth => currentShieldHealth;
    public float ShieldPercentage => shieldHealth <= 0f ? 0f : (currentShieldHealth / shieldHealth);
    public bool IsShieldActive => shieldActive;

    void OnDrawGizmosSelected()
    {
        Vector3 position = transform.position + Vector3.up * 1f;

        // 绘制生命条
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(position, new Vector3(1f, 0.1f, 0));

        float healthPercent = Application.isPlaying ? HealthPercentage : 1f;
        Gizmos.color = Color.green;
        Gizmos.DrawCube(position - new Vector3(0.5f * (1 - healthPercent), 0, 0),
                        new Vector3(healthPercent, 0.08f, 0));

        // BOSS额外绘制盾牌条
        if (isBoss)
        {
            Vector3 shieldPosition = transform.position + Vector3.up * 1.2f;

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(shieldPosition, new Vector3(1f, 0.08f, 0));

            float shieldPercent = Application.isPlaying ? ShieldPercentage : 1f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawCube(shieldPosition - new Vector3(0.5f * (1 - shieldPercent), 0, 0),
                            new Vector3(shieldPercent, 0.06f, 0));
        }
    }
}