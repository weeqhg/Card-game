using Unity.Netcode;
using UnityEngine;

public class PlayerCameraManager : NetworkBehaviour
{
    [SerializeField] GameObject playerCamera;
    private float rotationAngle = 180f;
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Активируем нашу камеру и управление
            if (playerCamera != null)
                playerCamera.SetActive(true);
        }
        else
        {
            if (playerCamera != null)
                playerCamera.SetActive(false);
        }

        SetupPlayerPerspective();
    }

    private void SetupPlayerPerspective()
    {
        if (IsHost)
        {
            playerCamera.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
        else
        {
            playerCamera.transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);
        }
    }
}