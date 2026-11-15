using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class CombatExpansionSetupChecks
{
    [MenuItem("Tools/Underdog/Check Combat Expansion Setup (Edit Mode)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Execute fora do Play Mode.");
        Check(UnityEngine.Object.FindFirstObjectByType<WaveManager>());
    }
    public static void Check(WaveManager manager)
    {
        Assert(manager!=null,"Abra a partida preparada.");
        Assert(manager.melhorias!=null&&manager.melhorias.catalogo.Where(b=>b!=null).Select(b=>b.tipo).Distinct().Count()==6,"Catalogo de seis buffs ausente.");
        var prefabs=new[]{manager.basico,manager.rapido,manager.distante};
        for(int i=0;i<3;i++)
        {
            var ai=prefabs[i];Assert(ai!=null,"Prefab de inimigo ausente.");
            var special=ai.GetComponent<EnemySpecialAttack>();
            Assert(special!=null&&ai.GetComponent<CombatDisplacement>()!=null,"Execute Prepare Combat Expansion nos prefabs.");
            Assert(ai.usarNavMesh&&ai.GetComponent<NavMeshAgent>()!=null,"Navegacao do inimigo ausente.");
            Assert(special.materialAviso!=null&&special.materialAviso.GetFloat("_Surface")==1,"Material de aviso transparente ausente.");
            var controller=ai.GetComponentInChildren<Animator>(true).runtimeAnimatorController as AnimatorController;
            Assert(controller!=null,"Controller de inimigo ausente.");
            var machine=controller.layers[i==2?ai.camadaAtaque:0].stateMachine;
            var attack=machine.states.Select(s=>s.state).Single(s=>s.name=="Attack");
            var clip=attack.motion as AnimationClip;
            Assert(clip!=null&&AnimationUtility.GetAnimationEvents(clip).Count(e=>e.functionName=="AplicarGolpe")==1,"Ataque basico precisa manter seu evento unico.");
            var preparation=machine.states.Select(s=>s.state).Single(s=>s.name=="Special");
            Assert(preparation.speedParameter=="SpecialRate"&&preparation.speedParameterActive,"Ritmo de preparo nao configurado.");
            Assert(AnimationUtility.GetAnimationEvents((AnimationClip)preparation.motion).Length==0,"Clip especial nao pode duplicar evento basico.");
            if(i==1)Assert(machine.states.Any(s=>s.state.name=="Dash"),"Animacao de investida do Rogue ausente.");
            if(i==2)
            {
                Assert(ai.camadaAtaque>0&&controller.layers[ai.camadaAtaque].avatarMask!=null,"Mascara superior do Mage ausente.");
                Assert(controller.layers[0].stateMachine.states.Single(s=>s.state.name=="Locomotion").state.motion is BlendTree,"Pernas do Mage precisam de locomocao independente.");
                Assert(special.projetilRajada!=null,"Prefab distinto da rajada ausente.");
            }
        }
        Debug.Log("PASS Combat Setup: catalogo, navegacao, aviso, ataques/eventos, preparos, Rogue Dash e Mage superior configurados.");
    }
    static void Assert(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
