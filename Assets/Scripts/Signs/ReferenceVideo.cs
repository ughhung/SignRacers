using System;
using Gtk.Unity;
using UnityEngine;

namespace SignRacers.Signs
{
    /// <summary>
    /// Plays the toolkit's reference clip for a sign ("how do I sign this word?"). Like AslRecognizer, this is the ONLY
    /// game script that touches the toolkit's video code, so a toolkit change means editing this one file.
    ///
    /// Put it on the same object as the toolkit's VideoView (the RawImage that shows the clip), or drag the VideoView in.
    /// The clips come from the "SLRGTk references.popsign" package. A clip is named "gallery:word", for example "pop:owl".
    /// </summary>
    public class ReferenceVideo : MonoBehaviour
    {
        [SerializeField] private VideoView view;

        [Tooltip("The gallery the clips belong to. The toolkit calls the PopSign gallery 'pop'.")]
        [SerializeField] private string gallery = "pop";

        /// <summary>Why the last Play() didn't start, in plain words. Empty after a successful start.</summary>
        public string LastProblem { get; private set; } = "";

        private void Awake()
        {
            if (view == null) view = GetComponent<VideoView>();
        }

        /// <summary>Start the clip for this word. Returns false (and sets LastProblem) if it can't be played.</summary>
        public bool Play(string word)
        {
            LastProblem = "";
            if (view == null)
            {
                LastProblem = "No video view is set up.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(word))
            {
                LastProblem = "No word to show.";
                return false;
            }

            try
            {
                view.Play(gallery + ":" + word.Trim().ToLowerInvariant());
                view.Play(); // make sure it is running even if the VideoView's Autoplay isn't set
                return true;
            }
            catch (Exception ex)
            {
                // Typical causes: the references package isn't installed, the gallery has no clip for this word,
                // or this platform can't play video. The toolkit's message says which.
                Debug.LogWarning("[ReferenceVideo] Could not play '" + word + "': " + ex.Message);
                LastProblem = "No demo video for this word yet.";
                return false;
            }
        }

        /// <summary>Stop the clip and free it. Safe to call when nothing is playing.</summary>
        public void Stop()
        {
            if (view == null) return;
            try { view.Stop(); }
            catch (Exception ex) { Debug.LogWarning("[ReferenceVideo] Stop failed: " + ex.Message); }
        }
    }
}
