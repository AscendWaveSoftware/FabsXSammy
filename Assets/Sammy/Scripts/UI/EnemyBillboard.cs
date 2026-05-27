using UnityEngine;

public class EnemyBillboard : MonoBehaviour
{
    private const string cameraTag = "PlayerCamera";

    private Transform m_cameraTransform;

    private void Start()
    {
        GameObject cameraObject = GameObject.FindWithTag(cameraTag);

        if(cameraObject != null)
            m_cameraTransform = cameraObject.transform;
        else
            Debug.LogWarning($"No camera found with tag: {cameraTag}");
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
