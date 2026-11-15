using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class WaveManager : MonoBehaviour
{
    public enum Estado { Inicio, Jogando, Vitoria, Derrota }
    [System.Serializable]
    public class Onda
    {
        [Min(0)] public int basicos = 4;
        [Min(0)] public int rapidos;
        [Min(0)] public int distantes;
    }
    public Onda[] ondas = {
        new Onda { basicos = 4 }, new Onda { basicos = 4, rapidos = 2 },
        new Onda { basicos = 4, rapidos = 2, distantes = 2 },
        new Onda { basicos = 4, rapidos = 4, distantes = 2 },
        new Onda { basicos = 5, rapidos = 4, distantes = 3 }
    };
    public EnemyAI basico;
    public EnemyAI rapido;
    public EnemyAI distante;
    public PlayerHp jogador;
    public Transform[] spawnPoints;
    [Min(0)] public float intervaloOndas = 4;
    [Min(0.1f)] public float intervaloSpawn = 0.7f;
    [Min(1f)] public float distanciaSegura = 6;
    public Estado Situacao { get; private set; }
    public int OndaAtual { get; private set; }
    public float ProximaOndaEm { get; private set; }
    public int Restantes => vivos.Count;
    public bool Surgindo { get; private set; }
    private readonly List<EnemyAI> vivos = new List<EnemyAI>();
    private PlayerMovement movement;
    private PlayerCombat combat;

    void Awake()
    {
        if (jogador == null) { Debug.LogError("WaveManager sem jogador.", this); enabled = false; return; }
        movement = jogador.GetComponent<PlayerMovement>();
        combat = jogador.GetComponent<PlayerCombat>();
        Comandos(false);
        jogador.Vida.aceitaDano = false;
        jogador.Vida.aoMorrer.AddListener(Perder);
    }

    void Update()
    {
        vivos.RemoveAll(ai => ai == null || ai.GetComponent<Vida>().EstaMorto);
        if (Situacao == Estado.Inicio && Input.GetKeyDown(KeyCode.Return)) Iniciar();
        if ((Situacao == Estado.Derrota || Situacao == Estado.Vitoria) && Input.GetKeyDown(KeyCode.R)) Reiniciar();
    }

    void Comandos(bool habilitar)
    {
        if (movement != null) movement.enabled = habilitar;
        if (combat != null) combat.enabled = habilitar;
    }

    public void Iniciar()
    {
        if (!enabled || Situacao != Estado.Inicio) return;
        if (basico == null || rapido == null || distante == null || spawnPoints == null || spawnPoints.Length == 0 || ondas.Length == 0 ||
            NavMesh.CalculateTriangulation().vertices.Length == 0)
        { Debug.LogError("Partida nao preparada: prefabs, ondas, spawns ou NavMesh ausentes.", this); return; }
        Situacao = Estado.Jogando;
        jogador.Vida.aceitaDano = true;
        Comandos(true);
        StartCoroutine(Partida());
    }

    IEnumerator Partida()
    {
        for (int i = 0; i < ondas.Length; i++)
        {
            ProximaOndaEm = Time.time + Mathf.Max(0, intervaloOndas);
            yield return new WaitForSeconds(Mathf.Max(0, intervaloOndas));
            OndaAtual = i + 1;
            var wave = ondas[i];
            Surgindo = true;
            int total = Mathf.Max(0, wave.basicos) + Mathf.Max(0, wave.rapidos) + Mathf.Max(0, wave.distantes);
            for (int n = 0; n < total; n++)
            {
                var prefab = n < wave.basicos ? basico : n < wave.basicos + wave.rapidos ? rapido : distante;
                // Reavalia os pontos enquanto o jogador se move; nunca forca um spawn inseguro.
                while (!TentarSpawn(prefab))
                {
                    if (Situacao != Estado.Jogando) yield break;
                    yield return new WaitForSeconds(0.4f);
                }
                yield return new WaitForSeconds(Mathf.Max(0.1f, intervaloSpawn));
            }
            Surgindo = false;
            while (vivos.Count > 0) yield return null;
        }
        Encerrar(true);
    }

    bool TentarSpawn(EnemyAI prefab)
    {
        var path = new NavMeshPath();
        if (!NavMesh.SamplePosition(jogador.transform.position, out var target, 2, NavMesh.AllAreas)) return false;
        int start = Random.Range(0, spawnPoints.Length);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var point = spawnPoints[(start + i) % spawnPoints.Length];
            if (point == null || !NavMesh.SamplePosition(point.position, out var hit, 2, NavMesh.AllAreas)) continue;
            if (Vector3.Distance(hit.position, jogador.transform.position) < distanciaSegura) continue;
            Vector3 position = hit.position + Vector3.up * 0.06f;
            if (Physics.CheckCapsule(position + Vector3.up * 0.5f, position + Vector3.up * 1.5f, 0.43f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            if (!NavMesh.CalculatePath(hit.position, target.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            var instance = Instantiate(prefab, position, Quaternion.identity);
            vivos.Add(instance);
            return true;
        }
        return false;
    }

    void Perder() { Encerrar(false); }
    void Encerrar(bool venceu)
    {
        if (Situacao != Estado.Jogando) return;
        Situacao = venceu ? Estado.Vitoria : Estado.Derrota;
        Comandos(false);
        jogador.Vida.aceitaDano = false;
        Surgindo = false;
        foreach (var ai in vivos) if (ai != null) ai.Desarmar();
        StopAllCoroutines();
    }

    public void Reiniciar()
    {
        Time.timeScale = 1;
        int index = gameObject.scene.buildIndex;
        if (index < 0) { Debug.LogError("Cena fora dos Build Settings; execute Prepare Survival Game.", this); return; }
        SceneManager.LoadScene(index);
    }
    void OnDestroy()
    {
        if (jogador == null) return;
        var life = jogador.Vida;
        if (life != null) life.aoMorrer.RemoveListener(Perder);
    }
}
