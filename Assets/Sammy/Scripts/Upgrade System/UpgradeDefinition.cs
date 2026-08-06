using UnityEngine;

[CreateAssetMenu(fileName = "New Upgrade", menuName = "Upgrades/Upgrade Definition")]
public class UpgradeDefinition : ScriptableObject
{
    [Header("Display")]
    public string UpgradeName;
    [TextArea] public string Description;
    public Sprite Icon;

    [Header("Effect")]
    public UpgradeType UpgradeType;
    public float Value;

    [Tooltip("Only used by UNLOCKSPELL. The spell this card hands to the player.")]
    public SpellDefinition SpellToUnlock;
}
