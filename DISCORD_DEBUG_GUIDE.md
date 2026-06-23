# WAPAWAPA 開発・デバッグ手順

Unity 6 / Photon Fusion で、タイトルシーンからルーム接続して2人プレイを確認するための共有メモです。  
Discordにそのまま貼る想定で書いています。

## まず開くシーン

基本はこの順番で確認してください。

1. `Assets/_Project/Scenes/Title.unity`
2. `Assets/_Project/Scenes/Game.unity`

プレイ開始は必ず `Title` シーンから行います。  
`Game` シーンを直接再生すると、Photon接続やプレイヤー生成の流れを通らないので、マルチプレイ確認には使いません。

## シーン構成

### Title

タイトル画面とPhoton接続を担当します。

Hierarchyに最初から置いてあるもの：

- `Title Camera`
- `Room Connection Controller`
  - `Fusion Runner`

`Room Connection Controller` がルームキー入力、Photon接続、Gameシーンへの遷移を担当します。  
`Fusion Runner` は実行時に自動生成せず、最初からHierarchyに置いてあります。

### Game

実際にプレイヤーが存在するシーンです。

接続後、Photon Fusionが `NetworkPlayer.prefab` をSpawnします。  
これは「参加したプレイヤーごとに生成されるもの」なので、実行時生成でOKです。

プレイヤープレハブ：

```text
Assets/_Project/Prefabs/NetworkPlayer.prefab
```

## 1人で動作確認する方法

1. Unityで `Title.unity` を開く
2. Playを押す
3. Room Keyに3文字以上入力する
   - 例: `test123`
4. `CREATE / JOIN` を押す
5. `Game` シーンに移動したら成功

操作：

- 移動: `WASD`
- 視点: 中クリック後にマウス
- 視点: 矢印キー
- ジャンプ: `Space`
- カーソル解除: `Esc`
- 左パンチ: 左クリック
- 右パンチ: 右クリック
- 再ロック: 中クリック

## 2人プレイをデバッグする方法

おすすめはこの2パターンです。

### パターンA: Unity Editor + ビルド版

一番わかりやすい確認方法です。

1. Unity Editorで `Title.unity` を開く
2. macOS / Windows向けにビルドする
3. ビルド版を起動する
4. Unity EditorでもPlayする
5. 両方で同じRoom Keyを入力する
   - 例: `team-test`
6. 両方がGameシーンに入り、2人のプレイヤーが見えればOK

### パターンB: ビルド版を2つ起動

Editorの影響を避けたいときはこちら。

1. ビルド版を2つ起動する
2. それぞれ同じRoom Keyを入力する
3. 2人ともGameシーンに入れるか確認する

macOSで同じアプリを2つ開きたい場合は、Finderでダブルクリックだけだと1つしか開けないことがあります。  
その場合はターミナルから実行すると確実です。

```bash
/path/to/Wapawapa.app/Contents/MacOS/wapawapa
```

## Unity 6で複数画面・複数プレイヤーを出す考え方

Unity EditorのGameビューを増やしても、基本的には同じEditor内の同じ実行状態を見るだけです。  
マルチプレイの「別プレイヤー」として確認したい場合は、別プロセスを起動する必要があります。

使い分けはこうです。

| 方法 | 用途 |
| --- | --- |
| Editorだけ | 1人分の操作確認 |
| Editor + ビルド版 | 普段の2人デバッグ |
| ビルド版2つ | より本番に近い2人デバッグ |
| Unity Multiplayer Play Mode | 導入済みなら複数Editorインスタンス確認に使える |

まずは `Editor + ビルド版` を標準にしてください。  
Photon Fusionの接続確認では、この方法が一番トラブルを切り分けやすいです。

## ビルド前に確認すること

Unity 6ではメニュー名が `Build Settings` ではなく `Build Profiles` になっている場合があります。

確認すること：

1. Scenes In Build の先頭が `Title`
2. 2番目が `Game`
3. Photon App IDが設定済み
4. Consoleに赤エラーが出ていない

Scenes In Buildはこの順番です。

