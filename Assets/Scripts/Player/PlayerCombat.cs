using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Transform cajado;
    public Vector3 offsetRotacaoCajado;
    private Quaternion rotacaoBaseCajado;
    public GameObject projectilePrefab;
    public Transform spellSpawnPoint;

    public float cooldown = 0.5f;

    private float ultimoAtaque;
    private Animator animator;
    private Camera mainCamera;

    private Vector3 pontoMira;

    void Start()
    {
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;

        rotacaoBaseCajado = cajado.localRotation;
    }

    void Update()
    {
        AtualizarMira();

        if (Input.GetMouseButtonDown(0))
        {
            Atacar();
        }
    }

    void AtualizarMira()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        Plane plano = new Plane(Vector3.up, transform.position);

        if (plano.Raycast(ray, out float distancia))
        {
            pontoMira = ray.GetPoint(distancia);

            Vector3 direcao = pontoMira - transform.position;
            direcao.y = 0;

            if (direcao != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direcao);
            }
        }

        Vector3 direcaoCajado = pontoMira - cajado.position;

        if (direcaoCajado != Vector3.zero)
        {
            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcaoCajado);

            cajado.rotation =
                rotacaoAlvo * Quaternion.Euler(offsetRotacaoCajado);
        }
    }

    void Atacar()
    {
        if (Time.time < ultimoAtaque + cooldown)
            return;

        ultimoAtaque = Time.time;

        animator.SetTrigger("Attack");

        Vector3 direcaoTiro = pontoMira - spellSpawnPoint.position;
        direcaoTiro.y = 0;

        Quaternion rotacaoTiro =
            Quaternion.LookRotation(direcaoTiro.normalized);

        Instantiate(
            projectilePrefab,
            spellSpawnPoint.position,
            rotacaoTiro
        );
    }
}