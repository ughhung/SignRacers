using UnityEngine;

namespace SignRacers.Core
{
    /// <summary>
    /// The player's display name. Names are never typed: they are "colour + animal" from fixed lists (for example "Blue Fox"),
    /// so nothing offensive can ever appear in a public room. The player taps to roll a new one; it is saved on the device.
    /// Every name is 12 characters or fewer, so it always fits the networked name field (16).
    /// </summary>
    public static class PlayerNames
    {
        private const string SaveKey = "SignRacers.PlayerName";

        private static readonly string[] Colours =
            { "Red", "Blue", "Green", "Pink", "Gold", "Teal", "Purple", "Orange", "Silver", "Coral", "Mint", "Sunny" };

        private static readonly string[] Animals =
            { "Fox", "Owl", "Cat", "Dog", "Bear", "Frog", "Duck", "Wolf", "Seal", "Hawk", "Bat", "Panda", "Tiger", "Koala", "Otter" };

        private static string current;

        /// <summary>The saved name. Makes (and saves) one the first time.</summary>
        public static string Get()
        {
            if (!string.IsNullOrEmpty(current)) return current;
            current = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(current)) Roll();
            return current;
        }

        /// <summary>Picks a new random name and saves it.</summary>
        public static string Roll()
        {
            string old = current;
            string name;
            do
            {
                name = Colours[Random.Range(0, Colours.Length)] + " " + Animals[Random.Range(0, Animals.Length)];
            } while (name == old);

            current = name;
            PlayerPrefs.SetString(SaveKey, name);
            PlayerPrefs.Save();
            return name;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => current = null;
    }
}
