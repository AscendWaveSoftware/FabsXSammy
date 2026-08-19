//Code by Fabian Schmiedel

using UnityEngine;

public class WidgetSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject[] widgets;
    [SerializeField] private int startingIndex = 0;

    void Start()
    {
        SetActiveWidget(startingIndex);
    }

    public void SetActiveWidget(int index)
    {
        if (index < 0 || index >= widgets.Length) return;

        for (int i = 0; i < widgets.Length; i++)
        {
            widgets[i].SetActive(i == index);
        }
    }
}
