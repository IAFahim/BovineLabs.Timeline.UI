using BovineLabs.Core.Asset;
using System;
using System.Collections.Generic;
using TMPro;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UIShowcase.Runtime;
using BovineLabs.Nerve.ObjectManagement;
using TargetsAuthoring = BovineLabs.Reaction.Authoring.Core.TargetsAuthoring;
using TargetSlot = BovineLabs.Reaction.Data.Core.Target;
using StatAuthoring = BovineLabs.Essence.Authoring.StatAuthoring;
using StatModifierAuthoring = BovineLabs.Essence.Authoring.StatModifierAuthoring;
using StatSchemaObject = BovineLabs.Essence.Authoring.StatSchemaObject;
using StatAuthoringType = BovineLabs.Essence.Authoring.StatAuthoringType;
using IntrinsicSchemaObject = BovineLabs.Essence.Authoring.IntrinsicSchemaObject;
using IntrinsicDefault = BovineLabs.Essence.Authoring.IntrinsicDefault;
using ConditionEventObject = BovineLabs.Reaction.Authoring.Conditions.ConditionEventObject;
using LifeCycleAuthoring = BovineLabs.Nerve.Authoring.LifeCycle.LifeCycleAuthoring;
using TimelineBeginAuthoring = BovineLabs.Timeline.Core.Authoring.TimelineBeginAuthoring;
using TimelineBeginMode = BovineLabs.Timeline.Core.Authoring.TimelineBeginMode;
using InputConsumerAuthoring = BovineLabs.Timeline.PlayerInputs.Authoring.InputConsumerAuthoring;
using PositionTrack = BovineLabs.Timeline.Transform.Authoring.TransformPositionTrack;
using PositionClip = BovineLabs.Timeline.Transform.Authoring.PositionClip;
using PositionType = BovineLabs.Timeline.Transform.Authoring.PositionType;
using HealthSchemaObject = BovineLabs.Timeline.UI.Authoring.HealthSchemaObject;
using UxmlViewTrack = BovineLabs.Timeline.UI.Authoring.UxmlViewTrack;
using UxmlViewClip = BovineLabs.Timeline.UI.Authoring.UxmlViewClip;
using UxmlAttachmentMode = BovineLabs.Timeline.UI.Data.UxmlAttachmentMode;
using UITextRevealTrack = BovineLabs.Timeline.UI.Authoring.UITextRevealTrack;
using UITextRevealClip = BovineLabs.Timeline.UI.Authoring.UITextRevealClip;
using UITextRevealMode = BovineLabs.Timeline.UI.Data.UITextRevealMode;
using UssClassTrack = BovineLabs.Timeline.UI.Authoring.UssClassTrack;
using UssClassClip = BovineLabs.Timeline.UI.Authoring.UssClassClip;
using NumberTrack = BovineLabs.Timeline.UI.Authoring.NumberTrack;
using NumberClip = BovineLabs.Timeline.UI.Authoring.NumberClip;
using DataDisplayTrack = BovineLabs.Timeline.UI.Authoring.DataDisplayTrack;
using DataDisplayClip = BovineLabs.Timeline.UI.Authoring.DataDisplayClip;
using EssenceUITrack = BovineLabs.Timeline.UI.Authoring.EssenceUITrack;
using EssenceUIClip = BovineLabs.Timeline.UI.Authoring.EssenceUIClip;
using EventUIConfig = BovineLabs.Timeline.UI.Authoring.EventUIConfig;
using UISourceAuthoring = BovineLabs.Timeline.UI.Authoring.UISourceAuthoring;
using UISourceMode = BovineLabs.Timeline.UI.Authoring.UISourceMode;

public static class UIShowcaseBuilder
{
    private const string SampleFolder = "Assets/Samples/UIShowcase";
    private const string TimelineFolder = SampleFolder + "/Timelines";
    private const string ParentPath = SampleFolder + "/UIShowcase.unity";
    private const string SubPath = SampleFolder + "/UIShowcase_Sub.unity";

