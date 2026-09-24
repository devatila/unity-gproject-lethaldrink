using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LethalDrink.Core
{
    public sealed class PlayerState
    {
        private readonly List<int> inventory = new List<int>();
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
            get; private set;
        }
        public bool IsAlive => Lives > 0;
        public ReadOnlyCollection<int> Inventory
        {
            get;
        }
        public PlayerState(int playerId, string displayName, int startingLives = 3)
        {
            if (playerId < 1 || string.IsNullOrWhiteSpace(displayName) || startingLives < 1)
                throw new ArgumentException("Jogador inicial inválido.");
            PlayerId = playerId;
            DisplayName = displayName;
            Lives = startingLives;
            Inventory = inventory.AsReadOnly();
        }
        internal void SetLives(int lives)
        {
            Lives = Math.Max(0, lives);
        }
        internal void AddItem(int id) => inventory.Add(id);
        internal void RemoveItem(int id) => inventory.Remove(id);
    }
}
