using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SignRacers.UI
{
    /// <summary>
    /// One practice card: the word, one shared media area (idle hint / reference video / live camera),
    /// a status line, and LEARN + REVIEW buttons. It only displays; VotingView decides what happens.
    /// </summary>
    public class PracticeCard : MonoBehaviour
    {
        private static readonly Color FrameIdle = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color FrameGood = new Color(0.475f, 0.773f, 0.412f, 1f)  /* #79C569 */;
        private static readonly Color FrameBad = new Color(0.835f, 0.400f, 0.561f, 1f)  /* #D5668F */;

        [SerializeField] private TMP_Text wordText;
        [SerializeField] private RectTransform mediaSlot;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private RawImage cameraImage;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text statusText;
        [FormerlySerializedAs("watchButton")]
        [SerializeField] private Button learnButton;
        [FormerlySerializedAs("testButton")]
        [SerializeField] private Button reviewButton;

        private Color baseStatusColor = Color.white;
        private bool statusCached;

        private void ResetStatusColor()
        {
            if (statusText == null) return;
            if (!statusCached) { baseStatusColor = statusText.color; statusCached = true; }
            statusText.color = baseStatusColor;
        }

        private Color baseWordColor = Color.white;
        private bool wordCached;

        private void ResetWord()
        {
            if (wordText == null) return;
            if (!wordCached) { baseWordColor = wordText.color; wordCached = true; }
            wordText.color = baseWordColor;
        }

        private Sprite baseFrameSprite;
        private Color baseFrameColor;
        private bool frameCached;

        private void CacheFrame()
        {
            if (frameCached || frame == null) return;
            baseFrameSprite = frame.sprite; baseFrameColor = frame.color; frameCached = true;
        }

        /// <summary>Back to the plain frame (the art you assigned in the prefab).</summary>
        private void ResetFrame()
        {
            if (frame == null) return;
            CacheFrame();
            frame.sprite = baseFrameSprite;
            frame.color = baseFrameColor;
        }

        public RectTransform MediaSlot => mediaSlot;
        public Button LearnButton => learnButton;
        public Button ReviewButton => reviewButton;

        public void SetWord(string display)
        {
            if (wordText != null && wordText.text != display) wordText.text = display;
        }

        public void SetStatus(string text)
        {
            if (statusText != null && statusText.text != text) statusText.text = text;
        }

        public void SetStatusColor(Color color)
        {
            if (statusText != null) statusText.color = color;
        }

        public void SetButtons(bool interactable)
        {
            if (learnButton != null) learnButton.interactable = interactable;
            if (reviewButton != null) reviewButton.interactable = interactable;
        }

        /// <summary>Nothing playing: show the hint, hide the camera, plain frame.</summary>
        public void ShowIdle()
        {
            if (hintText != null) hintText.gameObject.SetActive(true);
            if (cameraImage != null) cameraImage.gameObject.SetActive(false);
            ResetFrame();
            ResetWord();
            SetStatus("");
            ResetStatusColor();
            SetButtons(true);
        }

        /// <summary>The reference video is playing in the media slot (VotingView moves it there).</summary>
        public void ShowVideoMode()
        {
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (cameraImage != null) cameraImage.gameObject.SetActive(false);
            ResetFrame();
            ResetWord();
            SetStatus("");
            ResetStatusColor();
        }

        private int fitW = -1, fitH = -1;

        /// <summary>
        /// Draw the camera at its own shape, centred in the media box (bars at the sides for a portrait camera),
        /// so nothing is squashed or cut off: you see everything the recognizer sees. Cheap: does nothing unless the size changed.
        /// </summary>
        public void FitCamera(Texture texture)
        {
            if (cameraImage == null || mediaSlot == null || texture == null || texture.width < 32 || texture.height < 32) return;
            if (texture.width == fitW && texture.height == fitH) return;
            fitW = texture.width; fitH = texture.height;

            float aspect = (float)texture.width / texture.height;
            Vector2 box = mediaSlot.rect.size;
            float width = box.x, height = box.x / aspect;
            if (height > box.y) { height = box.y; width = box.y * aspect; }

            var rect = cameraImage.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Show the live camera in the media slot.</summary>
        public void ShowCamera(Texture texture, Rect uv)
        {
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (cameraImage == null) return;
            cameraImage.gameObject.SetActive(true);
            cameraImage.texture = texture;
            cameraImage.uvRect = uv;
            fitW = -1; // re-fit for this texture
            FitCamera(texture);
            ResetFrame();
            ResetWord();
            ResetStatusColor();
        }

        /// <summary>Green or red frame and message after a review.</summary>
        public void ShowResult(bool correct, string message)
        {
            if (frame != null)
            {
                CacheFrame();
                var skin = UiSkin.Resolve();
                var art = correct ? skin.frameGood : skin.frameBad;
                if (art != null) { frame.sprite = art; frame.color = Color.white; } // finished green / red frame art
                else frame.color = correct ? FrameGood : FrameBad;                  // otherwise tint the plain frame
            }
            SetStatus(message);
            // Green / red for the word and message, from the skin (dark enough to read on the background).
            var verdictSkin = UiSkin.Resolve();
            Color verdict = correct ? verdictSkin.correctTextColor : verdictSkin.wrongTextColor;
            if (wordText != null)
            {
                if (!wordCached) { baseWordColor = wordText.color; wordCached = true; }
                wordText.color = verdict;
            }
            if (statusText != null) { if (!statusCached) { baseStatusColor = statusText.color; statusCached = true; } statusText.color = verdict; }
        }
    }
}
