using UnityEngine;
using System;

public class PlayerResources : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] private int currentScrap;

    public int CurrentScrap => currentScrap;

    //Events
    public static event EventHandler<ResourceChangedEventArgs> OnResourceChanged;

    private void Start()
    {
        currentScrap = 0;

        OnResourceChanged?.Invoke(this, new ResourceChangedEventArgs
        {
            Amount = 0,
            CurrentResourceAmount = currentScrap,
            ResourceType = Resources.SCRAP
        });
    }


    public void AddScrap(int _amount, Resources _resourceType = Resources.SCRAP)
    {
        if (_amount <= 0) return;

        currentScrap += _amount;

        OnResourceChanged?.Invoke(this, new ResourceChangedEventArgs
        {
            Amount = _amount,
            CurrentResourceAmount = currentScrap,
            ResourceType = _resourceType
        });
    }

    public void DecreaseScrap(int _amount, Resources _resourceType = Resources.SCRAP)
    {
        if (_amount <= 0) return;
        if (currentScrap - _amount < 0) return;

        currentScrap -= _amount;

        OnResourceChanged?.Invoke(this, new ResourceChangedEventArgs
        {
            Amount = -_amount,
            CurrentResourceAmount = currentScrap,
            ResourceType = _resourceType
        });
    }

    public void ResetResources()
    {
        currentScrap = 0;

        OnResourceChanged?.Invoke(this, new ResourceChangedEventArgs
        {
            Amount = 0,
            CurrentResourceAmount = currentScrap,
            ResourceType = Resources.SCRAP
        });
    }
}
