using System;
using UnityEngine;

public class PlayerExperience : MonoBehaviour
{
    [Header("Level Settings")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int currentXP;
    [SerializeField] private int xpToNextLevel = 100;
    [SerializeField] private float xpRequirementMultiplier = 1.25f;

    public int CurrentLevel => currentLevel;
    public int CurrentXP => currentXP;
    public int XPToNextLevel => xpToNextLevel;

    public event Action<int, int, int> OnExperienceChanged;
    public event Action<int> OnLevelUp;

    private void Start()
    {
        OnExperienceChanged?.Invoke(currentLevel, currentXP, xpToNextLevel);
    }

    public void AddXP(int _amount)
    {
        if (_amount <= 0) return;

        currentXP += _amount;

        while(currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            LevelUp();
        }

        OnExperienceChanged?.Invoke(currentLevel, currentXP, xpToNextLevel);
    }

    public void ResetXP()
    {
        currentLevel = 1;
        currentXP = 0;
        xpToNextLevel = 100;

        OnExperienceChanged?.Invoke(currentLevel, currentXP, xpToNextLevel);
    }

    private void LevelUp()
    {
        currentLevel++;

        xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * xpRequirementMultiplier);

        OnLevelUp?.Invoke(currentLevel);
    }
}
