using System.Collections.Generic;
using UnityEngine;

namespace SignRacers.Core
{
    /// <summary>
    /// The list of signs a player can choose from. Read from Assets/Resources/signs.txt,
    /// one word per line (blank lines and lines starting with # are ignored). Words must be
    /// spelled exactly as the recognizer's gallery spells them.
    /// </summary>
    public static class SignLibrary
    {
        private static readonly string[] Fallback = { "yellow", "duck", "clown" };

        public static List<string> Load()
        {
            var words = new List<string>();
            var file = Resources.Load<TextAsset>("signs");

            if (file == null)
            {
                Debug.LogError("[SignLibrary] Assets/Resources/signs.txt not found, using a tiny fallback list.");
                words.AddRange(Fallback);
                return words;
            }

            foreach (string line in file.text.Split('\n'))
            {
                string word = line.Trim().ToLowerInvariant();
                if (word.Length == 0 || word.StartsWith("#") || words.Contains(word)) continue;
                words.Add(word);
            }

            words.Sort();
            return words;
        }

        /// <summary>How a word is shown on screen: "yellow" becomes "Yellow".</summary>
        public static string Display(string word) =>
            string.IsNullOrEmpty(word) ? word : char.ToUpperInvariant(word[0]) + word.Substring(1);
    }
}
