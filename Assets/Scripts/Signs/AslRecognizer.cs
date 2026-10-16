using System;
using System.Linq;
using GTk.Bindings;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.Signs
{
    /// <summary>The outcome of one recognition: best label, its score, and how far ahead
    /// of the runner-up it was (a small margin means the model was unsure).</summary>
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
    /// The game's own copy of the sign-recognition setup, adapted from the toolkit's
    /// InteractiveSLRSample (which stays untouched). Use THIS INSTEAD OF that sample, never
    /// alongside it in the same scene, because both would try to open the camera pipeline.
    ///
    /// The toolkit recognizes ON DEMAND, not continuously:
    ///   BeginCapture() -> clears the frame buffer; the player signs
    ///   Recognize()    -> classifies everything captured since, returns the top match
    /// A sign clip is about 2 seconds, so keep the capture window around that long.
    /// Recognize() always returns the closest word in the vocabulary, even when nobody
    /// signed anything, so callers must judge Score / Margin / IsHandPresent themselves.
    /// </summary>
    public class AslRecognizer : MonoBehaviour
    {
        [Header("Preview")]
        [Tooltip("Where the camera preview is shown. Required.")]
        [SerializeField] private RawImage previewImage;

        [Tooltip("Auto decides from the graphics API. Override only if Auto is wrong on this device.")]
        [SerializeField] private SLRTk.SlrtkPreviewOrientation.FlipOverride forceFlipPreview =
            SLRTk.SlrtkPreviewOrientation.FlipOverride.Auto;

        [Tooltip("Mirror the preview like a selfie camera. Display only, never affects results.")]
        [SerializeField] private bool mirrorPreview = true;

        [Header("Recognition")]
        [Tooltip("Which gallery backs recognition. Naming it is load-bearing, see SetupPipeline.")]
        [SerializeField] private string gallery = "popsign";
        [SerializeField] private int topK = 5;

        [Header("Overlay (turn off to save performance)")]
        [SerializeField] private bool enableOverlay = true;
        [SerializeField] private bool drawPose = true;

        [Header("Vocabulary")]
        [Tooltip("Signs recognition is limited to. Overwritten at runtime by SetVocabulary().")]
        [SerializeField] private string[] signs = { "yellow", "red", "owl", "dad", "elephant" };

        // The pipeline and its texture live for the whole app, not for one scene.
        // Closing and reopening the pipeline (or rebinding a fresh texture) is not a cycle
        // the native side survives, and the camera takes seconds to warm up each time. So:
        // open once, keep the same texture bound for the app's life, and only manage
        // RECORDING around scene changes and app pauses.
        private static DynamicSLRPipelineService pipeline;
        private static RenderTexture dynamicSlrTexture;
        private static bool recording;
        private static bool quitHooked;

        private float lastClearTime = -1f;

        /// <summary>True once the camera pipeline is open and recognition can run.</summary>
        public bool IsReady => pipeline != null;

        /// <summary>Is a hand (together with its same-side shoulder) in frame right now?</summary>
        public bool IsHandPresent => pipeline != null && pipeline.IsHandPresent().Present;

        /// <summary>Seconds since the capture window opened (last BeginCapture or recognition), or -1.</summary>
        public float CaptureSeconds => lastClearTime < 0f ? -1f : Time.realtimeSinceStartup - lastClearTime;

        /// <summary>How long the last Recognize() call blocked the main thread.</summary>
        public float LastRecognitionMs { get; private set; }

        // ---- Public API ----

        /// <summary>Limit recognition to these signs (the player's equipped signs).</summary>
        public void SetVocabulary(string[] newSigns)
        {
            signs = newSigns;
            UpdateVocab();
        }

        /// <summary>Open a fresh capture window: drop whatever the camera saw before now.</summary>
        public void BeginCapture()
        {
            if (pipeline == null) return;
            pipeline.ClearBuffer(new DynamicSLREmpty());
            lastClearTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Classify the current capture window. BLOCKS the main thread while it runs
        /// (see LastRecognitionMs). The buffer is cleared afterwards, so the next window
        /// starts clean. Returns AslResult.None if the pipeline isn't ready or nothing matched.
        /// </summary>
        public AslResult Recognize()
        {
            if (pipeline == null)
            {
                Debug.LogWarning("[ASL] Recognize ignored: the pipeline isn't ready yet.");
                return AslResult.None;
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            DynamicSLRRecognitionResponse response = pipeline.TriggerRecognition(new DynamicSLREmpty());

            try
            {
                if (response == null || !response.Success)
                {
                    Debug.LogError($"[ASL] Recognition failed: {(response?.ErrorMessage ?? "null response")}");
                    return AslResult.None;
                }
                if (response.Classifications.Count == 0) return AslResult.None;

                var ranked = response.Classifications.OrderByDescending(c => c.Score).ToList();
                float margin = ranked.Count > 1 ? ranked[0].Score - ranked[1].Score : 0f;
                return new AslResult(ranked[0].Label, ranked[0].Score, margin);
            }
            finally
            {
                // Always clear, even on the early returns, so a failed or empty recognition
                // never leaves the previous clip's frames in the buffer.
                pipeline.ClearBuffer(new DynamicSLREmpty());
                lastClearTime = Time.realtimeSinceStartup;
                timer.Stop();
                LastRecognitionMs = (float)timer.Elapsed.TotalMilliseconds;
            }
        }

        // ---- Setup and lifecycle (adapted from the toolkit sample) ----

        private void Start()
        {
            if (previewImage == null)
            {
                Debug.LogError("[ASL] Please assign a RawImage to display the camera preview.");
                return;
            }

            // Wait for GPU initialization before starting.
            NativePipelineHooks.OnGpuInitialized += SetupPipeline;
        }

        private void OnEnable()
        {
            // Null until the GPU callback has run; SetupPipeline handles the first time.
            if (pipeline == null) return;
            AttachPreview();
            ResumeRecording();
            UpdateVocab();
        }

        private void OnDisable() => SuspendRecording("component disabled");

        private void OnApplicationPause(bool paused)
        {
            // Don't hold the camera while the app is backgrounded.
            if (paused) SuspendRecording("app paused");
            else { AttachPreview(); ResumeRecording(); }
        }

        private void OnDestroy()
        {
            // Deliberately does NOT close the pipeline or release the texture. OnDestroy
            // runs on every scene change, and closing here left the preview black on every
            // camera scene after the first. Real teardown happens at app quit.
            NativePipelineHooks.OnGpuInitialized -= SetupPipeline;
        }

        private void SetupPipeline()
        {
            try
            {
                if (pipeline != null)
                {
                    // Already open from an earlier scene: reattach and resume, never reopen.
                    AttachPreview();
                    ResumeRecording();
                    UpdateVocab();
                    return;
                }

                HookQuit();

                // DontUnloadUnusedAsset because only a static field references this texture,
                // and a scene transition would otherwise be free to unload it out from under
                // the bound native pointer.
                dynamicSlrTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.BGRA32);
                dynamicSlrTexture.enableRandomWrite = true;
                dynamicSlrTexture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                dynamicSlrTexture.Create();
                AttachPreview();

                pipeline = new DynamicSLRPipelineService();

                // Open explicitly BEFORE binding the texture: this is the only place the
                // options reach the graph builder. Naming the gallery is load-bearing,
                // because the other recognition fields are proto defaults that serialise to
                // zero bytes and would silently build the legacy recogniser instead.
                var baseOptions = new CoreTk.Pipeline.PipelineBaseOptions
                {
                    EnableHdf5 = false,
                    EnableOverlay = enableOverlay,
                    Overlay = new CoreTk.Pipeline.OverlayOptions { DrawPose = drawPose },
                    SignRecognition = new CoreTk.Pipeline.SignRecognitionOptions
                    {
                        Modality = CoreTk.Pipeline.SignRecognitionOptions.Types.Modality.Isolated,
                        Language = CoreTk.Pipeline.SignRecognitionOptions.Types.Language.Asl,
                        Gallery = gallery,
                        TopK = topK,
                    },
                };
                var openResp = pipeline.OpenPipeline(new DynamicSLROpenRequest { Options = baseOptions });
                if (openResp != null && !openResp.Success)
                {
                    Debug.LogError("[ASL] OpenPipeline failed: " + openResp.ErrorMessage);
                    pipeline = null;
                    return;
                }

                IntPtr nativePtr = dynamicSlrTexture.GetNativeTexturePtr();
                pipeline.AddDynamicSLR(new AddDynamicSLRRequest
                {
                    TargetGraph = "GTk/Pipelines/DynamicSLR",
                    TexturePtr = (ulong)nativePtr.ToInt64(),
                    ViewPtr = 0,
                    // A GL texture name carries no dimensions, so without these the native
                    // viewport falls back to the camera's size and crops (Android/GL only).
                    TextureWidth = (uint)dynamicSlrTexture.width,
                    TextureHeight = (uint)dynamicSlrTexture.height,
                });

                ResumeRecording();
                UpdateVocab();
                Debug.Log("[ASL] Pipeline started.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ASL] Failed to start pipeline: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>Point this scene's RawImage at the shared texture. Runs on every
        /// activation, because the component can be re-enabled or replaced by a scene reload.</summary>
        private void AttachPreview()
        {
            if (previewImage == null || dynamicSlrTexture == null) return;
            previewImage.texture = dynamicSlrTexture;

            var uv = SLRTk.SlrtkPreviewOrientation.UvRect(forceFlipPreview);

            // Mirror like a bathroom mirror, not a photo: copying a sign is disorienting if
            // raising a hand moves the opposite side of the screen. Display only.
            if (mirrorPreview) uv = new Rect(uv.x + uv.width, uv.y, -uv.width, uv.height);

            // No aspect crop on purpose: a crop computed from rectTransform.rect doesn't
            // match the panel's real size during gameplay and stretches a narrow sliver.
            previewImage.uvRect = uv;
        }

        private void ResumeRecording()
        {
            if (pipeline == null || recording) return;
            // Empty string, not null: the field is null-checked and null would throw.
            pipeline.StartRecording(new DynamicSLRStartRequest { OutputHdf5File = "" });
            // Clear so frames from before a pause can't bleed into the next recognition.
            pipeline.ClearBuffer(new DynamicSLREmpty());
            lastClearTime = Time.realtimeSinceStartup;
            recording = true;
        }

        private void SuspendRecording(string why)
        {
            if (pipeline == null || !recording) return;
            pipeline.StopRecording(new DynamicSLREmpty());
            pipeline.ClearBuffer(new DynamicSLREmpty());
            recording = false;
        }

        private void UpdateVocab()
        {
            if (pipeline == null || signs == null) return;
            var request = new DynamicSLRSetVocabRequest();
            foreach (string sign in signs) request.Vocab.Add(sign);
            pipeline.SetVocabulary(request);
        }

        private static void HookQuit()
        {
            if (quitHooked) return;
            Application.quitting += CloseForGood;
            quitHooked = true;
        }

        /// <summary>Full teardown, once, when the app itself is going away.</summary>
        private static void CloseForGood()
        {
            if (pipeline != null && recording)
            {
                pipeline.StopRecording(new DynamicSLREmpty());
                recording = false;
            }
            if (pipeline != null)
            {
                pipeline.Dispose();
                pipeline = null;
            }
            if (dynamicSlrTexture != null)
            {
                dynamicSlrTexture.Release();
                dynamicSlrTexture = null;
            }
        }
    }
}
