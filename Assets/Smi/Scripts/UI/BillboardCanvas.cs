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
        transform.LookAt(transform.position + m_camera.gameObject.transform.rotation * -Vector3.forward, m_camera.gameObject.transform.rotation * Vector3.up);
    }
}
