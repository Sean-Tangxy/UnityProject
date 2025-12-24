using UnityEngine;
using UnityEngine.SceneManagement;

public class GameCondition : MonoBehaviour
{
    [Header("场景设置")]
    public int victorySceneIndex = 5;    // 胜利场景编号
    public int restartSceneIndex = 1;    // 重新开始场景
    public int menuSceneIndex = 0;       // 主菜单场景

    [Header("BOSS引用")]
    public EnemyHealth bossHealth;       // BOSS生命组件

    [Header("延迟设置")]
    public float delayBeforeVictory = 2f; // BOSS死亡后延迟切换场景

    [Header("调试选项")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool autoFindBoss = true; // 是否自动查找BOSS

    private bool isVictory = false;
    private bool hasSubscribed = false;

    void Start()
    {
        // 自动查找BOSS
        if (autoFindBoss)
        {
            FindBossAutomatically();
        }

        // 订阅死亡事件
        SubscribeToBossEvents();

        if (showDebugInfo)
        {
            if (bossHealth != null)
            {
                Debug.Log($"GameCondition: 已找到BOSS {bossHealth.gameObject.name}，当前生命值: {bossHealth.CurrentHealth}");
            }
            else
            {
                Debug.LogWarning("GameCondition: 未找到BOSS，将在Update中持续查找");
            }
        }
    }

    void Update()
    {
        // 如果开始没找到BOSS，持续尝试查找
        if (bossHealth == null && autoFindBoss)
        {
            FindBossAutomatically();
            SubscribeToBossEvents();
        }

        // 备用检测：如果事件系统有问题，直接检测血量
        if (bossHealth != null && !isVictory && bossHealth.CurrentHealth <= 0)
        {
            HandleBossDeath();
        }
    }

    /// <summary>
    /// 自动查找BOSS
    /// </summary>
    private void FindBossAutomatically()
    {
        // 方法1：通过标签查找
        GameObject bossObject = GameObject.FindWithTag("Boss");
        if (bossObject != null)
        {
            bossHealth = bossObject.GetComponent<EnemyHealth>();
            if (bossHealth != null && showDebugInfo)
            {
                Debug.Log($"GameCondition: 通过标签找到BOSS: {bossObject.name}");
                return;
            }
        }

        // 方法2：通过类型查找所有EnemyHealth，选择isBoss为true的
        EnemyHealth[] allEnemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        foreach (EnemyHealth enemy in allEnemies)
        {
            if (enemy.isBoss)
            {
                bossHealth = enemy;
                if (showDebugInfo)
                {
                    Debug.Log($"GameCondition: 通过isBoss属性找到BOSS: {enemy.gameObject.name}");
                }
                return;
            }
        }

        // 方法3：如果只有一个敌人，就假定它是BOSS
        if (allEnemies.Length == 1)
        {
            bossHealth = allEnemies[0];
            bossHealth.isBoss = true; // 设置为BOSS
            if (showDebugInfo)
            {
                Debug.Log($"GameCondition: 将唯一敌人设为BOSS: {bossHealth.gameObject.name}");
            }
        }
    }

    /// <summary>
    /// 订阅BOSS事件
    /// </summary>
    private void SubscribeToBossEvents()
    {
        if (bossHealth != null && !hasSubscribed)
        {
            bossHealth.OnDeath += HandleBossDeath;
            hasSubscribed = true;

            if (showDebugInfo)
            {
                Debug.Log("GameCondition: 成功订阅BOSS死亡事件");
            }
        }
    }

    /// <summary>
    /// 处理BOSS死亡
    /// </summary>
    private void HandleBossDeath()
    {
        if (!isVictory)
        {
            isVictory = true;

            if (showDebugInfo)
            {
                Debug.Log("GameCondition: BOSS死亡，准备加载胜利场景");
            }

            Invoke("LoadVictoryScene", delayBeforeVictory);
        }
    }

    /// <summary>
    /// 加载胜利场景
    /// </summary>
    private void LoadVictoryScene()
    {
        if (SceneManager.sceneCountInBuildSettings > victorySceneIndex)
        {
            SceneManager.LoadScene(victorySceneIndex);
        }
        else
        {
            Debug.LogError($"GameCondition: 胜利场景索引{victorySceneIndex}超出范围！Build Settings中只有{SceneManager.sceneCountInBuildSettings}个场景");

            // 尝试加载第一个场景作为备用
            if (SceneManager.sceneCountInBuildSettings > 0)
            {
                SceneManager.LoadScene(0);
            }
        }
    }

    void OnDestroy()
    {
        // 清理事件订阅
        if (bossHealth != null && hasSubscribed)
        {
            bossHealth.OnDeath -= HandleBossDeath;
        }
    }

    // ==================== 提供给UI按钮调用的公共方法 ====================

    /// <summary>
    /// 重新开始游戏
    /// </summary>
    public void RestartGame()
    {
        Time.timeScale = 1f; // 恢复时间（如果暂停了）

        if (SceneManager.sceneCountInBuildSettings > restartSceneIndex)
        {
            SceneManager.LoadScene(restartSceneIndex);
        }
        else
        {
            Debug.LogWarning($"重启场景索引{restartSceneIndex}无效，加载第一个场景");
            SceneManager.LoadScene(0);
        }
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void ReturnToMenu()
    {
        Time.timeScale = 1f; // 恢复时间（如果暂停了）

        if (SceneManager.sceneCountInBuildSettings > menuSceneIndex)
        {
            SceneManager.LoadScene(menuSceneIndex);
        }
        else
        {
            Debug.LogWarning($"主菜单场景索引{menuSceneIndex}无效，加载第一个场景");
            SceneManager.LoadScene(0);
        }
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 手动设置BOSS引用（如果需要）
    /// </summary>
    public void SetBossReference(EnemyHealth enemyHealth)
    {
        // 先清理旧的订阅
        if (bossHealth != null && hasSubscribed)
        {
            bossHealth.OnDeath -= HandleBossDeath;
        }

        // 设置新的BOSS引用
        bossHealth = enemyHealth;
        hasSubscribed = false;

        // 重新订阅事件
        SubscribeToBossEvents();

        if (showDebugInfo && bossHealth != null)
        {
            Debug.Log($"GameCondition: 已手动设置BOSS引用: {bossHealth.gameObject.name}");
        }
    }

    /// <summary>
    /// 手动触发胜利（用于测试）
    /// </summary>
    public void TriggerVictoryForTesting()
    {
        if (!isVictory)
        {
            HandleBossDeath();
        }
    }

    /// <summary>
    /// 获取当前胜利状态
    /// </summary>
    public bool IsVictoryAchieved()
    {
        return isVictory;
    }

    /// <summary>
    /// 获取BOSS血量百分比
    /// </summary>
    public float GetBossHealthPercentage()
    {
        if (bossHealth != null)
        {
            return bossHealth.HealthPercentage;
        }
        return 0f;
    }
}