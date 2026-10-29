using SignRacers.Core;
using SignRacers.Kart;
using SignRacers.Race;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.UI
{
    /// <summary>
    /// The end-of-race screen, on its own prefab (RaceResultsCanvas) so it can show after the
    /// race HUD hides. Appears once your kart finishes: your place, everyone's finish time,
    /// and RACE AGAIN (host only, once everyone is done) and MENU buttons.
    /// </summary>
    public class ResultsHud : MonoBehaviour
    {
        private static readonly Color GoldColor = new Color(1f, 0.84f, 0.25f);

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text placeText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject[] rows = new GameObject[4];
        [SerializeField] private TMP_Text[] nameTexts = new TMP_Text[4];
        [SerializeField] private TMP_Text[] timeTexts = new TMP_Text[4];
        [SerializeField] private Button againButton;
        [SerializeField] private Button menuButton;

        private KartPathFollower kart;

        private void Awake()
        {
            RaceHudView.EnsureEventSystem();
            if (againButton != null) againButton.onClick.AddListener(() =>
            {
                var race = RaceManager.Instance;
                if (race != null && race.State == RaceState.Finished && race.HasStateAuthority) race.Restart();
            });
            if (menuButton != null) menuButton.onClick.AddListener(() =>
            {
                var bootstrap = FindFirstObjectByType<GameBootstrap>();
                if (bootstrap != null) bootstrap.ReturnToMenu();
            });
        }

        private void Update()
        {
            if (kart == null)
            {
                foreach (var k in KartPathFollower.All)
                {
                    if (k.IsBot || !k.HasStateAuthority) continue;
                    kart = k;
                    break;
                }
            }

            var race = RaceManager.Instance;
            bool show = race != null && kart != null && kart.IsFinished;
            if (group != null)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
                group.interactable = show;
                group.blocksRaycasts = show;
            }
            if (!show) return;

            var standings = RaceManager.GetStandings();
            int place = standings.IndexOf(kart) + 1;
            bool everyoneDone = race.State == RaceState.Finished;

            if (title != null) title.text = everyoneDone ? "RACE OVER" : "FINISHED!";
            if (placeText != null) placeText.text = $"You came {Ordinal(place)}";

            for (int i = 0; i < rows.Length; i++)
            {
                bool has = i < standings.Count;
                if (rows[i] != null) rows[i].SetActive(has);
                if (!has) continue;

                var k = standings[i];
                string who = k == kart ? "You" : k.IsBot ? k.BotName : "Player";
                Color color = k == kart ? GoldColor : Color.white;
                if (nameTexts[i] != null) { nameTexts[i].text = $"{i + 1}.  {who}"; nameTexts[i].color = color; }
                if (timeTexts[i] != null) { timeTexts[i].text = k.IsFinished ? FormatTime(k.FinishTime) : "racing..."; timeTexts[i].color = color; }
            }

            bool canRestart = everyoneDone && race.HasStateAuthority;
            if (againButton != null) againButton.gameObject.SetActive(canRestart);
            if (menuButton != null)
            {
                // MENU sits in the middle when it is the only button.
                var rect = (RectTransform)menuButton.transform;
                rect.anchoredPosition = new Vector2(canRestart ? 215f : 0f, rect.anchoredPosition.y);
            }

            if (statusText != null)
                statusText.text = !everyoneDone ? "waiting for the others to finish..."
                                : !race.HasStateAuthority ? "waiting for the host to restart..."
                                : "";
        }

        private static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        private static string FormatTime(float t)
        {
            int minutes = (int)(t / 60f);
            float seconds = t - minutes * 60f;
            return $"{minutes}:{seconds:00.00}";
        }
    }
}
