using UnityEngine;

public class CameraReferences : MonoBehaviour
{
    public static CameraReferences Instance { get; private set; }

    [SerializeField] private Transform playerCameraTransform;
    [SerializeField] private Transform towerCameraTransform;

    public Transform PlayerCameraTransform => playerCameraTransform;
    public Transform TowerCameraTransform => towerCameraTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
