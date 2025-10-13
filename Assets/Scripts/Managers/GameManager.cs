using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private CardManager cardManager;
    [SerializeField] private int playersRequired = 2;

    private NetworkVariable<GamePhase> currentPhase = new NetworkVariable<GamePhase>(GamePhase.WaitingForPlayers);
    private NetworkVariable<int> connectedPlayers = new NetworkVariable<int>(0);

    private Dictionary<ulong, ulong> playerSelections = new Dictionary<ulong, ulong>();
    private void Start()
    {
        GlobalEventManager.OnDealingComplete.AddListener(OnDealingComplete);
        GlobalEventManager.OnSelectionComplete.AddListener(OnSelectionComplete);
    }
    public override void OnNetworkSpawn()
    {
        if (IsHost)
        {
            currentPhase.OnValueChanged += OnPhaseChanged;
            connectedPlayers.Value = NetworkManager.Singleton.ConnectedClients.Count;
            CheckGameReady();
        }
    }
    public override void OnNetworkDespawn()
    {
        // Не забываем отписаться
        currentPhase.OnValueChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase previousPhase, GamePhase newPhase)
    {
        if (!IsServer) return;

        // Запускаем обработку новой фазы
        HandleNewPhase(newPhase);
    }

    // Обработка новой фазы
    private void HandleNewPhase(GamePhase phase)
    {
        // Обработка смены фаз только на сервере
        switch (currentPhase.Value)
        {
            case GamePhase.DealingCards:
                DealCards();
                break;
            case GamePhase.PlayerSelection:
                SelectionCards();
                break;
            case GamePhase.Resolution:
                ResolveRound();
                break;
        }
    }

    private void CheckGameReady()
    {
        if (IsServer && connectedPlayers.Value >= playersRequired)
        {
            StartCoroutine(StartGame());
        }
    }

    private IEnumerator StartGame()
    {
        Debug.Log("Все игроки подключены! Начинаем игру...");
        yield return new WaitForSeconds(1f);
        currentPhase.Value = GamePhase.DealingCards;
    }


    // Этап 2: Раздача карт
    private void DealCards()
    {
        cardManager.DealCards();
    }
    private void OnDealingComplete()
    {
        currentPhase.Value = GamePhase.PlayerSelection;
    }

    // Этап 3: Выбор карт
    private void SelectionCards()
    {
        cardManager.SelectionCards();

    }
    private void OnSelectionComplete(Dictionary<ulong, ulong> playerSelected)
    {
        playerSelections = playerSelected;
        currentPhase.Value = GamePhase.Resolution;
    }

    private void ResolveRound()
    {
        // Определяем победителя и начинаем новый раунд
        if (IsServer)
        {
            foreach (var selection in playerSelections)
            {
                Debug.Log($"Игрок {selection.Key} → Карта {selection.Value}");
            }

            Invoke(nameof(StartNewRound), 3f);
        }
    }

    private void StartNewRound()
    {
        if (IsServer)
        {
            currentPhase.Value = GamePhase.DealingCards;
        }
    }

    // Вызывается клиентами когда они сделали выбор
    [ServerRpc(RequireOwnership = false)]
    public void PlayerCardSelectedServerRpc(ulong playerId, ulong cardId)
    {
        //cardManager.RegisterPlayerSelection(playerId, cardId);
    }

    // Вызывается CardManager когда все игроки сделали выбор
    public void AllPlayersSelected()
    {
        if (IsServer)
        {
            currentPhase.Value = GamePhase.Resolution;
        }
    }
}