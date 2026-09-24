using System.Collections.Generic;

namespace LethalDrink.Core
{
    public sealed class ItemDefinition
    {
        public ItemType Type
        {
            get;
        }
        public GameModeAvailability AllowedModes
        {
            get;
        }
        public ItemUsageTiming UsageTiming
        {
            get;
        }
        public ItemTargetType TargetType
        {
            get;
        }
        public ItemCost Cost
        {
            get;
        }
        public bool Consumable => true;
        private ItemDefinition(ItemType type, GameModeAvailability modes, ItemUsageTiming timing, ItemTargetType target)
        {
            Type = type;
            AllowedModes = modes;
            UsageTiming = timing;
            TargetType = target;
            Cost = ItemCost.Free;
        }
        private static readonly Dictionary<ItemType, ItemDefinition> definitions = new Dictionary<ItemType, ItemDefinition>
        {
            { ItemType.Inspection, new ItemDefinition(ItemType.Inspection, GameModeAvailability.Classic | GameModeAvailability.Collective, ItemUsageTiming.Proactive, ItemTargetType.Cup) },
            { ItemType.Purifier, new ItemDefinition(ItemType.Purifier, GameModeAvailability.Classic | GameModeAvailability.Collective, ItemUsageTiming.Proactive | ItemUsageTiming.Reactive, ItemTargetType.Cup) },
            { ItemType.Refusal, new ItemDefinition(ItemType.Refusal, GameModeAvailability.Classic, ItemUsageTiming.Reactive, ItemTargetType.PendingCup) },
            { ItemType.Swap, new ItemDefinition(ItemType.Swap, GameModeAvailability.Classic | GameModeAvailability.Collective, ItemUsageTiming.Proactive, ItemTargetType.TwoCups) },
            { ItemType.DoubleDrink, new ItemDefinition(ItemType.DoubleDrink, GameModeAvailability.Collective, ItemUsageTiming.Proactive, ItemTargetType.Player) }
        };
        public static ItemDefinition Get(ItemType type) => definitions[type];
        public bool Allows(GameMode mode) => (AllowedModes & (GameModeAvailability)(1 << (int)mode)) != 0;
    }
    public sealed class ItemState
    {
        public int ItemId
        {
            get;
        }
        public ItemDefinition Definition
        {
            get;
        }
        public bool IsUsed
        {
            get; internal set;
        }
        internal ItemState(int id, ItemType type)
        {
            ItemId = id;
            Definition = ItemDefinition.Get(type);
        }
    }
}
