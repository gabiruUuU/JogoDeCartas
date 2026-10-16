using System;

// Estrutura de runtime: o ScriptableObject nunca é modificado na partida.
public class CharacterRuntime
{
    public readonly CardData data;
    public readonly AttributePoints points = new AttributePoints();

    public int CurrentHealth { get; private set; }
    public int MaxHealth => data.health;
    public bool IsAlive => CurrentHealth > 0;
    public bool IsFurious => IsAlive && CurrentHealth < MaxHealth * 0.3f;
    public float HealthPercent => (float)CurrentHealth / MaxHealth;

    public event Action<CharacterRuntime> HealthChanged;
    public event Action<CharacterRuntime> Defeated;

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
        if (CurrentHealth == 0) Defeated?.Invoke(this);
    }
}