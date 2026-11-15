using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public static class MatchStartChecks
{
    static IEnumerator checks;
    public static bool Running => checks != null;
    public static bool Passed { get; private set; }

    [MenuItem("Tools/Underdog/Check Match Start (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying || EditorApplication.isPaused || Running)
        { Debug.LogWarning("Entre em Play Mode sem pausa e aguarde a checagem anterior."); return; }
        var hud = UnityEngine.Object.FindFirstObjectByType<SurvivalHUD>();
        if (hud == null || hud.partida == null || hud.partida.Situacao != WaveManager.Estado.Inicio)
        { Debug.LogWarning("Execute na tela inicial da partida. A checagem clica em Iniciar e observa o primeiro spawn real."); return; }
        Passed = false;
        checks = Check(hud);
        EditorApplication.update += Tick;
    }
    static void Tick()
    {
        try
        {
            Assert(Application.isPlaying && !EditorApplication.isPaused, "Play Mode interrompido ou pausado durante a checagem.");
            if (checks.MoveNext()) return;
            Passed = true;
            Debug.Log("PASS Match Start: clique, painel fechado, comandos ativos e primeiro inimigo da cena real.");
        }
        catch (Exception error) { Debug.LogException(error); }
        EditorApplication.update -= Tick;
        checks = null;
    }
    static IEnumerator Check(SurvivalHUD hud)
    {
        var manager = hud.partida;
        Assert(manager.isActiveAndEnabled, "WaveManager precisa estar ativo.");
        Assert(hud.iniciar != null && hud.iniciar.IsActive() && hud.iniciar.IsInteractable(), "Botao Iniciar indisponivel.");
        Assert(EventSystem.current != null, "EventSystem ausente.");
        Assert(NavMesh.CalculateTriangulation().vertices.Length > 0, "NavMesh nao foi carregado.");
        Assert(manager.basico != null && manager.rapido != null && manager.distante != null, "Prefab de inimigo ausente.");
        // Simula perda de listener runtime durante recarga sem apagar outros callbacks do botao.
        hud.enabled = false;
        hud.iniciar.onClick.RemoveListener(manager.Iniciar);
        hud.enabled = true;
        ExecuteEvents.Execute(hud.iniciar.gameObject,
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        Assert(manager.Situacao == WaveManager.Estado.Jogando, "Evento do botao nao iniciou a partida; confira os listeners e os erros do WaveManager.");
        int frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        Assert(!hud.inicio.activeInHierarchy, "HUD manteve o painel inicial aberto.");
        Assert(manager.jogador.GetComponent<PlayerMovement>().enabled && manager.jogador.GetComponent<PlayerCombat>().enabled,
            "Comandos do jogador nao foram liberados.");
        float end = Time.time + Mathf.Max(0, manager.intervaloOndas) + 5;
        while (manager.Restantes == 0 && Time.time < end) yield return null;
        Assert(manager.Restantes > 0, "Partida iniciou, mas nenhum inimigo nasceu; confira os spawns, colisoes e caminhos no NavMesh.");
    }
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
