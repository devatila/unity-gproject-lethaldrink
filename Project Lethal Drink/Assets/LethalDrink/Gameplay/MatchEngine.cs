using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LethalDrink.Core;

namespace LethalDrink.Gameplay
{
    public sealed class MatchEngine
    {
        private readonly Dictionary<int, PlayerState> players = new Dictionary<int, PlayerState>();
        private readonly Dictionary<int, ItemState> items = new Dictionary<int, ItemState>();
        private readonly Dictionary<int, Dictionary<int, bool>> knowledge = new Dictionary<int, Dictionary<int, bool>>();
        private readonly Random random;
        private readonly List<GameEvent> pendingEvents = new List<GameEvent>();
        private readonly List<Exception> notificationErrors = new List<Exception>();
        private bool busy;
        private int nextCupId = 1, nextItemId = 1, nextTrayId = 1;
        public int? StartingPlayerId { get; }
        public MatchConfig Config
        {
            get;
        }
        public MatchStatus Status { get; private set; } = MatchStatus.Running;
        public MatchOutcome Outcome
        {
            get; private set;
        }
        public int? WinnerId
        {
            get; private set;
        }
        public long Revision
        {
            get; private set;
        }
        public Tray Tray
        {
            get; private set;
        }
        public ReadOnlyCollection<PlayerState> Players
        {
            get;
        }
        public ReadOnlyCollection<Exception> NotificationErrors => notificationErrors.AsReadOnly();
        public ClassicTurnEngine Classic
        {
            get;
        }
        public CollectiveTurnEngine Collective
        {
            get;
        }
        public AlternativeTurnEngine Alternative
        {
            get;
        }
        public event Action<GameEvent> EventOccurred;
        public event Action<IGameAction, ActionResult> ActionResolved;

