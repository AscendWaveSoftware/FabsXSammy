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

    [Header("Spell Cards")]
    [Tooltip("Used by UNLOCKSPELL and SPELLPOWER. The spell asset this card refers to.")]
    public SpellDefinition Spell;
    [Tooltip("Only used by SPELLPOWER. Which value of the spell the card improves. For Cooldown, Value is the reduction.")]
    public SpellStat SpellStat;
}
