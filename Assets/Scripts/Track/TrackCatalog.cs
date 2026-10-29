namespace SignRacers.Track
{
    /// <summary>
    /// The list of tracks a room can race on. To add a track: make its scene (copy Track_Hand), add the scene to
    /// the Build Profiles scene list, and add one line here. The host picks by position in this list, so
    /// only ever add to the end, never reorder (everyone's build must agree on the order).
    /// </summary>
    public static class TrackCatalog
    {
        private static readonly (string scene, string name)[] Tracks =
        {
            ("Track_Loop", "Loop"),
            ("Track_Hand", "Hand"),
        };

        public static int Count => Tracks.Length;

        public static string SceneName(int id) => Tracks[Wrap(id)].scene;
        public static string DisplayName(int id) => Tracks[Wrap(id)].name;
        public static int Wrap(int id) => ((id % Tracks.Length) + Tracks.Length) % Tracks.Length;
    }
}
