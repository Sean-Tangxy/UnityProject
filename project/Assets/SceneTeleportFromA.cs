// SimpleTeleporter.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class SimpleTeleporter : MonoBehaviour
{
    [Header("场景设置")]
    [Tooltip("目标场景名称（在Build Settings中配置）")]
    public string targetScene = "Scene2";

    [Tooltip("是否使用异步加载")]
    public bool useAsyncLoad = true;

    [Header("传送参数")]
    [Tooltip("传送延迟时间")]
    public float teleportDelay = 0.5f;

    [Tooltip("是否在传送后禁用玩家输入")]
    public bool disableInputOnTeleport = true;

    [Tooltip("传送音效")]
    public AudioClip teleportSound;

    [Header("视觉效果")]
    [Tooltip("传送时的粒子效果")]
    public ParticleSystem teleportEffect;

    [Tooltip("传送时的屏幕特效")]
    public GameObject screenEffectPrefab;

    private AudioSource audioSource;
    private bool isTeleporting = false;

    void Start()
    {
        // 确保碰撞器是触发器
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        // 添加音频源组件
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && teleportSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // 如果没有设置标签，自动设置为Teleporter
        if (gameObject.tag == "Untagged")
        {
            gameObject.tag = "Teleporter";
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isTeleporting) return;

        if (other.CompareTag("Player"))
        {
            StartTeleportation(other.gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isTeleporting) return;

        if (other.CompareTag("Player"))
        {
            StartTeleportation(other.gameObject);
        }
    }

    void StartTeleportation(GameObject player)
    {
        if (isTeleporting) return;

        isTeleporting = true;

        // 播放音效
        if (teleportSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(teleportSound);
        }

        // 播放粒子效果
        if (teleportEffect != null)
        {
            teleportEffect.Play();
        }

        // 创建屏幕特效
        if (screenEffectPrefab != null)
        {
            Instantiate(screenEffectPrefab, player.transform.position, Quaternion.identity);
        }

        // 禁用玩家输入
        if (disableInputOnTeleport)
        {
            DisablePlayerInput(player);
        }

        // 开始传送协程
        StartCoroutine(TeleportCoroutine(player));
    }

    System.Collections.IEnumerator TeleportCoroutine(GameObject player)
    {
        // 等待传送延迟
        yield return new WaitForSeconds(teleportDelay);

        // 异步加载场景
        if (useAsyncLoad)
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);
            asyncLoad.allowSceneActivation = false;

            // 等待加载到90%
            while (asyncLoad.progress < 0.9f)
            {
                // 可以在这里显示加载进度
                Debug.Log($"Loading progress: {asyncLoad.progress * 100}%");
                yield return null;
            }

            // 激活场景
            asyncLoad.allowSceneActivation = true;

            // 等待场景激活完成
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }
        else
        {
            // 同步加载
            SceneManager.LoadScene(targetScene);
        }

        isTeleporting = false;
    }

    void DisablePlayerInput(GameObject player)
    {
        // 禁用玩家控制器
        MonoBehaviour[] controllers = player.GetComponents<MonoBehaviour>();
        foreach (var controller in controllers)
        {
            if (controller is MonoBehaviour && controller.enabled)
            {
                controller.enabled = false;
            }
        }

        // 禁用刚体运动
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Rigidbody2D rb2d = player.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.simulated = false;
        }
    }

    // 验证场景是否存在
    void OnValidate()
    {
#if UNITY_EDITOR
        // 检查场景是否在Build Settings中
        bool sceneExists = false;
        foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
        {
            if (scene.path.Contains(targetScene + ".unity"))
            {
                sceneExists = true;
                break;
            }
        }

        if (!sceneExists && !string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning($"场景 '{targetScene}' 未添加到Build Settings中！");
        }
#endif
    }
}