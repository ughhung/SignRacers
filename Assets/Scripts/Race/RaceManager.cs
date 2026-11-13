using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Fusion;
using SignRacers.Core;
using SignRacers.Kart;
using SignRacers.Track;
using UnityEngine;

namespace SignRacers.Race
{
    public enum RaceState { Waiting = 0, Countdown = 1, Racing = 2, Finished = 3, Lobby = 4, Voting = 5, Review = 6 }

    /// <summary>
    /// The shared race state, networked so every player sees the same countdown, the same
    /// GO, and the same results:
    ///   Waiting   -> until enough players (and their karts) are in
    ///   Countdown -> 3, 2, 1, karts are locked
    ///   Racing    -> karts drive; the race clock runs
    ///   Finished  -> every kart has crossed the line
    /// It lives on a spawned NetworkObject (see GameBootstrap). In Single mode the local
    /// player owns it; in Shared mode the master client does. Only the owner changes state.
    /// If no RaceManager exists in the scene, karts just drive freely like before.
    /// Quick Play (public) rooms: the lobby starts by itself on a timer once enough players are in,
    /// and the track is the one with the most votes (see PlayerVote.TrackVote).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class RaceManager : NetworkBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [SerializeField] private int totalLaps = 3;
        [Tooltip("Karts needed before the countdown starts. Use 1 for solo, 2 for a two-window online test.")]
        [SerializeField] private int playersNeeded = 1;
        [Tooltip("Seconds of 5-4-3-2-1 once everyone's camera is ready.")]
        [SerializeField] private float countdownSeconds = 5f;
        [Tooltip("If a camera never comes up, start the race anyway after this many seconds.")]
        [SerializeField] private float cameraWaitTimeout = 20f;

        [Header("Online word voting")]
        [Tooltip("Turn off to skip voting and go straight from the lobby to the race (handy for testing).")]
        [SerializeField] private bool votingEnabled = true;
        [SerializeField] private float votingSeconds = 45f;
        [SerializeField] private float reviewSeconds = 60f;
        [Tooltip("Solo: show the practice screen (try each of your signs on the camera) before the race.")]
        [SerializeField] private bool soloPractice = true;

        [Header("Quick Play (public rooms)")]
        [Tooltip("Players needed before the lobby timer starts. Use 1 to test Quick Play alone.")]
        [SerializeField] private int publicMinPlayers = 2;
        [Tooltip("Seconds the lobby waits once enough players are in. Late joiners can still join until it ends.")]
        [SerializeField] private float publicLobbySeconds = 20f;
        [Tooltip("When the room fills up (4 players), the timer is cut down to this.")]
        [SerializeField] private float publicFullRoomSeconds = 5f;

        [Tooltip("Online: if a player leaves before the race starts, wait this many seconds for them, then carry on without them.")]
        [SerializeField] private float missingPlayerSeconds = 8f;

        private float cameraWait;
        private float missingWait;
        private int requiredKarts; // set when the host starts from the lobby
        private bool fullHandled;
        private int[] trackTally = new int[0];

        [Networked] private int StateValue { get; set; }
        [Networked] private TickTimer CountdownTimer { get; set; }
        [Networked] public float RaceTime { get; set; }
        [Networked] private TickTimer PhaseTimer { get; set; }
        [Networked, Capacity(5)] private NetworkArray<int> FinalWords => default;
        [Networked] public int FinalWordCount { get; set; }

        /// <summary>Which track the room races on (a position in TrackCatalog). Private rooms: the host picks it. Quick Play: the most voted track.</summary>
        [Networked] public int TrackId { get; set; }

        /// <summary>True for a Quick Play room (public, starts on a timer, track by vote).</summary>
        [Networked] public NetworkBool IsPublicRoom { get; set; }
        [Networked] private TickTimer LobbyTimer { get; set; }
        /// <summary>A host-made room the host has opened to Quick Play players (the host still picks the track and presses START).</summary>
        [Networked] public NetworkBool OpenToPublic { get; set; }

