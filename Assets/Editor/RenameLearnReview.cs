#if UNITY_EDITOR
using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Renames the practice-card buttons on the VotingCanvas prefab: WATCH becomes LEARN, TEST becomes REVIEW
    /// (and the hint text on each card). Edits the prefab in place, so your other hand edits stay.
    /// Sign Racers > Rename Watch/Test To Learn/Review.
    /// </summary>
    public static class RenameLearnReview
    {
        private const string PrefabPath = "Assets/Prefabs/UI/VotingCanvas.prefab";

        [MenuItem("Sign Racers/Rename Watch-Test To Learn-Review")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                int changed = 0;
                foreach (var card in root.GetComponentsInChildren<PracticeCard>(true))
                {
                    foreach (var tr in card.GetComponentsInChildren<Transform>(true))
                    {
                        if (tr.name == "WatchButton") { tr.name = "LearnButton"; changed++; }
                        else if (tr.name == "TestButton") { tr.name = "ReviewButton"; changed++; }
                    }
                    foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
                    {
                        string t = text.text;
                        string n = t;
                        if (t == "WATCH") n = "LEARN";
                        else if (t == "TEST") n = "REVIEW";
                        else if (t.Contains("Tap WATCH") || t.Contains("TEST to try")) n = "Tap LEARN to see the sign\nor REVIEW to try it";
                        if (n != t) { text.text = n; changed++; }
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Renamed " + changed + " labels (WATCH > LEARN, TEST > REVIEW).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
