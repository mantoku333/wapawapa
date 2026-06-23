# WAPAWAPA Ability Implementation Guide for AI

あなたは、Unityプロジェクト `WAPAWAPA` の中で、VRボクシング用アビリティを実装するAIです。  
このファイルは人間向けの説明書ではなく、あなたが安全に作業するための実装ルールです。

ユーザーから「作りたいアビリティ」の説明を受け取ったら、このファイルのルールに必ず従って実装してください。

---

## 最重要ルール

あなたの担当は、**アビリティ単体の追加実装**です。  
Photon Fusion、ネットワーク接続、共通Player制御、Title/Gameシーンの基盤は変更してはいけません。

必ず守ること：

- `AbilityBase` を継承した新しいC#スクリプトを作る
- 発動処理は `Activate(in AbilityContext context, in AbilityActivationData activation)` に書く
- ダメージは `AbilityDamage` を作り、`AbilityDamageUtility.TryApplyDamage(...)` で与える
- 自分自身にはダメージを与えない
- キー入力はアビリティ側で直接処理しない
- 発動キーの管理は `AbilityLoadout` に任せる
- 既存のネットワーク基盤を編集しない
- 既存ファイルの大規模変更ではなく、追加中心で実装する

---

## 作業してよい場所

原則として、作業してよい場所は以下だけです。

```text
Assets/_Project/Scripts/Abilities/
Assets/_Project/Prefabs/Abilities/
Assets/_Project/Materials/Abilities/
Assets/_Project/Scenes/Test/
```

新しいアビリティスクリプトは、基本的にここに追加してください。

```text
Assets/_Project/Scripts/Abilities/
```

エフェクトや見た目用Prefabが必要な場合は、ここに追加してください。

```text
Assets/_Project/Prefabs/Abilities/
```

マテリアルが必要な場合は、ここに追加してください。

```text
Assets/_Project/Materials/Abilities/
```

メンバー別フォルダを作る場合は、以下のようにして構いません。

```text
Assets/_Project/Scripts/Abilities/MemberName/
Assets/_Project/Prefabs/Abilities/MemberName/
Assets/_Project/Materials/Abilities/MemberName/
```

ただし、C#のnamespaceは基本的に `Wapawapa.Abilities` を使ってください。

---

## 絶対に編集してはいけない場所

以下は変更禁止です。

```text
Assets/_Project/Scripts/Network/
Assets/_Project/Scripts/Gameplay/DesktopVrNetworkPlayer.cs
Assets/_Project/Scenes/Title.unity
Assets/_Project/Scenes/Game.unity
Assets/Photon/
Packages/
ProjectSettings/
```

禁止理由：

- Photon接続が壊れると全員の作業が止まる
- `DesktopVrNetworkPlayer` は移動、VR入力、同期Playerの共通基盤
- `Title.unity` と `Game.unity` はマルチプレイ接続確認用の本番寄りシーン
- `Packages` や `ProjectSettings` を変更すると、他メンバーの環境に影響する

---

## 既存の基盤クラス

このプロジェクトには、アビリティ用の基盤があります。  
アビリティを作るときは、以下の既存クラスを使ってください。

```text
Assets/_Project/Scripts/Abilities/AbilityBase.cs
Assets/_Project/Scripts/Abilities/AbilityLoadout.cs
Assets/_Project/Scripts/Abilities/AbilityContext.cs
Assets/_Project/Scripts/Abilities/AbilityActivationData.cs
Assets/_Project/Scripts/Abilities/AbilityDamage.cs
Assets/_Project/Scripts/Abilities/AbilityDamageUtility.cs
Assets/_Project/Scripts/Abilities/IAbilityDamageReceiver.cs
Assets/_Project/Scripts/Abilities/PlayerDamageReceiver.cs
```

見本アビリティ：

```text
Assets/_Project/Scripts/Abilities/ForwardShockwaveAbility.cs
```

見本は参考にしてよいですが、直接編集しないでください。  
新しいアビリティは新しいファイルとして追加してください。

---

## アビリティの基本形

