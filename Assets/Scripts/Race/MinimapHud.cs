using System.Collections.Generic;
using SignRacers.Kart;
using SignRacers.Track;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.Race
{
    /// <summary>
    /// Fills the HUD prefab's map box: draws the track outline once from the track's own data
    /// into a texture, then keeps one dot per kart on top of it. Your kart is the big yellow
    /// dot with a white ring. Where the box sits and how big it is comes from the prefab;
    /// this script only draws inside the RawImage it is given. When the Map Image slot is
    /// empty it does nothing.
    /// </summary>
    public class MinimapHud : MonoBehaviour
    {
        private const int Padding = 8;        // empty border around the track, in texture pixels
        private const float LineRadius = 3f;  // road line thickness on the map, in texture pixels

        [Header("Where it draws")]
        [Tooltip("The RawImage inside the map box. Dots are created as its children. Add an Aspect Ratio Fitter (Fit In Parent) to it so the map keeps its proportions.")]
        [SerializeField] private RawImage mapImage;

        [Header("Look")]
        [SerializeField] private Color roadColor = new Color(0.88f, 0.88f, 0.92f);
        [SerializeField] private Color outlineColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
        [SerializeField] private Color startLineColor = new Color(0.35f, 0.95f, 0.45f);
        [Tooltip("Size of the map image in pixels along its longest side.")]
        [SerializeField] private int textureSize = 256;
        [Tooltip("Dot size in HUD units (the HUD is 1080 wide).")]
        [SerializeField] private float dotSize = 26f;

        [Header("Orientation")]
        [Tooltip("Turns the whole map clockwise in steps of 90 degrees (0, 1, 2 or 3).")]
        [SerializeField, Range(0, 3)] private int quarterTurns = 0;
        [Tooltip("Mirrors the map left-to-right (applied before the turns).")]
        [SerializeField] private bool flipHorizontal = false;

        private static readonly Color[] PlayerColors =
        {
            new Color(1.0f, 0.35f, 0.35f),  // red
            new Color(0.40f, 0.65f, 1.0f),  // blue
            new Color(0.45f, 0.90f, 0.50f), // green
            new Color(1.0f, 0.65f, 0.25f),  // orange
        };

        private class Dot
        {
            public RectTransform root;
            public Image fill;
            public Image ring;
            public int seenFrame;
        }

        private readonly Dictionary<KartPathFollower, Dot> dots = new Dictionary<KartPathFollower, Dot>();
        private readonly List<KartPathFollower> stale = new List<KartPathFollower>();

        private Texture2D mapTexture;
        private int builtForTrackVersion = -1;
        private Sprite dotSprite;
        private float minX, minZ, scale;

        private void OnDestroy()
        {
            if (mapTexture != null) Destroy(mapTexture);
            if (dotSprite != null)
            {
                Destroy(dotSprite.texture);
                Destroy(dotSprite);
            }
        }

        private void LateUpdate()
        {
            if (mapImage == null) return;
            if (mapTexture != null && builtForTrackVersion != TrackLoader.Version)
            {
                Destroy(mapTexture); // a different track was loaded: draw the new outline
                mapTexture = null;
            }
            if (mapTexture == null)
            {
                if (!BuildMap()) return;
                builtForTrackVersion = TrackLoader.Version;
            }

            foreach (var kart in KartPathFollower.All)
            {
                if (kart == null) continue;
                if (!dots.TryGetValue(kart, out Dot dot))
                {
                    dot = CreateDot();
                    dots[kart] = dot;
                }
                dot.seenFrame = Time.frameCount;
                UpdateDot(kart, dot);
            }

            // Remove dots for karts that have left.
            stale.Clear();
            foreach (var pair in dots)
                if (pair.Value.seenFrame != Time.frameCount) stale.Add(pair.Key);
            foreach (var key in stale)
            {
                if (dots[key].root != null) Destroy(dots[key].root.gameObject);
                dots.Remove(key);
            }
        }

        // ---------- dots ----------

        private Dot CreateDot()
        {
            var rootObject = new GameObject("Dot", typeof(RectTransform));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(mapImage.rectTransform, false);
            root.pivot = new Vector2(0.5f, 0.5f);

            var ring = MakeDotImage("Ring", root, Color.white);
            var fill = MakeDotImage("Fill", root, Color.white);
            return new Dot { root = root, fill = fill, ring = ring };
        }

        private Image MakeDotImage(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = dotSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void UpdateDot(KartPathFollower kart, Dot dot)
        {
            bool local = !kart.IsBot && kart.HasStateAuthority;

            Vector2 m = Project(kart.transform.position);
            float tx = ((m.x - minX) * scale + Padding) / mapTexture.width;
            float ty = ((m.y - minZ) * scale + Padding) / mapTexture.height;

            dot.root.anchorMin = dot.root.anchorMax = new Vector2(tx, ty);
            dot.root.anchoredPosition = Vector2.zero;

            float size = local ? dotSize * 1.5f : dotSize;
            dot.root.sizeDelta = new Vector2(size, size);

            dot.fill.color = local ? new Color(1f, 0.92f, 0.2f) : ColorFor(kart);
            dot.ring.gameObject.SetActive(local);
            if (local)
            {
                // The ring is a slightly bigger white dot behind the yellow one.
                var ringRect = dot.ring.rectTransform;
                ringRect.anchorMin = new Vector2(-0.18f, -0.18f);
                ringRect.anchorMax = new Vector2(1.18f, 1.18f);
                ringRect.offsetMin = ringRect.offsetMax = Vector2.zero;
                dot.ring.transform.SetAsFirstSibling();

                // Draw your own dot above everyone else's.
                if (dot.root.GetSiblingIndex() != dot.root.parent.childCount - 1) dot.root.SetAsLastSibling();
            }
        }

        private static Color ColorFor(KartPathFollower kart)
        {
            if (kart.IsBot) return new Color(0.7f, 0.7f, 0.7f);
            int id = kart.Object != null && kart.Object.IsValid ? kart.Object.InputAuthority.PlayerId : 0;
            return PlayerColors[Mathf.Abs(id) % PlayerColors.Length];
        }

        /// <summary>World position to flat map coordinates, with the flip and turns applied.</summary>
        private Vector2 Project(Vector3 world)
        {
            float x = flipHorizontal ? -world.x : world.x;
            float z = world.z;
            switch (quarterTurns & 3)
            {
                case 1: return new Vector2(z, -x);   // 90 degrees clockwise
                case 2: return new Vector2(-x, -z);  // 180 degrees
                case 3: return new Vector2(-z, x);   // 270 degrees clockwise
                default: return new Vector2(x, z);
            }
        }

        // ---------- drawing the track outline (once) ----------

        private bool BuildMap()
        {
            var track = FindFirstObjectByType<TrackPath>();
            if (track == null) return false;
            track.EnsureBuilt();
            var info = TrackInfo.Current;
            if (info != null) { quarterTurns = info.MinimapQuarterTurns; flipHorizontal = info.MinimapFlipHorizontal; }
            if (!track.IsValid) return false;

            // Sample the track and find how big it is on the ground.
            int count = 400;
            float maxX = float.MinValue, maxZ = float.MinValue;
            minX = float.MaxValue;
            minZ = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = Project(track.GetPosition(track.TotalLength * i / count, out _));
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
            }

            float worldWidth = Mathf.Max(maxX - minX, 1f);
            float worldDepth = Mathf.Max(maxZ - minZ, 1f);
            scale = (textureSize - Padding * 2) / Mathf.Max(worldWidth, worldDepth);
            int width = Mathf.CeilToInt(worldWidth * scale + Padding * 2);
            int height = Mathf.CeilToInt(worldDepth * scale + Padding * 2);

            var pixels = new Color32[width * height];

            // Two passes: a dark outline first, then the lighter road line on top.
            for (int pass = 0; pass < 2; pass++)
            {
                float radius = pass == 0 ? LineRadius + 1.5f : LineRadius;
                Color32 color = pass == 0 ? (Color32)outlineColor : (Color32)roadColor;

                int steps = Mathf.Clamp(Mathf.CeilToInt(track.TotalLength * scale / 1.2f), 100, 3000);
                for (int i = 0; i <= steps; i++)
                {
                    Vector2 p = Project(track.GetPosition(track.TotalLength * i / steps, out _));
                    Stamp(pixels, width, height, (p.x - minX) * scale + Padding, (p.y - minZ) * scale + Padding, radius, color);
                }
            }

            // A short green tick across the road at the start and finish line.
            Vector3 startWorld = track.GetPosition(0f, out Vector3 forward);
            Vector2 start = Project(startWorld);
            Vector2 along = (Project(startWorld + forward) - start).normalized;
            Vector2 across = new Vector2(-along.y, along.x);
            float sx = (start.x - minX) * scale + Padding;
            float sz = (start.y - minZ) * scale + Padding;
            for (float t = -LineRadius * 1.8f; t <= LineRadius * 1.8f; t += 0.7f)
                Stamp(pixels, width, height, sx + across.x * t, sz + across.y * t, 1.4f, (Color32)startLineColor);

            mapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "TrackMinimap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            mapTexture.SetPixels32(pixels);
            mapTexture.Apply();

            mapImage.texture = mapTexture;
            mapImage.color = Color.white;

            // Keep the map's proportions inside its box.
            var fitter = mapImage.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)width / height;
            }

            dotSprite = MakeDotSprite(64);
            return true;
        }

        private static void Stamp(Color32[] pixels, int width, int height, float cx, float cy, float radius, Color32 color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(cx + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(cy + radius));
            float r2 = radius * radius;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r2) pixels[y * width + x] = color;
                }
            }
        }

        private static Sprite MakeDotSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "MinimapDot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    float alpha = Mathf.Clamp01(radius - distance); // soft one-pixel edge
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
