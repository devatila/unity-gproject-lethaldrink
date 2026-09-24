using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LethalDrink.Core;

namespace LethalDrink.Gameplay
{
    public sealed class CollectiveTurnEngine
    {
        private readonly MatchEngine match;
        private readonly Dictionary<int, int> reservations = new Dictionary<int, int>(); // cup -> player
        private readonly Dictionary<int, int> required = new Dictionary<int, int>();
        private readonly HashSet<int> ready = new HashSet<int>();
        private CollectivePhase phase;
        public CollectivePhase Phase => match.Status == MatchStatus.Ended ? CollectivePhase.Ended : phase;
        public int RoundNumber { get; private set; } = 1;
        public IReadOnlyDictionary<int, int> Reservations => new ReadOnlyDictionary<int, int>(reservations);
        internal CollectiveTurnEngine(MatchEngine engine)
        {
            match = engine;
            StartRound();
        }
        public int RequiredDrinkCount(int player) => required.TryGetValue(player, out var count) ? count : 0;
        public bool IsReady(int player) => ready.Contains(player);
        public int ReservedCount(int player) => reservations.Values.Count(id => id == player);
        public bool AllReady => match.Players.Where(p => p.IsAlive).All(p => ready.Contains(p.PlayerId) && ReservedCount(p.PlayerId) == required[p.PlayerId]);
        private void StartRound()
        {
            reservations.Clear();
            required.Clear();
            ready.Clear();
            foreach (var p in match.Players.Where(p => p.IsAlive))
                required[p.PlayerId] = 1;
            phase = match.Tray.RemainingCount < match.AliveCount ? CollectivePhase.AwaitingTrayDecision : CollectivePhase.Selection;
        }
        internal ActionResult Validate(IGameAction action)
        {
            if (Phase == CollectivePhase.AwaitingTrayDecision)
                return MatchEngine.Fail(ErrorCode.DesignPending, "Taças insuficientes: política de reposição Collective pendente. DebugForceTrayEnd permite laboratório.");
            int actor = action.ActorPlayerId;
            if (action is ResolveCollectiveRoundAction)
                return AllReady ? ActionResult.Ok() : MatchEngine.Fail(ErrorCode.NotReady, "Todos devem estar prontos com suas taças.");
            if (action is SetReadyAction r)
            {
                if (r.Ready && ReservedCount(actor) != required[actor])
                    return MatchEngine.Fail(ErrorCode.NotReady, "Reserve a quantidade exigida.");
                return ActionResult.Ok();
            }
            if (ready.Contains(actor))
                return MatchEngine.Fail(ErrorCode.WrongPhase, "Retire Ready antes de alterar sua preparação.");
            if (action is ReserveCupAction reserve)
            {
                if (!match.Available(reserve.CupId) || reservations.ContainsKey(reserve.CupId))
                    return MatchEngine.Fail(ErrorCode.CupUnavailable, "Taça usada ou reservada.");
                if (ReservedCount(actor) >= required[actor])
                    return MatchEngine.Fail(ErrorCode.CapacityExceeded, "Quantidade obrigatória já reservada.");
                return ActionResult.Ok();
            }
            if (action is CancelReservationAction cancel)
                return reservations.TryGetValue(cancel.CupId, out int owner) && owner == actor ? ActionResult.Ok() : MatchEngine.Fail(ErrorCode.InvalidTarget, "A reserva não pertence ao jogador.");
            ItemType type;
            if (action is UseDoubleDrinkAction dd)
            {
                if (!match.IsPlayerAlive(dd.TargetPlayerId))
                    return MatchEngine.Fail(ErrorCode.InvalidTarget, "Alvo eliminado/inexistente.");
                if (ready.Contains(dd.TargetPlayerId))
                    return MatchEngine.Fail(ErrorCode.DesignPending, "Alteração de alvo Ready ainda não definida; retire Ready antes do teste.");
                if (required[dd.TargetPlayerId] >= match.Config.MaxCollectiveDrinks || required.Values.Sum() + 1 > match.Tray.RemainingCount)
                    return MatchEngine.Fail(ErrorCode.CapacityExceeded, "Limite de bebidas ou taças insuficientes para todos.");
                type = ItemType.DoubleDrink;
            }
            else if (action is UseInspectionItemAction inspect)
            {
                if (!match.Available(inspect.CupId))
                    return MatchEngine.Fail(ErrorCode.CupUnavailable, "Taça indisponível.");
                type = ItemType.Inspection;
            }
            else if (action is UsePurifierAction purifier)
            {
                if (!reservations.TryGetValue(purifier.CupId, out int owner) || owner != actor || match.Cup(purifier.CupId).IsPurified)
                    return MatchEngine.Fail(ErrorCode.InvalidTarget, "Purifique uma taça própria reservada ainda não purificada.");
                type = ItemType.Purifier;
            }
            else if (action is UseSwapItemAction swap)
            {
                if (swap.FirstCupId == swap.SecondCupId || !match.Available(swap.FirstCupId) || !match.Available(swap.SecondCupId))
                    return MatchEngine.Fail(ErrorCode.InvalidTarget, "Escolha duas taças disponíveis.");
                // Reservation identity survives a position swap; whether reserved cups may be manipulated is undecided.
                if (reservations.ContainsKey(swap.FirstCupId) || reservations.ContainsKey(swap.SecondCupId))
                    return MatchEngine.Fail(ErrorCode.DesignPending, "Swap de taças reservadas aguarda decisão; teste com taças livres.");
                type = ItemType.Swap;
            }
            else
                return MatchEngine.Fail(ErrorCode.WrongMode, "Ação não pertence ao Collective.");
            // Collective free-item quota has no approved semantics yet. Do not silently copy Classic's quota.
            return match.ValidateItem((IItemAction)action, type, ItemUsageTiming.Proactive, true);
        }
        internal void Apply(IGameAction action)
        {
            int actor = action.ActorPlayerId;
            if (action is ReserveCupAction reserve)
            {
                reservations.Add(reserve.CupId, actor);
                match.Emit(GameEventKind.ReservationChanged, actor, cup: reserve.CupId, value: 1);
            }
            else if (action is CancelReservationAction cancel)
            {
                reservations.Remove(cancel.CupId);
                match.Emit(GameEventKind.ReservationChanged, actor, cup: cancel.CupId, value: 0);
            }
            else if (action is SetReadyAction r)
            {
                if (r.Ready)
                    ready.Add(actor);
                else
                    ready.Remove(actor);
                match.Emit(GameEventKind.ReadyChanged, actor, value: r.Ready ? 1 : 0);
            }
            else if (action is ResolveCollectiveRoundAction)
            {
                match.Emit(GameEventKind.RoundLocked, value: RoundNumber);
                var drinks = reservations.OrderBy(k => k.Value).ThenBy(k => k.Key).Select(k => new KeyValuePair<int, int>(k.Value, k.Key)).ToArray();
                match.ResolveDrinks(drinks);
                match.Emit(GameEventKind.RoundResolved, value: RoundNumber);
                if (match.Status == MatchStatus.Running)
                {
                    match.RenewEmptyTray();
                    RoundNumber++;
                    StartRound();
                }
                else
                {
                    reservations.Clear();
                    ready.Clear();
                }
            }
            else if (action is IItemAction item)
            {
                match.ConsumeItem(item);
                if (item is UseDoubleDrinkAction dd)
                    required[dd.TargetPlayerId]++;
                else if (item is UseInspectionItemAction inspect)
                    match.Inspect(actor, inspect.CupId);
                else if (item is UsePurifierAction purifier)
                    match.Purify(purifier.CupId);
                else if (item is UseSwapItemAction swap)
                    match.Swap(swap.FirstCupId, swap.SecondCupId);
            }
        }
        internal void ResetAfterDebug()
        {
            StartRound();
        }
    }
}
