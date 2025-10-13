using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public enum GamePhase
{
    WaitingForPlayers,
    DealingCards,
    PlayerSelection,
    Resolution,
    GameOver
}

public static class GlobalEventManager
{
    public static readonly UnityEvent OnDealingComplete = new UnityEvent();
    public static readonly UnityEvent<Dictionary<ulong, ulong>> OnSelectionComplete = new UnityEvent<Dictionary<ulong, ulong>>();
}
