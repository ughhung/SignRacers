using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace SignRacers.Track
{
    /// <summary>
    /// The invisible rail the karts follow. Karts don't steer: they just have a "distance
    /// along the track", and this class turns that distance into a world position and a
    /// facing direction.
    ///
    /// Put it on the same object as a Unity SplineContainer (adding this component adds one
    /// for you). The spline holds the SHAPE, which you drag around in the Scene view. This
    /// script samples that shape once into a table, so asking "where am I at distance d?"
    /// is fast and uses real distance in world units rather than the spline's own 0-to-1 scale.
    /// The spline must be Closed so it forms a loop.
    /// Not networked on purpose: every device builds the same path from the same scene.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class TrackPath : MonoBehaviour
    {
        private const int SamplesPerKnot = 60;
        private const int MinSamples = 200;

        private SplineContainer container;
        private Vector3[] samples;   // dense points along the curve
        private Vector3[] tangents;  // direction of travel at each sample
        private float[] cumulative;  // distance from sample 0 to sample i (last entry = total length)
        private float total;
        private int builtHash;

        public float TotalLength => total;
        public bool IsValid => samples != null && samples.Length >= 3 && total > 0.01f;

        private void Awake() => Rebuild();

        private Spline CurrentSpline
        {
            get
            {
                if (container == null) container = GetComponent<SplineContainer>();
                return container != null ? container.Spline : null;
            }
        }

        /// <summary>Rebuild the lookup table from the spline's current shape.</summary>
        public void Rebuild()
        {
            Spline spline = CurrentSpline;
            if (spline == null || spline.Count < 2)
            {
                samples = null;
                total = 0f;
                builtHash = 0;
                return;
            }

            if (!spline.Closed && Application.isPlaying)
                Debug.LogWarning("[TrackPath] The spline isn't closed, so the track joins its last knot straight back to the first. Make the spline a closed loop.", this);

            int count = Mathf.Max(MinSamples, spline.Count * SamplesPerKnot);
            samples = new Vector3[count];
            Transform t = container.transform;
            for (int i = 0; i < count; i++)
            {
                float3 local = SplineUtility.EvaluatePosition(spline, i / (float)count);
                samples[i] = t.TransformPoint(new Vector3(local.x, local.y, local.z));
            }

            cumulative = new float[count + 1];
            total = 0f;
            for (int i = 0; i < count; i++)
            {
                total += Vector3.Distance(samples[i], samples[(i + 1) % count]);
                cumulative[i + 1] = total;
            }

            tangents = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = samples[(i + 1) % count] - samples[(i - 1 + count) % count];
                tangents[i] = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            }

            builtHash = ComputeShapeHash();
        }

        /// <summary>
        /// Makes sure the table exists, and in the editor that it matches the shape as it is
        /// right now. Used by tools that run outside Play mode (the road, placed objects).
        /// </summary>
        public void EnsureBuilt()
        {
            if (samples == null || (!Application.isPlaying && ComputeShapeHash() != builtHash)) Rebuild();
        }

        /// <summary>A number that changes whenever the track's shape changes.</summary>
        public int ComputeShapeHash()
        {
            Spline spline = CurrentSpline;
            if (spline == null || spline.Count < 2) return 0;

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + spline.Count;
                hash = hash * 31 + (spline.Closed ? 1 : 0);
                for (int i = 0; i < spline.Count; i++)
                {
                    BezierKnot knot = spline[i];
                    hash = Mix(hash, knot.Position.x, knot.Position.y, knot.Position.z);
                    hash = Mix(hash, knot.TangentIn.x, knot.TangentIn.y, knot.TangentIn.z);
                    hash = Mix(hash, knot.TangentOut.x, knot.TangentOut.y, knot.TangentOut.z);
                    hash = Mix(hash, knot.Rotation.value.x, knot.Rotation.value.y, knot.Rotation.value.z);
                }
                hash = hash * 31 + transform.localToWorldMatrix.GetHashCode();
                return hash;
            }
        }

        private static int Mix(int hash, float a, float b, float c)
        {
            unchecked
            {
                hash = hash * 31 + a.GetHashCode();
                hash = hash * 31 + b.GetHashCode();
                hash = hash * 31 + c.GetHashCode();
                return hash;
            }
        }

        /// <summary>World position + forward direction at a given distance (wraps around the loop).</summary>
        public Vector3 GetPosition(float distance, out Vector3 forward)
        {
            forward = Vector3.forward;
            if (!IsValid) return transform.position;

            int count = samples.Length;
            float d = Mathf.Repeat(distance, total);

            // Binary search for the sample whose span contains d.
            int lo = 0, hi = count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cumulative[mid] <= d) lo = mid; else hi = mid - 1;
            }

            int i = lo;
            int next = (i + 1) % count;
            float spanLength = cumulative[i + 1] - cumulative[i];
            float t = spanLength > 0f ? (d - cumulative[i]) / spanLength : 0f;

            // Blend the directions too, so heading changes smoothly between samples.
            Vector3 blended = Vector3.Lerp(tangents[i], tangents[next], t);
            forward = blended.sqrMagnitude > 1e-6f ? blended.normalized : tangents[i];

            return Vector3.Lerp(samples[i], samples[next], t);
        }
    }
}