        /// <summary>Owner only, in a private-style lobby: show the room to Quick Play (public) or hide it again (private).</summary>
        public void SetOpenToPublic(bool open)
        {
            if (!HasStateAuthority || State != RaceState.Lobby || IsPublicRoom || IsSolo) return;
            OpenToPublic = open;
            if (Runner.SessionInfo.IsValid) Runner.SessionInfo.IsVisible = open;
        }

        private bool trackLoading;
        private string trackFailed;

        /// <summary>True once this device has the room's chosen track loaded.</summary>
        public bool TrackReady => !trackLoading && TrackLoader.LoadedName == TrackCatalog.SceneName(TrackId);

        /// <summary>Host only, lobby only (private rooms): step to the next (+1) or previous (-1) track.</summary>
        public void ChangeTrack(int step)
        {
            if (!HasStateAuthority || State != RaceState.Lobby || IsPublicRoom) return;
            TrackId = TrackCatalog.Wrap(TrackId + step);
        }

        private void Update()
        {
            if (Object == null || !Object.IsValid || trackLoading) return;
            string wanted = TrackCatalog.SceneName(TrackId);
            if (TrackLoader.LoadedName == wanted || trackFailed == wanted) return;
            _ = SwitchTrack(wanted);
        }

        private async Task SwitchTrack(string sceneName)
        {
            trackLoading = true;
            try { trackFailed = await TrackLoader.Load(sceneName) ? null : sceneName; }
            finally { trackLoading = false; }
        }

        public RaceState State => (RaceState)StateValue;

        /// <summary>Offline race (Solo): one human, no room code, no waiting for other players.</summary>
        public bool IsSolo => Runner != null && Runner.GameMode == GameMode.Single;

        /// <summary>False in Solo, where choosing signs has no time limit.</summary>
        public bool HasPhaseTimer => PhaseTimer.IsRunning;
        public int TotalLaps => TrackInfo.Current != null && TrackInfo.Current.Laps > 0 ? TrackInfo.Current.Laps : totalLaps;
        public int PlayersNeeded => requiredKarts > 0 ? requiredKarts : playersNeeded;
        public int KartsPresent => KartPathFollower.All.Count;
        public int ReadyKarts => KartPathFollower.All.Count(k => k.IsSignReady);

        /// <summary>Quick Play lobby: players needed before the timer starts.</summary>
        public int PublicMinPlayers => publicMinPlayers;

        /// <summary>Quick Play lobby: is the start timer running?</summary>
        public bool LobbyCountdownRunning => LobbyTimer.IsRunning;

        /// <summary>Quick Play lobby: seconds until the room moves on (0 when the timer isn't running).</summary>
        public float LobbySecondsLeft
        {
            get
            {
                float? remaining = LobbyTimer.RemainingTime(Runner);
                return remaining.HasValue ? Mathf.Max(0f, remaining.Value) : 0f;
            }
        }

        /// <summary>Seconds left in the current voting or review phase.</summary>
        public float PhaseSecondsLeft
        {
            get
            {
                float? remaining = PhaseTimer.RemainingTime(Runner);
                return remaining.HasValue ? Mathf.Max(0f, remaining.Value) : 0f;
            }
        }

        /// <summary>The winning word's position in the shared word list, or -1.</summary>
        public int GetFinalWord(int index) => index >= 0 && index < FinalWordCount ? FinalWords[index] : -1;

        /// <summary>The number to show during the countdown (3, 2, 1).</summary>
        public int CountdownNumber
        {
            get
            {
                if (State != RaceState.Countdown) return 0;
                float? remaining = CountdownTimer.RemainingTime(Runner);
                return remaining.HasValue ? Mathf.CeilToInt(remaining.Value) : 0;
            }
        }

        /// <summary>True when karts may drive and the player may sign (also true with no manager).</summary>
        public static bool AllowsDriving => Instance == null || Instance.State == RaceState.Racing;

