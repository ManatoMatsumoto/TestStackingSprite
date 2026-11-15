using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// スプライトスタッキング確認用のトップビューカメラ。
/// 注視点を中心に Z 軸まわりに回転（＝周囲を回り込む）し、積層スプライトを360度から眺められる。
///
/// 操作:
///   ← / → (A / D)       : カメラを左右に回り込ませる
///   マウスドラッグ (左/右) : カメラを回転
///   マウスホイール        : ズーム
///   ↑ / ↓ (W / S)       : 高さ倍率（擬似的なカメラの傾き）
///   Space               : 自動回転 ON/OFF
///   R                   : リセット
///   H                   : ヘルプ表示切替
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class SpriteStackOrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector2 targetPoint;
    public float distance = 10f;

    [Header("Rotation")]
    public float angle;
    public float keyRotateSpeed = 120f;      // 度/秒
    public float mouseSensitivity = 0.3f;    // 度/ピクセル
    public bool autoRotate;
    public float autoRotateSpeed = 30f;
    public float smoothing = 12f;

    [Header("Zoom")]
    public float minZoom = 1.5f;
    public float maxZoom = 15f;
    public float zoomStep = 0.88f;

    [Header("Height (pseudo tilt)")]
    public float heightScaleSpeed = 1.5f;
    public float minHeightScale = 0f;
    public float maxHeightScale = 2f;

    [Header("UI")]
    public bool showHelp = true;

    Camera cam;
    float targetAngle, currentAngle;
    float targetZoom, defaultZoom, defaultAngle;
    GUIStyle helpStyle;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        defaultZoom = targetZoom = cam.orthographicSize;
        defaultAngle = targetAngle = currentAngle = angle;
        SpriteStack.GlobalHeightScale = 1f;
        Apply();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float rot = 0f, mouseDx = 0f, scroll = 0f, height = 0f;
        bool toggleAuto = false, reset = false, toggleHelp = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) rot -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) rot += 1f;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) height += 1f;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed) height -= 1f;
            toggleAuto = kb.spaceKey.wasPressedThisFrame;
            reset = kb.rKey.wasPressedThisFrame;
            toggleHelp = kb.hKey.wasPressedThisFrame;
        }
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
                mouseDx = mouse.delta.ReadValue().x;
            scroll = mouse.scroll.ReadValue().y;
        }
#else
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) rot -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) rot += 1f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) height += 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) height -= 1f;
        toggleAuto = Input.GetKeyDown(KeyCode.Space);
        reset = Input.GetKeyDown(KeyCode.R);
        toggleHelp = Input.GetKeyDown(KeyCode.H);
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            mouseDx = Input.GetAxisRaw("Mouse X") * 10f;
        scroll = Input.mouseScrollDelta.y;
#endif

        if (toggleAuto) autoRotate = !autoRotate;
        if (toggleHelp) showHelp = !showHelp;
        if (reset)
        {
            targetAngle = defaultAngle;
            targetZoom = defaultZoom;
            SpriteStack.GlobalHeightScale = 1f;
            autoRotate = false;
        }

        // 左キー = カメラが左へ回り込む（画面内のモデルは反時計回りに回って見える）
        targetAngle += rot * keyRotateSpeed * dt;
        // ドラッグ = 世界を掴んで回す感覚
        targetAngle -= mouseDx * mouseSensitivity;
        if (autoRotate) targetAngle += autoRotateSpeed * dt;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoom *= scroll > 0f ? zoomStep : 1f / zoomStep;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        SpriteStack.GlobalHeightScale = Mathf.Clamp(
            SpriteStack.GlobalHeightScale + height * heightScaleSpeed * dt, minHeightScale, maxHeightScale);

        float k = 1f - Mathf.Exp(-smoothing * dt);
        currentAngle = Mathf.Lerp(currentAngle, targetAngle, k);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, k);
        angle = currentAngle;
        Apply();
    }

    void Apply()
    {
        Vector3 center = target != null ? target.position : (Vector3)targetPoint;
        transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, center.z - distance),
            Quaternion.Euler(0f, 0f, currentAngle));
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
            "← / → (A/D) : カメラ回転\n" +
            "マウスドラッグ : カメラ回転\n" +
            "ホイール : ズーム\n" +
            "↑ / ↓ (W/S) : 高さ倍率\n" +
            "Space : 自動回転  /  R : リセット  /  H : ヘルプ非表示\n" +
            $"Angle {a:0}°   Height x{SpriteStack.GlobalHeightScale:0.00}   Auto {(autoRotate ? "ON" : "OFF")}";
        GUI.Box(new Rect(10, 10, 420, 150), text, helpStyle);
    }
}