新しいアビリティは、必ず次の形をベースにしてください。

```csharp
using UnityEngine;

namespace Wapawapa.Abilities
{
    public sealed class ExampleAbility : AbilityBase
    {
        [Header("攻撃設定")]
        [SerializeField] private float damage = 20f;
        [SerializeField] private float range = 5f;
        [SerializeField] private float pushForce = 5f;

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            // ここにアビリティの効果を書く
        }
    }
}
```

守ること：

- クラス名はアビリティ内容に合う英語名にする
- ファイル名とクラス名を一致させる
- `public sealed class XxxAbility : AbilityBase` の形にする
- `Update()` で入力処理を書かない
- `Start()` や `Awake()` に過剰な自動生成処理を書かない
- 必要な値は `[SerializeField]` でInspector調整可能にする

---

## AbilityBaseの使い方

`AbilityBase` は、アビリティ共通の親クラスです。

すでに持っている機能：

```text
AbilityId         : アビリティ識別用ID
AbilityName       : 表示用の名前
CooldownSeconds   : クールダウン秒数
RemainingCooldown : 残りクールダウン
IsReady           : 発動可能か
TryActivate(...)  : 発動を試す
Activate(...)     : 実際の発動処理。子クラスで実装する
```

あなたが主に実装するのは、このメソッドだけです。

```csharp
protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
{
    // アビリティ効果
}
```

`TryActivate(...)`、クールダウン処理、発動ログは `AbilityBase` 側にあります。  
新しいアビリティ側で同じ仕組みを作り直さないでください。

---

## AbilityContextの使い方

`AbilityContext` には、アビリティを使ったプレイヤーの情報が入っています。

```text
context.Owner     : アビリティを持っているPlayerのGameObject
context.Head      : 頭、またはカメラのTransform
context.LeftHand  : 左手のTransform
context.RightHand : 右手のTransform
context.AimSource : 狙い元。HeadがあればHead、なければOwner
```

よく使う書き方：

```csharp
var origin = context.AimSource.position;
var forward = context.AimSource.forward;
```

右手から発動する場合：

```csharp
var origin = context.RightHand != null
    ? context.RightHand.position
    : context.AimSource.position;
```

左手から発動する場合：

```csharp
var origin = context.LeftHand != null
    ? context.LeftHand.position
    : context.AimSource.position;
```

`context.Owner` が `null` の可能性も考慮してください。

---

## AbilityActivationDataの使い方

`AbilityActivationData` は、発動した瞬間の情報です。  
将来ネットワーク同期するとき、このデータを使って他プレイヤー側でも同じアビリティを再生する想定です。

```text
activation.AbilityId : 発動したアビリティのID
activation.Origin    : 発動位置
activation.Direction : 発動方向
activation.Rotation  : 発動回転
activation.Source    : 発動したPlayerのGameObject
```

前方攻撃、弾、衝撃波などは、基本的にこちらを使ってください。

```csharp
var origin = activation.Origin;
var direction = activation.Direction;
var rotation = activation.Rotation;
```

---

## ダメージの実装ルール

攻撃系アビリティでダメージを与える場合、必ず `AbilityDamage` を使ってください。

```csharp
var damageData = new AbilityDamage(
    abilityId: activation.AbilityId,
    amount: damage,
    direction: activation.Direction,
    pushForce: pushForce,
    point: hitPoint,
    source: context.Owner
);
```

対象への適用は `AbilityDamageUtility.TryApplyDamage(...)` を使ってください。

```csharp
AbilityDamageUtility.TryApplyDamage(hitCollider, damageData);
```

ダメージを受ける側は、以下のようなコンポーネントを持っています。

```text
BoxingTarget          : テスト用サンドバッグなど
PlayerDamageReceiver  : プレイヤー用ダメージ受け口
```

禁止：

- 独自のHPシステムを勝手に作る
- `SendMessage(...)` でダメージを送る
- 相手のコンポーネントを決め打ちで直接操作する
- PhotonやRPCでダメージを同期しようとする

---

## 自分自身に当てない