        public override void Spawned()
        {
            Instance = this;

            // An online room starts in the lobby. Solo picks its signs on the menu, then gets the practice
            // screen (the lobby is skipped). Played directly in the editor: straight to waiting.
            // (Only the owner sets state, others just receive it.)
            if (HasStateAuthority && GameSession.StartedFromMenu &&
                (GameSession.Mode == SessionMode.Multiplayer || soloPractice))
                StateValue = (int)RaceState.Lobby;

            if (HasStateAuthority && GameSession.StartedFromMenu)
                IsPublicRoom = GameSession.Mode == SessionMode.Multiplayer && GameSession.IsPublic;
        }

        /// <summary>Owner only: leave the lobby, close the room to newcomers, and get the race going. (Private rooms: the host taps START. Quick Play: the lobby timer calls it.)</summary>
        public void StartFromLobby()
        {
            if (!HasStateAuthority || State != RaceState.Lobby) return;

            requiredKarts = KartsPresent; // everyone who is here now
            LobbyTimer = default;
            if (!IsSolo && Runner.SessionInfo.IsValid)
            {
                Runner.SessionInfo.IsOpen = false;    // nobody joins mid-race
                Runner.SessionInfo.IsVisible = false; // and Quick Play no longer lists it
            }

            if (votingEnabled)
            {
                PhaseTimer = IsSolo ? default : TickTimer.CreateFromSeconds(Runner, votingSeconds); // Solo: no time limit
                StateValue = (int)RaceState.Voting;
            }
            else
            {
                StateValue = (int)RaceState.Waiting;
            }
        }

        /// <summary>Quick Play lobby, owner only: the track follows the most votes (a tie keeps the current track), and the start timer runs once enough players are in.</summary>
        private void TickPublicLobby()
        {
            ApplyTrackVotes();

            int players = KartsPresent;
            if (players < publicMinPlayers)
            {
                LobbyTimer = default; // somebody left: wait for more
                fullHandled = false;
                return;
            }

            if (!LobbyTimer.IsRunning) LobbyTimer = TickTimer.CreateFromSeconds(Runner, publicLobbySeconds);

            if (players >= GameSession.MaxPlayers && !fullHandled)
            {
                fullHandled = true; // a full room doesn't need to wait the whole time
                float? left = LobbyTimer.RemainingTime(Runner);
                if (!left.HasValue || left.Value > publicFullRoomSeconds)
                    LobbyTimer = TickTimer.CreateFromSeconds(Runner, publicFullRoomSeconds);
            }
            else if (players < GameSession.MaxPlayers) fullHandled = false;

            if (LobbyTimer.Expired(Runner)) StartFromLobby();
        }

        private void ApplyTrackVotes()
        {
            int count = TrackCatalog.Count;
            if (count <= 1) return;
            if (trackTally.Length != count) trackTally = new int[count];
            System.Array.Clear(trackTally, 0, count);

            foreach (var vote in PlayerVote.All)
            {
                int track = vote.TrackVote - 1;
                if (track >= 0 && track < count) trackTally[track]++;
            }

            int best = TrackCatalog.Wrap(TrackId);
            int bestVotes = trackTally[best];
            for (int i = 0; i < count; i++)
            {
                if (trackTally[i] > bestVotes) { best = i; bestVotes = trackTally[i]; }
            }
            if (best != TrackId) TrackId = best;
        }

