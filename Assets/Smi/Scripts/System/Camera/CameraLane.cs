//Code by Fabian Schmiedel

using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public class CameraLane : MonoBehaviour
{
    [SerializeField] GameObject m_lockPoint;
    [SerializeField] AudioMixer m_audioMixerLane;
    CinemachineRotationComposer m_followCam;
    float m_audioTemp;

    [Header("Mausempfindlichkeit")]
    public float sensitivityZ = 0.1f;
    public float sensitivityY = 0.1f;

    [Header("Horizontale Begrenzung (Links/Rechts)")]
    public float minZAngle = -40.0f;
    public float maxZAngle = 40.0f;

    [Header("Vertikale Begrenzung (Unten/Oben)")]
    public float minYAngle = -25.0f;
    public float maxYAngle = 5.0f;

    private float positionZ = 0.0f;
    private float rotationY = 0.0f;

    private Vector3 m_startPosition;

    private void OnEnable()
    {
        m_audioMixerLane.SetFloat("arena", -80);

        m_audioTemp = PlayerPrefs.GetFloat("sfx", 0f);
        m_audioMixerLane.SetFloat("lane", m_audioTemp);
    }

    private void Start()
    {
        m_followCam = GetComponent<CinemachineRotationComposer>();

        m_startPosition = m_lockPoint.transform.position;
    }

    private void Update()
    {
        if (Time.timeScale != 0)
        {
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                m_startPosition.x = 5f;
                m_followCam.TargetOffset.x = 30.0f;
            }
            else if (Mouse.current != null)
            {
                m_startPosition.x = 30f;
                m_followCam.TargetOffset.x = 0.0f;
            }

            if (Mouse.current != null)
            {
                float mouseX = Mouse.current.delta.x.ReadValue() * sensitivityZ;
                float mouseY = Mouse.current.delta.y.ReadValue() * sensitivityY;

                positionZ += mouseX;
                rotationY += mouseY;

                positionZ = Mathf.Clamp(positionZ, minZAngle, maxZAngle);
                rotationY = Mathf.Clamp(rotationY, minYAngle, maxYAngle);

                if (m_followCam != null)
                {
                    m_followCam.TargetOffset.y = rotationY;
                }

                Vector3 newPos = new Vector3(m_startPosition.x, m_startPosition.y, m_startPosition.z + positionZ);

                m_lockPoint.transform.position = newPos;
            }
        }      
    }

    private void OnDisable()
    {
        m_audioMixerLane.SetFloat("lane", -80);

        m_audioTemp = PlayerPrefs.GetFloat("sfx", 0f);
        m_audioMixerLane.SetFloat("arena", m_audioTemp);
    }
}
