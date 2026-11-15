using UnityEngine;

[RequireComponent(typeof(Vida))]
public class PlayerHp : MonoBehaviour
{
    [SerializeField] private int vidaMaxima = 100;
    [Min(0f)] public float invulnerabilidade = 0.6f;
    private Vida vida;
    private Animator animator;
    private int frameHit;
    public Vida Vida => vida != null ? vida : GetComponent<Vida>();

    void Start()
    {
        vida = GetComponent<Vida>();
        if (vida == null) vida = gameObject.AddComponent<Vida>();
        vida.DefinirMaximo(vidaMaxima, true);
        vida.invulnerabilidade = invulnerabilidade;
        animator = GetComponent<Animator>();
        vida.aoReceberDano.AddListener(Atingido);
        vida.aoMorrer.AddListener(Morrer);
    }

    public void ReceberDano(int dano) => Vida.ReceberDano(dano);
    public void Curar(int quantidade) => Vida.Curar(quantidade);
    public void AumentarVidaMaxima(int quantidade)
    {
        if (quantidade <= 0 || Vida.EstaMorto) return;
        vidaMaxima += quantidade;
        Vida.DefinirMaximo(vidaMaxima);
        Vida.Curar(quantidade);
    }

    void Atingido(float quantidade)
    {
        if (vida.EstaMorto || animator == null) return;
        int layer = animator.GetLayerIndex("Feedback");
        if (layer >= 0)
        {
            frameHit = Time.frameCount;
            animator.SetLayerWeight(layer, 1);
            animator.Play("Feedback.Hit", layer, 0);
        }
    }

    void LateUpdate()
    {
        if (animator == null || vida.EstaMorto || Time.frameCount <= frameHit) return;
        int layer = animator.GetLayerIndex("Feedback");
        if (layer < 0) return;
        bool hit = animator.GetCurrentAnimatorStateInfo(layer).IsName("Hit");
        animator.SetLayerWeight(layer, Mathf.MoveTowards(animator.GetLayerWeight(layer), hit ? 1 : 0, Time.deltaTime * 12));
    }

    void Morrer()
    {
        var movement = GetComponent<PlayerMovement>();
        var combat = GetComponent<PlayerCombat>();
        if (movement != null) movement.enabled = false;
        if (combat != null) combat.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        if (animator == null) return;
        for (int layer = 1; layer < animator.layerCount; layer++) animator.SetLayerWeight(layer, 0);
        int death = Animator.StringToHash("Base Layer.Death");
        if (animator.HasState(0, death)) animator.CrossFadeInFixedTime(death, 0.08f, 0);
    }

    void OnDestroy()
    {
        if (vida == null) return;
        vida.aoReceberDano.RemoveListener(Atingido);
        vida.aoMorrer.RemoveListener(Morrer);
    }
}
