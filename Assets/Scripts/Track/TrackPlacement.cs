using UnityEngine;

namespace SignRacers.Track
{
    /// <summary>
    /// Puts an object at a position along the track, by distance. Add it to a mystery box
    /// (or any scenery) and type how far along the track it should sit. When you reshape
    /// the track, the object follows, so it never ends up off the road. Works in the editor
    /// as well as in the game.
    /// </summary>
    [ExecuteAlways]
    public class TrackPlacement : MonoBehaviour
    {
        [SerializeField] private TrackPath track;

        [Tooltip("How far along the track, in world units from the start line.")]
        [SerializeField] private float distance = 50f;
        [Tooltip("Sideways from the center line. Positive is to the right.")]
        [SerializeField] private float lateralOffset = 0f;
        [Tooltip("Height above the road.")]
        [SerializeField] private float heightOffset = 1f;
        [SerializeField] private bool faceAlongTrack;

        [Tooltip("Read-only: the track's total length, so you know the range for Distance.")]
        [SerializeField] private float trackLength;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (!Application.isPlaying) Apply();
        }

        private void Apply()
        {
            if (track == null) track = FindFirstObjectByType<TrackPath>();
            if (track == null) return;

            track.EnsureBuilt();
            if (!track.IsValid) return;
            trackLength = track.TotalLength;

            Vector3 position = track.GetPosition(distance, out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 target = position + right * lateralOffset + Vector3.up * heightOffset;

            if ((transform.position - target).sqrMagnitude > 1e-6f) transform.position = target;

            if (faceAlongTrack)
            {
                Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
                if (Quaternion.Angle(transform.rotation, rotation) > 0.01f) transform.rotation = rotation;
            }
        }
    }
}
