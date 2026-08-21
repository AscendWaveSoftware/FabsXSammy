//Code by Fabian Schmiedel

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;


public class ScrapShop : MonoBehaviour
{
    [Header("Shop Objects")]
    [SerializeField] private TextMeshProUGUI m_scrapText;
    [SerializeField] private TextMeshProUGUI m_buyMiniText;
    [SerializeField] private TextMeshProUGUI m_buyBigText;
    [SerializeField] private Button m_smallMinionButton;
    [SerializeField] private Button m_bigMinionButton;
    [SerializeField] private string m_nameUnit1 = "Small Unit";
    [SerializeField] private string m_nameUnit2 = "Big Unit";
    [SerializeField] private Canvas canvas;
    [SerializeField] private VideoPlayer[] m_minionsVid;

    [Header("Factory Objects")]
    [SerializeField] private Factory m_factory;
    [SerializeField] private GameObject[] m_minions;

    [Header("Prices")]
    [SerializeField] private SO_CurrencySystem m_prices;

    private PlayerResources m_playerResources;
    private Animation m_openAnim;
    private Animation m_buyAnim;


    private void Start()
    {
        m_factory = GetComponentInParent<Factory>();
        canvas = GetComponentInChildren<Canvas>();
        m_openAnim = canvas.GetComponent<Animation>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            m_openAnim.Play();
            PveRuntime.SetPaused(true);
            m_playerResources = FindAnyObjectByType<PlayerResources>();
            m_scrapText.text = "Current Scrap:\n" + m_playerResources.CurrentScrap.ToString();
            m_buyMiniText.text = $"[{m_nameUnit1}] \nCost: " + m_prices.m_PriceForUnit1;
            m_buyBigText.text = $"[{m_nameUnit2}] \nCost: " + m_prices.m_PriceForUnit2;
            
            m_smallMinionButton.interactable = PriceCheck(m_playerResources.CurrentScrap, m_prices.m_PriceForUnit1);
            m_bigMinionButton.interactable = PriceCheck(m_playerResources.CurrentScrap, m_prices.m_PriceForUnit2);

            for (int i = 0; i < m_minionsVid.Length; i++)
            {
                m_minionsVid[i].Play();
            }
        }
    }

    public void PlayerBuyMiniMinion()
    {
        if (m_playerResources.CurrentScrap >= m_prices.m_PriceForUnit1)
        {
            m_playerResources.DecreaseScrap(m_prices.m_PriceForUnit1, Resources.SCRAP);
            m_scrapText.text = "Current Scrap:\n" + m_playerResources.CurrentScrap.ToString();
            m_factory.GetMinion(m_minions[0]);
            m_buyAnim = m_smallMinionButton.GetComponent<Animation>();
            m_buyAnim.Play();
        }
    }

    public void PlayerBuyBigMinion()
    {
        if (m_playerResources.CurrentScrap >= m_prices.m_PriceForUnit2)
        {
            m_playerResources.DecreaseScrap(m_prices.m_PriceForUnit2, Resources.SCRAP);
            m_scrapText.text = "Current Scrap:\n" + m_playerResources.CurrentScrap.ToString();
            m_factory.GetMinion(m_minions[1]);

        }
        m_bigMinionButton.interactable = false;
    }

    public void CloseShop()
    {
        PveRuntime.SetPaused(false);
        canvas.enabled = false;
        for (int i = 0; i < m_minionsVid.Length; i++)
        {
            m_minionsVid[i].Stop();
        }
    }

    private bool PriceCheck(int resources, int _price)
    {
        bool interactable = resources >= _price;

        return interactable;
    }
}
