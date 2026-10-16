using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SignRacers.Signs
{
    /// <summary>
    /// Throwaway test rig for the recognizer, with no game involved.
    ///   B = begin a capture window (then do the sign)
    ///   R = recognize what was captured
    /// Prints the label, score, margin, capture length, and how long recognition blocked,
    /// so we can calibrate score thresholds and check that blocking is acceptable.
    /// A live line shows whether the toolkit currently sees a hand.
    /// </summary>
    public class AslTestHarness : MonoBehaviour
    {
        [SerializeField] private AslRecognizer recognizer;
        [SerializeField] private string[] vocabulary = { "yellow", "red", "owl", "dad", "elephant" };

        private string lastReport = "Press B, do a sign for about 2 seconds, then press R.";

        private void Awake()
        {
            if (recognizer == null) recognizer = GetComponent<AslRecognizer>();
        }

        private void Start() => recognizer.SetVocabulary(vocabulary);

        private void Update()
        {
            if (KeyPressed(true))
            {
                recognizer.BeginCapture();
                lastReport = "Capturing... do the sign, then press R.";
            }

            if (KeyPressed(false))
            {
                float window = recognizer.CaptureSeconds;
                AslResult r = recognizer.Recognize();

                lastReport = r.HasLabel
                    ? $"{r.Label}  |  score {r.Score:F3}  |  margin {r.Margin:F3}  |  window {window:F1}s  |  blocked {recognizer.LastRecognitionMs:F0} ms"
                    : $"No result  |  window {window:F1}s  |  blocked {recognizer.LastRecognitionMs:F0} ms";
                Debug.Log($"[ASL] {lastReport}");
            }
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.03f) };
            string status = recognizer.IsReady ? $"hand in frame: {recognizer.IsHandPresent}" : "starting camera...";
            GUI.Label(new Rect(20, 20, Screen.width - 40, Screen.height * 0.06f), status, style);
            GUI.Label(new Rect(20, 20 + Screen.height * 0.06f, Screen.width - 40, Screen.height * 0.1f), lastReport, style);
        }

#if ENABLE_INPUT_SYSTEM
        private static bool KeyPressed(bool begin) => Keyboard.current != null &&
            (begin ? Keyboard.current.bKey : Keyboard.current.rKey).wasPressedThisFrame;
#else
        private static bool KeyPressed(bool begin) => Input.GetKeyDown(begin ? KeyCode.B : KeyCode.R);
#endif
    }
}
