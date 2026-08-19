//Code by Fabian Schmiedel

using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraZoom : MonoBehaviour
{
    CinemachineCamera m_towerCam;
    float m_zoomOut = 95;
    float m_zoomIn = 70;

    private void Start()
    {
        m_towerCam = GetComponent<CinemachineCamera>();
        m_zoomOut = m_towerCam.Lens.FieldOfView;
    }

    private void Update()
    {
        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.isPressed)
                m_towerCam.Lens.FieldOfView = m_zoomIn;

            else
                m_towerCam.Lens.FieldOfView = m_zoomOut;
        }
    }
}
