using UnityEngine;

[RequireComponent(typeof(Health))]
public class Tower : MonoBehaviour
{
    public Team m_team;

    private Health m_health;

    private void Awake()
    {
        m_health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        RegisterTower();
    }

    private void OnDisable()
    {
        UnregisterTower();
    }

    private void RegisterTower()
    {
        if (m_team == Team.Blue)
            EntityManager.BlueTowers.Add(this);
        else
            EntityManager.RedTowers.Add(this);
    }

    private void UnregisterTower()
    {
        if (m_team == Team.Blue)
            EntityManager.BlueTowers.Remove(this);
        else
            EntityManager.RedTowers.Remove(this);
    }
}