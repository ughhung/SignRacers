using UnityEngine;

namespace SignRacers.UI
{
    /// <summary>
    /// One place for the game's UI colours. Create the asset with the menu
    /// "Sign Racers / Create UI Theme" (it is saved as Assets/Resources/UiTheme.asset), then change the colours
    /// here and every screen that uses the theme updates. Screens that find no asset use the defaults below,
    /// so the game always works.
    /// </summary>
    [CreateAssetMenu(fileName = "UiTheme", menuName = "Sign Racers/UI Theme")]
    public class UiTheme : ScriptableObject
    {
        [Header("Signing screen (the word you sign)")]
        [Tooltip("The word fills with this when the sign is correct. Also the camera flash.")]
        public Color correct = new Color(0.35f, 0.95f, 0.45f);
        [Tooltip("Wrong sign or timeout: the word and the camera flash.")]
        public Color wrong = new Color(1f, 0.33f, 0.28f);
        [Tooltip("The faded word before it is signed. Keep some transparency (Alpha) so it looks faded.")]
        public Color ghost = new Color(1f, 1f, 1f, 0.35f);

        [Header("Signing timer bar")]
        [Tooltip("The timer bar's colour. It stays this one colour (no blending) while it drains.")]
        public Color timerColor = new Color(0.35f, 0.95f, 0.45f);

        [Header("Race position (top right)")]
        [Tooltip("First place shows a top-to-bottom gradient between these two colours.")]
        public Color firstPlaceTop = new Color(1.00f, 0.95f, 0.45f);
        public Color firstPlaceBottom = new Color(1.00f, 0.65f, 0.10f);
        [Tooltip("2nd, 3rd and 4th place all use this colour.")]
        public Color otherPlaces = Color.white;

        [Header("Race buttons")]
        public Color boostButton = new Color(1.00f, 0.60f, 0.15f);
        public Color itemButton = new Color(0.25f, 0.55f, 0.95f);
        public Color emptyButton = new Color(0.35f, 0.38f, 0.45f);
        [Tooltip("The button colour while its effect is active (the CHECK state).")]
        public Color activeButton = new Color(1.00f, 0.90f, 0.25f);

        private static UiTheme fallback;

        /// <summary>
        /// Returns the theme a screen should use: the one assigned on it, else Resources/UiTheme,
        /// else a built-in default set (so nothing breaks if the asset hasn't been created).
        /// </summary>
        public static UiTheme Resolve(UiTheme assigned)
        {
            if (assigned != null) return assigned;
            if (fallback == null)
            {
                fallback = Resources.Load<UiTheme>("UiTheme");
                if (fallback == null) fallback = CreateInstance<UiTheme>();
            }
            return fallback;
        }
    }
}
