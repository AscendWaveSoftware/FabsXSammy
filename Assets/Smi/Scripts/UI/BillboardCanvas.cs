using UnityEngine;

public class BillboardCanvas : MonoBehaviour
{
    private Transform m_camera;

    void LateUpdate()
    {

        GameObject camObj = GameObject.FindGameObjectWithTag("TowerCamera");

        if (camObj == null || !camObj.activeInHierarchy)
            return;

        transform.forward = camObj.transform.forward;

    }
}