using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("生命值设置")]
    public float maxHealth = 30f;          // 最大生命值
    [SerializeField] private float currentHealth;  // 当前生命值

    [Header("受伤效果")]
    public Color hurtColor = Color.red;    // 受伤颜色
    public float hurtFlashDuration = 0.1f; // 闪烁时间
    public GameObject bloodEffect;         // 血液特效（可选）

    [Header("音效")]
    public AudioClip hurtSound;            // 受伤音效
    public AudioClip deathSound;           // 死亡音效

    // 组件引用
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Collider2D enemyCollider;

    // 事件（可选）
    public System.Action<float> OnHealthChanged;   // 生命值变化事件
    public System.Action OnDeath;                  // 死亡事件

    void Start()
    {
        // 初始化生命值
        currentHealth = maxHealth;

        // 获取组件
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyCollider = GetComponent<Collider2D>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        Debug.Log($"{gameObject.name} 已初始化，生命值: {currentHealth}/{maxHealth}");
    }

    // 受到伤害的主方法（公开接口）
    public void TakeDamage(float damageAmount, Vector3 hitPosition)
    {
        if (currentHealth <= 0) return; // 已经死亡

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
        {
            AudioSource.PlayClipAtPoint(hurtSound, transform.position, 0.5f);
        }

        // 6. 生成血液特效
        if (bloodEffect != null)
        {
            Instantiate(bloodEffect, hitPosition, Quaternion.identity);
        }

        // 7. 调试信息
        Debug.Log($"{gameObject.name} 受到 {damageAmount} 点伤害，剩余生命: {currentHealth}");

        // 8. 检查死亡
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // 显示伤害数字（简单实现）
    void ShowDamageNumber(float damage, Vector3 position)
    {
        // 创建文本对象（这里用简单的Debug，你可以扩展为UI）
        Debug.Log($"<color=red>伤害: {damage}</color>");

        // 高级版本：创建3D文本或UI
        /*
        GameObject damageText = new GameObject("DamageText");
        TextMesh textMesh = damageText.AddComponent<TextMesh>();
        textMesh.text = damage.ToString();
        textMesh.color = Color.red;
        textMesh.fontSize = 20;
        damageText.transform.position = position + Vector3.up * 0.5f;
        Destroy(damageText, 1f);
        */
    }

    // 受伤效果协程（闪烁）
    private IEnumerator HurtEffect()
    {
        if (spriteRenderer == null) yield break;

        // 变红
        spriteRenderer.color = hurtColor;

        // 等待
        yield return new WaitForSeconds(hurtFlashDuration);

        // 恢复颜色
        spriteRenderer.color = originalColor;
    }

    // 死亡处理
    void Die()
    {
        Debug.Log($"{gameObject.name} 已死亡");

        // 1. 播放死亡音效
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position, 0.7f);
        }

        // 2. 触发死亡事件
        OnDeath?.Invoke();

        // 3. 禁用碰撞体（避免继续被攻击）
        if (enemyCollider != null)
        {
            enemyCollider.enabled = false;
        }

        // 4. 可选：播放死亡动画
        // 这里简单实现为销毁对象

        // 5. 延迟销毁（给特效和音效时间）
        Destroy(gameObject, 0.5f);
    }

    // 治疗（可选）
    public void Heal(float healAmount)
    {
        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth / maxHealth);
        Debug.Log($"{gameObject.name} 恢复 {healAmount} 生命，当前: {currentHealth}");
    }

    // 获取当前生命值（只读属性）
    public float CurrentHealth
    {
        get { return currentHealth; }
    }

    // 获取生命值百分比
    public float HealthPercentage
    {
        get { return currentHealth / maxHealth; }
    }

    // 调试用：在Scene视图中显示生命值
    void OnDrawGizmosSelected()
    {
        // 绘制生命值条
        Vector3 position = transform.position + Vector3.up * 1f;

        // 背景条（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(position, new Vector3(1f, 0.1f, 0));

        // 生命条（绿色）
        Gizmos.color = Color.green;
        float healthPercent = Application.isPlaying ? HealthPercentage : 1f;
        Gizmos.DrawCube(position - new Vector3(0.5f * (1 - healthPercent), 0, 0),
                       new Vector3(healthPercent, 0.08f, 0));
    }
}