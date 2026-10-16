using System.Collections.Generic;
using Fusion;
using SignRacers.Events;
using SignRacers.Race;
using SignRacers.Track;
using UnityEngine;
using UnityEngine.Serialization;

namespace SignRacers.Kart
{
    /// <summary>
    /// The networked kart. Its entire shared state is:
    ///   Distance                           - how far along the track it is
    ///   Boost / SuperBoost / Penalty / Slowed timers
    ///   Slots + ItemCount                  - a 2-slot item inventory (slot 0 = oldest)
    /// Position/rotation are derived from those every frame in Render(), so there is no
    /// NetworkTransform and very little data to sync.
    /// Only the state authority (the owner in Shared mode, the local player in Single mode)
    /// listens to the SignEventSource and writes this state. To affect ANOTHER kart, we
    /// send an RPC asking that kart's owner to apply the effect to itself.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class KartPathFollower : NetworkBehaviour
    {
        public const int MaxItems = 2;

        /// <summary>Every kart currently spawned on this device.</summary>
        public static readonly List<KartPathFollower> All = new List<KartPathFollower>();

        [Header("Speeds")]
        [SerializeField] private float baseSpeed = 8f;
        [SerializeField] private float boostMultiplier = 2.5f;
        [SerializeField] private float superBoostMultiplier = 4.5f;
        [SerializeField] private float penaltyMultiplier = 0.4f;
        [SerializeField] private float slowedMultiplier = 0.5f;

        [Header("Durations (seconds)")]
        [SerializeField] private float boostDuration = 1.5f;
        [SerializeField] private float superBoostDuration = 2.5f;
        [SerializeField] private float penaltyDuration = 1.5f;
        [SerializeField] private float slowedDuration = 3f;

        [Header("Layout")]
        [SerializeField] private float heightOffset = 0.5f;

        [Header("Setup")]
        [Tooltip("Bots are AI karts. They collect items and use a BotSignSource, but the camera and HUD ignore them.")]
        [FormerlySerializedAs("isDummy")]
        [SerializeField] private bool isBot;

        [Networked] public float Distance { get; set; }
        [Networked] public float LaneOffset { get; set; }
        [Networked] public NetworkBool Finished { get; set; }
        [Networked] public NetworkBool SignReady { get; set; }
        [Networked] public float FinishTime { get; set; }
        [Networked] private TickTimer BoostTimer { get; set; }
        [Networked] private TickTimer SuperBoostTimer { get; set; }
        [Networked] private TickTimer PenaltyTimer { get; set; }
        [Networked] private TickTimer SlowedTimer { get; set; }
        [Networked] public int ItemCount { get; set; }
        [Networked, Capacity(2)] private NetworkArray<int> Slots => default;

        private TrackPath track;

        // Drawing position, smoothed between simulation ticks so motion is fluid at any frame rate.
        private float visualDistance;
        private bool visualInitialized;
        private float localSpeed;       // the owner's speed this tick
        private float estimatedSpeed;   // everyone else's speed, worked out from how Distance changes
        private float lastSeenDistance;
        private float lastSeenTime;
        private SignEventSource source;

        public bool IsBot => isBot;

        /// <summary>Which way the track points at the kart's position (unaffected by spin-outs). Used by the chase camera.</summary>
        public Vector3 TrackForward { get; private set; } = Vector3.forward;
        public bool IsFinished => Finished;

        /// <summary>Has this player's camera come up? Bots have no camera, so they always count as ready.</summary>
        public bool IsSignReady => SignReady || isBot;

        /// <summary>Called by the kart's owner once its camera is ready.</summary>
        public void MarkSignReady()
        {
            if (HasStateAuthority) SignReady = true;
        }

        /// <summary>Whole laps completed so far.</summary>
        public int LapsCompleted =>
            track != null && track.IsValid ? Mathf.FloorToInt(Distance / track.TotalLength) : 0;

        /// <summary>The lap to show on the HUD (1-based, capped at the race length).</summary>
        public int CurrentLap
        {
            get
            {
                int lap = LapsCompleted + 1;
                var race = RaceManager.Instance;
                return race != null ? Mathf.Min(lap, race.TotalLaps) : lap;
            }
        }

        /// <summary>Set before spawning (see GameBootstrap) so karts drive side by side instead of overlapping.</summary>
        public void SetLaneOffset(float offset) => LaneOffset = offset;
        public bool IsBoosted => IsActive(BoostTimer);
        public bool IsSuperBoosted => IsActive(SuperBoostTimer);
        public bool IsPenalized => IsActive(PenaltyTimer);
        public bool IsSlowed => IsActive(SlowedTimer);

        public ItemType GetItem(int slot) =>
            slot >= 0 && slot < ItemCount ? (ItemType)Slots[slot] : ItemType.None;

        private bool IsActive(TickTimer timer) =>
            Object != null && Object.IsValid && Runner != null && !timer.ExpiredOrNotRunning(Runner);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        public override void Spawned()
        {
            All.Add(this);

            track = FindFirstObjectByType<TrackPath>();
            if (track == null) Debug.LogError("[Kart] No TrackPath found in the scene.", this);

            if (HasStateAuthority)
            {
                source = GetComponent<SignEventSource>();
                if (source == null)
                {
                    if (!isBot) Debug.LogWarning("[Kart] No SignEventSource on this kart, it will never boost.", this);
                    return;
                }
                source.OnBoostTriggered += HandleBoost;
                source.OnPenaltyTriggered += HandlePenalty;
                source.OnItemUseTriggered += HandleItemUse;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);

            if (source == null) return;
            source.OnBoostTriggered -= HandleBoost;
            source.OnPenaltyTriggered -= HandlePenalty;
            source.OnItemUseTriggered -= HandleItemUse;
            source = null;
        }

        // ---- Sign events (state authority only) ----

        private void HandleBoost() => BoostTimer = TickTimer.CreateFromSeconds(Runner, boostDuration);
        private void HandlePenalty() => PenaltyTimer = TickTimer.CreateFromSeconds(Runner, penaltyDuration);

        private void HandleItemUse(int slot)
        {
            if (slot < 0 || slot >= ItemCount)
            {
                Debug.Log($"[Kart] Item slot {slot} is empty, nothing to use.");
                return;
            }

            // Take the item from that slot and shift the later ones down.
            ItemType item = (ItemType)Slots[slot];
            for (int i = slot; i < MaxItems - 1; i++) Slots.Set(i, Slots[i + 1]);
            Slots.Set(MaxItems - 1, 0);
            ItemCount--;

            ApplyItem(item);
        }

        private void ApplyItem(ItemType item)
        {
            Debug.Log($"[Kart] Used item: {item}");
            switch (item)
            {
                case ItemType.SuperBoost:
                    SuperBoostTimer = TickTimer.CreateFromSeconds(Runner, superBoostDuration);
                    break;

                case ItemType.SlowOthers:
                    foreach (var other in All)
                    {
                        if (other == this) continue;
                        other.RPC_ApplySlow(); // ask the other kart's owner to slow itself
                    }
                    break;
            }
        }

        /// <summary>Called by a mystery box. Returns false if the inventory is full.</summary>
        public bool TryAddItem(ItemType item)
        {
            if (!HasStateAuthority || ItemCount >= MaxItems || item == ItemType.None) return false;
            Slots.Set(ItemCount, (int)item);
            ItemCount++;
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ApplySlow()
        {
            SlowedTimer = TickTimer.CreateFromSeconds(Runner, slowedDuration);
        }

        /// <summary>Sent by the RaceManager on "race again": back to the start line, clean slate.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ResetForNewRace()
        {
            Distance = 0f;
            Finished = false;
            FinishTime = 0f;
            BoostTimer = default;
            SuperBoostTimer = default;
            PenaltyTimer = default;
            SlowedTimer = default;
            ItemCount = 0;
            for (int i = 0; i < MaxItems; i++) Slots.Set(i, 0);
        }

        // ---- Simulation ----

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            localSpeed = 0f;
            if (Finished) return;                    // parked on the finish line
            if (!RaceManager.AllowsDriving) return;  // locked during waiting / countdown

            float speed = baseSpeed;
            if (!BoostTimer.ExpiredOrNotRunning(Runner)) speed *= boostMultiplier;
            if (!SuperBoostTimer.ExpiredOrNotRunning(Runner)) speed *= superBoostMultiplier;
            if (!PenaltyTimer.ExpiredOrNotRunning(Runner)) speed *= penaltyMultiplier;
            if (!SlowedTimer.ExpiredOrNotRunning(Runner)) speed *= slowedMultiplier;

            Distance += speed * Runner.DeltaTime;
            localSpeed = speed;

            var race = RaceManager.Instance;
            if (race != null && track != null && track.IsValid)
            {
                float raceLength = race.TotalLaps * track.TotalLength;
                if (Distance >= raceLength)
                {
                    Distance = raceLength;
                    Finished = true;
                    FinishTime = race.RaceTime;
                }
            }
        }

        /// <summary>
        /// Moves the drawn position forward every frame at the kart's current speed, and gently
        /// pulls it toward the networked Distance. The networked value only changes on simulation
        /// ticks, so drawing it directly makes the kart advance in small steps (visible as jitter
        /// when the frame rate is higher than the tick rate).
        /// </summary>
        private void UpdateVisualDistance()
        {
            float networked = Distance;
            float now = Time.time;

            // Snap on the first frame, and after big jumps (a restart sends the kart back to the start).
            if (!visualInitialized || Mathf.Abs(networked - visualDistance) > 5f)
            {
                visualDistance = networked;
                visualInitialized = true;
                lastSeenDistance = networked;
                lastSeenTime = now;
                estimatedSpeed = 0f;
                return;
            }

            float speed;
            if (HasStateAuthority)
            {
                speed = localSpeed;
            }
            else
            {
                // Someone else's kart: measure its speed from how fast its Distance changes.
                if (!Mathf.Approximately(networked, lastSeenDistance))
                {
                    float elapsed = now - lastSeenTime;
                    if (elapsed > 0.0001f)
                    {
                        float measured = Mathf.Max(0f, (networked - lastSeenDistance) / elapsed);
                        estimatedSpeed = Mathf.Lerp(estimatedSpeed, measured, 0.3f);
                    }
                    lastSeenDistance = networked;
                    lastSeenTime = now;
                }
                else if (now - lastSeenTime > 0.2f)
                {
                    estimatedSpeed = 0f; // it has stopped
                }
                speed = estimatedSpeed;
            }

            float dt = Time.deltaTime;
            visualDistance += speed * dt;
            visualDistance = Mathf.Lerp(visualDistance, networked, 1f - Mathf.Exp(-15f * dt));
        }

        // Render() runs every frame and reads interpolated networked values,
        // so movement looks smooth on every device regardless of tick rate.
        public override void Render()
        {
            if (track == null || !track.IsValid) return;

            UpdateVisualDistance();
            Vector3 pos = track.GetPosition(visualDistance, out Vector3 forward);
            TrackForward = forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            transform.position = pos + right * LaneOffset + Vector3.up * heightOffset;

            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

            // Placeholder penalty visual: one full spin while the penalty is active.
            float? remaining = PenaltyTimer.RemainingTime(Runner);
            if (remaining.HasValue && remaining.Value > 0f)
            {
                float progress = 1f - (remaining.Value / penaltyDuration);
                rot *= Quaternion.Euler(0f, 360f * progress, 0f);
            }

            transform.rotation = rot;
        }
    }
}
