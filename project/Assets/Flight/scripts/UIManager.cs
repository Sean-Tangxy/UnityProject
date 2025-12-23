// UIManager.cs - 挂在Canvas上
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("玩家血条")]
    public Slider playerHealthSlider;
    public Image playerHealthFill;
    public Text playerHealthText;

    [Header("血条颜色")]
    public Color fullHealthColor = Color.green;
    public Color mediumHealthColor = Color.yellow;
    public Color lowHealthColor = Color.red;

    [Header("玩家引用")]
    public PlayerHealth playerHealth;

    void Start()
    {
        if (playerHealth == null)
        {
            // 尝试自动查找玩家
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerHealth = player.GetComponent<PlayerHealth>();
            }
        }

        if (playerHealth != null)
        {
            // 订阅玩家生命值变化事件
            playerHealth.OnHealthChanged += UpdatePlayerHealthUI;

            // 初始化血条
            if (playerHealthSlider != null)
            {
                playerHealthSlider.maxValue = playerHealth.maxHealth;
                playerHealthSlider.value = playerHealth.GetHealthPercentage() * playerHealth.maxHealth;
            }

            UpdatePlayerHealthUI(playerHealth.GetHealthPercentage());
        }
    }

    void UpdatePlayerHealthUI(float healthPercentage)
    {
        if (playerHealthSlider != null)
        {
            playerHealthSlider.value = playerHealth.GetHealthPercentage() * playerHealthSlider.maxValue;
        }

        if (playerHealthFill != null)
        {
            // 根据血量改变颜色
            if (healthPercentage > 0.6f)
            {
                playerHealthFill.color = fullHealthColor;
            }
            else if (healthPercentage > 0.3f)
            {
                playerHealthFill.color = mediumHealthColor;
            }
            else
            {
                playerHealthFill.color = lowHealthColor;
            }
        }

        if (playerHealthText != null && playerHealth != null)
        {
            playerHealthText.text = $"HP: {Mathf.Ceil(playerHealth.GetHealthPercentage() * 100)}%";
        }
    }

    void OnDestroy()
    {
        // 清理事件订阅
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdatePlayerHealthUI;
        }
    }
}