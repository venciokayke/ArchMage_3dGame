using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CombatFeedbackSelfCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Underdog/Check Combat Feedback (Edit Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Execute em Edit Mode.");
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Combat feedback reload self-check";
        try
        {
            var feedback = item.AddComponent<CombatFeedback>();
            var renderer = item.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Color.green);
            renderer.SetPropertyBlock(block);
            Set(feedback, "renderers", new[] { renderer });
            // A recarga de scripts pode restaurar Renderer[] sem o cache nativo de PropertyBlocks.
            Restore(feedback);
            Set(feedback, "originais", new MaterialPropertyBlock[0]);
            Restore(feedback);
            Set(feedback, "originais", new MaterialPropertyBlock[1]);
            Restore(feedback);
            renderer.GetPropertyBlock(block);
            Assert(block.GetColor("_BaseColor") == Color.green, "Cache ausente/incompleto nao pode apagar propriedades do Renderer.");
            var original = new MaterialPropertyBlock();
            original.SetColor("_BaseColor", Color.blue);
            Set(feedback, "originais", new[] { original });
            Restore(feedback);
            renderer.GetPropertyBlock(block);
            Assert(block.GetColor("_BaseColor") == Color.blue, "Cache valido deve restaurar o feedback normalmente.");
            Set(feedback, "renderers", new Renderer[] { null });
            Restore(feedback);
            Debug.Log("PASS Combat Feedback: OnDisable com cache ausente/incompleto, Renderer removido e restauracao normal.");
        }
        finally { UnityEngine.Object.DestroyImmediate(item); }
    }
    static void Restore(CombatFeedback feedback) => typeof(CombatFeedback).GetMethod("OnDisable", Private).Invoke(feedback, null);
    static void Set(CombatFeedback feedback, string field, object value) => typeof(CombatFeedback).GetField(field, Private).SetValue(feedback, value);
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