攻撃判定では、自分自身を必ず除外してください。

`OverlapSphere` や `Raycast` の結果を処理するときは、以下のチェックを入れてください。

```csharp
if (context.Owner != null && hit.transform.IsChildOf(context.Owner.transform))
{
    continue;
}
```

`RaycastHit` の場合：

```csharp
if (context.Owner != null && hit.transform.IsChildOf(context.Owner.transform))
{
    return;
}
```

理由：

- 自分の手、体、カメラ、Player本体に当たるとデバッグしづらい
- 自分自身にダメージが入ると、アビリティの挙動が分かりにくい
- 後からネットワーク対応するときに事故りやすい

---

## よく使う実装パターン

### 前方範囲攻撃

```csharp
[Header("攻撃設定")]
[SerializeField] private float damage = 20f;
[SerializeField] private float range = 4f;
[SerializeField] private float radius = 1.25f;
[SerializeField] private float pushForce = 5f;
[SerializeField] private LayerMask hitMask = ~0;

protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
{
    var origin = activation.Origin;
    var center = origin + activation.Direction * (range * 0.5f);
    var hits = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Collide);

    foreach (var hit in hits)
    {
        if (context.Owner != null && hit.transform.IsChildOf(context.Owner.transform))
        {
            continue;
        }

        var direction = (hit.transform.position - origin).normalized;
        if (direction.sqrMagnitude <= 0f)
        {
            direction = activation.Direction;
        }

        var damageData = new AbilityDamage(
            activation.AbilityId,
            damage,
            direction,
            pushForce,
            hit.ClosestPoint(origin),
            context.Owner
        );

        AbilityDamageUtility.TryApplyDamage(hit, damageData);
    }
}
```

### 正面Raycast攻撃

```csharp
[Header("攻撃設定")]
[SerializeField] private float damage = 30f;
[SerializeField] private float range = 5f;
[SerializeField] private float pushForce = 8f;
[SerializeField] private LayerMask hitMask = ~0;

protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
{
    if (!Physics.Raycast(activation.Origin, activation.Direction, out var hit, range, hitMask, QueryTriggerInteraction.Collide))
    {
        return;
    }

    if (context.Owner != null && hit.transform.IsChildOf(context.Owner.transform))
    {
        return;
    }

    var damageData = new AbilityDamage(
        activation.AbilityId,
        damage,
        activation.Direction,
        pushForce,
        hit.point,
        context.Owner
    );

    AbilityDamageUtility.TryApplyDamage(hit.collider, damageData);
}
```

### 周囲範囲攻撃

```csharp
[Header("攻撃設定")]
[SerializeField] private float damage = 15f;
[SerializeField] private float radius = 3f;
[SerializeField] private float pushForce = 4f;
[SerializeField] private LayerMask hitMask = ~0;

protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
{
    var origin = activation.Origin;
    var hits = Physics.OverlapSphere(origin, radius, hitMask, QueryTriggerInteraction.Collide);

    foreach (var hit in hits)
    {
        if (context.Owner != null && hit.transform.IsChildOf(context.Owner.transform))
        {
            continue;
        }

        var direction = (hit.transform.position - origin).normalized;
        if (direction.sqrMagnitude <= 0f)
        {
            direction = activation.Direction;
        }

        var damageData = new AbilityDamage(
            activation.AbilityId,
            damage,
            direction,
            pushForce,
            hit.ClosestPoint(origin),
            context.Owner
        );

        AbilityDamageUtility.TryApplyDamage(hit, damageData);
    }
}
```

### エフェクトPrefabを生成する

```csharp
[Header("見た目")]
[SerializeField] private GameObject effectPrefab;
[SerializeField] private float effectLifetime = 2f;

protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
{
    if (effectPrefab == null)
    {
        return;
    }

    var effect = Instantiate(effectPrefab, activation.Origin, activation.Rotation);
    Destroy(effect, effectLifetime);
}
```

一時的なエフェクトは必ず `Destroy(effect, effectLifetime)` で消してください。  
生成しっぱなしにしないでください。

---

## Inspectorで調整できるようにする

