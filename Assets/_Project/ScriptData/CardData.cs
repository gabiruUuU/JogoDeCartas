using UnityEngine;

public enum AbilityType
{
    None,
    SelfMiss,          // Burro: chance de errar o próprio ataque
    FuryDoubleDamage,  // Irritado: chance de dano dobrado em modo raiva
    Shield,            // Fedido: reduz o dano recebido
    Accumulate,        // Chato: acumula dano em vez de atacar
    Buy,               // Rico: compra uma carta inimiga
    Sadden,            // Chorão: deixa os inimigos tristes
    SelfHeal,          // Lindo: se cura no começo da vez
    Sleep              // Dorminhoco: dorme e se cura por inteiro
}

[CreateAssetMenu(fileName = "Card_", menuName = "Batalha/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName;
    public Sprite art;
    [TextArea(10, 30)] public string story;   // texto longo, pra exigir rolagem
    public int health = 100;
    public int minDamage = 10;
    public int maxDamage = 20;

    [Header("Habilidade")]
    public AbilityType ability = AbilityType.None;

    [Tooltip("Burro: chance de errar o ataque | Irritado: chance de dano dobrado | Chorão: chance de a carta inimiga não atacar")]
    [Range(0f, 1f)] public float abilityChance = 0f;

    [Tooltip("Fedido: redução do dano recebido (0,5 = 50%) | Lindo: cura máxima, com a vida quase zerada")]
    [Range(0f, 1f)] public float abilityAmount = 0f;

    [Tooltip("Lindo: cura mínima, com a vida cheia")]
    [Range(0f, 1f)] public float abilityAmountMin = 0f;

    [Tooltip("Chato: máximo de acúmulos | Rico: recarga (em turnos da própria carta) | Dorminhoco: rodadas dormindo")]
    public int abilityTurns = 0;

    public bool HasActiveAbility =>
        ability == AbilityType.Accumulate || ability == AbilityType.Buy || ability == AbilityType.Sleep;

    public string AbilityName => ability switch
    {
        AbilityType.SelfMiss => "Atrapalhado",
        AbilityType.FuryDoubleDamage => "Fúria explosiva",
        AbilityType.Shield => "Escudo",
        AbilityType.Accumulate => "Acumular",
        AbilityType.Buy => "Comprar",
        AbilityType.Sadden => "Tristeza",
        AbilityType.SelfHeal => "Beleza curativa",
        AbilityType.Sleep => "Soneca",
        _ => "Nenhuma"
    };

    public string AbilityDescription => ability switch
    {
        AbilityType.SelfMiss =>
            $"{Pct(abilityChance)}% de chance de errar o próprio ataque.",
        AbilityType.FuryDoubleDamage =>
            $"Com a vida abaixo de 30%, tem {Pct(abilityChance)}% de chance de causar o dobro de dano.",
        AbilityType.Shield =>
            $"Reduz em {Pct(abilityAmount)}% todo o dano recebido.",
        AbilityType.Accumulate =>
            $"No lugar de atacar, acumula o dano do turno (até {abilityTurns} vezes) e solta tudo de uma vez no próximo ataque.",
        AbilityType.Buy =>
            $"No lugar de atacar, compra uma carta inimiga: ela ataca uma carta do próprio time e perde a próxima vez. Pode comprar de novo a cada {abilityTurns} turnos dela.",
        AbilityType.Sadden =>
            $"As cartas inimigas ficam tristes durante toda a batalha: {Pct(abilityChance)}% de chance de não atacar (ainda podem tentar de novo se tiverem sorte).",
        AbilityType.SelfHeal =>
            $"No começo da vez, se cura entre {Pct(abilityAmountMin)}% (vida cheia) e {Pct(abilityAmount)}% (vida quase zerada) da vida máxima.",
        AbilityType.Sleep =>
            $"Dorme e fica inativo por {abilityTurns} rodadas; ao acordar, recupera toda a vida.",
        _ => "Sem habilidade especial."
    };

    static int Pct(float f) => Mathf.RoundToInt(f * 100f);
}