using SignRacers.Kart;
using SignRacers.Signs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.UI
{
    /// <summary>
    /// The signing screen on the Race HUD prefab. When you tap a power-up the top-left box grows
    /// and crossfades from the map into your camera, and the word you must sign appears in the
    /// middle, grayed out, with a timer bar under it. A correct sign fills the word with color;
    /// a wrong one (or a timeout) shakes it red. The box then shrinks back to the map.
    /// It only reads state from AslPowerupSignSource; it never decides anything.
    /// </summary>
    public class SignViewHud : MonoBehaviour
    {
        private const float ResultHoldSeconds = 1.1f;   // how long the result stays up before going back to the map
        private const float FillSeconds = 0.35f;        // correct: word fills with color
        private const float ShakeSeconds = 0.5f;        // wrong: word shakes
        private const float FlashSeconds = 0.5f;        // colored flash over the camera

        private static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color CorrectColor = new Color(0.35f, 0.95f, 0.45f);
        private static readonly Color WrongColor = new Color(1f, 0.33f, 0.28f);

        [Header("Camera box (top left)")]
        [SerializeField] private RectTransform box;
        [SerializeField] private CanvasGroup mapContent;
        [SerializeField] private CanvasGroup cameraArea;
        [SerializeField] private Image cameraFlash;
        [SerializeField] private Vector2 cameraBoxSize = new Vector2(520f, 600f);
        [SerializeField] private float transitionSeconds = 0.25f;

        [Header("Word and timer (center)")]
        [SerializeField] private CanvasGroup signPanel;
        [SerializeField] private RectTransform wordRoot;
        [SerializeField] private TMP_Text ghostText;
        [SerializeField] private TMP_Text fillText;
        [SerializeField] private RectMask2D fillMask;
        [SerializeField] private RectTransform timerFill;
        [SerializeField] private Image timerFillImage;
        [SerializeField] private GameObject hint;

        private KartPathFollower kart;
        private AslPowerupSignSource signs;
        private Vector2 mapSize;
        private float cameraT;
        private string shownWord = "";
        private float lastTimer;

        private void Awake()
        {
            if (box != null) mapSize = box.sizeDelta;
            if (cameraArea != null) cameraArea.blocksRaycasts = false;
        }

        private void Update()
        {
            FindLocalKart();
            if (signs == null || box == null) return;

            bool signing = signs.IsSigning;
            float since = Time.time - signs.LastResultTime;
            bool showingResult = !signing && signs.LastResult != PromptResult.None && since < ResultHoldSeconds;
            bool cameraMode = signing || showingResult;

            if (signing)
            {
                shownWord = string.IsNullOrEmpty(signs.Word) ? "" : signs.Word.ToUpperInvariant();
                lastTimer = signs.TimeLeft01;
            }

            UpdateBox(cameraMode, showingResult, since);
            UpdateWord(cameraMode, signing, showingResult, since);
        }

        // ---------- the top-left box: map <-> camera ----------

        private void UpdateBox(bool cameraMode, bool showingResult, float since)
        {
            cameraT = Mathf.MoveTowards(cameraT, cameraMode ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.05f, transitionSeconds));
            float e = Mathf.SmoothStep(0f, 1f, cameraT);

            box.sizeDelta = Vector2.Lerp(mapSize, cameraBoxSize, e);
            if (mapContent != null) mapContent.alpha = 1f - Mathf.Clamp01(e * 2f);      // the map fades out first...
            if (cameraArea != null) cameraArea.alpha = Mathf.Clamp01(e * 2f - 1f);      // ...then the camera fades in

            if (cameraFlash != null)
            {
                Color c = signs.LastResult == PromptResult.Correct ? CorrectColor : WrongColor;
                c.a = showingResult ? 0.55f * (1f - Mathf.Clamp01(since / FlashSeconds)) : 0f;
                cameraFlash.color = c;
            }
        }

        // ---------- the word, timer and result effects ----------

        private void UpdateWord(bool cameraMode, bool signing, bool showingResult, float since)
        {
            if (signPanel != null)
                signPanel.alpha = Mathf.MoveTowards(signPanel.alpha, cameraMode ? 1f : 0f, Time.unscaledDeltaTime * 6f);

            if (ghostText == null || fillText == null) return;

            if (ghostText.text != shownWord)
            {
                // Longer words get a smaller font so they stay inside the panel.
                float fontSize = Mathf.Min(190f, 880f / Mathf.Max(1f, shownWord.Length * 0.62f));
                ghostText.fontSize = fillText.fontSize = fontSize;
                ghostText.text = fillText.text = shownWord;
            }

            float fill = 0f;
            Color ghost = GhostColor;
            float shakeX = 0f;
            float scale = 1f;
            bool correct = signs.LastResult == PromptResult.Correct;

            if (showingResult)
            {
                if (correct)
                {
                    float f = Mathf.Clamp01(since / FillSeconds);
                    fill = f;
                    scale = 1f + 0.12f * Mathf.Sin(f * Mathf.PI); // a little pop as it fills
                }
                else
                {
                    ghost = Color.Lerp(GhostColor, WrongColor, Mathf.Clamp01(since / 0.1f));
                    shakeX = Mathf.Sin(since * 70f) * 18f * Mathf.Clamp01(1f - since / ShakeSeconds);
                }
            }

            ghostText.color = ghost;
            fillText.color = CorrectColor;
            if (wordRoot != null)
            {
                wordRoot.anchoredPosition = new Vector2(shakeX, wordRoot.anchoredPosition.y);
                wordRoot.localScale = new Vector3(scale, scale, 1f);
            }

            // Color the word in from left to right by clipping the colored copy from the right.
            if (fillMask != null)
            {
                float rectWidth = fillMask.rectTransform.rect.width;
                float wordWidth = fillText.preferredWidth;
                float left = (rectWidth - wordWidth) * 0.5f;
                float visibleRight = left + wordWidth * fill;
                fillMask.padding = new Vector4(0f, 0f, Mathf.Max(0f, rectWidth - visibleRight), 0f);
            }

            // Timer bar: full at the start, empty at the end. After a wrong sign it sits at empty.
            float t = signing ? signs.TimeLeft01 : (showingResult && correct ? lastTimer : 0f);
            if (timerFill != null) timerFill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
            if (timerFillImage != null) timerFillImage.color = Color.Lerp(WrongColor, CorrectColor, t);
            if (hint != null) hint.SetActive(signing);
        }

        private void FindLocalKart()
        {
            if (kart != null) return; // Unity's null check also covers a destroyed kart
            foreach (var k in KartPathFollower.All)
            {
                if (k.IsBot || !k.HasStateAuthority) continue;
                kart = k;
                signs = k.GetComponent<AslPowerupSignSource>();
                return;
            }
            signs = null;
        }
    }
}
