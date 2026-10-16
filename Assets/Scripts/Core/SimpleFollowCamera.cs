using SignRacers.Kart;
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

        private KartPathFollower kart;
        private bool snapped;

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

            Vector3 kartPosition = kart.transform.position;
            Vector3 desiredPosition = kartPosition - forward * distanceBehind + Vector3.up * height;
            Quaternion desiredRotation = Quaternion.LookRotation((kartPosition + forward * lookAhead) - desiredPosition, Vector3.up);

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
