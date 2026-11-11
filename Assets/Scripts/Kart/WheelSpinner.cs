using UnityEngine;

namespace SignRacers
{
    /// <summary>
    /// Visual only. Spins the wheel objects in proportion to how fast the kart is moving.
    /// Put it on the bike model (the child of the Kart, e.g. SM_Veh_QuadBike_01) and drag the four
    /// wheel objects into the Wheels list. It measures the model's own movement, so it works for
    /// every kart (yours, other players, bots) and doesn't touch the networking.
    /// </summary>
    public class WheelSpinner : MonoBehaviour
    {
        [Tooltip("The wheel objects to spin.")]
        [SerializeField] private Transform[] wheels;

        [Tooltip("Wheel radius in world units. Bigger = spins slower for the same speed.")]
        [SerializeField] private float wheelRadius = 0.35f;

        [Tooltip("The wheel's axle in its own space. Synty wheels are usually X. If they tumble " +
                 "instead of rolling, try Y or Z.")]
        [SerializeField] private Vector3 axle = Vector3.right;

        [Tooltip("Tick this if the wheels spin backwards.")]
        [SerializeField] private bool reverse;

        [Tooltip("How quickly the spin catches up with the speed (higher = snappier).")]
        [SerializeField] private float smoothing = 12f;

        private Vector3 lastPosition;
        private float spinSpeed; // degrees per second, smoothed
        private bool hasLast;

        private void OnEnable() => hasLast = false;

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = transform.position;
            if (!hasLast) { lastPosition = pos; hasLast = true; return; }

            Vector3 delta = pos - lastPosition;
            lastPosition = pos;

            // Ignore teleports (race reset, respawn), they aren't driving.
            float target = 0f;
            if (delta.magnitude < 5f)
            {
                float forwardSpeed = Vector3.Dot(delta, transform.forward) / dt;          // units per second
                float degreesPerSecond = forwardSpeed / Mathf.Max(0.01f, wheelRadius) * Mathf.Rad2Deg;
                target = reverse ? -degreesPerSecond : degreesPerSecond;
            }

            spinSpeed = Mathf.Lerp(spinSpeed, target, 1f - Mathf.Exp(-smoothing * dt));

            float step = spinSpeed * dt;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null) wheels[i].Rotate(axle, step, Space.Self);
            }
        }
    }
}
