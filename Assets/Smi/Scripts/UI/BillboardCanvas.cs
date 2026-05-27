using UnityEngine;

public class BillboardCanvas : MonoBehaviour
{
    GameObject m_camera;
    void Start()
    {
        m_camera = GameObject.FindWithTag("TowerCamera");
    }

    void LateUpdate()
    {
        if (m_camera == null)
            return;
        else
            transform.LookAt(transform.position + m_camera.gameObject.transform.rotation * -Vector3.forward, m_camera.gameObject.transform.rotation * Vector3.up);
    }
}
