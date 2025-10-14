using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class WinnerSelectionCards : NetworkBehaviour
{
    public enum CardType
    {
        Rock = 0,     // Камень
        Paper = 1,    // Бумага  
        Scissors = 2  // Ножницы
    }

    private Dictionary<ulong, ulong> playerSelections = new Dictionary<ulong, ulong>();
    private Dictionary<ulong, CardType> playerCardTypes = new Dictionary<ulong, CardType>();
    private void Start()
    {
        GlobalEventManager.OnSelectionCards.AddListener(OnSelectionCards);
    }

    public void OnSelectionCards(Dictionary<ulong, ulong> playerSelections)
    {
        this.playerSelections = playerSelections;

        DetermineCardTypes();
    }

    public void SelectWinner()
    {
        if (playerSelections.Count == 0) return;

        var players = new List<ulong>(playerSelections.Keys);
        SelectWinnerForTwoPlayers(players[0], players[1]);
    }

    private void SelectWinnerForTwoPlayers(ulong player1, ulong player2)
    {
        var card1 = playerCardTypes[player1];
        var card2 = playerCardTypes[player2];

        Debug.Log($"Сравниваем: {card1} vs {card2}");

        if (card1 == card2)
        {
            GlobalEventManager.OnWinnerSelected?.Invoke(ulong.MaxValue); // Ничья
            return;
        }

        ulong winner = DetermineWinner(card1, card2) ? player1 : player2;
        GlobalEventManager.OnWinnerSelected?.Invoke(winner);
    }
    private void DetermineCardTypes()
    {
        playerCardTypes.Clear();
        var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
        foreach (var selection in playerSelections)
        {
            // Находим карту по NetworkObjectId
            if (spawnedObjects.TryGetValue(selection.Value, out NetworkObject cardObject))
            {
                CardInteractable card = cardObject.GetComponent<CardInteractable>();
                if (card != null)
                {
                    playerCardTypes[selection.Key] = card.GetCardType();
                }
            }
        }
    }

    private bool DetermineWinner(CardType card1, CardType card2)
    {
        // Камень > Ножницы > Бумага > Камень
        return (card1 == CardType.Rock && card2 == CardType.Scissors) ||
               (card1 == CardType.Scissors && card2 == CardType.Paper) ||
               (card1 == CardType.Paper && card2 == CardType.Rock);
    }
}
