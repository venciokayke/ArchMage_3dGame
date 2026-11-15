using UnityEngine;

public class TimedEffect : MonoBehaviour
{
    [Min(0.01f)] public float duracao = 1f;
    void Start() { Destroy(gameObject, duracao); }
}
