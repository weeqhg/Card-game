using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SelectCards : NetworkBehaviour
{
    [SerializeField] private float selectionTime = 60f;

    private List<GameObject> availableCards = new List<GameObject>();
    private Dictionary<ulong, ulong> playerSelections = new Dictionary<ulong, ulong>();


    private bool selectionActive = false;
    public void StartSelectCards(List<GameObject> cardsPrefab)
    {
        availableCards = cardsPrefab;
        playerSelections.Clear();
        selectionActive = true;
        // Включаем интерактивность карт
        SetCardsInteractable(true);

        // Запускаем таймер выбора
        StartCoroutine(SelectionTimer());
    }

    private IEnumerator SelectionTimer()
    {
        Debug.Log($"На выбор карт дается {selectionTime} секунд");
        yield return new WaitForSeconds(selectionTime);

        // Если время вышло, принудительно завершаем
        if (selectionActive)
        {
            Debug.Log("Время выбора истекло!");
            CompleteSelection();
        }
    }
    private void CompleteSelection()
    {
        if (!selectionActive) return;

        selectionActive = false;
        SetCardsInteractable(false);

        StopAllCoroutines();

        // Отписываемся от событий
        foreach (GameObject cardObject in availableCards)
        {
            CardInteractable card = cardObject.GetComponent<CardInteractable>();
            if (card != null)
            {
                card.SetInteractable(false);
                card.OnCardSelected -= OnCardSelected;
            }
        }

        Debug.Log("Фаза выбора карт завершена!");
       
        // Уведомляем GameManager о завершении выбора
        GlobalEventManager.OnSelectionCards?.Invoke(playerSelections);
        GlobalEventManager.OnSelectionComplete?.Invoke();
    }

    private void SetCardsInteractable(bool interactable)
    {
        foreach (GameObject cardObject in availableCards)
        {
            CardInteractable card = cardObject.GetComponent<CardInteractable>();
            if (card != null)
            {
                card.SetInteractable(interactable);
                card.OnCardSelected += OnCardSelected;
            }
        }
    }

    // Вызывается когда игрок выбирает карту
    private void OnCardSelected(ulong playerId, ulong cardId)
    {
        if (!selectionActive || !IsServer) return;

        // Проверяем, не выбрал ли уже игрок карту
        if (playerSelections.ContainsKey(playerId))
        {
            Debug.Log($"Игрок {playerId} уже выбрал карту!");
            return;
        }

        // Проверяем, не выбрана ли уже эта карта
        if (playerSelections.ContainsValue(cardId))
        {
            Debug.Log($"Карта {cardId} уже выбрана другим игроком!");
            return;
        }

        // Регистрируем выбор
        playerSelections[playerId] = cardId;
        Debug.Log($"Игрок {playerId} выбрал карту {cardId}");


        // Проверяем, все ли игроки сделали выбор
        if (playerSelections.Count >= NetworkManager.Singleton.ConnectedClientsList.Count)
        {
            Debug.Log("Все игроки сделали выбор!");
            CompleteSelection();
        }
    }
}
