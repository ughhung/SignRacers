using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SignRacers.Signs
{
    /// <summary>
    /// Test rig for the recognizer, with no game involved. Works with touch buttons on a
    /// phone, or with the keyboard on a Mac (B, R, then 1-4).
    ///   BEGIN       - open a fresh capture window (then do the sign)
    ///   RECOGNIZE   - classify what was captured
    /// Then TAG the attempt, saying what you actually did:
    ///   Right       - signed a word from the list, and got that word
    ///   Missed      - signed a word from the list, but got a different word
    ///   Other sign  - signed a real sign that is NOT in the list
    ///   No sign     - not signing at all
    /// The screen shows min / avg / max score and margin for each tag.
    /// </summary>
    public class AslTestHarness : MonoBehaviour
    {
        private enum Tag { Untagged, Right, Missed, OtherSign, NoSign }
        private enum HarnessKey { Begin, Recognize, Tag1, Tag2, Tag3, Tag4 }

        private struct Attempt
        {
            public string Label;
            public float Score;
            public float Margin;
            public Tag Tag;
        }

        [SerializeField] private AslRecognizer recognizer;
        [SerializeField] private string[] vocabulary = { "yellow", "duck", "clown" };

        private readonly List<Attempt> attempts = new List<Attempt>();
        private string lastReport = "Tap BEGIN, do a sign for about 2 seconds, then tap RECOGNIZE.";

        private void Awake()
        {
            if (recognizer == null) recognizer = GetComponent<AslRecognizer>();
        }

        private void Start() => recognizer.SetVocabulary(vocabulary);

        private void Update()
        {
            if (Pressed(HarnessKey.Begin)) BeginNow();
            if (Pressed(HarnessKey.Recognize)) RecognizeNow();
            if (Pressed(HarnessKey.Tag1)) TagLast(Tag.Right);
            if (Pressed(HarnessKey.Tag2)) TagLast(Tag.Missed);
            if (Pressed(HarnessKey.Tag3)) TagLast(Tag.OtherSign);
            if (Pressed(HarnessKey.Tag4)) TagLast(Tag.NoSign);
        }

        private void BeginNow()
        {
            recognizer.BeginCapture();
            lastReport = "Capturing... do the sign, then tap RECOGNIZE.";
        }

        private void RecognizeNow()
        {
            float window = recognizer.CaptureSeconds;
            lastReport = "Recognizing...";
            // The toolkit answers a few frames later.
            recognizer.Recognize(r =>
            {
                attempts.Add(new Attempt { Label = r.Label, Score = r.Score, Margin = r.Margin, Tag = Tag.Untagged });
                lastReport = r.HasLabel
                    ? $"#{attempts.Count}: {r.Label}  |  score {r.Score:F3}  |  window {window:F1}s  |  answered in {recognizer.LastRecognitionMs:F0} ms\nNow tag it below."
                    : $"#{attempts.Count}: no result  |  window {window:F1}s  |  answered in {recognizer.LastRecognitionMs:F0} ms\nNow tag it below.";
            });
        }

        private void TagLast(Tag tag)
        {
            if (attempts.Count == 0) return;
            var last = attempts[attempts.Count - 1];
            last.Tag = tag;
            attempts[attempts.Count - 1] = last;

            // One clean line per attempt, easy to read out of the Console or Xcode log.
            Debug.Log($"[ASL-DATA] #{attempts.Count} tag={tag} label={last.Label} score={last.Score:F3} margin={last.Margin:F3}");
        }

        private string Stats(Tag tag, string name)
        {
            var list = attempts.Where(a => a.Tag == tag).ToList();
            if (list.Count == 0) return $"{name}: -";
            return $"{name} (n={list.Count})  score {list.Min(a => a.Score):F2} / {list.Average(a => a.Score):F2} / {list.Max(a => a.Score):F2}" +
                   $"  margin {list.Min(a => a.Margin):F2} / {list.Average(a => a.Margin):F2} / {list.Max(a => a.Margin):F2}";
        }

        private void OnGUI()
        {
            float w = Screen.width, h = Screen.height;
            float margin = Mathf.Min(w, h) * 0.03f;
            int font = Mathf.RoundToInt(Mathf.Min(w, h) * 0.035f);

            var label = new GUIStyle(GUI.skin.label) { fontSize = font, wordWrap = true };
            label.normal.textColor = Color.white;
            var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(font * 1.15f), fontStyle = FontStyle.Bold };

            string status = recognizer.IsReady ? $"hand in frame: {recognizer.IsHandPresent}" : "starting camera...";
            string text = status + "\n" + lastReport + "\n\n" +
                          "min / avg / max\n" +
                          Stats(Tag.Right, "Right") + "\n" +
                          Stats(Tag.Missed, "Missed") + "\n" +
                          Stats(Tag.OtherSign, "Other sign") + "\n" +
                          Stats(Tag.NoSign, "No sign");

            // Dark panel behind the text so it stays readable over the camera preview.
            var textRect = new Rect(margin, margin, w - margin * 2, h * 0.5f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(textRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(textRect, text, label);

            // Touch buttons along the bottom: two rows.
            float bh = h * 0.08f;
            float rowGap = margin * 0.5f;
            float row2Y = h - bh - margin;
            float row1Y = row2Y - bh - rowGap;
            float halfW = (w - margin * 3f) / 2f;

            if (GUI.Button(new Rect(margin, row1Y, halfW, bh), "BEGIN", button)) BeginNow();
            if (GUI.Button(new Rect(margin * 2f + halfW, row1Y, halfW, bh), "RECOGNIZE", button)) RecognizeNow();

            float quarterW = (w - margin * 2f - rowGap * 3f) / 4f;
            string[] names = { "Right", "Missed", "Other", "No sign" };
            Tag[] tags = { Tag.Right, Tag.Missed, Tag.OtherSign, Tag.NoSign };
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(margin + (quarterW + rowGap) * i, row2Y, quarterW, bh);
                if (GUI.Button(r, names[i], button)) TagLast(tags[i]);
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static bool Pressed(HarnessKey key)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case HarnessKey.Begin: return kb.bKey.wasPressedThisFrame;
                case HarnessKey.Recognize: return kb.rKey.wasPressedThisFrame;
                case HarnessKey.Tag1: return kb.digit1Key.wasPressedThisFrame;
                case HarnessKey.Tag2: return kb.digit2Key.wasPressedThisFrame;
                case HarnessKey.Tag3: return kb.digit3Key.wasPressedThisFrame;
                case HarnessKey.Tag4: return kb.digit4Key.wasPressedThisFrame;
                default: return false;
            }
        }
#else
        private static bool Pressed(HarnessKey key)
        {
            switch (key)
            {
                case HarnessKey.Begin: return Input.GetKeyDown(KeyCode.B);
                case HarnessKey.Recognize: return Input.GetKeyDown(KeyCode.R);
                case HarnessKey.Tag1: return Input.GetKeyDown(KeyCode.Alpha1);
                case HarnessKey.Tag2: return Input.GetKeyDown(KeyCode.Alpha2);
                case HarnessKey.Tag3: return Input.GetKeyDown(KeyCode.Alpha3);
                case HarnessKey.Tag4: return Input.GetKeyDown(KeyCode.Alpha4);
                default: return false;
            }
        }
#endif
    }
}
