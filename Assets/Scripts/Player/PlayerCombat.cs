using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    public Transform cajado;
    public Vector3 offsetRotacaoCajado;
    public GameObject projectilePrefab;
    public Transform spellSpawnPoint;
    public float cooldown = 0.5f;
    [Min(0f)] public float zonaMortaMira = 0.5f;

    private float ultimoAtaque = float.NegativeInfinity;
    private Animator animator;
    private Camera mainCamera;
    private Vector3 direcaoMira;
    private bool miraValida;
    private bool ataquePendente;
    private bool entrouNoAtaque;
    private int solicitacao;
    private int ataqueAtual;
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int EstadoAtaque = Animator.StringToHash("Base Layer.Ranged_Magic_Raise");

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null ||
            !animator.HasState(0, EstadoAtaque) || cajado == null ||
            projectilePrefab == null || spellSpawnPoint == null)
        {
            Debug.LogError("PlayerCombat requer Base Layer.Ranged_Magic_Raise, cajado, projectilePrefab e spellSpawnPoint.", this);
            enabled = false;
            return;
        }
        direcaoMira = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (direcaoMira.sqrMagnitude < 0.000001f)
            direcaoMira = Vector3.forward;
    }

    void Update()
    {
        AtualizarMira();
        if (Input.GetMouseButtonDown(0))
            Atacar();
    }

    void LateUpdate()
    {
        AplicarMira();
        if (!ataquePendente)
            return;
        if (!animator.isActiveAndEnabled)
        {
            CancelarAtaque("Animator desativado");
            return;
        }

        bool noAtaque = animator.GetCurrentAnimatorStateInfo(0).fullPathHash == EstadoAtaque ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == EstadoAtaque);
        if (noAtaque)
            entrouNoAtaque = true;
        else if (entrouNoAtaque)
        {
            Debug.LogError($"[Combat] ataque={ataqueAtual} FALHA: saiu do estado antes de LancarMagia. Verifique evento e interrupcoes.", this);
            CancelarAtaque("estado encerrado sem evento");
        }
    }

    void AtualizarMira()
    {
        miraValida = false;
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane plano = new Plane(Vector3.up, transform.position);
        if (plano.Raycast(ray, out float distancia))
            AtualizarDirecaoMira(ray.GetPoint(distancia));
    }

    void AtualizarDirecaoMira(Vector3 ponto)
    {
        Vector3 direcao = ponto - transform.position;
        direcao.y = 0f;
        float raio = Mathf.Max(0f, zonaMortaMira);
        // Dentro da zona morta mantemos a ultima direcao, inclusive durante o lancamento.
        if (direcao.sqrMagnitude > Mathf.Max(raio * raio, 0.000001f))
            direcaoMira = direcao.normalized;
        miraValida = true;
        transform.rotation = Quaternion.LookRotation(direcaoMira);
    }

    void AplicarMira()
    {
        if (direcaoMira.sqrMagnitude < 0.000001f)
            return;
        Quaternion rotacao = Quaternion.LookRotation(direcaoMira);
        transform.rotation = rotacao;
        if (cajado != null)
            cajado.rotation = rotacao * Quaternion.Euler(offsetRotacaoCajado);
    }

    void Atacar()
    {
        solicitacao++;
        Diagnostico($"SOLICITACAO id={solicitacao}");
        string rejeicao = null;
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled) rejeicao = "componente/Animator inativo";
        else if (Time.time < ultimoAtaque + cooldown) rejeicao = "cooldown";
        else if (ataquePendente) rejeicao = "ataque pendente";
        else if (!miraValida) rejeicao = "mira indisponivel";
        else if (cajado == null || spellSpawnPoint == null || projectilePrefab == null) rejeicao = "referencia ausente";
        if (rejeicao != null)
        {
            Diagnostico($"REJEITADO id={solicitacao} motivo={rejeicao}");
            return;
        }

        ultimoAtaque = Time.time;
        ataqueAtual = solicitacao;
        ataquePendente = true;
        entrouNoAtaque = false;
        animator.ResetTrigger(Attack);
        // Reinicia o estado sem aguardar Has Exit Time nem misturar eventos do ataque anterior.
        animator.Play(EstadoAtaque, 0, 0f);
        Diagnostico($"ACEITO id={ataqueAtual}");
    }

    // Somente o Animation Event instancia a magia; eventos repetidos consomem a mesma pendencia.
    public void LancarMagia()
    {
        Diagnostico($"EVENTO ataque={ataqueAtual}");
        if (!isActiveAndEnabled || !ataquePendente)
        {
            Diagnostico($"EVENTO IGNORADO ataque={ataqueAtual} (inativo/sem pendencia)");
            return;
        }
        if (!animator.isActiveAndEnabled || cajado == null || spellSpawnPoint == null || projectilePrefab == null)
        {
            CancelarAtaque("Animator inativo ou referencia removida antes do evento");
            return;
        }

        // A direcao aprovada em Update continua valida mesmo se a camera desaparecer antes do evento.
        ataquePendente = false;
        AplicarMira();
        var projectile = Instantiate(projectilePrefab, spellSpawnPoint.position, Quaternion.LookRotation(direcaoMira));
        Diagnostico($"INSTANCIADO ataque={ataqueAtual} objeto={projectile.GetInstanceID()} direcao={direcaoMira}");
    }

    void CancelarAtaque(string motivo)
    {
        if (ataquePendente)
            Diagnostico($"CANCELADO ataque={ataqueAtual} motivo={motivo}");
        ataquePendente = false;
        entrouNoAtaque = false;
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.ResetTrigger(Attack);
    }

    void OnDisable()
    {
        CancelarAtaque("componente/GameObject desativado");
        miraValida = false;
    }

    // Diagnostico temporario: remover este metodo e suas chamadas apos validar no Editor.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    void Diagnostico(string mensagem)
    {
        string estado = animator != null && animator.runtimeAnimatorController != null
            ? $" estado={animator.GetCurrentAnimatorStateInfo(0).fullPathHash} normalizado={animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F3} transicao={animator.IsInTransition(0)}"
            : "";
        Debug.Log($"[Combat] t={Time.time:F3} frame={Time.frameCount} {mensagem} pendente={ataquePendente}{estado}", this);
    }
}
