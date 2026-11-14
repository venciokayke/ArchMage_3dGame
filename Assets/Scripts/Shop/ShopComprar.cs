using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopComprar : MonoBehaviour
{
    [SerializeField] private string nomeItem = "Item";

    private bool jogadorPerto = false;
    private GameObject jogador;

    private ItemEffect itemEffect;

    void Start()
    {
        itemEffect = GetComponent<ItemEffect>();
    }

    void Update()
    {
        if (jogadorPerto && Input.GetKeyDown(KeyCode.E))
        {
            Comprar();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            jogadorPerto = true;
            jogador = other.gameObject;

            Debug.Log("Pressione E para comprar " + nomeItem);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            jogadorPerto = false;
            jogador = null;
        }
    }

    private void Comprar()
    {
        if (jogador == null)
            return;

        Debug.Log("Comprou: " + nomeItem);

        if (itemEffect != null)
        {
            itemEffect.AplicarEfeito(jogador);
        }

        gameObject.SetActive(false);
    }
}