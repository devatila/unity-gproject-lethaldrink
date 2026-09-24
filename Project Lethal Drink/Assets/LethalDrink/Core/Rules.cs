using System;

namespace LethalDrink.Core
{
    public enum GameMode
    {
        Classic, Collective, Alternative
    }
    [Flags]
    public enum GameModeAvailability
    {
        None = 0, Classic = 1, Collective = 2, Alternative = 4
    }
    [Flags]
    public enum ItemUsageTiming
    {
        None = 0, Proactive = 1, Reactive = 2
    }
    public enum ItemTargetType
    {
        Cup, TwoCups, PendingCup, Player
    }
    public enum ItemCost
    {
        Free, EndsInitiative
    }
    public enum ItemType
    {
        Inspection, Purifier, Refusal, Swap, DoubleDrink
    }
    public enum MatchStatus
    {
        Running, Ended
    }
    public enum MatchOutcome
    {
        None, Winner, Draw
    }
    public enum ClassicPhase
    {
        FreeDecision, CupSelected, Reaction, Ended
    }
    public enum CollectivePhase
    {
        Selection, AwaitingTrayDecision, Ended
    }
    public enum RefusalSuccession
    {
        Undecided, ResolveAsOriginatorDrink
    }
    public enum SuddenDeathRule
    {
        Disabled, FatalPoison, DoubleDamage
    }
    public enum ErrorCode
    {
        None, InvalidAction, WrongMode, WrongPlayer, WrongPhase, InvalidTarget, CupUnavailable, ItemUnavailable, ReactionNotAllowed, MatchEnded, QuotaSpent, CapacityExceeded, NotReady, StaleRevision, Busy, DesignPending
    }
}
