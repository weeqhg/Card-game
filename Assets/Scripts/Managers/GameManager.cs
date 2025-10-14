using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private CardManager cardManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private int playersRequired = 2;

    private NetworkVariable<GamePhase> currentPhase = new NetworkVariable<GamePhase>(GamePhase.WaitingForPlayers);
    private NetworkVariable<int> connectedPlayers = new NetworkVariable<int>(0);

    private void Start()
    {
        GlobalEventManager.OnDealingComplete.AddListener(OnDealingComplete);
        GlobalEventManager.OnSelectionComplete.AddListener(OnSelectionComplete);
        GlobalEventManager.OnWinnerSelected.AddListener(OnShowResult);
    }
    public override void OnNetworkSpawn()
    {
        if (IsHost)
        {
            currentPhase.OnValueChanged += OnPhaseChanged;
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
        if (IsServer)
        {
            Debug.Log("тук тук");
            StartCoroutine(StartGame());
        }
    }

    private IEnumerator StartGame()
    {
        yield return new WaitForSeconds(10f);
        connectedPlayers.Value = NetworkManager.Singleton.ConnectedClients.Count;
        if (connectedPlayers.Value >= playersRequired)
        {
            Debug.Log("Все игроки подключены! Начинаем игру...");
            currentPhase.Value = GamePhase.DealingCards;
        }
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
    private void OnSelectionComplete()
    {
        currentPhase.Value = GamePhase.Resolution;
    }

    private void ResolveRound()
    {
        // Определяем победителя и начинаем новый раунд
        if (IsServer)
        {
            cardManager.WinnerSelectionCards();
        }
    }
    
    private void OnShowResult(ulong player)
    {
        ulong winnerId = player;

        OnShowResultClientRpc(winnerId);

        Invoke("StartNewRound", 2f);
    }


    [ClientRpc]
    private void OnShowResultClientRpc(ulong winnerId)
    {
        // Netcode автоматически вызывает это на всех клиентах
        ShowResultUI(winnerId);
    }
    private void ShowResultUI(ulong winnerId)
    {
        if (winnerId == ulong.MaxValue)
        {
            uiManager.ShowWinnerText("ничья");
        }
        else
        {
            bool isWinner = NetworkManager.Singleton.LocalClientId == winnerId;
            uiManager.ShowWinnerText(isWinner ? "победа!" : "проигрыш");
        }
    }

    private void StartNewRound()
    {
        if (IsServer)
        {
            currentPhase.Value = GamePhase.DealingCards;
        }
    }
}