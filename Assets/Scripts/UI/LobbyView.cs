using System.Collections.Generic;
using SignRacers.Core;
using SignRacers.Kart;
using SignRacers.Race;
using SignRacers.Track;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.UI
{
    /// <summary>
    /// The online lobby screen, on a prefab (LobbyCanvas). Shows while the room is in the Lobby state:
    /// the room code (tap it to copy), who has joined, the track as swipeable cards, and START (host only) and LEAVE.
    /// Private room: only the host can change the track; everyone else sees the card follow the host.
    /// Quick Play (public) room: there is no code and no START. Everyone swipes to VOTE for a track (votes shown on the
    /// cards), and the room starts by itself on a timer once enough players are in.
    /// Build the prefab with Sign Racers > Build Lobby Prefab.
    /// Optional track pictures: put a Sprite named exactly like the track scene (e.g. Track_Hand) in Assets/Resources/TrackThumbs/.
    /// </summary>
    public class LobbyView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private Button visibilityButton;
        [SerializeField] private TMP_Text visibilityLabel;
        [SerializeField] private Button copyButton;
        [SerializeField] private TMP_Text copyHint;
        [SerializeField] private TMP_Text playersHeader;
        [SerializeField] private TMP_Text[] playerTexts = new TMP_Text[4];
        [Tooltip("Optional: the row behind each player name, shown and hidden together with the name.")]
        [SerializeField] private GameObject[] playerRows = new GameObject[4];

        [Header("Track cards")]
        [SerializeField] private SwipePager trackPager;
        [SerializeField] private TMP_Text[] trackCardNames = new TMP_Text[0];
        [SerializeField] private Image[] trackCardPreviews = new Image[0];
        [SerializeField] private TMP_Text[] trackCardVotes = new TMP_Text[0];
        [SerializeField] private Button[] trackCardButtons = new Button[0];
        [SerializeField] private Image[] trackCardFrames = new Image[0];
        [SerializeField] private Sprite frameNormal;
        [SerializeField] private Sprite frameVoted;   // the card I voted for (Quick Play)
        [SerializeField] private TMP_Text trackLabelText;
        [SerializeField] private Button prevTrackButton;
        [SerializeField] private Button nextTrackButton;

        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;

        private float copiedAt = -100f;
        private readonly List<KartPathFollower> karts = new List<KartPathFollower>();
        private int seenTrackId = -1;
        private bool synced;
        private bool cardsFilled;
        private bool browsed;      // Quick Play: I swiped or voted, so the card stops following the room's leading track
        private bool ignorePage;   // true while the code itself moves the pager (that must not count as a vote)
        private int[] voteCounts = new int[0];
        private int[] shownVotes = new int[0];

        private void Awake()
        {
            RaceHudView.EnsureEventSystem();
            if (copyButton != null) copyButton.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = GameSession.RoomCode;
                copiedAt = Time.time;
            });
            if (visibilityButton != null) visibilityButton.onClick.AddListener(() =>
            {
                var r = RaceManager.Instance;
                if (r != null) r.SetOpenToPublic(!r.OpenToPublic);
            });
            if (trackPager != null) trackPager.PageChanged += OnTrackPage;
            for (int i = 0; i < trackCardButtons.Length; i++)
            {
                int card = i;
                if (trackCardButtons[i] != null) trackCardButtons[i].onClick.AddListener(() => OnCardTapped(card));
            }
            if (prevTrackButton != null) prevTrackButton.onClick.AddListener(() => trackPager?.Previous());
            if (nextTrackButton != null) nextTrackButton.onClick.AddListener(() => trackPager?.Next());
            if (startButton != null) startButton.onClick.AddListener(() => RaceManager.Instance?.StartFromLobby());
            if (leaveButton != null) leaveButton.onClick.AddListener(() =>
            {
                var bootstrap = FindFirstObjectByType<GameBootstrap>();
                if (bootstrap != null) bootstrap.ReturnToMenu();
            });
            Show(false);
        }

        private void OnDestroy()
        {
            if (trackPager != null) trackPager.PageChanged -= OnTrackPage;
        }

        /// <summary>The host swiped (or tapped an arrow) to another card: tell the room.</summary>
        private void OnTrackPage(int page)
        {
            if (ignorePage) return;
            var race = RaceManager.Instance;
            if (race == null) return;

            if (race.IsPublicRoom)
            {
                browsed = true; // Quick Play: swiping only looks at the tracks. Tap a card to vote.
                return;
            }

            if (!race.HasStateAuthority) return; // private room: everyone else just follows the host
            race.ChangeTrack(page - race.TrackId);
            seenTrackId = race.TrackId;
        }

        /// <summary>Quick Play: tapping a track card is my vote for that track.</summary>
        private void OnCardTapped(int card)
        {
            var race = RaceManager.Instance;
            if (race == null || !race.IsPublicRoom) return;
            PlayerVote.Local?.SetTrackVote(card);
            browsed = true;
        }

        private void SyncTo(int page, bool instant)
        {
            if (trackPager == null) return;
            ignorePage = true;
            trackPager.GoTo(page, instant);
            ignorePage = false;
        }

        private void Show(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private static int PlayerId(KartPathFollower kart) =>
            kart.Object != null && kart.Object.IsValid ? kart.Object.InputAuthority.PlayerId : 0;

        private void FillCards()
        {
            int count = Mathf.Min(TrackCatalog.Count, trackCardNames.Length);
            for (int i = 0; i < count; i++)
            {
                if (trackCardNames[i] != null) trackCardNames[i].text = TrackCatalog.DisplayName(i);
                var thumb = Resources.Load<Sprite>("TrackThumbs/" + TrackCatalog.SceneName(i));
                if (thumb != null && i < trackCardPreviews.Length && trackCardPreviews[i] != null)
                {
                    trackCardPreviews[i].sprite = thumb;
                    trackCardPreviews[i].color = Color.white;
                    trackCardPreviews[i].preserveAspect = false;
                }
            }
            if (trackPager != null) trackPager.SetCount(count);
            cardsFilled = true;
        }

        private void Update()
        {
            var race = RaceManager.Instance;
            bool show = race != null && race.State == RaceState.Lobby && !race.IsSolo;
            Show(show);
            if (!show) { synced = false; browsed = false; return; }

            bool host = race.HasStateAuthority;

            bool isPublic = race.IsPublicRoom;
            if (codeText != null)
                codeText.text = isPublic ? "PUBLIC" : Time.time - copiedAt < 2f ? "COPIED!" : GameSession.RoomCode;
            if (copyButton != null) copyButton.interactable = !isPublic; // the code frame is the copy button
            if (copyHint != null) copyHint.gameObject.SetActive(!isPublic);
            if (visibilityButton != null)
            {
                // Host of a code room can open it to Quick Play players (or hide it again).
                visibilityButton.gameObject.SetActive(host && !isPublic);
                if (visibilityLabel != null) visibilityLabel.text = race.OpenToPublic ? "PUBLIC" : "PRIVATE";
            }

            karts.Clear();
            karts.AddRange(KartPathFollower.All);
            karts.Sort((a, b) => PlayerId(a).CompareTo(PlayerId(b)));
            if (playersHeader != null) playersHeader.text = $"Players  ({karts.Count}/{GameSession.MaxPlayers})";
            for (int i = 0; i < playerTexts.Length; i++)
            {
                if (playerTexts[i] == null) continue;
                bool used = i < karts.Count;
                var row = i < playerRows.Length ? playerRows[i] : null;
                if (row != null)
                {
                    row.SetActive(used);
                    var rowImage = row.GetComponent<Image>();
                    if (rowImage != null && used)
                        rowImage.color = karts[i].HasStateAuthority ? new Color(0.75f, 0.9f, 1f) : Color.white; // my own row is tinted
                }
                playerTexts[i].gameObject.SetActive(used);
                if (!used) continue;
                string label = PlayerVote.NameOf(karts[i].Object.InputAuthority);
                if (string.IsNullOrEmpty(label)) label = $"Player {PlayerId(karts[i])}";
                // The host is whoever owns the race manager. (My own row is tinted, so it needs no "you".)
                if (race.Object != null && race.Object.IsValid && karts[i].Object.InputAuthority == race.Object.StateAuthority)
                    label += "  (host)";
                playerTexts[i].text = label;
            }

            // Track cards. Private: the host swipes and everyone else follows. Quick Play: everyone swipes to vote.
            if (!cardsFilled) FillCards();
            if (trackPager != null)
            {
                bool canPick = trackPager.Count > 1 && (isPublic || host);
                trackPager.Locked = !canPick;

                if (!synced)
                {
                    SyncTo(race.TrackId, true);
                    seenTrackId = race.TrackId;
                    synced = true;
                }
                else if (race.TrackId != seenTrackId)
                {
                    // Private: follow the host. Quick Play: follow the leading track until I vote myself.
                    if (!isPublic || !browsed) SyncTo(race.TrackId, false);
                    seenTrackId = race.TrackId;
                }

                if (prevTrackButton != null)
                {
                    prevTrackButton.gameObject.SetActive(canPick);
                    prevTrackButton.interactable = trackPager.Page > 0;
                }
                if (nextTrackButton != null)
                {
                    nextTrackButton.gameObject.SetActive(canPick);
                    nextTrackButton.interactable = trackPager.Page < trackPager.Count - 1;
                }

                ShowTrackVotes(isPublic);
            }
            if (trackLabelText != null) trackLabelText.text = isPublic ? "TAP A TRACK TO VOTE" : "TRACK";

            if (startButton != null)
            {
                startButton.gameObject.SetActive(host && !isPublic);
                startButton.interactable = race.TrackReady;
            }
            if (leaveButton != null)
            {
                var rect = (RectTransform)leaveButton.transform;
                rect.anchoredPosition = new Vector2(host && !isPublic ? -215f : 0f, rect.anchoredPosition.y);
            }

            if (statusText != null)
            {
                string status;
                if (!race.TrackReady) status = "loading track...";
                else if (isPublic)
                    status = race.LobbyCountdownRunning
                        ? $"Race starts in {Mathf.CeilToInt(race.LobbySecondsLeft)}..."
                        : $"Waiting for players...  ({karts.Count}/{race.PublicMinPlayers})";
                else status = host ? "" : "waiting for the host to start...";
                if (statusText.text != status) statusText.text = status;
            }
        }

        /// <summary>Quick Play: shows how many players voted for each track under its card.</summary>
        private void ShowTrackVotes(bool isPublic)
        {
            int count = Mathf.Min(TrackCatalog.Count, trackCardVotes.Length);
            if (voteCounts.Length != count) { voteCounts = new int[count]; shownVotes = new int[count]; for (int i = 0; i < count; i++) shownVotes[i] = -1; }
            System.Array.Clear(voteCounts, 0, count);
            if (isPublic)
            {
                foreach (var vote in PlayerVote.All)
                {
                    int track = vote.TrackVote - 1;
                    if (track >= 0 && track < count) voteCounts[track]++;
                }
            }
            int mine = isPublic && PlayerVote.Local != null ? PlayerVote.Local.TrackVote - 1 : -1;
            for (int i = 0; i < count; i++)
            {
                int key = voteCounts[i] * 2 + (i == mine ? 1 : 0);
                if (key == shownVotes[i] || trackCardVotes[i] == null) continue;
                shownVotes[i] = key;
                string text = voteCounts[i] == 0 ? "" : voteCounts[i] == 1 ? "1 vote" : voteCounts[i] + " votes";
                if (i == mine) text = text.Length == 0 ? "Your vote" : "Your vote  -  " + text;
                trackCardVotes[i].text = text;
                if (i < trackCardFrames.Length && trackCardFrames[i] != null)
                {
                    var sprite = i == mine && frameVoted != null ? frameVoted : frameNormal;
                    if (sprite != null) trackCardFrames[i].sprite = sprite;
                }
            }
        }
    }
}
