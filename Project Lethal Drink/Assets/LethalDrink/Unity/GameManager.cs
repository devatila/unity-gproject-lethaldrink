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
        [Tooltip("Ative para repetir o sorteio nos testes; desativado gera uma nova sequência por partida.")]
        [SerializeField] private bool useFixedSeed = false;
        [Tooltip("0 sorteia quem começa. 1–4 força um jogador apenas para testes.")]
        [SerializeField] private int startingPlayerOverride = 0;
        [SerializeField] private bool autoCreate = true;
        [Tooltip("0 usa a quantidade de vivos; um valor maior antecipa a troca de Bandeja.")]
        [SerializeField] private int collectiveMinimumCups = 0;
        [Tooltip("Ativa o prazo e a distribuição automática no Collective. Altere antes de criar a partida.")]
        [SerializeField] private bool collectiveTimerEnabled = false;
        [SerializeField] private float collectiveInitialSeconds = 60;
        [SerializeField] private float collectiveReductionSeconds = 5;
        [SerializeField] private float collectiveMinimumSeconds = 20;
        public MatchEngine Engine { get; private set; }
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void Start() { if (autoCreate) CreateDebugMatch(); }
        private void Update()
        {
            if (Engine == null || Engine.Collective == null || !Engine.Config.CollectiveTimerEnabled || Engine.Status != MatchStatus.Running) return;
            // Local authoritative session. Future NGO bridge must call this only on the host.
            long previousRevision = Engine.Revision;
            var result = Engine.AdvanceTime(Time.unscaledDeltaTime);
            if (!result.Success) Debug.LogWarning(result.ToString());
            if (Engine.Revision != previousRevision)
                foreach (var error in Engine.NotificationErrors) Debug.LogError("Ouvinte falhou: " + error.Message);
        }
        [ContextMenu("Debug/Create Match")]
        public void CreateDebugMatch()
        {
            try { StartSession(new MatchConfig(mode, playerCount, new[] { new TrayConfiguration(cupCount, poisonCount) }, useFixedSeed ? (int?)seed : null, startingLives,
                startingPlayerId: startingPlayerOverride == 0 ? (int?)null : startingPlayerOverride,
                collectiveMinimumCups: collectiveMinimumCups, collectiveInitialSeconds: collectiveInitialSeconds,
                collectiveReductionSeconds: collectiveReductionSeconds, collectiveMinimumSeconds: collectiveMinimumSeconds,
                collectiveTimerEnabled: collectiveTimerEnabled)); }
            catch (ArgumentException ex) { Debug.LogError(ex.Message); }
        }
        public void StartSession(MatchConfig config)
        {
            var replacement = new MatchEngine(config);
            Unsubscribe(); Engine = replacement; Engine.EventOccurred += HandleEvent;
            Debug.Log("Sessão criada: " + config.Mode + ". Primeiro jogador: " + (Engine.StartingPlayerId?.ToString() ?? "todos (Collective)"));
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
