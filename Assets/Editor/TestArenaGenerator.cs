using System;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

public static class TestArenaGenerator
{
    const string Destination = "Assets/Scenes/ArenaTeste.unity";
    const string Models = "Assets/Assets_Externos/KayKit_Dungeon_Pack_1.1_FREE/Assets/fbx(unity)";
    const string TexturePath = "Assets/Assets_Externos/KayKit_Dungeon_Pack_1.1_FREE/Assets/textures/dungeon_texture.png";
    const string MaterialPath = "Assets/Generated/UnderdogArena/DungeonArena.mat";
    const float Cell = 4f;
    const int Count = 10;

    [MenuItem("Tools/Underdog/Generate Test Arena")]
    public static void Generate()
    {
        string temporary = null;
        Scene copy = default;
        var source = SceneManager.GetActiveScene();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode antes de gerar a arena.");
            if (!Application.unityVersion.StartsWith("2022.3."))
                throw new InvalidOperationException("Abra este projeto na Unity 2022.3.62f3. Nenhuma cena foi alterada.");
            if (!source.IsValid() || !source.isLoaded || source.path == Destination)
                throw new InvalidOperationException("Abra uma cena de origem, preferencialmente ShopAndHpTest, e nao ArenaTeste.");
            if (SceneManager.sceneCount != 1 || source.isDirty || string.IsNullOrEmpty(source.path))
                throw new InvalidOperationException("Salve manualmente a cena de origem e mantenha somente ela aberta. O gerador nao salva alteracoes da origem.");
            if (GraphicsSettings.currentRenderPipeline == null ||
                !GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal"))
                throw new InvalidOperationException("Ative uma configuracao URP compativel com Unity 2022.3 antes de gerar.");
            var players = Components<PlayerMovement>(source);
            var enemies = Components<EnemyAI>(source);
            var rigs = Components<CinemachineVirtualCamera>(source);
            if (players.Length != 1 || enemies.Length == 0 || rigs.Length != 1 || Components<Camera>(source).Length != 1)
                throw new InvalidOperationException("A origem precisa de exatamente um jogador, uma Camera, uma camera virtual e ao menos um EnemyAI. Use ShopAndHpTest.");
            if (!players[0].isActiveAndEnabled || !rigs[0].isActiveAndEnabled ||
                Components<Camera>(source)[0].GetComponent<CinemachineBrain>() == null)
                throw new InvalidOperationException("Jogador/camera virtual devem estar ativos e a Camera precisa de CinemachineBrain.");
            ValidateCombat(players[0].GetComponent<PlayerCombat>());
            string[] names = { "floor_tile_large", "floor_tile_large_rocks", "wall_half", "column", "barrel_large",
                "floor_tile_big_grate", "floor_tile_small_decorated", "rubble_half", "crates_stacked", "wall_arched" };
            var models = names.Select(LoadModel).ToArray();
            if (Shader.Find("Universal Render Pipeline/Lit") == null || AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null)
                throw new InvalidOperationException("Shader URP/Lit ou textura KayKit nao disponivel.");
            bool exists = File.Exists(Destination);
            if (exists && !EditorUtility.DisplayDialog("Substituir ArenaTeste?",
                "Uma copia de seguranca da arena existente sera criada antes da substituicao. Continuar?", "Substituir", "Cancelar"))
                return;
            if (exists && SceneManager.GetSceneByPath(Destination).isLoaded)
                throw new InvalidOperationException("Feche ArenaTeste antes de substituir para preservar alteracoes abertas.");

            // SaveAsCopy captura inclusive alteracoes nao salvas sem escrever na cena de origem.
            temporary = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/ArenaTeste_build.unity");
            if (!EditorSceneManager.SaveScene(source, temporary, true))
                throw new IOException("Nao foi possivel criar a copia de trabalho.");
            copy = EditorSceneManager.OpenScene(temporary, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(copy);
            var player = Components<PlayerMovement>(copy).Single();
            var enemyCopies = Components<EnemyAI>(copy);
            var rig = Components<CinemachineVirtualCamera>(copy).Single();
            var camera = Components<Camera>(copy).Single();
            // Mantem referencias e objetos antigos recuperaveis na copia, sem seus colliders de teste.
            foreach (var root in copy.GetRootGameObjects())
            {
                bool keep = root.GetComponentsInChildren<Component>(true).Any(c => c is PlayerMovement || c is EnemyAI ||
                    c is Camera || c is CinemachineVirtualCameraBase || c is Light || (c != null && c.GetType().Name == "Volume"));
                if (!keep) root.SetActive(false);
            }
            var environment = Group("Environment", copy);
            var floor = Group("Floor", copy, environment.transform);
            var walls = Group("Walls", copy, environment.transform);
            var props = Group("Props", copy, environment.transform);
            var spawns = Group("SpawnPoints", copy);
            var material = ArenaMaterial();
            for (int x = 0; x < Count; x++)
                for (int z = 0; z < Count; z++)
                {
                    bool border = x == 0 || z == 0 || x == Count - 1 || z == Count - 1;
                    int variant = border ? ((x + z) % 3 == 0 ? 5 : 1) :
                        ((x == 4 || x == 5) && (z == 4 || z == 5) ? 6 : 0);
                    Place(models[variant], floor.transform, new Vector3(-18 + x * Cell, 0, -18 + z * Cell),
                        new Vector3(Cell, 0.25f, Cell), ((x + z) % 4) * 90, material, true);
                }
            for (int i = 0; i < Count; i++)
            {
                float p = -18 + i * Cell;
                Place(models[2], walls.transform, new Vector3(p, 0, -20.25f), new Vector3(4, 0.9f, 0.5f), 0, material);
                Place(models[2], walls.transform, new Vector3(p, 0, 20.25f), new Vector3(4, 1.4f, 0.5f), 180, material);
                Place(models[2], walls.transform, new Vector3(-20.25f, 0, p), new Vector3(4, 1.2f, 0.5f), 90, material);
                Place(models[2], walls.transform, new Vector3(20.25f, 0, p), new Vector3(4, 1.2f, 0.5f), 270, material);
            }
            for (int i = 0; i < 8; i++)
            {
                float x = i % 2 == 0 ? -18 : 18;
                float z = new[] { -16f, -6f, 6f, 16f }[i / 2];
                Place(models[3], props.transform, new Vector3(x, 0, z), new Vector3(1, 1.8f, 1), 0, material);
                Place(models[i < 4 ? 4 : 8], props.transform, new Vector3(x - Mathf.Sign(x) * 1.5f, 0, z + 1.5f),
                    i < 4 ? new Vector3(0.8f, 1, 0.8f) : new Vector3(1.4f, 1.2f, 1), i * 90, material);
            }
            for (int i = 0; i < 4; i++)
                Place(models[7], props.transform, new Vector3(i % 2 == 0 ? -17 : 17, 0, i < 2 ? -11 : 11),
                    new Vector3(1.4f, 0.5f, 1), i * 90, material);
            foreach (float x in new[] { -12f, 12f })
                Place(models[9], props.transform, new Vector3(x, 0, 19.3f), new Vector3(3, 2.6f, 0.6f), 0, material);
            var playerSpawn = Group("PlayerSpawn", copy, spawns.transform);
            playerSpawn.transform.position = new Vector3(0, 0.15f, -4);
            var enemyPositions = new[] { new Vector3(0, 0.15f, 4), new Vector3(12, 0.15f, 0),
                new Vector3(0, 0.15f, -12), new Vector3(-12, 0.15f, 0) };
            for (int i = 0; i < enemyPositions.Length; i++)
                Group("EnemySpawn_" + (i + 1), copy, spawns.transform).transform.position = enemyPositions[i];
            MoveCharacter(player.gameObject, playerSpawn.transform.position);
            for (int i = 0; i < enemyCopies.Length; i++)
                MoveCharacter(enemyCopies[i].gameObject, enemyPositions[i % 4] + Vector3.right * (i / 4) * 2);
            ConfigureCamera(rig, camera, player.transform);
            ConfigureLight(copy);
            ValidateArena(copy, floor, walls, player, enemyCopies.Length);
            if (exists)
            {
                var backup = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/ArenaTeste_backup.unity");
                if (!AssetDatabase.CopyAsset(Destination, backup)) throw new IOException("Backup falhou; destino preservado.");
                Debug.Log("Backup da arena anterior: " + backup);
            }
            if (!EditorSceneManager.SaveScene(copy, Destination)) throw new IOException("Falha ao salvar a arena.");
            EditorSceneManager.CloseScene(source, true);
            Debug.Log("Arena salva em " + Destination + ". 40x40, 100 pisos, 40 paredes e 4 spawns. " +
                "Origem preservada e fechada para evitar jogadores duplicados no Play Mode. NavMesh ainda nao assado; EnemyAI continua com CharacterController.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (copy.IsValid() && copy.isLoaded) EditorSceneManager.CloseScene(copy, true);
            if (source.IsValid() && source.isLoaded) SceneManager.SetActiveScene(source);
            EditorUtility.DisplayDialog("Arena nao gerada", error.Message, "OK");
        }
        finally
        {
            if (temporary != null) AssetDatabase.DeleteAsset(temporary);
        }
    }

    static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    static GameObject LoadModel(string name)
    {
        string path = Models + "/" + name + ".fbx";
        string guid = AssetDatabase.AssetPathToGUID(path);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        if (asset == null || asset.GetComponentsInChildren<MeshFilter>(true).Length == 0)
            throw new InvalidOperationException("Modelo KayKit nao importado: " + path);
        return asset;
    }

    static GameObject Group(string name, Scene scene, Transform parent = null)
    {
        var item = new GameObject(name);
        SceneManager.MoveGameObjectToScene(item, scene);
        item.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(item, "Generate Test Arena");
        return item;
    }

    static Material ArenaMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder("Assets/Generated/UnderdogArena")) AssetDatabase.CreateFolder("Assets/Generated", "UnderdogArena");
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        material.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    static Bounds BoundsOf(GameObject item)
    {
        var renderers = item.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Modelo sem Renderer: " + item.name);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        if (bounds.size.x < 0.001f || bounds.size.y < 0.001f || bounds.size.z < 0.001f)
            throw new InvalidOperationException("Limites invalidos: " + item.name);
        return bounds;
    }

    static void Place(GameObject asset, Transform parent, Vector3 position, Vector3 size, float yaw, Material material, bool floor = false)
    {
        var item = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        Undo.RegisterCreatedObjectUndo(item, "Place KayKit module");
        item.transform.localPosition = Vector3.zero;
        item.transform.rotation = Quaternion.identity;
        var bounds = BoundsOf(item);
        item.transform.localScale = Vector3.Scale(item.transform.localScale,
            new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
        bounds = BoundsOf(item);
        item.transform.position += new Vector3(-bounds.center.x, floor ? -bounds.max.y : -bounds.min.y, -bounds.center.z);
        // Um pivot central separado respeita FBXs cujo pivot nao fica no centro do modulo.
        var pivot = Group(asset.name + "_Module", parent.gameObject.scene, parent);
        item.transform.SetParent(pivot.transform, true);
        pivot.transform.rotation = Quaternion.Euler(0, yaw, 0);
        pivot.transform.position = position;
        foreach (var collider in item.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var box = pivot.AddComponent<BoxCollider>();
        box.center = new Vector3(0, floor ? -size.y / 2 : size.y / 2, 0);
        box.size = size;
        foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
        foreach (var transform in pivot.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, StaticEditorFlags.NavigationStatic | StaticEditorFlags.OccluderStatic);
    }

    static void MoveCharacter(GameObject item, Vector3 position)
    {
        item.SetActive(true);
        var controller = item.GetComponent<CharacterController>();
        bool enabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        item.transform.position = position;
        if (controller != null) controller.enabled = enabled;
    }

    static void ConfigureCamera(CinemachineVirtualCamera rig, Camera camera, Transform player)
    {
        camera.enabled = true;
        camera.tag = "MainCamera";
        var brain = camera.GetComponent<CinemachineBrain>();
        brain.enabled = true;
        brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.LateUpdate;
        brain.m_WorldUpOverride = null;
        brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.25f);
        rig.enabled = true;
        rig.Priority = 100;
        rig.tag = "Untagged";
        rig.transform.SetParent(null, true);
        rig.Follow = player;
        rig.LookAt = null;
        // Aim = Do Nothing: nenhuma etapa pode substituir a orientacao fixa apos o Body.
        foreach (var component in rig.GetComponentPipeline().Where(c => c.Stage == CinemachineCore.Stage.Aim).ToArray())
            Undo.DestroyObjectImmediate(component);
        rig.transform.rotation = Quaternion.Euler(50, 0, 0);
        var lens = rig.m_Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        lens.OrthographicSize = 8.5f;
        lens.Dutch = 0;
        rig.m_Lens = lens;
        var framing = rig.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing == null) framing = rig.AddCinemachineComponent<CinemachineFramingTransposer>();
        framing.m_XDamping = framing.m_YDamping = framing.m_ZDamping = 0.6f;
        framing.m_ScreenX = framing.m_ScreenY = 0.5f;
        framing.m_DeadZoneWidth = framing.m_DeadZoneHeight = 0.05f;
        framing.m_CameraDistance = 25;
        framing.m_TrackedObjectOffset = Vector3.zero;
        framing.m_LookaheadTime = 0;
        camera.orthographic = true;
        camera.orthographicSize = 8.5f;
        rig.ForceCameraPosition(player.position - rig.transform.forward * 25, rig.transform.rotation);
        camera.transform.SetPositionAndRotation(rig.transform.position, rig.transform.rotation);
    }

    static void ConfigureLight(Scene scene)
    {
        var light = Components<Light>(scene).FirstOrDefault(l => l.type == LightType.Directional && l.enabled);
        if (light == null) light = Group("Arena Key Light", scene).AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);
        light.color = new Color(1, 0.92f, 0.8f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.shadowStrength = 0.8f;
        light.shadowBias = 0.03f;
        light.shadowNormalBias = 0.3f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.28f, 0.34f, 0.44f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.23f, 0.27f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.09f);
    }

