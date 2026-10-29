using UnityEngine;

namespace SignRacers.Track
{
    /// <summary>
    /// Put one of these on an empty object called "TrackInfo" in every track scene.
    /// It holds the settings that belong to that track, so the Game scene does not need to know them.
    /// </summary>
    public class TrackInfo : MonoBehaviour
    {
        /// <summary>The TrackInfo of the track scene that is loaded right now (null if none).</summary>
        public static TrackInfo Current { get; private set; }

        [SerializeField] private string displayName = "Hand";
        [Tooltip("Laps for this track. 0 = use the Race Manager's own value.")]
        [SerializeField, Min(0)] private int laps = 0;

        [Tooltip("Optional. Sky material for this track. Leave empty to use the sky set in the Game scene's Lighting window.")]
        [SerializeField] private Material skybox;

        [Header("Minimap")]
        [Tooltip("Turn the minimap this many quarter turns clockwise so the track reads nicely.")]
        [SerializeField, Range(0, 3)] private int minimapQuarterTurns = 0;
        [SerializeField] private bool minimapFlipHorizontal = false;

        public string DisplayName => displayName;
        public int Laps => laps;
        public int MinimapQuarterTurns => minimapQuarterTurns;
        public bool MinimapFlipHorizontal => minimapFlipHorizontal;

        private void Awake()
        {
            Current = this;
            if (skybox != null)
            {
                RenderSettings.skybox = skybox;
                DynamicGI.UpdateEnvironment();
            }
        }
        private void OnDestroy() { if (Current == this) Current = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }
}
