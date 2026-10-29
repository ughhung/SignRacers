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
            public TMP_Text label;
        }

        private static readonly Color BoostColor = new Color(1.00f, 0.60f, 0.15f);
        private static readonly Color ItemColor = new Color(0.25f, 0.55f, 0.95f);
        private static readonly Color EmptyColor = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color CheckColor = new Color(1.00f, 0.90f, 0.25f);
        private static readonly Color GoldColor = new Color(1.00f, 0.84f, 0.25f);

        [Header("Whole HUD")]
        [SerializeField] private CanvasGroup group;

        [Header("Top left and top right")]
        [SerializeField] private TMP_Text lapText;
        [SerializeField] private TMP_Text positionText;

        [Header("Buttons")]
        [SerializeField] private ButtonParts boost;
        [SerializeField] private ButtonParts[] items = new ButtonParts[2];

        private KartPathFollower kart;
        private AslPowerupSignSource signs;
        private float boostCooldownTotal = 1.5f;

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
            if (lapText != null) lapText.text = $"LAP {Mathf.Max(1, kart.CurrentLap)}/{race.TotalLaps}";

            if (positionText != null)
            {
                var standings = RaceManager.GetStandings();
                int place = Mathf.Max(1, standings.IndexOf(kart) + 1);
                positionText.text = $"{place}<size=55%>{Suffix(place)}</size>";
                positionText.color = place == 1 ? GoldColor : Color.white;
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
                      boostActive || signs.CanStartBoost, cooldown > 0.001f ? cooldown / boostCooldownTotal : 0f);

            // ITEM 1 and ITEM 2
            for (int slot = 0; slot < items.Length; slot++)
            {
                ItemType item = kart.GetItem(slot);
                bool active = signs.IsSigning && signs.PendingAction == PowerupAction.Item && signs.PendingSlot == slot;
                string label = active ? "CHECK" : item == ItemType.None ? "EMPTY" : ItemLabel(item);
                Color color = active ? CheckColor : item == ItemType.None ? EmptyColor : ItemColor;
                SetButton(items[slot], label, color, active || signs.CanStartItem(slot), 0f);
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

        private static void SetButton(ButtonParts parts, string label, Color color, bool interactable, float ring01)
        {
            if (parts == null) return;
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
