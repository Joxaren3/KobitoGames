using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 小人パルクールのプレイヤー移動。
/// 走る・ジャンプ（コヨーテタイム／先行入力／長押しで高さ調整）・壁走り・壁ジャンプ。
/// キーボード(WASD/矢印 + Space) と ゲームパッド(左スティック + 南ボタン) に対応。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("移動方向の基準にするカメラ。空ならMain Cameraを使う")]
    [SerializeField] Transform cameraTransform;

    [Header("走り")]
    [SerializeField] float maxSpeed = 9f;
    [SerializeField] float groundAccel = 60f;   // 地上での加速
    [SerializeField] float groundDecel = 50f;   // 入力がないときの減速
    [SerializeField] float airAccel = 20f;      // 空中での方向転換のしやすさ
    [SerializeField] float turnSpeed = 720f;    // 見た目の向きが変わる速さ(度/秒)

    [Header("ジャンプ")]
    [SerializeField] float jumpHeight = 2.2f;
    [SerializeField] float gravity = -30f;
    [SerializeField] float fallGravityMultiplier = 1.6f; // 落下時は重力を強めてキビキビさせる
    [SerializeField] float jumpCutMultiplier = 0.5f;     // ボタンを早く離すと低いジャンプになる
    [SerializeField] float coyoteTime = 0.12f;           // 足場から落ちた直後でもジャンプできる猶予
    [SerializeField] float jumpBufferTime = 0.12f;       // 着地直前の入力を受け付ける猶予
    [SerializeField] float maxFallSpeed = -40f;
    [SerializeField] float groundStickVelocity = -2f;

    [Header("壁走り")]
    [SerializeField] LayerMask wallMask = ~0;
    [SerializeField] float wallCheckDistance = 0.6f;
    [SerializeField] float wallRunMinSpeed = 4f;     // この速さ以上で壁に触れると壁走り開始
    [SerializeField] float wallRunSpeed = 10f;
    [SerializeField] float wallRunGravity = -4f;     // 壁走り中のゆるい重力
    [SerializeField] float wallRunMaxFallSpeed = -3f;
    [SerializeField] float wallRunMaxTime = 1.5f;
    [SerializeField] float wallStickForce = 1f;      // 壁に吸い付かせる力
    [SerializeField] float wallRunCooldown = 0.25f;

    [Header("壁ジャンプ")]
    [SerializeField] float wallJumpUp = 9f;
    [SerializeField] float wallJumpOut = 7f;
    [SerializeField] float wallJumpKeepAlong = 0.8f; // 壁沿いの勢いをどれだけ残すか

    CharacterController cc;
    InputAction moveAction;
    InputAction jumpAction;

    Vector3 horizontalVel;
    float verticalVel;
    float lastGroundedTime = -10f;
    float lastJumpPressedTime = -10f;
    bool isJumping;

    bool isWallRunning;
    Vector3 wallNormal;
    float wallRunTimer;
    float wallRunCooldownUntil;
    bool hasLastWall;
    Vector3 lastWallNormal;

    // カメラや演出用に公開
    public Vector3 HorizontalVelocity => horizontalVel;
    public float HorizontalSpeed => horizontalVel.magnitude;
    public bool IsGrounded => cc.isGrounded;
    public bool IsWallRunning => isWallRunning;
    public Vector3 WallNormal => wallNormal;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");

        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
    }

    void OnDestroy()
    {
        moveAction.Dispose();
        jumpAction.Dispose();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 wishDir = GetWishDirection();

        if (jumpAction.WasPressedThisFrame()) lastJumpPressedTime = Time.time;

        bool grounded = cc.isGrounded;
        if (grounded)
        {
            if (isWallRunning) StopWallRun();
            hasLastWall = false; // 着地したら同じ壁にもう一度走れる
            lastGroundedTime = Time.time;
            if (verticalVel < 0f)
            {
                verticalVel = groundStickVelocity;
                isJumping = false;
            }
        }

        if (isWallRunning)
        {
            UpdateWallRun(dt, wishDir);
        }
        else
        {
            UpdateHorizontal(dt, wishDir, grounded);
            TryStartWallRun(grounded);
            if (!isWallRunning)
            {
                UpdateJump();
                ApplyGravity(dt);
            }
        }

        Vector3 motion = (horizontalVel + Vector3.up * verticalVel) * dt;
        CollisionFlags flags = cc.Move(motion);
        if ((flags & CollisionFlags.Above) != 0 && verticalVel > 0f) verticalVel = 0f; // 天井に頭をぶつけた

        UpdateFacing(dt);
    }

    // ---------- 入力 ----------

    Vector3 GetWishDirection()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
    }

    // ---------- 走り・ジャンプ ----------

    void UpdateHorizontal(float dt, Vector3 wishDir, bool grounded)
    {
        bool hasInput = wishDir.sqrMagnitude > 0.01f;
        if (!grounded && !hasInput) return; // 空中で入力がなければ勢いを保つ

        Vector3 target = wishDir * maxSpeed;
        float accel = grounded ? (hasInput ? groundAccel : groundDecel) : airAccel;
        horizontalVel = Vector3.MoveTowards(horizontalVel, target, accel * dt);
    }

    void UpdateJump()
    {
        bool buffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canCoyote = Time.time - lastGroundedTime <= coyoteTime;

        if (buffered && canCoyote && !isJumping)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isJumping = true;
            lastJumpPressedTime = -10f;
            lastGroundedTime = -10f;
        }

        if (isJumping && verticalVel > 0f && jumpAction.WasReleasedThisFrame())
        {
            verticalVel *= jumpCutMultiplier;
        }
    }

    void ApplyGravity(float dt)
    {
        float g = gravity * (verticalVel < 0f ? fallGravityMultiplier : 1f);
        verticalVel = Mathf.Max(verticalVel + g * dt, maxFallSpeed);
    }

    // ---------- 壁走り ----------

    void TryStartWallRun(bool grounded)
    {
        if (grounded || Time.time < wallRunCooldownUntil) return;
        if (HorizontalSpeed < wallRunMinSpeed) return;

        Vector3 moveDir = horizontalVel.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, moveDir);
        Vector3 origin = transform.TransformPoint(cc.center);
        float dist = cc.radius + wallCheckDistance;

        Vector3[] dirs =
        {
            right,
            -right,
            (right + moveDir).normalized,
            (-right + moveDir).normalized,
        };

        foreach (Vector3 dir in dirs)
        {
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, dist, wallMask, QueryTriggerInteraction.Ignore))
                continue;

            if (Mathf.Abs(hit.normal.y) > 0.2f) continue;                 // ほぼ垂直な面だけ
            if (Vector3.Dot(moveDir, hit.normal) > 0.3f) continue;        // 壁から離れる方向に動いている
            if (hasLastWall && Vector3.Dot(hit.normal, lastWallNormal) > 0.9f) continue; // 直前と同じ壁

            isWallRunning = true;
            wallNormal = hit.normal;
            wallRunTimer = 0f;
            isJumping = false;
            if (verticalVel < 0f) verticalVel = 0f;
            return;
        }
    }

    void UpdateWallRun(float dt, Vector3 wishDir)
    {
        wallRunTimer += dt;

        Vector3 origin = transform.TransformPoint(cc.center);
        bool stillOnWall = Physics.Raycast(origin, -wallNormal, out RaycastHit hit,
            cc.radius + wallCheckDistance, wallMask, QueryTriggerInteraction.Ignore);
        if (stillOnWall) wallNormal = hit.normal;

        bool pullingAway = Vector3.Dot(wishDir, wallNormal) > 0.6f;
        if (!stillOnWall || wallRunTimer > wallRunMaxTime || pullingAway)
        {
            StopWallRun();
            ApplyGravity(dt);
            return;
        }

        if (Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            WallJump();
            return;
        }

        Vector3 along = Vector3.Cross(wallNormal, Vector3.up).normalized;
        if (Vector3.Dot(along, horizontalVel) < 0f) along = -along;

        float speed = Mathf.MoveTowards(Vector3.Dot(horizontalVel, along), wallRunSpeed, groundAccel * dt);
        horizontalVel = along * speed - wallNormal * wallStickForce;

        // 上昇中は通常の重力、下がり始めたらゆっくりずり落ちる
        float g = verticalVel > 0f ? gravity : wallRunGravity;
        verticalVel = Mathf.Max(verticalVel + g * dt, wallRunMaxFallSpeed);
    }

    void WallJump()
    {
        Vector3 along = horizontalVel - Vector3.Project(horizontalVel, wallNormal);
        along.y = 0f;

        horizontalVel = wallNormal * wallJumpOut + along * wallJumpKeepAlong;
        verticalVel = wallJumpUp;
        isJumping = true;
        lastJumpPressedTime = -10f;
        StopWallRun();
    }

    void StopWallRun()
    {
        if (!isWallRunning) return;
        isWallRunning = false;
        hasLastWall = true;
        lastWallNormal = wallNormal;
        wallRunCooldownUntil = Time.time + wallRunCooldown;
    }

    // ---------- 見た目 ----------

    void UpdateFacing(float dt)
    {
        Vector3 look = isWallRunning ? Vector3.ProjectOnPlane(horizontalVel, wallNormal) : horizontalVel;
        look.y = 0f;
        if (look.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(look);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * dt);
    }

    // ---------- 外部から使う ----------

    /// <summary>リスポーンなどで瞬間移動させる</summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        cc.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        cc.enabled = true;

        horizontalVel = Vector3.zero;
        verticalVel = 0f;
        isJumping = false;
        isWallRunning = false;
        hasLastWall = false;
    }
}
