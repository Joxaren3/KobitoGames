# 小人パルクール（仮）

小人になって巨大な部屋を駆け抜ける、3分で遊べるパルクールアクション。
2026年2月16日の展示に向けて制作中。

## 遊び方
| 操作 | キーボード | ゲームパッド |
|---|---|---|
| 移動 | WASD / 矢印キー | 左スティック |
| ジャンプ / 壁ジャンプ | Space | A（南ボタン） |
| カメラ | マウス | 右スティック |

走りながら壁に近づくと壁走り。壁走り中にジャンプで反対側へ飛べる。

## 開発環境
- Unity 6（URP 推奨）
- Input System パッケージ（Package Manager からインストール）

## セットアップ
1. Unity Hub で新規 3D(URP) プロジェクトを作成し、このリポジトリのファイルをプロジェクト直下に置く
2. Package Manager で **Input System** をインストール（再起動を求められたら Yes）
3. シーンに以下を配置
   - **Player**: Capsule を作成 → Capsule Collider を削除 → `CharacterController`、`PlayerController`、`PlayerRespawn` を追加
   - **Main Camera**: `ThirdPersonCamera` を追加し、Target に Player を入れる
   - **足場・壁**: Cube を並べて灰色のテストコースを作る
   - **チェックポイント**: 空の GameObject に Box Collider（Is Trigger）と `Checkpoint`
4. カメラの Collision Mask から Player のレイヤーを外す（Player レイヤーを作って割り当てる）

## 調整のコツ
操作感は `PlayerController` のインスペクターで全部いじれる。
まず `maxSpeed` と `jumpHeight` を決めてから、`gravity` と `fallGravityMultiplier` でジャンプの「重さ」を調整すると決まりやすい。

## スクリプト構成
```
Assets/Scripts/
├── Player/
│   ├── PlayerController.cs   走る・ジャンプ・壁走り・壁ジャンプ
│   └── ThirdPersonCamera.cs  三人称カメラ（速度でFOV拡大、壁走りで傾き）
└── Gameplay/
    ├── PlayerRespawn.cs      落下時に即リスポーン
    ├── Checkpoint.cs         リスポーン地点の更新
    └── KillZone.cs           触れたらリスポーン
```

## TODO
- [ ] 灰色の箱でテストコースを作って通しタイムを計る
- [ ] タイマーとリザルト画面
- [ ] ランキング（名前3文字）
- [ ] 放置で自動リセット・デモ画面
- [ ] ジップライン / バウンド床
- [ ] 部屋のモデル・演出
