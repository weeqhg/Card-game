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
            SetupPlayerPerspective();
            // Активируем нашу камеру и управление
            if (playerCamera != null)
                playerCamera.SetActive(true);
        }
        else
        {
            if (playerCamera != null)
                playerCamera.SetActive(false);
        }

    }

    private void SetupPlayerPerspective()
    {
        if (IsHost)
        {
            playerCamera.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
        if (!IsHost)
        {
            playerCamera.transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);
        }
    }
}