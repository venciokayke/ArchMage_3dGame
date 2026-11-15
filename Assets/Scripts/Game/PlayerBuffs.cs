using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TipoBuff { PoderArcano, Vitalidade, Agilidade, ConjuracaoRapida, ExplosaoAprimorada, Resistencia }

[Serializable]
public class DefinicaoBuff
{
    public TipoBuff tipo;
    public string nome;
    [TextArea] public string descricao;
    [Min(0)] public float valor;
    [Min(0)] public float valorSecundario;
    [Min(1)] public int maxAcumulos = 4;
}

public class PlayerBuffs : MonoBehaviour
{
    public DefinicaoBuff[] catalogo = Padroes();
    [Header("Limites por partida")]
    [Min(0)] public float bonusDanoMaximo = 1.5f;
    [Min(0)] public float bonusExplosaoMaximo = 1.2f;
    [Min(0)] public float bonusEmpurraoMaximo = 0.6f;
    [Min(0)] public float bonusVelocidadeMaximo = 0.5f;
    [Range(0, 0.8f)] public float reducaoCooldownMaxima = 0.5f;
    [Min(0.05f)] public float cooldownMinimo = 0.15f;
    [Range(0, 0.8f)] public float resistenciaMaxima = 0.6f;
    [SerializeField] int[] acumulos = new int[6];
    PlayerHp player;
    PlayerCombat combat;
    PlayerMovement movement;
    BuffSelectionUI ui;
    WaveManager manager;
    DefinicaoBuff[] definitions;
    DefinicaoBuff[] ofertas = new DefinicaoBuff[0];
    float basePrimary, baseArea, baseBurst, basePush, baseSpeed, baseCooldown, baseAreaCooldown, baseBurstCooldown, baseResistance;
    int ondaOferecida, ultimaOndaEscolhida;
    bool initialized;
    public bool Pendente { get; private set; }
    public IReadOnlyList<DefinicaoBuff> Ofertas => ofertas;
    public string UltimaEscolha { get; private set; }
    public int EscolhasAplicadas => acumulos.Sum();
    public int Acumulos(TipoBuff tipo) => acumulos[(int)tipo];

    public static DefinicaoBuff[] Padroes() => new[] {
        new DefinicaoBuff { tipo=TipoBuff.PoderArcano, nome="Poder Arcano", descricao="+20% de dano das magias", valor=0.2f },
        new DefinicaoBuff { tipo=TipoBuff.Vitalidade, nome="Vitalidade", descricao="+25 de vida máxima e recupera 25 HP", valor=25 },
        new DefinicaoBuff { tipo=TipoBuff.Agilidade, nome="Agilidade", descricao="+10% de velocidade de movimento", valor=0.1f },
        new DefinicaoBuff { tipo=TipoBuff.ConjuracaoRapida, nome="Conjuração Rápida", descricao="-10% de cooldown das habilidades", valor=0.1f },
        new DefinicaoBuff { tipo=TipoBuff.ExplosaoAprimorada, nome="Explosão Aprimorada", descricao="+20% de dano da explosão e +15% de empurrão", valor=0.2f, valorSecundario=0.15f },
        new DefinicaoBuff { tipo=TipoBuff.Resistencia, nome="Resistência", descricao="-10% do dano recebido", valor=0.1f }
    };

    public void Inicializar(PlayerHp target, WaveManager partida)
    {
        if (initialized) return;
        player = target; manager = partida;
        combat = player.GetComponent<PlayerCombat>(); movement = player.GetComponent<PlayerMovement>();
        if (combat != null)
        {
            basePrimary=combat.danoPrimario; baseArea=combat.explosao.dano; baseBurst=combat.rajada.dano; basePush=combat.explosao.empurrao;
            baseCooldown=combat.cooldown; baseAreaCooldown=combat.explosao.cooldown; baseBurstCooldown=combat.rajada.cooldown;
        }
        baseSpeed=movement!=null ? movement.velocidade : 0; baseResistance=player.Vida.reducaoDano;
        var valid = (catalogo ?? new DefinicaoBuff[0]).Where(d=>d!=null && (int)d.tipo>=0 && (int)d.tipo<6 && d.maxAcumulos>0)
            .GroupBy(d=>d.tipo).ToDictionary(g=>g.Key,g=>g.First());
        definitions=Padroes();
        for(int i=0;i<definitions.Length;i++) if(valid.TryGetValue((TipoBuff)i,out var custom)) definitions[i]=custom;
        initialized=true;
    }

