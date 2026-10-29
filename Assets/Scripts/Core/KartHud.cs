using SignRacers.Kart;
using SignRacers.UI;
using UnityEngine;

namespace SignRacers.Core
{
    /// <summary>
    /// Shows the local kart's active effects (BOOST, SUPER BOOST, PENALTY, SLOWED) in one line
    /// just above the three power-up buttons, and only while something is active.
    /// Which items you hold is already shown on the buttons themselves.
    /// Throwaway debug HUD (IMGUI). Replace with real UI later.
    /// </summary>
    public class KartHud : MonoBehaviour
    {
        private GUIStyle style;
        private Vector2Int builtFor;

        [Tooltip("Draw the old placeholder effects line. Leave off: EffectsHud on the Race HUD prefab replaces it.")]
        [SerializeField] private bool drawLegacy = false;

        private void OnGUI()
        {
            if (!drawLegacy) return;
            KartPathFollower kart = null;
            foreach (var k in KartPathFollower.All)
            {
                if (!k.IsBot && k.HasStateAuthority) { kart = k; break; }
            }
            if (kart == null) return;

            string effects = "";
            if (kart.IsBoosted) effects += "BOOST   ";
            if (kart.IsSuperBoosted) effects += "SUPER BOOST   ";
            if (kart.IsPenalized) effects += "PENALTY   ";
            if (kart.IsSlowed) effects += "SLOWED   ";
            if (effects.Length == 0) return;

            var size = new Vector2Int(Screen.width, Screen.height);
            float u = HudLayout.Unit;
            if (style == null || builtFor != size)
            {
                builtFor = size;
                style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.RoundToInt(0.045f * u),
                    fontStyle = FontStyle.Bold,
                };
            }

            // Green for good effects, orange when something is slowing you down.
            GUI.color = kart.IsPenalized || kart.IsSlowed ? new Color(1f, 0.6f, 0.25f) : new Color(0.5f, 1f, 0.55f);

            // The power-up buttons take the bottom 0.23 units (0.2 tall plus a margin), so sit just above them.
            Rect s = HudLayout.Safe;
            float buttonsTop = s.yMax - 0.23f * u;
            GUI.Label(new Rect(s.x, buttonsTop - 0.085f * u, s.width, 0.07f * u), effects.TrimEnd(), style);
            GUI.color = Color.white;
        }
    }
}
