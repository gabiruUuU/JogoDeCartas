using System;
using System.Collections.Generic;

// Estado de um personagem em partida. O ScriptableObject (data) nunca é modificado.
public class CharacterRuntime
{
    public readonly CardData data;
    public readonly AttributePoints points = new AttributePoints();

    public int CurrentHealth { get; private set; }
    public int MaxHealth => data.health;
    public bool IsAlive => CurrentHealth > 0;
    public bool IsFurious => IsAlive && CurrentHealth < MaxHealth * 0.3f;
    public float HealthPercent => (float)CurrentHealth / MaxHealth;

    // ----- Estado das habilidades (existe só durante a partida) -----
    public int AccumulatedDamage { get; private set; }   // Chato
    public int AccumulatedStacks { get; private set; }
    public int SleepTurnsLeft { get; set; }               // Dorminhoco
    public bool IsAsleep => SleepTurnsLeft > 0;
    public bool SkipNextAction { get; set; }              // carta comprada pelo Rico
    public float SadSkipChance { get; set; }              // > 0 = triste (Chorão)
    public bool IsSad => SadSkipChance > 0f;
    public int AbilityCooldownLeft { get; set; }          // Rico

    public event Action<CharacterRuntime> HealthChanged;
    public event Action<CharacterRuntime> Defeated;
    public event Action<CharacterRuntime> StatusChanged;

    public CharacterRuntime(CardData data)
    {
        this.data = data;
        CurrentHealth = data.health;
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return;
        CurrentHealth = Math.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(this);
        if (CurrentHealth == 0)
        {
            ClearStatus();
            Defeated?.Invoke(this);
        }
    }

    public int Heal(int amount)
    {
        if (!IsAlive || amount <= 0) return 0;
        int before = CurrentHealth;
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        int healed = CurrentHealth - before;
        if (healed > 0) HealthChanged?.Invoke(this);
        return healed;
    }

    public int HealToFull() => Heal(MaxHealth);

    public void Accumulate(int damage, int maxStacks)
    {
        if (AccumulatedStacks >= maxStacks) return;
        AccumulatedDamage += damage;
        AccumulatedStacks++;
        NotifyStatusChanged();
    }

    public int ConsumeAccumulated()
    {
        int value = AccumulatedDamage;
        if (AccumulatedStacks > 0)
        {
            AccumulatedDamage = 0;
            AccumulatedStacks = 0;
            NotifyStatusChanged();
        }
        return value;
    }

    public void NotifyStatusChanged() => StatusChanged?.Invoke(this);

    void ClearStatus()
    {
        AccumulatedDamage = 0;
        AccumulatedStacks = 0;
        SleepTurnsLeft = 0;
        SkipNextAction = false;
        NotifyStatusChanged();
    }

    // Texto curto mostrado na carta
    public string StatusText
    {
        get
        {
            if (!IsAlive) return "";
            var parts = new List<string>();
            if (IsAsleep) parts.Add("ZZZ");
            if (data.ability == AbilityType.Shield) parts.Add("ESCUDO");
            if (IsSad) parts.Add("TRISTE");
            if (AccumulatedStacks > 0) parts.Add($"CARGA x{AccumulatedStacks}");
            if (SkipNextAction) parts.Add("COMPRADO");
            return string.Join("  ", parts);
        }
    }
}