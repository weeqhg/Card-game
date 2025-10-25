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


    private NetworkVariable<int> player1Health = new NetworkVariable<int>(3);
    private NetworkVariable<int> player2Health = new NetworkVariable<int>(3);
    private NetworkVariable<ulong> player1Id = new NetworkVariable<ulong>(ulong.MaxValue);
    private NetworkVariable<ulong> player2Id = new NetworkVariable<ulong>(ulong.MaxValue);


    private void Start()
    {
        GlobalEventManager.OnDealingComplete.AddListener(OnDealingComplete);
        GlobalEventManager.OnSelectionComplete.AddListener(OnSelectionComplete);
        GlobalEventManager.OnWinnerSelected.AddListener(OnShowResult);
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentPhase.OnValueChanged += OnPhaseChanged;
            InitializePlayers();
            CheckGameReady();
        }
        // 🔥 ДОБАВЛЕНО: Подписка на изменения здоровья
        player1Health.OnValueChanged += OnPlayer1HealthChanged;
        player2Health.OnValueChanged += OnPlayer2HealthChanged;
    }
    public override void OnNetworkDespawn()
    {
        // Не забываем отписаться
        currentPhase.OnValueChanged -= OnPhaseChanged;
        //Отписка от событий здоровья
        player1Health.OnValueChanged -= OnPlayer1HealthChanged;
        player2Health.OnValueChanged -= OnPlayer2HealthChanged;
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
            case GamePhase.GameOver:
                GameOver();
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
        while (currentPhase.Value == GamePhase.WaitingForPlayers)
        {
            connectedPlayers.Value = NetworkManager.Singleton.ConnectedClients.Count;
            if (connectedPlayers.Value >= playersRequired)
            {
                Debug.Log("Все игроки подключены! Начинаем игру...");
                currentPhase.Value = GamePhase.DealingCards;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    // 🔥 ДОБАВИТЬ: Метод сброса состояния игры
    private void ResetGameState()
    {
        if (!IsServer) return;

        player1Health.Value = 3;
        player2Health.Value = 3;

        Debug.Log("Состояние игры сброшено. Здоровье: 3/3");

        // Принудительно обновляем UI
        UpdateHealthUI();
    }

    //Инициализация игроков
    private void InitializePlayers()
    {
        player1Id.Value = 0;
        player2Id.Value = 1;

        player1Health.Value = 3;
        player2Health.Value = 3;

        Debug.Log($"Инициализировано здоровье: P1={player1Health.Value}, P2={player2Health.Value}");
        // Обновляем UI здоровья
        UpdateHealthUI();
    }

    private void OnPlayer1HealthChanged(int oldHealth, int newHealth)
    {
        UpdateHealthUI();
        CheckGameOver();
    }

    private void OnPlayer2HealthChanged(int oldHealth, int newHealth)
    {
        UpdateHealthUI();
        CheckGameOver();
    }

    //Проверка окончания игры
    private void CheckGameOver()
    {
        if (!IsServer) return;

        if (player1Health.Value <= 0 || player2Health.Value <= 0)
        {
            currentPhase.Value = GamePhase.GameOver;
        }
    }

    [ClientRpc]
    private void GameOverClientRpc(ulong winnerId)
    {
        if (winnerId == ulong.MaxValue)
        {
            uiManager.ShowWinnerText("Игра окончена: Ничья!");
        }
        else
        {
            bool isWinner = NetworkManager.Singleton.LocalClientId == winnerId;
            uiManager.ShowWinnerText(isWinner ? "Вы победили!" : "Вы проиграли!");
        }

        // Можно добавить перезапуск игры через несколько секунд
        if (IsServer)
        {
            Invoke("RestartGame", 5f);
        }
    }

    //Обновление UI здоровья
    private void UpdateHealthUI()
    {
        if (uiManager != null)
        {
            uiManager.ShowHealthUI(player1Health.Value, player2Health.Value);
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

    private void GameOver()
    {
        ulong winnerId = player1Health.Value > 0 ? player1Id.Value : player2Id.Value;
        GameOverClientRpc(winnerId);
    }
    private void OnShowResult(ulong player)
    {
        ulong winnerId = player;

        if (winnerId == player1Id.Value)
        {
            player2Health.Value -= 1; // Проиграл второй игрок
        }
        else if (winnerId == player2Id.Value)
        {
            player1Health.Value -= 1; // Проиграл первый игрок
        }

        OnShowResultClientRpc(winnerId);

        if (currentPhase.Value != GamePhase.GameOver)
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

    private void RestartGame()
    {
        if (IsServer)
        {
            // Останавливаем сеть и загружаем начальную сцену
            StartCoroutine(RestartGameCoroutine());
        }
    }
    private IEnumerator RestartGameCoroutine()
    {
        Debug.Log("Завершение игры и возврат в меню...");

        if (IsServer)
        {
            ResetGameState();
        }

        // Уведомляем клиентов
        GameRestartClientRpc();
        yield return new WaitForSeconds(2f);

        if (IsServer)
        {
            // 🔥 Безопасное отключение
            ShutdownNetwork();

            // Ждем полного отключения
            yield return new WaitUntil(() => !NetworkManager.Singleton.IsListening);
            yield return new WaitForSeconds(0.5f);

            // Загружаем сцену меню
            UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
        }
    }

    [ClientRpc]
    private void GameRestartClientRpc()
    {
        uiManager.ShowWinnerText("Возврат в главное меню...");

        // На клиентах тоже отключаемся
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(ClientShutdown());
        }
    }

    private IEnumerator ClientShutdown()
    {
        yield return new WaitForSeconds(1.5f);
        if (NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
        }
        yield return new WaitForSeconds(1.5f);
        UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
    }

    private void ShutdownNetwork()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("NetworkManager shutdown completed");
        }
    }
}