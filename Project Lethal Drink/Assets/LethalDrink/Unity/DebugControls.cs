using System.Linq;
using LethalDrink.Core;
using LethalDrink.Gameplay;
using UnityEngine;

namespace LethalDrink.Unity
{
    public sealed class DebugControls : MonoBehaviour
    {
        [SerializeField] private int actorId = 1;
        [SerializeField] private int targetId = 2;
        [SerializeField] private int cupId = 1;
        [SerializeField] private int secondCupId = 2;
        [SerializeField] private int itemId = 1;
        [SerializeField] private int lives = 1;
        [SerializeField] private ItemType itemType = ItemType.Refusal;
        private MatchEngine Engine => GameManager.Instance == null ? null : GameManager.Instance.Engine;
        private void Send(IGameAction action) { if (GameManager.Instance == null) Debug.LogError("GameManager ausente."); else GameManager.Instance.ExecuteAction(action); }
        [ContextMenu("Classic/Select Cup")] public void SelectCup() => Send(new SelectCupAction(actorId, cupId));
        [ContextMenu("Classic/Cancel Selection")] public void CancelSelection() => Send(new CancelCupSelectionAction(actorId));
        [ContextMenu("Classic/Drink")] public void Drink() => Send(new DrinkCupAction(actorId));
        [ContextMenu("Classic/Offer")] public void Offer() => Send(new OfferCupAction(actorId, targetId));
        [ContextMenu("Items/Inspection")] public void UseInspection() => Send(new UseInspectionItemAction(actorId, itemId, cupId));
        [ContextMenu("Items/Purifier")] public void UsePurifier() => Send(new UsePurifierAction(actorId, itemId, cupId));
        [ContextMenu("Items/Refusal")] public void UseRefusal() => Send(new UseRefusalAction(actorId, itemId));
        [ContextMenu("Items/Swap")] public void UseSwap() => Send(new UseSwapItemAction(actorId, itemId, cupId, secondCupId));
        [ContextMenu("Items/Double Drink")] public void UseDoubleDrink() => Send(new UseDoubleDrinkAction(actorId, itemId, targetId));
        [ContextMenu("Collective/Reserve")] public void ReserveCup() => Send(new ReserveCupAction(actorId, cupId));
        [ContextMenu("Collective/Cancel Reservation")] public void CancelReservation() => Send(new CancelReservationAction(actorId, cupId));
        [ContextMenu("Collective/Ready")] public void Ready() => Send(new SetReadyAction(actorId));
        [ContextMenu("Collective/Unready")] public void Unready() => Send(new SetReadyAction(actorId, false));
        [ContextMenu("Collective/Resolve Round")] public void ResolveRound() => Send(new ResolveCollectiveRoundAction(actorId));
        [ContextMenu("Cheats/Give Item")] public void GiveItem() { if (Engine != null) Debug.Log(Engine.DebugGiveItem(actorId, itemType).ToString()); }
        [ContextMenu("Cheats/Force Lives")] public void ForcePlayerLives() { if (Engine != null) Debug.Log(Engine.DebugForceLives(actorId, lives).ToString()); }
        [ContextMenu("Cheats/Force Tray End")] public void ForceTrayEnd() { if (Engine != null) Debug.Log(Engine.DebugForceTrayEnd().ToString()); }
        [ContextMenu("State/Public")] public void PrintPublicState()
        {
            if (Engine == null) return; var v = Engine.GetPublicView();
            Debug.Log($"{v.Mode} {v.Status} rev={v.Revision} tray={v.TrayId} cups={v.RemainingCups} initialPoison={v.InitialPoisonCount} remainingPoison={v.RemainingPoisons} current={v.CurrentPlayerId} phase={v.ClassicPhase}/{v.CollectivePhase} pendingCup={v.PendingDrink?.CupId}");
            foreach (var p in v.Players) Debug.Log($"P{p.PlayerId} lives={p.Lives} inventory=" + string.Join(",", p.Inventory.Select(i => i.ItemId + ":" + i.Type)));
            foreach (var c in v.Cups) Debug.Log($"Cup{c.CupId} position={c.Position} used={c.IsUsed} purified={c.IsPurified}");
        }
        [ContextMenu("State/Private View Of Actor - HOST DEBUG ONLY")] public void PrintPlayerKnowledge()
        { if (Engine != null && Engine.Players.Any(p => p.PlayerId == actorId)) Debug.Log(string.Join(",", Engine.GetPlayerView(actorId).KnownCupContents.Select(k => k.Key + ":" + k.Value))); }
        [ContextMenu("State/SECRET - HOST DEBUG ONLY")] public void PrintSecretState() { if (Engine != null) Debug.Log(Engine.DebugSecretState()); }
    }
}
