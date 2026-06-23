using UnityEngine;

namespace Wapawapa.Testing
{
    public sealed class TestSceneNote : MonoBehaviour
    {
        [TextArea(6, 12)]
        [SerializeField]
        private string note =
            "Ability test scene.\n\n" +
            "Controls:\n" +
            "- WASD: move test player\n" +
            "- Mouse: look around after middle click\n" +
            "- Arrow keys: look around\n" +
            "- Space: jump\n" +
            "- Esc: unlock cursor\n" +
            "- Left / Right Click: left or right punch\n" +
            "- 1: sample ability\n\n" +
            "Network code is not used in this scene. Ability work should stay local and avoid editing Photon/Fusion scripts.";
    }
}
