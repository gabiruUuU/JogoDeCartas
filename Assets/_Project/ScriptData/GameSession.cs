using System.Collections.Generic;
using UnityEngine;

public class GameSession : MonoBehaviour
{
    public const int PlayersCount = 2;
    public const int CardsPerPlayer = 2;

    public static GameSession Instance { get; private set; }

    [SerializeField] CardData[] allCards;   // as 10 cartas
    public IReadOnlyList<CardData> AllCards => allCards;

    // cartas escolhidas por jogador (índice 0 = Jogador 1)
    public List<CardData>[] picks = { new List<CardData>(), new List<CardData>() };

    public CardData ViewedCard { get; set; }   // usada pela tela de Descrição

    void Awake() => Instance = this;

    public List<CharacterRuntime>[] teams =
    { new List<CharacterRuntime>(), new List<CharacterRuntime>() };

    public void BuildTeams()
    {
        for (int p = 0; p < PlayersCount; p++)
        {
            teams[p].Clear();
            foreach (var card in picks[p])
                teams[p].Add(new CharacterRuntime(card));   // os pontos ficam aqui, não no ScriptableObject
        }
    }

    public void ResetSession()
    {
        foreach (var list in picks) list.Clear();
        foreach (var t in teams) t.Clear();              
        ViewedCard = null;
    }

}