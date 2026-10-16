using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SignRacers.Race
{
    /// <summary>
    /// Turns everyone's votes into the room's winning words.
    ///   1. Words with the most votes win first.
    ///   2. If a tie doesn't fit in the remaining slots (or nobody voted much), the slots are
    ///      filled from the tied words, preferring words chosen by players who have no winning
    ///      word yet, with a random choice among whatever is still equal.
    /// Only the room's owner runs this, and the result is shared, so every device gets the same words.
    /// </summary>
    public static class VoteTally
    {
        public static List<int> Pick(int wordCount, IReadOnlyList<PlayerVote> voters, int pickCount)
        {
            var votes = new int[wordCount];
            var voterWords = new List<HashSet<int>>();

            foreach (var voter in voters)
            {
                var set = new HashSet<int>();
                for (int i = 0; i < voter.ChoiceCount; i++)
                {
                    int word = voter.GetChoice(i);
                    if (word < 0 || word >= wordCount) continue;
                    votes[word]++;
                    set.Add(word);
                }
                voterWords.Add(set);
            }

            var chosen = new List<int>();
            var remaining = Enumerable.Range(0, wordCount).ToList();

            while (chosen.Count < pickCount && remaining.Count > 0)
            {
                int best = remaining.Max(w => votes[w]);
                var group = remaining.Where(w => votes[w] == best).ToList();
                int slots = pickCount - chosen.Count;

                if (group.Count <= slots)
                {
                    // The whole tied group fits.
                    chosen.AddRange(group);
                    foreach (int w in group) remaining.Remove(w);
                    continue;
                }

                // Not everyone in the tied group fits. Pick one at a time.
                while (chosen.Count < pickCount && group.Count > 0)
                {
                    int bestScore = -1;
                    var top = new List<int>();
                    foreach (int w in group)
                    {
                        // How many players have no winning word yet but voted for this one?
                        int score = voterWords.Count(set => set.Contains(w) && !set.Overlaps(chosen));
                        if (score > bestScore) { bestScore = score; top.Clear(); top.Add(w); }
                        else if (score == bestScore) top.Add(w);
                    }

                    int pick = top[Random.Range(0, top.Count)];
                    chosen.Add(pick);
                    group.Remove(pick);
                    remaining.Remove(pick);
                }
            }

            return chosen;
        }
    }
}
