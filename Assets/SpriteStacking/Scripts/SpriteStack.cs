using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// スプライトスタッキング (Sprite Stacking) レンダラー。
///
/// 高さ方向に輪切りにしたスライス画像を、下の層から順に「画面の上方向」へ少しずつずらして
/// 重ね描きすることで、2D スプライトだけで 3D のような立体感を出す。
///
/// ・オブジェクトの回転 (Z 軸) は全スライスにそのまま適用される（＝モデルが回る）
/// ・積み上げ方向は常に「カメラの上方向」なので、カメラを回すと別の角度から見た絵になる
/// ・オブジェクト同士の前後関係は SortingGroup の sortingOrder を画面上の奥行きから毎フレーム計算
///
/// スライスの指定方法は2通り:
///   1) slices に下→上の順で Sprite を並べる（Sprite Editor でスライスした場合など）
///   2) sheet にスライスを一列に並べたテクスチャを指定し、sliceCount を設定（実行時に自動分割）
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SortingGroup))]
public class SpriteStack : MonoBehaviour
{
    public enum SheetLayout
    {
        /// <summary>左端が最下層、右へ行くほど上の層</summary>
        Horizontal,
        /// <summary>上端が最下層、下へ行くほど上の層</summary>
        Vertical,
    }

    const string SliceNamePrefix = "__Slice_";

    /// <summary>全スタック共通の高さ倍率（カメラの「傾き」の擬似表現としてデモで使用）</summary>
    public static float GlobalHeightScale = 1f;

    [Header("Source: Sprite 配列 (設定されていれば優先)")]
    [Tooltip("下の層 → 上の層 の順に並べたスライス")]
    public Sprite[] slices;

    [Header("Source: スライスシート")]
    public Texture2D sheet;
    [Min(1)] public int sliceCount = 1;
    public SheetLayout layout = SheetLayout.Horizontal;
    [Tooltip("シートの並び順を逆にする")]
    public bool reverseOrder;
    [Min(0.01f)] public float pixelsPerUnit = 16f;

    [Header("Stacking")]
    [Tooltip("1層あたりの高さ（ピクセル単位）")]
    [Min(0f)] public float layerHeightPixels = 1f;
    [Min(0f)] public float heightScale = 1f;
    [Tooltip("層の間を埋める補間描画の数。高さ倍率を 1 より大きくするとき隙間が出ないように増やす")]
    [Range(1, 4)] public int subSteps = 1;
    public Color tint = Color.white;

    [Header("Sorting")]
    [Tooltip("画面上の奥行き 1 ユニットあたりの sortingOrder の変化量")]
    public int sortingOrderPerUnit = 100;
    public int sortingOrderOffset;

    readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    readonly List<float> layerHeights = new List<float>();
    readonly List<Sprite> generatedSprites = new List<Sprite>();
    SortingGroup sortingGroup;
    float unitsPerPixel = 1f / 16f;
    bool dirty = true;

    public int LayerCount { get; private set; }

    void OnEnable()
    {
        sortingGroup = GetComponent<SortingGroup>();
        dirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += EditorDelayedRefresh;
#endif
    }

    void OnDisable()
    {
        // コンポーネントだけ無効化された場合もスライスを非表示にする（再有効化時に作り直す）
        foreach (var sr in renderers)
            if (sr != null) sr.enabled = false;
    }

    void OnValidate()
    {
        dirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += EditorDelayedRefresh;
#endif
    }

#if UNITY_EDITOR
    void EditorDelayedRefresh()
    {
        if (this == null || !isActiveAndEnabled) return;
        Refresh();
    }
#endif

    void OnDestroy()
    {
        DestroyGeneratedSprites();
    }

    void LateUpdate()
    {
        Refresh();
    }

    /// <summary>必要ならスライスを再構築し、カメラ方向に合わせて配置を更新する。</summary>
    public void Refresh()
    {
        if (dirty)
        {
            dirty = false;
            Rebuild();
        }
        UpdateLayout();
    }