    private const string RequiredInSubScenePath = "Assets/Prefabs/Required In Subscene.prefab";
    private const string HealthFolder = "Assets/Settings/Schemas/Health";
    private const string MaxHealthPath = "Assets/Settings/Schemas/Stats/Max Health.asset";
    private const string MovementSpeedPath = "Assets/Settings/Schemas/Stats/MovementSpeed.asset";
    private const string CurrentHealthPath = "Assets/Settings/Schemas/Intrinsics/CurrentHealth.asset";
    private const string GoldenOrbsPath = "Assets/Settings/Schemas/Intrinsics/GoldenOrbs.asset";
    private const string EventGainedPath = "Assets/Settings/Schemas/Events/OnArmorStackGained.asset";

    // package TrackColor per type
    private static readonly Color UxmlColor = new Color(0.20f, 0.90f, 0.50f);
    private static readonly Color RevealColor = new Color(0.90f, 0.30f, 0.55f);
    private static readonly Color UssColor = new Color(0.85f, 0.55f, 0.20f);
    private static readonly Color NumberColor = new Color(0.95f, 0.95f, 0.95f);
    private static readonly Color DataColor = new Color(0.20f, 0.70f, 0.90f);
    private static readonly Color EssenceColor = new Color(0.85f, 0.30f, 0.85f);

    private static readonly Color ActorColor = new Color(0.85f, 0.85f, 0.90f);
    private static readonly Color HostColor = new Color(0.30f, 0.65f, 0.85f);
    private static readonly Color PlayerColor = new Color(0.75f, 0.30f, 0.80f);
    private static readonly Color PadColor = new Color(0.22f, 0.24f, 0.29f);
    private static readonly Color BannerColor = new Color(0.06f, 0.08f, 0.12f);

    private const float UxmlX = -35f;
    private const float RevealX = -21f;
    private const float UssX = -7f;
    private const float NumberX = 7f;
    private const float DataX = 21f;
    private const float EssenceX = 35f;
    private const float RowStep = 8.0f;
    private const float ActorY = 1.0f;

    private static readonly Vector3 CameraPos = new Vector3(0f, 24f, -48f);

    private static Scene activeSub;
    private static StatSchemaObject maxHealth;
    private static StatSchemaObject movementSpeed;
    private static IntrinsicSchemaObject currentHealth;
    private static IntrinsicSchemaObject goldenOrbs;
    private static ConditionEventObject eventGained;
    private static HealthSchemaObject healthA;
    private static HealthSchemaObject healthB;

    private sealed class TrackBind
    {
        public string TrackName;
        public string BindActorName;
        public bool BindTransform;
    }

    private sealed class CellWire
    {
        public string DirectorName;
        public string TimelinePath;
        public List<TrackBind> Binds = new List<TrackBind>();
    }

    private static readonly List<CellWire> Wires = new List<CellWire>();

    private sealed class CaptionData
    {
        public string Title;
        public string Usage;
        public Vector3 CellPos;
        public Color Color;
    }

    private static readonly List<CaptionData> Captions = new List<CaptionData>();

    [MenuItem("Showcase/Build UI")]
    public static void Build()
    {
        Wires.Clear();
        Captions.Clear();

        EnsureFolders();
        if (!EnsureHealthSchemas())
            return;

        maxHealth = AssetDatabase.LoadAssetAtPath<StatSchemaObject>(MaxHealthPath);
        movementSpeed = AssetDatabase.LoadAssetAtPath<StatSchemaObject>(MovementSpeedPath);
        currentHealth = AssetDatabase.LoadAssetAtPath<IntrinsicSchemaObject>(CurrentHealthPath);
        goldenOrbs = AssetDatabase.LoadAssetAtPath<IntrinsicSchemaObject>(GoldenOrbsPath);
        eventGained = AssetDatabase.LoadAssetAtPath<ConditionEventObject>(EventGainedPath);

        if (maxHealth == null || movementSpeed == null || currentHealth == null || goldenOrbs == null || eventGained == null)
        {
            Debug.LogError("UIShowcase: schema asset(s) missing. maxHealth=" + (maxHealth != null) +
                           " movementSpeed=" + (movementSpeed != null) + " currentHealth=" + (currentHealth != null) +
                           " goldenOrbs=" + (goldenOrbs != null) + " eventGained=" + (eventGained != null));
            return;
        }

        ResetSceneAssets();

        var parent = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(parent, ParentPath);
        var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(sub);
        activeSub = sub;

        BuildRequiredInSubScene();
        BuildPads();
        BuildUxmlColumn();
        BuildRevealColumn();
        BuildUssColumn();
        BuildNumberColumn();
        BuildDataColumn();
        BuildEssenceColumn();

        EditorSceneManager.SaveScene(sub, SubPath);
        EditorSceneManager.SetActiveScene(parent);
        EditorSceneManager.CloseScene(sub, true);

        sub = EditorSceneManager.OpenScene(SubPath, OpenSceneMode.Additive);
        EditorSceneManager.SetActiveScene(sub);
        activeSub = sub;

        foreach (var w in Wires)
            WireCell(w);

        EditorSceneManager.MarkSceneDirty(sub);
        EditorSceneManager.SaveScene(sub);

        EditorSceneManager.SetActiveScene(parent);
        BuildParent();
        EditorSceneManager.SaveScene(parent);

        EditorSceneManager.CloseScene(sub, true);
        EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);

