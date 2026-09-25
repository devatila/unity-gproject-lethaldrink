using System;
using System.Collections.Generic;
using System.Linq;
using LethalDrink.Core;
using LethalDrink.Gameplay;

internal static class Program
{
    private static int passed, failed;
    private static void Check(bool condition, string message = "assertion")
    {
        if (!condition)
            throw new Exception(message);
    }
    private static void Ok(ActionResult result) => Check(result.Success, result.ToString());
    private static void Error(ActionResult result, ErrorCode code) => Check(!result.Success && result.Error == code, result.ToString());
    private static void Test(string name, Action run)
    {
        try
        {
            run();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + " | " + ex.Message); }
    }
    private static MatchEngine Match(GameMode mode = GameMode.Classic, int players = 4, int cups = 8, int poisons = 0, int lives = 3)
        => new MatchEngine(new MatchConfig(mode, players, new[] { new TrayConfiguration(cups, poisons) }, seed: 12345, startingLives: lives, startingPlayerId: 1, collectiveTimerEnabled: true));
    private static int Give(MatchEngine m, int player, ItemType type)
    {
        Ok(m.DebugGiveItem(player, type));
        return m.GetPlayer(player).Inventory.Last();
    }
    private static void Select(MatchEngine m, int actor, int cup) => Ok(m.ExecuteAction(new SelectCupAction(actor, cup)));
    private static void Offer(MatchEngine m, int actor, int target, int cup)
    {
        Select(m, actor, cup);
        Ok(m.ExecuteAction(new OfferCupAction(actor, target)));
    }
    private static void ReserveReady(MatchEngine m, int actor, int cup)
    {
        Ok(m.ExecuteAction(new ReserveCupAction(actor, cup)));
        Ok(m.ExecuteAction(new SetReadyAction(actor)));
    }
    public static int Main()
    {
        Test("Selection and cancellation never consume", () => { var m = Match(); Select(m, 1, 2); Check(m.Classic.SelectedCupId == 2 && !m.Tray.Cups[1].IsUsed); Ok(m.ExecuteAction(new CancelCupSelectionAction(1))); Check(m.Classic.SelectedCupId == null && m.Tray.RemainingCount == 8); });
        Test("Safe Drink retains initiative", () => { var m = Match(); Select(m, 1, 2); Ok(m.ExecuteAction(new DrinkCupAction(1))); Check(m.Classic.CurrentPlayerId == 1 && m.GetPlayer(1).Lives == 3 && m.Tray.Cups[1].IsUsed); });
        Test("Poison damages and passes to next alive", () => { var m = Match(poisons: 8); Select(m, 1, 1); Ok(m.ExecuteAction(new DrinkCupAction(1))); Check(m.GetPlayer(1).Lives == 2 && m.Classic.CurrentPlayerId == 2); });
        Test("Eliminated player cannot act", () => { var m = Match(); Ok(m.DebugForceLives(2, 0)); Error(m.ExecuteAction(new SelectCupAction(2, 1)), ErrorCode.WrongPlayer); });
        Test("Offer pending no consumption or official transfer", () => { var m = Match(); Offer(m, 1, 3, 5); Check(m.Classic.Pending.CupId == 5 && m.Classic.CurrentPlayerId == 1 && m.Tray.RemainingCount == 8); Check(m.Classic.CanPlayerReact(3) && !m.Classic.CanPlayerReact(1) && !m.Classic.CanPlayerReact(2)); });
        Test("Originator blocked for every action during reaction", () => { var m = Match(); int i = Give(m, 1, ItemType.Inspection); Offer(m, 1, 3, 5); foreach (var a in new IGameAction[] { new SelectCupAction(1, 2), new CancelCupSelectionAction(1), new DrinkCupAction(1), new OfferCupAction(1, 2), new UseInspectionItemAction(1, i, 2) }) Error(m.ExecuteAction(a), ErrorCode.ReactionNotAllowed); Error(m.ExecuteAction(new DrinkCupAction(2)), ErrorCode.ReactionNotAllowed); });
        Test("Safe Offer restriction ends on safe Drink", () => { var m = Match(); Offer(m, 1, 2, 3); Ok(m.ExecuteAction(new DrinkCupAction(2))); Check(m.Classic.CurrentPlayerId == 2 && m.Classic.CannotOfferToPlayerId == 1); Select(m, 2, 4); Error(m.ExecuteAction(new OfferCupAction(2, 1)), ErrorCode.InvalidTarget); Ok(m.ExecuteAction(new DrinkCupAction(2))); Check(m.Classic.CannotOfferToPlayerId == null); Offer(m, 2, 1, 5); });
        Test("Poison Offer allows immediate retaliation", () => { var m = Match(poisons: 8); Offer(m, 1, 2, 1); Ok(m.ExecuteAction(new DrinkCupAction(2))); Check(m.Classic.CurrentPlayerId == 2 && m.Classic.CannotOfferToPlayerId == null); Offer(m, 2, 1, 2); });
        Test("Offer kill skips originator under playtest policy", () => { var m = Match(poisons: 8); Ok(m.DebugForceLives(4, 0)); Ok(m.DebugForceLives(3, 1)); Offer(m, 1, 3, 1); Ok(m.ExecuteAction(new DrinkCupAction(3))); Check(m.Classic.CurrentPlayerId == 2); });
        Test("Refusal forces originator no second reaction window", () => { var m = Match(poisons: 8); int refusal = Give(m, 3, ItemType.Refusal); int other = Give(m, 1, ItemType.Refusal); Offer(m, 1, 3, 5); Ok(m.ExecuteAction(new UseRefusalAction(3, refusal))); Check(m.GetPlayer(1).Lives == 2 && m.GetPlayer(3).Lives == 3 && m.Classic.Pending == null && m.GetItem(refusal).IsUsed); Error(m.ExecuteAction(new UseRefusalAction(1, other)), ErrorCode.WrongPlayer); });
        Test("Refusal is enabled by default and safe forced Drink keeps originator", () => { var m = Match(); int i = Give(m, 2, ItemType.Refusal); Offer(m, 1, 2, 1); Ok(m.ExecuteAction(new UseRefusalAction(2, i))); Check(m.GetItem(i).IsUsed && m.Classic.Pending == null && m.Classic.CurrentPlayerId == 1); });
        Test("Reactive Purifier then Drink safe conceals original", () => { var m = Match(poisons: 8); int i = Give(m, 3, ItemType.Purifier); Offer(m, 1, 3, 5); Ok(m.ExecuteAction(new UsePurifierAction(3, i, 5))); Check(m.Classic.Pending != null && m.Tray.InitialPoisonCount == 8); Ok(m.ExecuteAction(new DrinkCupAction(3))); Check(m.GetPlayer(3).Lives == 3 && m.Classic.CurrentPlayerId == 3); });
        Test("Proactive Purifier requires selected cup", () => { var m = Match(poisons: 8); int i = Give(m, 1, ItemType.Purifier); Error(m.ExecuteAction(new UsePurifierAction(1, i, 1)), ErrorCode.InvalidTarget); Select(m, 1, 1); Ok(m.ExecuteAction(new UsePurifierAction(1, i, 1))); Ok(m.ExecuteAction(new DrinkCupAction(1))); Check(m.GetPlayer(1).Lives == 3); });
        Test("Multiple reactive items explicitly pending", () => { var m = Match(); int p = Give(m, 2, ItemType.Purifier); int r = Give(m, 2, ItemType.Refusal); Offer(m, 1, 2, 1); Ok(m.ExecuteAction(new UsePurifierAction(2, p, 1))); Error(m.ExecuteAction(new UseRefusalAction(2, r)), ErrorCode.DesignPending); Check(!m.GetItem(r).IsUsed); });
        Test("Last safe cup renews tray preserving quota", () => { var m = Match(cups: 1); int i = Give(m, 1, ItemType.Inspection); Ok(m.ExecuteAction(new UseInspectionItemAction(1, i, 1))); Select(m, 1, 1); Ok(m.ExecuteAction(new DrinkCupAction(1))); Check(m.Tray.TrayId == 2 && m.Classic.CurrentPlayerId == 1 && m.Classic.ProactiveItemsUsed == 1 && m.Tray.Cups[0].CupId != 1); });
        Test("Exact reproducible poison composition across 25 trays", () => { var a = Match(poisons: 3); var b = Match(poisons: 3); for (int k = 0; k < 25; k++) { Check(a.DebugSecretState().Split(new[] { "POISON" }, StringSplitOptions.None).Length - 1 == 3); Check(a.DebugSecretState() == b.DebugSecretState()); Ok(a.DebugForceTrayEnd()); Ok(b.DebugForceTrayEnd()); } });
        Test("Victory blocks actions no extra tray", () => { var m = Match(players: 2, cups: 1, poisons: 1, lives: 1); int wins = 0; m.EventOccurred += e => { if (e.Kind == GameEventKind.MatchEnded) wins++; }; Select(m, 1, 1); Ok(m.ExecuteAction(new DrinkCupAction(1))); Check(m.Status == MatchStatus.Ended && m.WinnerId == 2 && wins == 1 && m.Tray.TrayId == 1); Error(m.ExecuteAction(new SelectCupAction(2, 1)), ErrorCode.MatchEnded); });
        Test("Quota persists and resets when initiative returns", () => { var m = Match(); int inspect = Give(m, 1, ItemType.Inspection); int purify = Give(m, 1, ItemType.Purifier); Ok(m.ExecuteAction(new UseInspectionItemAction(1, inspect, 1))); Select(m, 1, 1); Ok(m.ExecuteAction(new DrinkCupAction(1))); Select(m, 1, 2); Error(m.ExecuteAction(new UsePurifierAction(1, purify, 2)), ErrorCode.QuotaSpent); Ok(m.ExecuteAction(new OfferCupAction(1, 2))); Ok(m.ExecuteAction(new DrinkCupAction(2))); Select(m, 2, 3); Ok(m.ExecuteAction(new DrinkCupAction(2))); Offer(m, 2, 1, 4); Ok(m.ExecuteAction(new DrinkCupAction(1))); Check(m.Classic.ProactiveItemsUsed == 0); });
        Test("Inspection private detached views", () => { var m = Match(); int i = Give(m, 1, ItemType.Inspection); var before = m.GetPlayerView(1); Ok(m.ExecuteAction(new UseInspectionItemAction(1, i, 2))); Check(before.KnownCupContents.Count == 0 && m.GetPlayerView(1).KnownCupContents.ContainsKey(2) && m.GetPlayerView(2).KnownCupContents.Count == 0); Check(typeof(CupView).GetProperty("IsPoisoned") == null && typeof(MatchView).GetProperty("Seed") == null); });
        Test("Swap changes positions not identity nor selection", () => { var m = Match(); int i = Give(m, 1, ItemType.Swap); Select(m, 1, 1); Ok(m.ExecuteAction(new UseSwapItemAction(1, i, 1, 2))); Check(m.Tray.Cups[0].Position == 1 && m.Tray.Cups[1].Position == 0 && m.Classic.SelectedCupId == 1); });
        Test("Item ownership capacity duplicates validated", () => { var m = Match(); int i = Give(m, 2, ItemType.Inspection); Error(m.ExecuteAction(new UseInspectionItemAction(1, i, 1)), ErrorCode.ItemUnavailable); Error(m.DebugGiveItem(2, ItemType.Inspection), ErrorCode.CapacityExceeded); Give(m, 2, ItemType.Purifier); Give(m, 2, ItemType.Refusal); Error(m.DebugGiveItem(2, ItemType.Swap), ErrorCode.CapacityExceeded); });
        Test("Invalid action no mutation no events", () => { var m = Match(); int events = 0; m.EventOccurred += e => events++; Error(m.ExecuteAction(new SelectCupAction(1, 900)), ErrorCode.CupUnavailable); Check(m.Revision == 0 && events == 0 && m.Tray.RemainingCount == 8); });
        Test("Observer exception isolation reentry blocked", () => { var m = Match(); Select(m, 1, 1); int seen = 0; m.EventOccurred += e => throw new Exception("presentation failure"); m.EventOccurred += e => { seen++; Check(m.Tray.Cups[0].IsUsed && m.Classic.SelectedCupId == null); Error(m.ExecuteAction(new SelectCupAction(1, 2)), ErrorCode.Busy); }; Ok(m.ExecuteAction(new DrinkCupAction(1))); Check(seen > 0 && m.NotificationErrors.Count == seen); });
        Test("Stale revision rejects repeated request", () => { var m = Match(); Ok(m.ExecuteAction(new SelectCupAction(1, 1), 0)); Error(m.ExecuteAction(new SelectCupAction(1, 2), 0), ErrorCode.StaleRevision); Check(m.Classic.SelectedCupId == 1); });
        Test("Unknown next-player origin returns null", () => { var m = Match(); Check(m.GetNextAlivePlayerId(99) == null && m.GetNextAlivePlayerId(4) == 1); });
        Test("Collective exclusive cancellable reservations", () => { var m = Match(GameMode.Collective); Check(m.Collective.RequiredDrinkCount(1) == 1); Ok(m.ExecuteAction(new ReserveCupAction(1, 1))); Error(m.ExecuteAction(new ReserveCupAction(2, 1)), ErrorCode.CupUnavailable); Ok(m.ExecuteAction(new CancelReservationAction(1, 1))); Ok(m.ExecuteAction(new ReserveCupAction(2, 1))); });
        Test("DoubleDrink stacks then resets", () => { var m = Match(GameMode.Collective); int a = Give(m, 2, ItemType.DoubleDrink); int b = Give(m, 3, ItemType.DoubleDrink); for (int p = 1; p <= 4; p++) Ok(m.ExecuteAction(new ReserveCupAction(p, p))); Ok(m.ExecuteAction(new UseDoubleDrinkAction(2, a, 1))); Ok(m.ExecuteAction(new UseDoubleDrinkAction(3, b, 1))); Check(m.Collective.RequiredDrinkCount(1) == 3); Error(m.ExecuteAction(new SetReadyAction(1)), ErrorCode.NotReady); Ok(m.ExecuteAction(new ReserveCupAction(1, 5))); Ok(m.ExecuteAction(new ReserveCupAction(1, 6))); for (int p = 1; p <= 4; p++) Ok(m.ExecuteAction(new SetReadyAction(p))); Ok(m.ExecuteAction(new ResolveCollectiveRoundAction(1))); Check(m.Collective.RequiredDrinkCount(1) == 1 && m.Collective.Phase == CollectivePhase.Selection && m.Tray.TrayId == 2); });
        Test("DoubleDrink accepts demand exceeding remaining cups", () => { var m = Match(GameMode.Collective, cups: 4); int i = Give(m, 2, ItemType.DoubleDrink); Ok(m.ExecuteAction(new UseDoubleDrinkAction(2, i, 1))); Check(m.GetItem(i).IsUsed && m.Collective.RequiredDrinkCount(1) == 2); });
        Test("DoubleDrink provisional cap", () => { var m = Match(GameMode.Collective); foreach (int p in new[] { 2, 3, 4 }) { int i = Give(m, p, ItemType.DoubleDrink); var r = m.ExecuteAction(new UseDoubleDrinkAction(p, i, 1)); if (p < 4) Ok(r); else Error(r, ErrorCode.CapacityExceeded); } });
        Test("Collective waits for everybody Ready", () => { var m = Match(GameMode.Collective); ReserveReady(m, 1, 1); Error(m.ExecuteAction(new ResolveCollectiveRoundAction(1)), ErrorCode.NotReady); Check(m.Tray.RemainingCount == 8); });
        Test("Two final deaths Draw without intermediate winner", () => { var m = Match(GameMode.Collective, players: 2, cups: 2, poisons: 2, lives: 1); ReserveReady(m, 1, 1); ReserveReady(m, 2, 2); var ends = new List<GameEvent>(); m.EventOccurred += e => { if (e.Kind == GameEventKind.MatchEnded) ends.Add(e); }; Ok(m.ExecuteAction(new ResolveCollectiveRoundAction(1))); Check(m.Outcome == MatchOutcome.Draw && m.WinnerId == null && m.Players.All(p => p.Lives == 0) && ends.Count == 1 && ends[0].PlayerId == null); });
        Test("Batch observers see all damage already applied", () => { var m = Match(GameMode.Collective, players: 2, cups: 2, poisons: 2); ReserveReady(m, 1, 1); ReserveReady(m, 2, 2); int seen = 0; m.EventOccurred += e => { if (e.Kind == GameEventKind.LivesChanged) { Check(m.GetPlayer(1).Lives == 2 && m.GetPlayer(2).Lives == 2); seen++; } }; Ok(m.ExecuteAction(new ResolveCollectiveRoundAction(1))); Check(seen == 2 && m.NotificationErrors.Count == 0 && m.Tray.TrayId == 2); });
        Test("Collective replaces insufficient leftovers automatically", () => { var m = Match(GameMode.Collective, players: 3, cups: 4); for (int p = 1; p <= 3; p++) ReserveReady(m, p, p); Ok(m.ExecuteAction(new ResolveCollectiveRoundAction(1))); Check(m.Tray.TrayId == 2 && m.Tray.RemainingCount == 4); Ok(m.ExecuteAction(new ReserveCupAction(1, m.Tray.Cups[0].CupId))); });
        Test("Ready target manipulation explicit pending", () => { var m = Match(GameMode.Collective); int i = Give(m, 2, ItemType.DoubleDrink); ReserveReady(m, 1, 1); Error(m.ExecuteAction(new UseDoubleDrinkAction(2, i, 1)), ErrorCode.DesignPending); Check(!m.GetItem(i).IsUsed); });
        Test("Alternative safe drink rotates", () => { var m = Match(GameMode.Alternative); Select(m, 1, 1); Ok(m.ExecuteAction(new DrinkCupAction(1))); Check(m.Alternative.CurrentPlayerId == 2); Error(m.ExecuteAction(new OfferCupAction(2, 3)), ErrorCode.DesignPending); });
        Test("Optional fatal poison preserves displayed lives until drink", () => { var m = new MatchEngine(new MatchConfig(GameMode.Classic, 2, new[] { new TrayConfiguration(2, 2) }, startingPlayerId: 1, suddenDeath: SuddenDeathRule.FatalPoison)); Check(m.GetPlayer(1).Lives == 3); Select(m, 1, 1); Ok(m.ExecuteAction(new DrinkCupAction(1))); Check(m.GetPlayer(1).Lives == 0 && m.WinnerId == 2); });
        Test("Default invalid tray rejected", () => { bool rejected = false; try { new MatchConfig(GameMode.Classic, 2, new[] { default(TrayConfiguration) }); } catch (ArgumentException) { rejected = true; } Check(rejected); });
        Test("Initial poison label never changes after poison or Purifier", () =>
        {
            var m = Match(poisons: 8);
            Select(m, 1, 1);
            Ok(m.ExecuteAction(new DrinkCupAction(1)));
            Check(m.GetPublicView().InitialPoisonCount == 8);
            int item = Give(m, 2, ItemType.Purifier);
            Select(m, 2, 2);
            Ok(m.ExecuteAction(new UsePurifierAction(2, item, 2)));
            Ok(m.ExecuteAction(new DrinkCupAction(2)));
            Check(m.GetPublicView().InitialPoisonCount == 8);
            Check(typeof(MatchView).GetProperty("RemainingPoisons") == null);
        });
        Test("Starting player draw is reproducible and reaches every seat", () =>
        {
            foreach (var mode in new[] { GameMode.Classic, GameMode.Alternative })
            {
                var starters = new HashSet<int>();
                for (int seed = 0; seed < 100; seed++)
                {
                    var cfg = new MatchConfig(mode, 4, new[] { new TrayConfiguration(8, 3) }, seed: seed);
                    var a = new MatchEngine(cfg);
                    var b = new MatchEngine(cfg);
                    Check(a.StartingPlayerId == b.StartingPlayerId);
                    Check(a.DebugSecretState() == b.DebugSecretState());
                    starters.Add(a.StartingPlayerId.Value);
                }
                Check(starters.SetEquals(new[] { 1, 2, 3, 4 }));
            }
        });
        Test("Explicit starter override and Collective without individual starter", () =>
        {
            var cfg = new MatchConfig(GameMode.Classic, 4, new[] { new TrayConfiguration(8, 3) }, startingPlayerId: 3);
            var m = new MatchEngine(cfg);
            Check(m.StartingPlayerId == 3 && m.Classic.CurrentPlayerId == 3);
            Check(Match(GameMode.Collective).StartingPlayerId == null);
        });
        Test("Collective keeps leftovers when sufficient for survivors", () =>
        {
            var m = Match(GameMode.Collective, players: 3, cups: 6);
            for (int p = 1; p <= 3; p++) ReserveReady(m, p, p);
            Ok(m.ExecuteAction(new ResolveCollectiveRoundAction(1)));
            Check(m.Tray.TrayId == 1 && m.Tray.RemainingCount == 3);
        });
        Test("Automatic allocation matches 3+2 missing drinks with only 4 free cups", () =>
        {
            var m = Match(GameMode.Collective, cups: 6);
            int one = Give(m, 1, ItemType.DoubleDrink);
            int two = Give(m, 4, ItemType.DoubleDrink);
            int three = Give(m, 3, ItemType.DoubleDrink);
            Ok(m.ExecuteAction(new UseDoubleDrinkAction(1, one, 3)));
            Ok(m.ExecuteAction(new UseDoubleDrinkAction(4, two, 3)));
            Ok(m.ExecuteAction(new UseDoubleDrinkAction(3, three, 2)));
            ReserveReady(m, 1, 1);
            ReserveReady(m, 4, 2);
            var recipients = new List<int>();
            m.EventOccurred += e => { if (e.Kind == GameEventKind.ReservationChanged) recipients.Add(e.PlayerId.Value); };
            Ok(m.CompleteCollectiveSelection());
            Check(recipients.SequenceEqual(new[] { 2, 3, 2, 3 }));
            Check(m.Collective.ReservedCount(2) == 2 && m.Collective.ReservedCount(3) == 2);
            Check(m.Collective.AllReady && m.Collective.Reservations.Count == 6);
            Check(m.Collective.Reservations[1] == 1 && m.Collective.Reservations[2] == 4);
            Ok(m.ExecuteAction(new ResolveCollectiveRoundAction(1)));
            Check(m.Tray.TrayId == 2);
        });
        Test("Automatic allocation gives missing first cups before extras", () =>
        {
            var m = Match(GameMode.Collective, cups: 4);
            int item = Give(m, 2, ItemType.DoubleDrink);
            Ok(m.ExecuteAction(new UseDoubleDrinkAction(2, item, 1)));
            Ok(m.ExecuteAction(new ReserveCupAction(1, 1)));
            Ok(m.CompleteCollectiveSelection());
            Check(m.Players.All(p => m.Collective.ReservedCount(p.PlayerId) == 1));
            Check(m.Collective.AllReady);
        });
        Test("Freeing a cup invalidates scarcity-only Ready", () =>
        {
            var m = Match(GameMode.Collective, players: 2, cups: 2);
            int item = Give(m, 2, ItemType.DoubleDrink);
            Ok(m.ExecuteAction(new UseDoubleDrinkAction(2, item, 1)));
            Ok(m.ExecuteAction(new ReserveCupAction(1, 1)));
            Ok(m.ExecuteAction(new ReserveCupAction(2, 2)));
            Ok(m.ExecuteAction(new SetReadyAction(1)));
            Check(m.Collective.IsReady(1));
            Ok(m.ExecuteAction(new CancelReservationAction(2, 2)));
            Check(!m.Collective.IsReady(1));
            Error(m.ExecuteAction(new SetReadyAction(1)), ErrorCode.NotReady);
        });
        Test("Tray deadline starts at 60 and resolves incomplete choices", () =>
        {
            var m = Match(GameMode.Collective, cups: 4);
            Ok(m.AdvanceTime(59));
            Check(m.Collective.RemainingSeconds == 1 && m.Tray.RemainingCount == 4 && m.Revision == 0);
            Ok(m.AdvanceTime(1));
            Check(m.Tray.TrayId == 2 && m.Collective.RoundNumber == 2);
            Check(m.Collective.RemainingSeconds == 55 && m.Revision == 1);
            Check(m.GetPublicView().CollectiveTimerEnabled && m.GetPublicView().CollectiveRemainingSeconds == 55);
        });
        Test("Disabled timer preserves choices and allows manual tray progression", () =>
        {
            var m = new MatchEngine(new MatchConfig(GameMode.Collective, 2, new[] { new TrayConfiguration(2, 0) }));
            Check(!m.Config.CollectiveTimerEnabled && !m.GetPublicView().CollectiveTimerEnabled);
            Check(m.GetPublicView().CollectiveRemainingSeconds == null);
            ReserveReady(m, 1, 1);
            long revision = m.Revision;
            int events = 0;
            m.EventOccurred += e => events++;
            Ok(m.AdvanceTime(10000));
            Check(m.Revision == revision && events == 0 && m.Tray.TrayId == 1);
            Check(m.Collective.RemainingSeconds == 60 && m.Collective.ReservedCount(1) == 1 && m.Collective.IsReady(1));
            Check(m.Tray.RemainingCount == 2 && m.Collective.ReservedCount(2) == 0);
            Ok(m.CompleteCollectiveSelection());
            Ok(m.ExecuteAction(new ResolveCollectiveRoundAction(1)));
            Check(m.Tray.TrayId == 2);
            revision = m.Revision;
            events = 0;
            Ok(m.AdvanceTime(10000));
            Check(m.Revision == revision && events == 0 && m.Tray.TrayId == 2 && m.Collective.RemainingSeconds == 55);
        });
        Test("Deadline auto-resolves remaining selections until tray replacement", () =>
        {
            var m = Match(GameMode.Collective, players: 2, cups: 8);
            int consumed = 0, resolved = 0, expired = 0;
            m.EventOccurred += e =>
            {
                if (e.Kind == GameEventKind.CupDrunk) consumed++;
                if (e.Kind == GameEventKind.RoundResolved) resolved++;
                if (e.Kind == GameEventKind.RoundDeadlineExpired) expired++;
            };
            Ok(m.AdvanceTime(60));
            Check(consumed == 8 && resolved == 4 && expired == 1);
            Check(m.Tray.TrayId == 2 && m.Collective.SelectionNumber == 1 && m.Collective.RemainingSeconds == 55);
        });
        Test("Manual selection resolution does not reset tray round or clock", () =>
        {
            var m = Match(GameMode.Collective, players: 2, cups: 6);
            Ok(m.AdvanceTime(10));
            ReserveReady(m, 1, 1);
            ReserveReady(m, 2, 2);
            Ok(m.ExecuteAction(new ResolveCollectiveRoundAction(1)));
            Check(m.Collective.RoundNumber == 1 && m.Collective.SelectionNumber == 2);
            Check(m.Collective.RemainingSeconds == 50);
        });
        Test("Timer decreases 5 seconds per tray and floors at 20", () =>
        {
            var m = Match(GameMode.Collective, cups: 4);
            for (int round = 1; round <= 12; round++)
            {
                Check(m.Collective.RoundNumber == round);
                Check(m.Collective.RemainingSeconds == Math.Max(20, 60 - (round - 1) * 5));
                Ok(m.AdvanceTime(m.Collective.RemainingSeconds));
            }
        });
        Test("Configured tray minimum replaces before exhausting leftovers", () =>
        {
            var config = new MatchConfig(GameMode.Collective, 2, new[] { new TrayConfiguration(6, 0) }, collectiveMinimumCups: 5);
            var m = new MatchEngine(config);
            ReserveReady(m, 1, 1);
            ReserveReady(m, 2, 2);
            Ok(m.ExecuteAction(new ResolveCollectiveRoundAction(1)));
            Check(m.Tray.TrayId == 2 && m.Collective.RemainingSeconds == 55);
        });
        Test("Timeout handles simultaneous death as Draw without extra tray", () =>
        {
            var m = Match(GameMode.Collective, players: 2, cups: 2, poisons: 2, lives: 1);
            Ok(m.AdvanceTime(60));
            Check(m.Status == MatchStatus.Ended && m.Outcome == MatchOutcome.Draw && m.Tray.TrayId == 1);
            Error(m.AdvanceTime(1), ErrorCode.MatchEnded);
        });
        Test("Timeout random cup choices are reproducible and observers cannot reenter", () =>
        {
            var a = Match(GameMode.Collective);
            var b = Match(GameMode.Collective);
            var first = new List<int>();
            var second = new List<int>();
            a.EventOccurred += e =>
            {
                if (e.Kind == GameEventKind.ReservationChanged) first.Add(e.CupId.Value);
                Error(a.AdvanceTime(100), ErrorCode.Busy);
                Check(a.Tray.TrayId == 2);
            };
            b.EventOccurred += e => { if (e.Kind == GameEventKind.ReservationChanged) second.Add(e.CupId.Value); };
            Ok(a.AdvanceTime(60));
            Ok(b.AdvanceTime(60));
            Check(a.NotificationErrors.Count == 0 && first.SequenceEqual(second) && first.Distinct().Count() == 8);
        });
        Test("Invalid elapsed time and tray thresholds rejected", () =>
        {
            var m = Match(GameMode.Collective);
            foreach (double seconds in new[] { -1.0, double.NaN, double.PositiveInfinity }) Error(m.AdvanceTime(seconds), ErrorCode.InvalidAction);
            Check(m.Collective.RemainingSeconds == 60 && m.Revision == 0);
            bool rejected = false;
            try { new MatchConfig(GameMode.Collective, 2, new[] { new TrayConfiguration(4, 0) }, collectiveMinimumCups: 5); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected);
        });
        Console.WriteLine($"RESULT {passed} passed / {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}


