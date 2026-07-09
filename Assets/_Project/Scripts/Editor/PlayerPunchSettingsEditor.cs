using UnityEditor;
using UnityEngine;
using Wapawapa.Boxing;

namespace Wapawapa.Editor
{
    [CustomEditor(typeof(PlayerPunchSettings))]
    public sealed class PlayerPunchSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("プレイヤーのパンチ設定", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "ネットワークプレイヤーに付けるパンチ調整用コンポーネントです。左右の手に付いている PunchHitbox は、この値を使ってダメージ判定を行います。",
                MessageType.Info);

            Draw("punchId", "パンチID", "ログや将来のネットワーク同期で使う識別名です。通常は basic.punch のままでOKです。");
            Draw("damage", "パンチダメージ", "パンチが相手に当たった時に減らす体力です。");
            Draw("minimumHitSpeed", "ダメージが入る最低速度", "手の移動速度がこの値以上の時だけダメージが入ります。小さいほど軽い接触でもダメージになります。");
            Draw("pushForce", "押し出す強さ", "ヒットした相手やサンドバッグを押す強さです。");
            Draw("repeatHitDelay", "連続ヒット間隔", "同じ相手に連続でダメージが入るまでの待ち時間です。");
            Draw("punchSwingVolume", "パンチ発動SE音量", "パンチを出した瞬間に鳴るSEの音量です。");
            Draw("ignoreHandToHandHits", "手と手の接触は無効", "ONの場合、相手の手に当たってもダメージは入りません。");
            Draw("ignoreSelfHits", "自分自身への接触は無効", "ONの場合、自分の体や手に当たってもダメージは入りません。");

            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string propertyName, string label, string tooltip)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), new GUIContent(label, tooltip));
        }
    }
}
