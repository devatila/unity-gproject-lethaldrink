using System.Collections.Generic;
using LethalDrink.Core;

namespace LethalDrink.Gameplay
{
    // MODE DESIGN INCOMPLETE: only selection/cancel and circular Drink are approved.
    public sealed class AlternativeTurnEngine
    {
        private readonly MatchEngine match;
        public int CurrentPlayerId
        {
            get; private set;
        }
        public int? SelectedCupId
        {
            get; private set;
        }
        internal AlternativeTurnEngine(MatchEngine engine)
        {
            match = engine;
            CurrentPlayerId = engine.Config.StartingPlayerId;
        }
        internal ActionResult Validate(IGameAction action)
        {
            if (action.ActorPlayerId != CurrentPlayerId)
                return MatchEngine.Fail(ErrorCode.WrongPlayer, "Outro jogador possui o turno.");
            if (action is SelectCupAction s)
                return match.Available(s.CupId) ? ActionResult.Ok() : MatchEngine.Fail(ErrorCode.CupUnavailable, "Taça indisponível.");
            if (action is CancelCupSelectionAction || action is DrinkCupAction)
                return SelectedCupId.HasValue ? ActionResult.Ok() : MatchEngine.Fail(ErrorCode.WrongPhase, "Selecione uma taça.");
            return MatchEngine.Fail(ErrorCode.DesignPending, "MODE DESIGN INCOMPLETE: Offer e itens não definidos.");
        }
        internal void Apply(IGameAction action)
        {
            if (action is SelectCupAction s)
            {
                SelectedCupId = s.CupId;
                match.Emit(GameEventKind.CupSelected, CurrentPlayerId, cup: s.CupId);
            }
            else if (action is CancelCupSelectionAction)
            {
                SelectedCupId = null;
                match.Emit(GameEventKind.SelectionCancelled, CurrentPlayerId);
            }
            else
            {
                int cup = SelectedCupId.Value;
                SelectedCupId = null;
                match.ResolveDrinks(new[] { new KeyValuePair<int, int>(CurrentPlayerId, cup) });
                if (match.Status == MatchStatus.Running)
                {
                    CurrentPlayerId = match.GetNextAlivePlayerId(CurrentPlayerId).Value;
                    match.Emit(GameEventKind.TurnStarted, CurrentPlayerId);
                    match.RenewEmptyTray();
                }
            }
        }
        internal void ResetAfterDebug()
        {
            SelectedCupId = null;
            if (match.Status == MatchStatus.Running && !match.IsPlayerAlive(CurrentPlayerId))
                CurrentPlayerId = match.GetNextAlivePlayerId(CurrentPlayerId).Value;
        }
    }
}