```text
0: Assets/_Project/Scenes/Title.unity
1: Assets/_Project/Scenes/Game.unity
```

`RoomConnectionController` の `gameSceneBuildIndex` は `1` を参照しています。  
なので `Game` シーンのBuild Indexが変わると接続後の遷移に失敗します。

## Consoleで見るログ

接続が成功していると、ConsoleやPlayer Logに以下のようなログが出ます。

```text
Wapawapa player joined. PlayerId=1
Wapawapa local player spawned. PlayerId=1
```

2人目が入ったときは、もう一方にもPlayerIdのログが出ます。

よく見るポイント：

- `Connection failed`
- `Player prefab is not configured`
- `NetworkRunner is not configured in the scene`
- `NetworkSceneManagerDefault is not configured in the scene`

これらが出た場合は、`Title` シーンの `Room Connection Controller` のInspectorを見てください。

## コードを見るときの入口

最初に追うべきスクリプトはこの2つです。

```text
Assets/_Project/Scripts/Network/RoomConnectionController.cs
Assets/_Project/Scripts/Gameplay/DesktopVrNetworkPlayer.cs
```

パンチのダメージや当たり判定の調整は、`NetworkPlayer` のInspectorにある `PlayerPunchSettings` を見てください。  
日本語ラベルで `パンチダメージ`、`ダメージが入る最低速度`、`連続ヒット間隔` などを調整できます。

### RoomConnectionController

Photon接続とシーン遷移を担当します。

見るメソッド：

- `ConnectAsync()`
  - Room KeyからPhotonの非公開ルームに接続
- `OnPlayerJoined(...)`
  - プレイヤー参加時に呼ばれる
- `TrySpawnLocalPlayer()`
  - `NetworkPlayer.prefab` をSpawnする
- `OnSceneLoadDone(...)`
  - Gameシーン読み込み後にSpawnを試す

### DesktopVrNetworkPlayer

プレイヤーの操作を担当します。

見る内容：

- WASD移動
- マウス視点操作
- HMD / コントローラーがある場合のVR入力
- 自分のカメラだけ有効化する処理

## デバッグ時の注意

Room Keyは完全一致が必要です。

```text
OK: player1 = test123 / player2 = test123
NG: player1 = test123 / player2 = Test123
```

また、現在のRoomは最大2人です。  
3人目は同じRoom Keyでも入れない想定です。

## VRなしでの確認

VRヘッドセットがなくても、デスクトップ操作で確認できます。

StandaloneのPCVRは標準ではオフです。  
PCVRとして起動したいときだけ、起動オプションに `-enableXR` を付けます。

```bash
/path/to/Wapawapa.app/Contents/MacOS/wapawapa -enableXR
```

Quest / AndroidビルドではXRを使う想定です。

## よくあるトラブル

### Gameシーンに移動しない

- Room Keyが3文字未満ではないか確認
- Photon App IDが入っているか確認
- Consoleに `Connection failed` が出ていないか確認
- Build Indexで `Game` が `1` になっているか確認

### 2人が同じ部屋に入らない

- Room Keyが完全一致しているか確認
- 片方だけ別のPhoton App IDを使っていないか確認
- 片方が古いビルドを起動していないか確認

### プレイヤーが出ない

- `Assets/_Project/Prefabs/NetworkPlayer.prefab` が存在するか確認
- `Title` シーンの `Room Connection Controller` に `playerPrefab` が設定されているか確認
- Consoleに `Wapawapa local player spawned` が出ているか確認

### 片方の操作しかできない

これは正常です。  
自分が操作できるのは自分のローカルプレイヤーだけです。  
相手プレイヤーはネットワーク同期された結果として見えます。

## チーム内ルール案

マルチプレイ関連を触ったら、PR前に最低限これを確認してください。

- `Title` から接続できる
- `Editor + ビルド版` で2人が同じRoomに入れる
- 2人分のプレイヤーが見える
- WASD + マウスで移動できる
- Consoleに赤エラーがない

ここまで通っていれば、ひとまずチームで共有してOKです。
