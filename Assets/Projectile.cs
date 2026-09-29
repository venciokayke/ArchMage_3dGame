using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float velocidade = 10f;
    public float tempoDeVida = 3f;
    public float dano = 25f;
    public float raio = 0.15f;

    // Quem disparou: o projétil atravessa o próprio conjurador.
    [HideInInspector] public Transform dono;

    void Start()
    {
        Destroy(gameObject, tempoDeVida);
    }

    void Update()
    {
        float distancia = velocidade * Time.deltaTime;

        // SphereCast no trecho do frame: não depende de Rigidbody e não atravessa alvos em alta velocidade.
        RaycastHit[] acertos = Physics.SphereCastAll(
            transform.position,
            raio,
            transform.forward,
            distancia,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit? maisProximo = null;

        foreach (RaycastHit acerto in acertos)
        {
            Transform atingido = acerto.collider.transform;

            if (atingido.IsChildOf(transform) || (dono != null && atingido.IsChildOf(dono)))
            {
                continue;
            }

            if (maisProximo == null || acerto.distance < maisProximo.Value.distance)
            {
                maisProximo = acerto;
            }
        }

        if (maisProximo != null)
        {
            Acertar(maisProximo.Value.collider);
            return;
        }

        transform.Translate(Vector3.forward * distancia);
    }

    private void Acertar(Collider alvo)
    {
        Vida vida = alvo.GetComponentInParent<Vida>();

        if (vida != null)
        {
            vida.ReceberDano(dano);
        }

        Destroy(gameObject);
    }
}
