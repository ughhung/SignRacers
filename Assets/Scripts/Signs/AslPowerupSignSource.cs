using System.Collections.Generic;
using SignRacers.Core;
using SignRacers.Events;
using SignRacers.Kart;
using SignRacers.Race;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SignRacers.Signs
{
    public enum PromptResult { None, Correct, Wrong }
    public enum PowerupAction { None, Boost, Item }

    /// <summary>
    /// The game's signing rules, driven by the real recognizer. It is a SignEventSource, so
    /// the kart listens to it exactly as it listened to the keyboard source.
    ///
    ///   1. The player taps BOOST, ITEM 1 or ITEM 2.
    ///   2. A random word from the equipped sign pool appears and a clean capture starts.
    ///   3. The player signs it. After the signing window (or a second tap on the same
    ///      button) the recognizer says which word it saw.
    ///   4. Right word -> the effect happens (boost, or that item is used).
    ///      Anything else -> penalty. A failed item attempt does NOT use up the item.
    /// Only the kart this device owns runs this, so remote karts never use the camera.
    /// </summary>
    [RequireComponent(typeof(KartPathFollower))]
    public class AslPowerupSignSource : SignEventSource
    {
        [Header("Sign pool (must exist in the recognizer's gallery)")]
        [SerializeField] private string[] equippedSigns = { "yellow", "duck", "clown" };

        [Header("Timing (seconds)")]
        [Tooltip("How long the player has to sign before the game checks automatically.")]
        [SerializeField] private float signingWindow = 3f;
        [Tooltip("Minimum wait between boost attempts, so boosting can't be spammed.")]
        [SerializeField] private float boostCooldown = 1.5f;
        [Tooltip("Short pause after any result, so a double tap can't start another attempt.")]
        [SerializeField] private float lockoutAfterResult = 0.5f;

        [Header("Testing")]
        [Tooltip("Keyboard shortcuts for testing without a camera: Q = boost, W = item 1, E = item 2, Space = check now, 1-5 = answer with a pool word. Off for normal play.")]
        [SerializeField] private bool enableDebugKeys = false;

        private enum Hotkey { Boost, Item1, Item2, CheckNow, Answer1, Answer2, Answer3, Answer4, Answer5 }

        private KartPathFollower kart;
        private AslRecognizer recognizer;
        private bool setupDone;
        private bool warnedNoRecognizer;
        private float windowEndsAt;
        private float nextBoostAllowedAt;
        private float lockUntil;
        private string lastWord;
        private int appliedSignsVersion;

        // ---- Read by the HUD ----
        public bool IsSigning { get; private set; }
        public string Word { get; private set; }
        public PowerupAction PendingAction { get; private set; }
        public int PendingSlot { get; private set; }
        public PromptResult LastResult { get; private set; }
        public float LastResultTime { get; private set; } = -100f;

        public bool CameraReady => recognizer != null && recognizer.IsReady;
        public bool DebugKeys => enableDebugKeys;
        public IReadOnlyList<string> EquippedSigns => equippedSigns;
        public float TimeLeft01 => IsSigning ? Mathf.Clamp01((windowEndsAt - Time.time) / signingWindow) : 0f;
        public float BoostCooldownLeft => Mathf.Max(0f, nextBoostAllowedAt - Time.time);

        private bool IsActiveKart =>
            kart.Object != null && kart.Object.IsValid && kart.HasStateAuthority && !kart.IsBot;

        private bool Ready => IsActiveKart && !IsSigning && Time.time >= lockUntil
                            && RaceManager.AllowsDriving && !kart.IsFinished;
        public bool CanStartBoost => Ready && Time.time >= nextBoostAllowedAt;
        public bool CanStartItem(int slot) => Ready && slot >= 0 && slot < kart.ItemCount;

        private void Awake() => kart = GetComponent<KartPathFollower>();

        // ---- Actions (called by the HUD buttons and the debug keys) ----

        public void StartBoost()
        {
            if (CanStartBoost) BeginAttempt(PowerupAction.Boost, 0);
        }

        public void StartItem(int slot)
        {
            if (CanStartItem(slot)) BeginAttempt(PowerupAction.Item, slot);
        }

        /// <summary>Check right now instead of waiting for the signing window to end.</summary>
        public void CheckNow()
        {
            if (IsSigning) RecognizeNow();
        }

        private void BeginAttempt(PowerupAction action, int slot)
        {
            IsSigning = true;
            PendingAction = action;
            PendingSlot = slot;
            Word = Pick();
            windowEndsAt = Time.time + signingWindow;
            if (CameraReady) recognizer.BeginCapture();
        }

        // ---- Per-frame ----

        private void Update()
        {
            if (!IsActiveKart) return;
            if (!setupDone) Setup();

            // Tell the race this player's camera is up. With no recognizer in the scene
            // (debug keys only) there is nothing to wait for.
            var raceManager = RaceManager.Instance;
            bool trackReady = raceManager == null || raceManager.TrackReady;
            kart.SetSignReady(trackReady && (recognizer == null || recognizer.IsReady));

            // A room finished voting: switch to the winning words.
            if (appliedSignsVersion != GameSession.SignsVersion && GameSession.HasSelection)
            {
                equippedSigns = GameSession.SelectedSigns;
                appliedSignsVersion = GameSession.SignsVersion;
                if (recognizer != null) recognizer.SetVocabulary(equippedSigns);
            }

            if (enableDebugKeys)
            {
                if (!IsSigning)
                {
                    if (Pressed(Hotkey.Boost)) StartBoost();
                    if (Pressed(Hotkey.Item1)) StartItem(0);
                    if (Pressed(Hotkey.Item2)) StartItem(1);
                }
                else
                {
                    if (Pressed(Hotkey.CheckNow)) CheckNow();
                    for (int i = 0; i < equippedSigns.Length && i < 5 && IsSigning; i++)
                    {
                        if (Pressed(Hotkey.Answer1 + i)) Finish(equippedSigns[i]);
                    }
                }
            }

            if (IsSigning && Time.time >= windowEndsAt) RecognizeNow();
        }

        private void Setup()
        {
            setupDone = true;
            if (GameSession.HasSelection) equippedSigns = GameSession.SelectedSigns;
            appliedSignsVersion = GameSession.SignsVersion;

            recognizer = FindFirstObjectByType<AslRecognizer>();
            if (recognizer != null) recognizer.SetVocabulary(equippedSigns);
            else if (!warnedNoRecognizer)
            {
                warnedNoRecognizer = true;
                Debug.LogWarning("[Powerups] No AslRecognizer in the scene. Only the debug keys will work.");
            }
        }

        private void RecognizeNow()
        {
            if (!CameraReady)
            {
                // Not the player's fault, so cancel the attempt without a penalty.
                Debug.LogWarning("[Powerups] The camera isn't ready, attempt cancelled.");
                IsSigning = false;
                Word = null;
                return;
            }

            AslResult result = recognizer.Recognize();
            Finish(result.HasLabel ? result.Label : null);
        }

        private void Finish(string answer)
        {
            bool correct = answer != null &&
                           string.Equals(answer, Word, System.StringComparison.OrdinalIgnoreCase);
            PowerupAction action = PendingAction;
            int slot = PendingSlot;

            IsSigning = false;
            lastWord = Word;
            Word = null;
            lockUntil = Time.time + lockoutAfterResult;
            if (action == PowerupAction.Boost) nextBoostAllowedAt = Time.time + boostCooldown;

            LastResult = correct ? PromptResult.Correct : PromptResult.Wrong;
            LastResultTime = Time.time;

            if (!correct) { RaisePenalty(); return; }

            if (action == PowerupAction.Boost) RaiseBoost();
            else RaiseItemUse(slot);
        }

        /// <summary>A random word from the pool, avoiding the previous one when possible.</summary>
        private string Pick()
        {
            var candidates = new List<string>();
            foreach (var s in equippedSigns) if (s != lastWord) candidates.Add(s);
            if (candidates.Count == 0) candidates.AddRange(equippedSigns);
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

#if ENABLE_INPUT_SYSTEM
        private static bool Pressed(Hotkey key)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case Hotkey.Boost: return kb.qKey.wasPressedThisFrame;
                case Hotkey.Item1: return kb.wKey.wasPressedThisFrame;
                case Hotkey.Item2: return kb.eKey.wasPressedThisFrame;
                case Hotkey.CheckNow: return kb.spaceKey.wasPressedThisFrame;
                case Hotkey.Answer1: return kb.digit1Key.wasPressedThisFrame;
                case Hotkey.Answer2: return kb.digit2Key.wasPressedThisFrame;
                case Hotkey.Answer3: return kb.digit3Key.wasPressedThisFrame;
                case Hotkey.Answer4: return kb.digit4Key.wasPressedThisFrame;
                case Hotkey.Answer5: return kb.digit5Key.wasPressedThisFrame;
                default: return false;
            }
        }
#else
        private static bool Pressed(Hotkey key)
        {
            switch (key)
            {
                case Hotkey.Boost: return Input.GetKeyDown(KeyCode.Q);
                case Hotkey.Item1: return Input.GetKeyDown(KeyCode.W);
                case Hotkey.Item2: return Input.GetKeyDown(KeyCode.E);
                case Hotkey.CheckNow: return Input.GetKeyDown(KeyCode.Space);
                case Hotkey.Answer1: return Input.GetKeyDown(KeyCode.Alpha1);
                case Hotkey.Answer2: return Input.GetKeyDown(KeyCode.Alpha2);
                case Hotkey.Answer3: return Input.GetKeyDown(KeyCode.Alpha3);
                case Hotkey.Answer4: return Input.GetKeyDown(KeyCode.Alpha4);
                case Hotkey.Answer5: return Input.GetKeyDown(KeyCode.Alpha5);
                default: return false;
            }
        }
#endif
    }
}