    /// <summary>設定を変更した後に呼ぶとスライスを作り直す。</summary>
    public void MarkDirty() => dirty = true;

    // ------------------------------------------------------------------

    void Rebuild()
    {
        ClearSlices();

        var source = CollectSlices();
        LayerCount = source.Count;
        if (source.Count == 0) return;

        unitsPerPixel = 1f / (source[0] != null ? source[0].pixelsPerUnit : pixelsPerUnit);

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;
            int steps = i == source.Count - 1 ? 1 : subSteps; // 最上層は補間しない
            for (int s = 0; s < steps; s++)
            {
                var go = new GameObject(SliceNamePrefix + i + (steps > 1 ? "_" + s : ""));
                go.hideFlags = HideFlags.DontSave; // シーンには保存せず、毎回生成する
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = source[i];
                sr.color = tint;
                sr.sortingOrder = renderers.Count; // 下の層から順に描画
                renderers.Add(sr);
                layerHeights.Add(i + s / (float)steps);
            }
        }
    }

    List<Sprite> CollectSlices()
    {
        var list = new List<Sprite>();
        if (slices != null && slices.Length > 0)
        {
            list.AddRange(slices);
            return list;
        }
        if (sheet == null || sliceCount <= 0) return list;

        int sw = layout == SheetLayout.Horizontal ? sheet.width / sliceCount : sheet.width;
        int sh = layout == SheetLayout.Horizontal ? sheet.height : sheet.height / sliceCount;
        if (sw <= 0 || sh <= 0) return list;

        for (int i = 0; i < sliceCount; i++)
        {
            int idx = reverseOrder ? sliceCount - 1 - i : i;
            Rect rect = layout == SheetLayout.Horizontal
                ? new Rect(idx * sw, 0, sw, sh)
                : new Rect(0, sheet.height - (idx + 1) * sh, sw, sh);
            var sprite = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = $"{sheet.name}_slice{i}";
            sprite.hideFlags = HideFlags.DontSave;
            generatedSprites.Add(sprite);
            list.Add(sprite);
        }
        return list;
    }

    void UpdateLayout()
    {
        if (renderers.Count == 0) return;

        Vector2 up = ViewUp();
        float scale = Mathf.Abs(transform.lossyScale.y);
        float step = layerHeightPixels * unitsPerPixel * heightScale * GlobalHeightScale * scale;
        Vector3 offset = new Vector3(up.x, up.y, 0f) * step;
        Vector3 basePos = transform.position;

        for (int i = 0; i < renderers.Count; i++)
        {
            var sr = renderers[i];
            if (sr == null) { dirty = true; continue; }
            // 回転はルートから継承（モデルの向き）、位置だけを画面上方向に積み上げる
            sr.transform.position = basePos + offset * layerHeights[i];
        }

        // 画面上で下にあるもの（カメラ上方向への射影が小さいもの）ほど手前に描く
        float depth = Vector2.Dot(new Vector2(basePos.x, basePos.y), up);
        int order = Mathf.RoundToInt(-depth * sortingOrderPerUnit) + sortingOrderOffset;
        sortingGroup.sortingOrder = Mathf.Clamp(order, -32000, 32000);
    }

    /// <summary>メインカメラの「画面上方向」をワールド XY 平面上のベクトルとして返す。</summary>
    public static Vector2 ViewUp()
    {
        var cam = Camera.main;
        if (cam == null) return Vector2.up;
        Vector3 u = cam.transform.up;
        var v = new Vector2(u.x, u.y);
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.up;
    }

    void ClearSlices()
    {
        renderers.Clear();
        layerHeights.Clear();
        DestroyGeneratedSprites();

        // 以前に生成したスライス（ドメインリロード後に残ったものも含む）を削除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith(SliceNamePrefix))
                SafeDestroy(child.gameObject);
        }
    }

    void DestroyGeneratedSprites()
    {
        foreach (var s in generatedSprites)
            if (s != null) SafeDestroy(s);
        generatedSprites.Clear();
    }

    static void SafeDestroy(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
