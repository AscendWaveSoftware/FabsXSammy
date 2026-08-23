using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Audio;

[InitializeOnLoad]
public static class PlayerAnimationAssetBuilder
{
    private const string SessionKey = "Sammy.PlayerAnimationAssets.V13";

    /// <summary>
    /// Mixer group every sound from this half of the game is routed through. It
    /// lives on the other branch, so it is looked up by name rather than held as
    /// a reference and simply stays unassigned until the asset arrives.
    /// </summary>
    private const string ArenaMixerGroupName = "Arena";
    private const string TextureRoot = "Assets/Sammy/Textures/Player";
    private const string AudioRoot = "Assets/Sammy/Audio";
    private const string OutputRoot = "Assets/Sammy/Animations/Player";
    private const string ControllerPath = OutputRoot + "/Player.controller";
    private const string PlayerPrefabPath = "Assets/Sammy/Prefabs/Player.prefab";
    private const int FrameWidth = 96;
    private const int FrameHeight = 84;
    private const float PixelsPerUnit = 100f;
    private const float VisualScale = 5.4f;
    private const float VisualGroundOffset = 0.08f;

    private readonly struct SheetDefinition
    {
        public SheetDefinition(string fileName, string stateName, int frameCount, float frameRate, bool loop)
        {
            FileName = fileName;
            StateName = stateName;
            FrameCount = frameCount;
            FrameRate = frameRate;
            Loop = loop;
        }

        public string FileName { get; }
        public string StateName { get; }
        public int FrameCount { get; }
        public float FrameRate { get; }
        public bool Loop { get; }
        public string TexturePath => $"{TextureRoot}/{FileName}.png";
        public string ClipPath => $"{OutputRoot}/{StateName}.anim";
    }

    private static readonly SheetDefinition[] Sheets =
    {
        new("IDLE", "Idle", 7, 8f, true),
        new("WALK", "Walk", 8, 10f, true),
        new("RUN", "Run", 8, 14f, true),
        new("ATTACK 1", "Attack1", 6, 14f, false),
        new("ATTACK 2", "Attack2", 5, 14f, false),
        new("ATTACK 3", "Attack3", 6, 14f, false),
        new("DEFEND", "Defend", 6, 12f, true),
        new("HURT", "Hurt", 4, 12f, false)
    };

    private static readonly string[] SwingClipPaths =
    {
        AudioRoot + "/SwordSwing_1.wav",
        AudioRoot + "/SwordSwing_2.wav",
        AudioRoot + "/SwordSwing_3.wav"
    };

    private const string BlockClipPath = AudioRoot + "/Blocking.wav";

    static PlayerAnimationAssetBuilder()
    {
        EditorApplication.delayCall += BuildOncePerSession;
    }

    [MenuItem("Tools/Sammy/Rebuild Player Animations _F8")]
    public static void RebuildFromMenu()
    {
        SessionState.EraseBool(SessionKey);
        BuildOncePerSession();
    }

    private static void BuildOncePerSession()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        SessionState.SetBool(SessionKey, true);

        try
        {
            EnsureOutputFolders();

            foreach (SheetDefinition sheet in Sheets)
                ConfigureAndSliceSheet(sheet);

            Dictionary<string, AnimationClip> clips = new();

            foreach (SheetDefinition sheet in Sheets)
                clips[sheet.StateName] = CreateOrUpdateClip(sheet);

            AnimatorController controller = CreateOrUpdateController(clips);
            ConfigurePlayerPrefab(controller, clips);
            AssetDatabase.SaveAssets();
            Debug.Log("Player animations rebuilt successfully.");
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
            AssetDatabase.CreateFolder("Assets/Sammy/Animations", "Player");
    }

    private static void ConfigureAndSliceSheet(SheetDefinition _sheet)
    {
        TextureImporter importer = AssetImporter.GetAtPath(_sheet.TexturePath) as TextureImporter;

        if (importer == null)
            throw new InvalidOperationException($"Player animation texture is missing: {_sheet.TexturePath}");

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(_sheet.TexturePath);

        if (texture == null || texture.width != _sheet.FrameCount * FrameWidth || texture.height != FrameHeight)
        {
            string actualSize = texture == null ? "unavailable" : $"{texture.width}x{texture.height}";
            throw new InvalidOperationException(
                $"Player animation sheet {_sheet.TexturePath} must be {_sheet.FrameCount * FrameWidth}x{FrameHeight}, " +
                $"but is {actualSize}."
            );
        }

        TextureImporterSettings textureSettings = new();
        importer.ReadTextureSettings(textureSettings);
        bool importerChanged = importer.textureType != TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Multiple ||
                               !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) ||
                               importer.filterMode != FilterMode.Point ||
                               importer.mipmapEnabled ||
                               !importer.alphaIsTransparency ||
                               importer.wrapMode != TextureWrapMode.Clamp ||
                               importer.textureCompression != TextureImporterCompression.Uncompressed ||
                               textureSettings.spriteMeshType != SpriteMeshType.FullRect ||
                               textureSettings.spriteGenerateFallbackPhysicsShape;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        textureSettings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(textureSettings);

