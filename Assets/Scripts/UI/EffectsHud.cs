using SignRacers.Kart;
using UnityEngine;

namespace SignRacers.UI
{
    /// <summary>
    /// Small colored chips above the power-up buttons that show what is affecting your kart
    /// right now: BOOST, SUPER BOOST, PENALTY, SLOWED. A chip is only visible while its effect
    /// is active. Lives on the Race HUD prefab.
    /// </summary>
    public class EffectsHud : MonoBehaviour
    {
        [SerializeField] private GameObject boostChip;
        [SerializeField] private GameObject superBoostChip;
        [SerializeField] private GameObject penaltyChip;
        [SerializeField] private GameObject slowedChip;

        private KartPathFollower kart;

        private void Update()
        {
            if (kart == null)
            {
                foreach (var k in KartPathFollower.All)
                {
                    if (k.IsBot || !k.HasStateAuthority) continue;
                    kart = k;
                    break;
                }
            }

            Set(boostChip, kart != null && kart.IsBoosted);
            Set(superBoostChip, kart != null && kart.IsSuperBoosted);
            Set(penaltyChip, kart != null && kart.IsPenalized);
            Set(slowedChip, kart != null && kart.IsSlowed);
        }

        private static void Set(GameObject chip, bool active)
        {
            if (chip != null && chip.activeSelf != active) chip.SetActive(active);
        }
    }
}
