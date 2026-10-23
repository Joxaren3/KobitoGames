using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 三人称カメラ。マウス／右スティックで回転、速度に応じてFOVが広がり、壁走り中は少し傾く。
/// 壁にめり込まないようにSphereCastで距離を縮める。
/// Main Cameraにアタッチし、targetにPlayerを入れる。
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("追従")]
    [SerializeField] PlayerController target;
    [SerializeField] Vector3 pivotOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] float distance = 5f;
    [SerializeField] float followSmooth = 15f;

    [Header("回転")]
    [SerializeField] float mouseSensitivity = 0.12f;
    [SerializeField] float stickSensitivity = 180f;  // 度/秒
    [SerializeField] float minPitch = -30f;
    [SerializeField] float maxPitch = 70f;
    [SerializeField] bool lockCursor = true;

    [Header("スピード演出")]
    [SerializeField] float baseFov = 60f;
    [SerializeField] float maxFov = 78f;
    [SerializeField] float fovSpeedForMax = 10f;
    [SerializeField] float fovSmooth = 6f;
    [SerializeField] float wallRunTilt = 10f;
    [SerializeField] float tiltSmooth = 8f;

    [Header("壁抜け防止")]
    [SerializeField] LayerMask collisionMask = ~0;
    [SerializeField] float collisionRadius = 0.25f;

    Camera cam;
    InputAction lookMouse;
    InputAction lookStick;
    float yaw;
    float pitch = 15f;
    float currentTilt;
    Vector3 smoothedPivot;

    void Awake()
    {
        cam = GetComponent<Camera>();

        lookMouse = new InputAction("LookMouse", InputActionType.Value, "<Mouse>/delta");
        lookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

        if (target != null)
        {
            yaw = target.transform.eulerAngles.y;
            smoothedPivot = target.transform.position + pivotOffset;
        }
    }

    void OnEnable()
    {
        lookMouse.Enable();
        lookStick.Enable();
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnDisable()
    {
        lookMouse.Disable();
        lookStick.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        lookMouse.Dispose();
        lookStick.Dispose();
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Time.deltaTime;

        Vector2 mouse = lookMouse.ReadValue<Vector2>() * mouseSensitivity;
        Vector2 stick = lookStick.ReadValue<Vector2>() * stickSensitivity * dt;
        yaw += mouse.x + stick.x;
        pitch = Mathf.Clamp(pitch - mouse.y - stick.y, minPitch, maxPitch);

        Vector3 pivot = target.transform.position + pivotOffset;
        smoothedPivot = Vector3.Lerp(smoothedPivot, pivot, 1f - Mathf.Exp(-followSmooth * dt));

        float targetTilt = 0f;
        if (target.IsWallRunning)
        {
            // 壁と反対側にカメラを傾ける
            Vector3 camRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            targetTilt = Vector3.Dot(target.WallNormal, camRight) > 0f ? -wallRunTilt : wallRunTilt;
        }
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, 1f - Mathf.Exp(-tiltSmooth * dt));

        Quaternion rot = Quaternion.Euler(pitch, yaw, currentTilt);
        Vector3 dir = rot * Vector3.back;

        float dist = distance;
        if (Physics.SphereCast(smoothedPivot, collisionRadius, dir, out RaycastHit hit, distance,
                collisionMask, QueryTriggerInteraction.Ignore))
        {
            dist = Mathf.Max(hit.distance, 0.3f);
        }

        transform.SetPositionAndRotation(smoothedPivot + dir * dist, rot);

        if (cam != null)
        {
            float t = Mathf.Clamp01(target.HorizontalSpeed / fovSpeedForMax);
            float fov = Mathf.Lerp(baseFov, maxFov, t * t);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-fovSmooth * dt));
        }
    }
}
