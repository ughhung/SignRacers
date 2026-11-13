using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using SignRacers.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.UI
{
    /// <summary>
    /// The "Browse public games" screen. Put it on the browse PANEL: while the panel is shown it connects to Photon's
    /// room list (no room is joined, so no player slot is used) and keeps a list of open, public, host-made rooms.
    /// Tapping a row raises JoinRequested with that room's name (its code). MenuController does the actual join.
    /// </summary>
    public class PublicGameBrowser : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private RectTransform listContent;
        [Tooltip("A hidden button that is copied once per room.")]
        [SerializeField] private Button rowTemplate;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button refreshButton;

        /// <summary>Raised with the room name (its code) when a row is tapped.</summary>
        public event Action<string> JoinRequested;

        private NetworkRunner runner;
        private bool connecting;
        private readonly List<GameObject> rows = new List<GameObject>();

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (refreshButton != null) refreshButton.onClick.AddListener(() => _ = Reconnect());
        }

        private void OnEnable() => _ = Connect();
        private void OnDisable() => _ = StopAsync();

        private async Task Connect()
        {
            if (runner != null || connecting) return;
            connecting = true;
            SetStatus("Looking for games...");
            ClearRows();

            var newRunner = gameObject.AddComponent<NetworkRunner>();
            newRunner.ProvideInput = false;
            newRunner.AddCallbacks(this);
            runner = newRunner;

            StartGameResult result;
            try { result = await newRunner.JoinSessionLobby(SessionLobby.Shared); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Browser] Lobby join failed: {ex.Message}");
                result = null;
            }
            connecting = false;

            if (this == null || runner != newRunner) return; // closed meanwhile
            if (result == null || !result.Ok)
                SetStatus("Couldn't reach the server. Check your connection and tap Refresh.");
        }

        private async Task Reconnect()
        {
            await StopAsync();
            await Connect();
        }

        /// <summary>Disconnects from the room list. MenuController awaits this before loading the race scene.</summary>
        public async Task StopAsync()
        {
            var old = runner;
            runner = null;
            if (old == null) return;
            try { await old.Shutdown(); }
            catch (Exception ex) { Debug.LogWarning($"[Browser] Shutdown reported: {ex.Message}"); }
            if (old != null) Destroy(old);
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        private void ClearRows()
        {
            foreach (var row in rows) if (row != null) Destroy(row);
            rows.Clear();
        }

        public void OnSessionListUpdated(NetworkRunner source, List<SessionInfo> sessionList)
        {
            if (source != runner) return;
            ClearRows();

            int shown = 0;
            foreach (var session in sessionList)
            {
                if (!session.IsVisible || !session.IsOpen) continue;
                if (session.PlayerCount >= session.MaxPlayers) continue;
                if (!session.Properties.TryGetValue(GameSession.KindKey, out var kind) ||
                    (string)kind.PropertyValue != GameSession.KindHosted) continue;

                var row = Instantiate(rowTemplate, listContent);
                row.gameObject.SetActive(true);
                row.name = "Room_" + session.Name;
                var label = row.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = $"Room {session.Name}      {session.PlayerCount}/{session.MaxPlayers}";
                string roomName = session.Name;
                row.onClick.AddListener(() => JoinRequested?.Invoke(roomName));
                rows.Add(row.gameObject);
                shown++;
            }

            SetStatus(shown == 0 ? "No public games right now.\nTap Refresh, or create your own." : "");
        }

        // ---- the rest of Fusion's callback interface: not needed here ----
        public void OnPlayerJoined(NetworkRunner r, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner r, PlayerRef player) { }
        public void OnInput(NetworkRunner r, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner r, ShutdownReason reason) { }
        public void OnConnectedToServer(NetworkRunner r) { }
        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner r, NetAddress address, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner r, HostMigrationToken token) { }
        public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner r, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner r) { }
        public void OnSceneLoadStart(NetworkRunner r) { }
        public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    }
}
