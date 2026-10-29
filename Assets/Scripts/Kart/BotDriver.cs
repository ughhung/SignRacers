using SignRacers.Race;
using UnityEngine;

namespace SignRacers.Kart
{
    /// <summary>
    /// Makes a bot kart race: it drives a little slower or faster than the base speed (its skill), takes
    /// a boost every so often, uses the items it picks up, and speeds up or eases off to stay close to the player
    /// (so the race stays tight). Added to bot karts by GameBootstrap when Solo starts, so the bot prefab
    /// needs nothing extra. Bots only exist offline, where this device owns every kart.
    /// </summary>
    [RequireComponent(typeof(KartPathFollower))]
    public class BotDriver : MonoBehaviour
    {
        [Tooltip("Speed multiplier before catching up or easing off. 1 = same as an unboosted player.")]
        [SerializeField] private float skill = 1f;
        [Tooltip("How much the speed may change to stay near the player. 0.12 = up to 12% faster or slower.")]
        [SerializeField] private float rubberBand = 0.12f;
        [Tooltip("Distance from the player (in units) at which the catch-up effect is at full strength.")]
        [SerializeField] private float rubberBandDistance = 150f;
        [SerializeField] private Vector2 secondsBetweenBoosts = new Vector2(7f, 14f);
        [SerializeField] private Vector2 secondsBeforeUsingItem = new Vector2(2f, 6f);

        private KartPathFollower kart;
        private KartPathFollower player;
        private float nextBoostAt = -1f;
        private float nextItemAt = -1f;

        private void Awake() => kart = GetComponent<KartPathFollower>();

        /// <summary>Called when the bot is created: skill about 1 is even with the player, lower is easier.</summary>
        public void Setup(float botSkill, string botName)
        {
            skill = botSkill;
            if (kart == null) kart = GetComponent<KartPathFollower>();
            kart.BotName = botName;
        }

        private void Update()
        {
            if (kart == null || kart.Object == null || !kart.Object.IsValid || !kart.HasStateAuthority) return;

            var race = RaceManager.Instance;
            if (race == null || race.State != RaceState.Racing)
            {
                // Not racing (waiting, countdown, results): reset, so each race schedules fresh.
                nextBoostAt = nextItemAt = -1f;
                kart.SpeedScale = skill;
                return;
            }

            float now = Time.time;

            // Keep the race close.
            if (player == null) player = FindPlayer();
            float band = 0f;
            if (player != null)
            {
                float gap = player.Distance - kart.Distance; // positive = the player is ahead
                band = Mathf.Clamp(gap / rubberBandDistance, -1f, 1f) * rubberBand;
            }
            kart.SpeedScale = skill * (1f + band);

            // An occasional boost, like a player getting a sign right.
            if (nextBoostAt < 0f) nextBoostAt = now + Random.Range(secondsBetweenBoosts.x, secondsBetweenBoosts.y);
            else if (now >= nextBoostAt)
            {
                kart.BotBoost();
                nextBoostAt = now + Random.Range(secondsBetweenBoosts.x, secondsBetweenBoosts.y);
            }

            // Use items a few seconds after picking them up.
            if (kart.ItemCount > 0)
            {
                if (nextItemAt < 0f) nextItemAt = now + Random.Range(secondsBeforeUsingItem.x, secondsBeforeUsingItem.y);
                else if (now >= nextItemAt)
                {
                    kart.BotUseItem();
                    nextItemAt = -1f;
                }
            }
            else nextItemAt = -1f;
        }

        private static KartPathFollower FindPlayer()
        {
            foreach (var k in KartPathFollower.All)
                if (!k.IsBot && k.HasStateAuthority) return k;
            return null;
        }
    }
}
