using UnityEngine;

public class BillboardCanvas : MonoBehaviour
{
    Transform m_camera;
    void Start()
    {
        m_camera = Camera.main.transform;
    }

    void LateUpdate()
    {
        transform.LookAt(transform.position + m_camera.rotation * -Vector3.forward, m_camera.rotation * Vector3.up);
    }
}
