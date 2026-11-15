using UnityEngine;

public class SurvivalHUD : MonoBehaviour
{
    public WaveManager partida;
    public UnityEngine.UI.Text status;
    public UnityEngine.UI.Text habilidades;
    public UnityEngine.UI.Slider vida;
    public GameObject inicio;
    public GameObject fim;
    public UnityEngine.UI.Text resultado;
    public UnityEngine.UI.Button iniciar;
    public UnityEngine.UI.Button reiniciar;
    private WaveManager.Estado anterior;

    void Start()
    {
        iniciar.onClick.AddListener(partida.Iniciar);
        reiniciar.onClick.AddListener(partida.Reiniciar);
    }
    void Update()
    {
        inicio.SetActive(partida.Situacao == WaveManager.Estado.Inicio);
        bool acabou = partida.Situacao == WaveManager.Estado.Vitoria || partida.Situacao == WaveManager.Estado.Derrota;
        fim.SetActive(acabou);
        if (partida.Situacao != anterior)
        {
            anterior = partida.Situacao;
            if (acabou && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(reiniciar.gameObject);
        }
        resultado.text = partida.Situacao == WaveManager.Estado.Vitoria ? $"VITORIA\n{partida.ondas.Length} ondas superadas" : "GAME OVER\nO calabouco venceu desta vez";
        var hp = partida.jogador.Vida;
        vida.maxValue = hp.vidaMaxima;
        vida.value = hp.VidaAtual;
        float espera = Mathf.Max(0, partida.ProximaOndaEm - Time.time);
        status.text = $"HP {Mathf.CeilToInt(hp.VidaAtual)} / {hp.vidaMaxima:0}     ONDA {partida.OndaAtual}/{partida.ondas.Length}     INIMIGOS {partida.Restantes}" +
            (partida.Surgindo ? " + chegando" : espera > 0 ? $"     Proxima em {espera:0.0}s" : "");
        var combat = partida.jogador.GetComponent<PlayerCombat>();
        habilidades.text = $"WASD  Mover     SHIFT  Dash\n{Skill("LMB  Projetil", combat.TempoRestante(0))}     {Skill("RMB  Explosao", combat.TempoRestante(1))}     {Skill("Q  Rajada", combat.TempoRestante(2))}";
    }
    static string Skill(string nome, float tempo) => tempo > 0 ? nome + $" [{tempo:0.0}s]" : nome + " [PRONTO]";
}
