// EnemyHealthBar.cs - 挂在敌人对象上
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("血条设置")]
    public GameObject healthBarPrefab; // 血条预制体
    public Vector3 offset = new Vector3(0, 1.5f, 0); // 血条位置偏移

    [Header("血条颜色")]
    public Color fullHealthColor = Color.green;
    public Color lowHealthColor = Color.red;

    private GameObject healthBarInstance;
    private Slider healthSlider;
    private Image fillImage;
    private EnemyHealth enemyHealth;
    private Transform mainCamera;

    void Start()
    {
        // 获取敌人生命值组件
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth == null)
        {
            Debug.LogError("EnemyHealth 组件未找到!");
            return;
        }

        // 获取主相机
        mainCamera = Camera.main.transform;

        // 创建血条UI
        CreateHealthBar();

        // 订阅生命值变化事件
        enemyHealth.OnHealthChanged += UpdateHealthBar;
        enemyHealth.OnDeath += OnEnemyDeath;
    }

    void CreateHealthBar()
    {
        if (healthBarPrefab == null)
        {
            // 创建默认血条
            healthBarInstance = new GameObject("EnemyHealthBar");
            healthBarInstance.transform.SetParent(transform);

            Canvas canvas = healthBarInstance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            // 设置画布大小
            RectTransform canvasRect = healthBarInstance.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(2, 0.5f);

            // 创建背景
            GameObject background = new GameObject("Background");
            background.transform.SetParent(healthBarInstance.transform);
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0);
            bgRect.anchorMax = new Vector2(1, 1);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 创建血条填充
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(healthBarInstance.transform);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0.1f, 0.3f);
            fillAreaRect.anchorMax = new Vector2(0.9f, 0.7f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            // 创建填充
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform);
            fillImage = fill.AddComponent<Image>();
            fillImage.color = fullHealthColor;

            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(1, 1);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // 创建滑块组件
            healthSlider = healthBarInstance.AddComponent<Slider>();
            healthSlider.fillRect = fillRect;
            healthSlider.targetGraphic = fillImage;
            healthSlider.interactable = false;
        }
        else
        {
            // 使用预制体
            healthBarInstance = Instantiate(healthBarPrefab, transform);
            healthSlider = healthBarInstance.GetComponentInChildren<Slider>();
            fillImage = healthSlider.fillRect.GetComponent<Image>();
        }

        // 设置初始位置
        if (healthBarInstance != null)
        {
            healthBarInstance.transform.localPosition = offset;

            // 初始血条值
            healthSlider.maxValue = enemyHealth.maxHealth;
            healthSlider.value = enemyHealth.CurrentHealth;
        }
    }

    void Update()
    {
        if (healthBarInstance != null && mainCamera != null)
        {
            // 让血条始终面向相机
            healthBarInstance.transform.LookAt(
                healthBarInstance.transform.position + mainCamera.forward
            );
        }
    }

    void UpdateHealthBar(float healthPercentage)
    {
        if (healthSlider != null)
        {
            // 更新血条值
            healthSlider.value = enemyHealth.CurrentHealth;

            // 根据血量改变颜色
            if (fillImage != null)
            {
                fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, healthPercentage);
            }
        }
    }

    void OnEnemyDeath()
    {
        // 敌人死亡时隐藏血条
        if (healthBarInstance != null)
        {
            healthBarInstance.SetActive(false);
        }

        // 清理事件订阅
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged -= UpdateHealthBar;
            enemyHealth.OnDeath -= OnEnemyDeath;
        }
    }

    void OnDestroy()
    {
        // 清理事件订阅
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged -= UpdateHealthBar;
            enemyHealth.OnDeath -= OnEnemyDeath;
        }
    }
}