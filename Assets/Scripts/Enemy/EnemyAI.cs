using UnityEngine;
using UnityEngine.AI;

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
    public int dano = 1;
    public float intervaloAtaque = 1.5f;
    // Tempo entre o início da animação de ataque e o golpe acertar.
    public float atrasoDoGolpe = 0.4f;

    [Header("Arma")]
    public GameObject arma;
    public string ossoDaMao = "handslot.r";

    [Header("Morte")]
    public float tempoParaSumir = 3f;
    [Header("Sobrevivencia")]
    public bool usarNavMesh;
    public bool usarAnimationEvent;
    public bool ataqueDistancia;
    public float distanciaMinima = 4f;
    public Projectile projetil;
    public float velocidadeProjetil = 10f;

    private CharacterController controller;
    private Animator animator;
    private Vida vida;
    private Transform alvo;
    private Vida vidaAlvo;
    private PlayerHp hpAlvo;

    private Estado estado = Estado.Parado;
    private Vector3 velocidadeVertical;
    private float proximoAtaque;
    private float momentoDoGolpe = -1f;
    private NavMeshAgent agent;
    private float proximoCaminho;
    private bool suspenso;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        vida = GetComponent<Vida>();
        agent = GetComponent<NavMeshAgent>();
        if (usarNavMesh && (agent == null || !agent.isOnNavMesh))
        {
            Debug.LogError("EnemyAI precisa nascer no NavMesh com NavMeshAgent.", this);
            suspenso = true;
        }
        if (agent != null)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.speed = velocidade;
            agent.stoppingDistance = alcanceAtaque * 0.8f;
        }

        vida.aoReceberDano.AddListener(AoReceberDano);
        vida.aoMorrer.AddListener(AoMorrer);

        EquiparArma();
        EncontrarJogador();
    }

    void Update()
    {
        if (estado == Estado.Morto || suspenso)
        {
            return;
        }

        // O PlayerHp desativa o jogador quando ele morre.
        if (alvo == null || !alvo.gameObject.activeInHierarchy
            || (vidaAlvo != null && vidaAlvo.EstaMorto))
        {
            estado = Estado.Parado;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
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

        if (usarNavMesh && alvo != null && vidaAlvo != null && !vidaAlvo.EstaMorto && alvo.gameObject.activeInHierarchy)
        {
            bool recuar = ataqueDistancia && DistanciaHorizontal(alvo.position) < distanciaMinima;
            bool bloqueado = ataqueDistancia && !LinhaLivre();
            bool mover = estado == Estado.Perseguindo || recuar || bloqueado;
            if (agent.isOnNavMesh)
            {
                agent.stoppingDistance = recuar || bloqueado ? 0.2f : alcanceAtaque * 0.8f;
                agent.isStopped = !mover;
                if (mover && Time.time >= proximoCaminho)
                {
                    Vector3 destino = recuar ? transform.position - DirecaoParaAlvo() * 3 : alvo.position;
                    if (NavMesh.SamplePosition(destino, out var hit, 2, agent.areaMask)) agent.SetDestination(hit.position);
                    proximoCaminho = Time.time + 0.2f;
                }
                direcao = mover ? Vector3.ClampMagnitude(agent.desiredVelocity / Mathf.Max(0.1f, velocidade), 1) : Vector3.zero;
            }
            else direcao = Vector3.zero;
        }

        AplicarGolpePendente();

        SetFloat("Speed", direcao.magnitude);

        controller.Move(direcao * velocidade * Time.deltaTime);
        AplicarGravidade();
        if (usarNavMesh && agent.isOnNavMesh) agent.nextPosition = transform.position;
    }

    private void EquiparArma()
    {
        if (arma == null)
        {
            return;
        }

        Transform mao = ProcurarFilho(transform, ossoDaMao);
        if (mao == null)
        {
            Debug.LogWarning($"{name}: osso '{ossoDaMao}' não encontrado para equipar a arma.");
            return;
        }

        Instantiate(arma, mao, false);
    }

    private static Transform ProcurarFilho(Transform pai, string nome)
    {
        foreach (Transform filho in pai)
        {
            if (filho.name == nome)
            {
                return filho;
            }

            Transform encontrado = ProcurarFilho(filho, nome);
            if (encontrado != null)
            {
                return encontrado;
            }
        }

        return null;
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
            hpAlvo = jogador.GetComponent<PlayerHp>();
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
        if (Time.time < proximoAtaque || momentoDoGolpe >= 0 || AnimacaoDeAtaque() || (ataqueDistancia && !LinhaLivre()))
        {
            return;
        }

        proximoAtaque = Time.time + intervaloAtaque;
        momentoDoGolpe = Time.time + atrasoDoGolpe;
        SetTrigger("Attack");
    }

    private void AplicarGolpePendente()
    {
        if (alvo == null || !alvo.gameObject.activeInHierarchy || (vidaAlvo != null && vidaAlvo.EstaMorto))
        {
            momentoDoGolpe = -1f;
            return;
        }

        if (usarAnimationEvent || momentoDoGolpe < 0f || Time.time < momentoDoGolpe)
        {
            return;
        }

        ExecutarGolpe();
    }

    public void AplicarGolpe()
    {
        if (usarAnimationEvent) ExecutarGolpe();
    }

    private void ExecutarGolpe()
    {
        if (momentoDoGolpe < 0 || estado == Estado.Morto || suspenso) return;
        momentoDoGolpe = -1f;
        if (alvo == null || !alvo.gameObject.activeInHierarchy || (vidaAlvo != null && vidaAlvo.EstaMorto)) return;
        if (!LinhaLivre()) return;
        if (ataqueDistancia)
        {
            if (projetil == null) return;
            Vector3 direction = DirecaoParaAlvo();
            if (direction.sqrMagnitude < 0.001f) return;
            var shot = Instantiate(projetil, transform.position + Vector3.up * 1.1f + direction * 0.65f, Quaternion.LookRotation(direction));
            shot.Configurar(transform, dano, velocidadeProjetil, true);
            return;
        }

        // Só acerta se o jogador ainda estiver no alcance quando o golpe desce.
        if (DistanciaHorizontal(alvo.position) > alcanceAtaque * 1.2f)
        {
            return;
        }

        if (hpAlvo != null)
        {
            hpAlvo.ReceberDano(dano);
        }
        else if (vidaAlvo != null)
        {
            vidaAlvo.ReceberDano(dano);
        }
    }

    private void AoReceberDano(float quantidade)
    {
        if (!vida.EstaMorto && momentoDoGolpe < 0 && !AnimacaoDeAtaque()) SetTrigger("Hit");

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
        // O Hit do golpe fatal ainda está armado e tiraria o esqueleto da animação de morte.
        ResetTrigger("Hit");
        ResetTrigger("Attack");
        SetTrigger("Die");

        controller.enabled = false;
        if (agent != null) agent.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        Destroy(gameObject, tempoParaSumir);
    }

    bool AnimacaoDeAtaque()
    {
        return animator != null && (animator.GetCurrentAnimatorStateInfo(0).IsName("Attack") ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Attack")));
    }

    bool LinhaLivre()
    {
        if (alvo == null) return false;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 delta = alvo.position + Vector3.up - origin;
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<Vida>() == null) return false;
        return true;
    }

    public void Desarmar()
    {
        suspenso = true;
        momentoDoGolpe = -1;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        SetFloat("Speed", 0);
        ResetTrigger("Attack");
    }

    void OnDestroy()
    {
        if (vida == null) return;
        vida.aoReceberDano.RemoveListener(AoReceberDano);
        vida.aoMorrer.RemoveListener(AoMorrer);
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

    private void ResetTrigger(string nome)
    {
        if (TemParametro(nome, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(nome);
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
