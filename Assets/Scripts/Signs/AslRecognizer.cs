using System;
using Gtk.Unity;
using UnityEngine;

namespace SignRacers.Signs
{
    /// <summary>The outcome of one recognition: best label, its score, and how far ahead
    /// of the runner-up it was. The new toolkit only reports the best sign and its score, so Margin is always 0.</summary>
    public readonly struct AslResult
    {
        public readonly string Label;
        public readonly float Score;
        public readonly float Margin;

        public AslResult(string label, float score, float margin)
        {
            Label = label;
            Score = score;
            Margin = margin;
        }

        public bool HasLabel => !string.IsNullOrEmpty(Label);
        public static readonly AslResult None = new AslResult(null, 0f, 0f);
    }

    /// <summary>
    /// The game's recognizer, backed by the SLRGTk toolkit's SignRecognizer (the component that is also the camera view).
    ///
    /// The toolkit answers ASYNCHRONOUSLY: Trigger() sends a request and the answer arrives on a later frame through the
    /// Recognized event. So Recognize(callback) asks, and calls the callback with the result (or None after a timeout).
    ///   BeginCapture()          -> forget what the camera saw before now
    ///   Recognize(done)         -> classify what was captured; done(result) is called a few frames later
    ///
    /// Setup: add GameObject > UI > SLRGTk > Sign Recognizer, UNTICK its "Tap To Trigger" (the game decides when to check),
    /// then put this component on the ASL object and drag the Sign Recognizer into the slot.
    /// </summary>
    public class AslRecognizer : MonoBehaviour
    {
        [Tooltip("The toolkit's Sign Recognizer. Leave empty to find the one in the scene.")]
        [SerializeField] private SignRecognizer recognizer;

        [Tooltip("The toolkit has no 'ready' flag, so the camera counts as ready this long after it is enabled.")]
        [SerializeField] private float warmupSeconds = 2f;

        [Tooltip("How long to wait for the toolkit's answer before giving up (the result is then 'nothing').")]
        [SerializeField] private float answerTimeoutSeconds = 3f;

        // The signs recognition is limited to, until the game calls SetVocabulary().
        [NonSerialized] private string[] signs = { "yellow", "red", "owl", "dad", "elephant" };

        private Action<AslResult> pendingDone;
        private float pendingStart;
        private float enabledAt = -1f;
        private float lastClearTime = -1f;

        /// <summary>The Sign Recognizer view, so the HUD can place or show it.</summary>
        public SignRecognizer View => recognizer;

        /// <summary>True once the camera has had time to start and recognition can run.</summary>
        public bool IsReady => recognizer != null && recognizer.isActiveAndEnabled &&
                               enabledAt >= 0f && Time.realtimeSinceStartup - enabledAt >= warmupSeconds;

        /// <summary>Is a hand in frame right now?</summary>
        public bool IsHandPresent => recognizer != null && recognizer.HandPresent;

        /// <summary>True while a Recognize() request is waiting for its answer.</summary>
        public bool IsRecognizing => pendingDone != null;

        /// <summary>Seconds since the capture window opened (last BeginCapture or recognition), or -1.</summary>
        public float CaptureSeconds => lastClearTime < 0f ? -1f : Time.realtimeSinceStartup - lastClearTime;

        /// <summary>How long the last Recognize() took from asking to the answer arriving. It does NOT block the game.</summary>
        public float LastRecognitionMs { get; private set; }

        // ---- Public API ----

        /// <summary>Limit recognition to these signs (the player's equipped signs).</summary>
        public void SetVocabulary(string[] newSigns)
        {
            signs = newSigns;
            ApplyFocus();
        }

        /// <summary>Open a fresh capture window: forget whatever the camera saw before now.</summary>
        public void BeginCapture()
        {
            if (recognizer == null) return;
            recognizer.Clear();
            lastClearTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Classify the current capture window. done(result) is called on a later frame with the answer, or with
        /// AslResult.None if nothing was recognized or the toolkit didn't answer in time. Returns false (and never calls
        /// done) if a previous request is still waiting.
        /// </summary>
        public bool Recognize(Action<AslResult> done)
        {
            if (done == null) return false;
            if (recognizer == null)
            {
                Debug.LogWarning("[ASL] Recognize ignored: no SignRecognizer.");
                done(AslResult.None);
                return true;
            }
            if (pendingDone != null)
            {
                Debug.LogWarning("[ASL] Recognize ignored: the previous request is still waiting for its answer.");
                return false;
            }

            pendingDone = done;
            pendingStart = Time.realtimeSinceStartup;
            recognizer.Trigger();
            return true;
        }

        // ---- Setup ----

        private void Awake()
        {
            if (recognizer == null) recognizer = FindFirstObjectByType<SignRecognizer>();
            if (recognizer == null) Debug.LogError("[ASL] No SignRecognizer in the scene. Add one with GameObject > UI > SLRGTk > Sign Recognizer.");
        }

        private void OnEnable()
        {
            enabledAt = Time.realtimeSinceStartup;
            if (recognizer == null) return;
            recognizer.Recognized.AddListener(OnRecognized);
            ApplyFocus();
        }

        private void OnDisable()
        {
            if (recognizer != null) recognizer.Recognized.RemoveListener(OnRecognized);
            Complete(AslResult.None); // never leave a caller waiting forever
        }

        private void Start()
        {
            ApplyFocus();
        }

        private void Update()
        {
            if (pendingDone != null && Time.realtimeSinceStartup - pendingStart > answerTimeoutSeconds)
            {
                Debug.LogWarning("[ASL] The toolkit didn't answer in time; counting it as nothing.");
                if (recognizer != null) recognizer.Clear();
                Complete(AslResult.None);
            }
        }

        // The toolkit calls this on the main thread, a frame or so after Trigger().
        private void OnRecognized(string label, float score)
        {
            if (pendingDone == null) return; // not ours (for example a tap while Tap To Trigger is on)

            AslResult result = string.IsNullOrEmpty(label) || label == SignRecognition.NoSign
                ? AslResult.None
                : new AslResult(StripGallery(label), score, 0f);

            // Start the next window clean.
            if (recognizer != null) recognizer.Clear();
            lastClearTime = Time.realtimeSinceStartup;
            Complete(result);
        }

        private void Complete(AslResult result)
        {
            Action<AslResult> done = pendingDone;
            if (done == null) return;
            pendingDone = null; // first, so the callback may ask again
            LastRecognitionMs = (Time.realtimeSinceStartup - pendingStart) * 1000f;
            done(result);
        }

        private void ApplyFocus()
        {
            if (recognizer == null || signs == null || signs.Length == 0) return;
            try
            {
                // With ONE gallery active, plain words work. With several, the toolkit needs "gallery:word"
                // and throws otherwise, so leave only popsign ticked on the Sign Recognizer.
                recognizer.Focus(signs);
            }
            catch (ArgumentException ex)
            {
                Debug.LogError("[ASL] Focus rejected the sign list: " + ex.Message);
            }
        }

        /// <summary>Labels may come back as "popsign:owl". The game only wants "owl".</summary>
        private static string StripGallery(string label)
        {
            int colon = label.IndexOf(':');
            return colon >= 0 ? label.Substring(colon + 1) : label;
        }
    }
}