        public MatchEngine(MatchConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            random = new Random(config.Seed ?? Guid.NewGuid().GetHashCode());
            for (int id = 1; id <= config.PlayerCount; id++)
            {
                players.Add(id, new PlayerState(id, "Player " + id, config.StartingLives));
                knowledge.Add(id, new Dictionary<int, bool>());
            }
            Players = players.Values.ToList().AsReadOnly();
            StartingPlayerId = config.Mode == GameMode.Collective
                ? (int?)null
                : config.StartingPlayerId ?? Players[random.Next(Players.Count)].PlayerId;
            CreateTray();
            pendingEvents.Clear();
            switch (config.Mode)
            {
                case GameMode.Classic:
                    Classic = new ClassicTurnEngine(this);
                    break;
                case GameMode.Collective:
                    Collective = new CollectiveTurnEngine(this);
                    break;
                case GameMode.Alternative:
                    Alternative = new AlternativeTurnEngine(this);
                    break;
            }
        }
        public PlayerState GetPlayer(int id) => players[id];
        public bool IsPlayerAlive(int id) => players.TryGetValue(id, out var p) && p.IsAlive;
        public int AliveCount => Players.Count(p => p.IsAlive);
        public ItemState GetItem(int id) => items[id];
        internal CupState Cup(int id) => Tray.Cups.FirstOrDefault(c => c.CupId == id);
        internal bool Available(int id) => Cup(id) != null && !Cup(id).IsUsed;
        internal int NextRandomIndex(int count) => random.Next(count);
        // Called by the authoritative session clock, never by a player action.
        public ActionResult AdvanceTime(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                return Fail(ErrorCode.InvalidAction, "Tempo decorrido inválido.");
            if (busy) return Fail(ErrorCode.Busy, "Operação em andamento.");
            if (Status == MatchStatus.Ended) return Fail(ErrorCode.MatchEnded, "Partida encerrada.");
            if (Collective == null) return Fail(ErrorCode.WrongMode, "Este modo não possui relógio coletivo.");
            // Countdown alone does not invalidate an otherwise current gameplay revision.
            if (!Collective.Elapse(elapsedSeconds)) return ActionResult.Ok();
            busy = true;
            pendingEvents.Clear();
            notificationErrors.Clear();
            try
            {
                int expiredTrayId = Tray.TrayId;
                Emit(GameEventKind.RoundDeadlineExpired, value: Collective.RoundNumber);
                do
                {
                    Collective.CompleteSelectionAutomatically();
                    Collective.ResolveSelection();
                }
                while (Status == MatchStatus.Running && Tray.TrayId == expiredTrayId);
                // New tray gets its full duration; a delayed frame never skips a newly presented tray.
                Revision++;
                Publish();
                return ActionResult.Ok();
            }
            finally { pendingEvents.Clear(); busy = false; }
        }
        // Trusted host operation, not an action accepted from a player/client.
        // Useful for debugging only the allocation step without resolving the drinks.
        public ActionResult CompleteCollectiveSelection()
        {
            if (Collective == null) return Fail(ErrorCode.WrongMode, "Somente Collective possui seleção coletiva.");
            if (busy) return Fail(ErrorCode.Busy, "Operação em andamento.");
            if (Status == MatchStatus.Ended) return Fail(ErrorCode.MatchEnded, "Partida encerrada.");
            if (Collective.AllReady) return ActionResult.Ok();
            busy = true;
            pendingEvents.Clear();
            notificationErrors.Clear();
            try
            {
                Collective.CompleteSelectionAutomatically();
                Revision++;
                Publish();
                return ActionResult.Ok();
            }
            finally { pendingEvents.Clear(); busy = false; }
        }
        public int? GetNextAlivePlayerId(int from, int? avoid = null)
        {
            int start = Players.ToList().FindIndex(p => p.PlayerId == from);
            if (start < 0)
                return null;
            int? fallback = null;
            for (int offset = 1; offset <= Players.Count; offset++)
            {
                var p = Players[(start + offset) % Players.Count];
                if (!p.IsAlive)
                    continue;
                if (p.PlayerId != avoid)
                    return p.PlayerId;
                fallback = p.PlayerId;
            }
            return fallback;
        }
        public ActionResult CanExecute(IGameAction action)
        {
            if (busy)
                return Fail(ErrorCode.Busy, "Uma operação/notificação está em andamento.");
            if (Status == MatchStatus.Ended)
                return Fail(ErrorCode.MatchEnded, "Partida encerrada.");
            if (action == null)
                return Fail(ErrorCode.InvalidAction, "Ação nula.");
            if (!IsPlayerAlive(action.ActorPlayerId))
                return Fail(ErrorCode.WrongPlayer, "Jogador inexistente ou eliminado.");
            if (Classic != null)
                return Classic.Validate(action);
            if (Collective != null)
                return Collective.Validate(action);
            return Alternative.Validate(action);
        }
        public ActionResult ExecuteAction(IGameAction action, long? expectedRevision = null)
        {
            if (expectedRevision.HasValue && expectedRevision.Value != Revision)
                return Fail(ErrorCode.StaleRevision, "O estado mudou; atualize a decisão.");
            var result = CanExecute(action);
            if (!result.Success)
                return result;
            busy = true;
            pendingEvents.Clear();
            notificationErrors.Clear();
            try
            {
                if (Classic != null)
                    Classic.Apply(action);
                else if (Collective != null)
                    Collective.Apply(action);
                else
                    Alternative.Apply(action);
                Revision++;
                Publish();
                Notify(ActionResolved, h => h(action, result));
                return result;
            }
            finally { pendingEvents.Clear(); busy = false; }
        }
        internal static ActionResult Fail(ErrorCode code, string message) => ActionResult.Fail(code, message);
        internal void Emit(GameEventKind kind, int? player = null, int? target = null, int? cup = null, int? value = null)
            => pendingEvents.Add(new GameEvent(kind, player, target, cup, value, Tray.TrayId, Revision + 1));
        private void Publish()
        {
            foreach (var e in pendingEvents)
                Notify(EventOccurred, h => h(e));
        }
        private void Notify<T>(T listeners, Action<T> call) where T : Delegate
        {
            if (listeners == null)
                return;
            foreach (var listener in listeners.GetInvocationList())
                try
                {
                    call((T)listener);
                }
                catch (Exception ex) { notificationErrors.Add(ex); }
        }
        internal void CreateTray()
        {
            var spec = Config.Trays[random.Next(Config.Trays.Count)];
            var poison = new bool[spec.CupCount];
            for (int i = 0; i < spec.PoisonCount; i++)
                poison[i] = true;
            for (int i = poison.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                bool tmp = poison[i];
                poison[i] = poison[j];
                poison[j] = tmp;
            }
            var cups = new List<CupState>();
            for (int i = 0; i < poison.Length; i++)
                cups.Add(new CupState(nextCupId++, i, poison[i]));
            Tray = new Tray(nextTrayId++, spec.PoisonCount, cups);
            foreach (var k in knowledge.Values)
                k.Clear();
            Emit(GameEventKind.TrayStarted, value: spec.CupCount);
        }
        internal void RenewEmptyTray()
        {
            RenewTrayIfInsufficient(1);
        }
        internal void RenewTrayIfInsufficient(int minimumCups)
        {
            if (Status == MatchStatus.Running && Tray.RemainingCount < minimumCups)
            {
                Emit(GameEventKind.TrayEnded);
                CreateTray();
            }
        }
        internal int PoisonDamage(int aliveBeforeResolution)
        {
            if (aliveBeforeResolution != 2)
                return 1;
            return Config.SuddenDeath == SuddenDeathRule.FatalPoison ? int.MaxValue : Config.SuddenDeath == SuddenDeathRule.DoubleDamage ? 2 : 1;
        }
        // All cup outcomes/damage are computed before any life changes or victory evaluation.
        internal bool ResolveDrinks(IEnumerable<KeyValuePair<int, int>> drinks)
        {
            var outcomes = drinks.Select(d => new { Player = d.Key, Cup = Cup(d.Value) }).ToList();
            int damage = PoisonDamage(AliveCount);
            var totals = new Dictionary<int, long>();
            foreach (var d in outcomes)
            {
                d.Cup.IsUsed = true;
                d.Cup.RevealedPoison = d.Cup.IsPoisoned;
                if (d.Cup.IsPoisoned)
                {
                    if (!totals.ContainsKey(d.Player))
                        totals[d.Player] = 0;
                    totals[d.Player] += damage;
                }
                Emit(GameEventKind.CupDrunk, d.Player, cup: d.Cup.CupId, value: d.Cup.IsPoisoned ? 1 : 0);
            }
            var eliminated = new List<int>();
            foreach (var pair in totals)
            {
                var p = players[pair.Key];
                p.SetLives((int)Math.Max(0L, p.Lives - pair.Value));
                Emit(GameEventKind.LivesChanged, p.PlayerId, value: p.Lives);
                if (!p.IsAlive)
                    eliminated.Add(p.PlayerId);
            }
            foreach (int id in eliminated)
                Emit(GameEventKind.PlayerEliminated, id);
            EvaluateMatch();
            return outcomes.Any(d => d.Cup.IsPoisoned);
        }
        internal void EvaluateMatch()
        {
            var alive = Players.Where(p => p.IsAlive).ToList();
            if (alive.Count > 1 || Status == MatchStatus.Ended)
                return;
            Status = MatchStatus.Ended;
            WinnerId = alive.Count == 1 ? (int?)alive[0].PlayerId : null;
            Outcome = WinnerId.HasValue ? MatchOutcome.Winner : MatchOutcome.Draw;
            Emit(GameEventKind.MatchEnded, WinnerId, value: (int)Outcome);
        }
        internal ActionResult ValidateItem(IItemAction action, ItemType type, ItemUsageTiming timing, bool quotaAvailable)
        {
            if (!items.TryGetValue(action.ItemId, out var item) || item.IsUsed || item.Definition.Type != type || !players[action.ActorPlayerId].Inventory.Contains(action.ItemId))
                return Fail(ErrorCode.ItemUnavailable, "Item indisponível ou não pertence ao jogador.");
            if (!item.Definition.Allows(Config.Mode) || !Config.ItemPool.Contains(type))
                return Fail(ErrorCode.WrongMode, "Item fora do pool deste modo.");
            if ((item.Definition.UsageTiming & timing) == 0)
                return Fail(ErrorCode.ReactionNotAllowed, "Timing não permitido.");
            if (timing == ItemUsageTiming.Proactive && !quotaAvailable)
                return Fail(ErrorCode.QuotaSpent, "Cota proativa esgotada.");
            return ActionResult.Ok();
        }
        internal void ConsumeItem(IItemAction action)
        {
            items[action.ItemId].IsUsed = true;
            players[action.ActorPlayerId].RemoveItem(action.ItemId);
            Emit(GameEventKind.ItemUsed, action.ActorPlayerId, value: action.ItemId);
        }
        internal void Inspect(int player, int cup)
        {
            knowledge[player][cup] = Cup(cup).IsPoisoned;
        }
        internal void Purify(int cup)
        {
            var c = Cup(cup);
            // The public label is the INITIAL count and never changes within this tray.
            c.IsPoisoned = false;
            c.IsPurified = true;
            foreach (var k in knowledge.Values)
            if (k.ContainsKey(cup))
                k[cup] = false;
            Emit(GameEventKind.CupPurified, cup: cup);
        }
        internal void Swap(int first, int second)
        {
            int position = Cup(first).Position;
            Cup(first).Position = Cup(second).Position;
            Cup(second).Position = position;
            Emit(GameEventKind.CupsSwapped, cup: first, value: second);
        }
        // Trusted host API: caller must select the view using authenticated identity, not a client-supplied ID.
        public PlayerView GetPlayerView(int playerId)
        {
            if (!players.ContainsKey(playerId))
                throw new ArgumentException("Jogador inexistente.");
            return new PlayerView(GetPublicView(), knowledge[playerId]);
        }
        public MatchView GetPublicView() => new MatchView(this);
        public string DebugSecretState() => "HOST DEBUG ONLY | " + string.Join(", ", Tray.Cups.Select(c => c.CupId + ":" + (c.IsPoisoned ? "POISON" : "SAFE")));
        public ActionResult DebugGiveItem(int playerId, ItemType type)
        {
            if (!IsPlayerAlive(playerId) || !Config.ItemPool.Contains(type))
                return Fail(ErrorCode.InvalidTarget, "Jogador ou item inválido.");
            var p = players[playerId];
            if (p.Inventory.Count >= Config.InventoryCapacity || p.Inventory.Any(id => items[id].Definition.Type == type))
                return Fail(ErrorCode.CapacityExceeded, "Inventário cheio ou tipo duplicado.");
            return DebugOperation(() => { var item = new ItemState(nextItemId++, type); items.Add(item.ItemId, item); p.AddItem(item.ItemId); Emit(GameEventKind.DebugChanged, playerId, value: item.ItemId); });
        }
        public ActionResult DebugForceLives(int playerId, int lives)
        {
            if (!players.ContainsKey(playerId) || lives < 0)
                return Fail(ErrorCode.InvalidTarget, "Vidas inválidas.");
            return DebugOperation(() =>
            {
                bool alive = players[playerId].IsAlive;
                players[playerId].SetLives(lives);
                Emit(GameEventKind.LivesChanged, playerId, value: lives);
                if (alive && lives == 0)
                    Emit(GameEventKind.PlayerEliminated, playerId);
                EvaluateMatch();
                Classic?.ResetAfterDebug();
                Collective?.ResetAfterDebug();
                Alternative?.ResetAfterDebug();
            });
        }
        public ActionResult DebugForceTrayEnd() => DebugOperation(() =>
        {
            Emit(GameEventKind.TrayEnded);
            CreateTray();
            Classic?.ResetAfterDebug();
            Collective?.ResetAfterDebug();
            Alternative?.ResetAfterDebug();
        });
        private ActionResult DebugOperation(Action operation)
        {
            if (busy)
                return Fail(ErrorCode.Busy, "Operação em andamento.");
            if (Status == MatchStatus.Ended)
                return Fail(ErrorCode.MatchEnded, "Crie outra partida para reiniciar.");
            busy = true;
            pendingEvents.Clear();
            notificationErrors.Clear();
            try
            {
                operation();
                Revision++;
                Publish();
                return ActionResult.Ok("Cheat de desenvolvimento aplicado.");
            }
            finally { pendingEvents.Clear(); busy = false; }
        }
    }
}
