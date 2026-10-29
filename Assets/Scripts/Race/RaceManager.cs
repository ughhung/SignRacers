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

        private float cameraWait;
        private int requiredKarts; // set when the host starts from the lobby

        [Networked] private int StateValue { get; set; }
        [Networked] private TickTimer CountdownTimer { get; set; }
        [Networked] public float RaceTime { get; set; }
        [Networked] private TickTimer PhaseTimer { get; set; }
        [Networked, Capacity(5)] private NetworkArray<int> FinalWords => default;
        [Networked] public int FinalWordCount { get; set; }

        /// <summary>Which track the room races on (a position in TrackCatalog). The host picks it in the lobby.</summary>
        [Networked] public int TrackId { get; set; }

        private bool trackLoading;
        private string trackFailed;

        /// <summary>True once this device has the room's chosen track loaded.</summary>
        public bool TrackReady => !trackLoading && TrackLoader.LoadedName == TrackCatalog.SceneName(TrackId);

        /// <summary>Host only, lobby only: step to the next (+1) or previous (-1) track.</summary>
        public void ChangeTrack(int step)
        {
            if (!HasStateAuthority || State != RaceState.Lobby) return;
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

            // An online room starts in the lobby. Everything else goes straight to
            // waiting for cameras. (Only the owner sets state, others just receive it.)
            // An online room starts in the lobby. Solo picks its signs on the menu, then gets the practice
            // screen (the lobby is skipped). Played directly in the editor: straight to waiting.
            // (Only the owner sets state, others just receive it.)
            if (HasStateAuthority && GameSession.StartedFromMenu &&
                (GameSession.Mode == SessionMode.Multiplayer || soloPractice))
                StateValue = (int)RaceState.Lobby;
        }

        /// <summary>Host only: leave the lobby, close the room to newcomers, and get the race going.</summary>
        public void StartFromLobby()
        {
            if (!HasStateAuthority || State != RaceState.Lobby) return;

            requiredKarts = KartsPresent; // everyone who is here now
            if (!IsSolo && Runner.SessionInfo.IsValid) Runner.SessionInfo.IsOpen = false; // nobody joins mid-race

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
                    break;

                case RaceState.Waiting:
                    if (KartsPresent < PlayersNeeded)
                    {
                        cameraWait = 0f;
                        break;
                    }

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
