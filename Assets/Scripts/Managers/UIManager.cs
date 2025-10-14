using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class UIManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI winnerText;

    private string winner;

    public void ShowWinnerText(string winner)
    {
        this.winner = winner;
        StartCoroutine(ShowAnimation());
    }    

    private IEnumerator ShowAnimation()
    {
        winnerText.text = winner;
        yield return new WaitForSeconds(1f);
        winner = "";
        winnerText.text = winner;
    }
}
