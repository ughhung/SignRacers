using SignRacers.Core;
using SignRacers.Race;
using TMPro;
using UnityEngine;

namespace SignRacers.UI
{
    /// <summary>
    /// A solid cover shown while the Game scene gets ready: connecting to the room (online), starting the
    /// race manager, and loading the track. It fades out once the race manager exists and the track is loaded,
    /// so the player never sees the bare track. Also covers a track change in the lobby.
    /// Build the prefab with Sign Racers > Build Loading Prefab.
    /// </summary>
    public class LoadingView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private float fadeSpeed = 4f;

        private string lastStatus = "", lastDetail = "";

        private void Awake()
        {
            if (group != null) { group.alpha = 1f; group.blocksRaycasts = true; group.interactable = false; }
        }

        private void Update()
        {
            var race = RaceManager.Instance;
            bool leaving = GameBootstrap.IsLeaving;
            bool loading = leaving || race == null || !race.TrackReady;

            if (group != null)
            {
                // Leaving covers the screen at once, so the track never shows while the room closes.
                if (leaving) group.alpha = 1f;
                else group.alpha = Mathf.MoveTowards(group.alpha, loading ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
                group.blocksRaycasts = group.alpha > 0.01f;
            }
            if (!loading) return;

            int dots = (int)(Time.unscaledTime * 2f) % 4;
            string word = leaving ? "Leaving"
                        : race == null && GameSession.Mode == SessionMode.Multiplayer ? "Connecting"
                        : "Loading";
            string status = word + new string('.', dots);
            string detail = leaving ? ""
                          : race == null ? (GameSession.Mode == SessionMode.Multiplayer ? "Room " + GameSession.RoomCode : "")
                          : "Loading the track";

            if (statusText != null && status != lastStatus) { statusText.text = status; lastStatus = status; }
            if (detailText != null && detail != lastDetail) { detailText.text = detail; lastDetail = detail; }
        }
    }
}
