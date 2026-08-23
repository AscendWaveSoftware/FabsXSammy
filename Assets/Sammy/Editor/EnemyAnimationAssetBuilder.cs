using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Slices the enemy sprite sheets, rebuilds their clips and controller and wires
/// the enemy prefab. Mirrors <see cref="PlayerAnimationAssetBuilder"/> so the
/// enemy setup is just as reproducible instead of assembled by hand.
/// </summary>
[InitializeOnLoad]
public static class EnemyAnimationAssetBuilder
{
    private const string SessionKey = "Sammy.EnemyAnimationAssets.V4";
    private const string TextureRoot = "Assets/Sammy/Textures/Enemies";
    private const string OutputRoot = "Assets/Sammy/Animations/Enemy";
    private const int FrameSize = 100;
    private const float PixelsPerUnit = 100f;

    private readonly struct SheetDefinition
    {
        public SheetDefinition(
            string _monsterName,
            string _spritePrefix,
            string _textureFolder,
            string _fileName,
            string _stateName,
            int _frameCount,
            float _frameRate,
            bool _loop)
        {
            MonsterName = _monsterName;
            SpritePrefix = _spritePrefix;
            TextureFolder = _textureFolder;
            FileName = _fileName;
            StateName = _stateName;
            FrameCount = _frameCount;
            FrameRate = _frameRate;
            Loop = _loop;
        }

        public string MonsterName { get; }
        public string SpritePrefix { get; }
        public string TextureFolder { get; }
        public string FileName { get; }
        public string StateName { get; }
        public int FrameCount { get; }
        public float FrameRate { get; }
        public bool Loop { get; }
        public string TexturePath => $"{TextureRoot}/{TextureFolder}/{FileName}.png";
        public string ClipPath => $"{OutputRoot}/{MonsterName} {StateName}.anim";
    }

    private readonly struct MonsterDefinition
    {
        public MonsterDefinition(
            string _name,
            string _spritePrefix,
            string _textureFolder,
            string _filePrefix,
            string _prefabPath,
            float _targetBodyHeight,
            int _attack1Frames,
            int _attack2Frames)
        {
            Name = _name;
            PrefabPath = _prefabPath;
            TargetBodyHeight = _targetBodyHeight;

            // Attack runs at roughly the 0.28s windup and hurt at the 0.24s hit
            // stun, so the animation lines up with the gameplay it belongs to.
            // The attack clips are time stretched onto the real windup anyway.
            Sheets = new[]
            {
                new SheetDefinition(_name, _spritePrefix, _textureFolder, $"{_filePrefix}Idle", "Idle", 6, 8f, true),
                new SheetDefinition(_name, _spritePrefix, _textureFolder, $"{_filePrefix}Walk", "Walk", 8, 12f, true),
                new SheetDefinition(_name, _spritePrefix, _textureFolder, $"{_filePrefix}Attack01", "Attack1", _attack1Frames, 28f, false),
                new SheetDefinition(_name, _spritePrefix, _textureFolder, $"{_filePrefix}Attack02", "Attack2", _attack2Frames, 28f, false),
                new SheetDefinition(_name, _spritePrefix, _textureFolder, $"{_filePrefix}Hurt", "Hurt", 4, 16f, false)
            };
        }

        public string Name { get; }
        public string PrefabPath { get; }

        /// <summary>Body height in world units, measured from the artwork's own pixels.</summary>
        public float TargetBodyHeight { get; }

        public SheetDefinition[] Sheets { get; }
        public string ControllerPath => $"{OutputRoot}/{Name}.controller";
    }

    /// <summary>
    /// The sprite prefix is part of every sliced sprite's name, and a sprite's id
    /// is looked up by that name on a reslice. Renaming one would hand out fresh
    /// ids and leave the existing clips pointing at sprites that no longer exist,
    /// so "MonsterA" has to stay exactly as it is.
    /// </summary>
    private static readonly MonsterDefinition[] Monsters =
    {
        new("Monster A", "MonsterA", "Monster A", "Blood Monster_A_",
            "Assets/Sammy/Prefabs/Enemy.prefab", 1.5f, 8, 8),

        // Taller than the player on purpose: this one carries twice the health, and
        // the size is what tells the player that at a glance.
        new("Monster B", "MonsterB", "Monster B", "Demon_A_",
            "Assets/Sammy/Prefabs/Enemy Variant Strong.prefab", 2f, 7, 7)
    };

