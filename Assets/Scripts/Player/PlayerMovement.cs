using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimento")]
    [Min(0)] public float velocidade = 5f;
    public float gravidade = -9.81f;
    public float velocidadeRotacao = 10f;
    [Min(0.1f)] public float aceleracao = 40f;
    [Min(0.1f)] public float desaceleracao = 55f;

    [Header("Corrida")]
    public KeyCode teclaCorrida = KeyCode.LeftControl;
    [Min(1)] public float multiplicadorCorrida = 1.4f;
    [Min(0)] public float staminaMaxima = 100f;
    [Min(0)] public float consumoStamina = 18f;
    [Min(0)] public float regeneracaoStamina = 25f;
    [Min(0)] public float atrasoRegeneracao = 0.75f;
    [Min(0)] public float recuperacaoMinima = 20f;

    [Header("Dash")]
    public float velocidadeDash = 12f;
    public float duracaoDash = 0.2f;
    public float cooldownDash = 1f;
    [Min(0.1f)] public float desaceleracaoFimDash = 120f;
    public GameObject efeitoDash;

    public float StaminaAtual => staminaAtual;
    public bool StaminaEsgotada => esgotado;
    public bool Correndo { get; private set; }
    public bool EstaDashando => estaDashando;
    public float VelocidadeAtual { get; private set; }
    private CharacterController controller;
    private Animator animator;
    private Vida vida;
    private bool movimentoDirecional;
    private bool parametroSpeed;
    private bool parametroRitmo;
    private bool estaDashando;
    private bool retornandoDash;
    private bool esgotado;
    private float tempoDashRestante;
    private float proximoDashDisponivel;
    private float staminaAtual;
    private float tempoAteRegenerar;
    private Vector3 direcaoDash;
    private Vector3 velocidadeHorizontal;
    private Vector3 velocidadeVertical;
    private CombatDisplacement displacement;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        vida = GetComponent<Vida>();
        displacement = GetComponent<CombatDisplacement>();
        staminaAtual = Mathf.Max(0, staminaMaxima);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            var parameters = animator.parameters;
            parametroSpeed = System.Array.Exists(parameters, p => p.name == "Speed" && p.type == AnimatorControllerParameterType.Float);
            parametroRitmo = System.Array.Exists(parameters, p => p.name == "LocomotionRate" && p.type == AnimatorControllerParameterType.Float);
            movimentoDirecional = System.Array.Exists(parameters, p => p.name == "MoveX") && System.Array.Exists(parameters, p => p.name == "MoveZ");
        }
    }

    void Update()
    {
        AtualizarMovimento(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            Input.GetKey(teclaCorrida), Input.GetKeyDown(KeyCode.LeftShift), Time.deltaTime);
    }

    void AtualizarMovimento(Vector2 entrada, bool pedirCorrida, bool pedirDash, float dt)
    {
        if (dt <= 0 || controller == null || !controller.enabled || !gameObject.activeInHierarchy || (vida != null && vida.EstaMorto)) return;
        entrada = Vector2.ClampMagnitude(entrada, 1);
        Vector3 direcao = new Vector3(entrada.x, 0, entrada.y);
        staminaAtual = Mathf.Clamp(staminaAtual, 0, Mathf.Max(0, staminaMaxima));
        if (pedirDash && !estaDashando && Time.time >= proximoDashDisponivel) IniciarDash(direcao);
        bool usouDash = estaDashando;
        bool sprint = pedirCorrida && direcao.sqrMagnitude > 0.0001f && !esgotado && staminaAtual > 0 && !usouDash;
        Vector3 antes = transform.position;
        Vector3 passo;
        if (usouDash)
        {
            velocidadeHorizontal = direcaoDash * Mathf.Max(0, velocidadeDash);
            passo = velocidadeHorizontal * Mathf.Min(dt, tempoDashRestante);
            tempoDashRestante = Mathf.Max(0, tempoDashRestante - dt);
            if (tempoDashRestante <= 0) { estaDashando = false; retornandoDash = true; }
        }
        else
        {
            Vector3 desejada = direcao * Mathf.Max(0, velocidade) * (sprint ? Mathf.Max(1, multiplicadorCorrida) : 1);
            float taxa = retornandoDash ? desaceleracaoFimDash : desejada.magnitude < velocidadeHorizontal.magnitude ? desaceleracao : aceleracao;
            velocidadeHorizontal = Vector3.MoveTowards(velocidadeHorizontal, desejada, Mathf.Max(0.1f, taxa) * dt);
            if ((velocidadeHorizontal - desejada).sqrMagnitude < 0.001f) retornandoDash = false;
            passo = velocidadeHorizontal * dt;
        }
        if (controller.isGrounded && velocidadeVertical.y < 0) velocidadeVertical.y = -2;
        velocidadeVertical.y += gravidade * dt;
        if (displacement != null) passo += displacement.ConsumirPasso(dt);
        controller.Move(passo + velocidadeVertical * dt);
        Vector3 real = Vector3.ProjectOnPlane(transform.position - antes, Vector3.up) / dt;
        VelocidadeAtual = real.magnitude;
        Correndo = sprint && VelocidadeAtual > 0.1f;
        AtualizarStamina(dt);
        AtualizarAnimacao(real, usouDash);
    }

    void AtualizarStamina(float dt)
    {
        if (Correndo)
        {
            staminaAtual = Mathf.Max(0, staminaAtual - Mathf.Max(0, consumoStamina) * dt);
            tempoAteRegenerar = Mathf.Max(0, atrasoRegeneracao);
            if (staminaAtual <= 0) { esgotado = true; Correndo = false; }
        }
        else
        {
            float tempoLivre = Mathf.Max(0, dt - tempoAteRegenerar);
            tempoAteRegenerar = Mathf.Max(0, tempoAteRegenerar - dt);
            staminaAtual = Mathf.Min(Mathf.Max(0, staminaMaxima), staminaAtual + Mathf.Max(0, regeneracaoStamina) * tempoLivre);
        }
        if (esgotado && staminaAtual >= Mathf.Clamp(recuperacaoMinima, 0, Mathf.Max(0, staminaMaxima))) esgotado = false;
    }

    void AtualizarAnimacao(Vector3 real, bool dash)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        float ratio = real.magnitude / Mathf.Max(0.1f, velocidade);
        if (parametroSpeed) animator.SetFloat("Speed", dash ? 1 : Mathf.Clamp01(ratio));
        if (parametroRitmo) animator.SetFloat("LocomotionRate", ratio > 0.02f ? Mathf.Clamp(ratio, 0.2f, 3) : 1);
        if (movimentoDirecional)
        {
            Vector3 local = transform.InverseTransformDirection(dash ? direcaoDash : Vector3.ClampMagnitude(real / Mathf.Max(0.1f, velocidade), 1));
            animator.SetFloat("MoveX", local.x);
            animator.SetFloat("MoveZ", local.z);
        }
    }

    void IniciarDash(Vector3 direcaoMovimento)
    {
        estaDashando = true;
        retornandoDash = false;
        tempoDashRestante = Mathf.Max(0.01f, duracaoDash);
        proximoDashDisponivel = Time.time + Mathf.Max(0, cooldownDash);
        direcaoDash = direcaoMovimento.sqrMagnitude > 0.0001f ? direcaoMovimento.normalized : transform.forward;
        int dash = Animator.StringToHash("Base Layer.Dash");
        if (animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, dash)) animator.CrossFadeInFixedTime(dash, 0.035f, 0);
        if (efeitoDash != null) Instantiate(efeitoDash, transform.position + Vector3.up * 0.3f, Quaternion.identity);
    }

    void OnDisable()
    {
        if (displacement != null) displacement.Limpar();
        estaDashando = retornandoDash = Correndo = false;
        velocidadeHorizontal = velocidadeVertical = Vector3.zero;
        VelocidadeAtual = 0;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        if (parametroSpeed) animator.SetFloat("Speed", 0);
        if (movimentoDirecional) { animator.SetFloat("MoveX", 0); animator.SetFloat("MoveZ", 0); }
    }
    public void Empurrar(Vector3 direction, float speed, float duration)
    {
        if (!isActiveAndEnabled || (vida != null && vida.EstaMorto)) return;
        if (displacement == null) displacement = GetComponent<CombatDisplacement>() ?? gameObject.AddComponent<CombatDisplacement>();
        displacement.Aplicar(direction, speed, duration);
    }
}
