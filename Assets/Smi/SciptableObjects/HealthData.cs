using UnityEngine;

[CreateAssetMenu(fileName = "HealthData", menuName = "Scriptable Objects/HealthData")]
public class HealthData : ScriptableObject
{
    public float m_targetHealth;
    public bool m_isEnemyBuilding = false;
}
