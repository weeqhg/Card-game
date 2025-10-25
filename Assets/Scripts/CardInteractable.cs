using DG.Tweening;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using static WinnerSelectionCards;
public class CardInteractable : NetworkBehaviour
{
    [SerializeField] private GameObject cardBack;
    [SerializeField] private GameObject cardFront;

    [SerializeField] private CardType cardType; // Добавьте это поле
    public CardType GetCardType() => cardType;

    private NetworkVariable<bool> isFaceUp = new NetworkVariable<bool>(false);

    private NetworkVariable<bool> isInteractableNet = new NetworkVariable<bool>(false);
    private bool isInteractable = false;

    // Событие только для сервера
    public event System.Action<ulong, ulong> OnCardSelected;
    public override void OnNetworkSpawn()
    {
        isInteractableNet.OnValueChanged += OnInteractableChanged;
        UpdateAppearance();
    }
    public override void OnNetworkDespawn()
    {
        // Не забываем отписаться
        isInteractableNet.OnValueChanged -= OnInteractableChanged;
    }

    private void OnInteractableChanged(bool oldValue, bool newValue)
    {
        isInteractable = newValue;
    }
    private void UpdateAppearance()
    {
        if (IsOwner)
        {
            // Владелец видит карту открытой
            cardBack.SetActive(false);
            cardFront.SetActive(true);
        }
        else
        {
            // Остальные видят рубашку
            cardBack.SetActive(true);
            cardFront.SetActive(false);
        }
    }

    public void SetInteractable(bool interactable)
    {
        isInteractableNet.Value = interactable;
    }

    private void OnMouseDown()
    {
        if (!isInteractable || !IsOwner) return;

        // Переворачиваем карту при клике
        FlipCardServerRpc();

        // Игрок выбирает карту
        SelectCard();
    }
    private void SelectCard()
    {
        // ✅ ВСЕГДА отправляем ServerRpc, даже если мы хост
        SelectCardServerRpc(OwnerClientId, NetworkObjectId);
    }
    [ServerRpc]
    private void FlipCardServerRpc()
    {
        isFaceUp.Value = !isFaceUp.Value;
    }
    [ServerRpc]
    private void SelectCardServerRpc(ulong playerId, ulong cardId)
    {
        OnCardSelected?.Invoke(playerId, cardId);
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);
    }
}