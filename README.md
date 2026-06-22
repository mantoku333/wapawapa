# wapawapa

Unity 6.3で開発する、Photon Fusion 2ベースの2人用VRマルチプレイプロジェクトです。

## 開発前提

- **Unity**: Unity 6.3 (`6000.3.13f1`)
- **XR**: OpenXR ベース（Meta Quest / PCVR を想定）
- **オンライン**: Photon Fusion 2.0.12（Shared Mode）
- **チーム規模**: 6人

## 現在の動作

1. `Title`シーンで3文字以上のルームキーを入力
2. 同じキーを使った最大2人が同じ非公開Fusionセッションへ接続
3. `Game`シーンへ移動し、各プレイヤーの位置・頭・両手を同期

デスクトップでは`WASD`で移動、マウスで視点操作します。`Esc`でカーソルを解放し、左クリックで再ロックします。XRランタイムが有効な場合はHMDと左右コントローラーを使用し、左スティックで移動、右スティックで旋回します。

StandaloneはHMDなしでも起動できるよう、標準ではデスクトップモードです。PCVRとして起動するときは実行ファイルへ`-enableXR`を渡してください。Quest向けAndroidビルドはOpenXRを自動初期化します。

## ローカル2人テスト

1. Build Settingsの先頭が`Title`、次が`Game`であることを確認
2. Standaloneビルドを作成して起動
3. Unity Editorでも`Title`をPlay
4. 両方で同じルームキーを入力

Photon App IDは`Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`で設定します。SDK更新時はこのファイルと`NetworkProjectConfig.fusion`を保持してください。

## 推奨フォルダ構成

```text
.
├── Assets/
│   ├── _Project/
│   │   ├── Addressables/
│   │   ├── Art/
│   │   ├── Audio/
│   │   ├── Materials/
│   │   ├── Prefabs/
│   │   ├── Scenes/
│   │   ├── ScriptableObjects/
│   │   ├── Scripts/
│   │   │   ├── Core/
│   │   │   ├── Gameplay/
│   │   │   ├── Network/
│   │   │   │   └── Fusion/
│   │   │   ├── UI/
│   │   │   └── VR/
│   │   └── XR/
│   └── Plugins/
│       └── PhotonFusion/
├── Packages/
└── ProjectSettings/
```

### フォルダ運用ルール（最小）

- `Scenes/`: `Boot`, `Lobby`, `Game` などシーン単位で管理
- `Scripts/Network/Fusion/`: `NetworkRunner`, `NetworkObject`, `Rpc` などネットワーク責務を集約
- `Scripts/VR/`: 入力・移動・ハンド操作など XR 専用ロジック
- `Scripts/Core/`: DI/初期化/共通ユーティリティ
- `ScriptableObjects/`: ゲーム設定値、ステージ定義、バランス調整用データ

## 開発フロー（推奨）

- `main`: 常に動く状態
- `feature/<area>-<topic>` でブランチ作成
- PR 単位でレビュー（最低 1 approval）
- マルチプレイ変更は **Host + Client の2窓検証** を必須

## セットアップメモ

1. Unity Hub で Unity6 3.13.f1 をインストール
2. 本リポジトリを Unity プロジェクトとして開く
3. Photon Fusion パッケージを導入し、`Assets/Plugins/PhotonFusion/` 配下で管理
4. `Boot` シーンから初期化して `Lobby` へ遷移する構成を基準に実装

---

まずはこの構成で「責務の分離」と「6人での並行開発」を優先し、機能追加時にフォルダを拡張してください。
