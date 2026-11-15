using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class SurvivalGameSetup
{
    const string Destination = "Assets/Scenes/UnderdogSurvival.unity";
    const string AnimationFolder = "Assets/Assets_Externos/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium";
    static string output;
    static readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
    static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();

    [MenuItem("Tools/Underdog/Prepare Survival Game")]
    public static void Prepare()
    {
        Scene source = SceneManager.GetActiveScene(), copy = default;
        string temporary = null;
        NavMeshDataInstance navigation = default;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !Application.unityVersion.StartsWith("2022.3."))
                throw new InvalidOperationException("Execute fora do Play Mode na Unity 2022.3.62f3.");
            if (SceneManager.sceneCount != 1 || !source.IsValid() || source.isDirty || source.path == Destination)
                throw new InvalidOperationException("Abra somente a ArenaTeste aprovada e salve suas alteracoes antes de preparar a partida.");
            if (Objects<WaveManager>(source).Any()) throw new InvalidOperationException("Use a arena original, nao uma partida ja preparada.");
            var playerSource = Objects<PlayerCombat>(source).Single(c => c.gameObject.activeInHierarchy);
            var enemySource = Objects<EnemyAI>(source).FirstOrDefault(e => e.gameObject.activeInHierarchy);
            if (enemySource == null) throw new InvalidOperationException("A origem precisa do inimigo funcional para preservar suas configuracoes.");
            var environment = source.GetRootGameObjects().SingleOrDefault(g => g.name == "Environment" && g.activeInHierarchy);
            var spawns = source.GetRootGameObjects().SingleOrDefault(g => g.name == "SpawnPoints" && g.activeInHierarchy);
            if (environment == null || spawns == null) throw new InvalidOperationException("Environment/SpawnPoints da arena ausentes.");
            if (Objects<CinemachineBrain>(source).Count(b => b.isActiveAndEnabled) != 1)
                throw new InvalidOperationException("A arena precisa de um Brain ativo. A camera aprovada nao sera reconfigurada.");
            LoadClips();
            foreach (string name in new[] { "Idle_A", "Running_A", "Walking_Backwards", "Running_Strafe_Left", "Running_Strafe_Right",
                "Dodge_Forward", "Dodge_Backward", "Dodge_Left", "Dodge_Right", "Hit_A", "Death_A",
                "Ranged_Magic_Raise", "Ranged_Magic_Summon", "Ranged_Magic_Spellcasting", "Ranged_Magic_Shoot", "Melee_1H_Attack_Chop" }) RequireClip(name);
            var warrior = Model("Skeleton_Warrior");
            var rogue = Model("Skeleton_Rogue");
            var mage = Model("Skeleton_Mage");
            if (Shader.Find("Universal Render Pipeline/Lit") == null || Shader.Find("Universal Render Pipeline/Particles/Unlit") == null)
                throw new InvalidOperationException("Shaders URP indisponiveis. Resolva os pacotes antes de executar.");
            if (!EditorUtility.DisplayDialog("Preparar sobrevivencia", "Criar uma copia jogavel da arena, assets novos e colocar a partida no inicio dos Build Settings (com backup)? A camera e a origem serao preservadas. Se o destino existir, sera feito backup antes da substituicao.", "Preparar", "Cancelar")) return;
            if (File.Exists(Destination) && !EditorUtility.DisplayDialog("Substituir partida existente?", Destination + " ja existe. Criar backup e substituir?", "Substituir", "Cancelar")) return;
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            output = AssetDatabase.GenerateUniqueAssetPath("Assets/Generated/UnderdogSurvival");
            AssetDatabase.CreateFolder("Assets/Generated", Path.GetFileName(output));
            if (!AssetDatabase.CopyAsset(source.path, output + "/ArenaSourceBackup.unity")) throw new IOException("Falha ao criar backup da origem.");
            File.Copy("ProjectSettings/EditorBuildSettings.asset", output + "/EditorBuildSettings.backup.txt");
            temporary = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Survival_build.unity");
            if (!EditorSceneManager.SaveScene(source, temporary, true)) throw new IOException("Falha ao copiar a arena.");
            copy = EditorSceneManager.OpenScene(temporary, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(copy);
            converted.Clear();
            var player = Objects<PlayerCombat>(copy).Single(c => c.gameObject.activeInHierarchy);
            environment = copy.GetRootGameObjects().Single(g => g.name == "Environment" && g.activeInHierarchy);
            spawns = copy.GetRootGameObjects().Single(g => g.name == "SpawnPoints" && g.activeInHierarchy);
            foreach (var enemy in Objects<EnemyAI>(copy)) enemy.gameObject.SetActive(false);

            // Apenas assets gerados sao editados; importadores e controladores originais ficam intactos.
            var blue = EffectMaterial("ArcaneBlue", new Color(0.2f, 0.65f, 1.5f));
            var purple = EffectMaterial("ArcaneViolet", new Color(0.9f, 0.25f, 1.5f));
            var red = EffectMaterial("EnemyAmber", new Color(1.6f, 0.35f, 0.1f));
            var hit = Effect("Hit", red, 9, 0.25f, 1.5f, false);
            var death = Effect("Death", purple, 24, 0.65f, 2, false);
            var impact = Effect("Impact", blue, 14, 0.4f, 2, false);
            var preparation = Effect("Preparation", blue, 16, 0.32f, 0.15f, false);
            var area = Effect("Explosion", purple, 60, 0.5f, 0.5f, true);
            var burstImpact = Effect("BurstImpact", purple, 12, 0.35f, 2, false);
            var enemyImpact = Effect("EnemyImpact", red, 12, 0.35f, 2, false);
            var primary = ProjectilePrefab("Primary", blue, impact, 25, 18, false);
            var burst = ProjectilePrefab("Burst", purple, burstImpact, 15, 22, false);
            var enemyShot = ProjectilePrefab("EnemyShot", red, enemyImpact, 12, 10, true);
            ConfigurePlayer(player, primary, burst, preparation, area, hit, death);
            player.explosao.preparo = Effect("AreaPreparation", purple, 18, 0.85f, 0.15f, false);
            player.rajada.preparo = Effect("BurstPreparation", purple, 16, 0.25f, 0.15f, false);
            player.GetComponent<PlayerMovement>().efeitoDash = Effect("Dash", blue, 18, 0.25f, 1.5f, false);
            var basic = EnemyPrefab(warrior, "Melee", 175, 10, 2.5f, false, enemySource.arma, enemyShot, hit, death);
            var fast = EnemyPrefab(rogue, "Fast", 60, 8, 5.5f, false, enemySource.arma, enemyShot, hit, death);
            var ranged = EnemyPrefab(mage, "Ranged", 75, 12, 2.6f, true, null, enemyShot, hit, death);
            var walls = environment.transform.Find("Walls");
            if (walls != null)
                foreach (var box in walls.GetComponentsInChildren<BoxCollider>().Where(c => c.enabled))
                {
                    // Limites fisicos altos seguram magias; as paredes visuais baixas continuam sem ocluir a camera.
                    float height = Mathf.Max(box.size.y, 3f / Mathf.Max(0.01f, Mathf.Abs(box.transform.lossyScale.y)));
                    box.size = new Vector3(box.size.x, height, box.size.z);
                    box.center = new Vector3(box.center.x, height * 0.5f, box.center.z);
                }
            var data = BakeNavigation(environment);
            var nav = environment.GetComponent<ArenaNavigation>();
            if (nav == null) nav = environment.AddComponent<ArenaNavigation>();
            nav.dados = data;
            navigation = NavMesh.AddNavMeshData(data);
            var points = spawns.GetComponentsInChildren<Transform>().Where(t => t != spawns.transform && t.name.StartsWith("EnemySpawn_")).ToArray();
            if (points.Length < 3 || points.Length > 5) throw new InvalidOperationException("Esperados entre 3 e 5 EnemySpawn_ na arena.");
            ValidateNavigation(player.transform, points);
            var game = new GameObject("SurvivalGame");
            SceneManager.MoveGameObjectToScene(game, copy);
            var waves = game.AddComponent<WaveManager>();
            waves.jogador = player.GetComponent<PlayerHp>();
            waves.basico = basic;
            waves.rapido = fast;
            waves.distante = ranged;
            waves.spawnPoints = points;
            CreateUI(waves);
            ValidateScene(copy, player, waves);
            if (File.Exists(Destination))
            {
                if (!AssetDatabase.CopyAsset(Destination, output + "/PreviousSurvivalBackup.unity")) throw new IOException("Falha ao criar backup do destino.");
            }
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(copy, Destination)) throw new IOException("Falha ao salvar a partida.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Destination, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != Destination)).ToArray();
            EditorSceneManager.CloseScene(source, true);
            Debug.Log("Sobrevivencia preparada: " + Destination + ". Assets e backups: " + output + ". Entre em Play Mode e pressione Iniciar. Camera preservada. Execute os testes de Play Mode antes de considerar validado.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (copy.IsValid() && copy.isLoaded) EditorSceneManager.CloseScene(copy, true);
            if (source.IsValid() && source.isLoaded) SceneManager.SetActiveScene(source);
            EditorUtility.DisplayDialog("Preparacao interrompida", error.Message + "\nA origem permanece intacta. Assets intermediarios ficam em Generated para inspecao.", "OK");
        }
        finally
        {
            if (navigation.valid) navigation.Remove();
            if (temporary != null) AssetDatabase.DeleteAsset(temporary);
        }
    }

    static T[] Objects<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

    static void LoadClips()
    {
        clips.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationFolder }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<AnimationClip>())
                if (!clip.name.StartsWith("__preview__") && !clips.ContainsKey(clip.name)) clips.Add(clip.name, clip);
    }
    static AnimationClip RequireClip(string name)
    {
        if (!clips.TryGetValue(name, out var clip)) throw new InvalidOperationException("Clipe real nao encontrado: " + name + ". Verifique importacao de " + AnimationFolder);
        return clip;
    }
    static AnimationClip Clip(string realName, string label, bool loop, string evento = null, float fracao = 0.35f)
    {
        var clip = UnityEngine.Object.Instantiate(RequireClip(realName));
        clip.name = label;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        float eventTime = realName == "Ranged_Magic_Raise" ? Mathf.Min(0.32457352f, clip.length * 0.5f) : clip.length * fracao;
        AnimationUtility.SetAnimationEvents(clip, evento == null ? new AnimationEvent[0] : new[] { new AnimationEvent { functionName = evento, time = eventTime } });
        AssetDatabase.CreateAsset(clip, output + "/" + label + ".anim");
        return clip;
    }
    static GameObject Model(string name)
    {
        var paths = AssetDatabase.FindAssets(name + " t:Model", new[] { "Assets/Assets_Externos/KayKit_Skeletons_1.1_FREE" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileNameWithoutExtension(p) == name && p.EndsWith(".fbx")).ToArray();
        if (paths.Length != 1) throw new InvalidOperationException("Modelo ambiguo/ausente: " + name);
        return AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);
    }

    static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion, float speed = 1)
    {
        var state = machine.AddState(name);
        state.motion = motion;
        state.speed = speed;
        state.writeDefaultValues = false;
        return state;
    }
    static void Exit(AnimatorState from, AnimatorState to, float duration = 0.06f)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 1;
        transition.hasFixedDuration = true;
        transition.duration = duration;
    }
    static void Trigger(AnimatorStateMachine machine, AnimatorState to, string parameter)
    {
        var transition = machine.AddAnyStateTransition(to);
        transition.AddCondition(AnimatorConditionMode.If, 0, parameter);
        transition.hasExitTime = false;
        transition.duration = 0.06f;
        transition.hasFixedDuration = true;
        transition.canTransitionToSelf = false;
    }
    static BlendTree Tree(AnimatorController controller, string name, bool directional)
    {
        var tree = new BlendTree { name = name, blendType = directional ? BlendTreeType.SimpleDirectional2D : BlendTreeType.Simple1D,
            blendParameter = directional ? "MoveX" : "Speed", blendParameterY = "MoveZ", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    static AnimatorController PlayerAnimator(PlayerCombat player)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(output + "/Player.controller");
        foreach (var name in new[] { "Speed", "MoveX", "MoveZ" }) controller.AddParameter(name, AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var locomotion = Tree(controller, "Locomotion", true);
        locomotion.AddChild(Clip("Idle_A", "PlayerIdle", true), Vector2.zero);
        locomotion.AddChild(Clip("Running_A", "PlayerForward", true), Vector2.up);
        locomotion.AddChild(Clip("Walking_Backwards", "PlayerBackward", true), Vector2.down);
        locomotion.AddChild(Clip("Running_Strafe_Left", "PlayerLeft", true), Vector2.left);
        locomotion.AddChild(Clip("Running_Strafe_Right", "PlayerRight", true), Vector2.right);
        var idle = State(machine, "Locomotion", locomotion);
        machine.defaultState = idle;
        var dodge = Tree(controller, "Dash", true);
        string[] dodges = { "Dodge_Forward", "Dodge_Backward", "Dodge_Left", "Dodge_Right" };
        Vector2[] axes = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        for (int i = 0; i < 4; i++) dodge.AddChild(Clip(dodges[i], "PlayerDash" + i, false), axes[i]);
        var children = dodge.children;
        float dashTime = Mathf.Max(0.05f, player.GetComponent<PlayerMovement>().duracaoDash);
        for (int i = 0; i < children.Length; i++) children[i].timeScale = Mathf.Max(0.01f, ((AnimationClip)children[i].motion).length) / dashTime;
        dodge.children = children;
        Exit(State(machine, "Dash", dodge), idle, 0.03f);
        State(machine, "Death", Clip("Death_A", "PlayerDeath", false));
        var mask = new AvatarMask { name = "MageUpperBody" };
        var paths = player.GetComponentsInChildren<Transform>(true).Select(t => AnimationUtility.CalculateTransformPath(t, player.transform)).ToArray();
        mask.transformCount = paths.Length;
        int active = 0;
        for (int i = 0; i < mask.transformCount; i++)
        {
            string path = paths[i];
            mask.SetTransformPath(i, path);
            bool upper = path.Contains("/spine") || path.EndsWith("spine");
            mask.SetTransformActive(i, upper);
            if (upper) active++;
        }
        if (active == 0) throw new InvalidOperationException("Rig do Mage nao possui caminho spine para a mascara superior.");
        AssetDatabase.CreateAsset(mask, output + "/MageUpperBody.mask");
        controller.AddLayer("Upper Body");
        controller.AddLayer("Feedback");
        var layers = controller.layers;
        for (int i = 1; i < layers.Length; i++) { layers[i].avatarMask = mask; layers[i].defaultWeight = 0; }
        controller.layers = layers;
        var upperMachine = layers[1].stateMachine;
        var empty = State(upperMachine, "Empty", null);
        upperMachine.defaultState = empty;
        Exit(State(upperMachine, "Ranged_Magic_Raise", Clip("Ranged_Magic_Raise", "PrimaryCast", false, "LancarMagia"), 2), empty);
        Exit(State(upperMachine, "Area", Clip("Ranged_Magic_Summon", "AreaCast", false, "LancarMagia"), 2), empty);
        Exit(State(upperMachine, "Burst", Clip("Ranged_Magic_Spellcasting", "BurstCast", false, "LancarMagia"), 2), empty);
        var feedback = layers[2].stateMachine;
        var rest = State(feedback, "Empty", null);
        feedback.defaultState = rest;
        Exit(State(feedback, "Hit", Clip("Hit_A", "PlayerHit", false)), rest);
        return controller;
    }

    static AnimatorController EnemyAnimator(string label, bool ranged, out float deathDuration)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(output + "/" + label + ".controller");
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        foreach (string name in new[] { "Attack", "Hit", "Die" }) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        var machine = controller.layers[0].stateMachine;
        var tree = Tree(controller, "Locomotion", false);
        tree.AddChild(Clip("Idle_A", label + "Idle", true), 0);
        tree.AddChild(Clip("Running_A", label + "Run", true), 1);
        var idle = State(machine, "Locomotion", tree);
        EnemyAISetup.ConfigureLocomotion(controller);
        machine.defaultState = idle;
        var attack = State(machine, "Attack", Clip(ranged ? "Ranged_Magic_Shoot" : "Melee_1H_Attack_Chop", label + "Attack", false, "AplicarGolpe"), 1.3f);
        var hit = State(machine, "Hit", Clip("Hit_A", label + "Hit", false));
        var death = Clip("Death_A", label + "Death", false);
        deathDuration = death.length + 0.25f;
        var dead = State(machine, "Death", death);
        Trigger(machine, attack, "Attack");
        Trigger(machine, hit, "Hit");
        Trigger(machine, dead, "Die");
        Exit(attack, idle);
        Exit(hit, idle);
        return controller;
    }

    static void ConfigurePlayer(PlayerCombat player, GameObject primary, GameObject burst, GameObject preparation, GameObject area, GameObject hit, GameObject death)
    {
        var animator = player.GetComponent<Animator>();
        animator.runtimeAnimatorController = PlayerAnimator(player);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var life = player.GetComponent<Vida>();
        if (life == null) life = player.gameObject.AddComponent<Vida>();
        var hp = player.GetComponent<PlayerHp>();
        if (hp == null) hp = player.gameObject.AddComponent<PlayerHp>();
        var serialized = new SerializedObject(hp);
        serialized.FindProperty("vidaMaxima").intValue = 100;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        hp.invulnerabilidade = 0.6f;
        life.vidaMaxima = 100;
        player.projectilePrefab = primary;
        player.cooldown = 0.5f;
        player.danoPrimario = 25;
        player.velocidadePrimario = 18;
        player.preparoPrimario = preparation;
        player.camadaAtaque = 1;
        player.estadoPrimario = "Upper Body.Ranged_Magic_Raise";
        player.explosao = new MagiaAdicional { dano = 35, cooldown = 4, raio = 4.5f, preparo = preparation, efeito = area, estado = "Upper Body.Area" };
        player.rajada = new MagiaAdicional { dano = 15, cooldown = 6, velocidade = 22, quantidade = 3, intervalo = 0.1f, projetil = burst, preparo = preparation, estado = "Upper Body.Burst" };
        var feedback = player.GetComponent<CombatFeedback>();
        if (feedback == null) feedback = player.gameObject.AddComponent<CombatFeedback>();
        feedback.efeitoHit = hit;
        feedback.efeitoMorte = death;
        ConvertMaterials(player.gameObject);
    }

    static EnemyAI EnemyPrefab(GameObject model, string label, float hp, int damage, float speed, bool ranged, GameObject weapon,
        GameObject shot, GameObject hit, GameObject death)
    {
        var item = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            item.name = label;
            var ai = item.AddComponent<EnemyAI>();
            var animator = item.GetComponent<Animator>();
            if (animator == null) animator = item.AddComponent<Animator>();
            animator.runtimeAnimatorController = EnemyAnimator(label, ranged, out float deathDuration);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var controller = item.GetComponent<CharacterController>();
            controller.height = 2;
            controller.radius = 0.4f;
            controller.center = new Vector3(0, 1.07f, 0);
            controller.stepOffset = 0.3f;
            var vida = item.GetComponent<Vida>();
            vida.vidaMaxima = hp;
            ai.dano = damage;
            ai.velocidade = speed;
            ai.intervaloAtaque = ranged ? 2f : label == "Fast" ? 1.2f : 1.5f;
            ai.raioDeteccao = 60;
            ai.raioPerderAlvo = 70;
            ai.alcanceAtaque = ranged ? 8 : 1.5f;
            ai.distanciaMinima = 6;
            ai.arma = weapon;
            ai.tempoParaSumir = deathDuration;
            ai.usarAnimationEvent = true;
            ai.usarNavMesh = true;
            ai.ataqueDistancia = ranged;
            ai.projetil = shot.GetComponent<Projectile>();
            var agent = item.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2;
            agent.speed = speed;
            agent.acceleration = 18;
            agent.angularSpeed = 0;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.autoRepath = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            var feedback = item.AddComponent<CombatFeedback>();
            feedback.efeitoHit = hit;
            feedback.efeitoMorte = death;
            ConvertMaterials(item);
            return PrefabUtility.SaveAsPrefabAsset(item, output + "/" + label + ".prefab").GetComponent<EnemyAI>();
        }
        finally { UnityEngine.Object.DestroyImmediate(item); }
    }

    static void ConvertMaterials(GameObject item)
    {
        foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var original = materials[i];
                if (original == null || original.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                if (!converted.TryGetValue(original, out var copy))
                {
                    copy = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = original.name + "_URP" };
                    copy.SetTexture("_BaseMap", original.mainTexture);
                    if (original.HasProperty("_Color")) copy.SetColor("_BaseColor", original.GetColor("_Color"));
                    copy.SetFloat("_Smoothness", 0.1f);
                    AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(output + "/" + copy.name + ".mat"));
                    converted.Add(original, copy);
                }
                materials[i] = copy;
            }
            renderer.sharedMaterials = materials;
        }
    }

    static Material EffectMaterial(string name, Color color)
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = name + "Glow", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), 1.5f)));
            }
        texture.Apply();
        AssetDatabase.CreateAsset(texture, output + "/" + name + "Glow.asset");
        var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", 2);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        AssetDatabase.CreateAsset(material, output + "/" + name + ".mat");
        return material;
    }

    static ParticleSystem Particles(GameObject parent, Material material, bool loop, int count, float duration, float speed, bool ring)
    {
        var child = new GameObject("Particles");
        child.transform.SetParent(parent.transform, false);
        if (ring) child.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var particles = child.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = loop;
        main.duration = Mathf.Max(0.1f, duration);
        main.startLifetime = duration;
        main.startSpeed = speed;
        main.startSize = ring ? 0.08f : loop ? 0.16f : 0.12f;
        main.maxParticles = loop ? 32 : 80;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = loop ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        var emission = particles.emission;
        emission.rateOverTime = loop ? 24 : 0;
        if (!loop) emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
        var shape = particles.shape;
        shape.shapeType = ring ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Sphere;
        shape.radius = ring ? 1 : 0.08f;
        shape.radiusThickness = ring ? 0 : 1;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return particles;
    }

    static GameObject Effect(string name, Material material, int count, float duration, float speed, bool ring)
    {
        var item = new GameObject(name);
        try
        {
            Particles(item, material, false, count, duration, speed, ring);
            item.AddComponent<TimedEffect>().duracao = duration + 0.2f;
            return PrefabUtility.SaveAsPrefabAsset(item, output + "/" + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(item); }
    }

    static GameObject ProjectilePrefab(string name, Material material, GameObject impact, float damage, float speed, bool enemy)
    {
        var item = new GameObject(name);
        try
        {
            var projectile = item.AddComponent<Projectile>();
            projectile.dano = damage;
            projectile.velocidade = speed;
            projectile.atingirJogador = enemy;
            projectile.impactoPrefab = impact;
            projectile.tempoDeVida = 3;
            Particles(item, material, true, 0, 0.22f, 0.05f, false);
            var trail = item.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.16f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return PrefabUtility.SaveAsPrefabAsset(item, output + "/" + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(item); }
    }

    static NavMeshData BakeNavigation(GameObject environment)
    {
        Physics.SyncTransforms();
        var sources = new List<NavMeshBuildSource>();
        var markups = new List<NavMeshBuildMarkup>();
        foreach (Transform child in environment.transform)
            if (child.name == "Walls" || child.name == "Props") markups.Add(new NavMeshBuildMarkup { root = child, overrideArea = true, area = 1 });
        NavMeshBuilder.CollectSources(environment.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
        if (sources.Count < 100) throw new InvalidOperationException("Geometria insuficiente para assar a arena.");
        if (NavMesh.GetSettingsCount() == 0) throw new InvalidOperationException("Nao ha agent type configurado na Unity.");
        var settings = NavMesh.GetSettingsByIndex(0);
        settings.agentRadius = 0.45f;
        settings.agentHeight = 2;
        settings.agentClimb = 0.3f;
        settings.agentSlope = 45;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.1f;
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Vector3.up, new Vector3(46, 10, 46)), Vector3.zero, Quaternion.identity);
        if (data == null) throw new InvalidOperationException("Bake de NavMesh falhou.");
        AssetDatabase.CreateAsset(data, output + "/ArenaNavigation.asset");
        return data;
    }

    static void ValidateNavigation(Transform player, Transform[] points)
    {
        if (!NavMesh.SamplePosition(player.position, out var target, 2, NavMesh.AllAreas)) throw new InvalidOperationException("Jogador fora do NavMesh.");
        var path = new NavMeshPath();
        foreach (var point in points)
            if (!NavMesh.SamplePosition(point.position, out var source, 2, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(source.position, target.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Spawn sem caminho completo ao jogador: " + point.name);
    }

    static GameObject UIObject(string name, Transform parent)
    {
        var item = new GameObject(name, typeof(RectTransform));
        item.transform.SetParent(parent, false);
        return item;
    }
    static void Rect(GameObject item, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = item.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
    static GameObject Panel(string name, Transform parent, Color color)
    {
        var item = UIObject(name, parent);
        Rect(item, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        item.AddComponent<UnityEngine.UI.Image>().color = color;
        return item;
    }
    static UnityEngine.UI.Text Label(string name, Transform parent, string text, int size, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        var item = UIObject(name, parent);
        Rect(item, min, max, offsetMin, offsetMax);
        var label = item.AddComponent<UnityEngine.UI.Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.9f, 0.94f, 1);
        label.raycastTarget = false;
        label.supportRichText = false;
        return label;
    }
    static UnityEngine.UI.Button Button(string name, Transform parent, string text)
    {
        var item = UIObject(name, parent);
        Rect(item, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(-170, -30), new Vector2(170, 30));
        var image = item.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.15f, 0.3f, 0.6f);
        var button = item.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        Label("Label", item.transform, text, 26, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }
    static void CreateUI(WaveManager waves)
    {
        var root = new GameObject("SurvivalHUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        var hud = root.AddComponent<SurvivalHUD>();
        hud.partida = waves;
        hud.status = Label("Status", root.transform, "", 22, new Vector2(0, 1), Vector2.one, new Vector2(24, -65), new Vector2(-24, -5));
        hud.habilidades = Label("Skills", root.transform, "", 20, Vector2.zero, new Vector2(1, 0), new Vector2(24, 10), new Vector2(-24, 95));
        var health = UIObject("Health", root.transform);
        Rect(health, new Vector2(0.25f, 1), new Vector2(0.75f, 1), new Vector2(0, -85), new Vector2(0, -67));
        health.AddComponent<UnityEngine.UI.Image>().color = new Color(0.06f, 0.08f, 0.14f);
        var fill = Panel("Fill", health.transform, new Color(0.2f, 0.75f, 0.7f));
        hud.vida = health.AddComponent<UnityEngine.UI.Slider>();
        hud.vida.fillRect = fill.GetComponent<RectTransform>();
        hud.vida.interactable = false;
        hud.vida.transition = UnityEngine.UI.Selectable.Transition.None;
        hud.vida.minValue = 0;
        hud.vida.maxValue = 100;
        hud.vida.value = 100;
        hud.inicio = Panel("Start", root.transform, new Color(0.025f, 0.04f, 0.1f, 0.82f));
        Label("Title", hud.inicio.transform, "UNDERDOG", 64, new Vector2(0, 0.6f), new Vector2(1, 0.8f), Vector2.zero, Vector2.zero);
        Label("Subtitle", hud.inicio.transform, "SOBREVIVA A CINCO ONDAS\nClique esquerdo: projetil | Direito: explosao | Q: rajada\nWASD: mover | Shift: dash", 24,
            new Vector2(0, 0.4f), new Vector2(1, 0.6f), new Vector2(20, 0), new Vector2(-20, 0));
        hud.iniciar = Button("StartButton", hud.inicio.transform, "Iniciar  [Enter]");
        hud.fim = Panel("End", root.transform, new Color(0.025f, 0.04f, 0.1f, 0.65f));
        hud.resultado = Label("Result", hud.fim.transform, "", 40, new Vector2(0, 0.45f), new Vector2(1, 0.75f), Vector2.zero, Vector2.zero);
        hud.reiniciar = Button("RestartButton", hud.fim.transform, "Jogar novamente  [R]");
        hud.fim.SetActive(false);
        if (!Objects<EventSystem>(waves.gameObject.scene).Any(e => e.isActiveAndEnabled))
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.GetComponent<EventSystem>().firstSelectedGameObject = hud.iniciar.gameObject;
        }
    }

    static void ValidateScene(Scene scene, PlayerCombat player, WaveManager waves)
    {
        if (waves.ondas.Length != 5 || waves.spawnPoints.Length < 3 || player.cajado == null || player.spellSpawnPoint == null)
            throw new InvalidOperationException("Partida incompleta.");
        foreach (var controller in new[] { player.GetComponent<Animator>().runtimeAnimatorController,
            waves.basico.GetComponent<Animator>().runtimeAnimatorController, waves.rapido.GetComponent<Animator>().runtimeAnimatorController, waves.distante.GetComponent<Animator>().runtimeAnimatorController })
        {
            foreach (var clip in controller.animationClips)
            {
                if (clip.name.EndsWith("Cast") || clip.name.EndsWith("Attack"))
                {
                    var events = AnimationUtility.GetAnimationEvents(clip);
                    if (events.Length != 1 || events[0].time <= 0 || events[0].time >= clip.length)
                        throw new InvalidOperationException("Evento ausente/duplicado no clipe " + clip.name);
                }
            }
        }
        if (Objects<PlayerMovement>(scene).Count(p => p.isActiveAndEnabled) != 1) throw new InvalidOperationException("Jogador ativo duplicado.");
    }
}
