using UnityEngine;
using UnityEngine.AI;

public class ArenaNavigation : MonoBehaviour
{
    public NavMeshData dados;
    private NavMeshDataInstance instance;
    void OnEnable()
    {
        if (dados != null) instance = NavMesh.AddNavMeshData(dados);
        else Debug.LogError("Arena sem dados de NavMesh. Execute Prepare Survival Game.", this);
    }
    void OnDisable() { if (instance.valid) instance.Remove(); }
}
