using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [Header("UI引用")]
    public Button restartButton;
    public Button menuButton;
    public Button quitButton;

    void Start()
    {
        // 创建GameOverChecker（如果不存在）
        GameOverChecker checker = FindAnyObjectByType<GameOverChecker>();
        if (checker == null)
        {
            GameObject checkerObj = new GameObject("GameOverChecker");
            checker = checkerObj.AddComponent<GameOverChecker>();
            DontDestroyOnLoad(checkerObj); // 保持跨场景存在
        }

        // 设置按钮点击事件
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(checker.RestartGame);
        }

        if (menuButton != null)
        {
            menuButton.onClick.AddListener(checker.ReturnToMenu);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(checker.QuitGame);
        }
    }
}