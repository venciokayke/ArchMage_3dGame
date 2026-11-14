using UnityEngine;
using UnityEngine.Events;

// Componente de vida genérico: serve para o jogador e para os inimigos.
public class Vida : MonoBehaviour
{
    public float vidaMaxima = 100f;

    public UnityEvent<float> aoReceberDano = new UnityEvent<float>();
    public UnityEvent aoMorrer = new UnityEvent();

    public float VidaAtual { get; private set; }
    public bool EstaMorto { get; private set; }

    void Awake()
    {
        VidaAtual = vidaMaxima;
    }

    public void ReceberDano(float quantidade)
    {
        if (EstaMorto || quantidade <= 0f)
        {
            return;
        }

        VidaAtual = Mathf.Max(VidaAtual - quantidade, 0f);
        aoReceberDano?.Invoke(quantidade);

        if (VidaAtual <= 0f)
        {
            EstaMorto = true;
            aoMorrer?.Invoke();
        }
    }

    public void Curar(float quantidade)
    {
        if (EstaMorto || quantidade <= 0f)
        {
            return;
        }

        VidaAtual = Mathf.Min(VidaAtual + quantidade, vidaMaxima);
    }
}
