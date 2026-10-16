using System.Collections.Generic;
using SignRacers.Core;
using SignRacers.Kart;
using SignRacers.UI;
using UnityEngine;

namespace SignRacers.Race
{
    /// <summary>
    /// Throwaway debug HUD (IMGUI) for the online lobby: the room code with a copy button,
    /// who has joined, and START (host only) and LEAVE buttons. Shown only while the race is
    /// in the Lobby state, and drawn on top of the other HUDs. Replace with real UI later.
    /// </summary>
    public class LobbyHud : MonoBehaviour
    {
        private GUIStyle code, medium, small, row, button;
        private Vector2Int builtFor;
        private float copiedAt = -100f;

        private void OnGUI()
        {
            var race = RaceManager.Instance;
            if (race == null || race.State != RaceState.Lobby) return;

            GUI.depth = -100; // in front of the other HUDs
            BuildStyles();
            float u = HudLayout.Unit;
            Rect s = HudLayout.Safe;
            float pad = 0.04f * u;

            // Backdrop over the whole screen.
            GUI.color = new Color(0.05f, 0.06f, 0.12f, 0.93f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = s.y + 0.06f * u;
            GUI.Label(new Rect(s.x, y, s.width, 0.07f * u), "ROOM CODE", small);
            y += 0.08f * u;
            GUI.Label(new Rect(s.x, y, s.width, 0.24f * u), GameSession.RoomCode, code);
            y += 0.26f * u;

            bool justCopied = Time.time - copiedAt < 2f;
            if (GUI.Button(new Rect(s.x + s.width * 0.25f, y, s.width * 0.5f, 0.11f * u), justCopied ? "COPIED!" : "COPY CODE", button))
            {
                GUIUtility.systemCopyBuffer = GameSession.RoomCode;
                copiedAt = Time.time;
            }
            y += 0.16f * u;

            // Players who have joined.
            var karts = new List<KartPathFollower>(KartPathFollower.All);
            karts.Sort((a, b) => PlayerId(a).CompareTo(PlayerId(b)));

            GUI.Label(new Rect(s.x, y, s.width, 0.08f * u), $"Players  ({karts.Count}/{GameSession.MaxPlayers})", medium);
            y += 0.1f * u;

            foreach (var kart in karts)
            {
                string label = $"Player {PlayerId(kart)}";
                if (kart.HasStateAuthority) label += race.HasStateAuthority ? "  (you, host)" : "  (you)";
                GUI.Label(new Rect(s.x + pad, y, s.width - pad * 2f, 0.07f * u), label, row);
                y += 0.075f * u;
            }

            // Bottom buttons.
            float buttonH = 0.13f * u;
            float buttonY = s.yMax - buttonH - pad;

            if (race.HasStateAuthority)
            {
                float leaveW = s.width * 0.38f;
                float startW = s.width - leaveW - pad * 3f;
                if (GUI.Button(new Rect(s.x + pad, buttonY, leaveW, buttonH), "LEAVE", button)) Leave();
                if (GUI.Button(new Rect(s.x + pad * 2f + leaveW, buttonY, startW, buttonH), "START", button)) race.StartFromLobby();
            }
            else
            {
                GUI.Label(new Rect(s.x, buttonY - 0.09f * u, s.width, 0.08f * u), "waiting for the host to start...", small);
                if (GUI.Button(new Rect(s.x + s.width * 0.25f, buttonY, s.width * 0.5f, buttonH), "LEAVE", button)) Leave();
            }
        }

        private static int PlayerId(KartPathFollower kart) =>
            kart.Object != null && kart.Object.IsValid ? kart.Object.InputAuthority.PlayerId : 0;

        private static void Leave()
        {
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null) bootstrap.ReturnToMenu();
        }

        private void BuildStyles()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (code != null && builtFor == size) return;
            builtFor = size;
            float u = HudLayout.Unit;

            code = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.2f * u), fontStyle = FontStyle.Bold };
            medium = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.05f * u), fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.035f * u) };
            row = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(0.05f * u) };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(0.05f * u), fontStyle = FontStyle.Bold };
        }
    }
}
