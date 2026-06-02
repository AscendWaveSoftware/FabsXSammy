using UnityEngine;
using System;

public class PlayerResources : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] private int currentGold;
    [SerializeField] private int currentScrap;

    public int CurrentGold => currentGold;
    public int CurrentScrap => currentScrap;

    //Events
    public static event EventHandler<ResourceChangedEventArgs> OnResourceChanged;

    private void Start()
    {
        currentGold = 0;
        currentScrap = 0;
    }

    public void AddGold(int _amount, Resources _resourceType)
    {
        if (_amount <= 0) return;

        currentGold += _amount;

        OnResourceChanged?.Invoke(this, new ResourceChangedEventArgs
        {
            Amount = _amount,
            CurrentResourceAmount = currentGold,
            ResourceType = _resourceType
        });
    }

    public void AddScrap(int _amount, Resources _resourceType)
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

    public void DecreaseGold(int _amount, Resources _resourceType)
    {
        if (_amount <= 0) return;
        if (currentGold - _amount < 0) return;

        currentGold -= _amount;

        OnResourceChanged?.Invoke(this, new ResourceChangedEventArgs
        {
            Amount = -_amount,
            CurrentResourceAmount = currentGold,
            ResourceType = _resourceType
        });
    }

    public void DecreaseScrap(int _amount, Resources _resourceType)
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
}
