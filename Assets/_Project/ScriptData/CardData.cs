using UnityEngine;

[CreateAssetMenu(fileName = "Card_", menuName = "Batalha/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName;
    public Sprite art;
    [TextArea(10, 30)] public string story;   // texto longo, pra exigir rolagem
    public int health = 100;
    public int minDamage = 10;
    public int maxDamage = 20;
}