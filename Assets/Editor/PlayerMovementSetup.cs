using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerMovementSetup
{
    [MenuItem("Tools/Underdog/Update Player Locomotion")]
    public static void UpdateLocomotion()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Execute fora do Play Mode.");
            var players = UnityEngine.Object.FindObjectsOfType<PlayerMovement>().Where(p => p.gameObject.scene.IsValid()).ToArray();
            if (players.Length != 1) throw new InvalidOperationException("Abra a arena com um unico PlayerMovement ativo.");
            var controller = players[0].GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
            Validate(controller);
            string path = AssetDatabase.GetAssetPath(controller);
            if (!EditorUtility.DisplayDialog("Atualizar locomocao", "Criar backup e ajustar somente a taxa do estado Locomotion em:\n" + path +
                "\n\nOs clips direcionais, Dash, ataques, Hit e Death serao preservados. A cena nao sera regravada.", "Criar backup e atualizar", "Cancelar")) return;
            Debug.Log("Locomocao configurada; backup em " + Apply(controller) + ". Inicie novamente o Play Mode para testar.");
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    static AnimatorState State(AnimatorController controller)
    {
        if (controller == null || controller.layers.Length == 0) throw new InvalidOperationException("AnimatorController do Mago ausente ou nao suportado.");
        var states = controller.layers[0].stateMachine.states.Where(s => s.state.name == "Locomotion").ToArray();
        if (states.Length != 1) throw new InvalidOperationException("Estado Locomotion da Base Layer ausente.");
        if (controller.parameters.Any(p => p.name == "LocomotionRate" && p.type != AnimatorControllerParameterType.Float))
            throw new InvalidOperationException("LocomotionRate precisa ser Float.");
        return states[0].state;
    }
    static void Validate(AnimatorController controller)
    {
        State(controller);
        string path = AssetDatabase.GetAssetPath(controller);
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("/Assets_Externos/"))
            throw new InvalidOperationException("Use o Controller de gameplay; assets KayKit originais sao preservados.");
        if (AssetDatabase.LoadAllAssetsAtPath(path).Any(EditorUtility.IsDirty)) throw new InvalidOperationException("Salve as alteracoes do Animator antes de atualizar.");
    }

    public static void ConfigureAnimator(AnimatorController controller)
    {
        var state = State(controller);
        if (!controller.parameters.Any(p => p.name == "LocomotionRate"))
            controller.AddParameter(new AnimatorControllerParameter { name = "LocomotionRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        state.speedParameter = "LocomotionRate";
        state.speedParameterActive = true;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);
    }

    public static string Apply(AnimatorController controller)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !Application.unityVersion.StartsWith("2022.3."))
            throw new InvalidOperationException("Execute fora do Play Mode na Unity 2022.3.");
        Validate(controller);
        string source = AssetDatabase.GetAssetPath(controller);
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/Generated/PlayerMovement_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        AssetDatabase.CreateFolder("Assets/Generated", Path.GetFileName(folder));
        string backup = folder + "/Before.controller";
        if (!AssetDatabase.CopyAsset(source, backup)) throw new IOException("Falha no backup; nada foi alterado.");
        try { ConfigureAnimator(controller); AssetDatabase.SaveAssetIfDirty(controller); }
        catch
        {
            File.Copy(backup, source, true);
            AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceUpdate);
            throw;
        }
        return backup;
    }
}
