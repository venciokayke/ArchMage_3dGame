using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class EnemyAISetup
{
    [MenuItem("Tools/Underdog/Update Enemy AI Presets")]
    public static void UpdatePresets()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Execute fora do Play Mode.");
            var managers = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<WaveManager>(true)).ToArray();
            if (managers.Length != 1) throw new InvalidOperationException("Abra a partida com um unico WaveManager.");
            var manager = managers[0];
            var prefabs = new[] { manager.basico, manager.rapido, manager.distante };
            string[] paths = Validate(prefabs);
            string details = "Atualizar somente os tres prefabs usados nesta partida:\n\n" +
                string.Join("\n", paths) + "\n\nWarrior: 175 HP / 2.5; Rogue: 60 HP / 5.5; Mage: 75 HP / 2.6, faixa 6-9.\n" +
                "Backup dos prefabs e copias dos Controllers serao criados. Ondas e cena nao serao regravadas.";
            if (!EditorUtility.DisplayDialog("Atualizar IA dos inimigos", details, "Criar backup e atualizar", "Cancelar")) return;
            string folder = Apply(prefabs);
            Debug.Log("IA atualizada. Backup e Controllers: " + folder + ". Entre em Play Mode para validar a arena.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(folder));
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    static string[] Validate(EnemyAI[] prefabs)
    {
        if (!Application.unityVersion.StartsWith("2022.3."))
            throw new InvalidOperationException("Este comando foi preparado para Unity 2022.3.");
        if (prefabs == null || prefabs.Length != 3 || prefabs.Any(p => p == null))
            throw new InvalidOperationException("Referencie os tres prefabs existentes no WaveManager.");
        string[] paths = prefabs.Select(p => AssetDatabase.GetAssetPath(p.gameObject)).ToArray();
        if (paths.Distinct().Count() != 3) throw new InvalidOperationException("Cada arquetipo precisa de seu proprio prefab.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && paths.Contains(stage.assetPath) && stage.scene.isDirty)
            throw new InvalidOperationException("Salve ou feche o Prefab Mode antes de atualizar a IA.");
        for (int i = 0; i < prefabs.Length; i++)
        {
            var ai = prefabs[i];
            if (!paths[i].StartsWith("Assets/", StringComparison.Ordinal) ||
                paths[i].Contains("/Assets_Externos/") || !paths[i].EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                !PrefabUtility.IsPartOfPrefabAsset(ai) || ai.transform.parent != null)
                throw new InvalidOperationException("Use prefabs de gameplay na raiz, fora dos assets originais: " + paths[i]);
            if (!ai.usarNavMesh || ai.GetComponent<NavMeshAgent>() == null || ai.GetComponent<Vida>() == null ||
                ai.GetComponent<CharacterController>() == null || ai.ataqueDistancia != (i == 2))
                throw new InvalidOperationException("Componentes ou papel de combate inesperados: " + paths[i]);
            var animator = ai.GetComponentInChildren<Animator>(true);
            var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            Locomotion(controller);
            if (ai.GetComponentsInChildren<Component>(true).Any(c => c != null && EditorUtility.IsDirty(c)) ||
                EditorUtility.IsDirty(ai.gameObject) ||
                AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).Any(EditorUtility.IsDirty))
                throw new InvalidOperationException("Salve as alteracoes pendentes do prefab/Controller antes de atualizar: " + paths[i]);
            if (controller.parameters.Any(p => p.name == "LocomotionRate" && p.type != AnimatorControllerParameterType.Float))
                throw new InvalidOperationException("Parametro LocomotionRate precisa ser Float: " + paths[i]);
        }
        return paths;
    }

    static AnimatorState Locomotion(AnimatorController controller)
    {
        if (controller == null || controller.layers.Length == 0)
            throw new InvalidOperationException("AnimatorController de inimigo ausente ou nao suportado.");
        var states = controller.layers[0].stateMachine.states.Where(s => s.state.name == "Locomotion").ToArray();
        if (states.Length != 1) throw new InvalidOperationException("Controller precisa preservar um estado Locomotion na Base Layer.");
        return states[0].state;
    }

    public static void ConfigureLocomotion(AnimatorController controller)
    {
        var state = Locomotion(controller);
        if (!controller.parameters.Any(p => p.name == "LocomotionRate"))
            controller.AddParameter(new AnimatorControllerParameter { name = "LocomotionRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        state.speedParameter = "LocomotionRate";
        state.speedParameterActive = true;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);
    }

    public static void ConfigureProfile(EnemyAI ai, EnemyAI.Comportamento style)
    {
        ai.comportamento = style;
        ai.velocidade = style == EnemyAI.Comportamento.Flanqueador ? 5.5f : style == EnemyAI.Comportamento.Distante ? 2.6f : 2.5f;
        ai.GetComponent<Vida>().vidaMaxima = style == EnemyAI.Comportamento.Guerreiro ? 175 : style == EnemyAI.Comportamento.Flanqueador ? 60 : 75;
        ai.intervaloDecisao = 0.35f;
        ai.intervaloReposicionamento = 2.4f;
        ai.espacamento = 0.9f;
        ai.raioSeparacao = 1.2f;
        ai.forcaSeparacao = 0.7f;
        ai.raioFlanco = 4;
        ai.anguloFlanco = 65;
        ai.distanciaMinima = 6;
        ai.distanciaIdeal = 7.5f;
        ai.distanciaMaxima = 9;
        ai.margemDistancia = 0.8f;
        ai.velocidadeReferenciaAnimacao = 2.5f;
        var agent = ai.GetComponent<NavMeshAgent>();
        agent.speed = ai.velocidade;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    public static string Apply(EnemyAI[] prefabs)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Execute fora do Play Mode.");
        string[] paths = Validate(prefabs);
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/Generated/EnemyAI_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        AssetDatabase.CreateFolder("Assets/Generated", Path.GetFileName(folder));
        string[] backups = new string[3];
        for (int i = 0; i < paths.Length; i++)
        {
            backups[i] = folder + "/Before_" + i + ".prefab";
            if (!AssetDatabase.CopyAsset(paths[i], backups[i])) throw new IOException("Falha no backup de " + paths[i]);
        }
        string[] labels = { "Warrior", "Rogue", "Mage" };
        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(paths[i]);
                try
                {
                    var ai = contents.GetComponent<EnemyAI>();
                    var animator = contents.GetComponentInChildren<Animator>(true);
                    string controllerPath = folder + "/" + labels[i] + ".controller";
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), controllerPath))
                        throw new IOException("Falha ao copiar Controller de " + paths[i]);
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                    ConfigureLocomotion(controller);
                    AssetDatabase.SaveAssetIfDirty(controller);
                    animator.runtimeAnimatorController = controller;
                    ConfigureProfile(ai, (EnemyAI.Comportamento)(i + 1));
                    if (PrefabUtility.SaveAsPrefabAsset(contents, paths[i]) == null)
                        throw new IOException("Falha ao salvar " + paths[i]);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
        }
        catch
        {
            // Restaura os bytes do backup mantendo os .meta/GUIDs originais.
            for (int i = 0; i < paths.Length; i++)
            {
                File.Copy(backups[i], paths[i], true);
                AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);
            }
            Debug.LogError("Atualizacao revertida. Backups preservados em " + folder);
            throw;
        }
        return folder;
    }
}
