using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    public float velocidadeDash = 12f;
    public float duracaoDash = 0.2f;
    public float cooldownDash = 1f;
    public GameObject efeitoDash;

    private bool estaDashando = false;
    private float tempoDashRestante;
    private float proximoDashDisponivel;

    private Vector3 direcaoDash;
    public float velocidade = 5f;
    public float gravidade = -9.81f;
    public float velocidadeRotacao = 10f;

    private CharacterController controller;
    private Animator animator;

    private Vector3 velocidadeVertical;
    private bool movimentoDirecional;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        movimentoDirecional = animator != null && System.Array.Exists(animator.parameters, p => p.name == "MoveX");
        if (controller == null || animator == null)
        {
            Debug.LogError("PlayerMovement requer CharacterController e Animator.", this);
            enabled = false;
        }
    }
    void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 direcao = new Vector3(horizontal, 0f, vertical);
        if (Input.GetKeyDown(KeyCode.LeftShift) && Time.time >= proximoDashDisponivel)
        {
            IniciarDash(direcao);
        }


        if (direcao.magnitude > 1f)
        {
            direcao.Normalize();
        }

        animator.SetFloat("Speed", estaDashando ? 1f : direcao.magnitude);
        if (movimentoDirecional)
        {
            Vector3 local = transform.InverseTransformDirection(estaDashando ? direcaoDash : direcao);
            animator.SetFloat("MoveX", local.x);
            animator.SetFloat("MoveZ", local.z);
        }

        if (estaDashando)
            ExecutarDash();
        else
            controller.Move(direcao * velocidade * Time.deltaTime);

        if (controller.isGrounded && velocidadeVertical.y < 0)
        {
            velocidadeVertical.y = -2f;
        }

        velocidadeVertical.y += gravidade * Time.deltaTime;

        controller.Move(
            velocidadeVertical * Time.deltaTime
        );
    }

    void IniciarDash(Vector3 direcaoMovimento)
    {
        estaDashando = true;
        tempoDashRestante = duracaoDash;
        proximoDashDisponivel = Time.time + cooldownDash;

        if (direcaoMovimento != Vector3.zero)
        {
            direcaoDash = direcaoMovimento.normalized;
        }
        else
        {
            direcaoDash = transform.forward;
        }
        int dash = Animator.StringToHash("Base Layer.Dash");
        if (animator.HasState(0, dash)) animator.CrossFadeInFixedTime(dash, 0.035f, 0);
        if (efeitoDash != null) Instantiate(efeitoDash, transform.position + Vector3.up * 0.3f, Quaternion.identity);
    }

    void ExecutarDash()
    {
        controller.Move(
            direcaoDash * velocidadeDash * Mathf.Min(Time.deltaTime, tempoDashRestante)
        );

        tempoDashRestante -= Time.deltaTime;

        if (tempoDashRestante <= 0f)
        {
            estaDashando = false;
        }
    }

    void OnDisable()
    {
        estaDashando = false;
        velocidadeVertical = Vector3.zero;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat("Speed", 0);
            if (movimentoDirecional) { animator.SetFloat("MoveX", 0); animator.SetFloat("MoveZ", 0); }
        }
    }
}
