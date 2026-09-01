using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float velocidade = 5f;
    public float gravidade = -9.81f;
    public float velocidadeRotacao = 10f;

    private CharacterController controller;
    private Animator animator;

    private Vector3 velocidadeVertical;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 direcao = new Vector3(horizontal, 0f, vertical);

        if (direcao.magnitude > 1f)
        {
            direcao.Normalize();
        }

        if (direcao != Vector3.zero)
        {
            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                rotacaoAlvo,
                velocidadeRotacao * Time.deltaTime
            );
        }

        animator.SetFloat("Speed", direcao.magnitude);

        controller.Move(
            direcao * velocidade * Time.deltaTime
        );

        if (controller.isGrounded && velocidadeVertical.y < 0)
        {
            velocidadeVertical.y = -2f;
        }

        velocidadeVertical.y += gravidade * Time.deltaTime;

        controller.Move(
            velocidadeVertical * Time.deltaTime
        );
    }
}