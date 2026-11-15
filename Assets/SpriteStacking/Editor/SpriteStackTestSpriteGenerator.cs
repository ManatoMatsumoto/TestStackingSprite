using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// スプライトスタッキング用のテストスプライト（スライス済みシート）を手続き的に生成するエディタ拡張。
/// 各モデルは「ボクセル関数 (x, y, z) -> 色」で定義し、z（高さ）ごとに1枚のスライスとして
/// 横一列（左 = 最下層、右 = 最上層）に並べた PNG を出力する。
/// </summary>
public static class SpriteStackTestSpriteGenerator
{
    public const string OutputDir = "Assets/SpriteStacking/Sprites";
    public const int PixelsPerUnit = 16;

    public struct ModelInfo
    {
        public string Name;
        public int Width, Height, Layers;
        public Func<int, int, int, Color32> Voxel;
    }

    public static readonly ModelInfo[] Models =
    {
        new ModelInfo { Name = "Car",    Width = 16, Height = 28, Layers = 11, Voxel = Car },
        new ModelInfo { Name = "House",  Width = 24, Height = 24, Layers = 22, Voxel = House },
        new ModelInfo { Name = "Tree",   Width = 20, Height = 20, Layers = 26, Voxel = Tree },
        new ModelInfo { Name = "Barrel", Width = 12, Height = 12, Layers = 14, Voxel = Barrel },
        new ModelInfo { Name = "Crate",  Width = 14, Height = 14, Layers = 13, Voxel = Crate },
    };

    public static string SheetPath(string name) => $"{OutputDir}/{name}_stack.png";
    public static string GrassPath => $"{OutputDir}/Grass.png";

    [MenuItem("Tools/Sprite Stacking/Generate Test Sprites")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(OutputDir);

        foreach (var m in Models)
        {
            var tex = BuildStrip(m.Width, m.Height, m.Layers, m.Voxel);
            WritePng(tex, SheetPath(m.Name));
        }
        WritePng(BuildGrass(32), GrassPath);

        AssetDatabase.Refresh();
        foreach (var m in Models) ConfigureImporter(SheetPath(m.Name));
        ConfigureImporter(GrassPath);

        Debug.Log($"[SpriteStack] Generated {Models.Length} test sprite sheets in {OutputDir}");
    }

    // ------------------------------------------------------------------
    // Sheet building
    // ------------------------------------------------------------------

    static readonly Color32 Empty = new Color32(0, 0, 0, 0);
    static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

    static Texture2D BuildStrip(int w, int h, int layers, Func<int, int, int, Color32> voxel)
    {
        int tw = w * layers;
        var src = new Color32[tw * h];
        for (int z = 0; z < layers; z++)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    src[y * tw + z * w + x] = voxel(x, y, z);

        bool Opaque(int x, int y, int z)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            return src[y * tw + z * w + x].a > 0;
        }

        // 陰影付け: 上の層ほど明るく、各スライスの輪郭（側面として見える部分）を少し暗くする
        var dst = new Color32[src.Length];
        for (int z = 0; z < layers; z++)
        {
            float heightShade = Mathf.Lerp(0.8f, 1.05f, layers > 1 ? z / (float)(layers - 1) : 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * tw + z * w + x;
                    var c = src[i];
                    if (c.a == 0) { dst[i] = Empty; continue; }
                    bool edge = !Opaque(x - 1, y, z) || !Opaque(x + 1, y, z) || !Opaque(x, y - 1, z) || !Opaque(x, y + 1, z);
                    float s = heightShade * (edge ? 0.85f : 1f);
                    dst[i] = new Color32(Mul(c.r, s), Mul(c.g, s), Mul(c.b, s), 255);
                }
        }

        var tex = new Texture2D(tw, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(dst);
        tex.Apply();
        return tex;
    }

