namespace LethalDrink.Core
{
    public enum GameEventKind
    {
        CupSelected, SelectionCancelled, CupOffered, ReactionEnded, CupDrunk, LivesChanged, PlayerEliminated, TurnStarted, TrayEnded, TrayStarted, ItemUsed, CupPurified, CupsSwapped, ReservationChanged, ReadyChanged, RoundLocked, RoundResolved, MatchEnded, DebugChanged, RoundDeadlineExpired
    }
    // Immutable historical facts; contains no inspection results or unrevealed poison.
    public sealed class GameEvent
    {
        public GameEventKind Kind
        {
            get;
        }
        public int? PlayerId
        {
            get;
        }
        public int? TargetId
        {
            get;
        }
        public int? CupId
        {
            get;
        }
        public int? Value
        {
            get;
        }
        public int TrayId
        {
            get;
        }
        public long Revision
        {
            get;
        }
        internal GameEvent(GameEventKind kind, int? player, int? target, int? cup, int? value, int tray, long revision)
        {
            Kind = kind;
            PlayerId = player;
            TargetId = target;
            CupId = cup;
            Value = value;
            TrayId = tray;
            Revision = revision;
        }
    }
}
