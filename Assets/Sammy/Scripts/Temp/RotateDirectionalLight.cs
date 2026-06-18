using UnityEngine;

public class MenuDirectionalLightMover : MonoBehaviour
{
    [Header("Rotation Range")]
    [SerializeField] private float minXRotation = 25f;
    [SerializeField] private float maxXRotation = 55f;

    [Header("Movement")]
    [SerializeField] private float speed = 0.2f;

    private float startY;
    private float startZ;

    private void Start()
    {
        startY = transform.eulerAngles.y;
        startZ = transform.eulerAngles.z;
    }

    private void Update()
    {
        float t = Mathf.PingPong(Time.time * speed, 1f);
        float xRotation = Mathf.Lerp(minXRotation, maxXRotation, t);

        transform.rotation = Quaternion.Euler(xRotation, startY, startZ);
    }
}