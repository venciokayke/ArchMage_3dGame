using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerCombatSelfCheck
{
    [MenuItem("Tools/Underdog/Check Combat Event (Edit Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Execute fora do Play Mode.");

        var player = new GameObject("Combat self-check");
        var cameraObject = new GameObject("Combat self-check camera");
        var prefab = new GameObject("Combat self-check projectile");
        prefab.AddComponent<Projectile>();
        var controller = new AnimatorController();
        controller.AddLayer("Base Layer");
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.layers[0].stateMachine.AddState("Ranged_Magic_Raise");
        try
        {
            var animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(10000f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var combat = player.AddComponent<PlayerCombat>();
            var staff = new GameObject("Combat self-check staff").transform;
            staff.SetParent(player.transform);
            staff.localPosition = new Vector3(0.4f, 1.5f, 0.3f);
            combat.cajado = staff;
            combat.spellSpawnPoint = staff;
            combat.projectilePrefab = prefab;
            Call(combat, "Start");
            Set(combat, "mainCamera", camera);
            combat.offsetRotacaoCajado = new Vector3(10f, 20f, 30f);
            Call(combat, "AtualizarDirecaoMira", new Vector3(3f, -10f, 4f));
            var aim = (Vector3)Get(combat, "direcaoMira");
            Check(Mathf.Abs(aim.y) < 0.00001f && Vector3.Distance(aim, new Vector3(0.6f, 0f, 0.8f)) < 0.00001f,
                "Mira deve ignorar altura e normalizar XZ.");
            Call(combat, "AtualizarDirecaoMira", new Vector3(-0.01f, -100f, -0.01f));
            Check(Vector3.Distance(aim, (Vector3)Get(combat, "direcaoMira")) < 0.00001f,
                "Zona morta deve preservar a direcao anterior.");
            Call(combat, "AtualizarDirecaoMira", Vector3.zero);
            Call(combat, "AplicarMira");
            Check(Vector3.Distance(player.transform.forward, aim) < 0.00001f, "Personagem deve usar a direcao da mira.");
            Check(Quaternion.Angle(staff.rotation, Quaternion.LookRotation(aim) * Quaternion.Euler(combat.offsetRotacaoCajado)) < 0.01f,
                "Cajado deve usar a mesma mira preservando o offset.");
            int before = CountClones(prefab.name);
            combat.LancarMagia();
            Check(CountClones(prefab.name) == before, "Evento sem clique nao pode disparar.");
            Call(combat, "Atacar");
            Check((bool)Get(combat, "ataquePendente"), "Primeiro clique deve ser aceito imediatamente.");
            Check(CountClones(prefab.name) == before, "Clique nao pode instanciar magia.");
            Set(combat, "miraValida", false);
            combat.LancarMagia();
            Check(CountClones(prefab.name) == before + 1, "Evento deve emitir uma magia.");
            foreach (var item in UnityEngine.Object.FindObjectsOfType<GameObject>())
                if (item.name == prefab.name + "(Clone)")
                {
                    Check(Vector3.Distance(item.transform.forward, aim) < 0.00001f,
                        "Projetil deve usar a direcao compartilhada mesmo se a mira ficar indisponivel apos aceitar.");
                    Check(item.GetComponent<Projectile>().dono == player.transform,
                        "Projetil deve identificar o conjurador para preservar as colisoes da main.");
                }
            combat.LancarMagia();
            Check(CountClones(prefab.name) == before + 1, "Evento duplicado nao pode emitir outra magia.");
            Call(combat, "Atacar");
            Check(!(bool)Get(combat, "ataquePendente"), "Cooldown deve impedir novo ataque.");
            Set(combat, "ultimoAtaque", float.NegativeInfinity);
            Set(combat, "miraValida", true);
            Call(combat, "Atacar");
            int secondAttack = (int)Get(combat, "ataqueAtual");
            Set(combat, "ultimoAtaque", float.NegativeInfinity);
            Call(combat, "Atacar");
            Check((int)Get(combat, "ataqueAtual") == secondAttack, "Clique com pendencia nao pode aceitar outro ataque.");
            combat.LancarMagia();
            combat.LancarMagia();
            Check(CountClones(prefab.name) == before + 2, "Segundo ataque aceito deve emitir exatamente uma magia.");
            CheckCancellation(combat, prefab.name, false);
            CheckCancellation(combat, prefab.name, true);
            Call(combat, "AtualizarDirecaoMira", new Vector3(3f, 0f, 4f));
            Call(combat, "Atacar");
            combat.LancarMagia();
            combat.LancarMagia();
            Check(CountClones(prefab.name) == before + 3, "Novo ataque apos reativar deve emitir exatamente uma magia.");
            Debug.Log("Underdog: mira XZ, zona morta, offset, evento, disparo unico, cooldown e cancelamento passaram.");
        }
        finally
        {
            foreach (var item in UnityEngine.Object.FindObjectsOfType<GameObject>())
                if (item.name == prefab.name + "(Clone)")
                    UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(controller);
        }
    }

    private static void CheckCancellation(PlayerCombat combat, string projectileName, bool deactivateObject)
    {
        Set(combat, "ataquePendente", true);
        Set(combat, "miraValida", true);
        int before = CountClones(projectileName);
        if (deactivateObject)
            combat.gameObject.SetActive(false);
        else
            combat.enabled = false;

        Check(!combat.isActiveAndEnabled, "Componente ou GameObject deve estar desativado.");
        combat.LancarMagia();
        Check(CountClones(projectileName) == before, "Desativado nao pode emitir magia, mesmo antes do callback.");

        // Em Edit Mode simulamos callbacks de runtime, como ja fazemos com Start.
        Call(combat, "OnDisable");
        Check(!(bool)Get(combat, "ataquePendente"), "OnDisable deve cancelar o ataque.");
        Check(!(bool)Get(combat, "miraValida"), "OnDisable deve invalidar a mira.");
        combat.LancarMagia();
        Check(CountClones(projectileName) == before, "Evento depois do cancelamento nao pode emitir magia.");

        if (deactivateObject)
            combat.gameObject.SetActive(true);
        else
            combat.enabled = true;

        Check(combat.isActiveAndEnabled, "Componente deve estar ativo novamente.");
        combat.LancarMagia();
        combat.LancarMagia();
        Check(CountClones(projectileName) == before, "Reativar nao pode recuperar o ataque cancelado, mesmo com eventos duplicados.");
    }

    private static int CountClones(string name)
    {
        int count = 0;
        foreach (var item in UnityEngine.Object.FindObjectsOfType<GameObject>())
            if (item.name == name + "(Clone)") count++;
        return count;
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
