using SignRacers.Events;
using SignRacers.Kart;
using SignRacers.Race;
using SignRacers.Signs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SignRacers.UI
{
    /// <summary>
    /// Drives the in-race HUD prefab (built by Sign Racers > Build Race HUD Prefab): the lap
    /// counter, your position, and the three power-up buttons (two items on top, BOOST below).
    /// It only reads the game's state and tells the sign source what was tapped; the
    /// signing itself still lives in AslPowerupSignSource. Shows itself only while you are
    /// waiting, counting down or racing, and hides once you finish.
    /// </summary>
    public class RaceHudView : MonoBehaviour
    {
        [System.Serializable]
        public class ButtonParts
        {
            public Button button;
            public Image face;
            public Image cooldownRing;
            [Tooltip("Optional. A child Image that shows the sprite from ItemVisuals.")]
            public Image icon;
            public TMP_Text label;
        }


        [Header("Whole HUD")]
        [SerializeField] private CanvasGroup group;

        [Header("Top left and top right")]
        [SerializeField] private TMP_Text lapText;
        [SerializeField] private TMP_Text positionText;

        [Header("Colours")]
        [Tooltip("Optional. Leave empty to use Resources/UiTheme (create it with Sign Racers > Create UI Theme). " +
                 "Position and button colours are set there.")]
        [SerializeField] private UiTheme theme;
        [Tooltip("Optional. Leave empty to use Resources/ItemVisuals (Sign Racers > Create Item Visuals). " +
                 "Buttons whose sprite is empty keep their text label.")]
        [SerializeField] private ItemVisuals visuals;
        [Tooltip("The position number appears this many seconds after GO, then fades in.")]
        [SerializeField] private float showPositionAfterSeconds = 3f;
        [SerializeField] private float positionFadeSeconds = 0.6f;

        [Header("Buttons")]
        [SerializeField] private ButtonParts boost;
        [SerializeField] private ButtonParts[] items = new ButtonParts[2];

        private KartPathFollower kart;
        private AslPowerupSignSource signs;
        private float boostCooldownTotal = 1.5f;
        private int shownPlace = -1;

        private UiTheme Th => UiTheme.Resolve(theme);
        private ItemVisuals Art => ItemVisuals.Resolve(visuals);
        private Color firstPlaceTop => Th.firstPlaceTop;
        private Color firstPlaceBottom => Th.firstPlaceBottom;
        private Color otherPlacesColor => Th.otherPlaces;
        private Color BoostColor => Th.boostButton;
        private Color ItemColor => Th.itemButton;
        private Color EmptyColor => Th.emptyButton;
        private Color CheckColor => Th.activeButton;

        private void Awake()
        {
            EnsureEventSystem();

            if (boost != null && boost.button != null) boost.button.onClick.AddListener(OnBoostClicked);
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null || items[i].button == null) continue;
                int slot = i; // captured per button
                items[i].button.onClick.AddListener(() => OnItemClicked(slot));
            }
        }

        private void Update()
        {
            FindLocalKart();

            var race = RaceManager.Instance;
            bool show = race != null && kart != null && !kart.IsFinished &&
                        (race.State == RaceState.Waiting || race.State == RaceState.Countdown || race.State == RaceState.Racing);
            SetVisible(show);
            if (!show) return;

            UpdateTopTexts(race);
            UpdateButtons();
        }

        // ---------- top texts ----------

        private void UpdateTopTexts(RaceManager race)
        {
            if (lapText != null) lapText.text = $"{Mathf.Max(1, kart.CurrentLap)}/{race.TotalLaps}"; // the lap icon next to it says what it is

            if (positionText != null)
            {
                // Hidden until a few seconds into the race, then fades in.
                float fade = race.State == RaceState.Racing
                    ? Mathf.Clamp01((race.RaceTime - showPositionAfterSeconds) / Mathf.Max(0.05f, positionFadeSeconds))
                    : 0f;
                positionText.canvasRenderer.SetAlpha(fade);

                var standings = RaceManager.GetStandings();
                int place = Mathf.Max(1, standings.IndexOf(kart) + 1);
                if (place != shownPlace)
                {
                    // Only touch the text when the place changes (saves work every frame).
                    shownPlace = place;
                    positionText.text = $"{place}<size=55%>{Suffix(place)}</size>";
                    if (place == 1)
                    {
                        positionText.enableVertexGradient = true;
                        positionText.colorGradient = new VertexGradient(firstPlaceTop, firstPlaceTop, firstPlaceBottom, firstPlaceBottom);
                        positionText.color = Color.white; // the gradient shows as is
                    }
                    else
                    {
                        positionText.enableVertexGradient = false;
                        positionText.color = otherPlacesColor;
                    }
                }
            }
        }

        private static string Suffix(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return "th";
            switch (n % 10)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
                default: return "th";
            }
        }

        // ---------- buttons ----------

        private void UpdateButtons()
        {
            if (signs == null) return;

            // BOOST
            bool boostActive = signs.IsSigning && signs.PendingAction == PowerupAction.Boost;
            float cooldown = signs.BoostCooldownLeft;
            if (cooldown <= 0.001f) boostCooldownTotal = 1.5f;
            boostCooldownTotal = Mathf.Max(boostCooldownTotal, cooldown);

            SetButton(boost, boostActive ? "CHECK" : "BOOST", boostActive ? CheckColor : BoostColor,
                      boostActive || signs.CanStartBoost, cooldown > 0.001f ? cooldown / boostCooldownTotal : 0f,
                      boostActive ? null : Art.boost);

            // ITEM 1 and ITEM 2
            for (int slot = 0; slot < items.Length; slot++)
            {
                ItemType item = kart.GetItem(slot);
                bool active = signs.IsSigning && signs.PendingAction == PowerupAction.Item && signs.PendingSlot == slot;
                string label = active ? "CHECK" : item == ItemType.None ? "" : ItemLabel(item); // empty slot: no text
                Color color = active ? CheckColor : item == ItemType.None ? EmptyColor : ItemColor;
                SetButton(items[slot], label, color, active || signs.CanStartItem(slot), 0f, active ? null : Art.For(item));
            }
        }

        private static string ItemLabel(ItemType item)
        {
            switch (item)
            {
                case ItemType.SuperBoost: return "SUPER\nBOOST";
                case ItemType.SlowOthers: return "SLOW\nOTHERS";
                default: return item.ToString();
            }
        }

        private static void SetButton(ButtonParts parts, string label, Color color, bool interactable, float ring01, Sprite sprite)
        {
            if (parts == null) return;
            // With a sprite the icon shows and the text hides; without one the text label stays.
            if (parts.icon != null)
            {
                bool hasIcon = sprite != null;
                if (parts.icon.sprite != sprite) parts.icon.sprite = sprite;
                if (parts.icon.enabled != hasIcon) parts.icon.enabled = hasIcon;
                if (parts.label != null && parts.label.enabled == hasIcon) parts.label.enabled = !hasIcon;
            }
            if (parts.label != null) parts.label.text = label;
            if (parts.face != null) parts.face.color = color;
            if (parts.button != null) parts.button.interactable = interactable;
            if (parts.cooldownRing != null) parts.cooldownRing.fillAmount = Mathf.Clamp01(ring01);
        }

        private void OnBoostClicked()
        {
            if (signs == null) return;
            bool active = signs.IsSigning && signs.PendingAction == PowerupAction.Boost;
            if (active) signs.CheckNow();
            else if (signs.CanStartBoost) signs.StartBoost();
        }

        private void OnItemClicked(int slot)
        {
            if (signs == null) return;
            bool active = signs.IsSigning && signs.PendingAction == PowerupAction.Item && signs.PendingSlot == slot;
            if (active) signs.CheckNow();
            else if (signs.CanStartItem(slot)) signs.StartItem(slot);
        }

        // ---------- helpers ----------

        private void FindLocalKart()
        {
            if (kart != null) return; // Unity's null check also covers a destroyed kart
            foreach (var k in KartPathFollower.All)
            {
                if (k.IsBot || !k.HasStateAuthority) continue;
                kart = k;
                signs = k.GetComponent<AslPowerupSignSource>();
                return;
            }
            signs = null;
        }

        private void SetVisible(bool visible)
        {
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        /// <summary>Buttons need an EventSystem in the scene to receive touches. Make one if there isn't.</summary>
        public static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
