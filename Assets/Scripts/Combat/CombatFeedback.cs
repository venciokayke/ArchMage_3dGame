using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Vida))]
public class CombatFeedback : MonoBehaviour
{
    public GameObject efeitoHit;
    public GameObject efeitoMorte;
    private Vida vida;
    private Renderer[] renderers;
    private MaterialPropertyBlock[] originais;
    private Coroutine flash;

    void Start()
    {
        vida = GetComponent<Vida>();
        renderers = GetComponentsInChildren<Renderer>();
        originais = new MaterialPropertyBlock[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originais[i] = new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(originais[i]);
        }
        vida.aoReceberDano.AddListener(Hit);
        vida.aoMorrer.AddListener(Death);
    }

    void Hit(float quantidade)
    {
        if (efeitoHit != null) Instantiate(efeitoHit, transform.position + Vector3.up, Quaternion.identity);
        if (flash != null) StopCoroutine(flash);
        flash = StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(2, 1.4f, 1.4f));
        foreach (var renderer in renderers) if (renderer != null) renderer.SetPropertyBlock(block);
        yield return new WaitForSeconds(0.09f);
        Restore();
        flash = null;
    }

    void Restore()
    {
        // A recarga de scripts pode restaurar Renderer[] sem os PropertyBlocks nativos.
        if (renderers == null || originais == null) return;
        for (int i = 0; i < renderers.Length && i < originais.Length; i++)
            if (renderers[i] != null && originais[i] != null) renderers[i].SetPropertyBlock(originais[i]);
    }
    void Death() { if (efeitoMorte != null) Instantiate(efeitoMorte, transform.position + Vector3.up * 0.6f, Quaternion.identity); }
    void OnDisable() { Restore(); }
    void OnDestroy()
    {
        if (vida == null) return;
        vida.aoReceberDano.RemoveListener(Hit);
        vida.aoMorrer.RemoveListener(Death);
    }
}
