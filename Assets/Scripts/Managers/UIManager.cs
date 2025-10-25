using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI winnerText;
    private string winner;

    [SerializeField] private List<Image> healthUiPlayer1;
    [SerializeField] private List<Image> healthUiPlayer2;

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

    public void ShowHealthUI(int player1, int player2)
    {
        // Для игрока 1
        if (healthUiPlayer1.Count > player1)
        {
            // Удаляем лишние объекты с конца списка
            for (int i = healthUiPlayer1.Count - 1; i >= player1; i--)
            {
                if (healthUiPlayer1[i] != null)
                {
                    Destroy(healthUiPlayer1[i]);
                }
                healthUiPlayer1.RemoveAt(i);
            }
        }

        // Для игрока 2
        if (healthUiPlayer2.Count > player2)
        {
            // Удаляем лишние объекты с конца списка
            for (int i = healthUiPlayer2.Count - 1; i >= player2; i--)
            {
                if (healthUiPlayer2[i] != null)
                {
                    Destroy(healthUiPlayer2[i]);
                }
                healthUiPlayer2.RemoveAt(i);
            }
        }
    }
}
