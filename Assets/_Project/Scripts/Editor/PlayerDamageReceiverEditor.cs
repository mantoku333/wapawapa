using UnityEditor;
using UnityEngine;
using Wapawapa.Abilities;

namespace Wapawapa.Editor
{
    [CustomEditor(typeof(PlayerDamageReceiver))]
    public sealed class PlayerDamageReceiverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("プレイヤーのダメージ受け口", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "パンチやアビリティからダメージを受けるためのコンポーネントです。ネットワークプレイヤーのルートに付けます。",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("maxHealth"),
                new GUIContent("最大体力", "プレイヤーの最大体力です。パンチやアビリティでこの値から減ります。"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("onKnockedOut"),
                new GUIContent("体力0時のイベント", "体力が0になった時に呼ばれるイベントです。"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
