using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Vida))]
public class EnemyAI : MonoBehaviour
{
    private enum Estado { Parado, Perseguindo, Aproximando, Reposicionando, Atacando, Recuando, Morto }

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
    public bool moverAoAtacar = true;
    public int camadaAtaque;
    public float pontoImpactoEspecial = 0.4f;
    public int OndaAtual { get; private set; } = 1;
    public Comportamento Tipo => Estilo;
    public Transform AlvoAtual => alvo;
    public bool Inativo => suspenso || (vida != null && vida.EstaMorto) || !isActiveAndEnabled;
    public bool AtaqueBasicoOcupado => momentoDoGolpe >= 0 || AnimacaoDeAtaque();
    CombatDisplacement displacement;
    EnemySpecialAttack special;
    bool PodeMoverAtacando => ataqueDistancia && moverAoAtacar;


    public enum Comportamento { Automatico, Guerreiro, Flanqueador, Distante }
    [Header("Posicionamento")]
    public Comportamento comportamento;
    [Min(0.1f)] public float intervaloDecisao = 0.35f;
    [Min(0.2f)] public float intervaloReposicionamento = 2.4f;
    [Min(0.1f)] public float espacamento = 0.9f;
    [Min(0.1f)] public float raioSeparacao = 1.2f;
    [Range(0, 2)] public float forcaSeparacao = 0.7f;
    [Min(0.1f)] public float distanciaIdeal = 7.5f;
    [Min(0.1f)] public float distanciaMaxima = 9f;
    [Min(0.1f)] public float margemDistancia = 0.8f;
    [Min(1f)] public float raioFlanco = 4f;
    [Range(15, 90)] public float anguloFlanco = 65;
    [Min(0.1f)] public float velocidadeReferenciaAnimacao = 2.5f;
    public Vector3 DestinoAtual => temDestino ? destinoTatico : transform.position;
    public string EstadoAtual => estado.ToString();
    public int Decisoes { get; private set; }
    public int CaminhosCalculados { get; private set; }

    // shortcut: busca linear no grupo; particionar espacialmente se houver centenas de agentes.
    static readonly List<EnemyAI> grupo = new List<EnemyAI>();
    static readonly float[] angulos = { 0, 45, -45, 90, -90, 135, -135, 180 };
    static int sequencia;
    NavMeshPath caminho;
    readonly Dictionary<string, AnimatorControllerParameterType> parametros = new Dictionary<string, AnimatorControllerParameterType>();
    int ordem;
    float proximaDecisao;
    float proximaManobra;
    Vector3 destinoTatico;
    Vector3 centroDestino;
    Vector3 separacao;
    bool temDestino;
    bool moverNav;
    bool flancoFeito;
    bool recuando;
    bool aproximando;
    bool inicializado;
    Comportamento Estilo => comportamento != Comportamento.Automatico ? comportamento :
        ataqueDistancia ? Comportamento.Distante : velocidade >= 3.5f ? Comportamento.Flanqueador : Comportamento.Guerreiro;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void LimparGrupo() { grupo.Clear(); sequencia = 0; }
    void OnEnable() { if (inicializado && usarNavMesh && !suspenso && !vida.EstaMorto && !grupo.Contains(this)) grupo.Add(this); }
    void OnDisable()
    {
        grupo.Remove(this); temDestino = false; momentoDoGolpe = -1;
        if (special != null) special.Cancelar();
        if (displacement != null) displacement.Limpar();
    }
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

