using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class DealCards : NetworkBehaviour
{
    [SerializeField] private GameObject[] cardPrefab;
    [SerializeField] private int cardsPerPlayer = 3;
    [SerializeField] private Transform[] playerHands;

    private List<GameObject> cardsSpawn = new List<GameObject>();
    private int playersDealt = 0;
    public List<GameObject> GetCards() => cardsSpawn;
    public void StartDealCards()
    {
        // Очищаем старые карты перед новой раздачей
        ClearPreviousCards();
        playersDealt = 0;

        Debug.Log("Начинаем раздачу карт");

        // Раздаем карты всем подключенным игрокам
        for (int i = 0; i < NetworkManager.ConnectedClientsList.Count; i++)
        {
            ulong playerId = NetworkManager.ConnectedClientsList[i].ClientId;
            StartCoroutine(DealToPlayer(playerId, i));
        }
        Debug.Log("Я тут");
    }

    private IEnumerator DealToPlayer(ulong playerId, int playerIndex)
    {
        for (int i = 0; i < cardsPerPlayer; i++)
        {
            int randomIndex = Random.Range(0, cardPrefab.Length);
            // Создаем карту
            GameObject card = Instantiate(cardPrefab[randomIndex]);
            NetworkObject cardNetworkObject = card.GetComponent<NetworkObject>();

            // Спавним в сети
            cardNetworkObject.SpawnWithOwnership(playerId);

            // Позиционируем в руке игрока
            if (playerIndex < playerHands.Length && playerHands[playerIndex] != null)
            {
                card.transform.SetParent(playerHands[playerIndex]);
                card.transform.localPosition = new Vector3(i * 1.5f, 0, 0);
            }

            cardsSpawn.Add(card);
            // Ждем перед следующей картой
            yield return new WaitForSeconds(0.2f);
        }
        // Увеличиваем счетчик и проверяем всех ли обслужмли
        playersDealt++;
        CheckAllPlayersDealt();
        Debug.Log($"Раздано {cardsPerPlayer} карт игроку {playerId}");
    }

    private void CheckAllPlayersDealt()
    {
        if (playersDealt >= NetworkManager.ConnectedClientsList.Count)
        {
            Debug.Log("Вся раздача завершена!");
            GlobalEventManager.OnDealingComplete?.Invoke();
        }
    }

    private void ClearPreviousCards()
    {
        foreach (var card in cardsSpawn)
        {
            if (card != null)
                Destroy(card);
        }
        cardsSpawn.Clear();
    }
}
