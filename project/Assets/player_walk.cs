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
    public float skinWidth = 0.002f;
    public LayerMask obstacleMask;

    [Header("八方向移动设置")]
    public bool allowDiagonalMovement = true;
    public bool normalizeDiagonal = true;

    private Animator animator;
    private Rigidbody2D rb;
    private Camera mainCamera;

    private float horizontal;
    private float vertical;
    private bool isMoving;
    private bool isRunning;

    public Vector2 LastFacingDir { get; private set; } = Vector2.down;

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

        // ✅ 切场景后新玩家在 Start() 应用落点（关键）
        if (TeleportData.TryConsume(out var pos))
        {
            ForceTeleport(pos);
        }
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

        if (isMoving)
        {
            LastFacingDir = To4Dir(new Vector2(horizontal, vertical));
        }
    }

    static Vector2 To4Dir(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return Vector2.down;

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

    public Vector2 GetFacing4Dir() => LastFacingDir;

    public void ForceTeleport(Vector3 worldPos)
    {
        rb.position = new Vector2(worldPos.x, worldPos.y);
        transform.position = worldPos;

        rb.linearVelocity = Vector2.zero;  // ✅ 正确字段
        ClearInput();

        UpdateAnimator();
        UpdateCameraPixelPerfect();
    }

    public void ClearInput()
    {
        horizontal = 0f;
        vertical = 0f;
        isMoving = false;
        isRunning = false;

        if (animator != null)
        {
            animator.SetBool(isMovingParam, false);
            animator.SetBool(isRunningParam, false);
            animator.SetFloat(moveXParam, 0f);
            animator.SetFloat(moveYParam, 0f);
        }
    }
}
