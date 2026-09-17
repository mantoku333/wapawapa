# まこらプレイヤー

`NetworkPlayer` と `AbilityTestPlayer` に `TrackedAvatar` を追加済み。モデルを手作業で入れ直す必要はありません。

## 確認方法

1. Unityのインポート・コンパイルが終わるまで待つ。
2. AbilityTestPlayerを使用するテストシーンをPlay。
3. VRでは頭・左右のコントローラーを動かす。グリップは親指・中指・薬指・小指、トリガーは人差し指を曲げる。
4. PCのAbilityTestPlayerではQで左、Eまたは5で右のパンチと握りを確認できる。NetworkPlayerのPC操作ではマウス左右ボタンを使う。
5. WASD／スティックで前後左右に動くと対応するHumanoidモーションが混ざり、停止するとIdleへ戻る。ジャンプの上昇中はJump、落下中はFalling Idleを再生する。

`Tools > Wapawapa > Validate Tracked Avatars` は両プレハブの参照、Missing Script、Humanoid、Fusionの握りデータ生成を検証する。Playを止めた状態で実行する。結果は `Temp/tracked-avatar-validation.txt`。

## 構成

- 既存のHead・LeftHand・RightHandは入力と攻撃のために残し、球・カプセルのRendererだけを隠している。
- AvatarVisual下のまこらを共通のTrackedAvatarで制御する。両プレハブにPlayerLocomotion.controllerを設定済み。VRChat SDKは不要。
- 頭の骨の原点ではなく、モデルの目の位置をHMDへ合わせる。
- 腰・脚は提供されたX BotのHumanoidモーションをリターゲットする。以前の脚IK・接地位置の記憶・自動踏み替えは撤去した。
- 腕は2本の骨のIK。到達できない手の位置は腕の長さで制限し、骨を引き伸ばさない。
- AvatarLocomotionAnimationがAnimator Controllerを手動評価してから、TrackedAvatarが頭・腕・指を上書きする。描画直前は保存したアニメーション姿勢に追従を再適用するため、アニメーションの二重更新や補正の蓄積は起きない。
- LowerBody.maskはルート・胴体・脚を有効にし、頭・腕・指を除外する。移動は既存のゲームプレイ側が担当し、Root Motionは無効。FBX側もルート移動・回転をポーズへベイクしている。
- 指はモデルの各指の向きから曲げ軸を計算して動かす。
- 13個の衣装の回転補助と方陣の追従はAvatarBoneFollowerへ置き換えた。VRChat専用のアバター登録情報は除去。
- 自分のカメラに限り、頭の骨に強く追従する三角形を省いた実行時メッシュを使う。他のカメラと相手には全身を表示する。
- NetworkPlayerは既存の頭・手のNetworkTransformと、追加したNetworkHandInputの4値で同期する。下半身の再生パラメーターは各端末で頭の水平移動とルートの上下移動・接地判定から計算する。

## 調整項目と範囲

両プレハブのTrackedAvatarコンポーネントで調整する。

| 項目 | 用途 |
| --- | --- |
| Standing Eye Height | モデルの立った目の高さ。初期値1.65m |
| Wrist Offset | コントローラーから手首までの位置補正。コントローラー座標でm指定 |
| Left / Right Wrist Euler | 左右の手首の角度補正 |
| Locomotion Controller | 下半身用のAnimator Controller |
| Animation Move Speed | 移動ブレンドが最大になる速度。NetworkPlayerは3.5m/s、AbilityTestPlayerは3m/s |
| Ground Mask | 地上／空中を判定する床のレイヤー |

VRの位置入力は床基準を想定する。足トラッカーや指トラッキングを読み取る実装ではなく、下半身はモーションを再生し、指はグリップとトリガーから生成する。脚の再生位相は通信で完全に同じフレームを再現する方式ではない。段差への足裏の吸着や、HMDの高さに合わせて膝を曲げる補正は行わない。

## モーションの編集

`Assets/_Project/Animations/Player/Motions` に提供された7本のFBXを保存。すべてHumanoid / Create From This Modelでインポートし、まこらのAvatarへリターゲットする。Jumpだけ非ループ、ほかはループ。

`PlayerLocomotion.controller` のLocomotionにはIdle・Running・Running Backward・Left Strafe・Right Strafeの2D Blend Treeを設定。MoveX / MoveZは体の向きに対する移動量、PlaybackSpeedは移動速度に応じた再生倍率。GroundedとVerticalSpeedでJump / Fallを切り替え、着地時は0.12秒でLocomotionへ戻る。素材の差し替えや遷移時間はこのControllerで編集できる。

パンチ判定は元のコントローラー側のターゲットに保持している。PCの手の移動はアバターの腕が届く範囲に制限する。VRでは実際のコントローラー位置を優先するため、腕の届かない距離ではモデルの拳と判定が離れる。アバターの身長や手首補正は使用環境に合わせて調整する。

## 検証

`Tools > Wapawapa > Validate Locomotion Animation` で、実際のまこらモデルを使い、前後左右のモーション選択と脚の動き、停止、ジャンプ／落下／着地、ゲームプレイルートが動かないこと、頭・手・指の追従、描画直前の再適用を検証できる。Playを止めた状態で実行し、結果は `Logs/locomotion-validation.txt` に出力する。分離したUnityプロジェクトで実行済み。元プロジェクトの参照を使ったC#コンパイルも確認。

VR実機での装着感と2端末間の通信プレイは別途確認が必要。