        Debug.Log("UIShowcase: built grid at " + ParentPath + " directors=" + Wires.Count +
                  " | healthA.Id=" + healthA.Id + " healthB.Id=" + healthB.Id +
                  " | maxHealth.Key=" + maxHealth.Key + " movementSpeed.Key=" + movementSpeed.Key +
                  " currentHealth.Key=" + currentHealth.Key + " goldenOrbs.Key=" + goldenOrbs.Key +
                  " eventGained.Key=" + eventGained.Key);
    }

    // ============================================================
    //  Health schema assets (DataDisplay rows) — IUID auto-assigned.
    // ============================================================

    private static bool EnsureHealthSchemas()
    {
        healthA = AssetDatabase.LoadAssetAtPath<HealthSchemaObject>(HealthFolder + "/Health_HP.asset");
        healthB = AssetDatabase.LoadAssetAtPath<HealthSchemaObject>(HealthFolder + "/Health_Shield.asset");

        var created = false;
        if (healthA == null)
        {
            healthA = ScriptableObject.CreateInstance<HealthSchemaObject>();
            AssetDatabase.CreateAsset(healthA, HealthFolder + "/Health_HP.asset");
            created = true;
        }

        if (healthB == null)
        {
            healthB = ScriptableObject.CreateInstance<HealthSchemaObject>();
            AssetDatabase.CreateAsset(healthB, HealthFolder + "/Health_Shield.asset");
            created = true;
        }

        if (created)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        healthA = AssetDatabase.LoadAssetAtPath<HealthSchemaObject>(HealthFolder + "/Health_HP.asset");
        healthB = AssetDatabase.LoadAssetAtPath<HealthSchemaObject>(HealthFolder + "/Health_Shield.asset");

        if (healthA == null || healthB == null)
        {
            Debug.LogError("UIShowcase: failed to create HealthSchemaObject assets.");
            return false;
        }

        if (healthA.Id == 0 || healthB.Id == 0 || healthA.Id == healthB.Id)
        {
            Debug.LogWarning("UIShowcase: HealthSchemaObject IDs not yet assigned by the AutoRef postprocessor " +
                             "(healthA.Id=" + healthA.Id + " healthB.Id=" + healthB.Id + "). " +
                             "Created the assets; RE-RUN 'Showcase/Build UI' so the delayed importer can assign IDs.");
            return false;
        }

        return true;
    }

    // ============================================================
    //  COLUMN 1 — UXML View (Family A, reversible VisualElement).
    // ============================================================

    private static void BuildUxmlColumn()
    {
        BuildUxmlCell(0, "HudBanner", "", UxmlAttachmentMode.AppendToRoot, "UXML AppendToRoot",
            "UxmlViewClip UxmlKey=\"HudBanner\" TargetId=\"\" Mode=AppendToRoot. Bakes UxmlViewData{UxmlKey,TargetId,Mode}. At runtime UxmlViewTrackSystem (ReversibleEffectSystem) instantiates the UXML via IUXMLService and appends it to AnchorApp.Current.RootVisualElement for the clip, removing it on exit. STRUCTURAL: needs a live Anchor app + registered IUXMLService resolving the key.");
        BuildUxmlCell(1, "Tooltip", "HudPanel", UxmlAttachmentMode.AppendToElement, "UXML AppendToElement",
            "UxmlViewClip UxmlKey=\"Tooltip\" TargetId=\"HudPanel\" Mode=AppendToElement. The other attachment enum: the instantiated tree is appended UNDER the element named \"HudPanel\" instead of the root. STRUCTURAL: same Anchor/IUXMLService prerequisites; the TargetId element must exist in the live tree.");
    }

    private static void BuildUxmlCell(int row, string key, string targetId, UxmlAttachmentMode mode,
        string title, string usage)
    {
        var z = row * RowStep;
        var cell = "Uxml" + row;
        MakeMarker(cell + "_Anchor", new Vector3(UxmlX, ActorY, z), UxmlColor);

        var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
        var t = timeline.CreateTrack<UxmlViewTrack>(null, "UxmlView");
        var c = AddClip<UxmlViewClip>(t, 1.0, 3.0, key);
        var a = (UxmlViewClip)c.asset;
        a.UxmlKey = key;
        a.TargetId = targetId;
        a.Mode = mode;
        Dirty(a);

        FinishCell(timeline, cell, UxmlX, z, title, usage, UxmlColor, new List<TrackBind>());
    }

    // ============================================================
    //  COLUMN 2 — Text Reveal (Family A, the only animated clip).
    // ============================================================

    private static void BuildRevealColumn()
    {
        BuildRevealCell(0, "DialogueLabel", "Welcome to the arena.", UITextRevealMode.Typewriter,
            "Text Reveal (Typewriter)",
            "UITextRevealClip TargetId=\"DialogueLabel\" Text=\"Welcome to the arena.\" Mode=Typewriter (duration=reveal time). The ONLY animated clip: clipCaps ClipIn|SpeedMultiplier. Advance() reveals Substring(0, round(len*percent)) each tick, restoring the original text on exit. STRUCTURAL: needs a live TextElement named \"DialogueLabel\" under the Anchor root.");
        BuildRevealCell(1, "DialogueLabel", "Boss incoming!", UITextRevealMode.Instant,
            "Text Reveal (Instant)",
            "UITextRevealClip Mode=Instant: writes the full string immediately (no per-char animation) and reverts on exit. The second reveal enum. STRUCTURAL: same live-TextElement prerequisite.");
    }

    private static void BuildRevealCell(int row, string targetId, string text, UITextRevealMode mode,
        string title, string usage)
    {
        var z = row * RowStep;
        var cell = "Reveal" + row;
        MakeMarker(cell + "_Anchor", new Vector3(RevealX, ActorY, z), RevealColor);

        var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
        var t = timeline.CreateTrack<UITextRevealTrack>(null, "TextReveal");
        var c = AddClip<UITextRevealClip>(t, 0.0, 2.5, mode.ToString());
        var a = (UITextRevealClip)c.asset;
        a.TargetId = targetId;
        a.Text = text;
        a.Mode = mode;
        Dirty(a);

        FinishCell(timeline, cell, RevealX, z, title, usage, RevealColor, new List<TrackBind>());
    }

    // ============================================================
    //  COLUMN 3 — USS Class (Family A, reversible class toggle).
    // ============================================================

    private static void BuildUssColumn()
    {
        var z = 0 * RowStep;
        var cell = "Uss0";
        MakeMarker(cell + "_Anchor", new Vector3(UssX, ActorY, z), UssColor);

        var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
        var t = timeline.CreateTrack<UssClassTrack>(null, "UssClass");
        var c = AddClip<UssClassClip>(t, 1.0, 3.0, "highlighted");
        var a = (UssClassClip)c.asset;
        a.TargetId = "HealthBar";
        a.ClassName = "highlighted";
        Dirty(a);

        FinishCell(timeline, cell, UssX, z, "USS Class toggle",
            "UssClassClip TargetId=\"HealthBar\" ClassName=\"highlighted\". Bakes UssClassData{TargetId,ClassName}. UssClassTrackSystem (ReversibleEffectSystem) adds the USS class to the named live element for the clip and removes it on exit. STRUCTURAL: needs the live element + a stylesheet defining .highlighted under the Anchor root.",
            UssColor, new List<TrackBind>());
    }

    // ============================================================
    //  COLUMN 4 — Number (Family B HUD ViewModel, MAX-fold rule).
    // ============================================================

    private static void BuildNumberColumn()
    {
        // Row 0 — single clip Number=3.
        {
            var z = 0 * RowStep;
            var cell = "Number0";
            MakeMarker(cell + "_Anchor", new Vector3(NumberX, ActorY, z), NumberColor);
            var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
            var t = timeline.CreateTrack<NumberTrack>(null, "Number");
            AddNumberClip(t, 1.0, 6.0, 3);
            FinishCell(timeline, cell, NumberX, z, "Number = 3",
                "NumberClip Number=3. Bakes NumberComponent{Value=3}. NumberTrackSystem folds active clips into NumberViewModel.Number while a clip is active (IsVisible). NUMERIC: NumberComponent + TimelineActive/ClipActive observable in ECS; the HUD pixel needs a panel binding NumberViewModel.",
                NumberColor, new List<TrackBind>());
        }

        // Row 1 — two overlapping clips: MAX(2,5)=5, NOT 7.
        {
            var z = 1 * RowStep;
            var cell = "Number1";
            MakeMarker(cell + "_Anchor", new Vector3(NumberX, ActorY, z), NumberColor);
            var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
            var t = timeline.CreateTrack<NumberTrack>(null, "Number");
            AddNumberClip(t, 0.0, 5.0, 2);
            AddNumberClip(t, 2.0, 5.0, 5);
            FinishCell(timeline, cell, NumberX, z, "Number MAX(2,5)=5",
                "Two overlapping NumberClips (Number=2 and Number=5). The fold is math.max, NOT a sum: the overlap window resolves to 5 (never 7). Demonstrates the documented MAX-fold rule. NUMERIC.",
                NumberColor, new List<TrackBind>());
        }
    }

    private static void AddNumberClip(TrackAsset t, double start, double dur, int number)
    {
        var c = AddClip<NumberClip>(t, start, dur, "n=" + number);
        var a = (NumberClip)c.asset;
        a.Number = number;
        Dirty(a);
    }

    // ============================================================
    //  COLUMN 5 — Data Display (Family B; binds Transform; IdValue).
    // ============================================================

    private static void BuildDataColumn()
    {
        var z = 0 * RowStep;
        var cell = "Data0";

        // DataHost carries the IdValue buffer the system reads through the Transform binding.
        var host = MakeMarker(cell + "_Host", new Vector3(DataX, ActorY, z), HostColor);
        host.AddComponent<LifeCycleAuthoring>();
        var idv = host.AddComponent<IdValueAuthoring>();
        idv.Entries = new[]
        {
            new IdValueAuthoring.Entry { Id = healthA.Id, Value = 87f },
            new IdValueAuthoring.Entry { Id = healthB.Id, Value = 42f },
        };

        var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
        var t = timeline.CreateTrack<DataDisplayTrack>(null, "DataDisplay");
        var c = AddClip<DataDisplayClip>(t, 1.0, 6.0, "Health rows");
        var a = (DataDisplayClip)c.asset;
        a.Health = new[] { healthA, healthB };
        Dirty(a);

        FinishCell(timeline, cell, DataX, z, "Data Display (Transform)",
            "DataDisplayClip Health[]={Health_HP,Health_Shield}; track binds the cyan DataHost's Transform. Each schema bakes ClipDataId{Id,Label=name}. The bound entity carries an IdValue buffer {HP=87, Shield=42}; DataDisplayTrackSystem matches ids -> rows. NUMERIC: ClipDataId + IdValue buffers observable; rows render only in a panel binding DataDisplayViewModel.",
            DataColor, new List<TrackBind> { new TrackBind { TrackName = "DataDisplay", BindActorName = cell + "_Host", BindTransform = true } });
    }

    // ============================================================
    //  COLUMN 6 — Essence UI (Family B; binds StatAuthoring; Player).
    // ============================================================

    private static void BuildEssenceColumn()
    {
        var z = 0 * RowStep;
        var cell = "Essence0";

        // Player: StatAuthoring (bind target) + Controllable PlayerId=0 so Mode=Player,Player=0 resolves.
        var player = MakeMarker(cell + "_Player", new Vector3(EssenceX, ActorY, z), PlayerColor);
        player.AddComponent<LifeCycleAuthoring>();

        var stats = player.AddComponent<StatAuthoring>();
        stats.AddStats = true;
        stats.StatsCanBeModified = true;
        stats.AddIntrinsics = true;
        stats.StatDefaults = new[]
        {
            new StatModifierAuthoring { Stat = maxHealth, ModifyType = StatAuthoringType.Added, Value = 120f },
            new StatModifierAuthoring { Stat = movementSpeed, ModifyType = StatAuthoringType.Added, Value = 8f },
        };
        stats.IntrinsicDefaults = new[]
        {
            new IntrinsicDefault { Intrinsic = currentHealth, Value = 95 },
            new IntrinsicDefault { Intrinsic = goldenOrbs, Value = 5 },
        };

        var targets = player.AddComponent<TargetsAuthoring>();
        targets.Owner = player;
        targets.Source = player;
        targets.Custom = player;
        targets.Target = player;

        var input = player.AddComponent<InputConsumerAuthoring>();
        input.PlayerId = 0;
        input.Controllable = true;

        var timeline = NewTimeline(TimelineFolder + "/" + cell + ".playable");
        var t = timeline.CreateTrack<EssenceUITrack>(null, "EssenceUI");
        var c = AddClip<EssenceUIClip>(t, 1.0, 6.0, "My HUD");
        var a = (EssenceUIClip)c.asset;
        a.Source = new UISourceAuthoring
        {
            Mode = UISourceMode.Player,
            Player = 0,
            Route = TargetSlot.Self,
            link = null,
        };
        a.Stats = new[] { maxHealth, movementSpeed };
        a.Intrinsics = new[] { currentHealth, goldenOrbs };
        a.Events = new[] { new EventUIConfig { Event = eventGained, DisplayDuration = 2.0f } };
        Dirty(a);

        FinishCell(timeline, cell, EssenceX, z, "Essence UI (StatAuthoring)",
            "EssenceUIClip Source{Mode=Player,Player=0,Route=Self} = \"show MY data\"; track binds the magenta Player's StatAuthoring. Stats[]={MaxHealth,MovementSpeed} Intrinsics[]={CurrentHealth,GoldenOrbs} Events[]={OnArmorStackGained@2s}. ControllableRegistry resolves Player 0 (InputConsumer Controllable, PlayerId=0). EssenceUITrackSystem reads post-mutation Essence buffers into EssenceUIViewModel rows. NUMERIC: baked ClipStat/ClipIntrinsic/ClipEvent + resolved player Stat/Intrinsic buffers observable; rows render only in a panel binding EssenceUIViewModel.",
            EssenceColor, new List<TrackBind> { new TrackBind { TrackName = "EssenceUI", BindActorName = cell + "_Player", BindTransform = false } });
    }

    // ============================================================
    //  actor builders
    // ============================================================

    private static GameObject MakeMarker(string name, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, color);
        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    // ============================================================
    //  wire / caption plumbing
    // ============================================================

    private static void FinishCell(TimelineAsset timeline, string cell, float x, float z,
        string label, string usage, Color color, List<TrackBind> binds)
    {
        FixDuration(timeline);
        Dirty(timeline);
        foreach (var tr in timeline.GetOutputTracks()) Dirty(tr);
        AssetDatabase.SaveAssets();

        var dirName = cell + "_Director";
        MakeDirector(dirName);
        Wires.Add(new CellWire
        {
            DirectorName = dirName,
            TimelinePath = AssetDatabase.GetAssetPath(timeline),
            Binds = binds,
        });
        Captions.Add(new CaptionData { Title = label, Usage = usage, CellPos = new Vector3(x, 4.4f, z), Color = color });
    }

    private static void WireCell(CellWire w)
    {
        var director = GameObject.Find(w.DirectorName).GetComponent<PlayableDirector>();
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(w.TimelinePath);
        director.playableAsset = timeline;

        foreach (var bind in w.Binds)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track.name != bind.TrackName) continue;
                var actor = GameObject.Find(bind.BindActorName);
                if (bind.BindTransform)
                    director.SetGenericBinding(track, actor.transform);
                else
                    director.SetGenericBinding(track, actor.GetComponent<StatAuthoring>());
            }
        }

        EditorUtility.SetDirty(director);
    }

    private static PlayableDirector MakeDirector(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, activeSub);
        var director = go.AddComponent<PlayableDirector>();
        director.playOnAwake = true;
        director.extrapolationMode = DirectorWrapMode.Loop;
        var begin = go.AddComponent<TimelineBeginAuthoring>();
        begin.Mode = TimelineBeginMode.OnLoad;
        begin.DelaySeconds = 0f;
        return director;
    }

    private static TimelineAsset NewTimeline(string path)
    {
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, path);
        return timeline;
    }

    private static TimelineClip AddClip<T>(TrackAsset track, double start, double duration, string name) where T : PlayableAsset
    {
        var clip = track.CreateClip<T>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = name;
        return clip;
    }

    private static void FixDuration(TimelineAsset timeline)
    {
        var end = 0.0;
        foreach (var track in timeline.GetOutputTracks())
            foreach (var clip in track.GetClips())
            {
                var clipEnd = clip.start + clip.duration;
                if (clipEnd > end) end = clipEnd;
            }

        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = end;
    }

    // ============================================================
    //  primitives / parent scene
    // ============================================================

    private static GameObject MakePad(string name, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, PadColor);
        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    private static void BuildRequiredInSubScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RequiredInSubScenePath);
        if (prefab == null)
        {
            Debug.LogWarning("UIShowcase: '" + RequiredInSubScenePath + "' missing; runtime singletons may be absent.");
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "Required In Subscene";
        SceneManager.MoveGameObjectToScene(go, activeSub);
    }

    private static void BuildPads()
    {
        float[] xs = { UxmlX, RevealX, UssX, NumberX, DataX, EssenceX };
        string[] names = { "Uxml", "Reveal", "Uss", "Number", "Data", "Essence" };
        var zCenter = RowStep * 0.5f;
        for (var i = 0; i < xs.Length; i++)
            MakePad(names[i] + "_Pad", new Vector3(xs[i], 0.05f, zCenter), new Vector3(11.0f, 0.12f, RowStep * 2f + 4f));
    }

    private static Material MakeMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { name = name + "_Mat" };
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        return mat;
    }

    private static void BuildParent()
    {
        FrameCamera();
        RenderSettings.fog = false;

        MakeBanner("Title_Banner", new Vector3(0f, 21.2f, 0f), new Vector3(78f, 3.6f, 0.1f));
        MakeWorldLabel("Title", "UI TIMELINE GRID — BovineLabs.Timeline.UI", new Vector3(0f, 21.6f, -0.4f), 78f, Color.white, 5.0f, TextAlignmentOptions.Center);
        MakeWorldLabel("Subtitle", "6 track families: 3 reversible VisualElement effects (Family A) + 3 ViewModel HUD panels (Family B)   ·   com.bovinelabs.timeline.ui", new Vector3(0f, 20.1f, -0.4f), 78f, new Color(0.85f, 0.9f, 1f), 1.9f, TextAlignmentOptions.Center);

        MakeColumnHeader("Uxml_Header", "UXML VIEW", UxmlX, UxmlColor);
        MakeColumnHeader("Reveal_Header", "TEXT REVEAL", RevealX, RevealColor);
        MakeColumnHeader("Uss_Header", "USS CLASS", UssX, UssColor);
        MakeColumnHeader("Number_Header", "NUMBER (MAX)", NumberX, NumberColor);
        MakeColumnHeader("Data_Header", "DATA DISPLAY", DataX, DataColor);
        MakeColumnHeader("Essence_Header", "ESSENCE UI", EssenceX, EssenceColor);

        foreach (var cap in Captions)
            MakeCaption(cap.Title, cap.Usage, cap.CellPos, cap.Color);

        MakeBanner("Usage_Banner", new Vector3(0f, 0.7f, -10.5f), new Vector3(82f, 2.6f, 0.1f));
        MakeWorldLabel("Usage",
            "FAMILY A (UXML / Text Reveal / USS Class) directly mutates the live VisualElement tree via ReversibleEffectSystem; NO track binding (addressed by string id), reverts on clip exit — STRUCTURAL here (needs a live Anchor app RootVisualElement + IUXMLService + named elements, none of which ship in an isolated showcase). FAMILY B (Number / Data Display / Essence UI) folds active clips into a ViewModel binding; Number/Essence have NO binding, Data binds Transform, Essence binds StatAuthoring — NUMERIC here (baked components + folded ECS data observable; HUD pixels need a project panel binding the ViewModel). Every director: TimelineBegin OnLoad, FixedLength + Loop.",
            new Vector3(0f, 0.7f, -10.8f), 80f, new Color(0.96f, 0.97f, 1f), 1.4f, TextAlignmentOptions.Center);

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubPath);
        if (sceneAsset == null)
        {
            Debug.LogError("UIShowcase: sub-scene asset missing at " + SubPath);
            return;
        }

        var subSceneGo = new GameObject("Showcase SubScene");
        var subScene = subSceneGo.AddComponent<SubScene>();
        subScene.SceneAsset = sceneAsset;
        subScene.AutoLoadScene = true;
        EditorUtility.SetDirty(subScene);
    }

    private static void MakeColumnHeader(string name, string text, float x, Color color)
    {
        var pos = new Vector3(x, 6.2f, -6.0f);
        MakeBanner(name + "_Banner", pos + new Vector3(0f, 0f, 0.08f), new Vector3(10.6f, 1.5f, 0.1f));
        MakeWorldLabel(name, "<b>" + text + "</b>", pos, 10.4f, color, 2.6f, TextAlignmentOptions.Center);
    }

    private static float CaptionY(float z)
    {
        return 5.8f + z * 0.10f;
    }

    private static void MakeCaption(string title, string usage, Vector3 cellPos, Color color)
    {
        var z = cellPos.z;
        var y = CaptionY(z);
        MakeBanner("CapBanner_" + title + "_" + z, new Vector3(cellPos.x, y, z + 0.06f), new Vector3(10.2f, 2.8f, 0.05f));
        MakeWorldLabel("Cap_" + title + "_" + z, "<b>" + title + "</b>", new Vector3(cellPos.x, y + 0.8f, z), 10.0f, color, 2.0f, TextAlignmentOptions.Center);
        MakeWorldLabel("Use_" + title + "_" + z, usage, new Vector3(cellPos.x, y - 0.5f, z), 10.0f, new Color(0.95f, 0.96f, 1f), 0.95f, TextAlignmentOptions.Center);
    }

    private static void FrameCamera()
    {
        var required = GameObject.Find("Required In Scene");
        if (required == null) return;
        var camTransform = required.transform.Find("Main Camera");
        if (camTransform == null) return;
        camTransform.position = CameraPos;
        camTransform.rotation = Quaternion.Euler(22f, 0f, 0f);
        var cam = camTransform.GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = 64f;
            cam.farClipPlane = 500f;
            EditorUtility.SetDirty(cam);
        }

        EditorUtility.SetDirty(camTransform);
    }

    private static void MakeBanner(string name, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(pos - CameraPos, Vector3.up);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, BannerColor);
    }

    private static void MakeWorldLabel(string name, string text, Vector3 pos, float width, Color color, float fontSize, TextAlignmentOptions alignment)
    {
        var holder = new GameObject(name);
        holder.transform.position = pos;
        holder.transform.rotation = Quaternion.LookRotation(pos - CameraPos, Vector3.up);

        var go = new GameObject("Text");
        go.transform.SetParent(holder.transform, false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.rectTransform.sizeDelta = new Vector2(width, 4f);
        tmp.rectTransform.localPosition = Vector3.zero;
        tmp.fontStyle = FontStyles.Bold;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Samples"))
            AssetDatabase.CreateFolder("Assets", "Samples");
        if (!AssetDatabase.IsValidFolder(SampleFolder))
            AssetDatabase.CreateFolder("Assets/Samples", "UIShowcase");
        if (!AssetDatabase.IsValidFolder(TimelineFolder))
            AssetDatabase.CreateFolder(SampleFolder, "Timelines");
        if (!AssetDatabase.IsValidFolder("Assets/Settings/Schemas/Health"))
            AssetDatabase.CreateFolder("Assets/Settings/Schemas", "Health");
    }

    private static void ResetSceneAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TimelineFolder) != null)
            foreach (var guid in AssetDatabase.FindAssets("t:TimelineAsset", new[] { TimelineFolder }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

        foreach (var p in new[] { ParentPath, SubPath })
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p) != null)
                AssetDatabase.DeleteAsset(p);
    }

    private static void Dirty(params UnityEngine.Object[] objects)
    {
        foreach (var o in objects)
            EditorUtility.SetDirty(o);
    }
}
