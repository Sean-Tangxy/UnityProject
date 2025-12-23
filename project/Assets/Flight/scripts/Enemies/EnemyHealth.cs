using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("生命值设置")]
    public float maxHealth = 30f;                 // 最大生命值
    private float currentHealth;                  // ✅ 不序列化，避免Inspector污染

    [Header("受伤效果")]
    public Color hurtColor = Color.red;
    public float hurtFlashDuration = 0.1f;
    public GameObject bloodEffect;

    [Header("音效")]
    public AudioClip hurtSound;
    public AudioClip deathSound;

    // 组件引用
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Collider2D enemyCollider;

    // ✅ 新增：缓存 EnemyAI（用于盾牌倍率/掉盾）
    private EnemyAI enemyAI;

    // 事件（可选）
    public System.Action<float> OnHealthChanged;
    public System.Action OnDeath;

    void Awake()
    {
        // ✅ Awake 就缓存，避免 Start 顺序问题
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyCollider = GetComponent<Collider2D>();
        enemyAI = GetComponent<EnemyAI>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    void Start()
    {
        // ✅ 每次生成都满血
        currentHealth = maxHealth;
        Debug.Log($"{gameObject.name} 已初始化，生命值: {currentHealth}/{maxHealth}");
    }

    // 受到伤害的主方法（公开接口）
    public void TakeDamage(float damageAmount, Vector3 hitPosition)
    {
        if (currentHealth <= 0f) return;
        if (damageAmount <= 0f) return;

        // ==============================
        // ✅ 盾牌/脆弱窗口：倍率 + 掉盾
        // ==============================
        if (enemyAI != null)
        {
            // 1) 先通知被玩家命中（放盾窗口命中 -> 掉盾）
            enemyAI.NotifyDamagedByPlayer();

            // 2) 再应用易伤倍率
            float mul = enemyAI.GetIncomingDamageMultiplier();
            if (mul < 1f) mul = 1f;
            damageAmount *= mul;

            // 调试：想看倍率是否生效就打开这句
            // Debug.Log($"[DMG] base*mul => mul={mul}, final={damageAmount}");
        }

        // 1. 减少生命值
        currentHealth -= damageAmount;

        // 2. 触发生命值变化事件
        OnHealthChanged?.Invoke(currentHealth / maxHealth);

        // 3. 显示伤害数字（可选）
        ShowDamageNumber(damageAmount, hitPosition);

        // 4. 受伤效果
        StartCoroutine(HurtEffect());

        // 5. 播放受伤音效
        if (hurtSound != null)
            AudioSource.PlayClipAtPoint(hurtSound, transform.position, 0.5f);

        // 6. 生成血液特效
        if (bloodEffect != null)
            Instantiate(bloodEffect, hitPosition, Quaternion.identity);

        Debug.Log($"{gameObject.name} 受到 {damageAmount} 点伤害，剩余生命: {currentHealth}");

        // 7. 检查死亡
        if (currentHealth <= 0f)
            Die();
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

    public float CurrentHealth => currentHealth;
    public float HealthPercentage => maxHealth <= 0f ? 0f : (currentHealth / maxHealth);

    void OnDrawGizmosSelected()
    {
        Vector3 position = transform.position + Vector3.up * 1f;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(position, new Vector3(1f, 0.1f, 0));

        Gizmos.color = Color.green;
        float healthPercent = Application.isPlaying ? HealthPercentage : 1f;
        Gizmos.DrawCube(position - new Vector3(0.5f * (1 - healthPercent), 0, 0),
                        new Vector3(healthPercent, 0.08f, 0));
    }
}
