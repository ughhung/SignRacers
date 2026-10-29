using System.Collections.Generic;
using SignRacers.Core;
using SignRacers.Signs;
using SignRacers.UI;
using UnityEngine;

namespace SignRacers.Race
{
    /// <summary>
    /// Throwaway debug HUD (IMGUI) for the online word vote and the review minute. Shown only
    /// while the race is in the Voting or Review state, drawn on top of the other HUDs.
    ///   Voting - tap words to choose up to 5 (live vote counts beside each), then DONE.
    ///   Review - the 5 winning words, a "demo coming soon" slot for each, a TRY IT button that
    ///            uses the camera, and a READY button.
    /// It also hands the winning words to this device's signing system. Replace with real UI later.
    /// </summary>
    public class VotingHud : MonoBehaviour
    {
        [Tooltip("The old IMGUI voting and review screens. Off now that they are a UI prefab (VotingCanvas).")]
        [SerializeField] private bool drawLegacy = false;

        private const float PracticeSeconds = 3f;

        private GUIStyle title, medium, small, note, row, wordLabel, count, button;
        private Vector2Int builtFor;

        private List<string> words;
        private int[] voteCounts = new int[0];
        private Vector2 scroll;
        private bool resultsApplied;

        private AslRecognizer recognizer;
        private bool practicing;
        private string practiceWord;
        private float practiceEndsAt;
        private string practiceMessage = "";

        private List<string> Words => words ??= SignLibrary.Load();

        private void Update()
        {
            if (!drawLegacy) return;
            var race = RaceManager.Instance;
            if (race == null) return;

            // Once voting is over, this device switches to the winning words.
            if (!resultsApplied && race.FinalWordCount > 0 &&
                race.State != RaceState.Lobby && race.State != RaceState.Voting)
            {
                var list = Words;
                var chosen = new List<string>();
                for (int i = 0; i < race.FinalWordCount; i++)
                {
                    int index = race.GetFinalWord(i);
                    if (index >= 0 && index < list.Count) chosen.Add(list[index]);
                }
                if (chosen.Count > 0)
                {
                    GameSession.SetSigns(chosen.ToArray());
                    resultsApplied = true;
                }
            }

            if (practicing && Time.time >= practiceEndsAt) FinishPractice();
        }

