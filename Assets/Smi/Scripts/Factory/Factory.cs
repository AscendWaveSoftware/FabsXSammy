using System;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class Factory : MonoBehaviour

{
    public Team m_team;

    private Health m_health;

    private void Awake()
    {
        m_health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        RegisterFactory();
    }

    private void OnDisable()
    {
        LoseState();
    }

    private void LoseState()
    {
        //TODO:
    }

    private void RegisterFactory()
    {
        if (m_team == Team.Blue)
            EntityManager.BlueFactory = this;
        else
            EntityManager.RedFactory = this;
    }
}