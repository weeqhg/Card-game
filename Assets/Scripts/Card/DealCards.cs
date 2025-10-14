using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DealCards : NetworkBehaviour
{
    [SerializeField] private GameObject[] cardPrefab;
    [SerializeField] private int cardsPerPlayer = 3;
    [SerializeField] private Transform[] playerHands;

    private List<GameObject> cardsSpawn = new List<GameObject>();
    private int playersDealt = 0;
    public List<GameObject> GetCards() => cardsSpawn;

    [SerializeField] private float dealDuration = 0.5f;
    [SerializeField] private float dealDelay = 0.2f;
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
        int randomIndex = 0;
        for (int i = 0; i < cardsPerPlayer; i++)
        {
            if (i >= 0 && i < 3) randomIndex = i;
            else randomIndex = 0;
            // Создаем карту
            GameObject card = Instantiate(cardPrefab[randomIndex]);
            NetworkObject cardNetworkObject = card.GetComponent<NetworkObject>();

            // Начальная позиция
            Vector3 startPosition = GetStartPosition(playerIndex);
            card.transform.position = startPosition;

            // Спавним в сети
            cardNetworkObject.SpawnWithOwnership(playerId);

            // Сразу устанавливаем родителя на сервере
            if (playerIndex < playerHands.Length && playerHands[playerIndex] != null)
            {
                card.transform.SetParent(playerHands[playerIndex]);
            }


            // Ждем спавна на всех клиентах
            yield return new WaitUntil(() => cardNetworkObject.IsSpawned);

            // Получаем целевую позицию (локальную относительно руки)
            Vector3 targetLocalPosition = GetCardPositionInHand(i, playerIndex);
            int handIndex = playerIndex; // Сохраняем для ClientRpc

            // Запускаем анимацию на ВСЕХ клиентах
            PlayDealAnimationClientRpc(cardNetworkObject.NetworkObjectId, targetLocalPosition, playerIndex);
            cardsSpawn.Add(card);
            // Ждем перед следующей картой
            yield return new WaitForSeconds(0.2f);
        }
        // Увеличиваем счетчик и проверяем всех ли обслужмли
        playersDealt++;
        CheckAllPlayersDealt();
        Debug.Log($"Раздано {cardsPerPlayer} карт игроку {playerId}");
    }
    private Vector3 GetStartPosition(int playerIndex)
    {
        Vector3 tableCenter = Vector3.zero;
        Vector3 offset = playerIndex == 0 ? new Vector3(-3f, 3f, 0f) : new Vector3(3f, 3f, 0f);
        return tableCenter + offset;
    }
    private Vector3 GetCardPositionInHand(int cardIndex, int playerIndex)
    {
        // Локальные координаты относительно руки
        float totalWidth = (cardsPerPlayer - 1) * 2f;
        float startX = -totalWidth / 2f;
        float xPosition = startX + (cardIndex * 2f);

        return new Vector3(xPosition, 0, 0);
    }

    [ClientRpc]
    private void PlayDealAnimationClientRpc(ulong cardId, Vector3 targetLocalPosition, int handIndex)
    {
        // Находим карту по NetworkObjectId
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardId, out NetworkObject cardObject))
        {
            PlayDealAnimation(cardObject.transform, targetLocalPosition, handIndex);
        }
    }

    private void PlayDealAnimation(Transform cardTransform, Vector3 targetLocalPosition, int handIndex)
    {
        // Карта уже имеет правильного родителя (синхронизировано сервером)
        // Начальная позиция для анимации (над рукой)
        Vector3 startLocalPosition = targetLocalPosition + new Vector3(0, 3f, -2f);
        cardTransform.localPosition = startLocalPosition;
        cardTransform.localRotation = Quaternion.Euler(0, 180, 0);

        Sequence dealSequence = DOTween.Sequence();

        // Анимация к целевой позиции в локальных координатах
        dealSequence.Append(cardTransform.DOLocalMove(targetLocalPosition, dealDuration).SetEase(Ease.OutCubic));
        dealSequence.Join(cardTransform.DOLocalRotate(Vector3.zero, dealDuration));

        // Эффекты анимации
        dealSequence.Join(cardTransform.DOScale(1.1f, dealDuration * 0.5f));
        dealSequence.Append(cardTransform.DOScale(1f, dealDuration * 0.5f));
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
