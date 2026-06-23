# :rightwards_pushing_hand::leftwards_pushing_hand: **領域展開** :leftwards_pushing_hand::rightwards_pushing_hand:

━━━━━━━━━━━━━━━━━━━━  
## :boxing_glove: **VRボクシング制作、開幕です**
━━━━━━━━━━━━━━━━━━━━

VRボクシングの制作土台を用意しました。  
Photon Fusionで2人接続できる `Title` / `Game` シーンと、各メンバーが安全に作業できるアビリティ用テストシーンがあります。  
メンバーは基本的にネットワーク部分を触らず、自分のアビリティ制作に集中できます。

━━━━━━━━━━━━━━━━━━━━  
## :video_game: 作るもの
━━━━━━━━━━━━━━━━━━━━

**VRボクシング + 各メンバー固有アビリティ** のゲームです。

- :boxing_glove: 基本はパンチで戦う
- :sparkles: 各メンバーが1つ以上アビリティを作る
- :package: 作ったアビリティは後で共通Playerに統合する
- :satellite: Photonなどのネットワーク部分は基本触らなくてOK

━━━━━━━━━━━━━━━━━━━━  
## :test_tube: 作業場所
━━━━━━━━━━━━━━━━━━━━

各メンバーは、自分用のテストシーンで作業してください。

```text
Assets/_Project/Scenes/Test/AbilityTest_自分の名前.unity
```

テストシーンでは、ネットワーク接続なしでアビリティをすぐ試せます。

━━━━━━━━━━━━━━━━━━━━  
## :sparkles: アビリティの基本
━━━━━━━━━━━━━━━━━━━━

アビリティは `AbilityBase` を継承したスクリプトとして作ります。  
`AbilityBase` には、後からネットワーク対応しやすい形の発動処理・クールダウン処理が入っています。

作成場所：

```text
Assets/_Project/Scripts/Abilities/
```

見本：

```text
Assets/_Project/Scripts/Abilities/ForwardShockwaveAbility.cs
```

Prefabやエフェクトが必要な場合：

```text
Assets/_Project/Prefabs/Abilities/
```

━━━━━━━━━━━━━━━━━━━━  
## :robot: AIを使った作業方法
━━━━━━━━━━━━━━━━━━━━

AIに渡す用のルールファイルを用意しています。

```text
Docs/ABILITY_WORKFLOW_FOR_AI.md
```

これはメンバーが読み込む資料というより、  
**AIにそのまま投げるための実装指示書**です。

### :scroll: 手順

1. AIに `Docs/ABILITY_WORKFLOW_FOR_AI.md` を渡す
2. 作りたいアビリティの説明を一緒に渡す
3. AIに実装してもらう
4. 自分のテストシーンで確認する

例：

```text
Docs/ABILITY_WORKFLOW_FOR_AI.md のルールに従って、
VRボクシング用の「炎のパンチ」アビリティを作ってください。

作りたいアビリティ：
- 名前：炎のパンチ
- 発動キー：2
- 効果：右手から前方に炎を飛ばす
- ダメージ：20
- 射程：5m
- ノックバック：少し強め
```

これでAI側が、  
`AbilityBase` を継承すること、`AbilityDamage` を使うこと、ネットワーク部分を触らないことなどを判断できるようにしています。

━━━━━━━━━━━━━━━━━━━━  
## :white_check_mark: 触ってOKな場所
━━━━━━━━━━━━━━━━━━━━

```text
Assets/_Project/Scripts/Abilities/
Assets/_Project/Prefabs/Abilities/
Assets/_Project/Materials/Abilities/
Assets/_Project/Scenes/Test/
```

━━━━━━━━━━━━━━━━━━━━  
## :no_entry_sign: 触らない場所
━━━━━━━━━━━━━━━━━━━━

```text
Assets/_Project/Scripts/Network/
Assets/_Project/Scripts/Gameplay/
Assets/Photon/
```

━━━━━━━━━━━━━━━━━━━━  
## :joystick: テスト時の操作
━━━━━━━━━━━━━━━━━━━━

- `WASD`: 移動
- マウス中クリック + 移動: 視点移動
- 矢印キー: 視点移動
- `Space`: ジャンプ
- 左クリック: 左パンチ
- 右クリック: 右パンチ
- `1`: 見本アビリティ

追加したアビリティは、`2`、`3`、`4` などに割り当てる想定です。

━━━━━━━━━━━━━━━━━━━━  
## :dart: まとめ
━━━━━━━━━━━━━━━━━━━━

やることはシンプルです。

```text
AIに Docs/ABILITY_WORKFLOW_FOR_AI.md を渡す
↓
作りたいアビリティの説明を渡す
↓
AIに AbilityBase 継承のアビリティを作ってもらう
↓
自分のテストシーンで確認する
```

ネットワーク対応や本番Playerへの統合は、後でこちらでまとめて行います。  
まずはそれぞれの「面白いアビリティ」をローカルで作っていきましょう。
