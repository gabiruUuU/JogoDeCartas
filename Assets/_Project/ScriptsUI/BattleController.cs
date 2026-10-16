using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleController : MonoBehaviour
{
    [Header("Times (índice 0 = Jogador 1, 1 = Jogador 2)")]
    [SerializeField] RectTransform[] teamParents = new RectTransform[2];
    [SerializeField] Image[] teamPanels = new Image[2];
    [SerializeField] CharacterSlotView slotPrefab;

    [Header("UI")]
    [SerializeField] TMP_Text turnLabel;
    [SerializeField] TMP_Text logLabel;
    [SerializeField] Button attackButton;
    [SerializeField] TMP_Text attackButtonLabel;
    [SerializeField] FloatingTextPool floatingTexts;

    static readonly Color HitColor = Color.white;
    static readonly Color CritColor = new Color(1f, 0.85f, 0.15f);
    static readonly Color DodgeColor = new Color(0.45f, 0.85f, 1f);
    static readonly Color FuryColor = new Color(1f, 0.35f, 0.25f);
    static readonly Color PanelIdle = new Color(1f, 1f, 1f, 0.06f);
    const int MaxLogLines = 4;

    readonly List<CharacterSlotView>[] slots = { new List<CharacterSlotView>(), new List<CharacterSlotView>() };
    readonly Queue<string> log = new();

    int turnPlayer;
    int turnNumber;
    bool inputLocked;
    CharacterSlotView selectedAttacker;
    CharacterSlotView selectedTarget;

    void Start()
    {
        attackButton.onClick.AddListener(OnAttack);
        ScreenManager.Instance.ScreenShown += OnScreenShown;
    }

    void OnDestroy()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.ScreenShown -= OnScreenShown;
    }

    void OnScreenShown(ScreenId id)
    {
        if (id == ScreenId.Battle) Begin();
    }

    // ---------- Início ----------
    void Begin()
    {
        StopAllCoroutines();
        var session = GameSession.Instance;

        for (int p = 0; p < GameSession.PlayersCount; p++)
        {
            EnsureSlots(p, session.teams[p].Count);
            for (int i = 0; i < session.teams[p].Count; i++)
                slots[p][i].Setup(session.teams[p][i], p);
        }

        log.Clear();
        UpdateLog();
        turnNumber = 0;
        StartTurn();
    }

    void EnsureSlots(int player, int count)
    {
        while (slots[player].Count < count)
        {
            CharacterSlotView s = Instantiate(slotPrefab, teamParents[player], false);
            s.Clicked += OnSlotClicked;
            slots[player].Add(s);
        }
    }

    // ---------- Turnos ----------
    void StartTurn()
    {
        turnNumber++;
        var teams = GameSession.Instance.teams;

        // 1. Velocidade decide quem ataca no turno
        turnPlayer = CombatRules.PlayerAGoesFirst(Weight(teams[0]), Weight(teams[1])) ? 0 : 1;

        selectedAttacker = null;
        selectedTarget = null;
        inputLocked = false;

        // Se só existe uma opção, já deixa escolhida
        var mine = AliveSlots(turnPlayer);
        var enemy = AliveSlots(1 - turnPlayer);
        if (mine.Count == 1) selectedAttacker = mine[0];
        if (enemy.Count == 1) selectedTarget = enemy[0];

        RefreshTurnUI();
    }

    static int Weight(List<CharacterRuntime> team)
    {
        int w = 0;
        foreach (var c in team) if (c.IsAlive) w += c.points.Velocidade;
        return w;
    }

    List<CharacterSlotView> AliveSlots(int player)
    {
        var list = new List<CharacterSlotView>();
        foreach (var s in slots[player]) if (s.Character.IsAlive) list.Add(s);
        return list;
    }

    bool TeamAlive(int player)
    {
        foreach (var s in slots[player]) if (s.Character.IsAlive) return true;
        return false;
    }

    void OnSlotClicked(CharacterSlotView slot)
    {
        if (inputLocked || !slot.Character.IsAlive) return;

        if (slot.PlayerIndex == turnPlayer) selectedAttacker = slot;   // meu personagem = atacante
        else selectedTarget = slot;                                     // inimigo = alvo
        RefreshTurnUI();
    }

    void RefreshTurnUI()
    {
        turnLabel.text = $"Turno {turnNumber} - Vez do Jogador {turnPlayer + 1}";
        turnLabel.color = PlayerPalette.All[turnPlayer];

        for (int p = 0; p < teamPanels.Length; p++)
        {
            Color c = PlayerPalette.All[p];
            c.a = 0.45f;
            teamPanels[p].color = p == turnPlayer ? c : PanelIdle;
        }

        foreach (var list in slots)
            foreach (var s in list)
                s.SetHighlight(s == selectedAttacker ? SlotHighlight.Attacker
                             : s == selectedTarget ? SlotHighlight.Target
                             : SlotHighlight.None);

        bool ready = selectedAttacker != null && selectedTarget != null;
        attackButton.interactable = ready && !inputLocked;
        attackButtonLabel.text = selectedAttacker == null ? "Escolha seu atacante"
                               : selectedTarget == null ? "Escolha o alvo"
                               : "ATACAR!";
    }

    // ---------- Ataque ----------
    void OnAttack()
    {
        if (inputLocked || selectedAttacker == null || selectedTarget == null) return;

        inputLocked = true;
        attackButton.interactable = false;

        CharacterSlotView atk = selectedAttacker;
        CharacterSlotView tgt = selectedTarget;

        // 2 a 5: esquiva, dano, crítico, fúria (regras em CombatRules)
        AttackResult r = CombatRules.Resolve(atk.Character, tgt.Character);

        GameSession.Instance.TotalDamage[turnPlayer] += r.damage;
        ShowFeedback(r, atk, tgt);
        AddLog(r);
        StartCoroutine(AfterAttack(r));
    }

    void ShowFeedback(AttackResult r, CharacterSlotView atk, CharacterSlotView tgt)
    {
        if (r.furyActive)
            floatingTexts.Show(atk.FloatAnchor, "Fúria!", FuryColor, 54);

        if (r.dodged)
            floatingTexts.Show(tgt.FloatAnchor, "Esquivou!", DodgeColor, 64);
        else if (r.critical)
            floatingTexts.Show(tgt.FloatAnchor, $"-{r.damage}!", CritColor, 120);
        else
            floatingTexts.Show(tgt.FloatAnchor, $"-{r.damage}", HitColor, 72);
    }

    void AddLog(AttackResult r)
    {
        string a = r.attacker.data.cardName;
        string t = r.target.data.cardName;

        string line;
        if (r.dodged) line = $"{a} atacou, mas {t} esquivou!";
        else if (r.critical) line = $"{a} deu CRÍTICO em {t}: {r.damage} de dano!";
        else line = $"{a} causou {r.damage} de dano em {t}.";

        if (r.furyActive) line += " (fúria)";
        if (r.targetDefeated) line += $" {t} foi derrotado!";

        string hex = ColorUtility.ToHtmlStringRGB(PlayerPalette.All[turnPlayer]);
        log.Enqueue($"<color=#{hex}>{line}</color>");
        while (log.Count > MaxLogLines) log.Dequeue();
        UpdateLog();
    }

    void UpdateLog() => logLabel.text = string.Join("\n", log);

    IEnumerator AfterAttack(AttackResult r)
    {
        yield return new WaitForSeconds(r.targetDefeated ? 1.1f : 0.9f);

        bool p1Alive = TeamAlive(0);
        bool p2Alive = TeamAlive(1);

        if (p1Alive && p2Alive)
        {
            StartTurn();
            yield break;
        }

        var session = GameSession.Instance;
        session.Winner = p1Alive ? 0 : 1;
        session.TurnsPlayed = turnNumber;
        ScreenManager.Instance.Show(ScreenId.Result);
    }
}