using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float velocidade = 10f;
    public float tempoDeVida = 3f;

    void Start()
    {
        Destroy(gameObject, tempoDeVida);
    }

    void Update()
    {
        transform.Translate(
            Vector3.forward * velocidade * Time.deltaTime
        );
    }
}