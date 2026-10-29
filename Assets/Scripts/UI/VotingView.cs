using System.Collections.Generic;
using SignRacers.Core;
using SignRacers.Race;
using SignRacers.Signs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.UI
{
    /// <summary>
    /// The word vote and the review minute, on one prefab (VotingCanvas) with two panels.
    ///   Voting - tap words to choose up to 5 (live vote counts beside each), then DONE VOTING.
    ///   Review - the 5 winning words, a "demo coming soon" note, a TRY IT button that uses the camera, and READY.
    /// It also hands the winning words to this device's signing system once voting ends.
    /// Build the prefab with Sign Racers > Build Voting And Review Prefab.
    /// </summary>
    public class VotingView : MonoBehaviour
    {
        private const float PracticeSeconds = 3f;
        private static readonly Color RowColor = new Color(0.20f, 0.24f, 0.37f, 1f);
        private static readonly Color RowChosenColor = new Color(0.27f, 0.62f, 0.38f, 1f);

        [SerializeField] private CanvasGroup group;
        [SerializeField] private GameObject votePanel;
        [SerializeField] private GameObject reviewPanel;

        [Header("Voting")]
        [SerializeField] private TMP_Text voteInfoText;
        [SerializeField] private TMP_Text voteStatusText;
        [SerializeField] private Button doneButton;
        [SerializeField] private TMP_Text doneLabel;
        [SerializeField] private ScrollRect wordScroll;
        [SerializeField] private RectTransform wordContent;
        [Tooltip("An inactive row inside the content. The view makes one copy per word.")]
        [SerializeField] private GameObject rowTemplate;

        [Header("Review")]
        [SerializeField] private TMP_Text reviewInfoText;
        [SerializeField] private GameObject[] reviewRows = new GameObject[PlayerVote.MaxChoices];
        [SerializeField] private TMP_Text[] reviewWordTexts = new TMP_Text[PlayerVote.MaxChoices];
        [SerializeField] private Button[] tryButtons = new Button[PlayerVote.MaxChoices];
        [SerializeField] private TMP_Text practiceText;
        [SerializeField] private TMP_Text reviewStatusText;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyLabel;

        private class WordRow
        {
            public Image background;
            public Button button;
            public TMP_Text votes;
            public int lastVotes = -1;
            public bool lastChosen;
        }

        private List<string> words;
        private readonly List<WordRow> wordRows = new List<WordRow>();
        private int[] voteCounts = new int[0];
        private bool resultsApplied;

        private AslRecognizer recognizer;
        private bool practicing;
        private string practiceWord;
        private float practiceEndsAt;
        private string practiceMessage = "";

        private List<string> Words => words ??= SignLibrary.Load();

        private void Awake()
        {
            RaceHudView.EnsureEventSystem();
            if (rowTemplate != null) rowTemplate.SetActive(false);

            if (doneButton != null) doneButton.onClick.AddListener(() => PlayerVote.Local?.SubmitDone());
            if (readyButton != null) readyButton.onClick.AddListener(() => PlayerVote.Local?.SetReady());
            for (int i = 0; i < tryButtons.Length; i++)
            {
                int slot = i;
                if (tryButtons[i] != null) tryButtons[i].onClick.AddListener(() => TryWord(slot));
            }
            Show(false);
        }

        private void Show(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }

        private void Update()
        {
            var race = RaceManager.Instance;
            if (race == null) { Show(false); return; }

            ApplyWinningWords(race);
            if (practicing && Time.time >= practiceEndsAt) FinishPractice();

            RaceState state = race.State;
            bool voting = state == RaceState.Voting;
            bool review = state == RaceState.Review;
            Show(voting || review);
            if (!voting && !review) return;

            if (votePanel != null && votePanel.activeSelf != voting) votePanel.SetActive(voting);
            if (reviewPanel != null && reviewPanel.activeSelf != review) reviewPanel.SetActive(review);

            var local = PlayerVote.Local;
            if (local == null)
            {
                SetText(voting ? voteInfoText : reviewInfoText, "Add the PlayerVote component to your kart prefab.");
                return;
            }

            if (voting) UpdateVoting(race, local);
            else UpdateReview(race, local);
        }

        /// <summary>Once voting is over, this device switches to the winning words.</summary>
        private void ApplyWinningWords(RaceManager race)
        {
            if (resultsApplied || race.FinalWordCount <= 0) return;
            if (race.State == RaceState.Lobby || race.State == RaceState.Voting) return;

            var list = Words;
            var chosen = new List<string>();
            for (int i = 0; i < race.FinalWordCount; i++)
            {
                int index = race.GetFinalWord(i);
                if (index >= 0 && index < list.Count) chosen.Add(list[index]);
            }
            if (chosen.Count == 0) return;
            GameSession.SetSigns(chosen.ToArray());
            resultsApplied = true;
        }

        // ---------------- Voting ----------------

        private void BuildRows()
        {
            if (wordRows.Count > 0 || rowTemplate == null || wordContent == null) return;
            var list = Words;
            for (int i = 0; i < list.Count; i++)
            {
                var go = Instantiate(rowTemplate, wordContent);
                go.name = "Word " + list[i];
                go.SetActive(true);
                var row = new WordRow
                {
                    background = go.GetComponent<Image>(),
                    button = go.GetComponent<Button>(),
                    votes = go.transform.Find("Votes").GetComponent<TMP_Text>(),
                };
                go.transform.Find("Label").GetComponent<TMP_Text>().text = SignLibrary.Display(list[i]);
                int word = i;
                row.button.onClick.AddListener(() => PlayerVote.Local?.Toggle(word));
                wordRows.Add(row);
            }
            if (wordScroll != null) wordScroll.verticalNormalizedPosition = 1f;
        }

        private void UpdateVoting(RaceManager race, PlayerVote local)
        {
            BuildRows();
            var list = Words;

            SetText(voteInfoText, (race.HasPhaseTimer ? FormatTime(race.PhaseSecondsLeft) + "      " : "") + $"{local.ChoiceCount} / {PlayerVote.MaxChoices} chosen");

            if (voteCounts.Length != list.Count) voteCounts = new int[list.Count];
            System.Array.Clear(voteCounts, 0, voteCounts.Length);
            int done = 0;
            foreach (var voter in PlayerVote.All)
            {
                if (voter.IsDone) done++;
                for (int i = 0; i < voter.ChoiceCount; i++)
                {
                    int word = voter.GetChoice(i);
                    if (word >= 0 && word < voteCounts.Length) voteCounts[word]++;
                }
            }

            for (int i = 0; i < wordRows.Count; i++)
            {
                var row = wordRows[i];
                bool chosen = local.Has(i);
                if (chosen != row.lastChosen || row.lastVotes < 0)
                {
                    row.background.color = chosen ? RowChosenColor : RowColor;
                    row.lastChosen = chosen;
                }
                if (voteCounts[i] != row.lastVotes)
                {
                    row.votes.text = voteCounts[i] == 0 ? "" : voteCounts[i] == 1 ? "1 vote" : voteCounts[i] + " votes";
                    row.lastVotes = voteCounts[i];
                }
                row.button.interactable = !local.IsDone;
            }

            SetText(voteStatusText, local.IsDone
                ? $"Waiting for the others...  ({done}/{PlayerVote.All.Count} done)"
                : $"{done}/{PlayerVote.All.Count} done");
            if (doneButton != null) doneButton.interactable = !local.IsDone;
            SetText(doneLabel, local.IsDone ? "DONE" : "DONE VOTING");
        }

        // ---------------- Review ----------------

        private void UpdateReview(RaceManager race, PlayerVote local)
        {
            var list = Words;
            SetText(reviewInfoText, (race.HasPhaseTimer ? FormatTime(race.PhaseSecondsLeft) + "      " : "") + "practice, then tap READY");

            for (int i = 0; i < reviewRows.Length; i++)
            {
                int index = i < race.FinalWordCount ? race.GetFinalWord(i) : -1;
                bool used = index >= 0 && index < list.Count;
                if (reviewRows[i] != null && reviewRows[i].activeSelf != used) reviewRows[i].SetActive(used);
                if (!used) continue;
                SetText(reviewWordTexts[i], SignLibrary.Display(list[index]));
                if (tryButtons[i] != null) tryButtons[i].interactable = !practicing;
            }

            SetText(practiceText, practicing
                ? $"Sign {SignLibrary.Display(practiceWord)} now...  {Mathf.CeilToInt(Mathf.Max(0f, practiceEndsAt - Time.time))}"
                : practiceMessage);

            int ready = 0;
            foreach (var voter in PlayerVote.All) if (voter.IsReady) ready++;
            SetText(reviewStatusText, local.IsReady
                ? $"Waiting for the others...  ({ready}/{PlayerVote.All.Count} ready)"
                : $"{ready}/{PlayerVote.All.Count} ready");
            if (readyButton != null) readyButton.interactable = !local.IsReady;
            SetText(readyLabel, local.IsReady ? "READY!" : "READY");
        }

        // ---------------- Practice with the camera ----------------

        private void TryWord(int slot)
        {
            var race = RaceManager.Instance;
            if (race == null || practicing) return;
            int index = race.GetFinalWord(slot);
            var list = Words;
            if (index < 0 || index >= list.Count) return;

            if (recognizer == null) recognizer = FindFirstObjectByType<AslRecognizer>();
            if (recognizer == null || !recognizer.IsReady)
            {
                practiceMessage = "The camera isn't ready yet.";
                return;
            }

            recognizer.BeginCapture();
            practicing = true;
            practiceWord = list[index];
            practiceEndsAt = Time.time + PracticeSeconds;
            practiceMessage = "";
        }

        private void FinishPractice()
        {
            practicing = false;
            if (recognizer == null || !recognizer.IsReady) return;

            AslResult result = recognizer.Recognize();
            bool correct = result.HasLabel &&
                           string.Equals(result.Label, practiceWord, System.StringComparison.OrdinalIgnoreCase);

            practiceMessage = correct
                ? $"You got {SignLibrary.Display(practiceWord)}!"
                : $"Saw: {(result.HasLabel ? SignLibrary.Display(result.Label) : "nothing")}. Try again.";
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{whole / 60}:{whole % 60:00}";
        }
    }
}
