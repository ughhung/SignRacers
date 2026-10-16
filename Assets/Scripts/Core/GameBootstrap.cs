using Fusion;
using SignRacers.Kart;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace SignRacers.Core
{
    /// <summary>
    /// Starts Fusion and spawns a kart for each player that joins.
    /// Put this on an empty GameObject in the scene (it adds the NetworkRunner itself).
    ///   Single = offline practice (no Photon connection, no CCU used)
    ///   Shared = online: every device owns its own kart, Photon Cloud relays everything
    /// When launched from the menu, the menu's choice decides the mode. An online room uses
    /// its room code as the Photon session name. Creating a room may make a new session;
    /// joining one may not, so a mistyped code fails instead of making an empty room.
    /// </summary>
    public class GameBootstrap : SimulationBehaviour, IPlayerJoined
    {
        // Side-by-side lanes so karts don't drive through each other.
        private static readonly float[] PlayerLanes = { 0f, 3f, -3f, 6f };
        private static readonly float[] BotLanes = { 3f, -3f, 6f };

        [SerializeField] private NetworkPrefabRef kartPrefab;
        [Tooltip("A prefab with NetworkObject + RaceManager. Spawned once per race.")]
        [SerializeField] private NetworkPrefabRef raceManagerPrefab;
        [SerializeField] private GameMode gameMode = GameMode.Single;
        [Tooltip("Used only when this scene is played directly, not from the menu.")]
        [SerializeField] private string sessionName = "SignRacersDev";
        [Tooltip("Exact name of the menu scene. It must be in the Build Profiles scene list.")]
        [SerializeField] private string menuSceneName = "MainMenu";

        [Header("Offline testing (Single mode only)")]
        [FormerlySerializedAs("dummyKartPrefab")]
        [Tooltip("Optional. A kart prefab with Is Bot ticked. Spawned beside you in Single mode only.")]
        [SerializeField] private NetworkPrefabRef botKartPrefab;
        [SerializeField, Range(0, 3)] private int botCount = 1;

        private NetworkRunner runner;
        private bool raceManagerSpawned;
        private bool leaving;

        private async void Start()
        {
            bool fromMenu = GameSession.StartedFromMenu;
            bool onlineFromMenu = fromMenu && GameSession.Mode == SessionMode.Multiplayer;

            // When launched from the menu, the menu's choice wins over the inspector.
            if (fromMenu) gameMode = onlineFromMenu ? GameMode.Shared : GameMode.Single;

            runner = gameObject.AddComponent<NetworkRunner>();
            runner.ProvideInput = false; // karts auto-steer, so there's no player input to send

            var args = new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = onlineFromMenu ? GameSession.RoomCode : sessionName,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            };

            if (onlineFromMenu)
            {
                args.PlayerCount = GameSession.MaxPlayers;
                args.IsVisible = false; // private: reachable only by its code
                args.IsOpen = true;
                args.EnableClientSessionCreation = GameSession.IsCreatingRoom;
            }

            var result = await runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[Bootstrap] Fusion failed to start: {result.ShutdownReason}");
                if (onlineFromMenu)
                {
                    GameSession.LastError = Describe(result.ShutdownReason);
                    ReturnToMenu();
                }
            }
            else
            {
                Debug.Log($"[Bootstrap] Fusion started in {gameMode} mode.");
            }
        }

        /// <summary>Leave the session cleanly and go back to the main menu.</summary>
        public async void ReturnToMenu()
        {
            if (leaving) return;
            leaving = true;

            try
            {
                if (runner != null) await runner.Shutdown(false); // false: keep this object, the scene load removes it
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Bootstrap] Shutdown reported: {ex.Message}");
            }

            SceneManager.LoadScene(menuSceneName);
        }

        private static string Describe(ShutdownReason reason)
        {
            switch (reason.ToString())
            {
                case "GameNotFound": return "No open room with that code. Check the code, or the race may have already started.";
                case "GameIsFull": return "That room is full.";
                case "GameClosed": return "That room is closed.";
                default: return $"Couldn't connect ({reason}). Check your internet connection and try again.";
            }
        }

        /// <summary>One RaceManager per race: the owner in Single mode, the master client in Shared mode.</summary>
        private void TrySpawnRaceManager()
        {
            if (raceManagerSpawned || !raceManagerPrefab.IsValid) return;

            bool shouldSpawn = Runner.GameMode == GameMode.Shared
                ? Runner.IsSharedModeMasterClient
                : Runner.IsServer;
            if (!shouldSpawn) return;

            raceManagerSpawned = true;
            Runner.Spawn(raceManagerPrefab, Vector3.zero, Quaternion.identity);
            Debug.Log("[Bootstrap] Spawned the race manager.");
        }

        public void PlayerJoined(PlayerRef player)
        {
            Debug.Log($"[Bootstrap] Player joined: {player}");
            TrySpawnRaceManager();

            // Who spawns depends on topology: only the host/server in Single/Host modes,
            // but each player spawns their own kart in Shared mode.
            bool shouldSpawn = Runner.GameMode == GameMode.Shared
                ? player == Runner.LocalPlayer
                : Runner.IsServer;

            if (!shouldSpawn) return;

            // PlayerIds start at 1, so player 1 gets lane 0, player 2 lane 3, and so on.
            int index = ((player.PlayerId - 1) % PlayerLanes.Length + PlayerLanes.Length) % PlayerLanes.Length;
            float lane = PlayerLanes[index];

            Runner.Spawn(kartPrefab, Vector3.zero, Quaternion.identity, player,
                (r, obj) => obj.GetComponent<KartPathFollower>().SetLaneOffset(lane));
            Debug.Log($"[Bootstrap] Spawned kart for {player} in lane {lane}.");

            if (Runner.GameMode == GameMode.Single && botKartPrefab.IsValid)
            {
                int count = Mathf.Min(botCount, BotLanes.Length);
                for (int i = 0; i < count; i++)
                {
                    float botLane = BotLanes[i];
                    Runner.Spawn(botKartPrefab, Vector3.zero, Quaternion.identity, null,
                        (r, obj) => obj.GetComponent<KartPathFollower>().SetLaneOffset(botLane));
                }
                Debug.Log($"[Bootstrap] Spawned {count} bot/dummy kart(s).");
            }
        }
    }
}
