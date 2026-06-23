# WAPAWAPA チーム制作方針：VRボクシング + アビリティ

今回の制作は、各メンバーが1人1つ以上のアビリティを作り、それらをプレイヤーが自由に発動しながら戦うVRボクシングです。

## 目的

チームメンバーがネットワーク部分を触らずに、アビリティ制作へ集中できる環境を作ります。

分担の考え方：

- 共通担当: Photon接続、Player、同期方針、Gameシーン
- 各メンバー: 自分のアビリティ、見た目Prefab、テストシーン
- 最後に統合: 共通の発動/ダメージ口を使って、ローカルで動いたアビリティを共通Playerへ載せる

## 作業場所

メンバーごとの作業は、専用テストシーンで行います。

```text
Assets/_Project/Scenes/Test/AbilityTest_Member01.unity
Assets/_Project/Scenes/Test/AbilityTest_Member02.unity
Assets/_Project/Scenes/Test/AbilityTest_Member03.unity
Assets/_Project/Scenes/Test/AbilityTest_Member04.unity
Assets/_Project/Scenes/Test/AbilityTest_Member05.unity
Assets/_Project/Scenes/Test/AbilityTest_Member06.unity
```

テストシーンはネットワークを使いません。  
そのため、各メンバーはPhoton接続を待たずにアビリティを作れます。

## 基本操作

テストシーンでは、VRヘッドセットなしで確認できます。

- `WASD`: 移動
- マウス: 中クリック後に視点移動
- 矢印キー: 視点移動
- `Space`: ジャンプ
- `Esc`: カーソル解除
- 左クリック: 左パンチ
- 右クリック: 右パンチ
- `1`: 見本アビリティ発動

## 実装済みの共通要素

### Ability基盤

```text
Assets/_Project/Scripts/Abilities/AbilityBase.cs
Assets/_Project/Scripts/Abilities/AbilityLoadout.cs
Assets/_Project/Scripts/Abilities/AbilityContext.cs
Assets/_Project/Scripts/Abilities/AbilityActivationData.cs
Assets/_Project/Scripts/Abilities/AbilityDamage.cs
Assets/_Project/Scripts/Abilities/IAbilityDamageReceiver.cs
Assets/_Project/Scripts/Abilities/PlayerDamageReceiver.cs
```

各アビリティは `AbilityBase` を継承して作ります。  
発動時は `AbilityActivationData`、ダメージ時は `AbilityDamage` を使います。

### ボクシング基盤

```text
Assets/_Project/Scripts/Boxing/BoxingTarget.cs
Assets/_Project/Scripts/Boxing/BoxingHit.cs
Assets/_Project/Scripts/Boxing/PunchHitbox.cs
Assets/_Project/Scripts/Boxing/PlayerPunchSettings.cs
```

パンチ判定とダメージ対象の最小実装です。  
サンドバッグは `BoxingTarget`、プレイヤーは `PlayerDamageReceiver` でダメージを受けます。
手と手が触れてもダメージは入りません。

パンチの調整は `NetworkPlayer` ルートに付いている `PlayerPunchSettings` で行います。  
Inspectorでは日本語で以下を編集できます。

- パンチダメージ
- ダメージが入る最低速度
- 押し出す強さ
- 連続ヒット間隔
- 手と手の接触を無効にするか
- 自分自身への接触を無効にするか

### 見本アビリティ

```text
Assets/_Project/Scripts/Abilities/ForwardShockwaveAbility.cs
```

`1` キーで前方衝撃波を出します。

## メンバーがやること

1. 自分のテストシーンを開く
2. `ForwardShockwaveAbility` を参考に新しいスクリプトを作る
3. 必要なら `Assets/_Project/Prefabs/Abilities/` にPrefabを作る
4. `AbilityTestPlayer` に自分のアビリティを付ける
5. `AbilityLoadout` のSlotにキーとアビリティを設定する
6. Playして確認する

## メンバーが触らないもの

原則として以下は触らないでください。

```text
Assets/_Project/Scripts/Network/
Assets/_Project/Scripts/Gameplay/DesktopVrNetworkPlayer.cs
Assets/_Project/Scenes/Title.unity
Assets/_Project/Scenes/Game.unity
Assets/Photon/
Packages/
ProjectSettings/
```

## シーン/Prefab生成

必要になったらUnityメニューから実行できます。

```text
Tools/Wapawapa/Generate Team Ability Sandbox
```

このメニューで作られるもの：

- メンバー別テストシーン
- ローカルテスト用Player Prefab
- サンドバッグPrefab
- 見本アビリティ用Effect Prefab
- NetworkPlayerへのAbility入口追加

普段は何度も押す必要はありません。  
初期生成や、壊れたときの再生成用です。

## 統合の考え方

最初はローカルで作ります。  
ただし、発動とダメージのデータ形式は最初から共通化しています。  
面白くなったものから、あとで共通Playerに載せてネットワーク対応します。

この順番がおすすめです。

1. ローカルテストシーンで面白さ確認
2. 共通Playerに載せて1人プレイ確認
3. Titleから接続して2人プレイ確認
4. 必要なものだけネットワーク同期対応

いきなり各メンバーがPhoton同期を入れないのが大事です。  
ただし、発動とダメージは `AbilityActivationData` / `AbilityDamage` を通すので、あとから同期基盤へ接続しやすくしています。