    static byte Mul(byte v, float s) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * s), 0, 255);

    static Texture2D BuildGrass(int size)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Hash(x, y, 7);
                var c = C(86, 156, 70);
                float s = 0.92f + n * 0.14f;
                if (Hash(x, y, 11) > 0.93f) s *= 0.82f;              // 濃い草
                c = new Color32(Mul(c.r, s), Mul(c.g, s), Mul(c.b, s), 255);
                if (Hash(x, y, 23) > 0.995f) c = C(240, 220, 90);      // 小さな花
                px[y * size + x] = c;
            }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static void WritePng(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    static void ConfigureImporter(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = PixelsPerUnit;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // Tiled 描画に必要
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    static float Hash(int x, int y, int z)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFF) / 65535f;
        }
    }

    // ------------------------------------------------------------------
    // Voxel models  (x: 右, y: 前方(画像の上), z: 高さ)
    // ------------------------------------------------------------------

    static Color32 Car(int x, int y, int z)
    {
        var red = C(205, 45, 50); var redDark = C(150, 30, 40);
        var tire = C(35, 35, 40); var rim = C(175, 175, 185);
        var glass = C(140, 200, 240); var bumper = C(120, 120, 130);
        var head = C(255, 235, 130); var tail = C(255, 70, 70); var stripe = C(245, 245, 245);

        bool wheelX = (x >= 1 && x <= 3) || (x >= 12 && x <= 14);
        bool wheelY = (y >= 4 && y <= 8) || (y >= 19 && y <= 23);
        if (z <= 3 && wheelX && wheelY)
        {
            bool outer = x == 1 || x == 14;
            bool hub = y == 6 || y == 21;
            return outer && hub && (z == 1 || z == 2) ? rim : tire;
        }

        if (z >= 2 && z <= 6 && x >= 2 && x <= 13 && y >= 1 && y <= 26)
        {
            bool lampX = x == 3 || x == 4 || x == 11 || x == 12;
            if (y == 26 && z == 5 && lampX) return head;
            if (y == 1 && z == 5 && lampX) return tail;
            if ((y == 1 || y == 26) && z <= 3) return bumper;
            if (z == 6 && (x == 7 || x == 8) && (y > 19 || y < 9)) return stripe;
            return z == 2 ? redDark : red;
        }

        if (z >= 7 && z <= 10)
        {
            int inset = (z - 7) / 2;
            int x0 = 3 + inset, x1 = 12 - inset, y0 = 9 + inset, y1 = 19 - inset;
            if (x < x0 || x > x1 || y < y0 || y > y1) return Empty;
            if (z == 10) return (x == 7 || x == 8) ? stripe : red;
            bool ex = x == x0 || x == x1, ey = y == y0 || y == y1;
            if (ex && ey) return red;          // ピラー
            if (ex || ey) return glass;        // 窓
            return C(70, 40, 40);              // 車内
        }
        return Empty;
    }

    static Color32 House(int x, int y, int z)
    {
        var stone = C(135, 135, 140); var wall = C(232, 214, 176); var beam = C(120, 80, 50);
        var glass = C(120, 180, 230); var door = C(110, 70, 40);
        var roofA = C(185, 72, 55); var roofB = C(150, 55, 45); var chimney = C(150, 90, 80);

        if (z >= 10 && z <= 21 && x >= 16 && x <= 17 && y >= 13 && y <= 15)
            return z == 21 ? C(55, 45, 45) : chimney;

        if (z == 0)
            return (x >= 1 && x <= 22 && y >= 1 && y <= 22) ? stone : Empty;

        if (z <= 11)
        {
            if (x < 3 || x > 20 || y < 3 || y > 20) return Empty;
            bool bx = x == 3 || x == 20, by = y == 3 || y == 20;
            if (bx && by) return beam;
            if (z == 11) return beam;
            if (bx || by)
            {
                bool doorZone = y == 3 && x >= 9 && x <= 14;
                if (y == 3 && x >= 10 && x <= 13 && z <= 7) return door;
                int along = bx ? y : x;
                int m = (along - 3) % 6;
                if (!doorZone && z >= 5 && z <= 7 && (m == 2 || m == 3)) return glass;
                return wall;
            }
            return wall;
        }

        if (z <= 21)
        {
            int k = z - 12;
            int x0 = 1 + k, x1 = 22 - k;
            if (x < x0 || x > x1 || y < 2 || y > 21) return Empty;
            bool slopeEdge = x <= x0 + 1 || x >= x1 - 1;
            if ((y == 2 || y == 21) && !slopeEdge) return wall; // 妻壁
            return (y % 3) == 0 ? roofB : roofA;
        }
        return Empty;
    }

    static Color32 Tree(int x, int y, int z)
    {
        float dx = x - 9.5f, dy = y - 9.5f;
        float n = Hash(x, y, z);

        // 樹冠 (いくつかの球の和集合 + ノイズ)
        float best = 99f;
        best = Mathf.Min(best, Blob(dx, dy, z, 0f, 0f, 16f, 7.2f));
        best = Mathf.Min(best, Blob(dx, dy, z, -2.5f, 1.5f, 21f, 4.5f));
        best = Mathf.Min(best, Blob(dx, dy, z, 2.5f, -2f, 20f, 4.2f));
        best = Mathf.Min(best, Blob(dx, dy, z, 0f, 0f, 23f, 3f));
        if (best < 1f + (n - 0.5f) * 0.2f)
        {
            if (best > 0.85f && Hash(x, y, z + 101) > 0.97f) return C(220, 40, 40); // 実
            float s = 0.85f + n * 0.25f;
            var g = C(70, 150, 60);
            return new Color32(Mul(g.r, s), Mul(g.g, s), Mul(g.b, s), 255);
        }

        // 幹
        float r = z == 0 ? 2.6f : 1.8f;
        if (z <= 14 && dx * dx + dy * dy <= r * r)
        {
            var bark = C(115, 78, 42);
            float s = 0.85f + Hash(x, y, z / 2) * 0.25f;
            return new Color32(Mul(bark.r, s), Mul(bark.g, s), Mul(bark.b, s), 255);
        }
        return Empty;
    }

    static float Blob(float dx, float dy, float z, float cx, float cy, float cz, float r)
    {
        float a = dx - cx, b = dy - cy, c = z - cz;
        return Mathf.Sqrt(a * a + b * b + c * c) / r;
    }

    static Color32 Barrel(int x, int y, int z)
    {
        float dx = x - 5.5f, dy = y - 5.5f;
        float t = (z - 6.5f) / 6.5f;
        float r = 4.3f + 0.9f * (1f - t * t);
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        if (d > r) return Empty;

        if (z == 13)
        {
            if (d > r - 1f) return C(105, 65, 35);
            return (x % 3 == 0) ? C(150, 105, 60) : C(175, 125, 75);
        }
        if (z == 2 || z == 3 || z == 10 || z == 11) return C(110, 110, 120);
        int stave = (int)((Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI) * 14f);
        return stave % 2 == 0 ? C(155, 98, 52) : C(135, 85, 45);
    }

    static Color32 Crate(int x, int y, int z)
    {
        if (x < 1 || x > 12 || y < 1 || y > 12) return Empty;
        var frame = C(120, 75, 35); var plank = C(200, 150, 85); var plankDark = C(175, 128, 70);

        bool ex = x == 1 || x == 12, ey = y == 1 || y == 12, ez = z == 0 || z == 12;
        int edges = (ex ? 1 : 0) + (ey ? 1 : 0) + (ez ? 1 : 0);
        if (edges >= 2) return frame;
        if (ez)
        {
            if (x == y || x == 13 - y) return frame;
            return y % 3 == 0 ? plankDark : plank;
        }
        if (ex || ey)
        {
            int a = ex ? y : x;
            if (a - 1 == z || 12 - a == z) return frame;
            return z % 3 == 0 ? plankDark : plank;
        }
        return plank;
    }
}
