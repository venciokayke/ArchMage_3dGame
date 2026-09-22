using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Vida))]
public class EnemyAI : MonoBehaviour
{
    private enum Estado { Parado, Perseguindo, Atacando, Morto }

    [Header("Movimento")]
    public float velocidade = 2.5f;
    public float velocidadeRotacao = 8f;
    public float gravidade = -9.81f;

    [Header("Detecção")]
    public float raioDeteccao = 10f;
    public float raioPerderAlvo = 15f;

    [Header("Ataque")]
    public float alcanceAtaque = 1.5f;
    public float dano = 10f;
    public float intervaloAtaque = 1.5f;
    // Tempo entre o início da animação de ataque e o golpe acertar.
    public float atrasoDoGolpe = 0.4f;

    [Header("Morte")]
    public float tempoParaSumir = 3f;

    private CharacterController controller;
    private Animator animator;
    private Vida vida;
    private Transform alvo;
    private Vida vidaAlvo;

    private Estado estado = Estado.Parado;
    private Vector3 velocidadeVertical;
    private float proximoAtaque;
    private float momentoDoGolpe = -1f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        vida = GetComponent<Vida>();

        vida.aoReceberDano.AddListener(AoReceberDano);
        vida.aoMorrer.AddListener(AoMorrer);

        EncontrarJogador();
    }

    void Update()
    {
        if (estado == Estado.Morto)
        {
            return;
        }

        if (alvo == null || (vidaAlvo != null && vidaAlvo.EstaMorto))
        {
            estado = Estado.Parado;
        }
        else
        {
            AtualizarEstado();
        }

        Vector3 direcao = Vector3.zero;

        switch (estado)
        {
            case Estado.Perseguindo:
                direcao = DirecaoParaAlvo();
                OlharPara(direcao);
                break;

            case Estado.Atacando:
                OlharPara(DirecaoParaAlvo());
                TentarAtacar();
                break;
        }

        AplicarGolpePendente();

        SetFloat("Speed", direcao.magnitude);

        controller.Move(direcao * velocidade * Time.deltaTime);
        AplicarGravidade();
    }

    private void EncontrarJogador()
    {
        GameObject jogador = GameObject.FindWithTag("Player");

        if (jogador == null)
        {
            PlayerMovement movimento = FindFirstObjectByType<PlayerMovement>();
            if (movimento != null)
            {
                jogador = movimento.gameObject;
            }
        }

        if (jogador != null)
        {
            alvo = jogador.transform;
            vidaAlvo = jogador.GetComponent<Vida>();
        }
    }

    private void AtualizarEstado()
    {
        float distancia = DistanciaHorizontal(alvo.position);

        if (distancia <= alcanceAtaque)
        {
            estado = Estado.Atacando;
        }
        else if (distancia <= raioDeteccao
            || (estado != Estado.Parado && distancia <= raioPerderAlvo))
        {
            estado = Estado.Perseguindo;
        }
        else
        {
            estado = Estado.Parado;
        }
    }

    private void TentarAtacar()
    {
        if (Time.time < proximoAtaque)
        {
            return;
        }

        proximoAtaque = Time.time + intervaloAtaque;
        momentoDoGolpe = Time.time + atrasoDoGolpe;
        SetTrigger("Attack");
    }

    private void AplicarGolpePendente()
    {
        if (momentoDoGolpe < 0f || Time.time < momentoDoGolpe)
        {
            return;
        }

        momentoDoGolpe = -1f;

        // Só acerta se o jogador ainda estiver no alcance quando o golpe desce.
        if (vidaAlvo != null && DistanciaHorizontal(alvo.position) <= alcanceAtaque * 1.2f)
        {
            vidaAlvo.ReceberDano(dano);
        }
    }

    private void AoReceberDano(float quantidade)
    {
        SetTrigger("Hit");

        // Leva dano de longe (magia): passa a perseguir mesmo fora do raio de detecção.
        if (estado == Estado.Parado && alvo != null)
        {
            estado = Estado.Perseguindo;
        }
    }

    private void AoMorrer()
    {
        estado = Estado.Morto;
        momentoDoGolpe = -1f;
        SetFloat("Speed", 0f);
        SetTrigger("Die");

        controller.enabled = false;
        Destroy(gameObject, tempoParaSumir);
    }

    private Vector3 DirecaoParaAlvo()
    {
        Vector3 direcao = alvo.position - transform.position;
        direcao.y = 0f;
        return direcao.normalized;
    }

    private void OlharPara(Vector3 direcao)
    {
        if (direcao == Vector3.zero)
        {
            return;
        }

        Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            rotacaoAlvo,
            velocidadeRotacao * Time.deltaTime
        );
    }

    private void AplicarGravidade()
    {
        if (controller.isGrounded && velocidadeVertical.y < 0)
        {
            velocidadeVertical.y = -2f;
        }

        velocidadeVertical.y += gravidade * Time.deltaTime;

        controller.Move(velocidadeVertical * Time.deltaTime);
    }

    private float DistanciaHorizontal(Vector3 posicao)
    {
        Vector3 diferenca = posicao - transform.position;
        diferenca.y = 0f;
        return diferenca.magnitude;
    }

    // Os parâmetros do Animator são opcionais: o inimigo funciona mesmo sem eles.
    private void SetFloat(string nome, float valor)
    {
        if (TemParametro(nome, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(nome, valor);
        }
    }

    private void SetTrigger(string nome)
    {
        if (TemParametro(nome, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(nome);
        }
    }

    private bool TemParametro(string nome, AnimatorControllerParameterType tipo)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parametro in animator.parameters)
        {
            if (parametro.name == nome && parametro.type == tipo)
            {
                return true;
            }
        }

        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, raioDeteccao);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceAtaque);
    }
}
