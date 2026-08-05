using System;
using UnityEngine;

public class PlayerExperience : MonoBehaviour
{
    [Header("Level Settings")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int currentXP;
    [SerializeField] private int xpToNextLevel = 100;
    [SerializeField] private float xpRequirementMultiplier = 1.25f;

    private float m_experienceMultiplier = 1f;

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

        int modifiedAmount = Mathf.Max(1, Mathf.RoundToInt(_amount * m_experienceMultiplier));
        currentXP += modifiedAmount;

        while(currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            LevelUp();
        }

        OnExperienceChanged?.Invoke(currentLevel, currentXP, xpToNextLevel);
    }

    public void AddExperienceGain(float _percentage)
    {
        if (_percentage <= 0f)
            return;

        m_experienceMultiplier += _percentage;
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
