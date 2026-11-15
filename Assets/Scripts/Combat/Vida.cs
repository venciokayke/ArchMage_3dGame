using UnityEngine;
using UnityEngine.Events;

public class Vida : MonoBehaviour
{
    [Min(1f)] public float vidaMaxima = 100f;
    [Min(0f)] public float invulnerabilidade;
    public UnityEvent<float> aoReceberDano = new UnityEvent<float>();
    public UnityEvent aoMorrer = new UnityEvent();
    public float VidaAtual { get; private set; }
    public bool EstaMorto { get; private set; }
    private float vulneravelEm = float.NegativeInfinity;
    [System.NonSerialized] public bool aceitaDano = true;

    void Awake() { VidaAtual = vidaMaxima; }

    public void ReceberDano(float quantidade)
    {
        if (!aceitaDano || EstaMorto || quantidade <= 0 || float.IsNaN(quantidade) || float.IsInfinity(quantidade) || Time.time < vulneravelEm) return;
        VidaAtual = Mathf.Max(0, VidaAtual - quantidade);
        vulneravelEm = Time.time + Mathf.Max(0, invulnerabilidade);
        bool morreu = VidaAtual <= 0;
        EstaMorto = morreu;
        aoReceberDano?.Invoke(quantidade);
        if (morreu) aoMorrer?.Invoke();
    }

    public void Curar(float quantidade)
    {
        if (!EstaMorto && quantidade > 0 && !float.IsInfinity(quantidade) && !float.IsNaN(quantidade))
            VidaAtual = Mathf.Min(vidaMaxima, VidaAtual + quantidade);
    }

    public void DefinirMaximo(float maximo, bool preencher = false)
    {
        vidaMaxima = Mathf.Max(1, maximo);
        if (!EstaMorto) VidaAtual = preencher ? vidaMaxima : Mathf.Min(VidaAtual, vidaMaxima);
    }
}
