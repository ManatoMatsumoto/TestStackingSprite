using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// スプライトスタッキング確認用のオービットカメラ（正投影）。
/// 注視点を中心に、ヨー（Z 軸まわりの回り込み）とピッチ（真上からの傾き）で周囲を回る。
///
/// ピッチ 0° で真上から見下ろし、角度を上げるほど斜めから見る。
/// カメラ自体が 3D 的に傾くので、地面は奥行き方向に圧縮され、積層スプライトは
/// 実際の高さに応じてずれて見える（SpriteStack.StackMode.World3D と組み合わせて使う）。
///
/// 操作:
///   ← / → (A / D)         : カメラを左右に回り込ませる
///   ↑ / ↓ (W / S)         : カメラの傾き（ピッチ）
///   マウスドラッグ (左/右)   : 横 = 回り込み、縦 = 傾き
///   マウスホイール          : ズーム
///   Space                 : 自動回転 ON/OFF
///   R                     : リセット
///   H                     : ヘルプ表示切替
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class SpriteStackOrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector2 targetPoint;
    public float distance = 20f;

    [Header("Yaw (回り込み)")]
    public float angle;
    public float keyRotateSpeed = 120f;      // 度/秒
    public float mouseSensitivity = 0.3f;    // 度/ピクセル
    public bool autoRotate;
    public float autoRotateSpeed = 30f;

    [Header("Pitch (傾き: 0 = 真上)")]
    [Range(0f, 89f)] public float pitch = 50f;
    public float minPitch = 0f;
    public float maxPitch = 70f;
    public float keyPitchSpeed = 60f;        // 度/秒

    [Header("Zoom")]
    public float minZoom = 1.5f;
    public float maxZoom = 15f;
    public float zoomStep = 0.88f;

    [Header("Misc")]
    public float smoothing = 12f;
    public bool showHelp = true;

    Camera cam;
    float targetAngle, currentAngle;
    float targetPitch, currentPitch;
    float targetZoom, defaultZoom, defaultAngle, defaultPitch;
    GUIStyle helpStyle;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        defaultZoom = targetZoom = cam.orthographicSize;
        defaultAngle = targetAngle = currentAngle = angle;
        defaultPitch = targetPitch = currentPitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        SpriteStack.GlobalHeightScale = 1f;
        Apply();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float rot = 0f, tilt = 0f, scroll = 0f;
        Vector2 mouseDelta = Vector2.zero;
        bool toggleAuto = false, reset = false, toggleHelp = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) rot -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) rot += 1f;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) tilt += 1f;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed) tilt -= 1f;
            toggleAuto = kb.spaceKey.wasPressedThisFrame;
            reset = kb.rKey.wasPressedThisFrame;
            toggleHelp = kb.hKey.wasPressedThisFrame;
        }
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
                mouseDelta = mouse.delta.ReadValue();
            scroll = mouse.scroll.ReadValue().y;
        }
#else
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) rot -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) rot += 1f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) tilt += 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) tilt -= 1f;
        toggleAuto = Input.GetKeyDown(KeyCode.Space);
        reset = Input.GetKeyDown(KeyCode.R);
        toggleHelp = Input.GetKeyDown(KeyCode.H);
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        scroll = Input.mouseScrollDelta.y;
#endif

        if (toggleAuto) autoRotate = !autoRotate;
        if (toggleHelp) showHelp = !showHelp;
        if (reset)
        {
            targetAngle = defaultAngle;
            targetPitch = defaultPitch;
            targetZoom = defaultZoom;
            autoRotate = false;
        }

        // 左キー = カメラが左へ回り込む（画面内のモデルは反時計回りに回って見える）
        targetAngle += rot * keyRotateSpeed * dt;
        // 横ドラッグ = 世界を掴んで回す感覚
        targetAngle -= mouseDelta.x * mouseSensitivity;
        if (autoRotate) targetAngle += autoRotateSpeed * dt;

        // ↑ = より斜め（横から）に、↓ = より真上に。縦ドラッグは上へドラッグで真上側へ
        targetPitch += tilt * keyPitchSpeed * dt;
        targetPitch -= mouseDelta.y * mouseSensitivity;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoom *= scroll > 0f ? zoomStep : 1f / zoomStep;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        float k = 1f - Mathf.Exp(-smoothing * dt);
        currentAngle = Mathf.Lerp(currentAngle, targetAngle, k);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, k);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, k);
        angle = currentAngle;
        pitch = currentPitch;
        Apply();
    }

    void Apply()
    {
        Vector3 center = target != null ? target.position : (Vector3)targetPoint;
        // 既定の向き: +Z 方向を見下ろし、画面上 = +Y。
        // ヨーで Z 軸まわりに回し、ピッチでローカル X 軸まわりに傾ける（画面の奥側へ視線を倒す）
        Quaternion rot = Quaternion.Euler(0f, 0f, currentAngle) * Quaternion.Euler(-currentPitch, 0f, 0f);
        Vector3 forward = rot * Vector3.forward;

        // 傾けると画面下端の地面がカメラに近づくので、ニア/ファーで切れない距離を確保する
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float reach = (halfH + halfW) * Mathf.Tan(Mathf.Min(currentPitch, 85f) * Mathf.Deg2Rad);
        float d = Mathf.Max(distance, reach + 5f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = d + reach + 20f;

        transform.SetPositionAndRotation(center - forward * d, rot);
    }

    void OnGUI()
    {
        if (!showHelp) return;
        if (helpStyle == null)
        {
            helpStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                padding = new RectOffset(10, 10, 8, 8),
            };
            helpStyle.normal.textColor = Color.white;
        }
        float a = Mathf.Repeat(currentAngle, 360f);
        string text =
            "Sprite Stacking Demo\n" +
            "← / → (A/D) : 回り込み\n" +
            "↑ / ↓ (W/S) : 傾き\n" +
            "マウスドラッグ : 横=回り込み / 縦=傾き\n" +
            "ホイール : ズーム\n" +
            "Space : 自動回転  /  R : リセット  /  H : ヘルプ非表示\n" +
            $"Yaw {a:0}°   Pitch {currentPitch:0}°   Auto {(autoRotate ? "ON" : "OFF")}";
        GUI.Box(new Rect(10, 10, 420, 150), text, helpStyle);
    }
}
