using UnityEngine;

namespace SignRacers.Events
{
    /// <summary>
    /// Optional debugging listener: logs every event from a SignEventSource.
    /// </summary>
    public class SignEventLogger : MonoBehaviour
    {
        [SerializeField] private SignEventSource source;

        private void Awake()
        {
            if (source == null) source = GetComponent<SignEventSource>();
        }

        private void OnEnable()
        {
            if (source == null) { Debug.LogWarning("SignEventLogger has no SignEventSource.", this); return; }
            source.OnBoostTriggered += HandleBoost;
            source.OnPenaltyTriggered += HandlePenalty;
            source.OnItemUseTriggered += HandleItemUse;
        }

        private void OnDisable()
        {
            if (source == null) return;
            source.OnBoostTriggered -= HandleBoost;
            source.OnPenaltyTriggered -= HandlePenalty;
            source.OnItemUseTriggered -= HandleItemUse;
        }

        private void HandleBoost() => Debug.Log("[SignEvents] Boost triggered");
        private void HandlePenalty() => Debug.Log("[SignEvents] Penalty triggered");
        private void HandleItemUse(int slot) => Debug.Log($"[SignEvents] Item used from slot {slot}");
    }
}
