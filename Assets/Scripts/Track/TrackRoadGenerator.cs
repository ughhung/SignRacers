using UnityEngine;

namespace SignRacers.Track
{
    /// <summary>
    /// Builds a visible road: a flat strip that follows the track's curve. Put it on an empty
    /// child object of the Track. In the editor it rebuilds itself whenever you reshape the
    /// track or change a setting here, so dragging a spline knot reshapes the road live.
    /// (Right-click the component > Generate Road to force a rebuild.)
    /// The strip is made by stepping along the curve and placing one point on each edge at
    /// every step, then joining neighboring pairs with triangles.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TrackRoadGenerator : MonoBehaviour
    {
        private const string MeshName = "TrackRoad";

        [SerializeField] private TrackPath track;

        [Header("Shape")]
        [Tooltip("Total road width. Karts use lanes up to 6 units to the right, so keep this at 16 or more.")]
        [SerializeField] private float width = 16f;
        [Tooltip("Distance between steps along the curve. Smaller is smoother but heavier.")]
        [SerializeField] private float stepLength = 2f;
        [Tooltip("Lifts the road slightly above the track curve so it doesn't flicker against the ground.")]
        [SerializeField] private float heightAboveTrack = 0.08f;

        [Header("Look")]
        [Tooltip("Leave empty for a plain dark gray road.")]
        [SerializeField] private Material roadMaterial;
        [Tooltip("How far along the road one repeat of the texture covers.")]
        [SerializeField] private float textureTileLength = 10f;

        [Header("Editor")]
        [SerializeField] private bool autoUpdateInEditor = true;
        [Tooltip("Tick this if the road is invisible from above (the triangles are facing down).")]
        [SerializeField] private bool flipFaces;

        private int lastHash = int.MinValue;

        private void OnEnable() => Generate();

        private void Update()
        {
            if (Application.isPlaying || !autoUpdateInEditor) return;
            if (track == null) track = FindFirstObjectByType<TrackPath>();
            if (track == null) return;

            int hash = track.ComputeShapeHash();
            unchecked
            {
                hash = hash * 31 + width.GetHashCode();
                hash = hash * 31 + stepLength.GetHashCode();
                hash = hash * 31 + heightAboveTrack.GetHashCode();
                hash = hash * 31 + textureTileLength.GetHashCode();
                hash = hash * 31 + (flipFaces ? 1 : 0);
                hash = hash * 31 + transform.localToWorldMatrix.GetHashCode();
            }

            if (hash != lastHash) Generate();
        }

        [ContextMenu("Generate Road")]
        public void Generate()
        {
            if (track == null) track = FindFirstObjectByType<TrackPath>();
            if (track == null)
            {
                Debug.LogWarning("[TrackRoad] No TrackPath found in the scene.", this);
                return;
            }

            track.EnsureBuilt();
            if (!track.IsValid) return;

            float length = track.TotalLength;
            int steps = Mathf.Max(8, Mathf.CeilToInt(length / Mathf.Max(0.25f, stepLength)));

            var vertices = new Vector3[(steps + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[steps * 6];

            for (int i = 0; i <= steps; i++)
            {
                float distance = length * i / steps; // the last step lands exactly back on the start
                Vector3 position = track.GetPosition(distance, out Vector3 forward);
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;

                Vector3 center = position + Vector3.up * heightAboveTrack;
                Vector3 half = right * (width * 0.5f);

                vertices[i * 2] = transform.InverseTransformPoint(center - half);     // left edge
                vertices[i * 2 + 1] = transform.InverseTransformPoint(center + half); // right edge

                float v = distance / Mathf.Max(0.1f, textureTileLength);
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }

            for (int i = 0; i < steps; i++)
            {
                int l0 = i * 2, r0 = i * 2 + 1, l1 = i * 2 + 2, r1 = i * 2 + 3;
                int t = i * 6;
                if (!flipFaces)
                {
                    triangles[t] = l0; triangles[t + 1] = l1; triangles[t + 2] = r1;
                    triangles[t + 3] = l0; triangles[t + 4] = r1; triangles[t + 5] = r0;
                }
                else
                {
                    triangles[t] = l0; triangles[t + 1] = r1; triangles[t + 2] = l1;
                    triangles[t + 3] = l0; triangles[t + 4] = r0; triangles[t + 5] = r1;
                }
            }

            var filter = GetComponent<MeshFilter>();
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || mesh.name != MeshName)
            {
                mesh = new Mesh { name = MeshName };
                filter.sharedMesh = mesh;
            }
            else
            {
                mesh.Clear();
            }

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            ApplyMaterial();

            lastHash = track.ComputeShapeHash();
            unchecked
            {
                lastHash = lastHash * 31 + width.GetHashCode();
                lastHash = lastHash * 31 + stepLength.GetHashCode();
                lastHash = lastHash * 31 + heightAboveTrack.GetHashCode();
                lastHash = lastHash * 31 + textureTileLength.GetHashCode();
                lastHash = lastHash * 31 + (flipFaces ? 1 : 0);
                lastHash = lastHash * 31 + transform.localToWorldMatrix.GetHashCode();
            }
        }

        private void ApplyMaterial()
        {
            var meshRenderer = GetComponent<MeshRenderer>();

            if (roadMaterial != null)
            {
                meshRenderer.sharedMaterial = roadMaterial;
                return;
            }

            // No material chosen: use a plain dark gray one (the built-in default shows pink in URP).
            var current = meshRenderer.sharedMaterial;
            if (current != null && current.name != "Default-Material") return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            var material = new Material(shader) { name = "RoadDefault" };
            material.color = new Color(0.22f, 0.22f, 0.25f);
            meshRenderer.sharedMaterial = material;
        }
    }
}
