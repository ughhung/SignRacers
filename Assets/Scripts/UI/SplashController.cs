using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignRacers.UI
{
    /// <summary>
    /// The splash screen before the main menu. The background scrolls sideways forever; at the start it is fast and it eases down to a
    /// steady cruise while the logo zooms in from the right and settles in the middle (a little overshoot, like a car braking).
    /// A "TAP TO START" prompt then pulses; tapping anywhere fades out and loads the main menu.
    /// Build the scene with Sign Racers > Build Splash Screen.
    /// </summary>
    public class SplashController : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] private RawImage road;
        [Tooltip("How wide one copy of the background picture is, in canvas pixels. 0 = one copy fills the screen width.")]
        [SerializeField] private float roadTileWidth = 0f;
        [SerializeField] private RectTransform logo;
        [SerializeField] private CanvasGroup prompt;
        [SerializeField] private Button tapCatcher;
        [SerializeField] private CanvasGroup fade;
        [Header("Credits (third-party attribution)")]
        [SerializeField] private Button creditsButton;
        [Tooltip("The web address the Credits link opens (for example your Google Drive link).")]
        [SerializeField] private string creditsUrl = "";
        [SerializeField] private string nextScene = "MainMenu";

        [Header("Road")]
        [Tooltip("Tick: the road slides to the right, as if the car drives to the left. Untick for the other way.")]
        [SerializeField] private bool roadMovesRight = true;
        [SerializeField] private float startRoadSpeed = 3200f;
        [SerializeField] private float cruiseRoadSpeed = 420f;
        [SerializeField] private float slowSeconds = 1.6f;

        [Header("Logo")]
        [SerializeField] private float startDelay = 0.25f;
        [SerializeField] private float logoSeconds = 1.0f;
        [Tooltip("How far past the middle the logo swings before it settles (0 = none).")]
        [SerializeField] private float overshoot = 1.2f;
        [SerializeField] private float bobHeight = 8f;

        private float elapsed;   // seconds of animation played so far (counted frame by frame, so a slow first frame cannot skip it)
        private float roadOffset;
        private bool leaving;

        private void OnEnable()
        {
            elapsed = 0f;
            roadOffset = 0f;
            leaving = false;
            if (fade != null) { fade.alpha = 0f; fade.blocksRaycasts = false; }
            if (prompt != null) prompt.alpha = 0f;
            if (tapCatcher != null) tapCatcher.onClick.AddListener(Continue);
            if (creditsButton != null) creditsButton.onClick.AddListener(OpenCredits);
            PlaceLogo(0f);
        }

        private void OnDisable()
        {
            if (tapCatcher != null) tapCatcher.onClick.RemoveListener(Continue);
            if (creditsButton != null) creditsButton.onClick.RemoveListener(OpenCredits);
        }

        public void OpenCredits()
        {
            if (string.IsNullOrWhiteSpace(creditsUrl))
            {
                Debug.LogWarning("Splash: no Credits Url set. Click the SplashController and paste your link into Credits Url.");
                return;
            }
            Application.OpenURL(creditsUrl.Trim());
        }

        private void Update()
        {
            // The first frames in the Editor can be very slow (loading pictures and fonts). Counting at most 1/20 s per frame
            // means that never eats the animation: it always plays from the start.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            elapsed += dt;
            float t = elapsed;

            // Road: fast at first, easing down to a cruise.
            if (road != null)
            {
                float screenWidth = road.rectTransform.rect.width;
                float tileWidth = roadTileWidth > 1f ? roadTileWidth : Mathf.Max(1f, screenWidth);
                float speed = Mathf.Lerp(startRoadSpeed, cruiseRoadSpeed, EaseOutCubic(Mathf.Clamp01(t / slowSeconds)));
                roadOffset += (roadMovesRight ? -1f : 1f) * speed * dt / tileWidth;
                roadOffset -= Mathf.Floor(roadOffset); // keep it between 0 and 1
                road.uvRect = new Rect(roadOffset, 0f, screenWidth / tileWidth, 1f);
            }

            // Logo: zooms in from the right, then floats gently.
            float p = Mathf.Clamp01((t - startDelay) / Mathf.Max(0.05f, logoSeconds));
            PlaceLogo(p, t);

            // Prompt: fades in after the logo lands, then pulses.
            if (prompt != null)
            {
                float appear = Mathf.Clamp01((t - (startDelay + logoSeconds + 0.3f)) / 0.4f);
                float pulse = 0.65f + 0.35f * Mathf.Sin(t * 3f);
                prompt.alpha = appear * pulse;
            }
        }

        private void PlaceLogo(float p, float t = 0f)
        {
            if (logo == null) return;
            var parent = logo.parent as RectTransform;
            float travel = (parent != null ? parent.rect.width : 1080f) + logo.rect.width;
            float x = p < 1f ? travel * (1f - EaseOutBack(p, overshoot)) : 0f;
            float y = p >= 1f ? Mathf.Sin(t * 2f) * bobHeight : 0f;
            logo.anchoredPosition = new Vector2(x, y);
        }

        public void Continue()
        {
            if (leaving) return;
            leaving = true;
            StartCoroutine(FadeAndLoad());
        }

        private IEnumerator FadeAndLoad()
        {
            if (fade != null)
            {
                fade.blocksRaycasts = true;
                for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / 0.25f)
                {
                    fade.alpha = a;
                    yield return null;
                }
                fade.alpha = 1f;
            }
            SceneManager.LoadScene(nextScene);
        }

        private static float EaseOutCubic(float p) => 1f - Mathf.Pow(1f - p, 3f);

        private static float EaseOutBack(float p, float c1)
        {
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
        }
    }
}
