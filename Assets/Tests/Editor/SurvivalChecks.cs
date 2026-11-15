using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class SurvivalChecks
{
    static IEnumerator checks;
    static GameObject root;
    static NavMeshData data;
    static NavMeshDataInstance navigation;
    public static bool Passed { get; private set; }
    public static bool Running => checks != null;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Underdog/Check Survival (Edit Mode)")]
    public static void EditChecks()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Execute fora do Play Mode.");
        var item = new GameObject("Health self-check");
        try
        {
            var vida = item.AddComponent<Vida>();
            Call(vida, "Awake");
            vida.invulnerabilidade = 100;
            vida.ReceberDano(25);
            Assert(vida.VidaAtual == 75, "Vida deve descontar dano.");
            vida.ReceberDano(25);
            Assert(vida.VidaAtual == 75, "Invulnerabilidade deve bloquear o segundo dano.");
            vida.Curar(10);
            Assert(vida.VidaAtual == 85, "Cura deve reutilizar a mesma vida.");
            int deaths = 0;
            var fatal = item.AddComponent<Vida>();
            Call(fatal, "Awake");
            fatal.aoMorrer.AddListener(() => deaths++);
            fatal.ReceberDano(float.NaN);
            Assert(fatal.VidaAtual == 100, "Dano NaN deve ser rejeitado.");
            fatal.ReceberDano(100);
            fatal.ReceberDano(100);
            fatal.Curar(100);
            Assert(deaths == 1 && fatal.EstaMorto && fatal.VidaAtual == 0, "Morte unica e sem ressuscitar por cura.");
            var chained = item.AddComponent<Vida>();
            Call(chained, "Awake");
            int chainedDeaths = 0;
            chained.aoReceberDano.AddListener(_ => chained.ReceberDano(100));
            chained.aoMorrer.AddListener(() => chainedDeaths++);
            chained.ReceberDano(10);
            Assert(chainedDeaths == 1, "Dano encadeado por evento nao pode duplicar a morte.");
            Debug.Log("PASS Survival Edit: HP, invulnerabilidade, cura e morte unica.");
        }
        finally { UnityEngine.Object.DestroyImmediate(item); }
    }

    [MenuItem("Tools/Underdog/Check Survival (Play Mode)")]
    public static void PlayChecks()
    {
        if (!Application.isPlaying || Running) { Debug.LogWarning("Entre em Play Mode e aguarde a checagem anterior."); return; }
        Passed = false;
        root = new GameObject("Survival checks (temporary)");
        checks = Run();
        EditorApplication.update += Tick;
    }
    static void Tick()
    {
        try
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode interrompido durante o teste.");
            if (checks.MoveNext()) return;
            Passed = true;
            Debug.Log("PASS Survival Play: passos de projetil 60/10 FPS, dano unico, bloqueio apos morte, area sem dano duplicado/atraves de parede, cinco ondas, vitoria e derrota.");
        }
        catch (Exception error) { Debug.LogException(error); }
        Cleanup();
    }
    static void Cleanup()
    {
        EditorApplication.update -= Tick;
        checks = null;
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        if (navigation.valid) navigation.Remove();
        if (data != null) UnityEngine.Object.DestroyImmediate(data);
    }
    static GameObject Item(string name, Vector3 position)
    {
        var item = new GameObject(name);
        item.transform.SetParent(root.transform);
        item.transform.position = position;
        return item;
    }
    static Vida Target(Vector3 position)
    {
        var item = Item("Target", position);
        var box = item.AddComponent<BoxCollider>();
        box.center = Vector3.up;
        box.size = new Vector3(0.6f, 2, 0.6f);
        return item.AddComponent<Vida>();
    }
    static IEnumerator Run()
    {
        var origin = new Vector3(5000, 0, 5000);
        var owner = Item("Owner", origin).AddComponent<Vida>();
        for (int rate = 0; rate < 2; rate++)
        {
            var target = Target(origin + Vector3.forward * 3);
            var shot = Item("Projectile", origin + Vector3.up).AddComponent<Projectile>();
            shot.Configurar(owner.transform, 25, 18);
            Physics.SyncTransforms();
            for (int step = 0; step < 30; step++) shot.Avancar(rate == 0 ? 1f / 60 : 0.1f);
            Assert(target.VidaAtual == 75, "Projetil deve acertar exatamente uma vez em ambos os passos de tempo.");
            UnityEngine.Object.DestroyImmediate(shot.gameObject);
            UnityEngine.Object.DestroyImmediate(target.gameObject);
        }
        var behind = Target(origin + Vector3.forward * 3);
        owner.ReceberDano(100);
        var canceled = Item("CanceledProjectile", origin + Vector3.up).AddComponent<Projectile>();
        canceled.Configurar(owner.transform, 25, 18);
        canceled.Avancar(1);
        Assert(behind.VidaAtual == 100, "Conjurador morto nao pode causar dano.");
        UnityEngine.Object.DestroyImmediate(canceled.gameObject);
        UnityEngine.Object.DestroyImmediate(behind.gameObject);

        var caster = Item("Area caster", origin + Vector3.right * 20).AddComponent<PlayerCombat>();
        var casterLife = caster.gameObject.AddComponent<Vida>();
        Set(caster, "vida", casterLife);
        var areaTarget = Target(caster.transform.position + Vector3.right * 3);
        areaTarget.gameObject.AddComponent<SphereCollider>().center = Vector3.up;
        Physics.SyncTransforms();
        Call(caster, "Explodir");
        Assert(areaTarget.VidaAtual == 65, "Area deve atingir cada Vida uma vez, mesmo com dois colliders.");
        var blocked = Target(caster.transform.position + Vector3.forward * 3);
        var wall = Item("Test wall", caster.transform.position + new Vector3(0, 1, 1.5f));
        wall.AddComponent<BoxCollider>().size = new Vector3(2, 2, 0.3f);
        Physics.SyncTransforms();
        Call(caster, "Explodir");
        Assert(blocked.VidaAtual == 100, "Area nao pode atravessar parede.");
        UnityEngine.Object.DestroyImmediate(caster.gameObject);
        UnityEngine.Object.DestroyImmediate(areaTarget.gameObject);
        UnityEngine.Object.DestroyImmediate(blocked.gameObject);
        UnityEngine.Object.DestroyImmediate(wall);

        var settings = NavMesh.GetSettingsByIndex(0);
        var sources = new System.Collections.Generic.List<NavMeshBuildSource> {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(20, 0.2f, 20),
                transform = Matrix4x4.TRS(origin - Vector3.up * 0.1f, Quaternion.identity, Vector3.one), area = 0 }
        };
        data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin, new Vector3(24, 8, 24)), Vector3.zero, Quaternion.identity);
        Assert(data != null, "NavMesh de teste deve ser criado.");
        navigation = NavMesh.AddNavMeshData(data);
        var player = Item("Wave player", origin).AddComponent<PlayerHp>();
        var prefab = Item("Wave prefab", origin + Vector3.right * 100);
        prefab.SetActive(false);
        var ai = prefab.AddComponent<EnemyAI>();
        var game = Item("Wave game", origin);
        game.SetActive(false);
        var waves = game.AddComponent<WaveManager>();
        waves.jogador = player;
        waves.basico = waves.rapido = waves.distante = ai;
        waves.spawnPoints = new[] { root.transform };
        waves.intervaloOndas = 0;
        waves.ondas = new WaveManager.Onda[5];
        for (int i = 0; i < 5; i++) waves.ondas[i] = new WaveManager.Onda { basicos = 0 };
        game.SetActive(true);
        int frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        waves.Iniciar();
        double end = EditorApplication.timeSinceStartup + 10;
        while (waves.Situacao == WaveManager.Estado.Jogando && EditorApplication.timeSinceStartup < end) yield return null;
        Assert(waves.Situacao == WaveManager.Estado.Vitoria && waves.OndaAtual == 5 && waves.Restantes == 0, "Cinco ondas concluidas devem produzir vitoria.");
        var loss = Item("Loss game", origin);
        loss.SetActive(false);
        var loser = loss.AddComponent<WaveManager>();
        loser.jogador = player;
        loser.basico = loser.rapido = loser.distante = ai;
        loser.spawnPoints = new[] { root.transform };
        loss.SetActive(true);
        loser.Iniciar();
        player.Vida.ReceberDano(100);
        Assert(loser.Situacao == WaveManager.Estado.Derrota, "Morte do jogador deve encerrar a partida em derrota.");
    }
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
}
