using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float velocidade = 18f;
    public float tempoDeVida = 3f;
    public float dano = 25f;
    public float raio = 0.15f;
    public bool atingirJogador;
    public GameObject impactoPrefab;
    [HideInInspector] public Transform dono;
    private bool consumido;
    private bool tinhaDono;
    private Vida vidaDono;

    public void Configurar(Transform origem, float damage, float speed, bool contraJogador = false)
    {
        dono = origem;
        dano = damage;
        velocidade = speed;
        atingirJogador = contraJogador;
        tinhaDono = origem != null;
        vidaDono = origem != null ? origem.GetComponent<Vida>() : null;
    }

    void Start()
    {
        tinhaDono = dono != null;
        vidaDono = dono != null ? dono.GetComponent<Vida>() : null;
        Destroy(gameObject, tempoDeVida);
    }

    bool Ignorar(Collider collider)
    {
        if (collider.transform.IsChildOf(transform) || (dono != null && collider.transform.IsChildOf(dono))) return true;
        var vida = collider.GetComponentInParent<Vida>();
        return vida != null && (vida.EstaMorto || (vida.GetComponent<PlayerHp>() != null) != atingirJogador);
    }

    void Update()
    {
        Avancar(Time.deltaTime);
    }

    public void Avancar(float deltaTime)
    {
        if (consumido) return;
        if (tinhaDono && (dono == null || (vidaDono != null && vidaDono.EstaMorto)))
        { Destroy(gameObject); return; }
        // SphereCast nao informa colliders que ja envolvem a origem.
        foreach (var collider in Physics.OverlapSphere(transform.position, raio, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!Ignorar(collider)) { Acertar(collider, transform.position); return; }
        float distancia = Mathf.Max(0, velocidade) * Mathf.Max(0, deltaTime);
        RaycastHit? proximo = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, raio, transform.forward, distancia,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!Ignorar(hit.collider) && (proximo == null || hit.distance < proximo.Value.distance)) proximo = hit;
        if (proximo != null) { Acertar(proximo.Value.collider, proximo.Value.point); return; }
        transform.position += transform.forward * distancia;
    }

    void Acertar(Collider alvo, Vector3 ponto)
    {
        if (consumido) return;
        consumido = true;
        var vida = alvo.GetComponentInParent<Vida>();
        if (vida != null) vida.ReceberDano(dano);
        if (impactoPrefab != null) Instantiate(impactoPrefab, ponto, Quaternion.identity);
        Destroy(gameObject);
    }
}
