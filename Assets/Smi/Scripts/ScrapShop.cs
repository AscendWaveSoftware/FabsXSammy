using System.Collections;
using TMPro;
using UnityEngine;


public class ScrapShop : MonoBehaviour
{
    [Header("Shop Objects")]
    [SerializeField] TextMeshProUGUI m_scrapText;
    [SerializeField] TextMeshProUGUI m_buyMiniText;
    [SerializeField] TextMeshProUGUI m_buyBigText;

    [Header("Factory Objects")]
    [SerializeField] Factory m_factory;
    [SerializeField] GameObject[] m_minions;

    [Header("Prices")]
    [SerializeField] SO_CurrencySystem m_prices;

    PlayerResources m_playerResources;
    int m_units = 0;

    Canvas canvas;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            m_playerResources = FindAnyObjectByType<PlayerResources>();
            m_scrapText.text = "Current Scrap: " + m_playerResources.CurrentScrap.ToString();
            m_buyMiniText.text = "[Minion] Cost: " + m_prices.m_PriceForUnit1;
            m_buyBigText.text = "[Big Minion] Cost: " + m_prices.m_PriceForUnit2;

            canvas = GetComponentInChildren<Canvas>();
            canvas.enabled = true;
            Time.timeScale = 0;
        }
    }
    public void PlayerBuyMiniMinion()
    {
        if (m_playerResources.CurrentScrap >= m_prices.m_PriceForUnit1)
        {
            m_playerResources.DecreaseScrap(m_prices.m_PriceForUnit1, Resources.SCRAP);
            m_units += 1;
            m_scrapText.text = "Current Scrap: " + m_playerResources.CurrentScrap.ToString();
            StartCoroutine(SpawnRoutine(m_minions[0]));
        }
    }

    public void PlayerBuyBigMinion()
    {
        if (m_playerResources.CurrentScrap >= m_prices.m_PriceForUnit2)
        {
            m_playerResources.DecreaseScrap(m_prices.m_PriceForUnit2, Resources.SCRAP);
            m_units += 1;
            m_scrapText.text = "Current Scrap: " + m_playerResources.CurrentScrap.ToString();
            StartCoroutine(SpawnRoutine(m_minions[1]));
        }
    }

    private IEnumerator SpawnRoutine(GameObject _minion)
    {
        yield return new WaitForSeconds(2);
        for (int i = 0; i < m_units; i++)
        {
            m_units--;
            m_factory.GetMinion(_minion);
        }
    }

    public void CloseShop()
    {
        canvas.enabled = false;
        Time.timeScale = 1;
    }
}
