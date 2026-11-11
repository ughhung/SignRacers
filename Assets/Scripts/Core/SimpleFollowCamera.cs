using SignRacers.Kart;
using SignRacers.Race;
using UnityEngine;

namespace SignRacers.Core
{
    /// <summary>
    /// Chase camera for a phone held upright: sits behind and above the player's kart and
    /// looks a little way ahead down the track. It follows the track's direction rather than
    /// the kart's own rotation, so a penalty spin-out doesn't whip the camera around.
    /// Follows only the kart this device owns (never a bot or another player).
    /// </summary>
    public class SimpleFollowCamera : MonoBehaviour
    {
        [SerializeField] private float distanceBehind = 9f;
        [SerializeField] private float height = 5f;
        [SerializeField] private float lookAhead = 8f;
        [SerializeField] private float positionSmoothing = 6f;
        [SerializeField] private float rotationSmoothing = 6f;

        [Header("Zoom in at GO")]
        [Tooltip("Far away view used while waiting and during the countdown.")]
        [SerializeField] private float introDistanceBehind = 24f;
        [SerializeField] private float introHeight = 14f;
        [SerializeField] private float introLookAhead = 10f;
        [Tooltip("Seconds the camera takes to zoom from the far view to the racing view when GO happens.")]
        [SerializeField] private float zoomDuration = 1.2f;

        private KartPathFollower kart;
        private bool snapped;
        private float blend = 1f; // 0 = far intro view, 1 = racing view

        private void LateUpdate()
        {
            if (kart == null)
            {
                foreach (var k in KartPathFollower.All)
                {
                    if (k.IsBot || !k.HasStateAuthority) continue;
                    kart = k;
                    snapped = false;
                    break;
                }
                if (kart == null) return;
            }

            Vector3 forward = kart.TrackForward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            // Far view until GO, then zoom in. With no RaceManager (testing a lone scene) it is always the racing view.
            var race = RaceManager.Instance;
            bool racingView = race == null || race.State == RaceState.Racing || race.State == RaceState.Finished;
            float target = racingView ? 1f : 0f;
            if (!snapped) blend = target;
            blend = Mathf.MoveTowards(blend, target, Time.deltaTime / Mathf.Max(0.01f, zoomDuration));
            float t = Mathf.SmoothStep(0f, 1f, blend);

            float dist = Mathf.Lerp(introDistanceBehind, distanceBehind, t);
            float h = Mathf.Lerp(introHeight, height, t);
            float ahead = Mathf.Lerp(introLookAhead, lookAhead, t);

            Vector3 kartPosition = kart.transform.position;
            Vector3 desiredPosition = kartPosition - forward * dist + Vector3.up * h;
            Quaternion desiredRotation = Quaternion.LookRotation((kartPosition + forward * ahead) - desiredPosition, Vector3.up);

            if (!snapped)
            {
                transform.SetPositionAndRotation(desiredPosition, desiredRotation);
                snapped = true;
                return;
            }

            float dt = Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionSmoothing * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSmoothing * dt));
        }
    }
}
