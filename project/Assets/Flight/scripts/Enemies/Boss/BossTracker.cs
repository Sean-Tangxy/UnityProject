using UnityEngine;

public class BossTracker : MonoBehaviour
{
    [Header("===== 玩家追踪设置 =====")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float detectionRange = 30f;
    [SerializeField] private float chaseSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private float stopDistance = 8f;

    [Header("===== 组件引用 =====")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private BossEnemyController bossController;

    [Header("===== 状态 =====")]
    private bool isPlayerDetected = false;
    private bool isActive = true;

    void Start()
    {
        InitializeComponents();
    }

    void Update()
    {
        if (!isActive) return;

        HandlePlayerDetection();
        HandleMovement();
    }

    void InitializeComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (bossController == null) bossController = GetComponent<BossEnemyController>();

        if (playerTarget == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTarget = playerObj.transform;
        }
    }

    void HandlePlayerDetection()
    {
        if (playerTarget == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);

        if (distanceToPlayer <= detectionRange)
        {
            if (!isPlayerDetected)
            {
                isPlayerDetected = true;
                Debug.Log("BOSS检测到玩家进入范围");
            }
        }
        else
        {
            if (isPlayerDetected)
            {
                isPlayerDetected = false;
                Debug.Log("玩家离开BOSS检测范围");
            }
        }
    }

    // 只有一个 HandleMovement 方法！删除了重复的那个
    void HandleMovement()
    {
        if (!isPlayerDetected || playerTarget == null) return;

        // 检查BOSS状态，某些状态下不移动
        if (bossController != null)
        {
            BossState currentState = bossController.GetCurrentBossState();
            if (currentState == BossState.VulnerablePhase ||
                currentState == BossState.Stunned)
            {
                // 脆弱或眩晕状态下停止移动
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
                return;
            }
        }

        Vector2 direction = (playerTarget.position - transform.position).normalized;
        float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);

        // 保持距离，给玩家反应时间
        if (distanceToPlayer > stopDistance)
        {
            Vector2 moveVelocity = direction * chaseSpeed;
            if (rb != null)
            {
                rb.linearVelocity = moveVelocity;
            }
            else
            {
                transform.position += (Vector3)moveVelocity * Time.deltaTime;
            }

            // BOSS只需要缓慢旋转
            if (rotationSpeed > 0)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.Euler(0, 0, angle - 90f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }
    }

    public void SetTrackingActive(bool active)
    {
        isActive = active;
        if (!active && rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    public void OnBossDeath()
    {
        isActive = false;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}