using TMPro;
using UnityEngine;

namespace SignRacers.UI
{
    /// <summary>
    /// The look of the menu screens (voting and practice cards): sprites AND colours in one asset.
    /// Create it with "Sign Racers / Create UI Skin" (saved as Assets/Resources/UiSkin.asset), drag sprites into the
    /// slots, then run "Sign Racers / Build Voting And Review Prefab" to apply them. Any sprite slot left empty
    /// keeps the built-in rounded shape, so you can fill the slots one at a time.
    /// (The race screen's colours stay in UiTheme.)
    /// </summary>
    [CreateAssetMenu(fileName = "UiSkin", menuName = "Sign Racers/UI Skin")]
    public class UiSkin : ScriptableObject
    {
        [Header("Font (optional)")]
        [Tooltip("Main (display) font: titles, the word on each card, buttons. Empty = the project's default font.")]
        public TMP_FontAsset font;
        [Tooltip("Optional second font for the small text: timers, hints, status lines, vote counts. Empty = use the main font.")]
        public TMP_FontAsset bodyFont;

        [Header("Sprites (all optional). Set 9-slice borders on the stretchy ones.")]
        [Tooltip("Full-screen backdrop. Stretched to fill the screen, no 9-slice needed.")]
        public Sprite background;
        [Tooltip("A word row on the voting screen. 9-sliced.")]
        public Sprite row;
        [Tooltip("A voting row you have picked (swaps in for Row). 9-sliced.")]
        public Sprite rowChosen;
        [Tooltip("The big panel behind each practice card. 9-sliced.")]
        public Sprite card;
        [Tooltip("The border around the camera/video box. 9-sliced. Use a WHITE or light grey sprite so the green/red flash can tint it.")]
        public Sprite frame;
        [Tooltip("Optional: the frame art shown after a CORRECT review. Leave empty to just tint the Frame sprite green.")]
        public Sprite frameGood;
        [Tooltip("Optional: the frame art shown after a WRONG review. Leave empty to just tint the Frame sprite pink/red.")]
        public Sprite frameBad;
        [Tooltip("Main buttons: DONE VOTING and REVIEW. 9-sliced.")]
        public Sprite buttonMain;
        [Tooltip("Quiet buttons: LEARN and the two arrows. 9-sliced.")]
        public Sprite buttonSecondary;
        [Tooltip("The READY button. 9-sliced.")]
        public Sprite buttonGo;
        [Tooltip("Icon drawn on the left arrow button (the < text is hidden when this is set).")]
        public Sprite arrowLeft;
        [Tooltip("Icon drawn on the right arrow button.")]
        public Sprite arrowRight;
        [Tooltip("Page dot for the card you are on. Keep it a small square image.")]
        public Sprite dotOn;
        [Tooltip("Page dot for the other cards.")]
        public Sprite dotOff;

        [Header("Colours")]
        public Color backgroundColor = new Color(0.941f, 0.827f, 0.408f, 1f); // Calm Gold #F0D368
        [Tooltip("Voting rows and the practice card panel.")]
        public Color panelColor = new Color(0.557f, 0.345f, 0.757f, 1f); // Original Purple #8E58C1
        [Tooltip("A voting row you have picked.")]
        public Color chosenColor = new Color(0.376f, 0.576f, 0.843f, 1f); // Boston #6093D7
        [Tooltip("DONE VOTING and REVIEW.")]
        public Color mainColor = new Color(0.376f, 0.576f, 0.843f, 1f);
        [Tooltip("LEARN and the arrows.")]
        public Color secondaryColor = new Color(0.557f, 0.345f, 0.757f, 1f);
        [Tooltip("READY.")]
        public Color goColor = new Color(0.475f, 0.773f, 0.412f, 1f); // Pretty Green #79C569
        [Tooltip("Titles, the word on each card, info and status lines (not the text on buttons). Dark, because the background is light gold.")]
        public Color textColor = new Color(0.18f, 0.11f, 0.29f, 1f);
        [Tooltip("The word and message after a CORRECT review. Dark green so it reads on the gold background.")]
        public Color correctTextColor = new Color(0.10f, 0.42f, 0.17f, 1f);
        [Tooltip("The word and message after a WRONG review. Deep pink so it reads on the gold background.")]
        public Color wrongTextColor = new Color(0.70f, 0.10f, 0.32f, 1f);
        [HideInInspector] public int paletteVersion;
        [Tooltip("Tick this if your sprites are plain white/grey shapes that should be coloured by the colours above. " +
                 "Leave it off for finished, coloured art (it is then drawn exactly as is).")]
        public bool tintSprites = false;

        private static UiSkin fallback;

        public static UiSkin Resolve()
        {
            if (fallback == null)
            {
                fallback = Resources.Load<UiSkin>("UiSkin");
                if (fallback == null) fallback = CreateInstance<UiSkin>();
            }
            return fallback;
        }

        /// <summary>The colour an image should get: untouched (white) for finished art, the skin colour otherwise.</summary>
        public Color Tint(Sprite sprite, Color color) => sprite != null && !tintSprites ? Color.white : color;

        public Color RowColor => Tint(row, panelColor);
        public Color RowChosenColor => rowChosen != null && !tintSprites ? Color.white : chosenColor;
    }
}
