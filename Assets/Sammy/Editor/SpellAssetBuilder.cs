using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Slices the spell sprite sheets and keeps the matching spell assets and the
/// player's spell slots wired up. Mirrors <see cref="PlayerAnimationAssetBuilder"/>
/// so the spell setup is reproducible instead of hand assembled in the Inspector.
/// </summary>
[InitializeOnLoad]
public static class SpellAssetBuilder
{
    // Bumped whenever a repair has to reach assets that already exist: the session
    // flag survives a domain reload, so the builder would otherwise skip a session.
    private const string SessionKey = "Sammy.SpellAssets.V8";
    private const string TextureRoot = "Assets/Sammy/Textures/Spells";
    private const string SpellAssetRoot = "Assets/Sammy/Scriptable Objects";
    private const string AudioRoot = "Assets/Sammy/Audio";
    private const string PlayerPrefabPath = "Assets/Sammy/Prefabs/Player.prefab";
    private const string EmissiveMaterialPath = "Assets/Sammy/Shader/MT_SpellEmissive.mat";
    private const float PixelsPerUnit = 100f;

    private readonly struct SpellSheet
    {
        public SpellSheet(string _fileName, string _spellName, int _frameSize, int _slotIndex, string _clipName = null)
        {
            FileName = _fileName;
            SpellName = _spellName;
            FrameSize = _frameSize;
            SlotIndex = _slotIndex;

            // Named after the spell unless the audio file says otherwise, so a clip
            // that arrived under its own name does not have to be renamed to be found.
            ClipName = string.IsNullOrEmpty(_clipName) ? $"{_spellName}_Spell" : _clipName;
        }

        public string FileName { get; }
        public string SpellName { get; }
        public int FrameSize { get; }
        public int SlotIndex { get; }
        public string ClipName { get; }
        public string TexturePath => $"{TextureRoot}/{FileName}.png";
        public string AssetPath => $"{SpellAssetRoot}/Spell_{SpellName}.asset";

        // Optional by convention: a spell without a matching file stays silent.
        public string CastClipPath => $"{AudioRoot}/{ClipName}.wav";
    }

    private static readonly SpellSheet[] Sheets =
    {
        new("Magic_Spell1_Astral", "Astral", 48, 0),
        new("Magic_Spell2_Poison", "Poison", 64, 1),
        new("Magic_Spell3_Hollow", "Hollow", 48, 2),
        // Far larger frames than the others because the source artwork carries its
        // detail at that resolution. Downscaling it would smear the accretion disc.
        new("Magic_Spell4_Vortex", "Vortex", 272, 3, "DarkHole_Spell")
    };

    static SpellAssetBuilder()
    {
        EditorApplication.delayCall += BuildOncePerSession;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    /// <summary>
    /// A build that lands while the editor is playing is skipped, and used to stay
    /// skipped until something else happened to reload the domain. Leaving play
    /// mode is exactly the moment it becomes safe to try again.
    /// </summary>
    private static void HandlePlayModeChanged(PlayModeStateChange _change)
    {
        if (_change == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += BuildOncePerSession;
    }

    [MenuItem("Tools/Sammy/Rebuild Spell Assets")]
    public static void RebuildFromMenu()
    {
        SessionState.EraseBool(SessionKey);
        BuildOncePerSession();
    }

    private static void BuildOncePerSession()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling)
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);

