using TMPro;
using UnityEngine;


public class ScrapShop : MonoBehaviour
{
    [Header("Shop Objects")]
    [SerializeField] TextMeshProUGUI m_scrapText;
    [SerializeField] TextMeshProUGUI m_buyMiniText;
    [SerializeField] TextMeshProUGUI m_buyBigText;
    [SerializeField] string m_nameUnit1 = "Small Unit";
    [SerializeField] string m_nameUnit2 = "Big Unit";
    [SerializeField] Canvas canvas;
    [Header("Factory Objects")]
    [SerializeField] Factory m_factory;
    [SerializeField] GameObject[] m_minions;

    [Header("Prices")]
    [SerializeField] SO_CurrencySystem m_prices;

    PlayerResources m_playerResources;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PveRuntime.SetPaused(true);
            m_playerResources = FindAnyObjectByType<PlayerResources>();
            m_scrapText.text = "Current Scrap: " + m_playerResources.CurrentScrap.ToString();
            m_buyMiniText.text = $"[{m_nameUnit1}] \nCost: " + m_prices.m_PriceForUnit1;
            m_buyBigText.text = $"[{m_nameUnit2}] \nCost: " + m_prices.m_PriceForUnit2;
            canvas.enabled = true;
        }
    }

    public void PlayerBuyMiniMinion()
    {
        if (m_playerResources.CurrentScrap >= m_prices.m_PriceForUnit1)
        {
            m_playerResources.DecreaseScrap(m_prices.m_PriceForUnit1, Resources.SCRAP);
            m_scrapText.text = "Current Scrap: " + m_playerResources.CurrentScrap.ToString();
            m_factory.GetMinion(m_minions[0]);

        }
    }

    public void PlayerBuyBigMinion()
    {
        if (m_playerResources.CurrentScrap >= m_prices.m_PriceForUnit2)
        {
            m_playerResources.DecreaseScrap(m_prices.m_PriceForUnit2, Resources.SCRAP);
            m_scrapText.text = "Current Scrap: " + m_playerResources.CurrentScrap.ToString();
            m_factory.GetMinion(m_minions[1]);
        }
    }

    public void CloseShop()
    {
        PveRuntime.SetPaused(false);
        canvas.enabled = false;
    }
}