アビリティの数値は、できるだけ `[SerializeField]` にしてください。  
プランナーや他メンバーがInspectorで調整できるようにするためです。

良い例：

```csharp
[Header("攻撃設定")]
[Tooltip("相手に与えるダメージ量です")]
[SerializeField] private float damage = 20f;

[Tooltip("攻撃が届く距離です")]
[SerializeField] private float range = 5f;

[Tooltip("攻撃判定の半径です")]
[SerializeField] private float radius = 1.25f;

[Tooltip("命中時に相手を押し出す強さです")]
[SerializeField] private float pushForce = 6f;

[Header("見た目")]
[SerializeField] private GameObject effectPrefab;
```

避けること：

- ダメージや射程をコード内に大量に直書きする
- Inspectorで変更できないprivate定数だけで作る
- 調整項目の意味が分からない名前にする

---

## AbilityLoadoutへの登録について

アビリティは、作っただけでは発動しません。  
テストPlayerやNetworkPlayerにある `AbilityLoadout` のSlotsに登録されて発動します。

あなたがコード内で直接キー入力を見る必要はありません。

登録作業の想定：

1. PlayerのGameObjectに、作成したアビリティコンポーネントをAdd Componentする
2. Playerの `AbilityLoadout` を開く
3. Slotsに要素を追加する
4. Activation Keyを設定する
5. Ability欄に、作成したアビリティコンポーネントを入れる

例：

```text
Label          : Fire Punch
Activation Key : Digit2
Ability        : FirePunchAbility
```

実装後の回答では、どのGameObjectに追加し、どのキーに登録すればよいかを必ず説明してください。

---

## テストシーン

アビリティの確認は、基本的にテストシーンで行います。

```text
Assets/_Project/Scenes/Test/AbilityTest_Member01.unity
Assets/_Project/Scenes/Test/AbilityTest_Member02.unity
Assets/_Project/Scenes/Test/AbilityTest_Member03.unity
Assets/_Project/Scenes/Test/AbilityTest_Member04.unity
Assets/_Project/Scenes/Test/AbilityTest_Member05.unity
Assets/_Project/Scenes/Test/AbilityTest_Member06.unity
Assets/_Project/Scenes/Test/AbilityTest_自分の名前.unity
```

操作：

- `WASD`: 移動
- マウス: 中クリック後に視点移動
- 矢印キー: 視点移動
- `Space`: ジャンプ
- `Esc`: カーソル解除
- 左クリック: 左パンチ
- 右クリック: 右パンチ
- `1`: 見本アビリティ発動

新しいアビリティは、`2`、`3`、`4` などに割り当てる想定で説明してください。

---

## ネットワーク同期について

この段階では、アビリティごとにPhoton同期を実装しないでください。

禁止：

- `NetworkBehaviour` を新しく継承する
- `[Networked]` プロパティを追加する
- RPCを追加する
- `NetworkRunner` を探して直接操作する
- Room接続処理を変更する
- Title/Gameシーンの接続フローを変更する

ただし、後から同期しやすくするために、以下の形は必ず守ってください。

```text
AbilityLoadout
↓
AbilityBase.TryActivate(...)
↓
AbilityActivationDataを作成
↓
AbilityBase.Activate(...)
↓
AbilityDamageでダメージ情報を作成
↓
AbilityDamageUtility.TryApplyDamage(...)
```

この形にしておくと、後で共通側が `AbilityActivationData` をPhoton経由で送るだけで、同じアビリティを再生しやすくなります。

---

## 命名ルール

クラス名：

```text
FirePunchAbility
IceRingAbility
ThunderUppercutAbility
GravityShockwaveAbility
```

ファイル名：

```text
FirePunchAbility.cs
IceRingAbility.cs
ThunderUppercutAbility.cs
GravityShockwaveAbility.cs
```

`abilityId` のInspector初期値は、可能なら分かりやすい文字列にしてください。

例：

```text
fire.punch
ice.ring
thunder.uppercut
gravity.shockwave
```

