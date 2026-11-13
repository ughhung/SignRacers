using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SignRacers.UI
{
    /// <summary>
    /// Horizontal swipe between full-width pages (cards). Put it on a RectTransform that clips its children
    /// (RectMask2D) and has a transparent Image so drags are caught. Pages are stretched children of it.
    /// Swipe left = next card, swipe right = previous. Drags that start on a button still swipe.
    /// </summary>
    public class SwipePager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float SwipeThreshold = 0.15f; // fraction of a page you must drag to change card

        [SerializeField] private RectTransform[] pages = new RectTransform[0];

        /// <summary>Raised when the card you are heading to changes.</summary>
        public event Action<int> PageChanged;

        /// <summary>While true, swiping is ignored (used during a review so the camera isn't left behind).</summary>
        public bool Locked { get; set; }

        public int Count { get; private set; }
        public int Page { get; private set; }

        private float position;
        private bool dragging;
        private float dragStartLocalX, dragStartPosition;
        private int dragStartPage;

        private RectTransform Rect => (RectTransform)transform;

        /// <summary>How many pages are in use (the rest stay hidden).</summary>
        public void SetCount(int count)
        {
            count = Mathf.Clamp(count, 0, pages.Length);
            if (count == Count) return;
            Count = count;
            if (Page >= Count) GoTo(Mathf.Max(0, Count - 1), true);
        }

        public void GoTo(int page, bool instant = false)
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, Count - 1));
            bool changed = page != Page;
            Page = page;
            if (instant) position = page;
            if (changed) PageChanged?.Invoke(Page);
        }

        public void Next() { if (!Locked) GoTo(Page + 1); }
        public void Previous() { if (!Locked) GoTo(Page - 1); }

        private float lastPosition = float.NaN, lastWidth = -1f;
        private int lastCount = -1;

        private void Update()
        {
            if (!dragging)
            {
                position = Mathf.Lerp(position, Page, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                if (Mathf.Abs(position - Page) < 0.001f) position = Page; // settle exactly, so we can stop updating
            }

            float width = Rect.rect.width;
            // Nothing moved since last frame: skip the work (a resting card costs nothing).
            if (position == lastPosition && width == lastWidth && Count == lastCount) return;
            lastPosition = position; lastWidth = width; lastCount = Count;

            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                if (page == null) continue;
                bool show = i < Count && Mathf.Abs(i - position) < 1.02f;
                if (page.gameObject.activeSelf != show) page.gameObject.SetActive(show);
                if (show) page.anchoredPosition = new Vector2((i - position) * width, 0f);
            }
        }

        private bool LocalX(PointerEventData e, out float x)
        {
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, e.position, e.pressEventCamera, out var p);
            x = p.x;
            return ok;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (Locked || Count <= 1 || !LocalX(e, out dragStartLocalX)) return;
            dragging = true;
            dragStartPosition = position;
            dragStartPage = Page;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging || !LocalX(e, out float x)) return;
            float width = Mathf.Max(1f, Rect.rect.width);
            position = Mathf.Clamp(dragStartPosition - (x - dragStartLocalX) / width, -0.25f, Count - 0.75f);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            float moved = position - dragStartPage;
            int target = dragStartPage;
            if (moved > SwipeThreshold) target++;
            else if (moved < -SwipeThreshold) target--;
            GoTo(target);
        }
    }
}
