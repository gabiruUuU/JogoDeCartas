using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleController : MonoBehaviour
{
    [Header("Times (índice 0 = Jogador 1 embaixo, 1 = Jogador 2 em cima, virado)")]
    [SerializeField] RectTransform[] teamParents = new RectTransform[2];
    [SerializeField] Image[] teamPanels = new Image[2];
    [SerializeField] CharacterSlotView slotPrefab;

    [Header("UI (índice 0 = Jogador 1, 1 = Jogador 2)")]
    [SerializeField] TMP_Text[] statusLabels = new TMP_Text[2];
    [SerializeField] TMP_Text[] logLabels = new TMP_Text[2];
    [SerializeField] Button attackButton;
    [SerializeField] FloatingTextPool floatingTexts;
    [SerializeField] CardInfoPopup infoPopup;

    [Header("Regras e registro")]
    [Tooltip("Desligado: quem abre a batalha é sorteado uma vez e os jogadores se alternam. Ligado: sorteia de novo a cada rodada.")]
    [SerializeField] bool redrawStarterEveryRound = false;
    [Tooltip("Desligado: cada log mostra só os ataques feitos pelas próprias cartas. Ligado: também mostra os ataques recebidos.")]
    [SerializeField] bool logReceivedAttacks = false;

    static readonly Color HitColor = Color.white;
    static readonly Color CritColor = new Color(1f, 0.85f, 0.15f);
    static readonly Color DodgeColor = new Color(0.45f, 0.85f, 1f);
    static readonly Color FuryColor = new Color(1f, 0.35f, 0.25f);
    static readonly Color ExtraColor = new Color(1f, 0.92f, 0.40f);
    static readonly Color PanelIdle = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color StatusIdle = new Color(1f, 1f, 1f, 0.55f);
    const int MaxLogLines = 3;

    readonly List<CharacterSlotView>[] slots = { new List<CharacterSlotView>(), new List<CharacterSlotView>() };
    readonly Queue<string>[] logs = { new Queue<string>(), new Queue<string>() };

    int starterPlayer;      // quem abre cada rodada
    int roundNumber;
    int attackCount;        // total de ataques = "turnos jogados" do Resultado
    int turnPlayer;         // jogador da vez
    int turnStep;           // 0 = primeiro jogador da rodada, 1 = segundo
    int cardIndex;          // carta que está atacando (0 ou 1)
    int attacksByCard;      // ataques já feitos pela carta atual
    bool inputLocked;

    CharacterSlotView currentAttacker;
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
        infoPopup.Hide();
        var session = GameSession.Instance;

        for (int p = 0; p < GameSession.PlayersCount; p++)
        {
            EnsureSlots(p, session.teams[p].Count);
            for (int i = 0; i < session.teams[p].Count; i++)
                slots[p][i].Setup(session.teams[p][i], p);
            logs[p].Clear();
        }

        UpdateLogs();
        roundNumber = 0;
        attackCount = 0;
        starterPlayer = DrawStarter();
        BeginRound();
    }

    void EnsureSlots(int player, int count)
    {
        while (slots[player].Count < count)
        {
            CharacterSlotView s = Instantiate(slotPrefab, teamParents[player], false);
            s.Clicked += OnSlotClicked;
            s.InfoClicked += OnSlotInfo;
            slots[player].Add(s);
        }
    }

    // Sorteio do PDF: peso = soma das velocidades dos personagens vivos
    int DrawStarter()
    {
        var t = GameSession.Instance.teams;
        return CombatRules.PlayerAGoesFirst(Weight(t[0]), Weight(t[1])) ? 0 : 1;
    }

    static int Weight(List<CharacterRuntime> team)
    {
        int w = 0;
        foreach (var c in team) if (c.IsAlive) w += CombatRules.TurnWeight(c);
        return w;
    }

    // ---------- Fluxo: rodada -> turno do jogador -> carta -> ataques ----------
    void BeginRound()
    {
        roundNumber++;
        if (roundNumber > 1 && redrawStarterEveryRound) starterPlayer = DrawStarter();
        turnStep = 0;
        StartPlayerTurn(starterPlayer);
    }

    void StartPlayerTurn(int player)
    {
        turnPlayer = player;
        cardIndex = -1;
        AdvanceToNextCard();
    }

    void AdvanceToNextCard()
    {
        var team = slots[turnPlayer];
        do { cardIndex++; }
        while (cardIndex < team.Count && !team[cardIndex].Character.IsAlive);

        if (cardIndex >= team.Count)       // as cartas deste jogador acabaram
        {
            EndPlayerTurn();
            return;
        }

        attacksByCard = 0;
        BeginAttackPrompt();
    }

    void EndPlayerTurn()
    {
        if (turnStep == 0)
        {
            turnStep = 1;
            StartPlayerTurn(1 - starterPlayer);
        }
        else
        {
            BeginRound();
        }
    }

    // Prepara um ataque da carta atual: atacante fixo, jogador só escolhe o alvo
    void BeginAttackPrompt()
    {
        inputLocked = false;
        currentAttacker = slots[turnPlayer][cardIndex];
        selectedTarget = null;

        var enemies = AliveSlots(1 - turnPlayer);
        if (enemies.Count == 1) selectedTarget = enemies[0];

        RefreshTurnUI();
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
        if (slot.PlayerIndex == turnPlayer) return;     // o atacante é automático

        selectedTarget = slot;
        RefreshTurnUI();
    }

    // Botão (i): mostra os atributos de qualquer carta, a qualquer momento
    void OnSlotInfo(CharacterSlotView slot)
    {
        infoPopup.Show(slot.Character, slot.PlayerIndex);
    }

    void RefreshTurnUI()
    {
        string cardName = currentAttacker.Character.data.cardName;
        string hint = selectedTarget == null ? "Escolha o alvo" : "Toque em atacar";

        for (int p = 0; p < 2; p++)
        {
            if (p == turnPlayer)
            {
                statusLabels[p].text =
                    $"<b>SUA VEZ</b> - Rodada {roundNumber}\n" +
                    $"{cardName} ataca ({attacksByCard + 1}/{CombatRules.MaxAttacksPerCard})\n" +
                    $"<b>{hint}</b>";
                statusLabels[p].color = PlayerPalette.All[p];
            }
            else
            {
                statusLabels[p].text = $"Vez do adversário\nRodada {roundNumber}";
                statusLabels[p].color = StatusIdle;
            }

            Color c = PlayerPalette.All[p];
            c.a = 0.45f;
            teamPanels[p].color = p == turnPlayer ? c : PanelIdle;
        }

        foreach (var list in slots)
            foreach (var s in list)
                s.SetHighlight(s == currentAttacker ? SlotHighlight.Attacker
                             : s == selectedTarget ? SlotHighlight.Target
                             : SlotHighlight.None);

        attackButton.interactable = selectedTarget != null && !inputLocked;
    }

    // ---------- Ataque ----------
    void OnAttack()
    {
        if (inputLocked || currentAttacker == null || selectedTarget == null) return;

        inputLocked = true;
        attackButton.interactable = false;

        CharacterSlotView atk = currentAttacker;
        CharacterSlotView tgt = selectedTarget;

        // esquiva, dano, crítico e fúria (regras em CombatRules)
        AttackResult r = CombatRules.Resolve(atk.Character, tgt.Character);

        attackCount++;
        GameSession.Instance.TotalDamage[turnPlayer] += r.damage;
        ShowFeedback(r, atk, tgt);
        AddLog(r);
        StartCoroutine(AfterAttack(r, atk));
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
        PushLog(line);
    }

    // O log do jogador da vez recebe o ataque; o do adversário só se logReceivedAttacks estiver ligado
    void PushLog(string line)
    {
        string hex = ColorUtility.ToHtmlStringRGB(PlayerPalette.All[turnPlayer]);
        string colored = $"<color=#{hex}>{line}</color>";

        AddToLog(turnPlayer, colored);
        if (logReceivedAttacks) AddToLog(1 - turnPlayer, colored);
        UpdateLogs();
    }

    void AddToLog(int player, string line)
    {
        logs[player].Enqueue(line);
        while (logs[player].Count > MaxLogLines) logs[player].Dequeue();
    }

    void UpdateLogs()
    {
        for (int p = 0; p < 2; p++)
            logLabels[p].text = string.Join("\n", logs[p]);
    }

    IEnumerator AfterAttack(AttackResult r, CharacterSlotView atk)
    {
        yield return new WaitForSeconds(r.targetDefeated ? 1.1f : 0.9f);

        bool p1Alive = TeamAlive(0);
        bool p2Alive = TeamAlive(1);

        if (!p1Alive || !p2Alive)                 // fim da partida
        {
            var session = GameSession.Instance;
            session.Winner = p1Alive ? 0 : 1;
            session.TurnsPlayed = attackCount;
            ScreenManager.Instance.Show(ScreenId.Result);
            yield break;
        }

        attacksByCard++;
        bool again = attacksByCard < CombatRules.MaxAttacksPerCard
                     && CombatRules.RollExtraAttack(atk.Character);

        if (again)
        {
            floatingTexts.Show(atk.FloatAnchor, "Ataca de novo!", ExtraColor, 56);
            PushLog($"{atk.Character.data.cardName} é rápido e ataca de novo!");
            BeginAttackPrompt();                  // mesma carta, novo alvo
        }
        else
        {
            AdvanceToNextCard();                  // próxima carta (ou próximo jogador)
        }
    }
}