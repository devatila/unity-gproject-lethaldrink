using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LethalDrink.Core
{
    public readonly struct TrayConfiguration
    {
        public int CupCount
        {
            get;
        }
        public int PoisonCount
        {
            get;
        }
        public TrayConfiguration(int cups, int poisons)
        {
            if (cups < 1 || poisons < 0 || poisons > cups)
                throw new ArgumentException("Composição inválida.");
            CupCount = cups;
            PoisonCount = poisons;
        }
    }
    // Immutable after construction: callers cannot change live rules behind the engine.
    public sealed class MatchConfig
    {
        public GameMode Mode
        {
            get;
        }
        public int PlayerCount
        {
            get;
        }
        public int StartingLives
        {
            get;
        }
        public int StartingPlayerId
        {
            get;
        }
        public int Seed
        {
            get;
        }
        public int InventoryCapacity
        {
            get;
        }
        public int ProactiveQuota
        {
            get;
        }
        public int MaxCollectiveDrinks
        {
            get;
        }
        public bool SkipOfferOriginatorOnKill
        {
            get;
        }
        public bool RestrictReturnInDuel
        {
            get;
        }
        public RefusalSuccession RefusalSuccession
        {
            get;
        }
        public SuddenDeathRule SuddenDeath
        {
            get;
        }
        public ReadOnlyCollection<TrayConfiguration> Trays
        {
            get;
        }
        public ReadOnlyCollection<ItemType> ItemPool
        {
            get;
        }
        public MatchConfig(GameMode mode, int playerCount, IEnumerable<TrayConfiguration> trays, int seed = 12345,
            int startingLives = 3, int startingPlayerId = 1, int inventoryCapacity = 3, int proactiveQuota = 1,
            int maxCollectiveDrinks = 3, bool skipOfferOriginatorOnKill = true, bool restrictReturnInDuel = true,
            RefusalSuccession refusalSuccession = RefusalSuccession.Undecided, SuddenDeathRule suddenDeath = SuddenDeathRule.Disabled,
            IEnumerable<ItemType> itemPool = null)
        {
            if (!Enum.IsDefined(typeof(GameMode), mode) || playerCount < 2 || playerCount > 4 || startingLives < 1 || startingPlayerId < 1 || startingPlayerId > playerCount || inventoryCapacity < 0 || proactiveQuota < 0 || maxCollectiveDrinks < 1)
                throw new ArgumentException("Configuração inválida.");
            if (!Enum.IsDefined(typeof(RefusalSuccession), refusalSuccession) || !Enum.IsDefined(typeof(SuddenDeathRule), suddenDeath))
                throw new ArgumentException("Política inválida.");
            var list = new List<TrayConfiguration>(trays ?? throw new ArgumentNullException(nameof(trays)));
            if (list.Count == 0)
                throw new ArgumentException("Configure ao menos uma Bandeja.");
            foreach (var t in list)
            if (t.CupCount < 1 || t.PoisonCount < 0 || t.PoisonCount > t.CupCount || (mode == GameMode.Collective && t.CupCount < playerCount))
                throw new ArgumentException("Bandeja incompatível.");
            var pool = new List<ItemType>();
            var requested = itemPool ?? (IEnumerable<ItemType>)Enum.GetValues(typeof(ItemType));
            foreach (var type in requested)
            {
                if (!Enum.IsDefined(typeof(ItemType), type))
                    throw new ArgumentException("Item inválido.");
                if (ItemDefinition.Get(type).Allows(mode) && !pool.Contains(type))
                    pool.Add(type);
            }
            Mode = mode;
            PlayerCount = playerCount;
            StartingLives = startingLives;
            StartingPlayerId = startingPlayerId;
            Seed = seed;
            InventoryCapacity = inventoryCapacity;
            ProactiveQuota = proactiveQuota;
            MaxCollectiveDrinks = maxCollectiveDrinks;
            SkipOfferOriginatorOnKill = skipOfferOriginatorOnKill;
            RestrictReturnInDuel = restrictReturnInDuel;
            RefusalSuccession = refusalSuccession;
            SuddenDeath = suddenDeath;
            Trays = list.AsReadOnly();
            ItemPool = pool.AsReadOnly();
        }
    }
}
