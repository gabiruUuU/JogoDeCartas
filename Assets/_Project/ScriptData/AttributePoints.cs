public enum Attribute { Forca, Velocidade, Intelecto, Destreza, Raiva }

[System.Serializable]
public class AttributePoints
{
    public const int TotalPoints = 20;
    public const int MaxPerAttribute = 10;

    readonly int[] values = new int[5];

    public int Get(Attribute a) => values[(int)a];

    public int Forca => Get(Attribute.Forca);
    public int Velocidade => Get(Attribute.Velocidade);
    public int Intelecto => Get(Attribute.Intelecto);
    public int Destreza => Get(Attribute.Destreza);
    public int Raiva => Get(Attribute.Raiva);

    public int Spent { get { int s = 0; foreach (var v in values) s += v; return s; } }
    public int Remaining => TotalPoints - Spent;
    public bool IsComplete => Remaining == 0;

    public bool CanAdd(Attribute a) => Remaining > 0 && Get(a) < MaxPerAttribute;
    public bool CanRemove(Attribute a) => Get(a) > 0;

    public bool Add(Attribute a)
    {
        if (!CanAdd(a)) return false;
        values[(int)a]++;
        return true;
    }

    public bool Remove(Attribute a)
    {
        if (!CanRemove(a)) return false;
        values[(int)a]--;
        return true;
    }
}