    public bool Oferecer(int onda)
    {
        if (!initialized || Pendente || onda<=ultimaOndaEscolhida || player.Vida.EstaMorto) return false;
        var available=definitions.Where(d=>acumulos[(int)d.tipo]<Mathf.Max(1,d.maxAcumulos)).ToList();
        if(available.Count<3) { Debug.LogError("Catalogo de buffs sem tres opcoes disponiveis.",this); return false; }
        for(int i=available.Count-1;i>0;i--) { int j=UnityEngine.Random.Range(0,i+1); var temp=available[i];available[i]=available[j];available[j]=temp; }
        ofertas=available.Take(3).ToArray(); ondaOferecida=onda; Pendente=true;
        if(ui==null) ui=BuffSelectionUI.Criar(manager);
        ui.Mostrar(this);
        return true;
    }

    public bool Escolher(int index)
    {
        if(!Pendente || index<0 || index>=ofertas.Length || player.Vida.EstaMorto || ultimaOndaEscolhida>=ondaOferecida) return false;
        var selected=ofertas[index]; int type=(int)selected.tipo;
        if(acumulos[type]>=Mathf.Max(1,selected.maxAcumulos)) return false;
        Pendente=false; ultimaOndaEscolhida=ondaOferecida; acumulos[type]++;
        if(selected.tipo==TipoBuff.Vitalidade) player.AumentarVidaMaxima(Mathf.RoundToInt(Valor(selected.valor)));
        Recalcular();
        UltimaEscolha=selected.nome;
        if(ui!=null) ui.Aplicada(UltimaEscolha);
        return true;
    }

    void Recalcular()
    {
        float Bonus(TipoBuff type)=>acumulos[(int)type]*Valor(definitions[(int)type].valor);
        float power=1+Mathf.Min(bonusDanoMaximo,Bonus(TipoBuff.PoderArcano));
        float area=1+Mathf.Min(bonusExplosaoMaximo,Bonus(TipoBuff.ExplosaoAprimorada));
        float cooldown=1-Mathf.Clamp(Bonus(TipoBuff.ConjuracaoRapida),0,reducaoCooldownMaxima);
        if(combat!=null)
        {
            combat.danoPrimario=basePrimary*power; combat.rajada.dano=baseBurst*power; combat.explosao.dano=baseArea*power*area;
            combat.explosao.empurrao=basePush*(1+Mathf.Min(bonusEmpurraoMaximo,acumulos[(int)TipoBuff.ExplosaoAprimorada]*Valor(definitions[(int)TipoBuff.ExplosaoAprimorada].valorSecundario)));
            combat.cooldown=Mathf.Min(baseCooldown,Mathf.Max(cooldownMinimo,baseCooldown*cooldown));
            combat.explosao.cooldown=Mathf.Min(baseAreaCooldown,Mathf.Max(cooldownMinimo,baseAreaCooldown*cooldown));
            combat.rajada.cooldown=Mathf.Min(baseBurstCooldown,Mathf.Max(cooldownMinimo,baseBurstCooldown*cooldown));
        }
        if(movement!=null) movement.velocidade=baseSpeed*(1+Mathf.Min(bonusVelocidadeMaximo,Bonus(TipoBuff.Agilidade)));
        player.Vida.reducaoDano=Mathf.Max(baseResistance,Mathf.Clamp(baseResistance+Bonus(TipoBuff.Resistencia),0,resistenciaMaxima));
    }
    static float Valor(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Max(0,value);
    public void Cancelar() { Pendente=false; if(ui!=null) ui.Ocultar(); }
}
