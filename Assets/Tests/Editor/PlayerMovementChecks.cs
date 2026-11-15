using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class PlayerMovementChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo StepMethod = typeof(PlayerMovement).GetMethod("AtualizarMovimento", Private);
    static PlayerMovement movement;
    static CharacterController body;
    static readonly Vector3 Origin = new Vector3(100, 0.02f, 100);

    [MenuItem("Tools/Underdog/Check Player Movement (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Execute em Play Mode sem pausa.");
        var root = new GameObject("Player movement checks (temporary)");
        try
        {
            var floor = new GameObject("Floor"); floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(100, -0.1f, 100);
            floor.AddComponent<BoxCollider>().size = new Vector3(80, 0.2f, 80);
            var actor = new GameObject("Movement fixture"); actor.transform.SetParent(root.transform);
            actor.transform.position = Origin;
            actor.AddComponent<Vida>();
            movement = actor.AddComponent<PlayerMovement>();
            movement.enabled = false;
            body = actor.GetComponent<CharacterController>(); body.height = 2; body.radius = 0.4f; body.center = Vector3.up;
            Physics.SyncTransforms();

            Steps(Vector2.up, false, 60);
            float walk = Vector3.Distance(Origin, actor.transform.position);
            Assert(Mathf.Abs(movement.VelocidadeAtual - 5) < 0.1f && walk > 4.5f, "Andar deve acelerar rapidamente ate 5.");
            Assert(movement.StaminaAtual == 100, "Andar nao pode consumir stamina.");
            Reset(); Steps(Vector2.one, false, 60);
            Assert(Mathf.Abs(Vector3.Distance(Origin, actor.transform.position) - walk) < 0.1f, "Diagonal deve manter a mesma velocidade.");
            Reset(); Steps(Vector2.up, true, 60);
            Assert(Mathf.Abs(movement.VelocidadeAtual - 7) < 0.1f && Mathf.Abs(movement.StaminaAtual - 82) < 0.2f, "Corrida deve atingir 7 e consumir 18/s.");
            float remaining = movement.StaminaAtual;
            Steps(Vector2.zero, true, 30);
            Assert(Mathf.Abs(movement.StaminaAtual - remaining) < 0.1f && movement.VelocidadeAtual < 0.1f, "Parado nao consome nem regenera antes do atraso.");
            Steps(Vector2.zero, false, 15);
            Assert(Mathf.Abs(movement.StaminaAtual - remaining) < 0.1f, "Atraso deve durar 0.75 s.");
            Steps(Vector2.zero, false, 24);
            Assert(Mathf.Abs(movement.StaminaAtual - remaining - 10) < 0.2f, "Regeneracao deve recuperar 25/s.");
            Set("staminaAtual", 0.2f); Steps(Vector2.up, true, 1);
            Assert(movement.StaminaEsgotada && !movement.Correndo, "Esgotamento deve cancelar a corrida.");
            int limit = 200;
            while (movement.StaminaAtual < 19 && --limit > 0) Steps(Vector2.up, true, 1);
            Assert(!movement.Correndo && movement.VelocidadeAtual <= 5.1f, "Esgotado continua andando sem penalidade e sem sprint precoce.");
            while (movement.StaminaEsgotada && --limit > 0) Steps(Vector2.up, true, 1);
            Assert(limit > 0 && movement.StaminaAtual >= 20, "Precisa recuperar 20 para liberar corrida.");
            Steps(Vector2.up, true, 20);
            Assert(movement.Correndo, "Corrida deve voltar depois da recuperacao minima.");

            Reset(); Set("staminaAtual", 0f); Set("esgotado", true); movement.regeneracaoStamina = 0;
            Set("proximoDashDisponivel", -1f);
            Step(Vector2.up, true, true, 0.1f); Step(Vector2.up, true, false, 0.1f);
            Assert(Mathf.Abs(actor.transform.position.z - Origin.z - 2.4f) < 0.1f && movement.StaminaAtual == 0, "Dash funciona sem stamina e conserva seu impulso de 2.4 unidades.");
            Steps(Vector2.up, true, 12);
            Assert(!movement.EstaDashando && Mathf.Abs(movement.VelocidadeAtual - 5) < 0.1f, "Fim do dash deve voltar a andar mesmo esgotado.");
            Step(Vector2.up, false, true, 1f / 60);
            Assert(!movement.EstaDashando, "Dash deve respeitar cooldown.");
            Reset(); Set("staminaAtual", 100f); Set("esgotado", false); Set("proximoDashDisponivel", -1f);
            Step(Vector2.one, true, true, 0.1f); Step(Vector2.one, true, false, 0.1f);
            Assert(Mathf.Abs(Vector3.ProjectOnPlane(actor.transform.position - Origin, Vector3.up).magnitude - 2.4f) < 0.1f && movement.StaminaAtual == 100,
                "Dash diagonal com stamina cheia deve manter distancia e nao consumir folego.");
            Steps(Vector2.up, true, 20);
            Assert(movement.Correndo && Mathf.Abs(movement.VelocidadeAtual - 7) < 0.1f, "Depois do dash deve retomar corrida quando disponivel.");

            Reset(); var wall = new GameObject("Wall"); wall.transform.SetParent(root.transform);
            wall.transform.position = Origin + new Vector3(0, 1.5f, 2);
            wall.AddComponent<BoxCollider>().size = new Vector3(10, 3, 0.5f); Physics.SyncTransforms();
            Set("proximoDashDisponivel", -1f); Step(Vector2.up, false, true, 0.3f);
            Assert(actor.transform.position.z < Origin.z + 1.5f, "Dash nao pode atravessar parede mesmo com passo de 0.3 s.");
            Steps(Vector2.up, false, 30);
            Set("staminaAtual", 80f); Set("esgotado", false); movement.regeneracaoStamina = 0;
            Steps(Vector2.up, true, 60);
            Assert(movement.StaminaAtual > 79, "Insistir parado contra parede nao deve consumir stamina.");
            var life = actor.GetComponent<Vida>(); life.ReceberDano(10);
            Reset(); Steps(Vector2.up, false, 20);
            Assert(actor.transform.position.z > Origin.z + 0.5f, "Hit nao pode bloquear movimentacao.");
            life.ReceberDano(1000); Vector3 dead = actor.transform.position;
            Step(Vector2.right, true, true, 0.1f);
            Assert(Vector3.Distance(dead, actor.transform.position) < 0.001f, "Morto nao pode mover ou usar dash.");
            Debug.Log("PASS Player Movement: aceleracao, diagonal, sprint, stamina/atraso/recuperacao, dash esgotado/cooldown/parede, Hit e morte.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); movement = null; body = null; }
    }
    static void Reset()
    {
        typeof(PlayerMovement).GetMethod("OnDisable", Private).Invoke(movement, null);
        body.enabled = false; movement.transform.position = Origin; body.enabled = true;
        Physics.SyncTransforms();
    }
    static void Steps(Vector2 input, bool sprint, int count) { for (int i = 0; i < count; i++) Step(input, sprint, false, 1f / 60); }
    static void Step(Vector2 input, bool sprint, bool dash, float dt) => StepMethod.Invoke(movement, new object[] { input, sprint, dash, dt });
    static void Set(string field, object value) => typeof(PlayerMovement).GetField(field, Private).SetValue(movement, value);
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