    private bool suspenso;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        vida = GetComponent<Vida>();
        inicializado = true;
        ordem = sequencia++;
        proximaDecisao = Time.time + (ordem % 7) * 0.035f;
        proximaManobra = Time.time + intervaloReposicionamento;
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var parameter in animator.parameters) parametros[parameter.name] = parameter.type;
        agent = GetComponent<NavMeshAgent>();
        displacement = GetComponent<CombatDisplacement>();
        if (displacement == null) displacement = gameObject.AddComponent<CombatDisplacement>();
        special = GetComponent<EnemySpecialAttack>();
        if (special == null) special = gameObject.AddComponent<EnemySpecialAttack>();
        special.Inicializar(this);
        if (ataqueDistancia && animator != null)
        {
            int upper = animator.GetLayerIndex("Upper Body");
            if (upper > 0) camadaAtaque = upper;
        }
        if (usarNavMesh) caminho = new NavMeshPath();
        if (usarNavMesh && (agent == null || !agent.isOnNavMesh))
        {
            Debug.LogError("EnemyAI precisa nascer no NavMesh com NavMeshAgent.", this);
            grupo.Remove(this);
            temDestino = false;
            suspenso = true;
        }
        if (agent != null)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.speed = velocidade;
            agent.stoppingDistance = 0.12f;
            agent.autoRepath = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = (Estilo == Comportamento.Guerreiro ? 25 : Estilo == Comportamento.Flanqueador ? 45 : 65) + ordem % 20;
        }

        vida.aoReceberDano.AddListener(AoReceberDano);
        vida.aoMorrer.AddListener(AoMorrer);

        if (usarNavMesh && !suspenso && !grupo.Contains(this)) grupo.Add(this);
        EquiparArma();
        EncontrarJogador();
    }

    void Update()
    {
        if (estado == Estado.Morto || suspenso) return;
        Vector3 direcao = Vector3.zero;
        bool alvoValido = alvo != null && alvo.gameObject.activeInHierarchy && (vidaAlvo == null || !vidaAlvo.EstaMorto);
        bool pushed = displacement != null && displacement.Ativo;
        if (pushed) { momentoDoGolpe = -1; special.Cancelar(); }
        else if (alvoValido) special.Atualizar(Time.deltaTime);
        else special.Cancelar();
        bool forced = pushed || special.BloqueiaMovimento;
        Vector3 forcedStep = pushed ? displacement.ConsumirPasso(Time.deltaTime) : special.Passo;
        if (!alvoValido)
        {
            estado = Estado.Parado;
            temDestino = false;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }
        else if (forced)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }
        else if (usarNavMesh)
        {
            if (Time.time >= proximaDecisao)
            {
                DecidirNavegacao();
                proximaDecisao = Time.time + Mathf.Max(0.15f, intervaloDecisao);
            }
            bool golpe = momentoDoGolpe >= 0 || AnimacaoDeAtaque();
            bool mover = moverNav && (!golpe || PodeMoverAtacando) && agent.isOnNavMesh;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = !mover;
                if (mover)
                {
                    Vector3 velocity = Vector3.ClampMagnitude(agent.desiredVelocity + separacao * forcaSeparacao, velocidade);
                    Vector3 passo = velocity * Time.deltaTime;
                    if (NavMesh.Raycast(agent.nextPosition, agent.nextPosition + passo, out var limite, agent.areaMask))
                        passo = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(limite.position - agent.nextPosition, Vector3.up), passo.magnitude);
                    direcao = Time.deltaTime > 0 ? passo / (Mathf.Max(0.1f, velocidade) * Time.deltaTime) : Vector3.zero;
                }
            }
            if (estado == Estado.Atacando || golpe || Estilo == Comportamento.Distante) OlharPara(DirecaoParaAlvo());
            else if (direcao.sqrMagnitude > 0.01f) OlharPara(direcao);
            if (estado == Estado.Atacando || PodeMoverAtacando) TentarAtacar();
        }
        else
        {
            AtualizarEstado();
            if (estado == Estado.Perseguindo) { direcao = DirecaoParaAlvo(); OlharPara(direcao); }
            else if (estado == Estado.Atacando) { OlharPara(DirecaoParaAlvo()); TentarAtacar(); }
        }
        AplicarGolpePendente();
        Vector3 antes = transform.position;
        Vector3 step = forced ? forcedStep : direcao * velocidade * Time.deltaTime;
        if (usarNavMesh) step = CombatDisplacement.LimitarAoNavMesh(step, agent);
        controller.Move(step);
        AplicarGravidade();
        if (usarNavMesh && agent.isOnNavMesh) agent.nextPosition = transform.position;
        special.AposMover(antes, transform.position);
        float speed = Time.deltaTime > 0 ? Vector3.ProjectOnPlane(transform.position - antes, Vector3.up).magnitude / Time.deltaTime : 0;
        SetFloat("Speed", Mathf.Clamp01(speed / Mathf.Max(0.1f, velocidade)));
        SetFloat("LocomotionRate", speed > 0.1f ? Mathf.Clamp(speed / Mathf.Max(0.1f, velocidadeReferenciaAnimacao), 0.2f, 3) : 1);
        if (camadaAtaque > 0 && animator != null)
            animator.SetLayerWeight(camadaAtaque, Mathf.MoveTowards(animator.GetLayerWeight(camadaAtaque), AtaqueBasicoOcupado || special.Preparando ? 1 : 0, Time.deltaTime * 12));
    }

    void DecidirNavegacao()
    {
        Decisoes++;
        AtualizarSeparacao();
        if (!agent.isOnNavMesh) { moverNav = false; temDestino = false; return; }
        float distancia = DistanciaHorizontal(alvo.position);
        if (distancia > raioPerderAlvo || (estado == Estado.Parado && distancia > raioDeteccao))
        { estado = Estado.Parado; moverNav = false; temDestino = false; return; }
        // Mantem o destino reservado durante a animacao, sem reiniciar ou cancelar o golpe.
        if (!PodeMoverAtacando && (momentoDoGolpe >= 0 || AnimacaoDeAtaque())) { moverNav = false; return; }
        bool livre = LinhaLivre();
        if (Estilo == Comportamento.Distante) { DecidirDistante(distancia, livre); return; }
        if (distancia > raioFlanco + 5) flancoFeito = false;
        bool flanco = Estilo == Comportamento.Flanqueador && !flancoFeito && distancia > raioFlanco - 0.5f;
        if (flanco && temDestino && Vector3.Distance(transform.position, destinoTatico) < 0.7f) { flancoFeito = true; flanco = false; temDestino = false; }
        if (!flanco) flancoFeito = true;
        bool precisa = !temDestino || Vector3.Distance(centroDestino, alvo.position) > 0.65f ||
            Ocupado(destinoTatico, false) || Time.time >= proximaManobra || agent.pathStatus != NavMeshPathStatus.PathComplete ||
            (Vector3.Distance(destinoTatico, transform.position) < 0.2f && distancia > alcanceAtaque);
        if (precisa)
        {
            float angulo = flanco ? (ordem % 2 == 0 ? anguloFlanco : -anguloFlanco) : 0;
            EscolherDestino(flanco ? raioFlanco : Mathf.Max(0.5f, alcanceAtaque * 0.8f), angulo, !flanco, false);
        }
        bool chegou = temDestino && Vector3.ProjectOnPlane(destinoTatico - transform.position, Vector3.up).magnitude < 0.2f;
        estado = chegou && distancia <= alcanceAtaque && livre ? Estado.Atacando : flanco ? Estado.Reposicionando : Estado.Perseguindo;
        moverNav = !chegou && temDestino;
    }

    void DecidirDistante(float distancia, bool livre)
    {
        float minimo = Mathf.Max(1, distanciaMinima);
        float maximo = Mathf.Max(minimo + 1, distanciaMaxima);
        float ideal = Mathf.Clamp(distanciaIdeal, minimo + 0.4f, maximo - 0.4f);
        if (distancia < minimo) recuando = true;
        else if (distancia >= minimo + margemDistancia) recuando = false;
        if (distancia > maximo || distancia > alcanceAtaque) aproximando = true;
        else if (distancia <= ideal + 0.3f) aproximando = false;
        bool chegou = temDestino && Vector3.ProjectOnPlane(destinoTatico - transform.position, Vector3.up).magnitude < 0.6f;
        bool renovar = !temDestino || Vector3.Distance(centroDestino, alvo.position) > 0.65f || Ocupado(destinoTatico, false) ||
            agent.pathStatus != NavMeshPathStatus.PathComplete || (chegou && Time.time >= proximaManobra);
        if (recuando || aproximando || !livre || renovar)
        {
            if (renovar || (recuando && estado != Estado.Recuando) || (aproximando && estado != Estado.Aproximando) || (!livre && chegou))
            {
                float lateral = !recuando && !aproximando && livre ? (ordem % 2 == 0 ? 25 : -25) : 0;
                if (!EscolherDestino(ideal, lateral, false, true)) EscolherDestino(ideal, 45, false, false);
            }
            chegou = temDestino && Vector3.ProjectOnPlane(destinoTatico - transform.position, Vector3.up).magnitude < 0.6f;
            moverNav = temDestino && !chegou;
            estado = recuando ? Estado.Recuando : aproximando ? Estado.Aproximando : Estado.Reposicionando;
        }
        else moverNav = temDestino && !chegou;
        if (!moverNav && livre && distancia <= alcanceAtaque) estado = Estado.Atacando;
        if (!temDestino) moverNav = false;
    }

    bool EscolherDestino(float raio, float anguloBase, bool permitirEspera, bool exigirVisao)
    {
        Vector3 radial = Vector3.ProjectOnPlane(transform.position - alvo.position, Vector3.up).normalized;
        if (radial.sqrMagnitude < 0.01f) radial = Quaternion.Euler(0, ordem * 137.5f, 0) * Vector3.forward;
        int paths = 0;
        for (int anel = 0; anel < (permitirEspera ? 4 : 1) && paths < 8; anel++)
            foreach (float angulo in angulos)
            {
                Vector3 candidato = alvo.position + Quaternion.Euler(0, anguloBase + angulo, 0) * radial * (raio + anel * espacamento * 1.4f);
                if (Ocupado(candidato, true)) continue;
                if (!NavMesh.SamplePosition(candidato, out var hit, 0.6f, agent.areaMask) || Ocupado(hit.position, true)) continue;
                if (exigirVisao && !LinhaLivreDe(hit.position)) continue;
                if (paths >= 8) break;
                paths++;
                CaminhosCalculados++;
                if (!agent.CalculatePath(hit.position, caminho) || caminho.status != NavMeshPathStatus.PathComplete) continue;
                destinoTatico = hit.position;
                centroDestino = alvo.position;
                temDestino = true;
                agent.SetPath(caminho);
                float distancia = Vector3.Distance(transform.position, destinoTatico);
                proximaManobra = Time.time + Mathf.Max(intervaloReposicionamento, distancia / Mathf.Max(0.1f, velocidade) + 1);
                return true;
            }
        temDestino = false;
        proximaManobra = Time.time + Mathf.Max(0.5f, intervaloDecisao);
        return false;
    }

    bool Ocupado(Vector3 ponto, bool corpos)
    {
        float raio = Mathf.Max(0.82f, espacamento);
        foreach (var outro in grupo)
        {
            if (outro == this || outro == null || outro.vida == null || outro.vida.EstaMorto || outro.suspenso || outro.alvo != alvo) continue;
            if (outro.temDestino && Vector3.ProjectOnPlane(outro.destinoTatico - ponto, Vector3.up).sqrMagnitude < raio * raio) return true;
            if (corpos && Vector3.ProjectOnPlane(outro.transform.position - ponto, Vector3.up).sqrMagnitude < raio * raio) return true;
        }
        return false;
    }

    void AtualizarSeparacao()
    {
        separacao = Vector3.zero;
        foreach (var outro in grupo)
        {
            if (outro == this || outro == null || outro.vida == null || outro.vida.EstaMorto || outro.suspenso || outro.alvo != alvo) continue;
            Vector3 delta = Vector3.ProjectOnPlane(transform.position - outro.transform.position, Vector3.up);
            float sqr = delta.sqrMagnitude;
            if (sqr > raioSeparacao * raioSeparacao) continue;
            if (sqr < 0.001f) delta = Quaternion.Euler(0, ordem * 137.5f, 0) * Vector3.forward * 0.1f;
            separacao += delta / Mathf.Max(0.1f, sqr);
        }
        separacao = Vector3.ClampMagnitude(separacao, 1);
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
        if (special != null && special.Ativo) return;
        if (alvo == null || DistanciaHorizontal(alvo.position) > alcanceAtaque || !LinhaLivre() || Time.time < proximoAtaque || momentoDoGolpe >= 0 || AnimacaoDeAtaque())
        {
            return;
        }

        proximoAtaque = Time.time + intervaloAtaque;
        momentoDoGolpe = Time.time + atrasoDoGolpe;
        if (camadaAtaque > 0 && animator != null) animator.CrossFadeInFixedTime("Upper Body.Attack", 0.05f, camadaAtaque, 0);
        else SetTrigger("Attack");
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
        if (!vida.EstaMorto && momentoDoGolpe < 0 && !AnimacaoDeAtaque() && (special == null || !special.Ativo)) SetTrigger("Hit");

        // Leva dano de longe (magia): passa a perseguir mesmo fora do raio de detecção.
        if (estado == Estado.Parado && alvo != null)
        {
            estado = Estado.Perseguindo;
        }
    }

    private void AoMorrer()
    {
        grupo.Remove(this);
        temDestino = false;
        estado = Estado.Morto;
        momentoDoGolpe = -1f;
        if (special != null) special.Cancelar();
        if (displacement != null) displacement.Limpar();
        if (animator != null && camadaAtaque > 0) animator.SetLayerWeight(camadaAtaque, 0);
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
        return animator != null && animator.runtimeAnimatorController != null && (animator.GetCurrentAnimatorStateInfo(camadaAtaque).IsName("Attack") ||
            (animator.IsInTransition(camadaAtaque) && animator.GetNextAnimatorStateInfo(camadaAtaque).IsName("Attack")));
    }

    bool LinhaLivre() { return LinhaLivreDe(transform.position); }
    public bool TemLinhaDeVisao() { return LinhaLivre(); }
    public void ConfigurarOnda(int onda) { OndaAtual = Mathf.Max(1, onda); }
    public void Empurrar(Vector3 direction, float speed, float duration)
    {
        if (Inativo) return;
        if (displacement == null) displacement = GetComponent<CombatDisplacement>() ?? gameObject.AddComponent<CombatDisplacement>();
        momentoDoGolpe = -1;
        if (special != null) special.Cancelar();
        displacement.Aplicar(direction, speed, duration);
    }
    public void AnimarEspecial(float preparo = 0.65f)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        SetFloat("SpecialRate", Mathf.Max(0.05f, pontoImpactoEspecial / Mathf.Max(0.05f, preparo)));
        string state = camadaAtaque > 0 ? "Upper Body.Special" : "Base Layer.Special";
        if (animator.HasState(camadaAtaque, Animator.StringToHash(state))) animator.CrossFadeInFixedTime(state, 0.06f, camadaAtaque, 0);
        else if (camadaAtaque > 0) animator.CrossFadeInFixedTime("Upper Body.Attack", 0.06f, camadaAtaque, 0);
        else SetTrigger("Attack");
    }
    public void ConcluirAnimacaoEspecial(bool dash = false)
    {
        SetFloat("SpecialRate", 1.3f);
        if (dash && animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, Animator.StringToHash("Base Layer.Dash")))
            animator.CrossFadeInFixedTime("Base Layer.Dash", 0.035f, 0, 0);
    }

    bool LinhaLivreDe(Vector3 posicao)
    {
        if (alvo == null) return false;
        Vector3 origin = posicao + Vector3.up;
        Vector3 delta = alvo.position + Vector3.up - origin;
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<Vida>() == null) return false;
        return true;
    }

    public void Desarmar()
    {
        grupo.Remove(this);
        temDestino = false;
        suspenso = true;
        momentoDoGolpe = -1;
        if (special != null) special.Cancelar();
        if (displacement != null) displacement.Limpar();
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

        return parametros.TryGetValue(nome, out var encontrado) && encontrado == tipo;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, raioDeteccao);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceAtaque);
    }
}
