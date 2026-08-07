public enum UpgradeType
{
    MIN,
    MOVESPEED,
    MAXHEALTH,
    DAMAGE,
    ATTACKRANGE,
    CRITICALCHANCE,
    HEALTHONHIT,
    DAMAGEREDUCTION,
    HEALTHREGEN,
    EXPERIENCEGAIN,
    // Appended before MAX on purpose. The values are serialised as integers in
    // the upgrade assets, so inserting anywhere else would silently rewire them.
    UNLOCKSPELL,
    SPELLPOWER,
    MAX,
}
