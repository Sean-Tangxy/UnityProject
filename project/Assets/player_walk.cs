using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class DirectionalMovement : MonoBehaviour
{
    [Header("移动设置")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;

    [Header("动画参数名称")]
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";
    public string isMovingParam = "IsMoving";
    public string isRunningParam = "IsRunning";

    [Header("摄像机设置")]
    public bool followCamera = true;
    public Vector3 cameraOffset = new Vector3(0, 0, -10);

    [Tooltip("像素风关键：统一PPU=100")]
    public int pixelsPerUnit = 100;

    [Header("退出设置")]
    public bool escToQuit = true;

    [Header("碰撞移动设置")]
    [Tooltip("与墙保持的最小距离（世界单位）。100PPU 时 0.01=1像素，常用 0.001~0.003")]
    public float skinWidth = 0.002f;

    [Tooltip("哪些层算作障碍物（墙、障碍物所在层）")]
    public LayerMask obstacleMask;

    [Header("八方向移动设置")]
    [Tooltip("是否允许斜角移动")]
    public bool allowDiagonalMovement = true;

    [Tooltip("斜角移动时是否保持对角线速度一致（true=八方向同等速度，false=对角线会稍快）")]
    public bool normalizeDiagonal = true;

    private Animator animator;
    private Rigidbody2D rb;
    private Camera mainCamera;

    // 输入与状态（供移动/动画使用）
    private float horizontal;
    private float vertical;
    private bool isMoving;
    private bool isRunning;

    // ✅ 新增：四向朝向（供武器/挂载点用）
    // 默认朝下
    public Vector2 LastFacingDir { get; private set; } = Vector2.down;

    // 物理检测缓存
    private readonly RaycastHit2D[] castResults = new RaycastHit2D[8];
    private ContactFilter2D contactFilter;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = obstacleMask,
            useTriggers = false
        };

        if (mainCamera == null)
            Debug.LogWarning("未找到主摄像机！");
    }

    void Update()
    {
        if (escToQuit && Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
            return;
        }

        UpdateInputValues();
        HandleRunInput();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        DoMoveWithCollision();
    }

    void LateUpdate()
    {
        UpdateCameraPixelPerfect();
    }

    // ✅ 八方向输入：允许斜角移动
    void UpdateInputValues()
    {
        float rawHorizontal = 0f;
        float rawVertical = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) rawHorizontal -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) rawHorizontal += 1f;

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) rawVertical -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) rawVertical += 1f;

        if (allowDiagonalMovement)
        {
            horizontal = rawHorizontal;
            vertical = rawVertical;

            if (normalizeDiagonal && horizontal != 0f && vertical != 0f)
            {
                Vector2 dir = new Vector2(horizontal, vertical).normalized;
                horizontal = dir.x;
                vertical = dir.y;
            }
        }
        else
        {
            if (Mathf.Abs(rawHorizontal) > Mathf.Abs(rawVertical))
            {
                horizontal = rawHorizontal;
                vertical = 0f;
            }
            else if (Mathf.Abs(rawVertical) > Mathf.Abs(rawHorizontal))
            {
                horizontal = 0f;
                vertical = rawVertical;
            }
            else
            {
                horizontal = rawHorizontal;
                vertical = rawVertical;
            }
        }

        isMoving = (horizontal != 0f || vertical != 0f);

        // ✅ 新增：只要有输入，就更新“朝向(四向)”
        if (isMoving)
        {
            LastFacingDir = To4Dir(new Vector2(horizontal, vertical));
        }
    }

    // ✅ 把任意方向归并为四向（斜向取“更强轴”）
    static Vector2 To4Dir(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return Vector2.down;

        // 注意：你 normalizeDiagonal=true 时斜向是 0.707/0.707，所以用 abs 比较即可
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return dir.x >= 0 ? Vector2.right : Vector2.left;
        else
            return dir.y >= 0 ? Vector2.up : Vector2.down;
    }

    void HandleRunInput()
    {
        isRunning = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && isMoving;
    }

    void DoMoveWithCollision()
    {
        Vector2 dir = new Vector2(horizontal, vertical);
        if (dir == Vector2.zero) return;

        if (allowDiagonalMovement && !normalizeDiagonal && horizontal != 0f && vertical != 0f)
        {
            dir = dir.normalized;
        }

        float speed = isRunning ? runSpeed : walkSpeed;
        float distance = speed * Time.fixedDeltaTime;

        int hitCount = rb.Cast(dir, contactFilter, castResults, distance + skinWidth);

        float allowed = distance;
        for (int i = 0; i < hitCount; i++)
        {
            float d = castResults[i].distance - skinWidth;
            if (d < allowed) allowed = Mathf.Max(0f, d);
        }

        rb.MovePosition(rb.position + dir * allowed);
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetBool(isMovingParam, isMoving);
        animator.SetBool(isRunningParam, isRunning && isMoving);

        animator.SetFloat(moveXParam, horizontal);
        animator.SetFloat(moveYParam, vertical);
    }

    void UpdateCameraPixelPerfect()
    {
        if (mainCamera == null || !followCamera) return;

        Vector3 target = new Vector3(rb.position.x, rb.position.y, 0f) + cameraOffset;
        target = QuantizeToPixelGrid(target, pixelsPerUnit);

        mainCamera.transform.position = target;
    }

    static Vector3 QuantizeToPixelGrid(Vector3 pos, int ppu)
    {
        if (ppu <= 0) return pos;
        float step = 1f / ppu;
        pos.x = Mathf.Round(pos.x / step) * step;
        pos.y = Mathf.Round(pos.y / step) * step;
        return pos;
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ✅ 给外部用：当前四向朝向
    public Vector2 GetFacing4Dir()
    {
        return LastFacingDir;
    }

    // 你原来的辅助方法保留
    public Vector2 GetMovementDirection()
    {
        return new Vector2(horizontal, vertical).normalized;
    }

    public float GetMovementAngle()
    {
        if (!isMoving) return 0f;

        Vector2 dir = new Vector2(horizontal, vertical);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        if (angle < 0) angle += 360f;
        return angle;
    }
}
