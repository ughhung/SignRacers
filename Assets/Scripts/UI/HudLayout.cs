using UnityEngine;

namespace SignRacers.UI
{
    /// <summary>
    /// Shared sizing for the code-drawn placeholder HUDs, so they fit a phone held upright.
    ///   Unit - the screen's SHORT side (the width in portrait). Sizes are fractions of it,
    ///          so text stays readable however tall the phone is.
    ///   Safe - the usable screen area (clear of the notch, rounded corners and home bar),
    ///          converted to the top-left coordinates the on-screen GUI uses.
    /// </summary>
    public static class HudLayout
    {
        public static float Unit => Mathf.Min(Screen.width, Screen.height);

        public static Rect Safe
        {
            get
            {
                Rect area = Screen.safeArea;
                return new Rect(area.x, Screen.height - area.yMax, area.width, area.height);
            }
        }
    }
}
