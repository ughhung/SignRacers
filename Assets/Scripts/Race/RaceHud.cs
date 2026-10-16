using System.Collections.Generic;
using SignRacers.Core;
using SignRacers.Kart;
using SignRacers.UI;
using UnityEngine;

namespace SignRacers.Race
{
    /// <summary>
    /// Throwaway debug HUD (IMGUI) for the race, laid out for a phone held upright:
    /// lap, position and clock across the top, the waiting / countdown / GO messages, and
    /// a results panel with a RACE AGAIN button. Replace with real UI later.
    /// </summary>
    public class RaceHud : MonoBehaviour
    {
        private const float GoFlashSeconds = 0.8f;

        private GUIStyle huge, big, medium, small, row, button, left, right;
        private Vector2Int builtFor;

        private void OnGUI()
        {
            var race = RaceManager.Instance;
            if (race == null) return;

            KartPathFollower me = null;
            foreach (var k in KartPathFollower.All)
            {
                if (!k.IsBot && k.HasStateAuthority) { me = k; break; }
            }

            BuildStyles();
            float u = HudLayout.Unit;
            Rect s = HudLayout.Safe;
            float pad = 0.03f * u;
            var standings = RaceManager.GetStandings();
            RaceState state = race.State;

            // Top bar: lap on the left, position in the middle, clock on the right.
            if ((state == RaceState.Racing || state == RaceState.Finished) && me != null)
            {
                int place = standings.IndexOf(me) + 1;
                float y = s.y + 0.02f * u;
                float third = (s.width - pad * 2f) / 3f;
                GUI.Label(new Rect(s.x + pad, y, third, 0.08f * u), $"LAP {me.CurrentLap}/{race.TotalLaps}", left);
                GUI.Label(new Rect(s.x + pad + third, y, third, 0.08f * u), $"{Ordinal(place)} / {standings.Count}", medium);
                GUI.Label(new Rect(s.x + pad + third * 2f, y, third, 0.08f * u), FormatTime(race.RaceTime), right);
            }

            if (state == RaceState.Waiting)
            {
                string message = race.KartsPresent < race.PlayersNeeded
                    ? $"Waiting for players  ({race.KartsPresent}/{race.PlayersNeeded})"
                    : race.PlayersNeeded > 1
                        ? $"Getting cameras ready...  ({race.ReadyKarts}/{race.KartsPresent})"
                        : "Getting the camera ready...";
                GUI.Label(new Rect(s.x + pad, s.y + s.height * 0.40f, s.width - pad * 2f, 0.25f * u), message, medium);
            }
            else if (state == RaceState.Countdown)
            {
                GUI.Label(new Rect(s.x, s.y + s.height * 0.30f, s.width, 0.4f * u), race.CountdownNumber.ToString(), huge);
            }
            else if (state == RaceState.Racing && race.RaceTime < GoFlashSeconds)
            {
                GUI.color = Color.green;
                GUI.Label(new Rect(s.x, s.y + s.height * 0.30f, s.width, 0.4f * u), "GO!", huge);
                GUI.color = Color.white;
            }

            // Results panel once the local kart has finished.
            if (me != null && me.IsFinished) DrawResults(race, me, standings, s, u);
        }

        private void DrawResults(RaceManager race, KartPathFollower me, List<KartPathFollower> standings, Rect s, float u)
        {
            float panelH = Mathf.Min(s.height * 0.8f, 1.0f * u);
            var panel = new Rect(s.x + s.width * 0.05f, s.y + (s.height - panelH) / 2f, s.width * 0.9f, panelH);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            int place = standings.IndexOf(me) + 1;
            GUI.Label(new Rect(panel.x, panel.y + 0.02f * u, panel.width, 0.13f * u),
                race.State == RaceState.Finished ? "RACE OVER" : "FINISHED!", big);
            GUI.Label(new Rect(panel.x, panel.y + 0.15f * u, panel.width, 0.07f * u), $"You came {Ordinal(place)}", medium);

            // As many result rows as fit between the heading and the bottom button.
            float lineH = 0.07f * u;
            float rowsTop = panel.y + 0.24f * u;
            float rowsBottom = panel.yMax - 0.26f * u;
            int maxRows = Mathf.Max(1, Mathf.FloorToInt((rowsBottom - rowsTop) / lineH));

            float y = rowsTop;
            for (int i = 0; i < standings.Count && i < maxRows; i++)
            {
                var k = standings[i];
                string name = k == me ? "You" : k.IsBot ? "Bot" : "Player";
                string time = k.IsFinished ? FormatTime(k.FinishTime) : "racing...";
                GUI.Label(new Rect(panel.x + 0.06f * u, y, panel.width - 0.12f * u, lineH), $"{i + 1}.  {name}      {time}", row);
                y += lineH;
            }

            float buttonW = panel.width * 0.42f;
            float buttonH = 0.12f * u;
            float buttonY = panel.yMax - 0.16f * u;

            if (race.State != RaceState.Finished)
                GUI.Label(new Rect(panel.x, buttonY - 0.07f * u, panel.width, 0.07f * u), "waiting for the others to finish...", small);
            else if (!race.HasStateAuthority)
                GUI.Label(new Rect(panel.x, buttonY - 0.07f * u, panel.width, 0.07f * u), "waiting for the host to restart...", small);

            if (race.State == RaceState.Finished && race.HasStateAuthority)
            {
                if (GUI.Button(new Rect(panel.x + panel.width * 0.05f, buttonY, buttonW, buttonH), "RACE AGAIN", button)) race.Restart();
                if (GUI.Button(new Rect(panel.xMax - panel.width * 0.05f - buttonW, buttonY, buttonW, buttonH), "MENU", button)) LeaveToMenu();
            }
            else
            {
                if (GUI.Button(new Rect(panel.x + (panel.width - buttonW) / 2f, buttonY, buttonW, buttonH), "MENU", button)) LeaveToMenu();
            }
        }

        private static void LeaveToMenu()
        {
            var bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null) bootstrap.ReturnToMenu();
        }

        private static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        private static string FormatTime(float t)
        {
            int minutes = (int)(t / 60f);
            float seconds = t - minutes * 60f;
            return $"{minutes}:{seconds:00.00}";
        }

        private void BuildStyles()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (huge != null && builtFor == size) return; // rebuild if the screen size or orientation changes
            builtFor = size;
            float u = HudLayout.Unit;

            huge = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.28f * u), fontStyle = FontStyle.Bold };
            big = new GUIStyle(huge) { fontSize = Mathf.RoundToInt(0.12f * u) };
            medium = new GUIStyle(huge) { fontSize = Mathf.RoundToInt(0.05f * u), wordWrap = true };
            left = new GUIStyle(medium) { alignment = TextAnchor.MiddleLeft };
            right = new GUIStyle(medium) { alignment = TextAnchor.MiddleRight };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(0.032f * u) };
            row = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(0.05f * u) };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(0.055f * u), fontStyle = FontStyle.Bold };
        }
    }
}