    static EnemyAnimationAssetBuilder()
    {
        EditorApplication.delayCall += BuildOncePerSession;
    }

    [MenuItem("Tools/Sammy/Rebuild Enemy Animations")]
    public static void RebuildFromMenu()
    {
        SessionState.EraseBool(SessionKey);
        BuildOncePerSession();
    }

    private static void BuildOncePerSession()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Reslicing while the pipeline is still busy leaves the new sprites
        // invisible to the AssetDatabase for the rest of this callback, so the
        // build waits for a quiet tick instead of racing it.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildOncePerSession;
            return;
        }

        SessionState.SetBool(SessionKey, true);

        try
        {
            EnsureOutputFolders();

            foreach (MonsterDefinition monster in Monsters)
            {
                foreach (SheetDefinition sheet in monster.Sheets)
                    ConfigureAndSliceSheet(sheet);

                Dictionary<string, AnimationClip> clips = new();

                foreach (SheetDefinition sheet in monster.Sheets)
                    clips[sheet.StateName] = CreateOrUpdateClip(sheet);

                AnimatorController controller = CreateOrUpdateController(monster, clips);
                ConfigureEnemyPrefab(monster, controller, clips);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Enemy animations rebuilt successfully for {Monsters.Length} monsters.");
        }
        catch (Exception exception)
        {
            SessionState.EraseBool(SessionKey);
            Debug.LogException(exception);
        }
    }

    private static void EnsureOutputFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Sammy/Animations"))
            AssetDatabase.CreateFolder("Assets/Sammy", "Animations");

        if (!AssetDatabase.IsValidFolder(OutputRoot))
            AssetDatabase.CreateFolder("Assets/Sammy/Animations", "Enemy");
    }

    private static void ConfigureAndSliceSheet(SheetDefinition _sheet)
    {
        TextureImporter importer = AssetImporter.GetAtPath(_sheet.TexturePath) as TextureImporter;

        if (importer == null)
            throw new InvalidOperationException($"Enemy animation sheet is missing: {_sheet.TexturePath}");

        // Measured on the source file. An unsliced sheet still counts as a plain
        // texture, and those are scaled up to the next power of two on import.
        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);

        if (sourceHeight != FrameSize || sourceWidth != _sheet.FrameCount * FrameSize)
        {
            throw new InvalidOperationException(
                $"Enemy sheet {_sheet.TexturePath} must be {_sheet.FrameCount * FrameSize}x{FrameSize}, " +
                $"but is {sourceWidth}x{sourceHeight}."
            );
        }

        // Two passes, because the sprite data provider builds its own serialised
        // view of the importer and would discard the type switch on a sheet that
        // is still a plain texture.
        if (ApplyImporterSettings(importer))
        {
            PersistImporter(importer, _sheet.TexturePath);
            importer = AssetImporter.GetAtPath(_sheet.TexturePath) as TextureImporter;

            if (importer == null)
                throw new InvalidOperationException($"Enemy sheet vanished while importing: {_sheet.TexturePath}");
        }

        // The slicing below can only work on a sheet that already imports as a
        // multiple sprite. Failing loudly here points at the real cause instead
        // of surfacing later as an empty clip.
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            throw new InvalidOperationException(
                $"Import settings for {_sheet.TexturePath} did not stick: " +
                $"textureType={importer.textureType}, spriteImportMode={importer.spriteImportMode}. " +
                "The file is probably locked or owned by version control.");
        }

        ApplySpriteRects(importer, _sheet);
    }

    /// <summary>
    /// Writes importer changes out explicitly. SaveAndReimport is supposed to do
    /// this on its own, but silently keeps them in memory when the pipeline is
    /// mid-batch, which leaves the sheet looking untouched.
    /// </summary>
    private static void PersistImporter(TextureImporter _importer, string _assetPath)
    {
        EditorUtility.SetDirty(_importer);
        AssetDatabase.WriteImportSettingsIfDirty(_assetPath);
        AssetDatabase.ImportAsset(_assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    private static Sprite[] LoadFrames(SheetDefinition _sheet) =>
        AssetDatabase.LoadAllAssetsAtPath(_sheet.TexturePath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();

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

    private static void ApplySpriteRects(TextureImporter _importer, SheetDefinition _sheet)
    {
        SpriteDataProviderFactories factories = new();
        factories.Init();
        ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(_importer);
        dataProvider.InitSpriteEditorDataProvider();

        SpriteRect[] existingRects = dataProvider.GetSpriteRects();
        bool hasExpectedRects = existingRects.Length == _sheet.FrameCount;

        for (int i = 0; hasExpectedRects && i < existingRects.Length; i++)
        {
            Rect expectedRect = new(i * FrameSize, 0f, FrameSize, FrameSize);
            hasExpectedRects = existingRects[i].name == GetSpriteName(_sheet, i) &&
                               existingRects[i].rect == expectedRect &&
                               existingRects[i].alignment == SpriteAlignment.Center &&
                               existingRects[i].pivot == new Vector2(0.5f, 0.5f) &&
                               existingRects[i].border == Vector4.zero;
        }

        if (hasExpectedRects)
            return;

        // Existing ids are reused so references already pointing at a frame
        // survive a reslice instead of turning into missing sprites.
        Dictionary<string, GUID> existingSpriteIds = existingRects
            .GroupBy(rect => rect.name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().spriteID, StringComparer.Ordinal);
        SpriteRect[] spriteRects = new SpriteRect[_sheet.FrameCount];

        for (int i = 0; i < spriteRects.Length; i++)
        {
            string spriteName = GetSpriteName(_sheet, i);
            spriteRects[i] = new SpriteRect
            {
                name = spriteName,

                // Every frame keeps the full cell and a centred pivot, which is
                // what holds the monster's feet on the same line across clips.
                rect = new Rect(i * FrameSize, 0f, FrameSize, FrameSize),
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

        // Saved through the provider's own importer instance. Apply writes into
        // that object, so saving the one we started from would drop the slicing.
        AssetImporter providerImporter = dataProvider.targetObject as AssetImporter;
        PersistImporter(
            (providerImporter as TextureImporter) != null ? (TextureImporter)providerImporter : _importer,
            _sheet.TexturePath);

        int slicedCount = LoadFrames(_sheet).Length;

        if (slicedCount != _sheet.FrameCount)
        {
            throw new InvalidOperationException(
                $"Slicing {_sheet.TexturePath} produced {slicedCount} sprites instead of {_sheet.FrameCount}.");
        }
    }

    private static AnimationClip CreateOrUpdateClip(SheetDefinition _sheet)
    {
        Sprite[] sprites = LoadFrames(_sheet);

        if (sprites.Length != _sheet.FrameCount)
        {
            throw new InvalidOperationException(
                $"Expected {_sheet.FrameCount} sprites in {_sheet.TexturePath}, found {sprites.Length}. " +
                "The sheet did not finish slicing; run Tools/Sammy/Rebuild Enemy Animations once the editor is idle.");
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(_sheet.ClipPath);
        bool clipChanged = false;

        if (clip == null)
        {
            clip = new AnimationClip { name = _sheet.StateName };
            AssetDatabase.CreateAsset(clip, _sheet.ClipPath);
            clipChanged = true;
        }

        if (!Mathf.Approximately(clip.frameRate, _sheet.FrameRate))
        {
            clip.frameRate = _sheet.FrameRate;
            clipChanged = true;
        }

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Length];

        for (int i = 0; i < sprites.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / _sheet.FrameRate,
                value = sprites[i]
            };
        }

        EditorCurveBinding spriteBinding = EditorCurveBinding.PPtrCurve("Visual", typeof(SpriteRenderer), "m_Sprite");
        ObjectReferenceKeyframe[] existingKeyframes = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);

        if (!KeyframesMatch(existingKeyframes, keyframes))
        {
            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
            clipChanged = true;
        }

        SerializedObject serializedClip = new(clip);
        SerializedProperty loopTime = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");

        if (loopTime != null && loopTime.boolValue != _sheet.Loop)
        {
            loopTime.boolValue = _sheet.Loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            clipChanged = true;
        }

        if (clipChanged)
            EditorUtility.SetDirty(clip);

        return clip;
    }

    private static AnimatorController CreateOrUpdateController(
        MonsterDefinition _monster,
        IReadOnlyDictionary<string, AnimationClip> _clips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_monster.ControllerPath);
        bool controllerChanged = false;

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(_monster.ControllerPath);
            controllerChanged = true;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        Dictionary<string, AnimatorState> existingStates = stateMachine.states
            .Where(child => child.state != null)
            .GroupBy(child => child.state.name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().state, StringComparer.Ordinal);
        HashSet<string> expectedStateNames = _monster.Sheets.Select(sheet => sheet.StateName).ToHashSet(StringComparer.Ordinal);

        foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
        {
            if (childState.state == null || !expectedStateNames.Contains(childState.state.name) ||
                existingStates[childState.state.name] != childState.state)
            {
                stateMachine.RemoveState(childState.state);
                controllerChanged = true;
            }
        }

        AnimatorState idleState = null;

        foreach (SheetDefinition sheet in _monster.Sheets)
        {
            if (!existingStates.TryGetValue(sheet.StateName, out AnimatorState state) || state == null)
            {
                state = stateMachine.AddState(sheet.StateName);
                controllerChanged = true;
            }

            if (state.motion != _clips[sheet.StateName])
            {
                state.motion = _clips[sheet.StateName];
                EditorUtility.SetDirty(state);
                controllerChanged = true;
            }

            if (!state.writeDefaultValues)
            {
                state.writeDefaultValues = true;
                EditorUtility.SetDirty(state);
                controllerChanged = true;
            }

            if (sheet.StateName == "Idle")
                idleState = state;
        }

        if (stateMachine.defaultState != idleState)
        {
            stateMachine.defaultState = idleState;
            controllerChanged = true;
        }

        if (controllerChanged)
        {
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        return controller;
    }

    private static void ConfigureEnemyPrefab(
        MonsterDefinition _monster,
        AnimatorController _controller,
        IReadOnlyDictionary<string, AnimationClip> _clips)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(_monster.PrefabPath);
        bool prefabChanged = false;

        try
        {
            Transform visual = root.transform.Find("Visual");

            if (visual == null)
                throw new InvalidOperationException($"{_monster.PrefabPath} has no Visual child.");

            SpriteRenderer spriteRenderer = visual.GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
                throw new InvalidOperationException($"{_monster.PrefabPath} Visual has no SpriteRenderer.");

            // Measured from the collider, not from the prefab's own position: the
            // spawner drops the enemy at a spawn point and overwrites that, while
            // the collider bottom is what actually rests on the ground.
            MonsterMetrics metrics = MeasureMonster(_monster);
            float groundBelowRoot = GetColliderBottomOffset(root);

            // The feet rest on the ground line, so the top of the artwork ends up
            // this far above the root. Everything that hovers over the monster -
            // damage numbers, warning signs - measures from here rather than from
            // the sprite bounds, which report the empty frame around the artwork.
            float headAboveRoot = metrics.BodyHeightUnits - groundBelowRoot;

            prefabChanged |= ApplyVisual(_monster, visual, spriteRenderer, metrics, groundBelowRoot);

            Animator animator = root.GetComponent<Animator>();

            if (animator == null)
            {
                animator = root.AddComponent<Animator>();
                prefabChanged = true;
            }

            if (animator.runtimeAnimatorController != _controller)
            {
                animator.runtimeAnimatorController = _controller;
                prefabChanged = true;
            }

            if (animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
                prefabChanged = true;
            }

            if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                prefabChanged = true;
            }

            prefabChanged |= ConfigureAnimationController(root, animator, spriteRenderer, _clips);
            prefabChanged |= ConfigureTelegraphBounds(root, headAboveRoot);
            prefabChanged |= ConfigureDamageTextBounds(root, headAboveRoot);
            prefabChanged |= EnsureComponent<EnemyDeathBurst>(root);

            if (prefabChanged)
                PrefabUtility.SaveAsPrefabAsset(root, _monster.PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>How far the collider reaches below the root, which is the ground line.</summary>
    private static float GetColliderBottomOffset(GameObject _root)
    {
        Collider enemyCollider = _root.GetComponent<Collider>();

        if (enemyCollider == null)
            return 0.5f;

        return _root.transform.position.y - enemyCollider.bounds.min.y;
    }

    private static bool ApplyVisual(
        MonsterDefinition _monster,
        Transform _visual,
        SpriteRenderer _renderer,
        MonsterMetrics _metrics,
        float _groundBelowRoot)
    {
        bool changed = false;

        Sprite idleSprite = AssetDatabase.LoadAllAssetsAtPath(_monster.Sheets[0].TexturePath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (idleSprite == null)
            throw new InvalidOperationException("Enemy idle sprite is missing.");

        if (_renderer.sprite != idleSprite)
        {
            _renderer.sprite = idleSprite;
            changed = true;
        }

        // The placeholder was tinted red. The artwork brings its own colour now,
        // and a tint would fight the windup flash that multiplies on top of it.
        if (_renderer.color != Color.white)
        {
            _renderer.color = Color.white;
            changed = true;
        }

        Vector3 expectedScale = Vector3.one * _metrics.Scale;

        if (_visual.localScale != expectedScale)
        {
            _visual.localScale = expectedScale;
            changed = true;
        }

        // Lifted so the measured feet line lands exactly on the ground line.
        float feetBelowPivot = _metrics.FeetOffsetPixels / PixelsPerUnit * _metrics.Scale;
        Vector3 expectedPosition = Vector3.up * (feetBelowPivot - _groundBelowRoot);

        if ((_visual.localPosition - expectedPosition).sqrMagnitude > 0.000001f)
        {
            _visual.localPosition = expectedPosition;
            changed = true;
        }

        return changed;
    }

    private static bool ConfigureAnimationController(
        GameObject _root,
        Animator _animator,
        SpriteRenderer _renderer,
        IReadOnlyDictionary<string, AnimationClip> _clips)
    {
        EnemyAnimationController animationController = _root.GetComponent<EnemyAnimationController>();
        bool changed = false;

        if (animationController == null)
        {
            animationController = _root.AddComponent<EnemyAnimationController>();
            changed = true;
        }

        SerializedObject serialized = new(animationController);
        changed |= SetObjectReference(serialized.FindProperty("m_animator"), _animator);
        changed |= SetObjectReference(serialized.FindProperty("m_bodyRenderer"), _renderer);
        changed |= SetObjectReference(serialized.FindProperty("m_movement"), _root.GetComponent<EnemyMovement>());
        changed |= SetObjectReference(serialized.FindProperty("m_attack"), _root.GetComponent<EnemyAttack>());
        changed |= SetObjectReference(serialized.FindProperty("m_stats"), _root.GetComponent<EnemyStats>());
        changed |= SetObjectReference(serialized.FindProperty("m_hurtClip"), _clips["Hurt"]);

        SerializedProperty attackClips = serialized.FindProperty("m_attackClips");

        if (attackClips == null)
            throw new InvalidOperationException("EnemyAnimationController attack clip array no longer exists.");

        if (attackClips.arraySize != EnemyAnimationController.AttackVariantCount)
        {
            attackClips.arraySize = EnemyAnimationController.AttackVariantCount;
            changed = true;
        }

        changed |= SetObjectReference(attackClips.GetArrayElementAtIndex(0), _clips["Attack1"]);
        changed |= SetObjectReference(attackClips.GetArrayElementAtIndex(1), _clips["Attack2"]);

        if (serialized.hasModifiedProperties)
            serialized.ApplyModifiedPropertiesWithoutUndo();

        return changed;
    }

    private static bool ConfigureTelegraphBounds(GameObject _root, float _headAboveRoot)
    {
        EnemyAttackTelegraph telegraph = _root.GetComponent<EnemyAttackTelegraph>();
        bool changed = false;

        // EnemyAttack adds this at runtime when it is missing, which would leave
        // the measured head height nowhere to live. Putting it on the prefab is
        // also what lets the value be seen and tweaked in the Inspector.
        if (telegraph == null)
        {
            telegraph = _root.AddComponent<EnemyAttackTelegraph>();
            changed = true;
        }

        SerializedObject serialized = new(telegraph);
        SerializedProperty headOffset = serialized.FindProperty("m_headOffsetOverride");

        if (headOffset == null)
            return changed;

        if (!Mathf.Approximately(headOffset.floatValue, _headAboveRoot))
        {
            headOffset.floatValue = _headAboveRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Damage numbers rise from the top of the enemy, which they normally find by
    /// scanning the sprite bounds. A padded animation frame reports itself as far
    /// taller than the monster, so the measured height is handed over instead.
    /// </summary>
    private static bool ConfigureDamageTextBounds(GameObject _root, float _headAboveRoot)
    {
        EnemyStats stats = _root.GetComponent<EnemyStats>();

        if (stats == null)
            return false;

        SerializedObject serialized = new(stats);
        SerializedProperty headOverride = serialized.FindProperty("damageTextHeadOverride");

        if (headOverride == null || Mathf.Approximately(headOverride.floatValue, _headAboveRoot))
            return false;

        headOverride.floatValue = _headAboveRoot;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return true;
    }

    private static bool EnsureComponent<T>(GameObject _root) where T : Component
    {
        if (_root.GetComponent<T>() != null)
            return false;

        _root.AddComponent<T>();
        return true;
    }

    private readonly struct MonsterMetrics
    {
        public MonsterMetrics(float _scale, float _feetOffsetPixels, float _headOffsetPixels)
        {
            Scale = _scale;
            FeetOffsetPixels = _feetOffsetPixels;
            HeadOffsetPixels = _headOffsetPixels;
        }

        public float Scale { get; }

        /// <summary>Distance from the centred pivot down to the lowest opaque pixel.</summary>
        public float FeetOffsetPixels { get; }

        /// <summary>Distance from the pivot up to the highest opaque pixel, at scale 1.</summary>
        public float HeadOffsetPixels { get; }

        /// <summary>Height of the artwork itself in world units, padding excluded.</summary>
        public float BodyHeightUnits => (FeetOffsetPixels + HeadOffsetPixels) / PixelsPerUnit * Scale;
    }

    /// <summary>
    /// Reads the real extent of the artwork out of the idle and walk sheets. The
    /// frame is mostly transparent padding, so every placement has to come from
    /// the pixels rather than from the sprite bounds.
    /// </summary>
    private static MonsterMetrics MeasureMonster(MonsterDefinition _monster)
    {
        int lowestOpaqueRow = int.MaxValue;
        int highestOpaqueRow = int.MinValue;

        // Only the looping sheets. An attack frame reaches out with claws well past
        // the body, and measuring that would shrink the monster to fit its lunge.
        foreach (SheetDefinition sheet in _monster.Sheets.Where(sheet => sheet.Loop))
        {
            // Read straight off disk, which sidesteps having to mark the imported
            // texture readable just to measure it once.
            byte[] fileBytes = System.IO.File.ReadAllBytes(sheet.TexturePath);

            Texture2D readable = new(2, 2, TextureFormat.RGBA32, false);

            try
            {
                if (!readable.LoadImage(fileBytes))
                    throw new InvalidOperationException($"Could not read {sheet.TexturePath} for measuring.");

                Color32[] pixels = readable.GetPixels32();

                for (int y = 0; y < readable.height; y++)
                {
                    for (int x = 0; x < readable.width; x++)
                    {
                        if (pixels[y * readable.width + x].a <= 16)
                            continue;

                        lowestOpaqueRow = Mathf.Min(lowestOpaqueRow, y);
                        highestOpaqueRow = Mathf.Max(highestOpaqueRow, y);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        if (lowestOpaqueRow > highestOpaqueRow)
            throw new InvalidOperationException($"{_monster.Name} sheets contain no visible pixels.");

        float bodyHeightPixels = Mathf.Max(1f, highestOpaqueRow - lowestOpaqueRow + 1);
        float scale = _monster.TargetBodyHeight / (bodyHeightPixels / PixelsPerUnit);

        float pivotRow = FrameSize * 0.5f;
        return new MonsterMetrics(scale, pivotRow - lowestOpaqueRow, highestOpaqueRow + 1 - pivotRow);
    }

    private static string GetSpriteName(SheetDefinition _sheet, int _index) =>
        $"{_sheet.SpritePrefix}_{_sheet.StateName}_{_index:00}";

    private static bool KeyframesMatch(ObjectReferenceKeyframe[] _existing, ObjectReferenceKeyframe[] _expected)
    {
        if (_existing == null || _existing.Length != _expected.Length)
            return false;

        for (int i = 0; i < _existing.Length; i++)
        {
            if (!Mathf.Approximately(_existing[i].time, _expected[i].time) ||
                _existing[i].value != _expected[i].value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SetObjectReference(SerializedProperty _property, UnityEngine.Object _value)
    {
        if (_property == null)
            throw new InvalidOperationException("A required serialized enemy animation reference no longer exists.");

        if (_property.objectReferenceValue == _value)
            return false;

        _property.objectReferenceValue = _value;
        return true;
    }
}
