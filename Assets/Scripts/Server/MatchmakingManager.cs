using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class CompleteMatchmakingSystem : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;

    [Header("UI Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject searchingPanel;

    [Header("Login UI")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private TextMeshProUGUI loginStatusText;

    [Header("Menu UI")]
    [SerializeField] private TextMeshProUGUI welcomeText;
    [SerializeField] private Button findMatchButton;
    [SerializeField] private Button logoutButton;

    [Header("Searching UI")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI searchStatusText;
    [SerializeField] private TextMeshProUGUI searchTimerText;

    [Header("Game Settings")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private float lobbyUpdateInterval = 2f;

    private bool isSearching = false;
    private bool isInitialized = false;
    private Lobby connectedLobby;
    private Coroutine searchCoroutine;
    private float searchTimer = 0f;
    private string playerName = "";

    private const string LOBBY_RELAY_CODE_KEY = "RelayCode";
    private const string LOBBY_STATUS_KEY = "Status";
    private const string LOBBY_HOST_ID_KEY = "HostId";
    private const string PLAYER_NAME_KEY = "PlayerName";

    private async void Start()
    {
        ShowLoginPanel();

        // Настраиваем кнопки
        loginButton.onClick.AddListener(OnLoginClicked);
        findMatchButton.onClick.AddListener(OnFindMatchClicked);
        cancelButton.onClick.AddListener(OnCancelSearchClicked);
        logoutButton.onClick.AddListener(OnLogoutClicked);

        // Инициализируем Unity Services
        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            loginStatusText.text = "Инициализация сервисов...";

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            // Проверяем есть ли сохраненная сессия
            if (AuthenticationService.Instance.IsSignedIn)
            {
                // Автоматически входим с сохраненными данными
                playerName = PlayerPrefs.GetString("PlayerName", "Player");
                ShowMenuPanel();
                welcomeText.text = $"Добро пожаловать, {playerName}!";
                isInitialized = true;
            }
            else
            {
                loginStatusText.text = "Введите имя игрока";
                loginButton.interactable = true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка инициализации: {e.Message}");
            loginStatusText.text = $"Ошибка: {e.Message}";
        }
    }

    private async void OnLoginClicked()
    {
        string inputName = playerNameInput.text.Trim();

        if (string.IsNullOrEmpty(inputName))
        {
            loginStatusText.text = "Введите имя игрока";
            return;
        }

        if (inputName.Length < 2 || inputName.Length > 20)
        {
            loginStatusText.text = "Имя должно быть от 2 до 20 символов";
            return;
        }

        loginButton.interactable = false;
        loginStatusText.text = "Регистрация...";

        try
        {
            playerName = inputName;

            // Аутентифицируем игрока
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // Сохраняем имя игрока
            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.Save();

            isInitialized = true;
            ShowMenuPanel();
            welcomeText.text = $"Добро пожаловать, {playerName}!";
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка входа: {e.Message}");
            loginStatusText.text = $"Ошибка входа: {e.Message}";
            loginButton.interactable = true;
        }
    }

    private async void OnFindMatchClicked()
    {
        if (isSearching || !isInitialized) return;

        isSearching = true;
        searchTimer = 0f;
        ShowSearchingPanel();
        searchStatusText.text = "Поиск противника...";

        try
        {
            // Ищем доступные лобби или создаем новое
            Lobby lobby = await FindOrCreateLobby();

            if (lobby != null)
            {
                connectedLobby = lobby;
                searchCoroutine = StartCoroutine(SearchAndMatchCoroutine());

                // Запускаем heartbeat если мы хост
                if (connectedLobby.HostId == AuthenticationService.Instance.PlayerId)
                {
                    heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка поиска игры: {e.Message}");
            searchStatusText.text = "Ошибка поиска";
            ResetSearch();
        }
    }

    private async Task<Lobby> FindOrCreateLobby()
    {
        try
        {
            searchStatusText.text = "Поиск доступных игр...";

            // Используем правильные фильтры для поиска лобби
            var queryOptions = new QueryLobbiesOptions
            {
                Count = 10,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.AvailableSlots,
                        op: QueryFilter.OpOptions.GT,
                        value: "0"),
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.IsLocked,
                        op: QueryFilter.OpOptions.EQ,
                        value: "0")
                }
            };

            var queryResult = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

            // Выбираем первое подходящее лобби
            if (queryResult.Results.Count > 0)
            {
                // Ищем лобби в статусе "waiting"
                foreach (var lobby in queryResult.Results)
                {
                    if (lobby.Data != null &&
                        lobby.Data.ContainsKey(LOBBY_STATUS_KEY) &&
                        lobby.Data[LOBBY_STATUS_KEY].Value == "waiting")
                    {
                        searchStatusText.text = "Найдена игра! Присоединяемся...";
                        return await JoinLobby(lobby);
                    }
                }

                // Если не нашли лобби в статусе waiting, берем первое доступное
                var availableLobby = queryResult.Results[0];
                searchStatusText.text = "Найдена игра! Присоединяемся...";
                return await JoinLobby(availableLobby);
            }
            else
            {
                searchStatusText.text = "Создание новой игры...";
                return await CreateLobby();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка поиска лобби: {e.Message}");

            // При ошибке поиска создаем новое лобби
            searchStatusText.text = "Создание новой игры...";
            return await CreateLobby();
        }
    }

    private async Task<Lobby> CreateLobby()
    {
        try
        {
            var createOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = CreatePlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    { LOBBY_STATUS_KEY, new DataObject(DataObject.VisibilityOptions.Public, "waiting") },
                    { LOBBY_HOST_ID_KEY, new DataObject(DataObject.VisibilityOptions.Public, AuthenticationService.Instance.PlayerId) },
                    { "Version", new DataObject(DataObject.VisibilityOptions.Public, "1.0") },
                    { "GameMode", new DataObject(DataObject.VisibilityOptions.Public, "RPS") }
                }
            };

            string lobbyName = $"{playerName}_Lobby_{System.DateTime.UtcNow.Ticks}";
            var lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createOptions);

            searchStatusText.text = "Ожидание противника...";
            Debug.Log($"Лобби создано: {lobby.Id}, Игроков: {lobby.Players.Count}/{maxPlayers}");

            return lobby;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка создания лобби: {e.Message}");
            throw;
        }
    }

    private async Task<Lobby> JoinLobby(Lobby lobby)
    {
        try
        {
            // Проверяем что в лобби есть свободные места
            if (lobby.AvailableSlots <= 0)
            {
                throw new System.Exception("В лобби нет свободных мест");
            }

            var joinOptions = new JoinLobbyByIdOptions
            {
                Player = CreatePlayer()
            };

            var joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, joinOptions);
            searchStatusText.text = "Присоединились к игре!";
            Debug.Log($"Присоединились к лобби: {joinedLobby.Id}, Игроков: {joinedLobby.Players.Count}/{maxPlayers}");

            return joinedLobby;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка присоединения к лобби {lobby.Id}: {e.Message}");

            // Если не удалось присоединиться, создаем новое лобби
            searchStatusText.text = "Не удалось присоединиться, создаем новую игру...";
            return await CreateLobby();
        }
    }

    private Player CreatePlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerId", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, AuthenticationService.Instance.PlayerId) },
                { PLAYER_NAME_KEY, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                { "Ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false") }
            }
        };
    }

    private IEnumerator SearchAndMatchCoroutine()
    {
        while (isSearching && connectedLobby != null)
        {
            yield return new WaitForSeconds(lobbyUpdateInterval);

            // Обновляем информацию о лобби
            var updateTask = UpdateLobbyInfo();
            yield return new WaitUntil(() => updateTask.IsCompleted);

            // Обновляем UI
            searchTimer += lobbyUpdateInterval;
            UpdateSearchTimer();
        }
    }

    private async Task UpdateLobbyInfo()
    {
        try
        {
            connectedLobby = await LobbyService.Instance.GetLobbyAsync(connectedLobby.Id);

            int playerCount = connectedLobby.Players.Count;
            searchStatusText.text = $"Игроков: {playerCount}/{maxPlayers}";

            // Если лобби заполнено, начинаем процесс создания игры
            if (playerCount >= maxPlayers)
            {
                await StartGame();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка обновления лобби: {e.Message}");
        }
    }

    private async Task StartGame()
    {
        // Простая логика: создатель лобби всегда хост
        bool isHost = connectedLobby.HostId == AuthenticationService.Instance.PlayerId;

        searchStatusText.text = isHost ?
            "Вы - хост! Создание игры..." : "Подключение к хосту...";

        // Даем время на обновление UI
        await Task.Delay(1000);

        if (isHost)
        {
            await CreateGameAsHost();
        }
        else
        {
            await JoinGameAsClient();
        }
    }

    private async Task CreateGameAsHost()
    {
        try
        {
            searchStatusText.text = "Создание игры...";

            // 1. Сначала создаем Relay комнату
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string relayCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // 2. Сохраняем Relay код в лобби ДО подключения
            var updateOptions = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { LOBBY_RELAY_CODE_KEY, new DataObject(DataObject.VisibilityOptions.Member, relayCode) },
                    { LOBBY_STATUS_KEY, new DataObject(DataObject.VisibilityOptions.Public, "ingame") }
                },
                IsLocked = true
            };

            // Обновляем лобби перед подключением
            await LobbyService.Instance.UpdateLobbyAsync(connectedLobby.Id, updateOptions);

            // 3. Ждем некоторое время чтобы клиенты успели получить Relay код
            searchStatusText.text = "Ожидание подключения игроков...";
            await Task.Delay(3000);

            // 4. Настраиваем и запускаем хост ПОСЛЕ обновления лобби
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            if (NetworkManager.Singleton.StartHost())
            {
                searchStatusText.text = "Игра создана! Запуск...";

                // 5. Ждем еще немного чтобы клиенты точно подключились
                await Task.Delay(2000);

                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка создания игры: {e.Message}");
            searchStatusText.text = "Ошибка создания игры";
            ResetSearch();
        }
    }

    private async Task JoinGameAsClient()
    {
        try
        {
            searchStatusText.text = "Подключение к игре...";

            // Ждем пока хост создаст Relay комнату
            string relayCode = await WaitForRelayCode();

            if (!string.IsNullOrEmpty(relayCode))
            {
                searchStatusText.text = "Подключение к Relay...";

                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayCode);

                // Настраиваем и запускаем клиент
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData
                );

                if (NetworkManager.Singleton.StartClient())
                {
                    searchStatusText.text = "Подключено! Ожидание начала игры...";

                    // Ждем загрузки сцены хоста
                    await Task.Delay(3000);

                    // Клиент автоматически загрузит сцену когда хост ее загрузит
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка подключения к игре: {e.Message}");
            searchStatusText.text = "Ошибка подключения";
            ResetSearch();
        }
    }

    private async Task<string> WaitForRelayCode()
    {
        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            await Task.Delay(2000);
            timer += 2f;

            try
            {
                connectedLobby = await LobbyService.Instance.GetLobbyAsync(connectedLobby.Id);

                if (connectedLobby.Data != null &&
                    connectedLobby.Data.ContainsKey(LOBBY_RELAY_CODE_KEY))
                {
                    string relayCode = connectedLobby.Data[LOBBY_RELAY_CODE_KEY].Value;
                    if (!string.IsNullOrEmpty(relayCode))
                    {
                        searchStatusText.text = "Relay код получен!";
                        return relayCode;
                    }
                }

                searchStatusText.text = $"Ожидание создания игры хостом... {timer:0}с";
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Ошибка ожидания Relay: {e.Message}");
            }
        }

        throw new System.TimeoutException("Хост не создал игру вовремя");
    }

    private void OnCancelSearchClicked()
    {
        ResetSearch();
        searchStatusText.text = "Поиск отменен";
    }

    private void OnLogoutClicked()
    {
        AuthenticationService.Instance.SignOut(true);
        PlayerPrefs.DeleteKey("PlayerName");
        ShowLoginPanel();
        playerNameInput.text = "";
        loginStatusText.text = "Введите имя игрока";
    }

    private async void ResetSearch()
    {
        isSearching = false;
        searchTimer = 0f;

        // Останавливаем корутины
        if (searchCoroutine != null) StopCoroutine(searchCoroutine);
        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);

        if (connectedLobby != null)
        {
            try
            {
                // Сначала проверяем существует ли лобби
                try
                {
                    // Пытаемся получить информацию о лобби
                    var existingLobby = await LobbyService.Instance.GetLobbyAsync(connectedLobby.Id);

                    // Если лобби существует, выходим из него
                    if (connectedLobby.HostId == AuthenticationService.Instance.PlayerId)
                    {
                        await LobbyService.Instance.DeleteLobbyAsync(connectedLobby.Id);
                        Debug.Log("Лобби удалено (хост вышел)");
                    }
                    else
                    {
                        await LobbyService.Instance.RemovePlayerAsync(connectedLobby.Id, AuthenticationService.Instance.PlayerId);
                        Debug.Log("Игрок вышел из лобби");
                    }
                }
                catch (System.Exception getLobbyException)
                {
                    // Если лобби не найдено, просто логируем и продолжаем
                    if (getLobbyException.Message.Contains("not found"))
                    {
                        Debug.Log("Лобби уже было удалено, пропускаем выход");
                    }
                    else
                    {
                        throw; // Перебрасываем другие исключения
                    }
                }
            }
            catch (System.Exception e)
            {
                // Игнорируем ошибки "lobby not found", так как лобби уже удалено
                if (!e.Message.Contains("not found"))
                {
                    Debug.LogError($"Ошибка выхода из лобби: {e.Message}");
                }
                else
                {
                    Debug.Log("Лобби уже было удалено");
                }
            }

            connectedLobby = null;
        }

        if (NetworkManager.Singleton != null)
        {
            //NetworkManager.Singleton.Shutdown();
        }

        ShowMenuPanel();
    }

    private void UpdateSearchTimer()
    {
        if (searchTimerText != null)
        {
            searchTimerText.text = $"Поиск: {searchTimer:0}с";
        }
    }

    private void ShowLoginPanel()
    {
        loginPanel.SetActive(true);
        menuPanel.SetActive(false);
        searchingPanel.SetActive(false);
    }

    private void ShowMenuPanel()
    {
        if (loginPanel != null)
            loginPanel.SetActive(false);
        if (menuPanel != null)
            menuPanel.SetActive(true);
        if (searchingPanel != null)
            searchingPanel.SetActive(false);
    }

    private void ShowSearchingPanel()
    {
        loginPanel.SetActive(false);
        menuPanel.SetActive(false);
        searchingPanel.SetActive(true);
    }

    private void OnDestroy()
    {
        ResetSearch();
    }

    private Coroutine heartbeatCoroutine;

    private IEnumerator HeartbeatCoroutine()
    {
        while (isSearching && connectedLobby != null)
        {
            yield return new WaitForSeconds(15f);

            if (connectedLobby.HostId == AuthenticationService.Instance.PlayerId)
            {
                var heartbeatTask = SendHeartbeatAsync();
                yield return new WaitUntil(() => heartbeatTask.IsCompleted);
            }
        }
    }

    private async Task SendHeartbeatAsync()
    {
        try
        {
            if (connectedLobby != null)
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(connectedLobby.Id);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка heartbeat: {e.Message}");
        }
    }
}