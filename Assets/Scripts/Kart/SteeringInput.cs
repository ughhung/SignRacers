using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SignRacers.Kart
{
    /// <summary>
    /// One-thumb steering. Hold a finger on the screen: the further left of centre it is, the harder
    /// the kart steers left, and the same to the right. Lift the finger and steering stops.
    /// A finger that starts on a button (BOOST, ITEM) is ignored, so signing never steers the kart.
    /// In the Editor, holding the left mouse button steers too, for testing.
    /// </summary>
    public static class SteeringInput
    {
        /// <summary>Fraction of the half-screen around the centre that does nothing, so a slightly off-centre touch doesn't drift the kart.</summary>
        public const float DeadZone = 0.08f;

        private const int MouseId = -1;

        // Every finger that is down, and whether it is allowed to steer (false if it started on a button).
        private static readonly Dictionary<int, bool> fingers = new Dictionary<int, bool>();
        private static readonly HashSet<int> seen = new HashSet<int>();
        private static readonly List<int> lifted = new List<int>();

        /// <summary>
        /// Steering from -1 (full left) to 1 (full right). Returns 0 when nothing is held.
        /// Call once per frame, on the kart this device owns.
        /// </summary>
        public static float Read()
        {
            seen.Clear();
            float steer = 0f;

#if ENABLE_INPUT_SYSTEM
            var screen = Touchscreen.current;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    steer = Consider(touch.touchId.ReadValue(), touch.position.ReadValue(), steer);
                }
            }
#if UNITY_EDITOR
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                steer = Consider(MouseId, mouse.position.ReadValue(), steer);
#endif
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
                steer = Consider(touch.fingerId, touch.position, steer);
            }
#if UNITY_EDITOR
            if (Input.GetMouseButton(0)) steer = Consider(MouseId, Input.mousePosition, steer);
#endif
#endif

            // Forget fingers that have lifted, so the next touch decides again.
            lifted.Clear();
            foreach (int id in fingers.Keys)
                if (!seen.Contains(id)) lifted.Add(id);
            foreach (int id in lifted) fingers.Remove(id);

            return steer;
        }

        /// <summary>Takes one finger that is down this frame and returns the strongest steer so far.</summary>
        private static float Consider(int id, Vector2 position, float current)
        {
            seen.Add(id);

            // Decide once, when the finger first goes down.
            if (!fingers.TryGetValue(id, out bool allowed))
            {
                allowed = !OverUi(id);
                fingers[id] = allowed;
            }
            if (!allowed) return current;

            // -1 at the left edge of the screen, 1 at the right edge.
            float raw = (position.x / Screen.width - 0.5f) * 2f;
            float value = Shape(raw);
            return Mathf.Abs(value) > Mathf.Abs(current) ? value : current;
        }

        /// <summary>Applies the dead zone and rescales what is left, so steering starts from zero at the edge of the dead zone.</summary>
        private static float Shape(float raw)
        {
            float magnitude = Mathf.Abs(raw);
            if (magnitude <= DeadZone) return 0f;
            float scaled = (magnitude - DeadZone) / (1f - DeadZone);
            return Mathf.Sign(raw) * Mathf.Clamp01(scaled);
        }

        /// <summary>True if this finger started on a UI element such as a button.</summary>
        private static bool OverUi(int id)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            return id == MouseId ? events.IsPointerOverGameObject() : events.IsPointerOverGameObject(id);
        }
    }
}
