using System.Collections.Generic;
using System.Linq;
using SignRacers.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignRacers.UI
{
    /// <summary>
    /// The main menu: Home (Solo / Multiplayer), the Solo word picker, and the Multiplayer
    /// screens (Quick play / Create game / Join game with a room code).
    /// The screens themselves are ordinary Unity UI objects (built by the editor tool
    /// "Sign Racers > Build Main Menu Scene"), so restyle them in the editor. This script
    /// only fills in the word buttons and reacts to taps.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        [Header("Scenes")]
        [Tooltip("Exact name of your race scene. It must be in the Build Profiles scene list.")]
        [SerializeField] private string raceSceneName = "Game";

        [Header("Panels")]
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject pickerPanel;
        [SerializeField] private GameObject multiplayerPanel;
        [SerializeField] private GameObject joinPanel;
        [Tooltip("Optional. The Browse public games screen.")]
        [SerializeField] private GameObject browsePanel;

        [Header("Home")]
        [Tooltip("Optional. Shows your name (for example Blue Fox). Tap it to get a new one. Add it with Sign Racers > Add Player Name To Menu.")]
        [SerializeField] private Button nameButton;
        [SerializeField] private Button soloButton;
        [SerializeField] private Button multiplayerButton;

        [Header("Word picker")]
        [Tooltip("Optional. Not needed now that the tray shows your picks.")]
        [SerializeField] private TMP_Text counterText;
        [Tooltip("Optional. The filter chips under the search box. The first one is 'All'; the others are named after a category in SignCategories.txt.")]
        [SerializeField] private Button[] filterChips;
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private RectTransform listContent;
        [Tooltip("A hidden button that is copied once per word.")]
        [SerializeField] private Button wordButtonTemplate;
        [SerializeField] private Button randomButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;
        [Tooltip("Optional. The row of 5 slots above the list that shows your picks. Tap a filled slot to remove that word.")]
        [SerializeField] private Button[] traySlots;
        [SerializeField] private Color normalColor = new Color(0.30f, 0.30f, 0.30f);
        [SerializeField] private Color selectedColor = new Color(0.55f, 0.55f, 0.55f);

        [Header("Multiplayer")]
        [Tooltip("Optional. Joins any open public room (or starts one). Add it with Sign Racers > Add Quick Play Button To Menu.")]
        [SerializeField] private Button quickPlayButton;
        [Tooltip("Optional. Opens the list of public rooms hosts have made. Add it with Sign Racers > Add Browse Games Screen To Menu.")]
        [SerializeField] private Button browseButton;
        [SerializeField] private Button createButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button multiplayerBackButton;

        [Header("Join")]
        [SerializeField] private TMP_InputField joinCodeField;
        [SerializeField] private Button joinConfirmButton;
        [SerializeField] private Button joinBackButton;
        [SerializeField] private TMP_Text joinErrorText;

        [Header("Browse")]
        [SerializeField] private PublicGameBrowser browser;
        [SerializeField] private Button browseBackButton;

        private List<string> allSigns = new List<string>();
        private readonly List<string> selected = new List<string>();
        private readonly Dictionary<string, Button> wordButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Sprite> normalSprites = new Dictionary<string, Sprite>();

        private int Required => Mathf.Min(GameSession.SignsPerGame, allSigns.Count);

        private void Start()
        {
            allSigns = SignLibrary.Load();
            BuildWordList();

            // The picker always starts empty (0 picked); last picks are not restored.

            // Home
            if (nameButton != null)
            {
                RefreshName();
                nameButton.onClick.AddListener(() => { PlayerNames.Roll(); RefreshName(); });
            }
            soloButton.onClick.AddListener(() => Show(pickerPanel));
            multiplayerButton.onClick.AddListener(() => Show(multiplayerPanel));

            // Word picker
            backButton.onClick.AddListener(() => Show(homePanel));
            randomButton.onClick.AddListener(FillRandom);
            startButton.onClick.AddListener(StartSolo);
            searchField.onValueChanged.AddListener(_ => ApplyFilter());
            SetupChips();

            // Multiplayer
            if (quickPlayButton != null) quickPlayButton.onClick.AddListener(QuickPlay);
            if (browseButton != null) browseButton.onClick.AddListener(() => Show(browsePanel));
            if (browseBackButton != null) browseBackButton.onClick.AddListener(() => Show(multiplayerPanel));
            if (browser != null) browser.JoinRequested += OnBrowseJoin;
            createButton.onClick.AddListener(CreateRoom);
            joinButton.onClick.AddListener(() => { ClearJoinForm(); Show(joinPanel); });
            multiplayerBackButton.onClick.AddListener(() => Show(homePanel));

            // Join
            joinCodeField.onValueChanged.AddListener(OnCodeChanged);
            joinConfirmButton.onClick.AddListener(JoinRoom);
            joinBackButton.onClick.AddListener(() => Show(multiplayerPanel));

            Refresh();
            Show(homePanel);

            // Coming back after a failed join: show why, on the join screen.
            string error = GameSession.ConsumeError();
            if (!string.IsNullOrEmpty(error))
            {
                ClearJoinForm();
                Show(joinPanel);
                joinErrorText.text = error;
            }
        }

        private void RefreshName()
        {
            var label = nameButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = PlayerNames.Get() + "  (tap to change)";
        }

        private void Show(GameObject panel)
        {
            homePanel.SetActive(panel == homePanel);
            pickerPanel.SetActive(panel == pickerPanel);
            multiplayerPanel.SetActive(panel == multiplayerPanel);
            joinPanel.SetActive(panel == joinPanel);
            if (browsePanel != null) browsePanel.SetActive(panel == browsePanel);
        }

        // ---------------- Solo word picker ----------------

        private void BuildWordList()
        {
            wordButtonTemplate.gameObject.SetActive(false);

            foreach (string word in allSigns)
            {
                Button button = Instantiate(wordButtonTemplate, listContent);
                button.gameObject.SetActive(true);
                button.name = "Word_" + word;

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = SignLibrary.Display(word);

                string captured = word;
                button.onClick.AddListener(() => Toggle(captured));
                wordButtons[word] = button;
                if (button.targetGraphic is Image image) normalSprites[word] = image.sprite;
            }
        }

        private void Toggle(string word)
        {
            if (selected.Contains(word)) selected.Remove(word);
            else if (selected.Count < Required) selected.Add(word);
            Refresh();
        }

        /// <summary>Keeps the player's picks and fills the remaining slots at random.</summary>
        private void FillRandom()
        {
            var pool = allSigns.Where(w => !selected.Contains(w)).OrderBy(_ => Random.value).ToList();
            foreach (string word in pool)
            {
                if (selected.Count >= Required) break;
                selected.Add(word);
            }
            Refresh();
        }

        private string activeCategory = ""; // "" = All

        private void SetupChips()
        {
            if (filterChips == null) return;
            for (int i = 0; i < filterChips.Length; i++)
            {
                var chip = filterChips[i];
                if (chip == null) continue;
                string category = i == 0 ? "" : chip.GetComponentInChildren<TMP_Text>(true).text;
                chip.onClick.AddListener(() => { activeCategory = category; RefreshChips(); ApplyFilter(); });
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
                string category = i == 0 ? "" : chip.GetComponentInChildren<TMP_Text>(true).text;
                bool on = category == activeCategory;
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

        /// <summary>Shows only the words that match the search text AND the chosen category chip.</summary>
        private void ApplyFilter()
        {
            string query = searchField.text.Trim().ToLowerInvariant();
            foreach (var pair in wordButtons)
            {
                bool matchesText = query.Length == 0 || pair.Key.Contains(query);
                bool matchesCategory = activeCategory.Length == 0 || SignCategories.Of(pair.Key) == activeCategory;
                pair.Value.gameObject.SetActive(matchesText && matchesCategory);
            }
        }

        private void Refresh()
        {
            var skin = UiSkin.Resolve();
            foreach (var pair in wordButtons)
            {
                bool chosen = selected.Contains(pair.Key);
                var image = pair.Value.targetGraphic as Image;
                if (image != null && skin.rowChosen != null)
                {
                    // Styled picker (Kenney skin): a picked word swaps to the blue row, like the voting screen.
                    if (chosen) image.sprite = skin.rowChosen;
                    else if (normalSprites.TryGetValue(pair.Key, out var normal)) image.sprite = normal;
                    image.color = chosen ? skin.RowChosenColor : skin.RowColor;
                    var rowLabel = pair.Value.GetComponentInChildren<TMP_Text>(true);
                    if (rowLabel != null) rowLabel.color = chosen ? Color.white : skin.textColor;
                }
                else
                {
                    pair.Value.targetGraphic.color = chosen ? selectedColor : normalColor;
                }
            }

            RefreshTray(skin);
            if (counterText != null) counterText.text = $"{selected.Count} / {Required} picked";
            startButton.interactable = Required > 0 && selected.Count == Required;
        }

        private void RefreshTray(UiSkin skin)
        {
            if (traySlots == null) return;
            for (int i = 0; i < traySlots.Length; i++)
            {
                var slot = traySlots[i];
                if (slot == null) continue;
                slot.gameObject.SetActive(i < Required);
                bool filled = i < selected.Count;
                var image = slot.targetGraphic as Image;
                var label = slot.GetComponentInChildren<TMP_Text>(true);
                slot.onClick.RemoveAllListeners();
                slot.interactable = filled;
                if (filled)
                {
                    string word = selected[i];
                    slot.onClick.AddListener(() => Toggle(word));
                    if (image != null) { if (skin.rowChosen != null) image.sprite = skin.rowChosen; image.color = skin.RowChosenColor; }
                    if (label != null) { label.text = SignLibrary.Display(word); label.color = Color.white; }
                }
                else
                {
                    if (image != null) { if (skin.row != null) image.sprite = skin.row; image.color = new Color(1f, 1f, 1f, 0.35f); }
                    if (label != null) { label.text = "empty"; label.color = new Color(skin.textColor.r, skin.textColor.g, skin.textColor.b, 0.45f); }
                }
            }
        }

        private void StartSolo()
        {
            GameSession.Mode = SessionMode.Solo;
            GameSession.IsCreatingRoom = false;
            GameSession.IsPublic = false;
            GameSession.RoomCode = "";
            GameSession.SelectedSigns = selected.ToArray();
            GameSession.SaveLastPicks(selected);
            LaunchRace();
        }

        // ---------------- Multiplayer ----------------

        /// <summary>Join any open public room, or start one if none is open. No room code.</summary>
        private void QuickPlay()
        {
            GameSession.Mode = SessionMode.Multiplayer;
            GameSession.IsCreatingRoom = false;
            GameSession.IsPublic = true;
            GameSession.RoomCode = "";
            GameSession.SelectedSigns = null; // the words come from the room's vote
            LaunchRace();
        }

        private void CreateRoom()
        {
            GameSession.Mode = SessionMode.Multiplayer;
            GameSession.IsCreatingRoom = true;
            GameSession.IsPublic = false;
            GameSession.RoomCode = GameSession.GenerateRoomCode();
            GameSession.SelectedSigns = null; // multiplayer words come from voting
            LaunchRace();
        }

        private void JoinRoom()
        {
            string code = GameSession.CleanCode(joinCodeField.text);
            if (code.Length != GameSession.RoomCodeLength) return;

            GameSession.Mode = SessionMode.Multiplayer;
            GameSession.IsCreatingRoom = false;
            GameSession.IsPublic = false;
            GameSession.RoomCode = code;
            GameSession.SelectedSigns = null;
            LaunchRace();
        }

        /// <summary>A row in the Browse list was tapped: leave the room list, then join that room by its code.</summary>
        private async void OnBrowseJoin(string roomName)
        {
            string code = GameSession.CleanCode(roomName);
            if (code.Length != GameSession.RoomCodeLength) return;

            if (browser != null) await browser.StopAsync();

            GameSession.Mode = SessionMode.Multiplayer;
            GameSession.IsCreatingRoom = false;
            GameSession.IsPublic = false;
            GameSession.RoomCode = code;
            GameSession.SelectedSigns = null;
            LaunchRace();
        }

        private void OnCodeChanged(string raw)
        {
            string cleaned = GameSession.CleanCode(raw);
            if (cleaned != raw) joinCodeField.SetTextWithoutNotify(cleaned);
            joinConfirmButton.interactable = cleaned.Length == GameSession.RoomCodeLength;
            joinErrorText.text = "";
        }

        private void ClearJoinForm()
        {
            joinCodeField.SetTextWithoutNotify("");
            joinConfirmButton.interactable = false;
            joinErrorText.text = "";
        }

        private void LaunchRace()
        {
            GameSession.StartedFromMenu = true;
            SceneManager.LoadScene(raceSceneName);
        }
    }
}
