# Betta Generator（ベタ自動生成）

ベタ（闘魚）を、色・形・模様などのパラメーターから自動で作り、柔らかいヒレの物理と本物らしい泳ぎで動かす Unity パッケージです。

- Unity 6（6000.3 以降）＋ URP（ユニバーサルレンダーパイプライン）
- 同じパラメーターなら、どの環境でも同じ見た目のベタになります

## 入れ方

Unity のメニュー **Window → Package Manager** を開き、左上の **＋** から次のどちらかを選びます。

- **Install package from git URL…** → `https://github.com/SugimotoJin/betta-generator.git`（版を固定するときは末尾に `#v0.1.0`）
- **Install package from disk…** → ダウンロードしたこのフォルダの `package.json` を選ぶ

## いちばん簡単な使い方

1. **GameObject → Betta → 水槽（泳ぐ範囲）** で水槽を置く（青い枠が泳ぐ範囲、上の線が水面）
2. 水槽を選んだまま **GameObject → Betta → 泳ぐベタ** でベタを入れる
3. 再生するとベタが泳ぎます

シーンにはライト（Directional Light）を 1 つ置いてください。カメラは水槽を正面から見る向き（Z 方向）が基本です。

## 見た目を決める

ベタ（BettaFish）を選ぶと、インスペクターで次のことができます。

- **Params**：尾の形、模様、各部の色、ヒレの長さ、胸びれの大きさ（2 以上でダンボ）、泳ぐ速さ など
- **組み込みの見た目を使う**：レッド・ハーフムーンなど 13 種類から選ぶ
- **色をおまかせ / 全部おまかせ / 模様の配置を変える**
- 再生中に変えると、その場で反映されます

## マウスへの反応・エサやり

**GameObject → Betta → マウスへの反応（カーソル・エサやり）** をシーンに置くと、次のことができます。

- カーソルを止めておくと近くのベタが寄ってきて見つめ、ゆっくり動かすとついてくる（**Follow Cursor**）
- クリックした所の水面にエサが落ち、ベタが食べに来る（**Click To Feed**）。エサはしばらく浮いてから沈みます
- ベタ同士が向かい合うとフレアする（これは置かなくても起きます）

コードからは次のように使えます。

```csharp
BettaAquarium.Feed(x, tank);                         // 水槽の x の位置の水面にエサを落とす
BettaAquarium.SetCursor(worldPos, true, Time.deltaTime); // 独自の入力（タッチなど）で「カーソル」を渡す
```

## プリセット（使い回し）

- ベタのインスペクターの **プリセットとして保存…** で、見た目をアセットとして保存できます
- 保存したプリセットをベタの **Preset** 欄に入れると、その見た目で生まれます
- プリセットのファイル（`.asset`）を他のプロジェクトにコピーすれば、同じベタを作れます
- 新しく作るときは、プロジェクトウィンドウで **Create → Betta → ベタのプリセット**

## コードから作る

```csharp
using Betta3D;

// 水槽の中に、プリセットの見た目のベタを 1 匹
BettaSwimmer s = tank.Spawn(myPreset.Create());

// 水槽なしで、位置を指定して
BettaParams p = BettaParams.Preset(0);   // 組み込みの見た目
p.RandomColors();
BettaFish.Create(p, new Vector3(0, 0, 0));
```

コードから作ったベタもビルド版で表示されるように、ビルドのときにベタのシェーダーを自動で「Always Included Shaders」に追加します。

## 見た目の全体設定（ライティング）

シェーダーは、環境光・逆光・映り込みの色を共通の値から読みます。何もしなければ水槽向けの値が使われます。変えるときは `BettaLook` の値を書き換えて `BettaLook.Apply()` を呼んでください。ポストプロセスを使わないカメラ（透明ウィンドウなど）では `BettaLook.ShaderTonemap = true` にすると、シェーダーの中で色調補正をします。

## 付録（Samples）

Package Manager でこのパッケージを選ぶと、**Samples** から読み込めます。

- **Demo**：色や形をその場で変えながら眺めるデモ。メニュー **Betta → デモシーンを作って開く**
- **Desktop App (Windows)**：デスクトップの上をベタが泳ぐ常駐アプリ（透明ウィンドウ、タスクトレイ、エサやり、複数モニター）。メニュー **Betta → デスクトップ版をビルド（Windows）**

## 注意

- ベタは水槽の XY 平面の中を泳ぎ、ターンのときだけ奥行き方向に弧を描きます
- 水槽やベタの親オブジェクトの Scale は 1 のままにして、大きさはベタの `size` で変えてください

## ライセンス

MIT License（[LICENSE.md](LICENSE.md)）。Copyright (c) 2026 Kyabe
