using UnityEngine;
using UnityEngine.SceneManagement;

public class portdoor : MonoBehaviour
{
    [Header("触发器")]
    public Collider2D triggerCol;     // 门的Collider2D（建议IsTrigger勾上）
    public LayerMask playerMask;      // 玩家所在层
    public KeyCode interactKey = KeyCode.T;

    [Header("倒计时")]
    public float intime = 0.2f;
    private float it;
    private bool isindoor;

    [Header("同场景传送")]
    public bool needscene = false;
    public Transform to;              // 同场景目的地点

    [Header("跨场景传送")]
    public int goscene = 0;           // build index
    public Vector3 sceneposition;     // 新场景落点（世界坐标）

    void Start()
    {
        it = intime;
    }

    void Update()
    {
        // 触发区内按键，仅触发一次
        if (!isindoor && triggerCol != null && triggerCol.IsTouchingLayers(playerMask))
        {
            if (Input.GetKeyDown(interactKey))
            {
                isindoor = true;
                it = intime;
            }
        }

        if (!isindoor) return;

        it -= Time.deltaTime;
        if (it > 0f) return;

        // 倒计时结束，执行传送
        isindoor = false;
        it = intime;

        if (!needscene)
        {
            // 同场景：找玩家并移动（本方案每场景一个玩家）
            var player = FindFirstObjectByType<DirectionalMovement>();
            if (player != null && to != null)
            {
                player.ForceTeleport(to.position);
            }
        }
        else
        {
            // 跨场景：先存落点，再加载场景
            TeleportData.Set(sceneposition);
            SceneManager.LoadScene(goscene);
        }
    }
}
