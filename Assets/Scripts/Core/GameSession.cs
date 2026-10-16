using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SignRacers.Core
{
    public enum SessionMode { Solo, Multiplayer }

    /// <summary>
    /// What the player chose in the menu, carried into the race scene.
    /// Static on purpose: it must survive the scene change from menu to race.
    /// </summary>
    public static class GameSession
    {
        public const int SignsPerGame = 5;
        public const int MaxPlayers = 4;
        public const int RoomCodeLength = 5;

        // No 0/O or 1/I/L, so codes can be read aloud and typed without mix-ups.
        private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const string LastPicksKey = "SignRacers.LastSigns";

        public static SessionMode Mode = SessionMode.Solo;

        /// <summary>The signs for this race. Null when the race scene is played directly in the Editor, or in multiplayer until voting exists.</summary>
        public static string[] SelectedSigns;

        /// <summary>True once a race was launched from the menu (so the race scene obeys the menu's choices).</summary>
        public static bool StartedFromMenu;

        /// <summary>Multiplayer: the room's code. It is also the Photon session name.</summary>
        public static string RoomCode = "";

        /// <summary>Multiplayer: true if this player is creating the room, false if joining an existing one.</summary>
        public static bool IsCreatingRoom;

        /// <summary>A message for the menu to show after a failed join.</summary>
        public static string LastError = "";

        public static bool HasSelection => SelectedSigns != null && SelectedSigns.Length > 0;

        /// <summary>Goes up whenever SelectedSigns changes mid-session (for example after a room's vote), so listeners know to refresh.</summary>
        public static int SignsVersion;

        public static void SetSigns(string[] words)
        {
            SelectedSigns = words;
            SignsVersion++;
        }

        public static string ConsumeError()
        {
            string error = LastError;
            LastError = "";
            return error;
        }

        public static string GenerateRoomCode()
        {
            var code = new StringBuilder();
            for (int i = 0; i < RoomCodeLength; i++)
                code.Append(CodeAlphabet[Random.Range(0, CodeAlphabet.Length)]);
            return code.ToString();
        }

        /// <summary>Uppercases, drops characters a code can't contain, and trims to the code length.</summary>
        public static string CleanCode(string raw)
        {
            var cleaned = new StringBuilder();
            foreach (char c in (raw ?? "").ToUpperInvariant())
            {
                if (CodeAlphabet.IndexOf(c) >= 0 && cleaned.Length < RoomCodeLength) cleaned.Append(c);
            }
            return cleaned.ToString();
        }

        public static void SaveLastPicks(IEnumerable<string> words) =>
            PlayerPrefs.SetString(LastPicksKey, string.Join(",", words));

        public static string[] LoadLastPicks()
        {
            string saved = PlayerPrefs.GetString(LastPicksKey, "");
            return saved.Length == 0 ? new string[0] : saved.Split(',');
        }
    }
}
