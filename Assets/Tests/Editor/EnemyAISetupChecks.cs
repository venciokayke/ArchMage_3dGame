using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyAISetupChecks
{
    [MenuItem("Tools/Underdog/Check Enemy AI Presets (Edit Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Execute em Edit Mode.");
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        string fixture = AssetDatabase.GenerateUniqueAssetPath("Assets/Generated/EnemyAI_Check");
        AssetDatabase.CreateFolder("Assets/Generated", System.IO.Path.GetFileName(fixture));
        string updated = null;
        try
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(fixture + "/Original.controller");
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.AddState("Locomotion");
            machine.defaultState = locomotion;
            var attack = machine.AddState("Attack");
            attack.speed = 1.3f;
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, fixture + "/Attack.anim");
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = 0.2f, functionName = "AplicarGolpe" } });
            attack.motion = clip;
            machine.AddState("Hit");
            machine.AddState("Death");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            var prefabs = new EnemyAI[3];
            for (int i = 0; i < prefabs.Length; i++)
            {
                var item = new GameObject("Enemy preset fixture");
                try
                {
                    var ai = item.AddComponent<EnemyAI>();
                    ai.usarNavMesh = true;
                    ai.usarAnimationEvent = true;
                    ai.ataqueDistancia = i == 2;
                    ai.dano = 10 + i;
                    ai.intervaloAtaque = 1.5f + i;
                    item.AddComponent<NavMeshAgent>().radius = 0.4f;
                    item.AddComponent<Animator>().runtimeAnimatorController = controller;
                    prefabs[i] = PrefabUtility.SaveAsPrefabAsset(item, fixture + "/Enemy_" + i + ".prefab").GetComponent<EnemyAI>();
                }
                finally { UnityEngine.Object.DestroyImmediate(item); }
            }
            string[] guids = prefabs.Select(p => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p))).ToArray();
            bool rejected = false;
            try { EnemyAISetup.Apply(new[] { prefabs[0], prefabs[0], prefabs[2] }); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "Mesmo prefab nao pode receber dois papeis.");
            updated = EnemyAISetup.Apply(prefabs);
            for (int i = 0; i < prefabs.Length; i++)
            {
                var ai = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i])).GetComponent<EnemyAI>();
                Assert(ai.comportamento == (EnemyAI.Comportamento)(i + 1), "Papel de IA deve corresponder ao slot do manager.");
                Assert(ai.GetComponent<Vida>().vidaMaxima == (i == 0 ? 175 : i == 1 ? 60 : 75), "HP deve seguir o preset.");
                Assert(Mathf.Approximately(ai.velocidade, i == 0 ? 2.5f : i == 1 ? 5.5f : 2.6f), "Velocidade deve seguir o preset.");
                Assert(ai.dano == 10 + i && ai.intervaloAtaque == 1.5f + i && ai.usarAnimationEvent, "Dano, cooldown e Animation Events devem ser preservados.");
                Assert(ai.GetComponent<NavMeshAgent>().speed == ai.velocidade && ai.distanciaMinima == 6, "Agent e faixa ranged devem ser configurados.");
                var copy = (AnimatorController)ai.GetComponent<Animator>().runtimeAnimatorController;
                Assert(copy != controller, "Controller original deve ser preservado.");
                var states = copy.layers[0].stateMachine.states;
                var move = states.Single(s => s.state.name == "Locomotion").state;
                Assert(move.speedParameterActive && move.speedParameter == "LocomotionRate", "Somente locomocao deve acompanhar velocidade real.");
                var copiedAttack = states.Single(s => s.state.name == "Attack").state;
                Assert(copiedAttack.motion == clip && copiedAttack.speed == 1.3f && !copiedAttack.speedParameterActive,
                    "Clip, evento e velocidade do ataque devem ser preservados.");
                Assert(states.Any(s => s.state.name == "Hit") && states.Any(s => s.state.name == "Death"), "Hit/Death devem continuar presentes.");
                var backup = AssetDatabase.LoadAssetAtPath<GameObject>(updated + "/Before_" + i + ".prefab");
                Assert(backup != null && backup.GetComponent<EnemyAI>().comportamento == EnemyAI.Comportamento.Automatico, "Backup deve preservar a versao anterior.");
            }
            Assert(!locomotion.speedParameterActive && !controller.parameters.Any(p => p.name == "LocomotionRate"), "Controller de origem nao pode ser alterado.");
            Assert(AnimationUtility.GetAnimationEvents(clip).Single().functionName == "AplicarGolpe", "Evento original deve permanecer intacto.");
            Debug.Log("PASS Enemy AI Presets: perfis, backup, GUIDs, Controller copiado, evento/ataque/Hit/Death preservados e prefab duplicado rejeitado.");
        }
        finally
        {
            if (updated != null) AssetDatabase.DeleteAsset(updated);
            AssetDatabase.DeleteAsset(fixture);
        }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
