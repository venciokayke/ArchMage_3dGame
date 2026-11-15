using UnityEngine;

[DisallowMultipleComponent]
public class EnemySpecialAttack : MonoBehaviour
{
    [System.Serializable]
    public class GolpePesado
    {
        public int ondaMinima = 3;
        [Min(0)] public float cooldown = 6, preparo = 0.9f, recuperacao = 0.5f;
        [Min(0)] public float dano = 25, alcance = 2.8f, empurrao = 7, duracaoEmpurrao = 0.2f;
        [Range(10, 180)] public float angulo = 100;
    }
    [System.Serializable]
    public class Investida
    {
        public int ondaMinima = 4;
        [Min(0)] public float aviso = 0.65f, duracao = 0.25f, velocidade = 13, dano = 15, cooldown = 5, recuperacao = 0.4f;
        [Min(0)] public float distanciaMinima = 3, distanciaMaxima = 8, largura = 1;
        [Min(0)] public float intervaloEntreRogues = 0.35f;
    }
    [System.Serializable]
    public class Rajada
    {
        public int ondaMinima = 5;
        [Min(0)] public float cooldown = 7, preparo = 0.65f, recuperacao = 0.25f, dano = 12;
        [Range(1, 30)] public float abertura = 12;
    }
    public GolpePesado pesado = new GolpePesado();
    public Investida investida = new Investida();
    public Rajada rajada = new Rajada();
    public Material materialAviso;
    public Projectile projetilRajada;
    public bool habilitado = true;
    enum Fase { Livre, Preparo, Investida, Recuperacao }
    Fase fase;
    EnemyAI owner;
    float until, nextUse;
    Vector3 direction;
    bool hitCharge, movedCharge;
    GameObject warning;
    Mesh warningMesh;
    Material fallbackMaterial;
    static float nextRogue;
    public bool Ativo => fase != Fase.Livre;
    public bool Preparando => fase == Fase.Preparo;
    public bool BloqueiaMovimento => Ativo && owner != null && owner.Tipo != EnemyAI.Comportamento.Distante;
    public Vector3 Passo { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCoordination() { nextRogue = 0; }
    public void Inicializar(EnemyAI ai) { owner = ai; nextUse = Time.time + Random.Range(1, 2); }

    public void Atualizar(float dt)
    {
        Passo = Vector3.zero;
        movedCharge = false;
        if (owner == null || !habilitado || owner.AlvoAtual == null || owner.Inativo)
        { Cancelar(); return; }
        var life = owner.AlvoAtual.GetComponent<Vida>();
        if (!owner.AlvoAtual.gameObject.activeInHierarchy || (life != null && life.EstaMorto)) { Cancelar(); return; }
        float distance = Vector3.ProjectOnPlane(owner.AlvoAtual.position - transform.position, Vector3.up).magnitude;
        bool mage = owner.Tipo == EnemyAI.Comportamento.Distante;
        bool rogue = owner.Tipo == EnemyAI.Comportamento.Flanqueador;
        if (fase == Fase.Livre)
        {
            int wave = mage ? rajada.ondaMinima : rogue ? investida.ondaMinima : pesado.ondaMinima;
            bool range = mage ? distance <= owner.alcanceAtaque : rogue ? distance >= investida.distanciaMinima && distance <= investida.distanciaMaxima : distance <= pesado.alcance;
            if (owner.OndaAtual < wave || Time.time < nextUse || owner.AtaqueBasicoOcupado || !range || !owner.TemLinhaDeVisao() || (rogue && Time.time < nextRogue)) return;
            if (rogue) nextRogue = Time.time + investida.intervaloEntreRogues + Random.Range(0, 0.15f);
            direction = Vector3.ProjectOnPlane(owner.AlvoAtual.position - transform.position, Vector3.up).normalized;
            fase = Fase.Preparo;
            until = Time.time + (mage ? rajada.preparo : rogue ? investida.aviso : pesado.preparo);
            nextUse = Time.time + Mathf.Max(0.2f, mage ? rajada.cooldown : rogue ? investida.cooldown : pesado.cooldown);
            owner.AnimarEspecial(mage ? rajada.preparo : rogue ? investida.aviso : pesado.preparo);
            if (!mage) MostrarAviso(rogue ? investida.velocidade * investida.duracao : pesado.alcance, rogue ? investida.largura : pesado.alcance);
        }
        if (fase == Fase.Preparo)
        {
            if (rogue)
            {
                Vector3 aim = Vector3.ProjectOnPlane(owner.AlvoAtual.position - transform.position, Vector3.up);
                if (aim.sqrMagnitude > 0.001f) direction = aim.normalized;
                AtualizarAviso();
            }
            if (Time.time < until) return;
            if (warning != null) warning.SetActive(false);
            owner.ConcluirAnimacaoEspecial(rogue);
            if (rogue) { fase = Fase.Investida; until = Time.time + investida.duracao; hitCharge = false; }
            else
            {
                if (owner.TemLinhaDeVisao())
                {
                    if (mage) DispararRajada();
                    else AcertarPesado();
                }
                Recuperar(mage ? rajada.recuperacao : pesado.recuperacao);
            }
        }
        if (fase == Fase.Investida)
        {
            float time = Mathf.Min(dt, Mathf.Max(0, until - Time.time));
            Passo = direction * investida.velocidade * time;
            movedCharge = true;
            transform.rotation = Quaternion.LookRotation(direction);
            if (Time.time + dt >= until) Recuperar(investida.recuperacao);
        }
        else if (fase == Fase.Recuperacao && Time.time >= until) fase = Fase.Livre;
    }

    public void AposMover(Vector3 before, Vector3 after)
    {
        if (!movedCharge || hitCharge || owner == null || owner.AlvoAtual == null) return;
        Vector3 segment = Vector3.ProjectOnPlane(after - before, Vector3.up);
        Vector3 toPlayer = Vector3.ProjectOnPlane(owner.AlvoAtual.position - before, Vector3.up);
        float t = segment.sqrMagnitude > 0.00001f ? Mathf.Clamp01(Vector3.Dot(toPlayer, segment) / segment.sqrMagnitude) : 0;
        var body = owner.AlvoAtual.GetComponent<CharacterController>();
        float radius = GetComponent<CharacterController>().radius + (body != null ? body.radius : 0.4f) + 0.15f;
        if ((toPlayer - segment * t).sqrMagnitude <= radius * radius && Mathf.Abs(owner.AlvoAtual.position.y - after.y) < 1.5f && owner.TemLinhaDeVisao())
        {
            hitCharge = true;
            owner.AlvoAtual.GetComponent<Vida>()?.ReceberDano(investida.dano);
        }
        if (segment.sqrMagnitude < 0.00001f && fase == Fase.Investida) Recuperar(investida.recuperacao);
    }

    void AcertarPesado()
    {
        Vector3 delta = Vector3.ProjectOnPlane(owner.AlvoAtual.position - transform.position, Vector3.up);
        if (delta.magnitude > pesado.alcance || Vector3.Angle(direction, delta) > pesado.angulo * 0.5f) return;
        var target = owner.AlvoAtual.GetComponent<Vida>();
        if (target == null) return;
        float before = target.VidaAtual;
        target.ReceberDano(pesado.dano);
        if (target.VidaAtual < before && !target.EstaMorto) owner.AlvoAtual.GetComponent<PlayerMovement>()?.Empurrar(delta, pesado.empurrao, pesado.duracaoEmpurrao);
    }
    void DispararRajada()
    {
        if (Vector3.Distance(transform.position, owner.AlvoAtual.position) > owner.alcanceAtaque) return;
        var prefab = projetilRajada != null ? projetilRajada : owner.projetil;
        if (prefab == null) return;
        Vector3 aim = Vector3.ProjectOnPlane(owner.AlvoAtual.position - transform.position, Vector3.up).normalized;
        for (int i = -1; i <= 1; i++)
        {
            Vector3 forward = Quaternion.Euler(0, i * rajada.abertura, 0) * aim;
            var shot = Instantiate(prefab, transform.position + Vector3.up * 1.1f + forward * 0.65f, Quaternion.LookRotation(forward));
            shot.Configurar(transform, rajada.dano, owner.velocidadeProjetil, true);
            foreach (var particles in shot.GetComponentsInChildren<ParticleSystem>())
            { var main = particles.main; main.startColor = new Color(0.7f, 0.3f, 1); }
            foreach (var trail in shot.GetComponentsInChildren<TrailRenderer>())
            { trail.startColor = new Color(0.8f, 0.4f, 1); trail.endColor = new Color(0.4f, 0.1f, 1, 0); trail.widthMultiplier *= 1.3f; }
        }
    }
    void Recuperar(float time) { fase = Fase.Recuperacao; until = Time.time + Mathf.Max(0, time); }
    public void Cancelar()
    {
        if (warning != null) warning.SetActive(false);
        fase = Fase.Livre; Passo = Vector3.zero; movedCharge = false;
        nextUse = Mathf.Max(nextUse, Time.time + 0.4f);
    }
    void MostrarAviso(float length, float width)
    {
        if (warning == null)
        {
            warning = new GameObject("Charge Warning"); warning.transform.SetParent(transform, false);
            warningMesh = new Mesh { name = "Charge trajectory" };
            warningMesh.vertices = new[] { new Vector3(-0.5f,0,0), new Vector3(0.5f,0,0), new Vector3(-0.5f,0,0.8f), new Vector3(0.5f,0,0.8f), new Vector3(0,0,1) };
            warningMesh.triangles = new[] { 0,2,1, 1,2,3, 2,4,3 }; warningMesh.RecalculateNormals();
            warning.AddComponent<MeshFilter>().sharedMesh = warningMesh;
            var renderer = warning.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (materialAviso == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    fallbackMaterial = new Material(shader);
                    fallbackMaterial.SetColor("_BaseColor", new Color(1, 0.08f, 0.04f, 0.4f));
                    fallbackMaterial.SetFloat("_Surface", 1); fallbackMaterial.SetFloat("_ZWrite", 0);
                    fallbackMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    fallbackMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    fallbackMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); fallbackMaterial.renderQueue = 3000;
                }
            }
            renderer.sharedMaterial = materialAviso != null ? materialAviso : fallbackMaterial;
        }
        warning.transform.localScale = new Vector3(width, 1, length);
        warning.SetActive(true); AtualizarAviso();
    }
    void AtualizarAviso()
    {
        if (warning == null) return;
        warning.transform.position = transform.position + Vector3.up * 0.08f;
        if (direction.sqrMagnitude > 0.001f) warning.transform.rotation = Quaternion.LookRotation(direction);
    }
    void OnDisable() { Cancelar(); }
    void OnDestroy()
    {
        if (warningMesh != null) Destroy(warningMesh);
        if (fallbackMaterial != null) Destroy(fallbackMaterial);
    }
}
