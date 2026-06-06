using System.Collections;
using UnityEngine;

public class BuyMinion : MonoBehaviour
{
    [SerializeField] Factory factory;
    [SerializeField] GameObject m_minion;

    [SerializeField] SO_CurrencySystem m_prices;

    PlayerResources m_playerResources;

    int m_units = 0;
    void Start()
    {
        m_playerResources = FindAnyObjectByType<PlayerResources>();
    }

    public void PlayerBuyMinion()
    {
        if (m_playerResources.CurrentScrap >= m_prices.m_PriceForUnit1)
        {
            m_playerResources.DecreaseScrap(m_prices.m_PriceForUnit1, Resources.SCRAP);
            m_units += 1;
            StartCoroutine(SpawnRoutine());
        }
    }

    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(2);
        for (int i = 0; i < m_units; i++)
        {
            m_units--;
            factory.GetMinion(m_minion);
        }
    }
}
