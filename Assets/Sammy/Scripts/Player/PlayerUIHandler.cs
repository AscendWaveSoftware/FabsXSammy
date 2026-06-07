using TMPro;
using UnityEngine;

public class PlayerUIHandler : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI scrapText;

    private void OnEnable()
    {
        PlayerResources.OnResourceChanged += HandleResourceChanged;
        //TODO: hier muss noch initial Gold und Scrap Text gesetzt werden
    }

    private void OnDisable()
    {
        PlayerResources.OnResourceChanged -= HandleResourceChanged;
    }

    private void HandleResourceChanged(object sender, ResourceChangedEventArgs e)
    {
        switch (e.ResourceType)
        {
            case Resources.GOLD:
                goldText.text = e.CurrentResourceAmount.ToString();
                break;
            case Resources.SCRAP:
                scrapText.text = e.CurrentResourceAmount.ToString();
                break;
        }
    }
}
