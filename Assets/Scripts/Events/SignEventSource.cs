using System;
using UnityEngine;

namespace SignRacers.Events
{
    /// <summary>
    /// The seam of the whole game. Anything that can decide "a boost happened",
    /// "a penalty happened" or "an item was used" is a SignEventSource:
    ///   - DebugKeyboardSignSource   (testing without a camera)
    ///   - AslPowerupSignSource      (the real camera + sign recognizer)
    ///   - BotSignSource             (later, scripted AI decisions)
    /// Karts, VFX and UI only subscribe to these events and never know which kind of
    /// source is feeding them. Item use carries the inventory slot (0 or 1) being used.
    /// </summary>
    public abstract class SignEventSource : MonoBehaviour
    {
        public event Action OnBoostTriggered;
        public event Action OnPenaltyTriggered;
        public event Action<int> OnItemUseTriggered;

        protected void RaiseBoost() => OnBoostTriggered?.Invoke();
        protected void RaisePenalty() => OnPenaltyTriggered?.Invoke();
        protected void RaiseItemUse(int slot = 0) => OnItemUseTriggered?.Invoke(slot);
    }
}
