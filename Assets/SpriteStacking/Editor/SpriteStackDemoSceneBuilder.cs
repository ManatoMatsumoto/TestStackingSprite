using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// メニュー「Tools/Sprite Stacking/Create Demo Scene」でテストスプライトを生成し、
/// 確認用のデモシーン (Assets/Scenes/SpriteStackDemo.unity) を構築して開く。
/// </summary>
public static class SpriteStackDemoSceneBuilder
{
    const string ScenePath = "Assets/Scenes/SpriteStackDemo.unity";

    [MenuItem("Tools/Sprite Stacking/Create Demo Scene")]
    public static void CreateDemoScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SpriteStackTestSpriteGenerator.GenerateAll();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Camera ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.22f, 0.18f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 50f;
        camGo.transform.position = new Vector3(0, 0, -10);
        var orbit = camGo.AddComponent<SpriteStackOrbitCamera>();
        orbit.targetPoint = Vector2.zero;

        // --- Ground ---
        var grass = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteStackTestSpriteGenerator.GrassPath);
        var ground = new GameObject("Ground");
        var gsr = ground.AddComponent<SpriteRenderer>();
        gsr.sprite = grass;
        gsr.drawMode = SpriteDrawMode.Tiled;
        gsr.size = new Vector2(26, 26);
        gsr.sortingOrder = -32700;

        // --- Objects ---
        var root = new GameObject("StackedObjects").transform;

        // 内側
        Create(root, "House", new Vector2(0f, 0.5f), 0f);
        Create(root, "Tree", new Vector2(-1.4f, 1.3f), 0f);
        Create(root, "Car", new Vector2(1.2f, -1.0f), 30f);
        Create(root, "Barrel", new Vector2(-1.5f, -0.9f), 0f);
        Create(root, "Crate", new Vector2(-0.5f, -1.7f), 15f);

        // 外側
        Create(root, "Tree", new Vector2(-5f, 3f), 40f);
        Create(root, "Tree", new Vector2(5.2f, 1.5f), 120f);
        Create(root, "Tree", new Vector2(-4.5f, -3.8f), 200f);
        Create(root, "Tree", new Vector2(3.5f, -4.5f), 300f);
        Create(root, "Tree", new Vector2(0.5f, 5.3f), 80f);
        Create(root, "Crate", new Vector2(5f, -1.8f), -20f);
        Create(root, "Crate", new Vector2(5.6f, -1.1f), 10f);
        Create(root, "Barrel", new Vector2(-5.3f, 0.2f), 0f);
        Create(root, "Barrel", new Vector2(-5f, -0.6f), 0f);

        // 円周を走る車
        var movingCar = Create(root, "Car", new Vector2(3.6f, 0f), 0f);
        movingCar.name = "Car (Moving)";
        var mover = movingCar.AddComponent<SpriteStackDemoMover>();
        mover.mode = SpriteStackDemoMover.Mode.Circle;
        mover.radius = 3.6f;
        mover.speed = 35f;

        // その場で回る樽
        var spinBarrel = Create(root, "Barrel", new Vector2(1.7f, 1.3f), 0f);
        spinBarrel.name = "Barrel (Spin)";
        var spin = spinBarrel.AddComponent<SpriteStackDemoMover>();
        spin.mode = SpriteStackDemoMover.Mode.Spin;
        spin.speed = 60f;

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[SpriteStack] Demo scene created: {ScenePath}  (Play で確認できます)");
    }

    static GameObject Create(Transform parent, string model, Vector2 pos, float rotation)
    {
        int layers = 1;
        foreach (var m in SpriteStackTestSpriteGenerator.Models)
            if (m.Name == model) layers = m.Layers;

        var go = new GameObject(model);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y, 0f), Quaternion.Euler(0, 0, rotation));

        var stack = go.AddComponent<SpriteStack>();
        stack.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteStackTestSpriteGenerator.SheetPath(model));
        stack.sliceCount = layers;
        stack.layout = SpriteStack.SheetLayout.Horizontal;
        stack.pixelsPerUnit = SpriteStackTestSpriteGenerator.PixelsPerUnit;
        stack.layerHeightPixels = 1f;
        stack.subSteps = 2; // 高さ倍率 2 倍まで隙間なく描画
        stack.MarkDirty();
        return go;
    }
}
