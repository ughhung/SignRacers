using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SignRacers.Events
{
    /// <summary>
    /// Stand-in for real sign detection while building the game.
    ///   Space = correct sign (boost)
    ///   X     = wrong sign / timeout (penalty)
    ///   Z     = sign the item word (uses the oldest item in the inventory)
    /// Works with Unity 6's new Input System or the legacy Input Manager.
    /// </summary>
    public class DebugKeyboardSignSource : SignEventSource
    {
        private void Update()
        {
            if (BoostPressed()) RaiseBoost();
            if (PenaltyPressed()) RaisePenalty();
            if (UseItemPressed()) RaiseItemUse();
        }

#if ENABLE_INPUT_SYSTEM
        private static bool BoostPressed() => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        private static bool PenaltyPressed() => Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame;
        private static bool UseItemPressed() => Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame;
#else
        private static bool BoostPressed() => Input.GetKeyDown(KeyCode.Space);
        private static bool PenaltyPressed() => Input.GetKeyDown(KeyCode.X);
        private static bool UseItemPressed() => Input.GetKeyDown(KeyCode.Z);
#endif
    }
}
