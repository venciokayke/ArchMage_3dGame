using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class BuffChecks
{
    static IEnumerator checks;
    static GameObject root;
    static WaveManager manager;
    static PlayerHp player;
    static AnimatorController controller;
    static NavMeshData data;
    static NavMeshDataInstance nav;
    static readonly Vector3 Origin=new Vector3(300,0,300);
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static bool Running=>checks!=null;
    public static bool Passed{get;private set;}
    [MenuItem("Tools/Underdog/Check Buffs And Five Waves (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying||EditorApplication.isPaused||Running){Debug.LogWarning("Execute em Play Mode sem pausa.");return;}
        Passed=false;checks=Suite();EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        try
        {
            Assert(Application.isPlaying,"Play Mode interrompido.");
            if(checks.MoveNext())return;
            Passed=true;Debug.Log("PASS Buffs: seis efeitos/acumulos/limites, escolhas unicas, morte, 40 spawns, quatro selecoes e quinta onda/vitoria.");
        }
        catch(Exception error){Debug.LogException(error);}
        EditorApplication.update-=Tick;checks=null;Cleanup();
    }
    static void Cleanup()
    {
        if(root!=null)UnityEngine.Object.DestroyImmediate(root);
        if(controller!=null)UnityEngine.Object.DestroyImmediate(controller);
        if(nav.valid)nav.Remove();
        if(data!=null)UnityEngine.Object.DestroyImmediate(data);
    }
    static GameObject Item(string name,Vector3 point)
    {var item=new GameObject(name);item.transform.SetParent(root.transform);item.transform.position=Origin+point;return item;}
    static void Fixture()
    {
        Cleanup();root=new GameObject("Buff checks (temporary)");
        var floor=Item("Floor",new Vector3(0,-0.1f,0)).AddComponent<BoxCollider>();floor.size=new Vector3(40,0.2f,40);
        var sources=new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=floor.size,transform=floor.transform.localToWorldMatrix}};
        data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),sources,new Bounds(Origin,new Vector3(44,8,44)),Vector3.zero,Quaternion.identity);
        Assert(data!=null,"NavMesh de teste ausente.");nav=NavMesh.AddNavMeshData(data);
        var actor=Item("Player",Vector3.up*0.05f);actor.SetActive(false);
        actor.AddComponent<Vida>();player=actor.AddComponent<PlayerHp>();actor.AddComponent<PlayerMovement>();
        var body=actor.GetComponent<CharacterController>();body.height=2;body.radius=0.4f;body.center=Vector3.up;
        controller=new AnimatorController();controller.AddLayer("Base Layer");controller.layers[0].stateMachine.AddState("Ranged_Magic_Raise");
        actor.GetComponent<Animator>().runtimeAnimatorController=controller;
        var combat=actor.AddComponent<PlayerCombat>();combat.cajado=Item("Staff",Vector3.zero).transform;combat.spellSpawnPoint=combat.cajado;
        combat.projectilePrefab=Item("Shot template",Vector3.right*40);combat.projectilePrefab.SetActive(false);
        actor.SetActive(true);
        var template=Item("Enemy template",Vector3.right*30);template.SetActive(false);
        var ai=template.AddComponent<EnemyAI>();ai.velocidade=0;ai.dano=0;ai.tempoParaSumir=0.05f;
        var cc=template.GetComponent<CharacterController>();cc.height=2;cc.radius=0.4f;cc.center=Vector3.up;
        template.SetActive(true);
        var game=Item("Game",Vector3.zero);game.SetActive(false);manager=game.AddComponent<WaveManager>();manager.jogador=player;
        manager.basico=manager.rapido=manager.distante=ai;manager.intervaloOndas=0.05f;manager.intervaloSpawn=0.1f;
        manager.spawnPoints=new[]{Item("Spawn A",new Vector3(9,0,9)).transform,Item("Spawn B",new Vector3(-9,0,9)).transform,
            Item("Spawn C",new Vector3(9,0,-9)).transform,Item("Spawn D",new Vector3(-9,0,-9)).transform};
        game.SetActive(true);Physics.SyncTransforms();
    }
    static void Apply(PlayerBuffs buffs,TipoBuff type,int wave)
    {
        Assert(buffs.Oferecer(wave),"Oferta precisa ser criada.");
        var all=(DefinicaoBuff[])Get(buffs,"definitions");
        Set(buffs,"ofertas",new[]{all[(int)type],all[((int)type+1)%6],all[((int)type+2)%6]});
        Assert(buffs.Escolher(0),"Escolha valida deve aplicar.");
        Assert(!buffs.Escolher(0),"Escolha duplicada deve ser rejeitada.");
    }
    static IEnumerator Suite()
    {
        Fixture();int frame=Time.frameCount;while(Time.frameCount<=frame+1)yield return null;
        player.Vida.invulnerabilidade=0;
        player.Vida.aceitaDano=true;
        var buffs=manager.melhorias;buffs.Inicializar(player,manager);
        var combat=player.GetComponent<PlayerCombat>();var movement=player.GetComponent<PlayerMovement>();
        Apply(buffs,TipoBuff.PoderArcano,1);
        Assert(Mathf.Approximately(combat.danoPrimario,30)&&Mathf.Approximately(combat.explosao.dano,60),"Poder Arcano deve dar +20% a todas as magias.");
        Apply(buffs,TipoBuff.PoderArcano,2);
        Assert(Mathf.Approximately(combat.danoPrimario,35),"Acumulos devem somar sobre a base, sem reaplicar multiplicacao.");
        Apply(buffs,TipoBuff.ExplosaoAprimorada,3);
        Assert(Mathf.Approximately(combat.explosao.dano,84)&&Mathf.Abs(combat.explosao.empurrao-13.8f)<0.01f,"Explosao aprimorada precisa combinar dano e empurrao.");
        player.Vida.ReceberDano(50);Apply(buffs,TipoBuff.Vitalidade,4);
        Assert(player.Vida.vidaMaxima==125&&player.Vida.VidaAtual==75,"Vitalidade deve aumentar maximo e curar 25.");
        Apply(buffs,TipoBuff.Agilidade,5);Assert(Mathf.Approximately(movement.velocidade,5.5f),"Agilidade deve dar +10% de movimento.");
        Apply(buffs,TipoBuff.ConjuracaoRapida,6);Assert(Mathf.Abs(combat.cooldown-0.45f)<0.001f,"Conjuracao deve reduzir cooldown em 10%.");
        Apply(buffs,TipoBuff.Resistencia,7);float hp=player.Vida.VidaAtual;player.Vida.ReceberDano(20);
        Assert(Mathf.Abs(player.Vida.VidaAtual-hp+18)<0.01f,"Resistencia deve reduzir dano em 10%.");
        float power=combat.danoPrimario;float end=Time.time+0.5f;while(Time.time<end)yield return null;
        Assert(combat.danoPrimario==power,"Buffs nao podem multiplicar atributos em Update.");
        buffs.bonusDanoMaximo=0.5f;buffs.reducaoCooldownMaxima=0.2f;
        Apply(buffs,TipoBuff.PoderArcano,8);Assert(Mathf.Approximately(combat.danoPrimario,37.5f),"Limite de dano deve ser respeitado.");
        Apply(buffs,TipoBuff.ConjuracaoRapida,9);Apply(buffs,TipoBuff.ConjuracaoRapida,10);
        Assert(Mathf.Abs(combat.cooldown-0.4f)<0.001f,"Limite de reducao de cooldown deve ser respeitado.");
        Debug.Log("PASS: seis buffs, acumulos aditivos, limites, vitalidade e resistencia.");

        Fixture();frame=Time.frameCount;while(Time.frameCount<=frame+1)yield return null;
        manager.Iniciar();var seen=new HashSet<int>();var choices=new HashSet<int>();end=Time.time+40;
        while(manager.Situacao!=WaveManager.Estado.Vitoria&&Time.time<end)
        {
            var alive=(List<EnemyAI>)Get(manager,"vivos");
            foreach(var ai in alive.ToArray())
            {
                if(ai==null)continue;seen.Add(ai.GetInstanceID());
                Assert(ai.OndaAtual==manager.OndaAtual,"Spawn precisa receber sua onda sem mudar a ordem.");
                if((bool)Get(ai,"inicializado"))ai.GetComponent<Vida>().ReceberDano(1000);
            }
            if(manager.Situacao==WaveManager.Estado.EscolhendoBuff)
            {
                int wave=manager.OndaAtual;Assert(choices.Add(wave),"Selecao nao pode aparecer duas vezes para a mesma onda.");
                Assert(wave<5&&manager.Restantes==0&&!manager.Surgindo,"Escolha somente entre ondas, sem spawns.");
                Assert(manager.melhorias.Ofertas.Select(o=>o.tipo).Distinct().Count()==3,"Oferta precisa ter tres buffs diferentes.");
                float wait=Time.time+0.3f;while(Time.time<wait)yield return null;
                Assert(manager.OndaAtual==wave&&manager.Restantes==0,"Proxima onda nao pode avancar sem escolha.");
                int count=manager.melhorias.EscolhasAplicadas;
                manager.EscolherBuff(-1);manager.EscolherBuff(3);
                Assert(manager.Situacao==WaveManager.Estado.EscolhendoBuff&&manager.melhorias.EscolhasAplicadas==count,"Indice invalido deve ser rejeitado.");
                var ui=manager.GetComponentInChildren<BuffSelectionUI>();
                var buttons=(UnityEngine.UI.Button[])Get(ui,"buttons");buttons[0].onClick.Invoke();buttons[0].onClick.Invoke();
                Assert(manager.Situacao==WaveManager.Estado.Jogando&&manager.melhorias.EscolhasAplicadas==count+1,"Botao deve aplicar exatamente uma escolha.");
            }
            yield return null;
        }
        Assert(manager.Situacao==WaveManager.Estado.Vitoria&&manager.OndaAtual==5,"Quinta onda deve concluir em vitoria.");
        Assert(seen.Count==40&&choices.SetEquals(new[]{1,2,3,4})&&manager.melhorias.EscolhasAplicadas==4,"Quantidades aprovadas e quatro selecoes devem ser preservadas.");
        Debug.Log("PASS: 40 spawns, quatro pausas sem spawn, selecao unica por botao e quinta onda/vitoria.");

        Fixture();frame=Time.frameCount;while(Time.frameCount<=frame+1)yield return null;
        for(int i=0;i<5;i++)manager.ondas[i]=new WaveManager.Onda{basicos=0};manager.Iniciar();
        end=Time.time+2;while(manager.Situacao!=WaveManager.Estado.EscolhendoBuff&&Time.time<end)yield return null;
        Assert(manager.Situacao==WaveManager.Estado.EscolhendoBuff,"Precisa chegar a selecao.");player.Vida.ReceberDano(1000);
        Assert(manager.Situacao==WaveManager.Estado.Derrota&&!manager.melhorias.Pendente,"Morte deve cancelar selecao.");manager.EscolherBuff(0);
        Assert(manager.Situacao==WaveManager.Estado.Derrota&&manager.melhorias.EscolhasAplicadas==0,"Morto nao pode selecionar nem reiniciar ondas.");
        Debug.Log("PASS: morte durante selecao cancela e bloqueia escolha tardia.");
    }
    static object Get(object target,string name)=>target.GetType().GetField(name,Private).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    static void Assert(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
