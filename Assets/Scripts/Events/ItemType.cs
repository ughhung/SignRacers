namespace SignRacers.Events
{
    /// <summary>
    /// The item list. Values are stored as ints in the kart's networked inventory,
    /// so don't reorder existing entries once you start saving anything.
    /// </summary>
    public enum ItemType
    {
        None = 0,
        SuperBoost = 1, // much bigger boost than a normal sign boost
        SlowOthers = 2, // slows every other kart for a few seconds
    }
}
