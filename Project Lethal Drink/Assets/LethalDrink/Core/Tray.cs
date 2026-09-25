using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LethalDrink.Core
{
    public sealed class Tray
    {
        public int TrayId
        {
            get;
        }
        public int InitialPoisonCount
        {
            get;
        }
        public ReadOnlyCollection<CupState> Cups
        {
            get;
        }
        public int RemainingCount => Cups.Count(c => !c.IsUsed);
        internal Tray(int id, int poisons, List<CupState> cups)
        {
            TrayId = id;
            InitialPoisonCount = poisons;
            Cups = cups.AsReadOnly();
        }
    }
}
