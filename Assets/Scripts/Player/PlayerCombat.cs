using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MagiaAdicional
{
    [Min(0f)] public float dano = 35f;
    [Min(0f)] public float cooldown = 4f;
    [Min(0.1f)] public float velocidade = 18f;
    [Min(0.1f)] public float raio = 4.5f;
    [Min(1)] public int quantidade = 3;
    [Min(0.01f)] public float intervalo = 0.1f;
    public GameObject projetil;
    public GameObject preparo;
    public GameObject efeito;
    public string estado = "Upper Body.Area";
    [Min(0)] public float empurrao = 12f;
    [Min(0)] public float duracaoEmpurrao = 0.3f;
}

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    public Transform cajado;
    public Vector3 offsetRotacaoCajado;
    public GameObject projectilePrefab;
    public Transform spellSpawnPoint;
    public float cooldown = 0.5f;
    public float danoPrimario = 25f;
    public float velocidadePrimario = 18f;
    public GameObject preparoPrimario;
    [Min(0f)] public float zonaMortaMira = 0.5f;
    public int camadaAtaque;
    public string estadoPrimario = "Base Layer.Ranged_Magic_Raise";
    public MagiaAdicional explosao = new MagiaAdicional { dano = 50 };
    public MagiaAdicional rajada = new MagiaAdicional { dano = 15, cooldown = 6, estado = "Upper Body.Burst" };

    private float ultimoAtaque = float.NegativeInfinity;
    private float ultimaExplosao = float.NegativeInfinity;
    private float ultimaRajada = float.NegativeInfinity;
    private Animator animator;
    private Vida vida;
    private Camera mainCamera;
    private Vector3 direcaoMira;
    private bool miraValida;
    private bool ataquePendente;
    private bool entrouNoAtaque;
    private bool executandoRajada;
    private int tipoPendente;
    private int estadoPendente;
    private int solicitacao;
    private int ataqueAtual;
    private GameObject preparoAtual;
    [SerializeField] private int versaoExplosao;
    private int frameHabilitado;
    void OnEnable() { frameHabilitado = Time.frameCount; }

    void Awake()
    {
        // Migra apenas o valor padrao antigo; balanceamentos personalizados permanecem.
        if (versaoExplosao == 0)
        {
            if (Mathf.Approximately(explosao.dano, 35)) explosao.dano = 50;
            versaoExplosao = 1;
        }
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        vida = GetComponent<Vida>();
        if (animator == null || animator.runtimeAnimatorController == null ||
            !animator.HasState(camadaAtaque, Animator.StringToHash(estadoPrimario)) ||
            cajado == null || projectilePrefab == null || spellSpawnPoint == null)
        { Debug.LogError("PlayerCombat: referencias ou estado primario invalidos.", this); enabled = false; return; }
        direcaoMira = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (direcaoMira.sqrMagnitude < 0.000001f) direcaoMira = Vector3.forward;
    }

    void Update()
    {
        AtualizarMira();
        if (Time.frameCount == frameHabilitado) return;
        if (Input.GetMouseButtonDown(0)) Atacar();
        if (Input.GetMouseButtonDown(1)) SolicitarAtaque(1);
        if (Input.GetKeyDown(KeyCode.Q)) SolicitarAtaque(2);
    }

    void LateUpdate()
    {
        AplicarMira();
        if (camadaAtaque > 0 && animator != null)
        {
            bool tocando = ataquePendente || animator.GetCurrentAnimatorStateInfo(camadaAtaque).fullPathHash == estadoPendente ||
                (animator.IsInTransition(camadaAtaque) && animator.GetNextAnimatorStateInfo(camadaAtaque).fullPathHash == estadoPendente);
            animator.SetLayerWeight(camadaAtaque, Mathf.MoveTowards(animator.GetLayerWeight(camadaAtaque), tocando ? 1 : 0, Time.deltaTime * 12));
        }
        if (!ataquePendente) return;
        if (!animator.isActiveAndEnabled || (vida != null && vida.EstaMorto)) { CancelarAtaque("inativo/morto"); return; }
        bool noAtaque = animator.GetCurrentAnimatorStateInfo(camadaAtaque).fullPathHash == estadoPendente ||
            (animator.IsInTransition(camadaAtaque) && animator.GetNextAnimatorStateInfo(camadaAtaque).fullPathHash == estadoPendente);
        if (noAtaque) entrouNoAtaque = true;
        else if (entrouNoAtaque)
        {
            Debug.LogError("Ataque aceito saiu da animacao sem LancarMagia.", this);
            CancelarAtaque("animacao interrompida/sem evento");
        }
    }

    void AtualizarMira()
    {
        miraValida = false;
        if (mainCamera == null || !mainCamera.isActiveAndEnabled) mainCamera = Camera.main;
        if (mainCamera == null) return;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (new Plane(Vector3.up, transform.position).Raycast(ray, out float distancia)) AtualizarDirecaoMira(ray.GetPoint(distancia));
    }

    void AtualizarDirecaoMira(Vector3 ponto)
    {
        Vector3 direcao = ponto - transform.position;
        direcao.y = 0;
        float raio = Mathf.Max(0, zonaMortaMira);
        if (direcao.sqrMagnitude > Mathf.Max(raio * raio, 0.000001f)) direcaoMira = direcao.normalized;
        miraValida = true;
        transform.rotation = Quaternion.LookRotation(direcaoMira);
    }

    void AplicarMira()
    {
        if (direcaoMira.sqrMagnitude < 0.000001f) return;
        var rotation = Quaternion.LookRotation(direcaoMira);
        transform.rotation = rotation;
        if (cajado != null) cajado.rotation = rotation * Quaternion.Euler(offsetRotacaoCajado);
    }

    void Atacar() { SolicitarAtaque(0); }

    public float TempoRestante(int tipo)
    {
        float ultimo = tipo == 0 ? ultimoAtaque : tipo == 1 ? ultimaExplosao : ultimaRajada;
        float intervalo = tipo == 0 ? cooldown : tipo == 1 ? explosao.cooldown : rajada.cooldown;
        return Mathf.Max(0, ultimo + intervalo - Time.time);
    }

    void SolicitarAtaque(int tipo)
    {
        solicitacao++;
        Diagnostico("SOLICITACAO " + solicitacao + " tipo=" + tipo);
        string estado = tipo == 0 ? estadoPrimario : tipo == 1 ? explosao.estado : rajada.estado;
        if (!isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled || (vida != null && vida.EstaMorto)) return;
        if (TempoRestante(tipo) > 0) { Diagnostico("REJEITADO cooldown"); return; }
        if (ataquePendente || executandoRajada) { Diagnostico("REJEITADO pendente"); return; }
        if (!miraValida || cajado == null || spellSpawnPoint == null || projectilePrefab == null) return;
        if (!animator.HasState(camadaAtaque, Animator.StringToHash(estado))) { Diagnostico("REJEITADO estado ausente"); return; }
        if (tipo == 2 && rajada.projetil == null) return;
        if (tipo == 0) ultimoAtaque = Time.time;
        else if (tipo == 1) ultimaExplosao = Time.time;
        else ultimaRajada = Time.time;
        tipoPendente = tipo;
        estadoPendente = Animator.StringToHash(estado);
        ataqueAtual = solicitacao;
        ataquePendente = true;
        entrouNoAtaque = false;
        if (camadaAtaque > 0) animator.SetLayerWeight(camadaAtaque, 1);
        animator.Play(estadoPendente, camadaAtaque, 0);
        var preparo = tipo == 0 ? preparoPrimario : tipo == 1 ? explosao.preparo : rajada.preparo;
        if (preparo != null)
        {
            preparoAtual = Instantiate(preparo, spellSpawnPoint.position, Quaternion.identity);
            preparoAtual.transform.SetParent(spellSpawnPoint, true);
        }
        Diagnostico("ACEITO " + ataqueAtual);
    }

    // Unico ponto de emissao: clipes gerados recebem exatamente um Animation Event.
    public void LancarMagia()
    {
        Diagnostico("EVENTO " + ataqueAtual);
        if (!isActiveAndEnabled || !ataquePendente) return;
        if (!animator.isActiveAndEnabled || (vida != null && vida.EstaMorto) || spellSpawnPoint == null || projectilePrefab == null)
        { CancelarAtaque("referencia/inativo"); return; }
        ataquePendente = false;
        if (preparoAtual != null) Destroy(preparoAtual);
        AplicarMira();
        if (tipoPendente == 0) Emitir(projectilePrefab, danoPrimario, velocidadePrimario, direcaoMira);
        else if (tipoPendente == 1) Explodir();
        else StartCoroutine(Rajada());
    }

    void Emitir(GameObject prefab, float damage, float speed, Vector3 direcao)
    {
        var item = Instantiate(prefab, spellSpawnPoint.position, Quaternion.LookRotation(direcao));
        if (item.TryGetComponent(out Projectile projectile)) projectile.Configurar(transform, damage, speed);
        Diagnostico("INSTANCIADO " + ataqueAtual);
    }

    void Explodir()
    {
        if (explosao.efeito != null)
        {
            var efeito = Instantiate(explosao.efeito, transform.position + Vector3.up * 0.15f, Quaternion.identity);
            efeito.transform.localScale *= explosao.raio;
            foreach (var particles in efeito.GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main;
                main.startSizeMultiplier *= 1.35f;
                var emission = particles.emission;
                for (int i = 0; i < emission.burstCount; i++)
                {
                    var burst = emission.GetBurst(i);
                    burst.count = new ParticleSystem.MinMaxCurve(burst.count.constantMax * 1.5f);
                    emission.SetBurst(i, burst);
                }
            }
        }
        var atingidos = new HashSet<Vida>();
        foreach (var collider in Physics.OverlapSphere(transform.position, explosao.raio, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var target = collider.GetComponentInParent<Vida>();
            if (target == null || target == vida || target.EstaMorto || target.GetComponent<PlayerHp>() != null || !atingidos.Add(target)) continue;
            Vector3 origem = transform.position + Vector3.up;
            Vector3 delta = target.transform.position + Vector3.up - origem;
            bool bloqueado = false;
            foreach (var hit in Physics.RaycastAll(origem, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<Vida>() == null) bloqueado = true;
            if (!bloqueado)
            {
                float before = target.VidaAtual;
                target.ReceberDano(explosao.dano);
                if (target.VidaAtual < before && !target.EstaMorto)
                    target.GetComponent<EnemyAI>()?.Empurrar(target.transform.position - transform.position, explosao.empurrao, explosao.duracaoEmpurrao);
            }
        }
    }

    IEnumerator Rajada()
    {
        executandoRajada = true;
        Vector3 direcao = direcaoMira;
        for (int i = 0; i < Mathf.Max(1, rajada.quantidade); i++)
        {
            if (!isActiveAndEnabled || (vida != null && vida.EstaMorto)) break;
            Emitir(rajada.projetil, rajada.dano, rajada.velocidade, direcao);
            if (i + 1 < rajada.quantidade) yield return new WaitForSeconds(Mathf.Max(0.01f, rajada.intervalo));
        }
        executandoRajada = false;
    }

    void CancelarAtaque(string motivo)
    {
        if (ataquePendente) Diagnostico("CANCELADO " + motivo);
        ataquePendente = false;
        entrouNoAtaque = false;
        executandoRajada = false;
        if (preparoAtual != null) Destroy(preparoAtual);
    }
    void OnDisable()
    {
        CancelarAtaque("desativado");
        StopAllCoroutines();
        if (animator != null && camadaAtaque > 0 && camadaAtaque < animator.layerCount) animator.SetLayerWeight(camadaAtaque, 0);
        miraValida = false;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    void Diagnostico(string mensagem) { Debug.Log($"[Combat] {Time.time:F3} {mensagem}", this); }
}
