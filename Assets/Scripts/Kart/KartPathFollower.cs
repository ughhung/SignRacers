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
    ///   LaneOffset                         - how far across the road it is (set by steering)
    ///   Boost / SuperBoost / Penalty / Slowed timers
    ///   Slots + ItemCount                  - a 2-slot item inventory (slot 0 = oldest)
    /// Position/rotation are derived from those every frame in Render(), so there is no
    /// NetworkTransform and very little data to sync.
    /// Only the state authority (the owner in Shared mode, the local player in Single mode)
    /// listens to the SignEventSource and writes this state. To affect ANOTHER kart, we
    /// send an RPC asking that kart's owner to apply the effect to itself.
    /// Steering: while the owner holds a finger on the screen (see SteeringInput), the kart
    /// slides across the road. Only the owner reads the screen, and only the owner writes LaneOffset.
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

        [Header("Steering")]
        [Tooltip("How fast the kart crosses the road at full steer, in world units per second.")]
        [SerializeField] private float steerSpeed = 6f;
        [Tooltip("How far from the centre line the kart may go. The road is 16 wide, so 7 leaves a little room at each edge.")]
        [SerializeField] private float maxLaneOffset = 7f;

        [Header("Visuals")]
        [Tooltip("How far the kart leans into a steer, in degrees.")]
        [SerializeField] private float maxLeanDegrees = 15f;
        [Tooltip("How quickly the lean follows the steer. Higher is snappier.")]
        [SerializeField] private float leanSmoothing = 8f;

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

        private TrackPath trackCache;
        /// <summary>The loaded track. Looked up again whenever the track scene has been swapped.</summary>
        private TrackPath track
        {
            get
            {
                if (trackCache == null) trackCache = FindFirstObjectByType<TrackPath>();
                return trackCache;
            }
        }

        // Drawing position, smoothed between simulation ticks so motion is fluid at any frame rate.
        private float visualDistance;
        private bool visualInitialized;
        private float localSpeed;       // the owner's speed this tick
        private float estimatedSpeed;   // everyone else's speed, worked out from how Distance changes
        private float lastSeenDistance;
        private float lastSeenTime;
        private SignEventSource source;

        // The player's steering this frame, read on the owner only (0 for everyone else and for bots).
        private float steerInput;

        // Lane bookkeeping and lean: the lane the kart was placed in (so a new race can put it back),
        // the lane as drawn (smoothed between network ticks), and the lean angle in degrees.
        private float spawnLane;
        private float visualLane;
        private bool laneVisualInitialized;
        private float leanDegrees;

        public bool IsBot => isBot;

        /// <summary>Name shown in the results for a bot ("Bot 1"). Set when the bot is created.</summary>
        public string BotName { get; set; } = "Bot";

        /// <summary>Multiplies this kart's speed. 1 normally. A bot's driver changes it to catch up or ease off. Only used by the owner.</summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>A bot takes a boost, as if it had signed a word correctly.</summary>
        public void BotBoost()
        {
            if (HasStateAuthority) HandleBoost();
        }

        /// <summary>A bot uses its oldest item, if it has one.</summary>
        public void BotUseItem()
        {
            if (HasStateAuthority && ItemCount > 0) HandleItemUse(0);
        }

        /// <summary>Which way the track points at the kart's position (unaffected by spin-outs). Used by the chase camera.</summary>
        public Vector3 TrackForward { get; private set; } = Vector3.forward;
        /// <summary>False before this kart has spawned and after it has been removed (for example after leaving a room). Networked values can only be read while true.</summary>
        private bool Live => Object != null && Object.IsValid;

        public bool IsFinished => Live && Finished;

        /// <summary>Has this player's camera come up? Bots have no camera, so they always count as ready.</summary>
        public bool IsSignReady => isBot || (Live && SignReady);

        /// <summary>Called by the kart's owner: true once its camera and the room's track are both ready.</summary>
        public void SetSignReady(bool ready)
        {
            if (HasStateAuthority && SignReady != ready) SignReady = ready;
        }

        /// <summary>Called by the kart's owner once its camera is ready.</summary>
        public void MarkSignReady()
        {
            if (HasStateAuthority) SignReady = true;
        }

        /// <summary>Whole laps completed so far.</summary>
        public int LapsCompleted =>
            Live && track != null && track.IsValid ? Mathf.FloorToInt(Distance / track.TotalLength) : 0;

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
        public void SetLaneOffset(float offset)
        {
            spawnLane = offset;
            LaneOffset = offset;
        }
        public bool IsBoosted => Live && IsActive(BoostTimer);
        public bool IsSuperBoosted => Live && IsActive(SuperBoostTimer);
        public bool IsPenalized => Live && IsActive(PenaltyTimer);
        public bool IsSlowed => Live && IsActive(SlowedTimer);

        public ItemType GetItem(int slot) =>
            Live && slot >= 0 && slot < ItemCount ? (ItemType)Slots[slot] : ItemType.None;

        private bool IsActive(TickTimer timer) =>
            Object != null && Object.IsValid && Runner != null && !timer.ExpiredOrNotRunning(Runner);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        public override void Spawned()
        {
            All.Add(this);


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

        // ---- Steering input (owner only, read every frame) ----

        private void Update()
        {
            // Only the player's own kart steers. Bots and other players' karts stay where they are.
            steerInput = Live && HasStateAuthority && !isBot ? SteeringInput.Read() : 0f;
        }

        // ---- Sign events (state authority only) ----

        private void HandleBoost() => BoostTimer = TickTimer.CreateFromSeconds(Runner, boostDuration);
        private void HandlePenalty() => PenaltyTimer = TickTimer.CreateFromSeconds(Runner, penaltyDuration);

        private void HandleItemUse(int slot)
        {
            if (!TakeItem(slot, out ItemType item)) return;
            ApplyItem(item);
        }

        /// <summary>
        /// Throws away the item in a slot with no effect. Used when the player signs an
        /// item's word wrong: the item is lost. Only the state authority may call this.
        /// </summary>
        public void DiscardItem(int slot)
        {
            if (!Live || !HasStateAuthority) return;
            if (TakeItem(slot, out ItemType item)) Debug.Log($"[Kart] Lost item after a wrong sign: {item}");
        }

        /// <summary>Removes the item from a slot and shifts the later ones down.</summary>
        private bool TakeItem(int slot, out ItemType item)
        {
            item = ItemType.None;
            if (slot < 0 || slot >= ItemCount)
            {
                Debug.Log($"[Kart] Item slot {slot} is empty, nothing to use.");
                return false;
            }

            item = (ItemType)Slots[slot];
            for (int i = slot; i < MaxItems - 1; i++) Slots.Set(i, Slots[i + 1]);
            Slots.Set(MaxItems - 1, 0);
            ItemCount--;
            return true;
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

        /// <summary>Testing cheat (see DebugCheats): start a super boost right now, no item or signing needed.</summary>
        public void DebugSuperBoost()
        {
            if (!HasStateAuthority) return;
            SuperBoostTimer = TickTimer.CreateFromSeconds(Runner, superBoostDuration);
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
            LaneOffset = spawnLane; // back to the lane it started in, not wherever the player steered to
            for (int i = 0; i < MaxItems; i++) Slots.Set(i, 0);
        }

        // ---- Simulation ----

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            localSpeed = 0f;
            if (Finished) return;                    // parked on the finish line
            if (!RaceManager.AllowsDriving) return;  // locked during waiting / countdown

            // Steering: slide across the road while the player holds a finger on the screen.
            if (steerInput != 0f)
                LaneOffset = Mathf.Clamp(LaneOffset + steerInput * steerSpeed * Runner.DeltaTime, -maxLaneOffset, maxLaneOffset);

            float speed = baseSpeed * SpeedScale;
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
            float lateralSpeed = UpdateVisualLane();
            transform.position = pos + right * visualLane + Vector3.up * heightOffset;

            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

            // Placeholder penalty visual: one full spin while the penalty is active.
            float? remaining = PenaltyTimer.RemainingTime(Runner);
            if (remaining.HasValue && remaining.Value > 0f)
            {
                float progress = 1f - (remaining.Value / penaltyDuration);
                rot *= Quaternion.Euler(0f, 360f * progress, 0f);
            }

            // Lean into the steer: tilt the kart toward the way it is sliding across the road.
            // Negative because a positive roll tips the top of the kart to the left.
            rot *= Quaternion.Euler(0f, 0f, -UpdateLean(lateralSpeed));

            transform.rotation = rot;
        }

        /// <summary>
        /// Eases the drawn lane toward the networked LaneOffset every frame, so a steer moves smoothly
        /// instead of stepping on each network tick. Returns how fast the drawn lane is sliding, in units per second.
        /// Big jumps (for example a race restart) snap straight to the new lane.
        /// </summary>
        private float UpdateVisualLane()
        {
            float dt = Time.deltaTime;
            if (!laneVisualInitialized || Mathf.Abs(LaneOffset - visualLane) > 5f)
            {
                visualLane = LaneOffset;
                laneVisualInitialized = true;
                return 0f;
            }

            float previous = visualLane;
            visualLane = Mathf.Lerp(visualLane, LaneOffset, 1f - Mathf.Exp(-15f * dt));
            return dt > 0f ? (visualLane - previous) / dt : 0f;
        }

        /// <summary>
        /// How far to lean this frame, in degrees (positive = leaning right). It is worked out from how fast
        /// the drawn lane is sliding, so it works for every kart, including other players', and it stays smooth
        /// because the lane it reads from is already smoothed.
        /// </summary>
        private float UpdateLean(float lateralSpeed)
        {
            float dt = Time.deltaTime;

            // Full lean at full steer speed.
            float steer = Mathf.Clamp(lateralSpeed / steerSpeed, -1f, 1f);
            leanDegrees = Mathf.Lerp(leanDegrees, steer * maxLeanDegrees, 1f - Mathf.Exp(-leanSmoothing * dt));
            return leanDegrees;
        }
    }
}
