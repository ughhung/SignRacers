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
    /// screens (Create game / Join game with a room code).
    /// The screens themselves are ordinary Unity UI objects (built by the editor tool
    /// "Sign Racers > Build Main Menu Scene"), so restyle them in the editor. This script
    /// only fills in the word buttons and reacts to taps.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        [Header("Scenes")]
        [Tooltip("Exact name of your race scene. It must be in the Build Profiles scene list.")]
        [SerializeField] private string raceSceneName = "Race";

        [Header("Panels")]
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject pickerPanel;
        [SerializeField] private GameObject multiplayerPanel;
        [SerializeField] private GameObject joinPanel;

        [Header("Home")]
        [SerializeField] private Button soloButton;
        [SerializeField] private Button multiplayerButton;

        [Header("Word picker")]
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private RectTransform listContent;
        [Tooltip("A hidden button that is copied once per word.")]
        [SerializeField] private Button wordButtonTemplate;
        [SerializeField] private Button randomButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Color normalColor = new Color(0.20f, 0.24f, 0.36f);
        [SerializeField] private Color selectedColor = new Color(0.25f, 0.70f, 0.40f);

        [Header("Multiplayer")]
        [SerializeField] private Button createButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button multiplayerBackButton;

        [Header("Join")]
        [SerializeField] private TMP_InputField joinCodeField;
        [SerializeField] private Button joinConfirmButton;
        [SerializeField] private Button joinBackButton;
        [SerializeField] private TMP_Text joinErrorText;

        private List<string> allSigns = new List<string>();
        private readonly List<string> selected = new List<string>();
        private readonly Dictionary<string, Button> wordButtons = new Dictionary<string, Button>();

        private int Required => Mathf.Min(GameSession.SignsPerGame, allSigns.Count);

        private void Start()
        {
            allSigns = SignLibrary.Load();
            BuildWordList();

            // Start with the last picks selected, if those words still exist.
            foreach (string word in GameSession.LoadLastPicks())
            {
                if (wordButtons.ContainsKey(word) && !selected.Contains(word) && selected.Count < Required)
                    selected.Add(word);
            }

            // Home
            soloButton.onClick.AddListener(() => Show(pickerPanel));
            multiplayerButton.onClick.AddListener(() => Show(multiplayerPanel));

            // Word picker
            backButton.onClick.AddListener(() => Show(homePanel));
            randomButton.onClick.AddListener(FillRandom);
            startButton.onClick.AddListener(StartSolo);
            searchField.onValueChanged.AddListener(ApplyFilter);

            // Multiplayer
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

        private void Show(GameObject panel)
        {
            homePanel.SetActive(panel == homePanel);
            pickerPanel.SetActive(panel == pickerPanel);
            multiplayerPanel.SetActive(panel == multiplayerPanel);
            joinPanel.SetActive(panel == joinPanel);
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

        private void ApplyFilter(string query)
        {
            query = query.Trim().ToLowerInvariant();
            foreach (var pair in wordButtons)
                pair.Value.gameObject.SetActive(query.Length == 0 || pair.Key.Contains(query));
        }

        private void Refresh()
        {
            foreach (var pair in wordButtons)
                pair.Value.targetGraphic.color = selected.Contains(pair.Key) ? selectedColor : normalColor;

            counterText.text = $"{selected.Count} / {Required} picked";
            startButton.interactable = Required > 0 && selected.Count == Required;
        }

        private void StartSolo()
        {
            GameSession.Mode = SessionMode.Solo;
            GameSession.IsCreatingRoom = false;
            GameSession.RoomCode = "";
            GameSession.SelectedSigns = selected.ToArray();
            GameSession.SaveLastPicks(selected);
            LaunchRace();
        }

        // ---------------- Multiplayer ----------------

        private void CreateRoom()
        {
            GameSession.Mode = SessionMode.Multiplayer;
            GameSession.IsCreatingRoom = true;
            GameSession.RoomCode = GameSession.GenerateRoomCode();
            GameSession.SelectedSigns = null; // multiplayer words come from voting, which isn't built yet
            LaunchRace();
        }

        private void JoinRoom()
        {
            string code = GameSession.CleanCode(joinCodeField.text);
            if (code.Length != GameSession.RoomCodeLength) return;

            GameSession.Mode = SessionMode.Multiplayer;
            GameSession.IsCreatingRoom = false;
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
