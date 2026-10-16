using SignRacers.Events;
using SignRacers.Kart;
using SignRacers.UI;
using UnityEngine;

namespace SignRacers.Signs
{
    /// <summary>
    /// Throwaway debug HUD (IMGUI) for signing, laid out for a phone held upright:
    /// the word to sign with a countdown bar in the upper middle, a result flash, a dark
    /// "oil" strip on penalties, and the three power-up buttons (BOOST, ITEM 1, ITEM 2)
    /// along the bottom within thumb reach. Replace with real UI later.
    /// </summary>
    public class AslPowerupHud : MonoBehaviour
    {
        private const float ResultFlashSeconds = 0.8f;
        private const float OilFadeSeconds = 1.5f;

        [Header("Layout")]
        [Tooltip("How far down the screen the word, its timer bar and the CORRECT / WRONG flash sit. 0 is the top, 1 is the bottom. You can drag this while the game is running to find the spot you like.")]
        [SerializeField, Range(0.05f, 0.75f)] private float wordPosition = 0.40f;

        private GUIStyle big, small, button;
        private Vector2Int builtFor;

        private void OnGUI()
        {
            KartPathFollower kart = null;
            foreach (var k in KartPathFollower.All)
            {
                if (!k.IsBot && k.HasStateAuthority) { kart = k; break; }
            }
            if (kart == null) return;

            var p = kart.GetComponent<AslPowerupSignSource>();
            if (p == null) return;

            BuildStyles();
            float u = HudLayout.Unit;
            Rect s = HudLayout.Safe;
            float pad = 0.03f * u;
            float sinceResult = Time.time - p.LastResultTime;

            // The dark penalty strip is switched off for now. To bring it back, remove the // from the lines below.
            // Placeholder penalty visual: dark strip across the top of the whole screen, fading out.
            // if (p.LastResult == PromptResult.Wrong && sinceResult < OilFadeSeconds)
            // {
            //     GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.85f * (1f - sinceResult / OilFadeSeconds));
            //     GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height * 0.25f), Texture2D.whiteTexture);
            //     GUI.color = Color.white;
            // }

            // The word to sign, with a shrinking countdown bar.
            if (p.IsSigning)
            {
                float wordY = s.y + s.height * wordPosition;
                GUI.Label(new Rect(s.x, wordY, s.width, 0.16f * u), p.Word.ToUpperInvariant(), big);

                float barW = s.width * 0.6f, barH = 0.02f * u;
                float barX = s.x + (s.width - barW) / 2f, barY = wordY + 0.17f * u;
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);
                float t = p.TimeLeft01;
                GUI.color = Color.Lerp(Color.red, Color.green, t);
                GUI.DrawTexture(new Rect(barX, barY, barW * t, barH), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(s.x + pad, barY + 0.03f * u, s.width - pad * 2f, 0.1f * u),
                    "sign it now, or tap the glowing button to check early", small);
            }

            // Result flash.
            if (sinceResult < ResultFlashSeconds && p.LastResult != PromptResult.None)
            {
                bool ok = p.LastResult == PromptResult.Correct;
                GUI.color = ok ? Color.green : new Color(1f, 0.5f, 0.2f);
                GUI.Label(new Rect(s.x, s.y + s.height * wordPosition, s.width, 0.16f * u), ok ? "CORRECT!" : "WRONG SIGN", big);
                GUI.color = Color.white;
            }

            // The three power-up buttons along the bottom, inside the safe area.
            float bh = 0.2f * u;
            float by = s.yMax - bh - pad;
            float bw = (s.width - pad * 4f) / 3f;
            float bx = s.x + pad;

            bool boostActive = p.IsSigning && p.PendingAction == PowerupAction.Boost;
            string boostLabel = boostActive ? "CHECK NOW"
                              : p.BoostCooldownLeft > 0.05f ? $"BOOST\n{p.BoostCooldownLeft:F1}s"
                              : "BOOST";
            if (DrawButton(new Rect(bx, by, bw, bh), boostLabel, boostActive || p.CanStartBoost, boostActive))
            {
                if (boostActive) p.CheckNow(); else p.StartBoost();
            }

            for (int slot = 0; slot < 2; slot++)
            {
                bool active = p.IsSigning && p.PendingAction == PowerupAction.Item && p.PendingSlot == slot;
                ItemType item = kart.GetItem(slot);
                string label = active ? "CHECK NOW"
                             : item == ItemType.None ? "EMPTY"
                             : item.ToString();
                var rect = new Rect(bx + (bw + pad) * (slot + 1), by, bw, bh);
                if (DrawButton(rect, label, active || p.CanStartItem(slot), active))
                {
                    if (active) p.CheckNow(); else p.StartItem(slot);
                }
            }

            // Status line above the buttons (active effects are drawn just above the buttons by KartHud).
            if (!p.CameraReady)
                GUI.Label(new Rect(s.x, by - 0.16f * u, s.width, 0.05f * u), "camera starting...", small);
        }

        private bool DrawButton(Rect rect, string label, bool enabled, bool highlight)
        {
            GUI.enabled = enabled;
            if (highlight) GUI.color = Color.yellow;
            bool clicked = GUI.Button(rect, label, button);
            GUI.color = Color.white;
            GUI.enabled = true;
            return clicked;
        }

        private void BuildStyles()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (big != null && builtFor == size) return; // rebuild if the screen size or orientation changes
            builtFor = size;
            float u = HudLayout.Unit;

            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.12f * u), fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.032f * u), wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(0.045f * u), fontStyle = FontStyle.Bold, wordWrap = true };
        }
    }
}
