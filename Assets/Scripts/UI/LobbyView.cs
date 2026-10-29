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
    /// the room code with a copy button, who has joined, the track (the host can step through the list),
    /// and START (host only) and LEAVE. Build the prefab with Sign Racers > Build Lobby Prefab.
    /// </summary>
    public class LobbyView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private Button copyButton;
        [SerializeField] private TMP_Text copyLabel;
        [SerializeField] private TMP_Text playersHeader;
        [SerializeField] private TMP_Text[] playerTexts = new TMP_Text[4];
        [SerializeField] private TMP_Text trackNameText;
        [SerializeField] private Button prevTrackButton;
        [SerializeField] private Button nextTrackButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;

        private float copiedAt = -100f;
        private readonly List<KartPathFollower> karts = new List<KartPathFollower>();

        private void Awake()
        {
            RaceHudView.EnsureEventSystem();
            if (copyButton != null) copyButton.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = GameSession.RoomCode;
                copiedAt = Time.time;
            });
            if (prevTrackButton != null) prevTrackButton.onClick.AddListener(() => RaceManager.Instance?.ChangeTrack(-1));
            if (nextTrackButton != null) nextTrackButton.onClick.AddListener(() => RaceManager.Instance?.ChangeTrack(+1));
            if (startButton != null) startButton.onClick.AddListener(() => RaceManager.Instance?.StartFromLobby());
            if (leaveButton != null) leaveButton.onClick.AddListener(() =>
            {
                var bootstrap = FindFirstObjectByType<GameBootstrap>();
                if (bootstrap != null) bootstrap.ReturnToMenu();
            });
            Show(false);
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

        private void Update()
        {
            var race = RaceManager.Instance;
            bool show = race != null && race.State == RaceState.Lobby && !race.IsSolo;
            Show(show);
            if (!show) return;

            bool host = race.HasStateAuthority;

            if (codeText != null) codeText.text = GameSession.RoomCode;
            if (copyLabel != null) copyLabel.text = Time.time - copiedAt < 2f ? "COPIED!" : "COPY CODE";

            karts.Clear();
            karts.AddRange(KartPathFollower.All);
            karts.Sort((a, b) => PlayerId(a).CompareTo(PlayerId(b)));
            if (playersHeader != null) playersHeader.text = $"Players  ({karts.Count}/{GameSession.MaxPlayers})";
            for (int i = 0; i < playerTexts.Length; i++)
            {
                if (playerTexts[i] == null) continue;
                bool used = i < karts.Count;
                playerTexts[i].gameObject.SetActive(used);
                if (!used) continue;
                string label = $"Player {PlayerId(karts[i])}";
                if (karts[i].HasStateAuthority) label += host ? "  (you, host)" : "  (you)";
                playerTexts[i].text = label;
            }

            if (trackNameText != null) trackNameText.text = TrackCatalog.DisplayName(race.TrackId);
            bool canPick = host && TrackCatalog.Count > 1;
            if (prevTrackButton != null) prevTrackButton.gameObject.SetActive(canPick);
            if (nextTrackButton != null) nextTrackButton.gameObject.SetActive(canPick);

            if (startButton != null)
            {
                startButton.gameObject.SetActive(host);
                startButton.interactable = race.TrackReady;
            }
            if (leaveButton != null)
            {
                var rect = (RectTransform)leaveButton.transform;
                rect.anchoredPosition = new Vector2(host ? -215f : 0f, rect.anchoredPosition.y);
            }

            if (statusText != null)
                statusText.text = !race.TrackReady ? "loading track..." : host ? "" : "waiting for the host to start...";
        }
    }
}
