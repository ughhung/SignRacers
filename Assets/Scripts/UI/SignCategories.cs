using System.Collections.Generic;
using UnityEngine;

namespace SignRacers.UI
{
    /// <summary>
    /// Which group each sign belongs to (Animals, Food, ...), read from Assets/Resources/SignCategories.txt
    /// (lines like "dad,People"). Used by the Solo picker's filter chips. A word that is not listed is "Other".
    /// </summary>
    public static class SignCategories
    {
        public const string Other = "Other";
        private static Dictionary<string, string> map;

        public static string Of(string word)
        {
            if (map == null) Load();
            return map.TryGetValue(word.Trim().ToLowerInvariant(), out var category) ? category : Other;
        }

        private static void Load()
        {
            map = new Dictionary<string, string>();
            var file = Resources.Load<TextAsset>("SignCategories");
            if (file == null) return;
            foreach (string raw in file.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int comma = line.IndexOf(',');
                if (comma <= 0) continue;
                map[line.Substring(0, comma).Trim().ToLowerInvariant()] = line.Substring(comma + 1).Trim();
            }
        }
    }
}
