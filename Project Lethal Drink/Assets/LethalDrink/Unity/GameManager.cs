using System;
using LethalDrink.Core;
using LethalDrink.Gameplay;
using UnityEngine;

namespace LethalDrink.Unity
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        [SerializeField] private GameMode mode = GameMode.Classic;
        [SerializeField] private int playerCount = 4;
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int cupCount = 8;
        [SerializeField] private int poisonCount = 3;
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool autoCreate = true;
        [Tooltip("Experimento: Recusa segura mantém ofertante; veneno passa ao próximo vivo. Não é design final.")]
        [SerializeField] private bool testRefusalAsDrink = false;
        public MatchEngine Engine { get; private set; }
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void Start() { if (autoCreate) CreateDebugMatch(); }
        [ContextMenu("Debug/Create Match")]
        public void CreateDebugMatch()
        {
            try { StartSession(new MatchConfig(mode, playerCount, new[] { new TrayConfiguration(cupCount, poisonCount) }, seed, startingLives,
                refusalSuccession: testRefusalAsDrink ? RefusalSuccession.ResolveAsOriginatorDrink : RefusalSuccession.Undecided)); }
            catch (ArgumentException ex) { Debug.LogError(ex.Message); }
        }
        public void StartSession(MatchConfig config)
        {
            var replacement = new MatchEngine(config);
            Unsubscribe(); Engine = replacement; Engine.EventOccurred += HandleEvent;
            Debug.Log("Sessão criada: " + config.Mode);
        }
        public ActionResult ExecuteAction(IGameAction action)
        {
            var result = Engine == null ? ActionResult.Fail(ErrorCode.WrongPhase, "Crie a partida.") : Engine.ExecuteAction(action);
            Debug.Log(result.ToString());
            if (Engine != null) foreach (var error in Engine.NotificationErrors) Debug.LogError("Ouvinte falhou: " + error.Message);
            return result;
        }
        private void HandleEvent(GameEvent e) => Debug.Log($"[{e.Revision}] {e.Kind} Player={e.PlayerId} Target={e.TargetId} Cup={e.CupId} Value={e.Value} Tray={e.TrayId}");
        private void Unsubscribe() { if (Engine != null) Engine.EventOccurred -= HandleEvent; }
        private void OnDestroy() { Unsubscribe(); if (Instance == this) Instance = null; }
    }
}
