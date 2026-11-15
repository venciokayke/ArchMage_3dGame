using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyNavigationChecks
{
    static IEnumerator running;
    static GameObject root;
    static Transform target;
    static NavMeshData data;
    static NavMeshDataInstance mesh;
    static readonly Vector3 Origin = new Vector3(200, 0, 200);
    static readonly List<EnemyAI> actors = new List<EnemyAI>();
    public static bool Running => running != null;
    public static bool Passed { get; private set; }
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Underdog/Check Enemy Navigation (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying || EditorApplication.isPaused || Running)
        { Debug.LogWarning("Execute em Play Mode sem pausa; aguarde a checagem anterior."); return; }
        Passed = false;
        running = Suite();
        EditorApplication.update += Tick;
    }
    static void Tick()
    {
        try
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode interrompido.");
            if (running.MoveNext()) return;
            Passed = true;
            Debug.Log("PASS Enemy Navigation: individual, melee distribuido, flanco, ranged, parede, cobertura, 25 agentes mistos e alvo em dash.");
        }
        catch (Exception error) { Debug.LogException(error); }
        EditorApplication.update -= Tick;
        running = null;
        Cleanup();
    }
    static void Cleanup()
    {
        actors.Clear();
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        if (mesh.valid) mesh.Remove();
        if (data != null) UnityEngine.Object.DestroyImmediate(data);
    }
    static GameObject Item(string name, Vector3 position)
    {
        var item = new GameObject(name);
        item.transform.SetParent(root.transform);
        item.transform.position = Origin + position;
        return item;
    }
    static void Fixture(Vector3 player, bool wall = false)
    {
        Cleanup();
        root = new GameObject("Enemy navigation checks (temporary)");
        var sources = new List<NavMeshBuildSource>();
        var floor = Item("Floor", new Vector3(0, -0.1f, 0)).AddComponent<BoxCollider>();
        floor.size = new Vector3(40, 0.2f, 40);
        sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = floor.size,
            transform = floor.transform.localToWorldMatrix, area = 0 });
        if (wall)
        {
            var obstacle = Item("Obstacle", Vector3.up * 1.5f).AddComponent<BoxCollider>();
            obstacle.size = new Vector3(1, 3, 10);
            sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = obstacle.size,
                transform = obstacle.transform.localToWorldMatrix, area = 1 });
        }
        var settings = NavMesh.GetSettingsByIndex(0);
        settings.agentRadius = 0.45f;
        settings.agentHeight = 2;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.1f;
        data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Origin, new Vector3(44, 10, 44)), Vector3.zero, Quaternion.identity);
        Assert(data != null, "NavMesh de teste deve ser criado.");
        mesh = NavMesh.AddNavMeshData(data);
        var playerObject = Item("Target", player);
        var box = playerObject.AddComponent<BoxCollider>();
        box.center = Vector3.up;
        box.size = new Vector3(0.8f, 2, 0.8f);
        playerObject.AddComponent<Vida>().aceitaDano = false;
        target = playerObject.transform;
        Physics.SyncTransforms();
    }
    static EnemyAI Actor(Vector3 point, EnemyAI.Comportamento style)
    {
        var item = Item(style.ToString(), point + Vector3.up * 0.05f);
        item.SetActive(false);
        var ai = item.AddComponent<EnemyAI>();
        ai.usarNavMesh = true;
        ai.comportamento = style;
        ai.ataqueDistancia = style == EnemyAI.Comportamento.Distante;
        ai.velocidade = style == EnemyAI.Comportamento.Flanqueador ? 5.5f : 2.5f;
        ai.raioDeteccao = ai.raioPerderAlvo = 70;
        ai.dano = 0;
        ai.alcanceAtaque = ai.ataqueDistancia ? 8 : 1.5f;
        ai.distanciaMinima = 6;
        var body = item.GetComponent<CharacterController>();
        body.height = 2; body.radius = 0.4f; body.center = new Vector3(0, 1.07f, 0);
        var agent = item.AddComponent<NavMeshAgent>();
        agent.radius = 0.4f; agent.height = 2; agent.acceleration = 18;
        item.SetActive(true);
        actors.Add(ai);
        return ai;
    }
    static void Bind()
    {
        foreach (var ai in actors)
        {
            typeof(EnemyAI).GetField("alvo", Private).SetValue(ai, target);
            typeof(EnemyAI).GetField("vidaAlvo", Private).SetValue(ai, target.GetComponent<Vida>());
            typeof(EnemyAI).GetField("hpAlvo", Private).SetValue(ai, null);
        }
    }
    static float Distance(EnemyAI ai) => Vector3.ProjectOnPlane(ai.transform.position - target.position, Vector3.up).magnitude;
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static IEnumerator Suite()
    {
        Fixture(Vector3.zero);
        var single = Actor(new Vector3(-10, 0, 0), EnemyAI.Comportamento.Guerreiro);
        int frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        Bind();
        float end = Time.time + 5;
        while (Time.time < end) yield return null;
        Assert(Distance(single) < 2, "Guerreiro individual precisa alcancar o jogador. " + Diagnostics(single));
        Assert(!single.GetComponent<NavMeshAgent>().updatePosition && !single.GetComponent<NavMeshAgent>().updateRotation, "Agent nao pode disputar o Transform.");
        Debug.Log("PASS: guerreiro individual.");
        single.usarAnimationEvent = true;
        single.dano = 10;
        single.intervaloAtaque = 10;
        Set(single, "momentoDoGolpe", -1f);
        Set(single, "proximoAtaque", -1f);
        target.GetComponent<Vida>().aceitaDano = true;
        end = Time.time + 2;
        while (Time.time < end && Pending(single) < 0) yield return null;
        Assert(Pending(single) >= 0 && Distance(single) <= single.alcanceAtaque,
            "A aproximacao deve terminar em um ataque valido. " + Diagnostics(single));
        Vector3 attackPosition = single.transform.position;
        end = Time.time + 0.2f;
        while (Time.time < end) yield return null;
        Assert(Vector3.Distance(attackPosition, single.transform.position) < 0.15f, "Golpe pendente deve pausar a locomocao.");
        single.AplicarGolpe();
        single.AplicarGolpe();
        Assert(target.GetComponent<Vida>().VidaAtual == 90, "Evento duplicado nao pode duplicar dano.");
        typeof(EnemyAI).GetMethod("TentarAtacar", Private).Invoke(single, null);
        Assert(Pending(single) < 0, "Cooldown deve rejeitar novo ataque.");
        Set(single, "proximoAtaque", -1f);
        typeof(EnemyAI).GetMethod("TentarAtacar", Private).Invoke(single, null);
        Assert(Pending(single) >= 0, "Ataque deve poder ser aceito apos liberar o cooldown.");
        target.position += Vector3.right * 4;
        Physics.SyncTransforms();
        single.AplicarGolpe();
        Assert(target.GetComponent<Vida>().VidaAtual == 90, "Dash para fora do alcance deve evitar o golpe pendente.");
        Set(single, "momentoDoGolpe", Time.time);
        single.GetComponent<Vida>().ReceberDano(1000);
        single.AplicarGolpe();
        Assert(Pending(single) < 0 && target.GetComponent<Vida>().VidaAtual == 90, "Morte deve cancelar evento pendente.");
        Debug.Log("PASS: alcance final, pausa no ataque, evento unico, cooldown, esquiva e cancelamento por morte.");

        Fixture(Vector3.zero);
        for (int i = 0; i < 8; i++) Actor(new Vector3(-10 - i * 0.8f, 0, (i - 3.5f) * 1.1f), EnemyAI.Comportamento.Guerreiro);
        frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        Bind();
        end = Time.time + 9;
        while (Time.time < end) yield return null;
        Assert(actors.Select(a => a.DestinoAtual).Distinct().Count() >= 6, "Melee precisam de destinos distintos.");
        Assert(actors.Count(a => Distance(a) < 3.5f) >= 5, "Grupo melee precisa pressionar o jogador.");
        CheckBodies();
        Debug.Log("PASS: oito melee com destinos distribuidos.");

        Fixture(Vector3.zero);
        var rogue = Actor(new Vector3(-12, 0, 0), EnemyAI.Comportamento.Flanqueador);
        frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        Bind();
        end = Time.time + 1.6f;
        while (Time.time < end) yield return null;
        Assert(Mathf.Abs(rogue.transform.position.z - Origin.z) > 1, "Rogue precisa aproximar lateralmente.");
        end = Time.time + 4;
        while (Time.time < end) yield return null;
        Assert(Distance(rogue) < 2, "Flanco deve terminar em alcance de ataque.");
        Debug.Log("PASS: flanco e aproximacao final.");

        Fixture(Vector3.zero);
        var ranged = Actor(new Vector3(14, 0, 0), EnemyAI.Comportamento.Distante);
        frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        Bind();
        end = Time.time + 5;
        while (Time.time < end) yield return null;
        Assert(Distance(ranged) >= 5.7f && Distance(ranged) <= 9.5f, "Mage deve manter sua faixa ideal.");
        target.position = ranged.transform.position - Vector3.right * 2;
        Physics.SyncTransforms();
        end = Time.time + 2.5f;
        while (Time.time < end) yield return null;
        Assert(Distance(ranged) > 5, "Mage precisa recuar quando o jogador se aproxima.");
        Debug.Log("PASS: distancia e recuo ranged.");

        Fixture(new Vector3(17, 0, 0));
        ranged = Actor(new Vector3(18.5f, 0, 0), EnemyAI.Comportamento.Distante);
        frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        Bind();
        end = Time.time + 4;
        while (Time.time < end) yield return null;
        Assert(Distance(ranged) > 5 && ranged.GetComponent<NavMeshAgent>().isOnNavMesh, "Recuo na borda deve buscar alternativa dentro da arena.");
        Debug.Log("PASS: recuo proximo da borda.");

        Fixture(new Vector3(8, 0, 0), true);
        ranged = Actor(new Vector3(-8, 0, 0), EnemyAI.Comportamento.Distante);
        frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        Bind();
        end = Time.time + 12;
        while (Time.time < end) yield return null;
        Assert((bool)typeof(EnemyAI).GetMethod("LinhaLivre", Private).Invoke(ranged, null),
            $"Mage precisa buscar linha de tiro contornando a cobertura. pos={ranged.transform.position - Origin}, destino={ranged.DestinoAtual - Origin}, estado={ranged.EstadoAtual}, restante={ranged.GetComponent<NavMeshAgent>().remainingDistance}, caminhos={ranged.CaminhosCalculados}");
        Debug.Log("PASS: busca de linha de tiro com obstaculo.");

        Fixture(Vector3.zero);
        for (int i = 0; i < 25; i++)
            Actor(Quaternion.Euler(0, i * 360f / 25, 0) * Vector3.forward * 15,
                i % 3 == 0 ? EnemyAI.Comportamento.Guerreiro : i % 3 == 1 ? EnemyAI.Comportamento.Flanqueador : EnemyAI.Comportamento.Distante);
        frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        Bind();
        float start = Time.time;
        end = Time.time + 10;
        while (Time.time < end) yield return null;
        CheckBodies();
        Assert(actors.All(a => a.GetComponent<NavMeshAgent>().isOnNavMesh), "Todos os 25 agentes devem permanecer no NavMesh.");
        Assert(actors.Select(a => a.DestinoAtual).Distinct().Count() >= 20, "Grupo misto precisa distribuir destinos.");
        target.position += new Vector3(8, 0, -4);
        Physics.SyncTransforms();
        end = Time.time + 4;
        while (Time.time < end) yield return null;
        Assert(actors.All(a => a.GetComponent<NavMeshAgent>().isOnNavMesh), "Dash do alvo nao pode tirar os agentes da malha.");
        int decisions = actors.Sum(a => a.Decisoes), paths = actors.Sum(a => a.CaminhosCalculados);
        Assert(decisions <= actors.Count * ((Time.time - start) / 0.15f + 5), "Decisoes devem respeitar o intervalo.");
        Assert(paths <= decisions * 16, "Calculo de caminhos precisa ter orcamento limitado.");
        actors[0].GetComponent<Vida>().ReceberDano(1000);
        Assert(!actors[0].GetComponent<NavMeshAgent>().enabled && !actors[0].GetComponent<CharacterController>().enabled, "Morto deve parar de mover e bloquear.");
        var group = (List<EnemyAI>)typeof(EnemyAI).GetField("grupo", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Assert(!group.Contains(actors[0]), "Morto deve sair imediatamente da separacao/reservas.");
        Debug.Log($"PASS: 25 agentes mistos; {decisions} decisoes, {paths} caminhos em {Time.time - start:F1}s simulados; dash e morte.");
    }
    static void CheckBodies()
    {
        for (int i = 0; i < actors.Count; i++)
            for (int j = i + 1; j < actors.Count; j++)
                Assert(Vector3.ProjectOnPlane(actors[i].transform.position - actors[j].transform.position, Vector3.up).magnitude > 0.55f,
                    "Inimigos nao devem permanecer sobrepostos.");
    }
    static string Diagnostics(EnemyAI ai)
    {
        var agent = ai.GetComponent<NavMeshAgent>();
        return $"pos={ai.transform.position - Origin}, destino={ai.DestinoAtual - Origin}, estado={ai.EstadoAtual}, malha={agent.isOnNavMesh}, restante={agent.remainingDistance}, path={agent.pathStatus}, pending={agent.pathPending}, velocidade={agent.desiredVelocity}, decisoes={ai.Decisoes}, caminhos={ai.CaminhosCalculados}";
    }
    static void Set(EnemyAI ai, string field, object value) => typeof(EnemyAI).GetField(field, Private).SetValue(ai, value);
    static float Pending(EnemyAI ai) => (float)typeof(EnemyAI).GetField("momentoDoGolpe", Private).GetValue(ai);
}
