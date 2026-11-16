https://x.com/asynkimo/status/2121716250820964431?a=pHpdPMg7GTUKLATqhXg&s=46

添付URLの縦軸(UnityではY軸)にスライスされたSpriteを積層することで3Dに見せるシステムのテストプロジェクトです

ClaudeCode Opus5.5で全て生成しています

---

# 仕様リファレンス

## 目次

- [動作環境](#動作環境)
- [クイックスタート](#クイックスタート)
- [ファイル構成](#ファイル構成)
- [座標系](#座標系)
- [仕組み](#仕組み)
- [SpriteStack](#spritestack)
- [SpriteStackOrbitCamera](#spritestackorbitcamera)
- [SpriteStackDemoMover](#spritestackdemomover)
- [エディタメニュー](#エディタメニュー)
- [スライスシートの仕様](#スライスシートの仕様)
- [自分のスプライトを使う](#自分のスプライトを使う)
- [制約・既知の注意点](#制約既知の注意点)

## 動作環境

| 項目 | 内容 |
|---|---|
| Unity | 6000.0.82f1 (Unity 6) |
| レンダーパイプライン | URP (Universal Renderer) |
| 入力 | Input System（旧 Input Manager にもフォールバック対応） |
| 追加パッケージ | 不要 |

## クイックスタート

1. メニュー **Tools → Sprite Stacking → Create Demo Scene** を実行する
   - テスト用スプライトが `Assets/SpriteStacking/Sprites/` に生成される
   - デモシーン `Assets/Scenes/SpriteStackDemo.unity` が作成されて開く
2. **Play** で実行する

### デモの操作

| 入力 | 動作 |
|---|---|
| ← / →（A / D） | カメラを左右に回り込ませる |
| ↑ / ↓（W / S） | カメラの傾き（↑ でより斜め、↓ でより真上） |
| マウスドラッグ（左 / 右ボタン） | 横 = 回り込み、縦 = 傾き |
| マウスホイール | ズーム |
| Space | 自動回転 ON / OFF |
| R | 回転・傾き・ズームを初期値に戻す |
| H | ヘルプ表示の切り替え |

## ファイル構成

```
Assets/
├─ Scenes/
│  └─ SpriteStackDemo.unity              デモシーン
└─ SpriteStacking/
   ├─ Scripts/                           ランタイム
   │  ├─ SpriteStack.cs                  積層描画の本体
   │  ├─ SpriteStackOrbitCamera.cs       確認用オービットカメラ
   │  └─ SpriteStackDemoMover.cs         デモ用の移動・回転
   ├─ Editor/                            エディタ拡張
   │  ├─ SpriteStackTestSpriteGenerator.cs   テスト用スプライト生成
   │  └─ SpriteStackDemoSceneBuilder.cs      デモシーン構築
   └─ Sprites/                           生成されたスライスシート (PNG)
```

## 座標系

| 方向 | ワールド軸 |
|---|---|
| 地面 | XY 平面（Z = 0） |
| 高さ（上） | **-Z**（カメラ側） |
| モデルの前方 | ローカル +Y（スライス画像の上方向） |
| モデルの向き | Z 軸まわりの回転 |

デフォルトのカメラは +Z 方向を見下ろし、画面の上が +Y になります。
スライス画像の「縦軸」は、ワールドでは地面上の前後方向（+Y）にあたります。積み上げる方向は -Z です。

## 仕組み

1. 立体を高さ方向に輪切りにした画像（スライス）を用意する
2. `SpriteStack` がスライスごとに子オブジェクトとして `SpriteRenderer` を生成する
3. 毎フレーム `LateUpdate` で、各スライスを高さに応じた位置へ配置する
   - **World3D**（既定）: 実際の高さ（-Z）に地面と平行に置く。カメラを傾けると、地面とスライスが同じように奥行き方向へ縮み、層のずれ量も傾きに応じて変わる
   - **ScreenOffset**: 地面と同じ平面上で、カメラの画面上方向へずらす（真上視点専用の、従来の 2D 手法）
4. 回転はルートオブジェクトの回転を全スライスが継承する。そのため、オブジェクトを回すとモデルが回り、カメラを回すと別の角度から見える
5. 描画順
   - スタック内: 下の層ほど小さい `sortingOrder`
   - スタック同士: ルートの `SortingGroup.sortingOrder` を、画面上の奥行き（カメラの奥方向への射影）から毎フレーム計算する。画面の奥にあるものほど先に描かれる

## SpriteStack

`[ExecuteAlways]` `[DisallowMultipleComponent]` `[RequireComponent(typeof(SortingGroup))]`

積層スプライトを描画するコンポーネントです。エディタで再生していないときもプレビューされます。

### Inspector プロパティ

#### Mode

| プロパティ | 型 | 既定値 | 説明 |
|---|---|---|---|
| `mode` | `StackMode` | `World3D` | 積み上げ方式。下表参照 |

| `StackMode` | 説明 |
|---|---|
| `World3D` | スライスを実際の高さ（-Z）に配置する。傾けたカメラで正しく見える |
| `ScreenOffset` | 地面と同じ平面上で画面の上方向へずらす。真上視点でしか正しく見えない |

#### Source（どちらか一方を指定）

| プロパティ | 型 | 既定値 | 説明 |
|---|---|---|---|
| `slices` | `Sprite[]` | 空 | 下の層 → 上の層の順に並べたスライス。1 つ以上あれば `sheet` より優先される |
| `sheet` | `Texture2D` | なし | スライスを一列に並べたシート画像。実行時に自動で分割する |
| `sliceCount` | `int` (≥1) | 1 | `sheet` に含まれるスライスの数 |
| `layout` | `SheetLayout` | `Horizontal` | シートの並び方向。下表参照 |
| `reverseOrder` | `bool` | false | シートの並び順を逆にする |
| `pixelsPerUnit` | `float` | 16 | `sheet` から作るスプライトの Pixels Per Unit |

| `SheetLayout` | 並び |
|---|---|
| `Horizontal` | 左端が最下層、右へ行くほど上の層 |
| `Vertical` | 上端が最下層、下へ行くほど上の層 |

#### Stacking

| プロパティ | 型 | 既定値 | 説明 |
|---|---|---|---|
| `layerHeightPixels` | `float` | 1 | 1 層あたりの高さ（ピクセル） |
| `heightScale` | `float` | 1 | このスタックの高さ倍率 |
| `subSteps` | `int` (1〜4) | 1 | 層と層の間に挟む補間描画の数。高さ倍率を上げたときの隙間を埋める。最上層には挟まない |
| `tint` | `Color` | 白 | 全スライスの色 |

1 層あたりのワールド上の高さは次の式で決まります。

```
layerHeightPixels / PPU × heightScale × SpriteStack.GlobalHeightScale × |lossyScale.y|
```

PPU には、先頭スライスの `Sprite.pixelsPerUnit` が使われます。

#### Sorting

| プロパティ | 型 | 既定値 | 説明 |
|---|---|---|---|
| `sortingOrderPerUnit` | `int` | 100 | 画面上の奥行き 1 ユニットあたりの `sortingOrder` の変化量 |
| `sortingOrderOffset` | `int` | 0 | `sortingOrder` に加算する値 |

`SortingGroup.sortingOrder` は -32000〜32000 に収まるよう切り詰められます。

### スクリプト API

| メンバー | 説明 |
|---|---|
| `static float GlobalHeightScale` | 全スタック共通の高さ倍率（既定 1） |
| `int LayerCount { get; }` | 現在のスライス数 |
| `void Refresh()` | 必要ならスライスを作り直し、配置と描画順を更新する。通常は `LateUpdate` で自動的に呼ばれる |
| `void MarkDirty()` | 次の `Refresh` でスライスを作り直す。スクリプトから Source などを変更した後に呼ぶ |
| `static Vector2 ViewUp()` | メインカメラの「画面上方向」を XY 平面上の単位ベクトルで返す |

### 生成される子オブジェクト

- 名前: `__Slice_<層番号>`（`subSteps` > 1 のときは `__Slice_<層番号>_<補間番号>`）
- `HideFlags.DontSave` が付くため、シーンには保存されず、有効化されるたびに作り直される
- `__Slice_` で始まる子オブジェクトは、作り直すときに削除される。この名前を自分のオブジェクトに付けないこと
- `sheet` から作った `Sprite` は、スライスを作り直すときと `OnDestroy` で破棄される

## SpriteStackOrbitCamera

`[RequireComponent(typeof(Camera))]` `[DefaultExecutionOrder(-100)]`

注視点を中心に、ヨー（回り込み）とピッチ（傾き）で動く正投影カメラです。
`SpriteStack` より先に更新されるよう、実行順を早めています。`Awake` でカメラを正投影に切り替え、`SpriteStack.GlobalHeightScale` を 1 に戻します。

### Inspector プロパティ

| グループ | プロパティ | 既定値 | 説明 |
|---|---|---|---|
| Target | `target` | なし | 注視対象。指定がなければ `targetPoint` を使う |
| | `targetPoint` | (0, 0) | 注視点 |
| | `distance` | 20 | 注視点からの距離。傾きとズームに応じて、必要な距離まで自動で延ばされる |
| Yaw | `angle` | 0 | 回り込み角度（度）。実行中は現在値が入る |
| | `keyRotateSpeed` | 120 | キー操作の回転速度（度/秒） |
| | `mouseSensitivity` | 0.3 | ドラッグ感度（度/ピクセル）。回り込みと傾きで共通 |
| | `autoRotate` | false | 自動回転 |
| | `autoRotateSpeed` | 30 | 自動回転の速度（度/秒） |
| Pitch | `pitch` | 50 | 傾き（度）。0 = 真上 |
| | `minPitch` / `maxPitch` | 0 / 70 | 傾きの範囲 |
| | `keyPitchSpeed` | 60 | キー操作の傾き速度（度/秒） |
| Zoom | `minZoom` / `maxZoom` | 1.5 / 15 | `orthographicSize` の範囲 |
| | `zoomStep` | 0.88 | ホイール 1 回あたりのズーム倍率 |
| Misc | `smoothing` | 12 | 回転・傾き・ズームの補間の速さ |
| | `showHelp` | true | 操作ヘルプを画面に表示する |

### カメラの姿勢

```
rotation = Euler(0, 0, yaw) * Euler(-pitch, 0, 0)
position = 注視点 - forward * d
d = max(distance, (halfHeight + halfWidth) * tan(pitch) + 5)
farClipPlane = d + (halfHeight + halfWidth) * tan(pitch) + 20
```

傾けると画面手前の地面がカメラに近づくため、距離 `d` とファークリップを毎フレーム調整し、地面が切れないようにしています。

## SpriteStackDemoMover

デモ用のコンポーネントです。モデルの前方はローカル +Y です。

| プロパティ | 既定値 | 説明 |
|---|---|---|
| `mode` | `Circle` | `Circle`: 円周上を進行方向を向いて走る / `Spin`: その場で回転する |
| `center` | (0, 0) | 円の中心（`Circle` のとき） |
| `radius` | 3.5 | 円の半径（`Circle` のとき） |
| `speed` | 40 | 角速度（度/秒）。正の値で反時計回り |
| `angle` | 0 | 現在の角度 |

## エディタメニュー

| メニュー | 動作 |
|---|---|
| **Tools → Sprite Stacking → Generate Test Sprites** | テスト用スライスシートと草地テクスチャを `Assets/SpriteStacking/Sprites/` に生成する（既存のファイルは上書き） |
| **Tools → Sprite Stacking → Create Demo Scene** | スプライトを生成し、デモシーンを `Assets/Scenes/SpriteStackDemo.unity` に作成して開く（既存のシーンは上書き。未保存のシーンがあれば保存を確認する） |

### 生成されるテスト用スプライト

| ファイル | 1 スライスのサイズ (px) | 層数 |
|---|---|---|
| `Car_stack.png` | 16 × 28 | 11 |
| `House_stack.png` | 24 × 24 | 22 |
| `Tree_stack.png` | 20 × 20 | 26 |
| `Barrel_stack.png` | 12 × 12 | 14 |
| `Crate_stack.png` | 14 × 14 | 13 |
| `Grass.png` | 32 × 32 | —（地面用のタイル） |

- 各モデルは、位置 (x, y, z) から色を返す関数で定義し、z ごとに輪切りにしてシートにしている
- 上の層ほど明るく、各スライスの輪郭を少し暗くして立体感を付けている
- インポート設定: Sprite (Single)、PPU 16、Point フィルタ、非圧縮、ミップマップなし、Mesh Type Full Rect

### デモシーンの内容

- `Main Camera`: 正投影、`orthographicSize` 5、`SpriteStackOrbitCamera` 付き
- `Ground`: 草地スプライトを Tiled で 80 × 80 ユニットに敷き詰めたもの（`sortingOrder` -32700）
- `StackedObjects`: 家・木・車・樽・木箱。円周を走る車とその場で回る樽を含む。`subSteps` は 2

## スライスシートの仕様

- すべてのスライスを同じサイズで一列に並べる（隙間なし）
- `Horizontal` の場合: 画像の幅 = 1 スライスの幅 × `sliceCount`
- `Vertical` の場合: 画像の高さ = 1 スライスの高さ × `sliceCount`
- 各スライスは真上から見た断面で、画像の上がモデルの前方になる
- ピボットはスライスの中心

推奨のインポート設定:

| 項目 | 値 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Filter Mode | Point (no filter) |
| Compression | None |
| Generate Mip Maps | オフ |

`sheet` から作るスプライトは Mesh Type Full Rect で生成されるため、テクスチャの Read/Write は不要です。

## 自分のスプライトを使う

### シート画像を使う場合

1. スライスを一列に並べた PNG をインポートし、上記の推奨設定にする
2. 空の GameObject に `SpriteStack` を追加する（`SortingGroup` は自動で追加される）
3. `sheet`・`sliceCount`・`layout`・`pixelsPerUnit` を設定する

### Sprite Editor でスライスした場合

1. テクスチャを Sprite Mode = Multiple にしてスライスする
2. `slices` に、下の層から順にスプライトを並べる

### シーンの条件

- `MainCamera` タグが付いたカメラが必要（`Camera.main` から向きを取得するため）
- 地面などの平らなスプライトは、積層オブジェクトより十分小さい `sortingOrder` にする（デモでは -32700）

## 制約・既知の注意点

- オブジェクト同士の前後関係は、ルートの位置だけで決まる。大きなオブジェクトや重なったオブジェクトでは、前後が入れ替わって見えることがある
- `World3D` で真上（傾き 0°）から見ると、すべての層が重なって上面だけが見える。これは正しい見え方
- 傾きを大きくするとスライスが真横に近づき、薄く見える。そのためカメラの傾きの上限を 70° にしている
- 斜めから見ると、ドット絵が縦につぶれたり、細い線がチラついたりすることがある
- 回転は Z 軸まわりだけに対応している。X / Y 軸まわりに回すと正しく見えない
- スライスの数 × `subSteps` 個の `SpriteRenderer` を作るため、大量に配置するときは描画負荷に注意する
- スクリプトの `.meta` を手で作ったり、別の場所へコピーしたりするときは、Unity 標準の形式（`MonoImporter` を含むもの）を保つこと。形式が崩れると、シーンからスクリプトが外れることがある
