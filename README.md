# wapawapa

Unity6（3.13.f1）でのVRゲーム開発を前提に、Photon Fusion を使ったオンラインマルチプレイ構成の土台を整えたリポジトリです。

## 開発前提

- **Unity**: Unity6 3.13.f1
- **XR**: OpenXR ベース（Meta Quest / PCVR を想定）
- **オンライン**: Photon Fusion（Host/Client 方式を想定）
- **チーム規模**: 6人

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
