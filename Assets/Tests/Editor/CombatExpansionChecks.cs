using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class CombatExpansionChecks
{
    static IEnumerator checks;
    static GameObject root;
    static Transform player;
    static EnemyAI enemy;
    static NavMeshData data;
    static NavMeshDataInstance nav;
    static readonly Vector3 Origin = new Vector3(200, 0, 200);
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static bool Running => checks != null;
    public static bool Passed { get; private set; }
    [MenuItem("Tools/Underdog/Check Combat Expansion (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying || EditorApplication.isPaused || Running) { Debug.LogWarning("Execute em Play Mode sem pausa."); return; }
        Passed = false; checks = Suite(); EditorApplication.update += Tick;
    }
    static void Tick()
    {
        try
        {
            Assert(Application.isPlaying, "Play Mode interrompido.");
            if (checks.MoveNext()) return;
            Passed = true; Debug.Log("PASS Combat Expansion: pesado/empurrao, aviso/direcao fixa/dano unico/cancelamento, Mage movel/rajada e NavMesh.");
        }
        catch (Exception error) { Debug.LogException(error); }
        EditorApplication.update -= Tick; checks = null; Cleanup();
    }
    static void Cleanup()
    {
        if (enemy != null)
            foreach (var shot in UnityEngine.Object.FindObjectsOfType<Projectile>(true))
                if (shot.dono == enemy.transform) UnityEngine.Object.DestroyImmediate(shot.gameObject);
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        if (nav.valid) nav.Remove();
        if (data != null) UnityEngine.Object.DestroyImmediate(data);
    }
    static GameObject Item(string name, Vector3 position)
    {
        var item = new GameObject(name); item.transform.SetParent(root.transform); item.transform.position = Origin + position; return item;
    }
    static void Fixture(EnemyAI.Comportamento type, Vector3 start, Vector3 target, bool wall = false)
    {
        Cleanup(); root = new GameObject("Combat expansion checks (temporary)");
        var floor = Item("Floor", new Vector3(0,-0.1f,0)).AddComponent<BoxCollider>(); floor.size = new Vector3(40,0.2f,40);
        var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = floor.size, transform = floor.transform.localToWorldMatrix } };
        if (wall)
        {
            var block = Item("Wall", new Vector3(0,1.5f,0)).AddComponent<BoxCollider>(); block.size = new Vector3(1,3,8);
            sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = block.size, transform = block.transform.localToWorldMatrix, area = 1 });
        }
        var settings = NavMesh.GetSettingsByIndex(0); settings.agentRadius = 0.45f; settings.agentHeight = 2;
        settings.overrideVoxelSize = true; settings.voxelSize = 0.1f;
        data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Origin, new Vector3(44,8,44)), Vector3.zero, Quaternion.identity);
        Assert(data != null, "NavMesh de teste ausente."); nav = NavMesh.AddNavMeshData(data);
        var targetObject = Item("Target", target + Vector3.up * 0.05f);
        targetObject.AddComponent<Vida>(); targetObject.AddComponent<PlayerHp>(); targetObject.AddComponent<PlayerMovement>();
        var body = targetObject.GetComponent<CharacterController>(); body.height=2; body.radius=0.4f; body.center=Vector3.up;
        player = targetObject.transform;
        var actor = Item("Enemy", start + Vector3.up * 0.05f); actor.SetActive(false);
        enemy = actor.AddComponent<EnemyAI>(); enemy.comportamento=type; enemy.ataqueDistancia=type==EnemyAI.Comportamento.Distante;
        enemy.usarNavMesh=true; enemy.usarAnimationEvent=false; enemy.velocidade=2.5f; enemy.dano=0; enemy.raioDeteccao=enemy.raioPerderAlvo=70;
        enemy.alcanceAtaque=enemy.ataqueDistancia ? 8 : 1.5f; enemy.distanciaMinima=6; enemy.ConfigurarOnda(5);
        var cc=actor.GetComponent<CharacterController>(); cc.height=2; cc.radius=0.4f; cc.center=Vector3.up;
        var agent=actor.AddComponent<NavMeshAgent>(); agent.radius=0.4f; agent.height=2; agent.acceleration=18;
        actor.SetActive(true); Physics.SyncTransforms();
    }
    static void Bind()
    {
        Set(enemy,"alvo",player); Set(enemy,"vidaAlvo",player.GetComponent<Vida>()); Set(enemy,"hpAlvo",player.GetComponent<PlayerHp>());
        player.GetComponent<Vida>().invulnerabilidade=0;
        Set(enemy.GetComponent<EnemySpecialAttack>(),"nextUse",-1f);
    }
    static IEnumerator Suite()
    {
        Fixture(EnemyAI.Comportamento.Guerreiro,new Vector3(-2.5f,0,0),Vector3.zero);
        int frame=Time.frameCount; while(Time.frameCount<=frame+1) yield return null; Bind();
        var skill=enemy.GetComponent<EnemySpecialAttack>();
        float end=Time.time+0.4f; while(!skill.Preparando && Time.time<end) yield return null;
        Assert(skill.Preparando,"Guerreiro precisa preparar golpe pesado.");
        Assert(player.GetComponent<Vida>().VidaAtual==100,"Aviso nao pode causar dano antes da execucao.");
        Vector3 before=player.position; end=Time.time+1.2f; while(Time.time<end) yield return null;
        Assert(player.GetComponent<Vida>().VidaAtual==75,"Pesado deve atingir uma vez por 25.");
        Assert(Vector3.Distance(before,player.position)>0.2f,"Pesado deve empurrar o jogador.");
        Debug.Log("PASS: pesado lento e empurrao.");

        Fixture(EnemyAI.Comportamento.Flanqueador,new Vector3(-3,0,0),Vector3.zero);
        frame=Time.frameCount; while(Time.frameCount<=frame+1) yield return null; Bind(); skill=enemy.GetComponent<EnemySpecialAttack>();
        end=Time.time+1; while(!skill.Preparando && Time.time<end) yield return null;
        Assert(skill.Preparando,"Rogue precisa mostrar preparo.");
        var warning=(GameObject)Get(skill,"warning"); Assert(warning!=null && warning.activeSelf,"Aviso de trajetoria ausente.");
        end=Time.time+1.3f; while(Time.time<end) yield return null;
        Assert(player.GetComponent<Vida>().VidaAtual==85,"Investida deve causar 15 uma unica vez.");
        Assert(!warning.activeSelf,"Aviso deve desaparecer ao executar.");
        Debug.Log("PASS: aviso de Rogue e dano unico.");

        Fixture(EnemyAI.Comportamento.Flanqueador,new Vector3(-4,0,0),Vector3.zero);
        frame=Time.frameCount; while(Time.frameCount<=frame+1) yield return null; Bind(); skill=enemy.GetComponent<EnemySpecialAttack>();
        end=Time.time+2; while(Get(skill,"fase").ToString()!="Investida" && Time.time<end) yield return null;
        Assert(Get(skill,"fase").ToString()=="Investida","Rogue precisa iniciar dash apos aviso.");
        Vector3 locked=(Vector3)Get(skill,"direction"); player.position+=Vector3.forward*4; Physics.SyncTransforms();
        end=Time.time+0.5f; while(Time.time<end) yield return null;
        Assert(Vector3.Angle(locked,(Vector3)Get(skill,"direction"))<0.1f,"Rogue nao pode corrigir direcao durante dash.");
        Assert(player.GetComponent<Vida>().VidaAtual==100,"Jogador fora da trajetoria deve desviar.");
        Debug.Log("PASS: direcao travada e esquiva.");

        Fixture(EnemyAI.Comportamento.Flanqueador,new Vector3(-4,0,0),Vector3.zero);
        frame=Time.frameCount; while(Time.frameCount<=frame+1) yield return null; Bind(); skill=enemy.GetComponent<EnemySpecialAttack>();
        end=Time.time+2; while(!skill.Preparando && Time.time<end) yield return null;
        warning=(GameObject)Get(skill,"warning"); enemy.GetComponent<Vida>().ReceberDano(1000);
        Assert(!skill.Ativo && (warning==null || !warning.activeSelf),"Morte deve cancelar aviso e investida.");
        Debug.Log("PASS: morte cancela preparo.");

        Fixture(EnemyAI.Comportamento.Distante,new Vector3(6.5f,0,0),Vector3.zero);
        frame=Time.frameCount; while(Time.frameCount<=frame+1) yield return null; Bind(); skill=enemy.GetComponent<EnemySpecialAttack>(); skill.rajada.dano=19;
        var template=Item("Shot template",new Vector3(30,0,30)); template.SetActive(false); enemy.projetil=template.AddComponent<Projectile>();
        end=Time.time+2; while(!skill.Preparando && Time.time<end) yield return null;
        Assert(skill.Preparando,"Mage deve preparar rajada."); before=enemy.transform.position;
        end=Time.time+0.3f; while(Time.time<end) yield return null;
        Assert(Vector3.Distance(before,enemy.transform.position)>0.2f,"Mage deve reposicionar durante preparo.");
        end=Time.time+0.6f; while(Time.time<end) yield return null;
        var shots=UnityEngine.Object.FindObjectsOfType<Projectile>(true).Where(p=>p.dono==enemy.transform && p.dano==19).ToArray();
        Assert(shots.Length==3,"Rajada precisa de tres projeteis exatamente.");
        Assert(Vector3.Angle(shots[0].transform.forward,shots[2].transform.forward)>10,"Rajada precisa formar leque.");
        Debug.Log("PASS: Mage movel e leque de tres projeteis.");

        Fixture(EnemyAI.Comportamento.Guerreiro,new Vector3(-2,0,0),new Vector3(6,0,0),true);
        frame=Time.frameCount; while(Time.frameCount<=frame+1) yield return null; Bind(); enemy.ConfigurarOnda(1);
        enemy.Empurrar(Vector3.right,20,0.3f); end=Time.time+0.3f; while(Time.time<end) yield return null;
        Assert(enemy.transform.position.x<Origin.x-0.7f,"Empurrao nao pode atravessar parede.");
        Assert(enemy.GetComponent<NavMeshAgent>().isOnNavMesh,"Empurrao precisa manter inimigo na malha.");
        before=enemy.transform.position;
        end=Time.time+0.6f; while(Time.time<end) yield return null;
        Assert(!enemy.GetComponent<CombatDisplacement>().Ativo && enemy.GetComponent<NavMeshAgent>().enabled,"IA deve retomar apos empurrao.");
        Assert(Vector3.Distance(before,enemy.transform.position)>0.2f,"IA precisa voltar a contornar o obstaculo apos empurrao.");
        Debug.Log("PASS: empurrao respeita parede/NavMesh e termina.");

        Fixture(EnemyAI.Comportamento.Guerreiro,new Vector3(2,0,0),Vector3.zero);
        frame=Time.frameCount; while(Time.frameCount<=frame+1) yield return null; Bind(); enemy.ConfigurarOnda(1);
        var extra=new GameObject("Second collider");extra.transform.SetParent(enemy.transform,false);extra.AddComponent<SphereCollider>().center=Vector3.up;
        var second=UnityEngine.Object.Instantiate(enemy.gameObject,Origin+Vector3.forward*2+Vector3.up*0.05f,Quaternion.identity,root.transform).GetComponent<EnemyAI>();
        var caster=player.gameObject.AddComponent<PlayerCombat>(); caster.enabled=false;
        Set(caster,"vida",player.GetComponent<Vida>()); before=enemy.transform.position;
        Physics.SyncTransforms(); typeof(PlayerCombat).GetMethod("Explodir",Private).Invoke(caster,null);
        Assert(enemy.GetComponent<Vida>().VidaAtual==50 && second.GetComponent<Vida>().VidaAtual==50,"Explosao deve causar 50 a varios alvos, uma vez por Vida.");
        end=Time.time+0.25f; while(Time.time<end) yield return null;
        Assert(enemy.transform.position.x>before.x+0.5f && second.transform.position.z>Origin.z+2.5f,"Empurrao da explosao deve ser radial.");
        Assert(enemy.GetComponent<NavMeshAgent>().isOnNavMesh && second.GetComponent<NavMeshAgent>().isOnNavMesh,"Explosao precisa manter alvos no NavMesh.");
        Debug.Log("PASS: explosao 50, multiplos alvos, dano unico e empurrao radial.");
    }
    static object Get(object target,string name)=>target.GetType().GetField(name,Private).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    static void Assert(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