`AbilityBase` の `abilityId` はprivate SerializeFieldなので、通常はUnity Inspectorで設定します。  
コード側から無理に書き換える必要はありません。

---

## 作ってよいアビリティ例

以下のようなアビリティは、このルールで実装できます。

- 前方に衝撃波を出す
- 右手から炎を飛ばす
- 左手から氷の弾を出す
- 自分の周囲に範囲攻撃を出す
- 一定時間だけパンチ威力を上げる
- 一定時間だけ移動速度を上げる
- 相手を少し押し返す
- 見た目エフェクトを出す
- サウンドを鳴らす

ただし、以下は勝手に実装しないでください。

- オンライン同期そのもの
- ルーム接続
- プレイヤー生成
- 入力システム全体の変更
- VR Rig全体の変更
- 独自の共通HPシステム

---

## 実装前に確認すること

作業前に、必要に応じて既存ファイルを確認してください。

優先して読むファイル：

```text
Assets/_Project/Scripts/Abilities/AbilityBase.cs
Assets/_Project/Scripts/Abilities/AbilityLoadout.cs
Assets/_Project/Scripts/Abilities/AbilityContext.cs
Assets/_Project/Scripts/Abilities/AbilityActivationData.cs
Assets/_Project/Scripts/Abilities/AbilityDamage.cs
Assets/_Project/Scripts/Abilities/AbilityDamageUtility.cs
Assets/_Project/Scripts/Abilities/ForwardShockwaveAbility.cs
```

確認目的：

- 継承元のAPIを正しく使う
- 既存のnamespaceに合わせる
- 見本アビリティと同じ実装方針にする
- 既存基盤を壊さない

---

## 実装後に確認すること

実装後、可能であれば以下を確認してください。

- C#コンパイルエラーがない
- `AbilityBase` を継承している
- `Activate(...)` をoverrideしている
- `Update()` でキー入力を見ていない
- Photon / Network関連ファイルを変更していない
- 自分自身へのダメージ除外がある
- ダメージは `AbilityDamage` 経由になっている
- 値がInspectorで調整できる
- 一時エフェクトは一定時間でDestroyされる

---

## 回答フォーマット

作業完了後、ユーザーには以下の形式で簡潔に報告してください。

```text
実装しました。

追加したファイル：
- Assets/_Project/Scripts/Abilities/〇〇Ability.cs

Unityでの設定：
1. テストシーンのPlayerを選択
2. 〇〇AbilityをAdd Component
3. AbilityLoadoutのSlotsに追加
4. Activation KeyをDigit2などに設定
5. Ability欄に〇〇Abilityを入れる

Inspectorで調整できる値：
- damage
- range
- radius
- pushForce
- effectPrefab

テスト方法：
- AbilityTest_〇〇.unityを開く
- Playする
- 指定キーを押す
- BoxingTargetに当たるか確認する
```

---

## ユーザーからの入力が曖昧な場合

ユーザーのアビリティ説明が曖昧でも、すぐに止まらず、合理的な初期値で実装してください。

例：

```text
ダメージ指定なし       -> 20
射程指定なし           -> 4〜5m
範囲指定なし           -> 半径1〜1.5m
クールダウン指定なし   -> 2〜5秒程度
ノックバック指定なし   -> 4〜6
発動位置指定なし       -> activation.Origin / activation.Direction
```

ただし、以下が不明な場合は質問しても構いません。

- アビリティの効果そのものが分からない
- 攻撃系か補助系か判断できない
- 既存基盤の変更が必要になりそう
- ネットワーク同期の実装を求められている

---

## 最終方針

あなたは、メンバーが安全にアビリティを増やせるように、以下の方針で作業してください。

```text
ネットワークは触らない
共通Player基盤は触らない
AbilityBaseを継承する
AbilityActivationDataを使う
AbilityDamageを使う
AbilityLoadoutから発動する前提にする
Inspectorで調整できるようにする
追加中心で実装する
実装後にUnityでの登録手順を説明する
```

このルールを守れば、ローカルで作ったアビリティを後から本番NetworkPlayerへ統合しやすくなります。
