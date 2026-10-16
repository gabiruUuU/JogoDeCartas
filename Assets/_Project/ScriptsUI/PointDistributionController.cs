using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PointDistributionController : MonoBehaviour
{
    [SerializeField] TMP_Text headerLabel;
    [SerializeField] Image characterArt;
    [SerializeField] TMP_Text characterName;
    [SerializeField] TMP_Text remainingLabel;
    [SerializeField] RectTransform rowsParent;
    [SerializeField] AttributeRowView rowPrefab;
    [SerializeField] Button confirmButton;
    [SerializeField] TMP_Text confirmLabel;

    static readonly Attribute[] Order =
        { Attribute.Forca, Attribute.Velocidade, Attribute.Intelecto, Attribute.Destreza, Attribute.Raiva };
    static readonly string[] Names = { "Força", "Velocidade", "Intelecto", "Destreza", "Raiva" };

    readonly List<AttributeRowView> rows = new();
    readonly List<CharacterRuntime> steps = new();   // ordem: J1-p1, J1-p2, J2-p1, J2-p2
    int step;

    CharacterRuntime Current => steps[step];

    void Start()
    {
        confirmButton.onClick.AddListener(OnConfirm);
        ScreenManager.Instance.ScreenShown += OnScreenShown;
    }

    void OnDestroy()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.ScreenShown -= OnScreenShown;
    }

    void OnScreenShown(ScreenId id)
    {
        if (id == ScreenId.PointDistribution) Begin();
    }

    void Begin()
    {
        var session = GameSession.Instance;
        session.BuildTeams();

        steps.Clear();
        for (int p = 0; p < GameSession.PlayersCount; p++) steps.AddRange(session.teams[p]);

        BuildRowsOnce();
        step = 0;
        ShowStep();
    }

    void BuildRowsOnce()
    {
        if (rows.Count > 0) return;
        for (int i = 0; i < Order.Length; i++)
        {
            AttributeRowView row = Instantiate(rowPrefab, rowsParent, false);
            row.Setup(Order[i], Names[i]);
            row.Changed += OnRowChanged;
            rows.Add(row);
        }
    }

    void ShowStep()
    {
        int player = step / GameSession.CardsPerPlayer;
        int number = step % GameSession.CardsPerPlayer + 1;

        headerLabel.text = $"Jogador {player + 1} - Personagem {number}/{GameSession.CardsPerPlayer}";
        headerLabel.color = PlayerPalette.All[player];

        CardData card = Current.data;
        characterName.text = card.cardName;
        characterArt.preserveAspect = true;
        characterArt.sprite = card.art;
        characterArt.color = card.art != null ? Color.white : new Color(0.5f, 0.55f, 0.65f);

        confirmLabel.text = step == steps.Count - 1 ? "Iniciar batalha" : "Confirmar";
        Refresh();
    }

    void OnRowChanged(Attribute attr, int delta)
    {
        var points = Current.points;
        if (delta > 0) points.Add(attr); else points.Remove(attr);
        Refresh();
    }

    void OnConfirm()
    {
        if (!Current.points.IsComplete) return;

        if (step < steps.Count - 1)
        {
            step++;
            ShowStep();
        }
        else
        {
            ScreenManager.Instance.Show(ScreenId.Battle);
        }
    }

    // Só roda quando um valor muda (sem Update)
    void Refresh()
    {
        var c = Current;
        var p = c.points;

        remainingLabel.text = $"Pontos restantes: {p.Remaining}";
        confirmButton.interactable = p.IsComplete;

        for (int i = 0; i < rows.Count; i++)
        {
            Attribute a = Order[i];
            rows[i].Refresh(p.Get(a), p.CanAdd(a), p.CanRemove(a), PreviewFor(a, c));
        }
    }

    static string PreviewFor(Attribute a, CharacterRuntime c)
    {
        var p = c.points;
        switch (a)
        {
            case Attribute.Forca:
                return $"Dano: {CombatRules.MinDamage(c)} a {CombatRules.MaxDamage(c)}";
            case Attribute.Velocidade:
                return $"Iniciativa: peso {p.Velocidade}";
            case Attribute.Intelecto:
                return $"Esquiva: {Pct(CombatRules.DodgeChance(p))}%";
            case Attribute.Destreza:
                return $"Crítico: {Pct(CombatRules.CritChance(p))}% (x{CombatRules.CritMultiplier:0.0})";
            default:
                return $"Fúria (vida < 30%): +{Pct(CombatRules.FuryBonus(p))}% de dano";
        }
    }

    static int Pct(float f) => Mathf.RoundToInt(f * 100f);
}