using SignRacers.Events;
using SignRacers.Kart;
using UnityEngine;

namespace SignRacers.Items
{
    /// <summary>
    /// A pickup box sitting on the track. When the local player's kart drives through it,
    /// a random item goes into their inventory (bots collect them too) and the box disappears briefly.
    /// If the inventory is full the box is still used up, but the kart gets nothing.
    /// Uses a simple distance check instead of physics triggers, so karts need no Rigidbody.
    /// It also spins and bobs while the game runs, for a bit of life. Spinning is around the
    /// WORLD up axis, so a box you tilted onto its corner in the Inspector keeps its tilt.
    /// Greybox simplification: each device hides/respawns its own boxes locally, boxes
    /// are not networked yet.
    /// </summary>
    public class MysteryBox : MonoBehaviour
    {
        [Header("Pickup")]
        [SerializeField] private float pickupRadius = 2.5f;
        [SerializeField] private float respawnSeconds = 5f;

        [Header("Spin and bob (Play mode only)")]
        [Tooltip("Degrees per second around each axis. Y = 90 is one full turn every 4 seconds.")]
        [SerializeField] private Vector3 spinDegreesPerSecond = new Vector3(0f, 90f, 0f);
        [Tooltip("How far it floats up and down. 0 turns bobbing off.")]
        [SerializeField] private float bobHeight = 0.25f;
        [SerializeField] private float bobSpeed = 2f;
        [Tooltip("Gives each box a different starting point so a row of boxes doesn't bob in sync.")]
        [SerializeField] private bool randomizeBobStart = true;

        private static readonly ItemType[] Pool = { ItemType.SuperBoost, ItemType.SlowOthers };

        private bool available = true;
        private float respawnAt;
        private Vector3 basePosition;
        private float bobPhase;

        // Start, not OnEnable: Track Placement moves the box when it is enabled, and we want to
        // remember the spot it chose before bobbing around it.
        private void Start()
        {
            basePosition = transform.position;
            bobPhase = randomizeBobStart ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        }

        private void Update()
        {
            SpinAndBob();

            if (!available)
            {
                if (Time.time >= respawnAt) SetAvailable(true);
                return;
            }

            foreach (var kart in KartPathFollower.All)
            {
                if (!kart.HasStateAuthority) continue;

                // Only the distance along the ground counts, so floating higher doesn't matter.
                Vector3 delta = kart.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > pickupRadius * pickupRadius) continue;

                // The box is used up either way. A kart with a full inventory gets nothing,
                // and the items it already holds are left alone.
                ItemType item = Pool[Random.Range(0, Pool.Length)];
                bool received = kart.TryAddItem(item);
                Debug.Log(received
                    ? $"[MysteryBox] Kart picked up {item}"
                    : "[MysteryBox] Inventory full, the box was used up without giving an item");

                respawnAt = Time.time + respawnSeconds;
                SetAvailable(false);
                return;
            }
        }

        private void SpinAndBob()
        {
            transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.World);

            if (bobHeight > 0f)
            {
                float offset = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
                transform.position = basePosition + Vector3.up * offset;
            }
        }

        private void SetAvailable(bool value)
        {
            available = value;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = value;
        }
    }
}
