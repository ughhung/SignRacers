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
    /// race HUD hides. Appears once your kart finishes: your place, everyone's finish time (with their names),
    /// and the buttons:
    ///   Code room: RACE AGAIN (host only, once everyone is done) and MENU.
    ///   Quick Play room: NEW GAME (everyone, once everyone is done: leaves the closed room and joins a fresh public one) and MENU.
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
        private TMP_Text againLabel;

        private void Awake()
        {
            RaceHudView.EnsureEventSystem();
            if (againButton != null) againLabel = againButton.GetComponentInChildren<TMP_Text>(true);
            if (againButton != null) againButton.onClick.AddListener(() =>
            {
                var race = RaceManager.Instance;
                if (race == null || race.State != RaceState.Finished) return;

                if (race.IsPublicRoom)
                {
                    // Quick Play: the room is closed, so everyone goes and finds a new game.
                    var bootstrap = FindFirstObjectByType<GameBootstrap>();
                    if (bootstrap != null) bootstrap.FindNewGame();
                }
                else if (race.HasStateAuthority)
                {
                    race.Restart();
                }
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
                bool me = k == kart;
                var skin = UiSkin.Resolve();
                Color color = me ? Color.white : skin.textColor; // white on your blue row, dark on the grey rows
                var rowImage = rows[i] != null ? rows[i].GetComponent<Image>() : null;
                if (rowImage != null)
                {
                    Sprite sprite = me && skin.rowChosen != null ? skin.rowChosen : skin.row;
                    if (sprite != null && rowImage.sprite != sprite) rowImage.sprite = sprite;
                    rowImage.color = me ? skin.RowChosenColor : skin.RowColor;
                }
                if (nameTexts[i] != null) { nameTexts[i].text = $"{i + 1}.  {NameOf(k)}"; nameTexts[i].color = color; }
                if (timeTexts[i] != null) { timeTexts[i].text = k.IsFinished ? FormatTime(k.FinishTime) : "racing..."; timeTexts[i].color = color; }
            }

            bool publicRoom = race.IsPublicRoom;
            bool canAgain = everyoneDone && (publicRoom || race.HasStateAuthority);
            if (againButton != null) againButton.gameObject.SetActive(canAgain);
            if (againLabel != null) againLabel.text = publicRoom ? "NEW GAME" : "RACE AGAIN";
            if (menuButton != null)
            {
                // MENU sits in the middle when it is the only button.
                var rect = (RectTransform)menuButton.transform;
                rect.anchoredPosition = new Vector2(canAgain ? 215f : 0f, rect.anchoredPosition.y);
            }

            if (statusText != null)
                statusText.text = !everyoneDone ? "waiting for the others to finish..."
                                : !publicRoom && !race.HasStateAuthority ? "waiting for the host to restart..."
                                : "";
        }

        private static string NameOf(KartPathFollower k)
        {
            if (k.HasStateAuthority && !k.IsBot) return "You";
            if (k.IsBot) return k.BotName;
            string name = k.Object != null && k.Object.IsValid ? PlayerVote.NameOf(k.Object.InputAuthority) : "";
            return string.IsNullOrEmpty(name) ? "Player" : name;
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
