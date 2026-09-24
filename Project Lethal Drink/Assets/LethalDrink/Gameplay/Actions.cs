namespace LethalDrink.Gameplay
{
    // Intentions only. All validation and mutation enter through MatchEngine.
    public interface IGameAction
    {
        int ActorPlayerId
        {
            get;
        }
    }
    public sealed class SelectCupAction : IGameAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public int CupId
        {
            get;
        }
        public SelectCupAction(int actor, int cup)
        {
            ActorPlayerId = actor;
            CupId = cup;
        }
    }
    public sealed class CancelCupSelectionAction : IGameAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public CancelCupSelectionAction(int actor)
        {
            ActorPlayerId = actor;
        }
    }
    public sealed class DrinkCupAction : IGameAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public DrinkCupAction(int actor)
        {
            ActorPlayerId = actor;
        }
    }
    public sealed class OfferCupAction : IGameAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public int TargetPlayerId
        {
            get;
        }
        public OfferCupAction(int actor, int target)
        {
            ActorPlayerId = actor;
            TargetPlayerId = target;
        }
    }
    public interface IItemAction : IGameAction
    {
        int ItemId
        {
            get;
        }
    }
    public sealed class UseInspectionItemAction : IItemAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public int ItemId
        {
            get;
        }
        public int CupId
        {
            get;
        }
        public UseInspectionItemAction(int actor, int item, int cup)
        {
            ActorPlayerId = actor;
            ItemId = item;
            CupId = cup;
        }
    }
    public sealed class UsePurifierAction : IItemAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public int ItemId
        {
            get;
        }
        public int CupId
        {
            get;
        }
        public UsePurifierAction(int actor, int item, int cup)
        {
            ActorPlayerId = actor;
            ItemId = item;
            CupId = cup;
        }
    }
    public sealed class UseRefusalAction : IItemAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public int ItemId
        {
            get;
        }
        public UseRefusalAction(int actor, int item)
        {
            ActorPlayerId = actor;
            ItemId = item;
        }
    }
    public sealed class UseSwapItemAction : IItemAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public int ItemId
        {
            get;
        }
        public int FirstCupId
        {
            get;
        }
        public int SecondCupId
        {
            get;
        }
        public UseSwapItemAction(int actor, int item, int first, int second)
        {
            ActorPlayerId = actor;
            ItemId = item;
            FirstCupId = first;
            SecondCupId = second;
        }
    }
    public sealed class UseDoubleDrinkAction : IItemAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public int ItemId
        {
            get;
        }
        public int TargetPlayerId
        {
            get;
        }
        public UseDoubleDrinkAction(int actor, int item, int target)
        {
            ActorPlayerId = actor;
            ItemId = item;
            TargetPlayerId = target;
        }
    }
    public sealed class ReserveCupAction : IGameAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public int CupId
        {
            get;
        }
        public ReserveCupAction(int actor, int cup)
        {
            ActorPlayerId = actor;
            CupId = cup;
        }
    }
    public sealed class CancelReservationAction : IGameAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public int CupId
        {
            get;
        }
        public CancelReservationAction(int actor, int cup)
        {
            ActorPlayerId = actor;
            CupId = cup;
        }
    }
    public sealed class SetReadyAction : IGameAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public bool Ready
        {
            get;
        }
        public SetReadyAction(int actor, bool ready = true)
        {
            ActorPlayerId = actor;
            Ready = ready;
        }
    }
    public sealed class ResolveCollectiveRoundAction : IGameAction
    {
        public int ActorPlayerId
        {
            get;
        }
        public ResolveCollectiveRoundAction(int actor)
        {
            ActorPlayerId = actor;
        }
    }
}
