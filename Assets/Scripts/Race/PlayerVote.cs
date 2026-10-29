using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace SignRacers.Race
{
    /// <summary>
    /// One player's part of the room's word vote, networked so every device can see everyone's
    /// choices live. It lives on the player's kart prefab, so (as with the kart) only the owner
    /// ever changes it. It holds:
    ///   Choices     - up to 5 word numbers (positions in the shared word list)
    ///   Done        - the player finished voting
    ///   Ready       - the player is ready to race after the review minute
    /// </summary>
    public class PlayerVote : NetworkBehaviour
    {
        public const int MaxChoices = 5;

        /// <summary>Every player's vote on this device.</summary>
        public static readonly List<PlayerVote> All = new List<PlayerVote>();

        /// <summary>The vote that belongs to this device's player.</summary>
        public static PlayerVote Local { get; private set; }

        [Networked, Capacity(5)] private NetworkArray<int> Choices => default;
        [Networked] public int ChoiceCount { get; set; }
        [Networked] public NetworkBool Done { get; set; }
        [Networked] public NetworkBool Ready { get; set; }

        public bool IsDone => Done;
        public bool IsReady => Ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            Local = null;
        }

        public override void Spawned()
        {
            var kart = GetComponent<SignRacers.Kart.KartPathFollower>();
            if (kart != null && kart.IsBot) return; // bots do not vote, and nobody waits for them
            All.Add(this);
            if (HasStateAuthority) Local = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);
            if (Local == this) Local = null;
        }

        public int GetChoice(int index) => index >= 0 && index < ChoiceCount ? Choices[index] : -1;

        public bool Has(int wordIndex) => IndexOf(wordIndex) >= 0;

        private int IndexOf(int wordIndex)
        {
            for (int i = 0; i < ChoiceCount; i++)
                if (Choices[i] == wordIndex) return i;
            return -1;
        }

        /// <summary>Pick or un-pick a word. At most 5, and locked once the player is Done.</summary>
        public void Toggle(int wordIndex)
        {
            if (!HasStateAuthority || Done) return;

            int at = IndexOf(wordIndex);
            if (at >= 0)
            {
                for (int i = at; i < ChoiceCount - 1; i++) Choices.Set(i, Choices[i + 1]);
                ChoiceCount--;
                Choices.Set(ChoiceCount, 0);
            }
            else if (ChoiceCount < MaxChoices)
            {
                Choices.Set(ChoiceCount, wordIndex);
                ChoiceCount++;
            }
        }

        public void SubmitDone()
        {
            if (HasStateAuthority) Done = true;
        }

        public void SetReady()
        {
            if (HasStateAuthority) Ready = true;
        }
    }
}
