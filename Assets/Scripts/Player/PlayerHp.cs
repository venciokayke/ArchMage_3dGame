using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHp : MonoBehaviour
{
    [SerializeField] private int vidaMaxima = 5;

    private int vidaAtual;
    private PlayerMovement playerMovement;

    void Start()
    {
        vidaAtual = vidaMaxima;
        playerMovement = GetComponent<PlayerMovement>();
    }

    public void ReceberDano(int dano)
    {
        vidaAtual -= dano;

        Debug.Log("HP: " + vidaAtual + "/" + vidaMaxima);

        if (vidaAtual <= 0)
        {
            Morrer();
        }
    }

    public void Curar(int quantidade)
    {
        vidaAtual += quantidade;

        if (vidaAtual > vidaMaxima)
        {
            vidaAtual = vidaMaxima;
        }

        Debug.Log("HP: " + vidaAtual + "/" + vidaMaxima);
    }

    public void AumentarVidaMaxima(int quantidade)
    {
        vidaMaxima += quantidade;
        vidaAtual += quantidade;

        Debug.Log("Vida máxima aumentou para: " + vidaMaxima);
    }

    private void Morrer()
    {
        vidaAtual = 0;

        Debug.Log("Você morreu!");

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        gameObject.SetActive(false);
    }
}