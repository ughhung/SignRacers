using SignRacers.Events;
using UnityEngine;

namespace SignRacers.UI
{
    /// <summary>
    /// The art for the power-up buttons. Create it with "Sign Racers / Create Item Visuals" (saved as
    /// Assets/Resources/ItemVisuals.asset) and drop sprites into the slots. Any slot left empty keeps
    /// showing the text label, so you can add icons one at a time.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemVisuals", menuName = "Sign Racers/Item Visuals")]
    public class ItemVisuals : ScriptableObject
    {
        [Tooltip("The BOOST button.")]
        public Sprite boost;
        [Tooltip("Shown on an item button with no item in it.")]
        public Sprite empty;
        public Sprite superBoost;
        public Sprite slowOthers;

        private static ItemVisuals fallback;

        /// <summary>The assigned asset, else Resources/ItemVisuals, else an empty set (text labels only).</summary>
        public static ItemVisuals Resolve(ItemVisuals assigned)
        {
            if (assigned != null) return assigned;
            if (fallback == null)
            {
                fallback = Resources.Load<ItemVisuals>("ItemVisuals");
                if (fallback == null) fallback = CreateInstance<ItemVisuals>();
            }
            return fallback;
        }

        /// <summary>The sprite for an item, or null if none was assigned.</summary>
        public Sprite For(ItemType item)
        {
            switch (item)
            {
                case ItemType.SuperBoost: return superBoost;
                case ItemType.SlowOthers: return slowOthers;
                default: return empty;
            }
        }
    }
}
