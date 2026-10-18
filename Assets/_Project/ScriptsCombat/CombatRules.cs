using UnityEngine;

public struct AttackResult
{
    public CharacterRuntime attacker;
    public CharacterRuntime target;
    public bool dodged;
    public bool critical;
    public bool furyActive;
    public int damage;
    public bool targetDefeated;
}

public static class CombatRules
{
    // ---------- Valores numéricos ajustáveis (as regras em si são fixas) ----------

    // Força
    public const int DamagePerStrength = 2;

    // Intelecto (esquiva)
    public const float DodgePerPoint = 0.03f;
    public const float DodgeCap = 0.30f;

    // Destreza (crítico)
    public const float CritPerPoint = 0.07f;
    public const float CritCap = 0.70f;
    public const float CritMultiplier = 1.8f;

    // Raiva (fúria)
    public const float FuryPerPoint = 0.06f;

    // Velocidade
    public const int BaseTurnWeight = 1;        // peso base no sorteio de quem abre a batalha
    public const int MaxAttacksPerCard = 2;     // máximo de ataques seguidos da mesma carta
    public const int ExtraAttackRange = 20;     // sorteia um número de 0 a (Range - 1)
    public const int ExtraAttackBonus = 1;      // "pontos de graça": dá chance mínima com velocidade 0

    // Equilíbrio: dano do jogador que abre a batalha, no primeiro turno dele (1 = desativado)
    public const float FirstTurnDamageMultiplier = 0.75f;

    // ---------- Prévias (tela de distribuição de pontos) ----------
    public static int MinDamage(CharacterRuntime c) => c.data.minDamage + c.points.Forca * DamagePerStrength;
    public static int MaxDamage(CharacterRuntime c) => c.data.maxDamage + c.points.Forca * DamagePerStrength;
    public static float DodgeChance(AttributePoints p) => Mathf.Min(DodgeCap, p.Intelecto * DodgePerPoint);
    public static float CritChance(AttributePoints p) => Mathf.Min(CritCap, p.Destreza * CritPerPoint);
    public static float FuryBonus(AttributePoints p) => p.Raiva * FuryPerPoint;

    // Chance de a carta atacar de novo (igual para todos: só depende da velocidade da própria carta)
    public static float ExtraAttackChance(AttributePoints p) =>
        Mathf.Clamp01((p.Velocidade + ExtraAttackBonus) / (float)ExtraAttackRange);

    // ---------- Velocidade: quem abre a batalha ----------
    // Peso do jogador = soma dos pesos dos personagens vivos
    public static int TurnWeight(CharacterRuntime c) => c.points.Velocidade + BaseTurnWeight;

    public static bool PlayerAGoesFirst(int weightA, int weightB)
    {
        int total = weightA + weightB;
        if (total <= 0) return Random.value < 0.5f;
        return Random.Range(0, total) < weightA;
    }

    // ---------- Velocidade: ataque extra da mesma carta ----------
    // Sorteia 0..(Range-1); se for menor que (velocidade + bônus), a carta ataca de novo
    public static bool RollExtraAttack(CharacterRuntime attacker)
    {
        return Random.Range(0, ExtraAttackRange) < attacker.points.Velocidade + ExtraAttackBonus;
    }

    // ---------- Resolução de um ataque ----------
    // damageMultiplier: usado para a penalidade do primeiro turno (1 = dano normal)
    public static AttackResult Resolve(CharacterRuntime attacker, CharacterRuntime target, float damageMultiplier = 1f)
    {
        var r = new AttackResult { attacker = attacker, target = target };

        // Intelecto do defensor tenta esquivar
        if (Random.value < DodgeChance(target.points))
        {
            r.dodged = true;
            return r;
        }

        // Força sorteia o dano
        float dmg = Random.Range(MinDamage(attacker), MaxDamage(attacker) + 1);

        // Destreza verifica o crítico
        if (Random.value < CritChance(attacker.points))
        {
            r.critical = true;
            dmg *= CritMultiplier;
        }

        // Raiva aplica bônus se o atacante estiver com menos de 30% de vida
        if (attacker.IsFurious && attacker.points.Raiva > 0)
        {
            r.furyActive = true;
            dmg *= 1f + FuryBonus(attacker.points);
        }

        // Penalidade de equilíbrio (primeiro turno de quem abre a batalha)
        dmg *= damageMultiplier;

        r.damage = Mathf.RoundToInt(dmg);
        target.TakeDamage(r.damage);
        r.targetDefeated = !target.IsAlive;
        return r;
    }
}