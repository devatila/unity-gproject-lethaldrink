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
        public int? StartingPlayerId
        {
            get;
        }
        public int? Seed
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
        // Zero uses the alive-player count. A configured minimum can only increase it.
        public int CollectiveMinimumCups { get; }
        public bool CollectiveTimerEnabled { get; }
        public double CollectiveInitialSeconds { get; }
        public double CollectiveReductionSeconds { get; }
        public double CollectiveMinimumSeconds { get; }
        public MatchConfig(GameMode mode, int playerCount, IEnumerable<TrayConfiguration> trays, int? seed = null,
            int startingLives = 3, int? startingPlayerId = null, int inventoryCapacity = 3, int proactiveQuota = 1,
            int maxCollectiveDrinks = 3, bool skipOfferOriginatorOnKill = true, bool restrictReturnInDuel = true,
            SuddenDeathRule suddenDeath = SuddenDeathRule.Disabled,
            IEnumerable<ItemType> itemPool = null, int collectiveMinimumCups = 0,
            double collectiveInitialSeconds = 60, double collectiveReductionSeconds = 5, double collectiveMinimumSeconds = 20,
            bool collectiveTimerEnabled = false)
        {
            if (!Enum.IsDefined(typeof(GameMode), mode) || playerCount < 2 || playerCount > 4 || startingLives < 1 || (startingPlayerId.HasValue && (startingPlayerId < 1 || startingPlayerId > playerCount)) || inventoryCapacity < 0 || proactiveQuota < 0 || maxCollectiveDrinks < 1)
                throw new ArgumentException("Configuração inválida.");
            if (!Enum.IsDefined(typeof(SuddenDeathRule), suddenDeath))
                throw new ArgumentException("Política inválida.");
            if (collectiveMinimumCups < 0 || !Finite(collectiveInitialSeconds) || !Finite(collectiveReductionSeconds) || !Finite(collectiveMinimumSeconds)
                || collectiveMinimumSeconds <= 0 || collectiveInitialSeconds < collectiveMinimumSeconds || collectiveReductionSeconds < 0)
                throw new ArgumentException("Configuração de tempo/mínimo Collective inválida.");
            var list = new List<TrayConfiguration>(trays ?? throw new ArgumentNullException(nameof(trays)));
            if (list.Count == 0)
                throw new ArgumentException("Configure ao menos uma Bandeja.");
            foreach (var t in list)
            if (t.CupCount < 1 || t.PoisonCount < 0 || t.PoisonCount > t.CupCount || (mode == GameMode.Collective && t.CupCount < Math.Max(playerCount, collectiveMinimumCups)))
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
            SuddenDeath = suddenDeath;
            Trays = list.AsReadOnly();
            ItemPool = pool.AsReadOnly();
            CollectiveMinimumCups = collectiveMinimumCups;
            CollectiveTimerEnabled = collectiveTimerEnabled;
            CollectiveInitialSeconds = collectiveInitialSeconds;
            CollectiveReductionSeconds = collectiveReductionSeconds;
            CollectiveMinimumSeconds = collectiveMinimumSeconds;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public double CollectiveDurationForRound(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            return Math.Max(CollectiveMinimumSeconds, CollectiveInitialSeconds - (round - 1) * CollectiveReductionSeconds);
        }
    }
}
