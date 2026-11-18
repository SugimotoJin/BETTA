# Betta Desktop — デスクトップを泳ぐベタ

Windows のデスクトップの上を、リアルなベタ（闘魚）が泳ぐ常駐アプリの Unity プロジェクトです。

- 画面全体が透明な水槽になり、クリックは下のウィンドウに通ります（作業の邪魔をしません）
- タスクトレイのアイコンから、設定・エサやり・ベタの追加・終了
- 色・形・模様・ヒレの長さ・胸びれ（ダンボ）などを自由に変更。設定は自動で保存
- カーソルを止めると寄ってきて、ゆっくり動かすとついてくる。ベタ同士が向かい合うとフレア
- 複数モニター対応（泳がせるモニターを選べる）、30fps / 60fps

ベタの生成と泳ぎは [Betta Generator](https://github.com/SugimotoJin/betta-generator) パッケージを使っています（v0.1.0 を `Packages/com.kyabe.betta` にそのまま取り込み済み）。

## ビルド

1. Unity 6（6000.3）でこのフォルダを開く
2. メニュー **Betta → デスクトップ版をビルド（Windows）**
3. `Builds/BettaDesktop/BettaDesktop.exe` を起動

エディタで試すときは **Betta → デスクトップ版のシーンを作って開く** で再生します（透明化はビルド版のみ）。

## ライセンス

MIT License（[LICENSE](LICENSE)）。Copyright (c) 2026 Kyabe
