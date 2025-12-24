using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverChecker : MonoBehaviour
{
    [Header("场景设置")]
    public int gameOverSceneIndex = 4;  // 游戏结束场景
    public int restartSceneIndex = 1;    // 重新开始场景
    public int menuSceneIndex = 0;       // 主菜单场景

    [Header("玩家引用")]
    public PlayerHealth playerHealth;

    [Header("延迟设置")]
    public float delayBeforeGameOver = 1f; // 死亡后延迟切换场景

    private bool isGameOver = false;

    void Start()
    {
        if (playerHealth == null)
        {
            // 使用新的 API 查找玩家生命组件
            playerHealth = FindFirstObjectByType<PlayerHealth>();

            // 如果还是没找到，尝试通过标签查找
            if (playerHealth == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    playerHealth = player.GetComponent<PlayerHealth>();
                }
            }
        }

        // 订阅死亡事件
        if (playerHealth != null)
        {
            playerHealth.OnDeath += HandlePlayerDeath;
            Debug.Log("GameOverChecker: 成功订阅玩家死亡事件");
        }
        else
        {
            Debug.LogWarning("GameOverChecker: 未找到PlayerHealth组件，将在Update中持续查找");
        }
    }

    void Update()
    {
        // 如果开始没找到，持续尝试查找
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnDeath += HandlePlayerDeath;
                Debug.Log("GameOverChecker: 在Update中找到并订阅PlayerHealth");
            }
        }
    }

    void HandlePlayerDeath()
    {
        if (!isGameOver)
        {
            isGameOver = true;
            Debug.Log("GameOverChecker: 玩家死亡，准备加载游戏结束场景");
            Invoke("LoadGameOverScene", delayBeforeGameOver);
        }
    }

    void LoadGameOverScene()
    {
        if (SceneManager.sceneCountInBuildSettings > gameOverSceneIndex)
        {
            SceneManager.LoadScene(gameOverSceneIndex);
        }
        else
        {
            Debug.LogError($"GameOverChecker: 场景索引{gameOverSceneIndex}超出范围！");
        }
    }

    void OnDestroy()
    {
        // 清理事件订阅
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandlePlayerDeath;
        }
    }

    // 提供给UI按钮调用的方法
    public void RestartGame()
    {
        Time.timeScale = 1f; // 恢复时间（如果暂停了）
        SceneManager.LoadScene(restartSceneIndex);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f; // 恢复时间（如果暂停了）
        SceneManager.LoadScene(menuSceneIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}