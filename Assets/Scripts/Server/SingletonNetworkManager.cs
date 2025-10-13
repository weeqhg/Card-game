using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class PersistentNetworkManager : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}