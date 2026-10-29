using SignRacers.Kart;
using SignRacers.Race;
using TMPro;
using UnityEngine;

namespace SignRacers.UI
{
    /// <summary>
    /// The big message in the middle of the screen: "Waiting for players", "Getting cameras
    /// ready...", the 3-2-1 countdown, and GO! when the race starts. Each new number pops in
    /// large and settles. Lives on the Race HUD prefab.
    /// </summary>
    public class RaceMessagesHud : MonoBehaviour
    {
        private const float GoSeconds = 0.8f;
        private const float PopSeconds = 0.35f;

        [SerializeField] private TMP_Text message;

        private string lastText = "";
        private float changedAt;

        private void Update()
        {
            if (message == null) return;

            string text = "";
            float size = 340f;
            Color color = Color.white;

            var race = RaceManager.Instance;
            if (race != null)
            {
                switch (race.State)
                {
                    case RaceState.Waiting:
                        size = 64f;
                        if (race.KartsPresent < race.PlayersNeeded)
                            text = $"Waiting for players  ({race.KartsPresent}/{race.PlayersNeeded})";
                        else if (race.PlayersNeeded > 1)
                            text = $"Getting cameras ready...  ({race.ReadyKarts}/{race.KartsPresent})";
                        else
                            text = "Getting the camera ready...";
                        break;

                    case RaceState.Countdown:
                        int n = race.CountdownNumber;
                        text = n > 0 ? n.ToString() : "";
                        break;

                    case RaceState.Racing:
                        if (race.RaceTime < GoSeconds)
                        {
                            text = "GO!";
                            color = new Color(0.35f, 0.95f, 0.45f);
                        }
                        break;
                }
            }

            if (text != lastText)
            {
                lastText = text;
                changedAt = Time.time;
            }

            message.text = text;
            message.fontSize = size;
            message.color = color;

            // New numbers start big and settle.
            float k = 1f - Mathf.Clamp01((Time.time - changedAt) / PopSeconds);
            float scale = size > 100f ? 1f + 0.4f * k * k : 1f;
            message.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