    static void ValidateCombat(PlayerCombat combat)
    {
        if (combat == null || combat.cajado == null || combat.spellSpawnPoint == null || combat.projectilePrefab == null)
            throw new InvalidOperationException("Referencias de PlayerCombat ausentes na origem.");
        var animator = combat.GetComponent<Animator>();
        var controller = animator != null ? animator.runtimeAnimatorController : null;
        if (controller == null || !controller.animationClips.Any(clip => AnimationUtility.GetAnimationEvents(clip).Any(e => e.functionName == "LancarMagia")))
            throw new InvalidOperationException("Animator ou Animation Event LancarMagia ausente.");
    }

    static void ValidateArena(Scene scene, GameObject floor, GameObject walls, PlayerMovement player, int enemies)
    {
        ValidateCombat(player.GetComponent<PlayerCombat>());
        var rig = Components<CinemachineVirtualCamera>(scene).Single();
        var originalRotation = player.transform.rotation;
        try
        {
            player.transform.rotation = Quaternion.Euler(0, 137, 0);
            rig.PreviousStateIsValid = false;
            rig.UpdateCameraState(Vector3.up, -1);
            if (Quaternion.Angle(rig.State.FinalOrientation, Quaternion.Euler(50, 0, 0)) > 0.1f ||
                !rig.State.Lens.Orthographic || Mathf.Abs(rig.State.Lens.OrthographicSize - 8.5f) > 0.01f)
                throw new InvalidOperationException("Pipeline Cinemachine nao preservou 50 graus/ortografica/size 8.5 com jogador rotacionado.");
        }
        finally { player.transform.rotation = originalRotation; }
        if (floor.transform.childCount != 100 || walls.transform.childCount != 40 || Components<EnemyAI>(scene).Length != enemies)
            throw new InvalidOperationException("Checagem da montagem falhou: contagem de modulos ou inimigos.");
        foreach (var box in floor.GetComponentsInChildren<BoxCollider>().Concat(walls.GetComponentsInChildren<BoxCollider>()))
            if (!box.enabled || box.isTrigger || box.size.x <= 0 || box.size.z <= 0)
                throw new InvalidOperationException("Collider de arena invalido.");
        Physics.SyncTransforms();
        var tiles = floor.GetComponentsInChildren<BoxCollider>();
        var bounds = tiles[0].bounds;
        foreach (var tile in tiles.Skip(1)) bounds.Encapsulate(tile.bounds);
        if (Mathf.Abs(bounds.size.x - 40) > 0.01f || Mathf.Abs(bounds.size.z - 40) > 0.01f || Mathf.Abs(bounds.max.y) > 0.01f)
            throw new InvalidOperationException("Piso precisa cobrir 40x40 com superficie em Y=0.");
    }

