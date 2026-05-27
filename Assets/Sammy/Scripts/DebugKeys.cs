using UnityEngine;
using UnityEngine.InputSystem;

public class DebugKey : MonoBehaviour
{
    [SerializeField] private PlayerResources playerResources;
    [SerializeField] private FactoryUnitSpawner unitSpawner;
    [SerializeField] private GameObject playerCamera;
    [SerializeField] private GameObject towerCamera;

    private bool playerCameraActive = true;

    private void Start()
    {

    }

    void Update()
    {
        if (Keyboard.current.nKey.wasPressedThisFrame)
            playerResources.AddGold(25, Resources.GOLD);

        if (Keyboard.current.mKey.wasPressedThisFrame)
            playerResources.DecreaseGold(25, Resources.GOLD);

        if (Keyboard.current.vKey.wasPressedThisFrame)
            playerResources.AddScrap(25, Resources.SCRAP);

        if (Keyboard.current.bKey.wasPressedThisFrame)
            playerResources.DecreaseScrap(25, Resources.SCRAP);

        if (Keyboard.current.cKey.wasPressedThisFrame)
            ChangeCamera();

        if (Keyboard.current.eKey.wasPressedThisFrame && unitSpawner != null && playerResources.CurrentScrap >= 15)
        {
                playerResources.DecreaseScrap(25, Resources.SCRAP);
                unitSpawner.GetMinion(unitSpawner.m_minionPrefab);
        }
    }

    void ChangeCamera()
    {
        playerCameraActive = !playerCameraActive;

        SetCameraState(playerCameraActive);
    }

    void SetCameraState(bool _playerCameraActive)
    {
        if (playerCamera != null)
            playerCamera.gameObject.SetActive(_playerCameraActive);

        if (towerCamera != null)
            towerCamera.gameObject.SetActive(!_playerCameraActive);
    }
}
