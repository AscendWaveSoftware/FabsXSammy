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
    private const string SessionKey = "Sammy.SpellAssets.V5";
    private const string TextureRoot = "Assets/Sammy/Textures/Spells";
    private const string SpellAssetRoot = "Assets/Sammy/Scriptable Objects";
    private const string AudioRoot = "Assets/Sammy/Audio";
    private const string PlayerPrefabPath = "Assets/Sammy/Prefabs/Player.prefab";
    private const float PixelsPerUnit = 100f;

    private readonly struct SpellSheet
    {
        public SpellSheet(string _fileName, string _spellName, int _frameSize, int _slotIndex)
        {
            FileName = _fileName;
            SpellName = _spellName;
            FrameSize = _frameSize;
            SlotIndex = _slotIndex;
        }

        public string FileName { get; }
        public string SpellName { get; }
        public int FrameSize { get; }
        public int SlotIndex { get; }
        public string TexturePath => $"{TextureRoot}/{FileName}.png";
        public string AssetPath => $"{SpellAssetRoot}/Spell_{SpellName}.asset";

        // Optional by convention: a spell without a matching file stays silent.
        public string CastClipPath => $"{AudioRoot}/{SpellName}_Spell.wav";
    }

    private static readonly SpellSheet[] Sheets =
    {
        new("Magic_Spell1_Astral", "Astral", 48, 0),
        new("Magic_Spell2_Poison", "Poison", 64, 1),
        new("Magic_Spell3_Hollow", "Hollow", 48, 2)
    };

    static SpellAssetBuilder()
    {
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
            importer.SaveAndReimport();
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
        (providerImporter != null ? providerImporter : _importer).SaveAndReimport();
    }

    private static SpellDefinition CreateOrUpdateSpell(SpellSheet _sheet)
    {
        SpellDefinition spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(_sheet.AssetPath);

        if (spell == null)
        {
            spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.SpellName = _sheet.SpellName;
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

            if (prefabChanged)
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
