using UnityEngine;

public class ScrapShopAnimation : MonoBehaviour
{
    Animation m_animation;
    [SerializeField] AnimationClip m_Open;
    [SerializeField] AnimationClip m_Close;

    void Start()
    {
        m_animation = GetComponent<Animation>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            m_animation.clip = m_Open;
            m_animation.Play();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            m_animation.clip = m_Close;
            m_animation.Play();

        }
    }
}
