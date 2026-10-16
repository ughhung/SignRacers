using System.Collections.Generic;
using System.Linq;
using Fusion;
using SignRacers.Core;
using SignRacers.Kart;
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

        private float cameraWait;
        private int requiredKarts; // set when the host starts from the lobby

        [Networked] private int StateValue { get; set; }
        [Networked] private TickTimer CountdownTimer { get; set; }
        [Networked] public float RaceTime { get; set; }
        [Networked] private TickTimer PhaseTimer { get; set; }
        [Networked, Capacity(5)] private NetworkArray<int> FinalWords => default;
        [Networked] public int FinalWordCount { get; set; }

        public RaceState State => (RaceState)StateValue;
        public int TotalLaps => totalLaps;
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
            if (HasStateAuthority && GameSession.StartedFromMenu && GameSession.Mode == SessionMode.Multiplayer)
                StateValue = (int)RaceState.Lobby;
        }

        /// <summary>Host only: leave the lobby, close the room to newcomers, and get the race going.</summary>
        public void StartFromLobby()
        {
            if (!HasStateAuthority || State != RaceState.Lobby) return;

            requiredKarts = KartsPresent; // everyone who is here now
            if (Runner.SessionInfo.IsValid) Runner.SessionInfo.IsOpen = false; // nobody joins mid-race

            if (votingEnabled)
            {
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, votingSeconds);
                StateValue = (int)RaceState.Voting;
            }
            else
            {
                StateValue = (int)RaceState.Waiting;
            }
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

            PhaseTimer = TickTimer.CreateFromSeconds(Runner, reviewSeconds);
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
