using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpikeDamage : MonoBehaviour
{
    [SerializeField] private int dano = 5;

    private void OnTriggerEnter(Collider other)
    {
        PlayerHp playerHealth = other.GetComponent<PlayerHp>();

        if (playerHealth != null)
        {
            playerHealth.ReceberDano(dano);
        }
    }
}