        /// <summary>Solo: the signs were picked on the menu, so go straight to the practice screen with them.</summary>
        private void StartSoloPractice()
        {
            var library = SignLibrary.Load();
            var chosen = GameSession.SelectedSigns;
            int count = 0;
            if (chosen != null)
            {
                foreach (string word in chosen)
                {
                    int index = library.FindIndex(w => string.Equals(w, word, System.StringComparison.OrdinalIgnoreCase));
                    if (index < 0 || count >= PlayerVote.MaxChoices) continue;
                    FinalWords.Set(count++, index);
                }
            }

            if (count == 0) // nothing to practise
            {
                cameraWait = 0f;
                StateValue = (int)RaceState.Waiting;
                return;
            }

            requiredKarts = KartsPresent;
            FinalWordCount = count;
            PhaseTimer = default; // no time limit in Solo
            StateValue = (int)RaceState.Review;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            switch (State)
            {
                case RaceState.Lobby:
                    // Solo skips the lobby screen: go on to the practice screen once the player's kart exists.
                    if (IsSolo && PlayerVote.All.Count > 0) StartSoloPractice();
                    else if (IsPublicRoom && !IsSolo) TickPublicLobby();
                    break;

                case RaceState.Waiting:
                    if (KartsPresent < PlayersNeeded)
                    {
                        cameraWait = 0f;
                        // A player who left (or lost connection) is not coming back: the room is closed. Stop waiting for them.
                        if (!IsSolo && requiredKarts > 0 && KartsPresent > 0)
                        {
                            missingWait += Runner.DeltaTime;
                            if (missingWait >= missingPlayerSeconds)
                            {
                                requiredKarts = KartsPresent;
                                missingWait = 0f;
                            }
                        }
                        break;
                    }
                    missingWait = 0f;

                    // Everyone is here. Start once every camera is ready (or the wait times out).
                    cameraWait += Runner.DeltaTime;
                    if (ReadyKarts >= KartsPresent || cameraWait >= cameraWaitTimeout)
                    {
                        cameraWait = 0f;
                        CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownSeconds);
                        RaceTime = 0f;
                        StateValue = (int)RaceState.Countdown;
                    }
                    break;

                case RaceState.Voting:
                    if (PhaseTimer.Expired(Runner) || PlayerVote.All.Count == 0 || AllVoters(v => v.IsDone))
                        FinishVoting();
                    break;

                case RaceState.Review:
                    if (PhaseTimer.Expired(Runner) || PlayerVote.All.Count == 0 || AllVoters(v => v.IsReady))
                    {
                        cameraWait = 0f;
                        StateValue = (int)RaceState.Waiting;
                    }
                    break;

                case RaceState.Countdown:
                    if (CountdownTimer.Expired(Runner)) StateValue = (int)RaceState.Racing;
                    break;

                case RaceState.Racing:
                    RaceTime += Runner.DeltaTime;
                    if (AllKartsFinished()) StateValue = (int)RaceState.Finished;
                    break;
            }
        }

        private static bool AllVoters(System.Func<PlayerVote, bool> test)
        {
            foreach (var vote in PlayerVote.All)
                if (!test(vote)) return false;
            return true;
        }

        /// <summary>Voting is over: pick the winning words, share them with everyone, and open the review phase.</summary>
        private void FinishVoting()
        {
            int wordCount = SignLibrary.Load().Count;
            List<int> winners = VoteTally.Pick(wordCount, PlayerVote.All, PlayerVote.MaxChoices);

            for (int i = 0; i < PlayerVote.MaxChoices; i++)
                FinalWords.Set(i, i < winners.Count ? winners[i] : 0);
            FinalWordCount = winners.Count;

            PhaseTimer = IsSolo ? default : TickTimer.CreateFromSeconds(Runner, reviewSeconds);
            StateValue = (int)RaceState.Review;
        }

        private static bool AllKartsFinished()
        {
            var karts = KartPathFollower.All;
            return karts.Count > 0 && karts.All(k => k.IsFinished);
        }

        /// <summary>Reset everyone and go back to Waiting (which starts a new countdown).</summary>
        public void Restart()
        {
            if (!HasStateAuthority) return;
            foreach (var kart in KartPathFollower.All) kart.RPC_ResetForNewRace();
            RaceTime = 0f;
            cameraWait = 0f;
            requiredKarts = KartsPresent;
            StateValue = (int)RaceState.Waiting;
        }

        /// <summary>All karts in race order: finished karts by finish time, then the rest by distance.</summary>
        public static List<KartPathFollower> GetStandings()
        {
            var list = new List<KartPathFollower>(KartPathFollower.All);
            list.Sort((a, b) =>
            {
                bool fa = a.IsFinished, fb = b.IsFinished;
                if (fa != fb) return fa ? -1 : 1;
                if (fa) return a.FinishTime.CompareTo(b.FinishTime);
                return b.Distance.CompareTo(a.Distance);
            });
            return list;
        }
    }
}
