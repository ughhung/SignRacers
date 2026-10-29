using SignRacers.Kart;
using SignRacers.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SignRacers.Core
{
    /// <summary>
    /// Testing cheats. Press B (or tap the small CHEAT button, bottom left) to give your kart a
    /// super boost right now, with no item or signing needed.
    /// Only works in the Editor and in Development Builds. In a normal release build this
    /// component removes itself, so the cheat can't ship by accident.
    /// </summary>
    public class DebugCheats : MonoBehaviour
    {
        [Tooltip("Show the small on-screen CHEAT button (handy on the iPhone, where there's no keyboard).")]
        [SerializeField] private bool showButton = true;

        private GUIStyle buttonStyle;
        private Vector2Int builtFor;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (KeyPressed()) GiveSuperBoost();
        }

        private void OnGUI()
        {
            if (!showButton) return;

            var size = new Vector2Int(Screen.width, Screen.height);
            float u = HudLayout.Unit;
            if (buttonStyle == null || builtFor != size)
            {
                builtFor = size;
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(0.035f * u), fontStyle = FontStyle.Bold, wordWrap = true };
            }

            Rect s = HudLayout.Safe;
            var rect = new Rect(s.x + 0.03f * u, s.yMax - 0.03f * u - 0.14f * u, 0.3f * u, 0.14f * u);
            if (GUI.Button(rect, "CHEAT\nSUPER BOOST", buttonStyle)) GiveSuperBoost();
        }

        private static void GiveSuperBoost()
        {
            foreach (var kart in KartPathFollower.All)
            {
                if (kart.IsBot || !kart.HasStateAuthority) continue;
                kart.DebugSuperBoost();
                return;
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static bool KeyPressed() => Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame;
#else
        private static bool KeyPressed() => Input.GetKeyDown(KeyCode.B);
#endif
#else
        private void Awake() => Destroy(this); // release builds: no cheats
#endif
    }
}
