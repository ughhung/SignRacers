using System;
using SignRacers.UI;
using UnityEngine;

namespace SignRacers.Core
{
    /// <summary>
    /// Debug meter (top right, under the lap bar). Every half second it shows:
    ///   fps    - average frames per second
    ///   worst  - the slowest single frame in that window, in milliseconds
    ///   GC     - how many times Unity cleaned up memory in that window
    /// Low fps with a small "worst" means the game is simply too heavy. Fine fps but a big
    /// "worst" next to GC counts means stutters from memory cleanup. Remove it when done.
    /// </summary>
    public class PerfMeter : MonoBehaviour
    {
        [SerializeField] private float sampleSeconds = 0.5f;

        private float elapsed;
        private int frames;
        private float worst;
        private int lastGcCount;
        private string text = "measuring...";

        private GUIStyle style;
        private Vector2Int builtFor;

        private void Awake() => lastGcCount = GC.CollectionCount(0);

        private void Update()
        {
            float frameTime = Time.unscaledDeltaTime;
            elapsed += frameTime;
            frames++;
            if (frameTime > worst) worst = frameTime;

            if (elapsed < sampleSeconds) return;

            int gcCount = GC.CollectionCount(0);
            text = $"{frames / elapsed:F0} fps   worst {worst * 1000f:F0} ms   GC {gcCount - lastGcCount}";
            lastGcCount = gcCount;
            elapsed = 0f;
            frames = 0;
            worst = 0f;
        }

        private void OnGUI()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            float u = HudLayout.Unit;
            if (style == null || builtFor != size)
            {
                builtFor = size;
                style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = Mathf.RoundToInt(0.03f * u),
                    fontStyle = FontStyle.Bold,
                };
            }

            Rect s = HudLayout.Safe;
            GUI.Label(new Rect(s.x, s.y + 0.11f * u, s.width - 0.03f * u, 0.05f * u), text, style);
        }
    }
}
