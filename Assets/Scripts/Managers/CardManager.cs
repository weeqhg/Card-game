using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CardManager : NetworkBehaviour
{
    [SerializeField] private DealCards dealCards;
    [SerializeField] private SelectCards selectCards;
    [SerializeField] private WinnerSelectionCards winnerSelectionCards;

    private void Start()
    {
        dealCards = GetComponent<DealCards>();
        selectCards = GetComponent<SelectCards>();
        winnerSelectionCards = GetComponent<WinnerSelectionCards>();
    }
    public void DealCards()
    {
        dealCards.StartDealCards();
    }

    public void SelectionCards()
    {
        if (dealCards.GetCards().Count == 0)
        {
            Debug.LogWarning("Попытка начать выбор карт, но карты не разданы!");
            return;
        }
        selectCards.StartSelectCards(dealCards.GetCards());
    }

    public void WinnerSelectionCards()
    {
        winnerSelectionCards.SelectWinner();
    }
}