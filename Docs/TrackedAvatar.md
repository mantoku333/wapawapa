# まこらプレイヤー

`NetworkPlayer` と `AbilityTestPlayer` に `TrackedAvatar` を追加済み。モデルを手作業で入れ直す必要はありません。

## 確認方法

1. Unityのインポート・コンパイルが終わるまで待つ。
2. AbilityTestPlayerを使用するテストシーンをPlay。
3. VRでは頭・左右のコントローラーを動かす。グリップは親指・中指・薬指・小指、トリガーは人差し指を曲げる。
4. PCのAbilityTestPlayerではQで左、Eまたは5で右のパンチと握りを確認できる。NetworkPlayerのPC操作ではマウス左右ボタンを使う。
5. 歩くと足が交互に出て、HMDを下げると膝が曲がる。

`Tools > Wapawapa > Validate Tracked Avatars` は両プレハブの参照、Missing Script、Humanoid、Fusionの握りデータ生成を検証する。Playを止めた状態で実行する。結果は `Temp/tracked-avatar-validation.txt`。

## 構成

- 既存のHead・LeftHand・RightHandは入力と攻撃のために残し、球・カプセルのRendererだけを隠している。
- AvatarVisual下のまこらを共通のTrackedAvatarで制御する。追加のAnimator ControllerやVRChat SDKは不要。
- 頭の骨の原点ではなく、モデルの目の位置をHMDへ合わせる。
- 腕・脚は2本の骨のIK。到達できない手の位置は腕の長さで制限し、骨を引き伸ばさない。
- 指はモデルの各指の向きから曲げ軸を計算して動かす。
- 13個の衣装の回転補助と方陣の追従はAvatarBoneFollowerへ置き換えた。VRChat専用のアバター登録情報は除去。
- 自分のカメラに限り、頭の骨に強く追従する三角形を省いた実行時メッシュを使う。他のカメラと相手には全身を表示する。
- NetworkPlayerは既存の頭・手のNetworkTransformと、追加したNetworkHandInputの4値で同期する。脚の動きは各端末で頭とルートの移動から再計算する。

## 調整項目と範囲

両プレハブのTrackedAvatarコンポーネントで調整する。

| 項目 | 用途 |
| --- | --- |
| Standing Eye Height | モデルの立った目の高さ。初期値1.65m |
| Wrist Offset | コントローラーから手首までの位置補正。コントローラー座標でm指定 |
| Left / Right Wrist Euler | 左右の手首の角度補正 |
| Step Distance / Duration / Height | 足を踏み出す距離・時間・高さ |
| Ground Mask | 足が接地する床のレイヤー |

VRの位置入力は床基準を想定する。足トラッカーや指トラッキングを読み取る実装ではなく、足は移動から生成し、指はグリップとトリガーから生成する。脚の足運びは通信で完全に同じフレームを再現する方式ではない。

パンチ判定は元のコントローラー側のターゲットに保持している。PCの手の移動はアバターの腕が届く範囲に制限する。VRでは実際のコントローラー位置を優先するため、腕の届かない距離ではモデルの拳と判定が離れる。アバターの身長や手首補正は使用環境に合わせて調整する。

## 検証

実際のHumanoidモデルを別のUnityプロジェクトに読み込み、左右の手のIK、指の独立制御と解除、しゃがみ、歩行、自己Colliderの除外、到達不能なIK目標、一人称用メッシュの切替をテストした。元プロジェクトの参照を使ったC#コンパイルも確認。

VR実機での装着感と2端末間の通信プレイは別途確認が必要。
