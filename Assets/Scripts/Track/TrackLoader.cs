using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignRacers.Track
{
    /// <summary>
    /// Loads a track scene on top of the Game scene (which stays the active scene). Switching track unloads the old one first. Every device does this itself.
    /// Leaving to the menu loads MainMenu normally, which removes the track scene too.
    /// </summary>
    public static class TrackLoader
    {
        /// <summary>Name of the track scene that is loaded right now (null when none).</summary>
        public static string LoadedName { get; private set; }

        /// <summary>Goes up by one every time a track finishes loading. Lets other scripts notice a new track.</summary>
        public static int Version { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { LoadedName = null; Version = 0; }

        /// <summary>Loads the track. Returns true when a TrackPath is ready to use.</summary>
        public static async Task<bool> Load(string sceneName)
        {
            if (LoadedName == sceneName && SceneManager.GetSceneByName(sceneName).isLoaded) return true;

            // Remove the previous track first.
            if (!string.IsNullOrEmpty(LoadedName))
            {
                Scene old = SceneManager.GetSceneByName(LoadedName);
                LoadedName = null;
                if (old.isLoaded)
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(old);
                    while (unload != null && !unload.isDone) await Task.Yield();
                }
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                AsyncOperation op;
                try { op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Track] Could not load '{sceneName}': {ex.Message}");
                    return false;
                }
                if (op == null)
                {
                    Debug.LogError($"[Track] Scene '{sceneName}' is not in the Build Profiles scene list.");
                    return false;
                }
                while (!op.isDone) await Task.Yield();
                scene = SceneManager.GetSceneByName(sceneName);
            }

            // The Game scene stays the active scene: Fusion spawns the karts and the race manager into the
            // active scene, so they must not live in a track scene that gets unloaded when the track changes.

            var path = Object.FindFirstObjectByType<TrackPath>();
            if (path == null)
            {
                Debug.LogError($"[Track] '{sceneName}' has no TrackPath.");
                return false;
            }
            path.EnsureBuilt();
            if (!path.IsValid) return false;

            LoadedName = sceneName;
            Version++;
            return true;
        }
    }
}