    [MenuItem("Tools/Underdog/Validate Arena (Play Mode)")]
    public static void ValidatePlayingArena()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().path != Destination)
        {
            Debug.LogWarning("Abra ArenaTeste, entre em Play Mode e execute a validacao novamente.");
            return;
        }
        var scene = SceneManager.GetActiveScene();
        var cameras = UnityEngine.Object.FindObjectsOfType<Camera>().Where(c => c.isActiveAndEnabled && c.targetTexture == null).ToArray();
        if (cameras.Length != 1 || SceneManager.sceneCount != 1)
            Debug.LogWarning("Mais de uma cena/camera pode estar participando da Game View. Confira cenas aditivas e cameras sobrepostas.");
        foreach (var camera in cameras)
        {
            var brain = camera.GetComponent<CinemachineBrain>();
            var live = brain != null ? brain.ActiveVirtualCamera : null;
            Debug.Log($"[Arena Camera] {camera.name}, Camera.main={Camera.main}, cena={camera.gameObject.scene.name}, display={camera.targetDisplay}, depth={camera.depth}, " +
                $"rotacao={camera.transform.eulerAngles}, ortografica={camera.orthographic}, size={camera.orthographicSize}, " +
                $"BrainAtivo={brain != null && brain.isActiveAndEnabled}, Live={live?.Name ?? "nenhuma"}, blend={brain != null && brain.IsBlending}", camera);
            if (brain == null || !brain.isActiveAndEnabled || live == null || !camera.orthographic ||
                Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(50, 0, 0)) > 1 || Mathf.Abs(camera.orthographicSize - 8.5f) > 0.1f)
                Debug.LogError("A saida real da camera nao corresponde a camera da arena. Confira Live Camera no Brain e outros scripts/Timelines.", camera);
        }
        foreach (var rig in Components<CinemachineVirtualCamera>(scene))
            Debug.Log($"[Arena Rig] {rig.name}, ativo={rig.isActiveAndEnabled}, priority={rig.Priority}, Follow={rig.Follow}, " +
                $"LookAt={rig.LookAt}, Body={rig.GetCinemachineComponent(CinemachineCore.Stage.Body)}, Aim={rig.GetCinemachineComponent(CinemachineCore.Stage.Aim)}", rig);

        var environment = scene.GetRootGameObjects().SingleOrDefault(root => root.name == "Environment" && root.activeInHierarchy);
        if (environment == null) { Debug.LogError("Environment ativo nao encontrado."); return; }
        Physics.SyncTransforms();
        var floor = environment.transform.Find("Floor");
        var walls = environment.transform.Find("Walls");
        if (floor == null || walls == null) { Debug.LogError("Floor/Walls ausentes."); return; }
        foreach (var module in floor.Cast<Transform>().Concat(walls.Cast<Transform>()))
        {
            var box = module.GetComponent<BoxCollider>();
            if (box == null || !box.enabled || box.isTrigger)
                Debug.LogError("Collider solido ausente: " + module.name, module);
        }
        if (NavMesh.CalculateTriangulation().vertices.Length == 0)
        {
            Debug.LogWarning("Nenhum NavMesh carregado. Cobertura e caminhos NAO validados. EnemyAI usa perseguicao direta com CharacterController.");
            return;
        }
        int checkedPoints = 0, failures = 0;
        var path = new NavMeshPath();
        bool hasCenter = NavMesh.SamplePosition(Vector3.zero, out var center, 0.5f, NavMesh.AllAreas);
        for (int x = -16; x <= 16; x += 4)
            for (int z = -16; z <= 16; z += 4)
            {
                var point = new Vector3(x, 0, z);
                if (Physics.OverlapCapsule(point + Vector3.up * 0.5f, point + Vector3.up * 1.5f, 0.35f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(c => c.transform.IsChildOf(environment.transform))) continue;
                checkedPoints++;
                if (!hasCenter || !NavMesh.SamplePosition(point, out var sample, 0.5f, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(sample.position, center.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    failures++;
            }
        Debug.Log($"[Arena Navigation] amostras livres={checkedPoints}, sem cobertura/caminho ao centro={failures}. " +
            "Amostragem do NavMesh padrao; nao substitui teste de perseguicao e nao muda o controlador do inimigo.");
        if (failures > 0 || checkedPoints == 0)
            Debug.LogWarning("Cobertura/conectividade do NavMesh incompleta ou sem amostras validas. Confira bake, agent type e limites da arena.");
    }
}
