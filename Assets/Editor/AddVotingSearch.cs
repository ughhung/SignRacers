#if UNITY_EDITOR
using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Adds a search box to the voting screen's word list, by editing the existing VotingCanvas prefab in place.
    /// Unlike "Build Voting And Review Prefab", this keeps your hand edits (such as the reference video's Autoplay and Loop).
    /// Sign Racers > Add Search To Voting Screen.
    /// </summary>
    public static class AddVotingSearch
    {
        private const string PrefabPath = "Assets/Prefabs/UI/VotingCanvas.prefab";

        [MenuItem("Sign Racers/Add Search To Voting Screen")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<VotingView>();
                var vote = root.transform.Find("SafeArea/VotePanel") as RectTransform;
                var scroll = vote != null ? vote.Find("WordScroll") as RectTransform : null;
                if (view == null || vote == null || scroll == null)
                {
                    Debug.LogError("Couldn't find SafeArea/VotePanel/WordScroll in " + PrefabPath + ". Use Build Voting And Review Prefab instead.");
                    return;
                }

                var so = new SerializedObject(view);
                if (so.FindProperty("searchField").objectReferenceValue != null)
                {
                    Debug.Log("The voting screen already has a search box.");
                    return;
                }

                UiSkin skin = VotingBuilder.EnsureSkin();
                Sprite panel = RaceHudBuilder.GetPanelSprite();
                var input = VotingBuilder.AddSearchBar(vote, scroll, skin, panel, out var noMatch);

                so.FindProperty("searchField").objectReferenceValue = input;
                so.FindProperty("noMatchText").objectReferenceValue = noMatch.gameObject;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Search box added to the voting screen.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
