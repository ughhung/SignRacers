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
    ///   Review - one swipeable card per winning word (LEARN the reference video or REVIEW with the camera), then READY.
    /// It also hands the winning words to this device's signing system once voting ends.
    /// Build the prefab with Sign Racers > Build Voting And Review Prefab.
    /// </summary>
    public class VotingView : MonoBehaviour
    {
        private const float PracticeSeconds = 3f;
        private static Color RowColor => UiSkin.Resolve().RowColor;
        private static Color RowChosenColor => UiSkin.Resolve().RowChosenColor;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private GameObject votePanel;
        [SerializeField] private GameObject reviewPanel;

        [Header("Voting")]
        [SerializeField] private TMP_Text voteInfoText;
        [SerializeField] private TMP_Text voteStatusText;
        [SerializeField] private Button doneButton;
        [SerializeField] private TMP_Text doneLabel;
        [SerializeField] private ScrollRect wordScroll;
        [Tooltip("Optional. Type to show only the signs that contain the letters.")]
        [SerializeField] private TMP_InputField searchField;
        [Tooltip("Optional. Shown when the search matches no sign.")]
        [SerializeField] private GameObject noMatchText;
        [Tooltip("Optional. The row of 5 slots above the list that shows your votes. Tap a filled slot to remove that vote.")]
        [SerializeField] private Button[] traySlots;
        [Tooltip("Optional. Filter chips under the search box. The first is 'All'; the others are named after a category in SignCategories.txt.")]
        [SerializeField] private Button[] filterChips;
        [SerializeField] private RectTransform wordContent;
        [Tooltip("An inactive row inside the content. The view makes one copy per word.")]
        [SerializeField] private GameObject rowTemplate;

        [Header("Review (swipeable cards)")]
        [SerializeField] private TMP_Text reviewInfoText;
        [SerializeField] private SwipePager pager;
        [SerializeField] private PracticeCard[] cards = new PracticeCard[PlayerVote.MaxChoices];
        [SerializeField] private Image[] dots = new Image[PlayerVote.MaxChoices];
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [Tooltip("The one reference video. It starts hidden and is moved into whichever card you press LEARN on.")]
        [SerializeField] private ReferenceVideo referenceVideo;

        [SerializeField] private TMP_Text reviewStatusText;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyLabel;

        private class WordRow
        {
            public Image background;
            public Button button;
            public TMP_Text votes;
            public Sprite normalSprite;
            public string word;   // lower case, as in signs.txt (used by the search)
            public int lastVotes = -1;
            public bool lastChosen;
        }

        private List<string> words;
        private readonly List<WordRow> wordRows = new List<WordRow>();
        private int[] voteCounts = new int[0];
        private bool resultsApplied;
        private bool searchHooked;
        private string activeCategory = ""; // "" = All
        private int[] trayWord;      // the word index shown in each tray slot (-1 = empty)
        private int[] lastTrayVotes;
        private bool[] lastTrayMine;
        private readonly List<int> leaders = new List<int>();

        private enum CardMode { Idle, Learning, Reviewing, Checking, Result }

        private AslRecognizer recognizer;
        private CardMode mode = CardMode.Idle;
        private int modeCard;
        private string reviewWord;
        private float modeEndsAt;
        private int reviewToken;
        private bool reviewWasShown;
        private int lastSecondsShown = -1;
        private Texture shownCameraTexture;

        private List<string> Words => words ??= SignLibrary.Load();

        private void Awake()
        {
            RaceHudView.EnsureEventSystem();
            if (rowTemplate != null) rowTemplate.SetActive(false);

            if (doneButton != null) doneButton.onClick.AddListener(() => PlayerVote.Local?.SubmitDone());
            if (readyButton != null) readyButton.onClick.AddListener(() => PlayerVote.Local?.SetReady());
            for (int i = 0; i < cards.Length; i++)
            {
                int slot = i;
                if (cards[i] == null) continue;
                if (cards[i].LearnButton != null) cards[i].LearnButton.onClick.AddListener(() => LearnWord(slot));
                if (cards[i].ReviewButton != null) cards[i].ReviewButton.onClick.AddListener(() => ReviewWord(slot));
            }
            if (pager != null) pager.PageChanged += OnPageChanged;
            if (prevButton != null) prevButton.onClick.AddListener(() => pager?.Previous());
            if (nextButton != null) nextButton.onClick.AddListener(() => pager?.Next());
            if (referenceVideo != null) referenceVideo.gameObject.SetActive(false);
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
            TickCard();

            RaceState state = race.State;
            bool voting = state == RaceState.Voting;
            bool review = state == RaceState.Review;
            if (!voting) ClearSearch();
            Show(voting || review);
            if (!review && reviewWasShown) ResetCards(); // never leave a clip or the camera running behind the race
            reviewWasShown = review;
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
                row.normalSprite = row.background.sprite;
                row.word = list[i];
                go.transform.Find("Label").GetComponent<TMP_Text>().text = SignLibrary.Display(list[i]);
                int word = i;
                row.button.onClick.AddListener(() => PlayerVote.Local?.Toggle(word));
                wordRows.Add(row);
            }
            if (wordScroll != null) wordScroll.verticalNormalizedPosition = 1f;
            if (!searchHooked)
            {
                searchHooked = true;
                if (searchField != null) searchField.onValueChanged.AddListener(_ => ApplyFilters());
                SetupChips();
                SetupTray();
            }
        }

        // ---- filter chips ----

        private string ChipCategory(int i) => i == 0 ? "" : filterChips[i].GetComponentInChildren<TMP_Text>(true).text;

        private void SetupChips()
        {
            if (filterChips == null) return;
            for (int i = 0; i < filterChips.Length; i++)
            {
                if (filterChips[i] == null) continue;
                string category = ChipCategory(i);
                filterChips[i].onClick.AddListener(() => { activeCategory = category; RefreshChips(); ApplyFilters(); });
            }
            RefreshChips();
        }

        private void RefreshChips()
        {
            if (filterChips == null) return;
            var skin = UiSkin.Resolve();
            for (int i = 0; i < filterChips.Length; i++)
            {
                var chip = filterChips[i];
                if (chip == null) continue;
                bool on = ChipCategory(i) == activeCategory;
                var image = chip.targetGraphic as Image;
                if (image != null)
                {
                    if (on && skin.rowChosen != null) image.sprite = skin.rowChosen;
                    else if (!on && skin.row != null) image.sprite = skin.row;
                    image.color = on ? skin.RowChosenColor : skin.RowColor;
                }
                var label = chip.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.color = on ? Color.white : skin.textColor;
            }
        }

        // ---- the tray of your votes ----

        private void SetupTray()
        {
            if (traySlots == null) return;
            trayWord = new int[traySlots.Length];
            lastTrayVotes = new int[traySlots.Length];
            lastTrayMine = new bool[traySlots.Length];
            for (int i = 0; i < traySlots.Length; i++)
            {
                trayWord[i] = -1; lastTrayVotes[i] = -2;
                if (traySlots[i] == null) continue;
                int slot = i;
                // Tapping a leader votes for it (or removes your vote), like tapping its row.
                traySlots[i].onClick.AddListener(() =>
                {
                    var local = PlayerVote.Local;
                    if (local != null && !local.IsDone && trayWord[slot] >= 0) local.Toggle(trayWord[slot]);
                });
            }
        }

        /// <summary>The tray shows the words with the most votes right now. A chip is blue when you voted for that word.</summary>
        private void RefreshTray(PlayerVote local, List<string> list)
        {
            if (traySlots == null || trayWord == null) return;
            var skin = UiSkin.Resolve();

            leaders.Clear();
            for (int w = 0; w < voteCounts.Length; w++) if (voteCounts[w] > 0) leaders.Add(w);
            leaders.Sort((a, b) => voteCounts[a] != voteCounts[b] ? voteCounts[b].CompareTo(voteCounts[a]) : a.CompareTo(b));

            for (int i = 0; i < traySlots.Length; i++)
            {
                var slot = traySlots[i];
                if (slot == null) continue;
                int word = i < leaders.Count ? leaders[i] : -1;
                if (word >= list.Count) word = -1;
                int votes = word >= 0 ? voteCounts[word] : 0;
                bool mine = word >= 0 && local.Has(word);
                slot.interactable = word >= 0 && !local.IsDone;
                if (word == trayWord[i] && votes == lastTrayVotes[i] && mine == lastTrayMine[i]) continue;
                trayWord[i] = word; lastTrayVotes[i] = votes; lastTrayMine[i] = mine;

                var image = slot.targetGraphic as Image;
                var label = slot.GetComponentInChildren<TMP_Text>(true);
                if (word >= 0)
                {
                    if (image != null)
                    {
                        Sprite sprite = mine ? skin.rowChosen : skin.row;
                        if (sprite != null) image.sprite = sprite;
                        image.color = mine ? skin.RowChosenColor : skin.RowColor;
                    }
                    if (label != null)
                    {
                        label.text = SignLibrary.Display(list[word]) + "\n" + votes;
                        label.color = mine ? Color.white : skin.textColor;
                    }
                }
                else
                {
                    if (image != null) { if (skin.row != null) image.sprite = skin.row; image.color = new Color(1f, 1f, 1f, 0.35f); }
                    if (label != null) { label.text = "-"; label.color = new Color(skin.textColor.r, skin.textColor.g, skin.textColor.b, 0.45f); }
                }
            }
        }

        /// <summary>Shows only the signs that match the search text AND the chosen category chip.</summary>
        private void ApplyFilters()
        {
            string query = searchField != null ? searchField.text.Trim().ToLowerInvariant() : "";
            int shown = 0;
            foreach (var row in wordRows)
            {
                bool match = (query.Length == 0 || row.word.Contains(query))
                             && (activeCategory.Length == 0 || SignCategories.Of(row.word) == activeCategory);
                if (row.button.gameObject.activeSelf != match) row.button.gameObject.SetActive(match);
                if (match) shown++;
            }
            if (noMatchText != null) noMatchText.SetActive(shown == 0);
            if (wordScroll != null) wordScroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>A new vote starts with the whole list showing.</summary>
        private void ClearSearch()
        {
            if (!searchHooked) return;
            bool dirty = (searchField != null && searchField.text.Length > 0) || activeCategory.Length > 0;
            if (!dirty) return;
            if (searchField != null) searchField.SetTextWithoutNotify("");
            activeCategory = "";
            RefreshChips();
            ApplyFilters();
        }

        private void UpdateVoting(RaceManager race, PlayerVote local)
        {
            BuildRows();
            var list = Words;

            SetText(voteInfoText, (race.HasPhaseTimer ? FormatTime(race.PhaseSecondsLeft) + "      " : "") + $"You picked {local.ChoiceCount} / {PlayerVote.MaxChoices}");

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
                    var skin = UiSkin.Resolve();
                    if (skin.rowChosen != null) row.background.sprite = chosen ? skin.rowChosen : row.normalSprite;
                    row.background.color = chosen ? RowChosenColor : RowColor;
                    row.lastChosen = chosen;
                }
                if (voteCounts[i] != row.lastVotes)
                {
                    row.votes.text = ""; // the counts live in the tray now
                    row.lastVotes = voteCounts[i];
                }
                row.button.interactable = !local.IsDone;
            }

            RefreshTray(local, list);

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
            SetText(reviewInfoText, (race.HasPhaseTimer ? FormatTime(race.PhaseSecondsLeft) + "      " : "") + "swipe to practice each sign, then tap READY");

            int count = 0;
            for (int i = 0; i < cards.Length && i < race.FinalWordCount; i++)
            {
                int index = race.GetFinalWord(i);
                if (index < 0 || index >= list.Count || cards[i] == null) break;
                cards[i].SetWord(SignLibrary.Display(list[index]));
                count++;
            }
            if (pager != null) pager.SetCount(count);

            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                bool used = i < count;
                if (dots[i].gameObject.activeSelf != used) dots[i].gameObject.SetActive(used);
                if (!used) continue;
                bool current = pager != null && i == pager.Page;
                var dotSkin = UiSkin.Resolve();
                var dotSprite = current ? dotSkin.dotOn : dotSkin.dotOff;
                if (dotSprite != null && dots[i].sprite != dotSprite) dots[i].sprite = dotSprite;
                dots[i].color = current || dotSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            }

            if (pager != null)
            {
                if (prevButton != null) prevButton.interactable = !pager.Locked && pager.Page > 0;
                if (nextButton != null) nextButton.interactable = !pager.Locked && pager.Page < count - 1;
            }

            int ready = 0;
            foreach (var voter in PlayerVote.All) if (voter.IsReady) ready++;
            SetText(reviewStatusText, local.IsReady
                ? $"Waiting for the others...  ({ready}/{PlayerVote.All.Count} ready)"
                : $"{ready}/{PlayerVote.All.Count} ready");
            if (readyButton != null) readyButton.interactable = !local.IsReady;
            SetText(readyLabel, local.IsReady ? "READY!" : "READY");
        }

        private PracticeCard Card(int slot) => slot >= 0 && slot < cards.Length ? cards[slot] : null;

        private string WordAt(int slot)
        {
            var race = RaceManager.Instance;
            if (race == null || slot >= race.FinalWordCount) return null;
            int index = race.GetFinalWord(slot);
            var list = Words;
            return index >= 0 && index < list.Count ? list[index] : null;
        }

        /// <summary>Swiping to another card puts the old one back to normal.</summary>
        private void OnPageChanged(int page) => ResetCards();

        /// <summary>Stop any clip or review and put every card back to its plain look.</summary>
        private void ResetCards()
        {
            reviewToken++; // any answer still on its way is ignored
            mode = CardMode.Idle;
            if (referenceVideo != null && referenceVideo.gameObject.activeSelf)
            {
                referenceVideo.Stop();
                referenceVideo.gameObject.SetActive(false);
            }
            for (int i = 0; i < cards.Length; i++) if (cards[i] != null) cards[i].ShowIdle();
            if (pager != null) pager.Locked = false;
        }

        // ---------------- LEARN ----------------

        /// <summary>LEARN: play the toolkit's reference clip in this card's media area.</summary>
        private void LearnWord(int slot)
        {
            var card = Card(slot);
            string word = WordAt(slot);
            if (card == null || word == null || referenceVideo == null) return;
            if (mode == CardMode.Reviewing || mode == CardMode.Checking) return;

            ResetCards();
            var rect = (RectTransform)referenceVideo.transform;
            rect.SetParent(card.MediaSlot, false);
            rect.SetAsFirstSibling();
            FitVideoInto(rect, card.MediaSlot);
            referenceVideo.gameObject.SetActive(true);

            card.ShowVideoMode();
            bool playing = referenceVideo.Play(word);
            card.SetStatus(playing ? "" : referenceVideo.LastProblem);
            mode = CardMode.Learning;
            modeCard = slot;
        }

        /// <summary>Centre the clip in the media box at its own shape (letterboxed): never bigger than the box, never squashed.</summary>
        private static void FitVideoInto(RectTransform video, RectTransform box)
        {
            var fitter = video.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.enabled = false; // we size it ourselves, so the two can't fight

            float aspect = 16f / 9f;
            var image = video.GetComponent<RawImage>();
            if (image != null && image.texture != null && image.texture.height > 0)
                aspect = (float)image.texture.width / image.texture.height;

            Vector2 size = box.rect.size;
            float width = size.x, height = size.x / aspect;
            if (height > size.y) { height = size.y; width = size.y * aspect; }

            video.anchorMin = video.anchorMax = video.pivot = new Vector2(0.5f, 0.5f);
            video.anchoredPosition = Vector2.zero;
            video.localScale = Vector3.one;
            video.sizeDelta = new Vector2(width, height);
        }

        // ---------------- REVIEW ----------------

        /// <summary>REVIEW: the camera replaces the media area, 3 seconds to sign, then green/red.</summary>
        private void ReviewWord(int slot)
        {
            var card = Card(slot);
            string word = WordAt(slot);
            if (card == null || word == null) return;
            if (mode == CardMode.Reviewing || mode == CardMode.Checking) return;

            if (recognizer == null) recognizer = FindFirstObjectByType<AslRecognizer>();
            if (recognizer == null || !recognizer.IsReady || recognizer.View == null)
            {
                card.SetStatus("The camera isn't ready yet.");
                return;
            }

            ResetCards();
            recognizer.BeginCapture();
            reviewWord = word;
            lastSecondsShown = -1;
            modeCard = slot;
            mode = CardMode.Reviewing;
            modeEndsAt = Time.time + PracticeSeconds;
            if (pager != null) pager.Locked = true;
            card.SetButtons(false);
            shownCameraTexture = recognizer.View.texture;
            card.ShowCamera(shownCameraTexture, recognizer.View.uvRect);
        }

        /// <summary>Runs every frame: counts the review down, shows the result for a moment, then resets the card.</summary>
        private void TickCard()
        {
            var card = Card(modeCard);
            if (card == null) return;
            switch (mode)
            {
                case CardMode.Reviewing:
                    if (recognizer != null && recognizer.View != null && recognizer.View.texture != shownCameraTexture)
                    {
                        // the camera texture can appear (or be replaced) a frame late: only touch the card when it changes
                        shownCameraTexture = recognizer.View.texture;
                        card.ShowCamera(shownCameraTexture, recognizer.View.uvRect);
                    }
                    if (recognizer != null && recognizer.View != null) card.FitCamera(recognizer.View.texture); // the camera size can settle a few frames after it starts
                    int secondsLeft = Mathf.CeilToInt(Mathf.Max(0f, modeEndsAt - Time.time));
                    if (secondsLeft != lastSecondsShown) // only rebuild the text when the number changes
                    {
                        lastSecondsShown = secondsLeft;
                        card.SetStatus($"Sign {SignLibrary.Display(reviewWord)} now...  {secondsLeft}");
                    }
                    if (Time.time >= modeEndsAt) FinishReview(card);
                    break;
                case CardMode.Result:
                    if (Time.time >= modeEndsAt) ResetCards();
                    break;
            }
        }

        private void FinishReview(PracticeCard card)
        {
            if (recognizer == null || !recognizer.IsReady) { ResetCards(); return; }

            mode = CardMode.Checking;
            card.SetStatus("Checking...");
            string word = reviewWord;
            int token = ++reviewToken;
            // The toolkit answers a few frames later.
            recognizer.Recognize(result =>
            {
                if (this == null || token != reviewToken || mode != CardMode.Checking) return; // swiped away or left review
                bool correct = result.HasLabel &&
                               string.Equals(result.Label, word, System.StringComparison.OrdinalIgnoreCase);
                card.ShowResult(correct, correct
                    ? "Correct!"
                    : "Try again.");
                mode = CardMode.Result;
                modeEndsAt = Time.time + 1.5f;
                if (pager != null) pager.Locked = false;
                card.SetButtons(true);
            });
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{whole / 60}:{whole % 60:00}";
        }
    }
}