        try
        {
            Dictionary<int, SpellDefinition> spellsBySlot = new();

            foreach (SpellSheet sheet in Sheets)
            {
                ConfigureAndSliceSheet(sheet);
                spellsBySlot[sheet.SlotIndex] = CreateOrUpdateSpell(sheet);
            }

            ConfigurePlayerPrefab(spellsBySlot);
            AssetDatabase.SaveAssets();
            Debug.Log("Spell assets rebuilt successfully.");
        }
        catch (Exception exception)
        {
            SessionState.EraseBool(SessionKey);
            Debug.LogException(exception);
        }
    }

    private static void ConfigureAndSliceSheet(SpellSheet _sheet)
    {
        TextureImporter importer = AssetImporter.GetAtPath(_sheet.TexturePath) as TextureImporter;

        if (importer == null)
            throw new InvalidOperationException($"Spell sheet is missing: {_sheet.TexturePath}");

        // Measured on the source file, not on the imported texture. An unsliced
        // sheet still counts as a default texture, and those get scaled up to the
        // next power of two, which would report 512x64 for a 480x48 strip.
        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);

        if (sourceHeight != _sheet.FrameSize || sourceWidth <= 0 || sourceWidth % _sheet.FrameSize != 0)
        {
            throw new InvalidOperationException(
                $"Spell sheet {_sheet.TexturePath} must be a horizontal strip of {_sheet.FrameSize}x{_sheet.FrameSize} " +
                $"frames, but is {sourceWidth}x{sourceHeight}."
            );
        }

        int frameCount = sourceWidth / _sheet.FrameSize;

        // Two passes on purpose. The sprite data provider below builds its own
        // serialised view of the importer, and on a sheet that is still a plain
        // default texture that view silently discards the type switch. Importing
        // the settings first means the provider always sees a multiple sprite.
        if (ApplyImporterSettings(importer))
        {
            PersistImporter(importer, _sheet.TexturePath);
            importer = AssetImporter.GetAtPath(_sheet.TexturePath) as TextureImporter;

            if (importer == null)
                throw new InvalidOperationException($"Spell sheet vanished while importing: {_sheet.TexturePath}");
        }

        ApplySpriteRects(importer, _sheet, frameCount);
    }

    private static bool ApplyImporterSettings(TextureImporter _importer)
    {
        TextureImporterSettings textureSettings = new();
        _importer.ReadTextureSettings(textureSettings);

        bool importerChanged = _importer.textureType != TextureImporterType.Sprite ||
                               _importer.spriteImportMode != SpriteImportMode.Multiple ||
                               !Mathf.Approximately(_importer.spritePixelsPerUnit, PixelsPerUnit) ||
                               _importer.filterMode != FilterMode.Point ||
                               _importer.mipmapEnabled ||
                               !_importer.alphaIsTransparency ||
                               _importer.wrapMode != TextureWrapMode.Clamp ||
                               _importer.textureCompression != TextureImporterCompression.Uncompressed ||
                               _importer.npotScale != TextureImporterNPOTScale.None ||
                               textureSettings.spriteMeshType != SpriteMeshType.FullRect ||
                               textureSettings.spriteGenerateFallbackPhysicsShape;

        if (!importerChanged)
            return false;

        _importer.textureType = TextureImporterType.Sprite;
        _importer.spriteImportMode = SpriteImportMode.Multiple;
        _importer.spritePixelsPerUnit = PixelsPerUnit;
        // Rescaling to a power of two would shift every frame off its pixel grid.
        _importer.npotScale = TextureImporterNPOTScale.None;
        _importer.filterMode = FilterMode.Point;
        _importer.mipmapEnabled = false;
        _importer.alphaIsTransparency = true;
        _importer.wrapMode = TextureWrapMode.Clamp;
        _importer.textureCompression = TextureImporterCompression.Uncompressed;

        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        textureSettings.spriteGenerateFallbackPhysicsShape = false;

        // Repeated on the settings block rather than trusted to the properties
        // above. SetTextureSettings writes this whole block back, and it still
        // holds the values read before those properties were touched, so the read
        // would quietly restore the old filter and blur the pixel art.
        textureSettings.filterMode = FilterMode.Point;
        textureSettings.mipmapEnabled = false;
        textureSettings.wrapMode = TextureWrapMode.Clamp;
        textureSettings.alphaIsTransparency = true;
        textureSettings.npotScale = TextureImporterNPOTScale.None;

        _importer.SetTextureSettings(textureSettings);
        return true;
    }

    private static void ApplySpriteRects(TextureImporter _importer, SpellSheet _sheet, int _frameCount)
    {
        SpriteDataProviderFactories factories = new();
        factories.Init();
        ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(_importer);
        dataProvider.InitSpriteEditorDataProvider();

        SpriteRect[] existingRects = dataProvider.GetSpriteRects();
        bool hasExpectedRects = existingRects.Length == _frameCount;

        for (int i = 0; hasExpectedRects && i < existingRects.Length; i++)
        {
            Rect expectedRect = new(i * _sheet.FrameSize, 0f, _sheet.FrameSize, _sheet.FrameSize);
            hasExpectedRects = existingRects[i].name == GetSpriteName(_sheet, i) &&
                               existingRects[i].rect == expectedRect &&
                               existingRects[i].alignment == SpriteAlignment.Center &&
                               existingRects[i].pivot == new Vector2(0.5f, 0.5f) &&
                               existingRects[i].border == Vector4.zero;
        }

        if (hasExpectedRects)
            return;

        // Existing sprite ids are reused so references already pointing at a
        // frame survive a reslice instead of turning into missing sprites.
        Dictionary<string, GUID> existingSpriteIds = existingRects
            .GroupBy(rect => rect.name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().spriteID, StringComparer.Ordinal);
        SpriteRect[] spriteRects = new SpriteRect[_frameCount];

        for (int i = 0; i < spriteRects.Length; i++)
        {
            string spriteName = GetSpriteName(_sheet, i);
            spriteRects[i] = new SpriteRect
            {
                name = spriteName,
                rect = new Rect(i * _sheet.FrameSize, 0f, _sheet.FrameSize, _sheet.FrameSize),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
                spriteID = existingSpriteIds.TryGetValue(spriteName, out GUID existingId)
                    ? existingId
                    : GUID.Generate()
            };
        }

        dataProvider.SetSpriteRects(spriteRects);
        ISpriteNameFileIdDataProvider nameProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameProvider?.SetNameFileIdPairs(spriteRects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        dataProvider.Apply();

        // Saved through the provider's own importer instance. Apply() writes into
        // that object, so saving the importer we started from would persist a copy
        // that never saw the new rects and silently drop the whole slicing.
        AssetImporter providerImporter = dataProvider.targetObject as AssetImporter;
        PersistImporter(providerImporter != null ? providerImporter : _importer, _sheet.TexturePath);
    }

    /// <summary>
    /// Writes importer settings all the way to disk. SaveAndReimport on its own
    /// keeps them in memory often enough that a sheet quietly stays a default
    /// texture, slices into nothing, and the spell ends up with no frames at all.
    /// </summary>
    private static void PersistImporter(AssetImporter _importer, string _assetPath)
    {
        EditorUtility.SetDirty(_importer);
        AssetDatabase.WriteImportSettingsIfDirty(_assetPath);
        AssetDatabase.ImportAsset(
            _assetPath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport
        );
    }

    private static SpellDefinition CreateOrUpdateSpell(SpellSheet _sheet)
    {
        SpellDefinition spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(_sheet.AssetPath);

        if (spell == null)
        {
            spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.SpellName = _sheet.SpellName;

            // Only ever on creation. Re-applying these on every build would throw
            // away whatever was tuned in the Inspector afterwards.
            ApplyNewSpellDefaults(spell, _sheet);
            AssetDatabase.CreateAsset(spell, _sheet.AssetPath);
        }

        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(_sheet.TexturePath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();

        if (frames.Length == 0)
        {
            throw new InvalidOperationException(
                $"Spell sheet {_sheet.TexturePath} produced no sprites. The texture is most likely still " +
                "importing as a default texture instead of a sliced sprite sheet.");
        }

        SerializedObject serializedSpell = new(spell);
        SerializedProperty frameArray = serializedSpell.FindProperty(nameof(SpellDefinition.Frames));

        if (frameArray == null)
            throw new InvalidOperationException("SpellDefinition frame array no longer exists.");

        frameArray.arraySize = frames.Length;

        for (int i = 0; i < frames.Length; i++)
            frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];

        // Wired only when a clip with the matching name exists, so a spell that
        // has no sound yet keeps its empty field instead of erroring out.
        AudioClip castClip = AssetDatabase.LoadAssetAtPath<AudioClip>(_sheet.CastClipPath);
        SerializedProperty castClipProperty = serializedSpell.FindProperty(nameof(SpellDefinition.CastClip));

        if (castClip != null && castClipProperty != null && castClipProperty.objectReferenceValue != castClip)
            castClipProperty.objectReferenceValue = castClip;

        if (serializedSpell.hasModifiedProperties)
        {
            serializedSpell.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spell);
        }

        return spell;
    }

    /// <summary>
    /// Starting values for a spell asset the builder has just brought into being.
    /// Everything not listed here keeps the field defaults from SpellDefinition.
    /// </summary>
    private static void ApplyNewSpellDefaults(SpellDefinition _spell, SpellSheet _sheet)
    {
        _spell.EmissiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(EmissiveMaterialPath);

        if (_sheet.SpellName != "Vortex")
            return;

        _spell.Delivery = SpellDelivery.Vortex;
        _spell.UiColor = new Color(0.62f, 0.36f, 0.96f, 1f);

        // The strongest spell in the game, so it glows hardest and comes back
        // slowest. Everything else is balanced around that trade.
        _spell.EmissionIntensity = 3.2f;
        _spell.Cooldown = 16f;
        _spell.TargetSearchRange = 18f;
        _spell.SpawnHeight = 0.25f;

        _spell.ImpactStartFrame = 0;
        _spell.ImpactFrameRate = 12f;
        _spell.VortexPeakFrame = 4;

        // Timed against DarkHole_Spell.wav rather than picked by feel. Its envelope
        // builds to a peak at 2.2 seconds and has bottomed out by 3.0, so the hold
        // ends exactly on the loudest moment and the implosion lands with it, while
        // the collapse plays out over the decay.
        _spell.VortexFormDuration = 0.5f;
        _spell.VortexHoldDuration = 1.7f;
        _spell.VortexCollapseDuration = 0.8f;

        _spell.Damage = 110;
        _spell.ImpactRadius = 3.2f;

        // The artwork only fills roughly three quarters of its square canvas, so
        // the visual scale has to run above 1 for the disc to actually cover the
        // radius it damages.
        _spell.ImpactVisualScale = 1.45f;

        _spell.GlowColor = new Color(0.74f, 0.44f, 1f, 0.95f);
        _spell.TrailColor = new Color(0.55f, 0.3f, 0.95f, 0.8f);
        _spell.CastFlashDiameter = 0f;
    }

    private static void ConfigurePlayerPrefab(IReadOnlyDictionary<int, SpellDefinition> _spellsBySlot)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        bool prefabChanged = false;

        try
        {
            PlayerSpellCaster spellCaster = root.GetComponent<PlayerSpellCaster>();

            if (spellCaster == null)
            {
                spellCaster = root.AddComponent<PlayerSpellCaster>();
                prefabChanged = true;
            }

            SerializedObject serializedCaster = new(spellCaster);
            prefabChanged |= SetObjectReference(serializedCaster.FindProperty("m_playerHealth"), root.GetComponent<PlayerHealth>());
            prefabChanged |= SetObjectReference(serializedCaster.FindProperty("m_playerResources"), root.GetComponent<PlayerResources>());
            prefabChanged |= SetObjectReference(serializedCaster.FindProperty("m_rigidbody"), root.GetComponent<Rigidbody>());
            prefabChanged |= SetObjectReference(serializedCaster.FindProperty("m_movement"), root.GetComponent<PlayerMovementHandler>());

            SerializedProperty spellSlots = serializedCaster.FindProperty("m_spells");

            if (spellSlots == null)
                throw new InvalidOperationException("PlayerSpellCaster spell slot array no longer exists.");

            if (spellSlots.arraySize != PlayerSpellCaster.SpellSlotCount)
            {
                spellSlots.arraySize = PlayerSpellCaster.SpellSlotCount;
                prefabChanged = true;
            }

            foreach (KeyValuePair<int, SpellDefinition> slot in _spellsBySlot)
            {
                if (slot.Key < 0 || slot.Key >= spellSlots.arraySize)
                    continue;

                prefabChanged |= SetObjectReference(spellSlots.GetArrayElementAtIndex(slot.Key), slot.Value);
            }

            // The enemy layer is the same mask melee combat already hits, so the
            // spell can never drift out of sync with what counts as an enemy.
            PlayerCombat combat = root.GetComponent<PlayerCombat>();
            SerializedProperty spellEnemyLayer = serializedCaster.FindProperty("m_enemyLayer");
            SerializedProperty combatEnemyLayer = combat != null
                ? new SerializedObject(combat).FindProperty("m_enemyLayer")
                : null;

            if (spellEnemyLayer != null && combatEnemyLayer != null &&
                spellEnemyLayer.intValue != combatEnemyLayer.intValue)
            {
                spellEnemyLayer.intValue = combatEnemyLayer.intValue;
                prefabChanged = true;
            }

            if (serializedCaster.hasModifiedProperties)
                serializedCaster.ApplyModifiedPropertiesWithoutUndo();

            prefabChanged |= ConfigureSpellBar(root, spellCaster);

            _spellsBySlot.TryGetValue(3, out SpellDefinition vortexSpell);
            prefabChanged |= ConfigureVortexUpgrades(root, vortexSpell);

            if (prefabChanged)
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Creates the level up cards for the vortex and makes sure the player carries
    /// them. Without the unlock card the spell could never be earned, so this is
    /// part of building the spell rather than something to wire by hand.
    /// </summary>
    private static bool ConfigureVortexUpgrades(GameObject _root, SpellDefinition _vortexSpell)
    {
        PlayerUpgradeHandler upgradeHandler = _root.GetComponent<PlayerUpgradeHandler>();

        if (_vortexSpell == null || upgradeHandler == null)
            return false;

        UpgradeDefinition[] cards =
        {
            EnsureUpgrade("Upgrade_UnlockSpell_Vortex", _vortexSpell, card =>
            {
                card.UpgradeName = "Singularity";
                card.Description = "Unlocks the Vortex on key 4. Tears open a black hole that drags " +
                                   "everything nearby into its centre and implodes.";
                card.UpgradeType = UpgradeType.UNLOCKSPELL;
                card.Value = 0f;
            }),
            EnsureUpgrade("Upgrade_Spell_Vortex_Damage", _vortexSpell, card =>
            {
                card.UpgradeName = "Vortex Collapse";
                card.Description = "The Vortex implosion hits for 35 more.";
                card.UpgradeType = UpgradeType.SPELLPOWER;
                card.SpellStat = SpellStat.Damage;
                card.Value = 35f;
            }),
            EnsureUpgrade("Upgrade_Spell_Vortex_Pull", _vortexSpell, card =>
            {
                card.UpgradeName = "Vortex Grip";
                card.Description = "The Vortex drags its victims in 1.2 faster.";
                card.UpgradeType = UpgradeType.SPELLPOWER;
                card.SpellStat = SpellStat.VortexPull;
                card.Value = 1.2f;
            }),
            EnsureUpgrade("Upgrade_Spell_Vortex_Duration", _vortexSpell, card =>
            {
                card.UpgradeName = "Vortex Hunger";
                card.Description = "The Vortex stays open 0.5 seconds longer.";
                card.UpgradeType = UpgradeType.SPELLPOWER;
                card.SpellStat = SpellStat.VortexDuration;
                card.Value = 0.5f;
            }),
            EnsureUpgrade("Upgrade_Spell_Vortex_Cooldown", _vortexSpell, card =>
            {
                card.UpgradeName = "Vortex Rift";
                card.Description = "The Vortex comes back 2 seconds sooner.";
                card.UpgradeType = UpgradeType.SPELLPOWER;
                card.SpellStat = SpellStat.Cooldown;
                card.Value = 2f;
            })
        };

        SerializedObject serializedHandler = new(upgradeHandler);
        SerializedProperty cardList = serializedHandler.FindProperty("m_spellUpgrades");

        if (cardList == null)
            throw new InvalidOperationException("PlayerUpgradeHandler spell upgrade array no longer exists.");

        bool prefabChanged = false;

        foreach (UpgradeDefinition card in cards)
        {
            if (card == null || ContainsCard(cardList, card))
                continue;

            cardList.arraySize++;
            cardList.GetArrayElementAtIndex(cardList.arraySize - 1).objectReferenceValue = card;
            prefabChanged = true;
        }

        if (serializedHandler.hasModifiedProperties)
            serializedHandler.ApplyModifiedPropertiesWithoutUndo();

        return prefabChanged;
    }

    private static bool ContainsCard(SerializedProperty _cardList, UpgradeDefinition _card)
    {
        for (int i = 0; i < _cardList.arraySize; i++)
        {
            if (_cardList.GetArrayElementAtIndex(i).objectReferenceValue == _card)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Loads a card, creating it the first time. An existing card is left alone
    /// apart from a lost spell reference, which would otherwise quietly stop it
    /// from ever being offered again.
    /// </summary>
    private static UpgradeDefinition EnsureUpgrade(
        string _assetName,
        SpellDefinition _spell,
        Action<UpgradeDefinition> _configure)
    {
        string assetPath = $"{SpellAssetRoot}/{_assetName}.asset";
        UpgradeDefinition upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(assetPath);

        if (upgrade != null)
        {
            if (upgrade.Spell == null)
            {
                upgrade.Spell = _spell;
                EditorUtility.SetDirty(upgrade);
            }

            return upgrade;
        }

        upgrade = ScriptableObject.CreateInstance<UpgradeDefinition>();
        upgrade.Spell = _spell;

        // The same built in sprite the existing spell cards use, so the new ones do
        // not stand out as the only cards without an icon.
        upgrade.Icon = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        _configure(upgrade);

        AssetDatabase.CreateAsset(upgrade, assetPath);
        return upgrade;
    }

    private static bool ConfigureSpellBar(GameObject _root, PlayerSpellCaster _spellCaster)
    {
        PlayerSpellBarUI spellBar = _root.GetComponent<PlayerSpellBarUI>();
        bool prefabChanged = false;

        if (spellBar == null)
        {
            spellBar = _root.AddComponent<PlayerSpellBarUI>();
            prefabChanged = true;
        }

        SerializedObject serializedSpellBar = new(spellBar);
        prefabChanged |= SetObjectReference(serializedSpellBar.FindProperty("m_spellCaster"), _spellCaster);
        prefabChanged |= SetObjectReference(serializedSpellBar.FindProperty("m_uiHandler"), _root.GetComponent<PlayerUIHandler>());

        if (serializedSpellBar.hasModifiedProperties)
            serializedSpellBar.ApplyModifiedPropertiesWithoutUndo();

        return prefabChanged;
    }

    private static string GetSpriteName(SpellSheet _sheet, int _index) => $"{_sheet.SpellName}_{_index:00}";

    private static bool SetObjectReference(SerializedProperty _property, UnityEngine.Object _value)
    {
        if (_property == null)
            throw new InvalidOperationException("A required serialized spell reference no longer exists.");

        if (_property.objectReferenceValue == _value)
            return false;

        _property.objectReferenceValue = _value;
        return true;
    }
}
