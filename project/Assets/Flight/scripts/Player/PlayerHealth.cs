using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("生命值设置")]
    public float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("防御设置")]
    public float defense = 5f; // 基础防御力

    [Header("受伤效果")]
    public float invincibilityTime = 1f; // 受伤无敌时间
    public float hurtFlashRate = 0.1f; // 闪烁频率
    private float invincibilityTimer = 0f;
    private bool isInvincible = false;

    [Header("组件")]
    private SpriteRenderer spriteRenderer;

    // 事件
    public System.Action<float> OnHealthChanged;
    public System.Action OnDeath;

    void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        UpdateInvincibility();
    }

    public void TakeDamage(float damage, Vector3 damageSource)
    {
        if (isInvincible) return;

        // 计算最终伤害（考虑防御）
        float finalDamage = Mathf.Max(1, damage - defense);

        // 减少生命值
        currentHealth -= finalDamage;

        // 触发事件
        OnHealthChanged?.Invoke(currentHealth / maxHealth);

        // 受伤效果
        StartCoroutine(HurtEffect());

        // 无敌时间
        StartInvincibility();

        // 击退效果
        ApplyKnockback(damageSource);

        Debug.Log($"玩家受到 {finalDamage} 伤害，剩余生命: {currentHealth}");

        // 检查死亡
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void StartInvincibility()
    {
        isInvincible = true;
        invincibilityTimer = invincibilityTime;
    }

    void UpdateInvincibility()
    {
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = true;
                }
            }
        }
    }

    System.Collections.IEnumerator HurtEffect()
    {
        if (spriteRenderer == null) yield break;

        float timer = invincibilityTime;
        bool visible = true;

        while (timer > 0)
        {
            visible = !visible;
            spriteRenderer.enabled = visible;

            yield return new WaitForSeconds(hurtFlashRate);
            timer -= hurtFlashRate;
        }

        spriteRenderer.enabled = true;
    }

    void ApplyKnockback(Vector3 damageSource)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 knockbackDirection = (transform.position - damageSource).normalized;
            rb.AddForce(knockbackDirection * 5f, ForceMode2D.Impulse);
        }
    }

    void Die()
    {
        Debug.Log("玩家死亡");
        OnDeath?.Invoke();

        // 这里可以添加玩家死亡逻辑
        // 例如：游戏结束界面、重生等
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth / maxHealth);
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }
}