        SpriteDataProviderFactories factories = new();
        factories.Init();
        ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        SpriteRect[] existingRects = dataProvider.GetSpriteRects();
        bool hasExpectedRects = existingRects.Length == _sheet.FrameCount;

        for (int i = 0; hasExpectedRects && i < existingRects.Length; i++)
        {
            Rect expectedRect = new(i * FrameWidth, 0f, FrameWidth, FrameHeight);
            hasExpectedRects = existingRects[i].name == GetSpriteName(_sheet, i) &&
                               existingRects[i].rect == expectedRect &&
                               existingRects[i].alignment == SpriteAlignment.Center &&
                               existingRects[i].pivot == new Vector2(0.5f, 0.5f) &&
                               existingRects[i].border == Vector4.zero;
        }

        if (!hasExpectedRects)
        {
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
                    rect = new Rect(i * FrameWidth, 0f, FrameWidth, FrameHeight),
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
            importerChanged = true;
        }

        if (importerChanged)
            importer.SaveAndReimport();
    }

    private static AnimationClip CreateOrUpdateClip(SheetDefinition _sheet)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(_sheet.TexturePath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();

        if (sprites.Length != _sheet.FrameCount)
            throw new InvalidOperationException($"Expected {_sheet.FrameCount} sprites in {_sheet.TexturePath}, found {sprites.Length}.");

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

    private static AnimatorController CreateOrUpdateController(IReadOnlyDictionary<string, AnimationClip> _clips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        bool controllerChanged = false;

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controllerChanged = true;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        Dictionary<string, AnimatorState> existingStates = stateMachine.states
            .Where(child => child.state != null)
            .GroupBy(child => child.state.name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().state, StringComparer.Ordinal);
        HashSet<string> expectedStateNames = Sheets
            .Select(sheet => sheet.StateName)
            .ToHashSet(StringComparer.Ordinal);

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

        foreach (SheetDefinition sheet in Sheets)
        {
            AnimatorState state;

            if (!existingStates.TryGetValue(sheet.StateName, out state) || state == null)
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

    private static void ConfigurePlayerPrefab(AnimatorController _controller, IReadOnlyDictionary<string, AnimationClip> _clips)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        bool prefabChanged = false;

        try
        {
            Transform visual = root.transform.Find("Visual");

            if (visual == null)
                throw new InvalidOperationException("Player prefab has no Visual child.");

            Vector3 expectedPosition = Vector3.up * VisualGroundOffset;
            Vector3 expectedScale = Vector3.one * VisualScale;

            if (visual.localPosition != expectedPosition)
            {
                visual.localPosition = expectedPosition;
                prefabChanged = true;
            }

            if (visual.localScale != expectedScale)
            {
                visual.localScale = expectedScale;
                prefabChanged = true;
            }

            SpriteRenderer spriteRenderer = visual.GetComponent<SpriteRenderer>();
            Sprite idleSprite = AssetDatabase.LoadAllAssetsAtPath(Sheets[0].TexturePath)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                .FirstOrDefault();

            if (spriteRenderer == null || idleSprite == null)
                throw new InvalidOperationException("Player prefab Visual or its idle sprite is missing.");

            if (spriteRenderer.sprite != idleSprite)
            {
                spriteRenderer.sprite = idleSprite;
                prefabChanged = true;
            }

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

            if (animator.updateMode != AnimatorUpdateMode.Normal)
            {
                animator.updateMode = AnimatorUpdateMode.Normal;
                prefabChanged = true;
            }

            if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                prefabChanged = true;
            }

            PlayerAnimationController animationController = root.GetComponent<PlayerAnimationController>();

            if (animationController == null)
            {
                animationController = root.AddComponent<PlayerAnimationController>();
                prefabChanged = true;
            }

            Rigidbody rigidbody = root.GetComponent<Rigidbody>();
            PlayerMovementHandler movement = root.GetComponent<PlayerMovementHandler>();
            PlayerCombat combat = root.GetComponent<PlayerCombat>();
            PlayerHealth health = root.GetComponent<PlayerHealth>();

            if (rigidbody == null || movement == null || combat == null || health == null)
                throw new InvalidOperationException("Player prefab is missing a required gameplay component for animation wiring.");

            SerializedObject serializedController = new(animationController);
            prefabChanged |= SetObjectReference(serializedController.FindProperty("m_animator"), animator);
            prefabChanged |= SetObjectReference(serializedController.FindProperty("m_rigidbody"), rigidbody);
            prefabChanged |= SetObjectReference(serializedController.FindProperty("m_movement"), movement);
            prefabChanged |= SetObjectReference(serializedController.FindProperty("m_combat"), combat);
            prefabChanged |= SetObjectReference(serializedController.FindProperty("m_health"), health);
            prefabChanged |= SetObjectReference(serializedController.FindProperty("m_defendClip"), _clips["Defend"]);
            prefabChanged |= SetObjectReference(serializedController.FindProperty("m_hurtClip"), _clips["Hurt"]);

            SerializedProperty attackClips = serializedController.FindProperty("m_attackClips");

            if (attackClips == null)
                throw new InvalidOperationException("PlayerAnimationController attack clip array no longer exists.");

            if (attackClips.arraySize != 3)
            {
                attackClips.arraySize = 3;
                prefabChanged = true;
            }

            prefabChanged |= SetObjectReference(attackClips.GetArrayElementAtIndex(0), _clips["Attack1"]);
            prefabChanged |= SetObjectReference(attackClips.GetArrayElementAtIndex(1), _clips["Attack2"]);
            prefabChanged |= SetObjectReference(attackClips.GetArrayElementAtIndex(2), _clips["Attack3"]);

            if (serializedController.hasModifiedProperties)
                serializedController.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedCombat = new(combat);
            prefabChanged |= SetObjectReference(serializedCombat.FindProperty("m_playerAnimation"), animationController);
            prefabChanged |= SetObjectReference(serializedCombat.FindProperty("m_playerHealth"), health);

            if (serializedCombat.hasModifiedProperties)
                serializedCombat.ApplyModifiedPropertiesWithoutUndo();

            prefabChanged |= ConfigureCombatAudio(root, animationController);
            prefabChanged |= ConfigureBlockFeedback(root, health, spriteRenderer);
            prefabChanged |= ConfigureFootstepDust(root, rigidbody, movement, health);

            // Purely visual and fully self-configuring, so it only has to exist.
            if (root.GetComponent<CharacterDropShadow>() == null)
            {
                root.AddComponent<CharacterDropShadow>();
                prefabChanged = true;
            }

            if (prefabChanged)
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool ConfigureCombatAudio(GameObject _root, PlayerAnimationController _animationController)
    {
        PlayerCombatAudio combatAudio = _root.GetComponent<PlayerCombatAudio>();
        bool prefabChanged = false;

        if (combatAudio == null)
        {
            combatAudio = _root.AddComponent<PlayerCombatAudio>();
            prefabChanged = true;
        }

        SerializedObject serializedCombatAudio = new(combatAudio);
        prefabChanged |= SetObjectReference(serializedCombatAudio.FindProperty("m_playerAnimation"), _animationController);
        prefabChanged |= SetObjectReference(serializedCombatAudio.FindProperty("m_playerHealth"), _root.GetComponent<PlayerHealth>());
        prefabChanged |= SetObjectReference(serializedCombatAudio.FindProperty("m_playerSpellCaster"), _root.GetComponent<PlayerSpellCaster>());
        prefabChanged |= SetObjectReference(serializedCombatAudio.FindProperty("m_blockClip"), LoadCombatClip(BlockClipPath));

        SerializedProperty swingClips = serializedCombatAudio.FindProperty("m_swingClips");

        if (swingClips == null)
            throw new InvalidOperationException("PlayerCombatAudio swing clip array no longer exists.");

        if (swingClips.arraySize != SwingClipPaths.Length)
        {
            swingClips.arraySize = SwingClipPaths.Length;
            prefabChanged = true;
        }

        for (int i = 0; i < SwingClipPaths.Length; i++)
            prefabChanged |= SetObjectReference(swingClips.GetArrayElementAtIndex(i), LoadCombatClip(SwingClipPaths[i]));

        // Only written when the group is actually there. Assigning null instead
        // would clear a reference that someone wired by hand, and the sources
        // already fall back to the default output on their own.
        AudioMixerGroup arenaGroup = FindArenaMixerGroup();

        if (arenaGroup != null)
            prefabChanged |= SetObjectReference(serializedCombatAudio.FindProperty("m_outputMixerGroup"), arenaGroup);

        if (serializedCombatAudio.hasModifiedProperties)
            serializedCombatAudio.ApplyModifiedPropertiesWithoutUndo();

        return prefabChanged;
    }

    /// <summary>
    /// Finds the arena mixer group anywhere in the project. Both readings of
    /// "the Arena mixer" are covered: a group called Arena inside some mixer, and
    /// a whole mixer asset named Arena, whose first group is then the target.
    ///
    /// Returns null while the mixer is still missing from this branch, which is a
    /// normal state and not an error.
    /// </summary>
    private static AudioMixerGroup FindArenaMixerGroup()
    {
        List<AudioMixerGroup> groupsOfArenaMixer = null;

        foreach (string mixerGuid in AssetDatabase.FindAssets("t:AudioMixer"))
        {
            string mixerPath = AssetDatabase.GUIDToAssetPath(mixerGuid);
            AudioMixerGroup[] groups = AssetDatabase.LoadAllAssetsAtPath(mixerPath)
                .OfType<AudioMixerGroup>()
                .ToArray();

            foreach (AudioMixerGroup group in groups)
            {
                if (string.Equals(group.name, ArenaMixerGroupName, StringComparison.OrdinalIgnoreCase))
                    return group;
            }

            // Remembered as the fallback, but only used once no group by that name
            // turned up anywhere, because a named group is the more precise match.
            if (groupsOfArenaMixer == null && groups.Length > 0 &&
                string.Equals(
                    System.IO.Path.GetFileNameWithoutExtension(mixerPath),
                    ArenaMixerGroupName,
                    StringComparison.OrdinalIgnoreCase))
            {
                groupsOfArenaMixer = groups.ToList();
            }
        }

        return groupsOfArenaMixer?.FirstOrDefault();
    }

    private static bool ConfigureFootstepDust(
        GameObject _root,
        Rigidbody _rigidbody,
        PlayerMovementHandler _movement,
        PlayerHealth _health)
    {
        PlayerFootstepDust footstepDust = _root.GetComponent<PlayerFootstepDust>();
        bool prefabChanged = false;

        if (footstepDust == null)
        {
            footstepDust = _root.AddComponent<PlayerFootstepDust>();
            prefabChanged = true;
        }

        SerializedObject serializedFootstepDust = new(footstepDust);
        prefabChanged |= SetObjectReference(serializedFootstepDust.FindProperty("m_rigidbody"), _rigidbody);
        prefabChanged |= SetObjectReference(serializedFootstepDust.FindProperty("m_movement"), _movement);
        prefabChanged |= SetObjectReference(serializedFootstepDust.FindProperty("m_health"), _health);

        if (serializedFootstepDust.hasModifiedProperties)
            serializedFootstepDust.ApplyModifiedPropertiesWithoutUndo();

        return prefabChanged;
    }

    private static bool ConfigureBlockFeedback(GameObject _root, PlayerHealth _health, SpriteRenderer _spriteRenderer)
    {
        PlayerBlockFeedback blockFeedback = _root.GetComponent<PlayerBlockFeedback>();
        bool prefabChanged = false;

        if (blockFeedback == null)
        {
            blockFeedback = _root.AddComponent<PlayerBlockFeedback>();
            prefabChanged = true;
        }

        SerializedObject serializedBlockFeedback = new(blockFeedback);
        prefabChanged |= SetObjectReference(serializedBlockFeedback.FindProperty("m_playerHealth"), _health);
        prefabChanged |= SetObjectReference(serializedBlockFeedback.FindProperty("m_playerSprite"), _spriteRenderer);

        if (serializedBlockFeedback.hasModifiedProperties)
            serializedBlockFeedback.ApplyModifiedPropertiesWithoutUndo();

        return prefabChanged;
    }

    private static AudioClip LoadCombatClip(string _clipPath)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(_clipPath);

        if (clip == null)
            throw new InvalidOperationException($"Player combat audio clip is missing: {_clipPath}");

        return clip;
    }

    private static string GetSpriteName(SheetDefinition _sheet, int _index) =>
        $"Player_{_sheet.StateName}_{_index:00}";

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
            throw new InvalidOperationException("A required serialized animation reference no longer exists.");

        if (_property.objectReferenceValue == _value)
            return false;

        _property.objectReferenceValue = _value;
        return true;
    }
}
