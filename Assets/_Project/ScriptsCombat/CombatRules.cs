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
    // Valores numéricos ajustáveis (as regras em si são fixas)
    public const int DamagePerStrength = 2;
    public const float DodgePerPoint = 0.03f, DodgeCap = 0.30f;
    public const float CritPerPoint = 0.07f, CritCap = 0.70f, CritMultiplier = 1.8f;
    public const float FuryPerPoint = 0.06f;

    // ----- Prévias (tela de distribuição de pontos) -----
    public static int MinDamage(CharacterRuntime c) => c.data.minDamage + c.points.Forca * DamagePerStrength;
    public static int MaxDamage(CharacterRuntime c) => c.data.maxDamage + c.points.Forca * DamagePerStrength;
    public static float DodgeChance(AttributePoints p) => Mathf.Min(DodgeCap, p.Intelecto * DodgePerPoint);
    public static float CritChance(AttributePoints p) => Mathf.Min(CritCap, p.Destreza * CritPerPoint);
    public static float FuryBonus(AttributePoints p) => p.Raiva * FuryPerPoint;

    // ----- 1. Velocidade decide quem ataca -----
    // Pesos = soma da velocidade dos personagens vivos de cada jogador
    public static bool PlayerAGoesFirst(int weightA, int weightB)
    {
        int total = weightA + weightB;
        if (total <= 0) return Random.value < 0.5f;
        return Random.Range(0, total) < weightA;
    }

    public static int PlayerWeight(CharacterRuntime a, CharacterRuntime b)
    {
        int w = 0;
        if (a != null && a.IsAlive) w += a.points.Velocidade;
        if (b != null && b.IsAlive) w += b.points.Velocidade;
        return w;
    }

    // ----- 2 a 5. Resolução do ataque -----
    public static AttackResult Resolve(CharacterRuntime attacker, CharacterRuntime target)
    {
        var r = new AttackResult { attacker = attacker, target = target };

        // 2. Intelecto do defensor tenta esquivar
        if (Random.value < DodgeChance(target.points))
        {
            r.dodged = true;
            return r;
        }

        // 3. Força sorteia o dano
        float dmg = Random.Range(MinDamage(attacker), MaxDamage(attacker) + 1);

        // 4. Destreza verifica o crítico
        if (Random.value < CritChance(attacker.points))
        {
            r.critical = true;
            dmg *= CritMultiplier;
        }

        // 5. Raiva aplica bônus se o atacante estiver ferido (<30% de vida)
        if (attacker.IsFurious && attacker.points.Raiva > 0)
        {
            r.furyActive = true;
            dmg *= 1f + FuryBonus(attacker.points);
        }

        r.damage = Mathf.RoundToInt(dmg);
        target.TakeDamage(r.damage);
        r.targetDefeated = !target.IsAlive;
        return r;
    }
}