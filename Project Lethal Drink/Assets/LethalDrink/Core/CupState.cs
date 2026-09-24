namespace LethalDrink.Core
{
    // Authoritative content is never part of a public CupView.
    public sealed class CupState
    {
        public int CupId
        {
            get;
        }
        public int Position
        {
            get; internal set;
        }
        public bool IsUsed
        {
            get; internal set;
        }
        public bool IsPurified
        {
            get; internal set;
        }
        public bool? RevealedPoison
        {
            get; internal set;
        }
        internal bool IsPoisoned
        {
            get; set;
        }
        internal CupState(int id, int position, bool poisoned)
        {
            CupId = id;
            Position = position;
            IsPoisoned = poisoned;
        }
    }
}
