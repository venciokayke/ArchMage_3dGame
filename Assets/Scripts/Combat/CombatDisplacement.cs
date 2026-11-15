using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class CombatDisplacement : MonoBehaviour
{
    Vector3 velocity;
    float remaining;
    float braking;
    public bool Ativo => remaining > 0 && velocity.sqrMagnitude > 0.001f;

    public void Aplicar(Vector3 direction, float speed, float duration)
    {
        if (!gameObject.activeInHierarchy || speed <= 0 || duration <= 0) return;
        var life = GetComponent<Vida>();
        if (life != null && life.EstaMorto) return;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        velocity = Vector3.ClampMagnitude(velocity + direction * speed, Mathf.Max(speed, 20));
        remaining = Mathf.Max(remaining, duration);
        braking = velocity.magnitude / remaining;
    }

    // O dono aplica este passo junto da gravidade; este componente nunca chama Move.
    public Vector3 ConsumirPasso(float dt)
    {
        if (!Ativo || dt <= 0) return Vector3.zero;
        float step = Mathf.Min(dt, remaining);
        Vector3 next = Vector3.MoveTowards(velocity, Vector3.zero, braking * step);
        Vector3 delta = (velocity + next) * (0.5f * step);
        velocity = next;
        remaining = Mathf.Max(0, remaining - dt);
        return delta;
    }
    public void Limpar() { velocity = Vector3.zero; remaining = 0; }
    void OnDisable() { Limpar(); }

    public static Vector3 LimitarAoNavMesh(Vector3 delta, NavMeshAgent agent)
    {
        if (agent == null) return delta;
        if (!agent.enabled || !agent.isOnNavMesh) return Vector3.zero;
        if (NavMesh.Raycast(agent.nextPosition, agent.nextPosition + delta, out var hit, agent.areaMask))
            return Vector3.ClampMagnitude(Vector3.ProjectOnPlane(hit.position - agent.nextPosition, Vector3.up), delta.magnitude);
        return delta;
    }
}