        private void OnGUI()
        {
            if (!drawLegacy) return;
            var race = RaceManager.Instance;
            if (race == null) return;

            RaceState state = race.State;
            if (state != RaceState.Voting && state != RaceState.Review) return;

            GUI.depth = -100; // in front of the other HUDs
            BuildStyles();
            float u = HudLayout.Unit;
            Rect s = HudLayout.Safe;
            float pad = 0.04f * u;

            GUI.color = new Color(0.05f, 0.06f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var local = PlayerVote.Local;
            if (local == null)
            {
                GUI.Label(new Rect(s.x + pad, s.y + s.height * 0.4f, s.width - pad * 2f, 0.3f * u),
                    "Add the PlayerVote component to your kart prefab.", medium);
                return;
            }

            if (state == RaceState.Voting) DrawVoting(race, local, s, u, pad);
            else DrawReview(race, local, s, u, pad);
        }

        // ---------------- Voting ----------------

        private void DrawVoting(RaceManager race, PlayerVote local, Rect s, float u, float pad)
        {
            var list = Words;
            float y = s.y + 0.03f * u;

            GUI.Label(new Rect(s.x, y, s.width, 0.09f * u), "VOTE FOR 5 SIGNS", title);
            y += 0.09f * u;
            GUI.Label(new Rect(s.x, y, s.width, 0.06f * u),
                $"{FormatTime(race.PhaseSecondsLeft)}      {local.ChoiceCount} / {PlayerVote.MaxChoices} chosen", medium);
            y += 0.08f * u;

            // Live vote counts from every player.
            if (voteCounts.Length != list.Count) voteCounts = new int[list.Count];
            System.Array.Clear(voteCounts, 0, voteCounts.Length);
            foreach (var voter in PlayerVote.All)
            {
                for (int i = 0; i < voter.ChoiceCount; i++)
                {
                    int word = voter.GetChoice(i);
                    if (word >= 0 && word < voteCounts.Length) voteCounts[word]++;
                }
            }

            float footerHeight = 0.24f * u;
            var viewRect = new Rect(s.x + pad, y, s.width - pad * 2f, s.yMax - footerHeight - y);
            float rowHeight = 0.11f * u;
            float gap = 0.01f * u;
            var contentRect = new Rect(0, 0, viewRect.width - 0.04f * u, list.Count * (rowHeight + gap));

            scroll = GUI.BeginScrollView(viewRect, scroll, contentRect);
            for (int i = 0; i < list.Count; i++)
            {
                var r = new Rect(0, i * (rowHeight + gap), contentRect.width, rowHeight);
                bool mine = local.Has(i);

                GUI.color = mine ? new Color(0.45f, 0.9f, 0.55f) : Color.white;
                GUI.enabled = !local.IsDone;
                if (GUI.Button(r, "   " + SignLibrary.Display(list[i]), row)) local.Toggle(i);
                GUI.enabled = true;
                GUI.color = Color.white;

                string votes = voteCounts[i] == 1 ? "1 vote" : $"{voteCounts[i]} votes";
                GUI.Label(new Rect(r.xMax - 0.3f * u, r.y, 0.28f * u, r.height), votes, count);
            }
            GUI.EndScrollView();

            int done = 0;
            foreach (var voter in PlayerVote.All) if (voter.IsDone) done++;

            float by = s.yMax - footerHeight + 0.02f * u;
            GUI.Label(new Rect(s.x, by, s.width, 0.06f * u),
                local.IsDone ? $"Waiting for the others...  ({done}/{PlayerVote.All.Count} done)"
                             : $"{done}/{PlayerVote.All.Count} done", small);

            GUI.enabled = !local.IsDone;
            if (GUI.Button(new Rect(s.x + s.width * 0.2f, by + 0.08f * u, s.width * 0.6f, 0.12f * u),
                    local.IsDone ? "DONE" : "DONE VOTING", button))
                local.SubmitDone();
            GUI.enabled = true;
        }

        // ---------------- Review ----------------

        private void DrawReview(RaceManager race, PlayerVote local, Rect s, float u, float pad)
        {
            var list = Words;
            float y = s.y + 0.03f * u;

            GUI.Label(new Rect(s.x, y, s.width, 0.09f * u), "YOUR 5 SIGNS", title);
            y += 0.09f * u;
            GUI.Label(new Rect(s.x, y, s.width, 0.06f * u), $"{FormatTime(race.PhaseSecondsLeft)}      practice, then tap READY", medium);
            y += 0.09f * u;

            float rowHeight = 0.17f * u;
            for (int i = 0; i < race.FinalWordCount; i++)
            {
                int index = race.GetFinalWord(i);
                if (index < 0 || index >= list.Count) continue;
                string word = list[index];

                var r = new Rect(s.x + pad, y, s.width - pad * 2f, rowHeight - 0.015f * u);
                GUI.color = new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(r.x + 0.03f * u, r.y + 0.01f * u, r.width * 0.6f, 0.08f * u), SignLibrary.Display(word), wordLabel);
                GUI.Label(new Rect(r.x + 0.03f * u, r.y + 0.09f * u, r.width * 0.6f, 0.05f * u), "demo coming soon", note);

                GUI.enabled = !practicing;
                if (GUI.Button(new Rect(r.xMax - 0.3f * u, r.y + 0.03f * u, 0.27f * u, 0.1f * u), "TRY IT", button))
                    StartPractice(word);
                GUI.enabled = true;

                y += rowHeight;
            }

            string status = practicing
                ? $"Sign {SignLibrary.Display(practiceWord)} now...  {Mathf.CeilToInt(Mathf.Max(0f, practiceEndsAt - Time.time))}"
                : practiceMessage;
            GUI.Label(new Rect(s.x + pad, y + 0.01f * u, s.width - pad * 2f, 0.09f * u), status, medium);

            int ready = 0;
            foreach (var voter in PlayerVote.All) if (voter.IsReady) ready++;

            float by = s.yMax - 0.24f * u;
            GUI.Label(new Rect(s.x, by, s.width, 0.06f * u),
                local.IsReady ? $"Waiting for the others...  ({ready}/{PlayerVote.All.Count} ready)"
                              : $"{ready}/{PlayerVote.All.Count} ready", small);

            GUI.enabled = !local.IsReady;
            if (GUI.Button(new Rect(s.x + s.width * 0.2f, by + 0.08f * u, s.width * 0.6f, 0.12f * u),
                    local.IsReady ? "READY!" : "READY", button))
                local.SetReady();
            GUI.enabled = true;
        }

        // ---------------- Practice with the camera ----------------

        private void StartPractice(string word)
        {
            if (recognizer == null) recognizer = FindFirstObjectByType<AslRecognizer>();
            if (recognizer == null || !recognizer.IsReady)
            {
                practiceMessage = "The camera isn't ready yet.";
                return;
            }

            recognizer.BeginCapture();
            practicing = true;
            practiceWord = word;
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

        // ---------------- Helpers ----------------

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{whole / 60}:{whole % 60:00}";
        }

        private void BuildStyles()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (title != null && builtFor == size) return;
            builtFor = size;
            float u = HudLayout.Unit;

            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.07f * u), fontStyle = FontStyle.Bold };
            medium = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.04f * u), wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.03f * u) };
            note = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(0.03f * u) };
            row = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(0.05f * u) };
            wordLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(0.06f * u), fontStyle = FontStyle.Bold };
            count = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = Mathf.RoundToInt(0.035f * u) };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(0.05f * u), fontStyle = FontStyle.Bold };
        }
    }
}
