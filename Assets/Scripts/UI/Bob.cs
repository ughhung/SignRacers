using UnityEngine;

// Moves a UI object gently up and down over time. Works on any UI element.
// Speed sets how fast it moves, and Height sets how far.
public class Bob : MonoBehaviour
{
    [Tooltip("How far up and down the object moves, in canvas pixels.")]
    [SerializeField] private float height = 8f;

    [Tooltip("How fast it moves. Higher = faster. 2 matches the splash logo.")]
    [SerializeField] private float speed = 2f;

    private RectTransform rect;
    private Vector2 basePosition;

    private void Awake()
    {
        rect = transform as RectTransform;
        basePosition = rect.anchoredPosition;
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.unscaledTime * speed) * height;
        rect.anchoredPosition = basePosition + new Vector2(0f, offset);
    }

    private void OnDisable()
    {
        // Put it back where it started when it's hidden.
        if (rect != null) rect.anchoredPosition = basePosition;
    }
}
