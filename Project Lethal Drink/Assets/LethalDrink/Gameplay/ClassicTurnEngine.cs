using System.Collections.Generic;
using LethalDrink.Core;

namespace LethalDrink.Gameplay
{
    public sealed class PendingDrink
    {
        public int CupId
        {
            get;
        }
        public int OriginatorId
        {
            get;
        }
        public int TargetId
        {
            get;
        }
        internal PendingDrink(int cup, int originator, int target)
        {
            CupId = cup;
            OriginatorId = originator;
            TargetId = target;
        }
    }
    public sealed class ClassicTurnEngine
    {
        private readonly MatchEngine match;
        private ClassicPhase phase = ClassicPhase.FreeDecision;
        public ClassicPhase Phase => match.Status == MatchStatus.Ended ? ClassicPhase.Ended : phase;
        public int CurrentPlayerId
        {
            get; private set;
        }
        public int? SelectedCupId
        {
            get; private set;
        }
        public int? CannotOfferToPlayerId
        {
            get; private set;
        }
        public int ProactiveItemsUsed
        {
            get; private set;
        }
        public PendingDrink Pending
        {
            get; private set;
        }
        public bool ReactiveItemUsed
        {
            get; private set;
        }
        internal ClassicTurnEngine(MatchEngine engine)
        {
            match = engine;
            CurrentPlayerId = engine.StartingPlayerId.Value;
        }
        public bool CanPlayerReact(int player) => Phase == ClassicPhase.Reaction && Pending.TargetId == player && match.IsPlayerAlive(player);
        internal ActionResult Validate(IGameAction action)
        {
            bool reactive = Phase == ClassicPhase.Reaction;
            if (reactive && !CanPlayerReact(action.ActorPlayerId))
                return MatchEngine.Fail(ErrorCode.ReactionNotAllowed, "Somente o alvo da oferta pode reagir.");
            if (!reactive && action.ActorPlayerId != CurrentPlayerId)
                return MatchEngine.Fail(ErrorCode.WrongPlayer, "Outro jogador controla a iniciativa.");
            if (action is DrinkCupAction)
                return reactive || SelectedCupId.HasValue ? ActionResult.Ok() : MatchEngine.Fail(ErrorCode.WrongPhase, "Selecione uma taça.");
            if (action is UseRefusalAction refusal)
            {
                if (!reactive)
                    return MatchEngine.Fail(ErrorCode.ReactionNotAllowed, "Recusa exige oferta pendente.");
                if (ReactiveItemUsed)
                    return MatchEngine.Fail(ErrorCode.DesignPending, "Combinar itens na mesma reação aguarda decisão; beber continua permitido.");
                return match.ValidateItem(refusal, ItemType.Refusal, ItemUsageTiming.Reactive, true);
            }
            if (action is UsePurifierAction purifier)
            {
                if (reactive && ReactiveItemUsed)
                    return MatchEngine.Fail(ErrorCode.DesignPending, "Combinar itens na mesma reação aguarda decisão.");
                int? expected = reactive ? (int?)Pending.CupId : SelectedCupId;
                if (expected != purifier.CupId || !match.Available(purifier.CupId))
                    return MatchEngine.Fail(ErrorCode.InvalidTarget, "Purifique a taça selecionada/pendente.");
                if (match.Cup(purifier.CupId).IsPurified)
                    return MatchEngine.Fail(ErrorCode.InvalidTarget, "Taça já purificada.");
                return match.ValidateItem(purifier, ItemType.Purifier, reactive ? ItemUsageTiming.Reactive : ItemUsageTiming.Proactive, ProactiveItemsUsed < match.Config.ProactiveQuota);
            }
            if (reactive)
                return MatchEngine.Fail(ErrorCode.WrongPhase, "Durante reação: beber, Recusa ou Purifier.");
            if (action is SelectCupAction select)
                return match.Available(select.CupId) ? ActionResult.Ok() : MatchEngine.Fail(ErrorCode.CupUnavailable, "Taça indisponível.");
            if (action is CancelCupSelectionAction)
                return SelectedCupId.HasValue ? ActionResult.Ok() : MatchEngine.Fail(ErrorCode.WrongPhase, "Não há seleção.");
            if (action is OfferCupAction offer)
            {
                if (!SelectedCupId.HasValue)
                    return MatchEngine.Fail(ErrorCode.WrongPhase, "Selecione uma taça.");
                if (!match.IsPlayerAlive(offer.TargetPlayerId) || offer.TargetPlayerId == CurrentPlayerId || offer.TargetPlayerId == CannotOfferToPlayerId)
                    return MatchEngine.Fail(ErrorCode.InvalidTarget, "Alvo não permitido.");
                return ActionResult.Ok();
            }
            if (action is UseInspectionItemAction inspect)
            {
                if (!match.Available(inspect.CupId))
                    return MatchEngine.Fail(ErrorCode.CupUnavailable, "Taça indisponível.");
                return match.ValidateItem(inspect, ItemType.Inspection, ItemUsageTiming.Proactive, ProactiveItemsUsed < match.Config.ProactiveQuota);
            }
            if (action is UseSwapItemAction swap)
            {
                if (swap.FirstCupId == swap.SecondCupId || !match.Available(swap.FirstCupId) || !match.Available(swap.SecondCupId))
                    return MatchEngine.Fail(ErrorCode.InvalidTarget, "Escolha duas taças disponíveis diferentes.");
                return match.ValidateItem(swap, ItemType.Swap, ItemUsageTiming.Proactive, ProactiveItemsUsed < match.Config.ProactiveQuota);
            }
            return MatchEngine.Fail(ErrorCode.WrongMode, "Ação não pertence ao Classic.");
        }
        internal void Apply(IGameAction action)
        {
            if (action is SelectCupAction s)
            {
                SelectedCupId = s.CupId;
                phase = ClassicPhase.CupSelected;
                match.Emit(GameEventKind.CupSelected, s.ActorPlayerId, cup: s.CupId);
            }
            else if (action is CancelCupSelectionAction)
            {
                SelectedCupId = null;
                phase = ClassicPhase.FreeDecision;
                match.Emit(GameEventKind.SelectionCancelled, CurrentPlayerId);
            }
            else if (action is OfferCupAction offer)
            {
                Pending = new PendingDrink(SelectedCupId.Value, CurrentPlayerId, offer.TargetPlayerId);
                ReactiveItemUsed = false;
                SelectedCupId = null;
                phase = ClassicPhase.Reaction;
                match.Emit(GameEventKind.CupOffered, CurrentPlayerId, offer.TargetPlayerId, Pending.CupId);
            }
            else if (action is DrinkCupAction)
                ResolveDrink(false);
            else if (action is UseRefusalAction refusal)
            {
                match.ConsumeItem(refusal);
                ResolveDrink(true);
            }
            else if (action is IItemAction item)
            {
                bool reactive = Phase == ClassicPhase.Reaction;
                match.ConsumeItem(item);
                if (!reactive)
                    ProactiveItemsUsed++;
                else
                    ReactiveItemUsed = true;
                if (item is UseInspectionItemAction inspect)
                    match.Inspect(item.ActorPlayerId, inspect.CupId);
                else if (item is UsePurifierAction purifier)
                    match.Purify(purifier.CupId);
                else if (item is UseSwapItemAction swap)
                    match.Swap(swap.FirstCupId, swap.SecondCupId);
            }
        }
        private void ResolveDrink(bool refused)
        {
            var pending = Pending;
            int drinker = pending == null ? CurrentPlayerId : refused ? pending.OriginatorId : pending.TargetId;
            int cup = pending == null ? SelectedCupId.Value : pending.CupId;
            Pending = null;
            SelectedCupId = null;
            ReactiveItemUsed = false;
            phase = ClassicPhase.FreeDecision;
            if (pending != null)
                match.Emit(GameEventKind.ReactionEnded, pending.TargetId, cup: cup);
            bool poisoned = match.ResolveDrinks(new[] { new KeyValuePair<int, int>(drinker, cup) });
            if (match.Status == MatchStatus.Ended)
            {
                CannotOfferToPlayerId = null;
                return;
            }
            if (pending != null && !refused)
            {
                if (match.IsPlayerAlive(drinker))
                    StartInitiative(drinker, !poisoned && (match.AliveCount > 2 || match.Config.RestrictReturnInDuel) ? (int?)pending.OriginatorId : null);
                else
                    StartInitiative(NextAfterOfferElimination(drinker, pending.OriginatorId).Value, null);
            }
            else if (poisoned)
                StartInitiative(match.GetNextAlivePlayerId(drinker).Value, null);
            else
            {
                CannotOfferToPlayerId = null; // Safe Drink, including forced Drink after Refusal, keeps the same initiative quota.
            }
            match.RenewEmptyTray();
        }
        private int? NextAfterOfferElimination(int victim, int originator)
            => match.GetNextAlivePlayerId(victim, match.Config.SkipOfferOriginatorOnKill ? (int?)originator : null);
        private void StartInitiative(int player, int? restriction)
        {
            CurrentPlayerId = player;
            CannotOfferToPlayerId = restriction;
            ProactiveItemsUsed = 0;
            match.Emit(GameEventKind.TurnStarted, player);
        }
        internal void ResetAfterDebug()
        {
            Pending = null;
            SelectedCupId = null;
            CannotOfferToPlayerId = null;
            ReactiveItemUsed = false;
            phase = ClassicPhase.FreeDecision;
            if (match.Status == MatchStatus.Running && !match.IsPlayerAlive(CurrentPlayerId))
                StartInitiative(match.GetNextAlivePlayerId(CurrentPlayerId).Value, null);
        }
    }
}
