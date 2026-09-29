using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemEffect : MonoBehaviour
{
    public enum TipoEfeito
    {
        VidaMaxima,
        Cura,
        DanoExtra
    }

    [SerializeField] private TipoEfeito tipoEfeito;

    [SerializeField] private int quantidade = 1;

    public void AplicarEfeito(GameObject jogador)
    {
        PlayerHp health = jogador.GetComponent<PlayerHp>();

        switch (tipoEfeito)
        {
            case TipoEfeito.VidaMaxima:

                if (health != null)
                {
                    health.AumentarVidaMaxima(quantidade);
                    Debug.Log("Vida máxima aumentada em " + quantidade);
                }

                break;


            case TipoEfeito.Cura:

                if (health != null)
                {
                    health.Curar(quantidade);
                    Debug.Log("Jogador recuperou " + quantidade + " HP");
                }

                break;


            case TipoEfeito.DanoExtra:

                Debug.Log("Dano extra comprado! Efeito ainda não funcional.");

                break;
        }
    }
}