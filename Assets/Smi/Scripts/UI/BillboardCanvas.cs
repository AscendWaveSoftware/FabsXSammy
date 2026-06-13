using UnityEngine;
using UnityEngine.UI;

public class BillboardCanvas : MonoBehaviour
{
    private Transform m_camera;
    Slider slider;

    private void Awake()
    {
        slider = GetComponentInChildren<Slider>();
    }

    void LateUpdate()
    {
        GameObject camObj = GameObject.FindGameObjectWithTag("TowerCamera");

        if (camObj == null || !camObj.activeInHierarchy)
        {

            slider.gameObject.SetActive(false);
            return;
        }

        else
        {
            slider.transform.forward = camObj.transform.forward;
            slider.gameObject.SetActive(true);
        }
    }
}