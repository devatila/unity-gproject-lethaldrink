using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LethalDrink.Core;

namespace LethalDrink.Gameplay
{
    public sealed class ItemView
    {
        public int ItemId
        {
            get;
        }
        public ItemType Type
        {
            get;
        }
        internal ItemView(ItemState i)
        {
            ItemId = i.ItemId;
            Type = i.Definition.Type;
        }
    }
    public sealed class PlayerPublicView
    {
        public int PlayerId
        {
            get;
        }
        public string DisplayName
        {
            get;
        }
        public int Lives
        {
            get;
        }
        public ReadOnlyCollection<ItemView> Inventory
        {
            get;
        }
        internal PlayerPublicView(PlayerState p, MatchEngine engine)
        {
            PlayerId = p.PlayerId;
            DisplayName = p.DisplayName;
            Lives = p.Lives;
            Inventory = p.Inventory.Select(id => new ItemView(engine.GetItem(id))).ToList().AsReadOnly();
        }
    }
    public sealed class CupView
    {
        public int CupId
        {
            get;
        }
        public int Position
        {
            get;
        }
        public bool IsUsed
        {
            get;
        }
        public bool IsPurified
        {
            get;
        }
        public bool? RevealedPoison
        {
            get;
        }
        internal CupView(CupState cup)
        {
            CupId = cup.CupId;
            Position = cup.Position;
            IsUsed = cup.IsUsed;
            IsPurified = cup.IsPurified;
            RevealedPoison = cup.RevealedPoison;
        }
    }
    // Detached snapshots: no mutable authoritative objects and no seed/content are returned.
    public sealed class MatchView
    {
        public long Revision
        {
            get;
        }
        public GameMode Mode
        {
            get;
        }
        public MatchStatus Status
        {
            get;
        }
        public MatchOutcome Outcome
        {
            get;
        }
        public int? WinnerId
        {
            get;
        }
        public int TrayId
        {
            get;
        }
        public int InitialPoisonCount
        {
            get;
        }
        public int RemainingCups
        {
            get;
        }
        public ReadOnlyCollection<PlayerPublicView> Players
        {
            get;
        }
        public ReadOnlyCollection<CupView> Cups
        {
            get;
        }
        public int? CurrentPlayerId
        {
            get;
        }
        public int? SelectedCupId
        {
            get;
        }
        public int? CannotOfferToPlayerId
        {
            get;
        }
        public int ProactiveItemsUsed
        {
            get;
        }
        public bool ReactiveItemUsed
        {
            get;
        }
        public PendingDrink PendingDrink
        {
            get;
        }
        public ClassicPhase? ClassicPhase
        {
            get;
        }
        public CollectivePhase? CollectivePhase
        {
            get;
        }
        public int? RoundNumber
        {
            get;
        }
        public int? SelectionNumber { get; }
        public double? CollectiveRemainingSeconds { get; }
        public IReadOnlyDictionary<int, int> Reservations
        {
            get;
        }
        public IReadOnlyDictionary<int, int> RequiredDrinks
        {
            get;
        }
        public ReadOnlyCollection<int> ReadyPlayers
        {
            get;
        }
        internal MatchView(MatchEngine engine)
        {
            Revision = engine.Revision;
            Mode = engine.Config.Mode;
            Status = engine.Status;
            Outcome = engine.Outcome;
            WinnerId = engine.WinnerId;
            TrayId = engine.Tray.TrayId;
            InitialPoisonCount = engine.Tray.InitialPoisonCount;
            RemainingCups = engine.Tray.RemainingCount;
            Players = engine.Players.Select(p => new PlayerPublicView(p, engine)).ToList().AsReadOnly();
            Cups = engine.Tray.Cups.Select(c => new CupView(c)).ToList().AsReadOnly();
            CurrentPlayerId = engine.Classic?.CurrentPlayerId ?? engine.Alternative?.CurrentPlayerId;
            SelectedCupId = engine.Classic?.SelectedCupId ?? engine.Alternative?.SelectedCupId;
            CannotOfferToPlayerId = engine.Classic?.CannotOfferToPlayerId;
            ProactiveItemsUsed = engine.Classic?.ProactiveItemsUsed ?? 0;
            ReactiveItemUsed = engine.Classic?.ReactiveItemUsed ?? false;
            PendingDrink = engine.Classic?.Pending;
            ClassicPhase = engine.Classic?.Phase;
            CollectivePhase = engine.Collective?.Phase;
            RoundNumber = engine.Collective?.RoundNumber;
            SelectionNumber = engine.Collective?.SelectionNumber;
            CollectiveRemainingSeconds = engine.Collective?.RemainingSeconds;
            var reservations = new Dictionary<int, int>();
            var required = new Dictionary<int, int>();
            var ready = new List<int>();
            if (engine.Collective != null)
            {
                foreach (var pair in engine.Collective.Reservations)
                    reservations.Add(pair.Key, pair.Value);
                foreach (var p in engine.Players.Where(p => p.IsAlive))
                {
                    required.Add(p.PlayerId, engine.Collective.RequiredDrinkCount(p.PlayerId));
                    if (engine.Collective.IsReady(p.PlayerId))
                        ready.Add(p.PlayerId);
                }
            }
            Reservations = new ReadOnlyDictionary<int, int>(reservations);
            RequiredDrinks = new ReadOnlyDictionary<int, int>(required);
            ReadyPlayers = ready.AsReadOnly();
        }
    }
    public sealed class PlayerView
    {
        public MatchView Public
        {
            get;
        }
        public IReadOnlyDictionary<int, bool> KnownCupContents
        {
            get;
        }
        internal PlayerView(MatchView publicView, IDictionary<int, bool> knowledge)
        {
            Public = publicView;
            KnownCupContents = new ReadOnlyDictionary<int, bool>(new Dictionary<int, bool>(knowledge));
        }
    }
}
