using UnityEngine;

public class EnemyBillboard : MonoBehaviour
{
    private const string cameraTag = "PlayerCamera";

    private Transform m_cameraTransform;

    private void Start()
    {
        if (CameraReferences.Instance == null)
            return;

        m_cameraTransform = CameraReferences.Instance.PlayerCameraTransform;

        if(m_cameraTransform == null)
            Debug.LogWarning("PlayerCameraTransform is not assigned in CameraReferences.");
    }

    private void LateUpdate()
    {
        if (m_cameraTransform == null)
            return;

        transform.LookAt(
            transform.position + m_cameraTransform.rotation * -Vector3.forward,
            m_cameraTransform.rotation * Vector3.up
        );
    }
}
