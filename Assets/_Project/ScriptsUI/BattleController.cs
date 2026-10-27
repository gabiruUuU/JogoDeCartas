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
    [SerializeField] CardFx cardFx;

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
    static readonly Color HealColor = new Color(0.40f, 1f, 0.55f);
    static readonly Color SleepColor = new Color(0.70f, 0.80f, 1f);
    static readonly Color SadColor = new Color(0.60f, 0.70f, 1f);
    static readonly Color MissColor = new Color(0.80f, 0.80f, 0.80f);
    static readonly Color DoubleColor = new Color(1f, 0.60f, 0.10f);
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
    int cardIndex;          // carta que está agindo (0 ou 1)
    int attacksByCard;      // tentativas de ataque já feitas pela carta atual
    bool inputLocked;
    bool buyMode;           // Rico: escolhendo a carta a comprar
    bool inspireUsedThisAction;   // Feliz: o Inspirar só vale uma vez por ação

    CharacterSlotView currentAttacker;
    CharacterSlotView selectedTarget;

    // Primeiro turno da batalha (quem abre causa menos dano, para equilibrar)
    bool OpeningPenaltyActive =>
        roundNumber == 1 && turnStep == 0 && CombatRules.FirstTurnDamageMultiplier < 1f;

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
        cardFx.ResetAll();
        infoPopup.Hide();
        inputLocked = true;
        buyMode = false;

        var session = GameSession.Instance;
        ApplySadness(session);

        for (int p = 0; p < GameSession.PlayersCount; p++)
        {
            EnsureSlots(p, session.teams[p].Count);
            for (int i = 0; i < session.teams[p].Count; i++)
                slots[p][i].Setup(session.teams[p][i], p);
            logs[p].Clear();
        }

        // Feio: o medo dura enquanto ele estiver em campo
        foreach (var team in session.teams)
            foreach (var c in team)
                if (c.data.ability == AbilityType.Fear)
                {
                    c.Defeated -= OnFearSourceDefeated;
                    c.Defeated += OnFearSourceDefeated;
                }
        RecalculateFear();

        UpdateLogs();
        roundNumber = 0;
        attackCount = 0;
        StartCoroutine(BeginRoutine());
    }

    // Animação de abertura: as cartas saem do baralho (botão central) até a mão de cada jogador
    IEnumerator BeginRoutine()
    {
        attackButton.interactable = false;
        for (int p = 0; p < 2; p++)
        {
            statusLabels[p].text = "Distribuindo as cartas...";
            statusLabels[p].color = StatusIdle;
            teamPanels[p].color = PanelIdle;
        }

        // esconde as cartas e entrega intercalando os jogadores
        var order = new List<CharacterSlotView>();
        int max = Mathf.Max(slots[0].Count, slots[1].Count);
        for (int i = 0; i < max; i++)
        {
            for (int p = 0; p < 2; p++)
                if (i < slots[p].Count)
                {
                    slots[p][i].SetVisualVisible(false);
                    order.Add(slots[p][i]);
                }
        }

        yield return cardFx.PlayDealIn(order);

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
            s.AbilityClicked += OnAbilityClicked;
            slots[player].Add(s);
        }
    }

    // Chorão: as cartas do time inimigo ficam tristes durante toda a batalha
    static void ApplySadness(GameSession session)
    {
        for (int p = 0; p < GameSession.PlayersCount; p++)
            foreach (var c in session.teams[p])
            {
                if (c.data.ability != AbilityType.Sadden) continue;
                foreach (var enemy in session.teams[1 - p])
                    enemy.SadSkipChance = Mathf.Max(enemy.SadSkipChance, c.data.abilityChance);
            }
    }

    void OnFearSourceDefeated(CharacterRuntime c) => RecalculateFear();

    // Cada carta fica com medo se houver um Feio vivo no time inimigo
    void RecalculateFear()
    {
        var teams = GameSession.Instance.teams;
        for (int p = 0; p < GameSession.PlayersCount; p++)
        {
            float chance = 0f;
            foreach (var source in teams[1 - p])
                if (source.IsAlive && source.data.ability == AbilityType.Fear)
                    chance = Mathf.Max(chance, source.data.abilityChance);

            foreach (var c in teams[p])
            {
                if (Mathf.Approximately(c.FearChance, chance)) continue;
                c.FearChance = chance;
                c.NotifyStatusChanged();
            }
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

        // Termina a ação da carta anterior (o bônus do Feliz vale só para uma ação)
        if (cardIndex >= 0 && cardIndex < team.Count) EndCardAction(team[cardIndex]);

        do { cardIndex++; }
        while (cardIndex < team.Count && !team[cardIndex].Character.IsAlive);

        if (cardIndex >= team.Count)       // as cartas deste jogador acabaram
        {
            EndPlayerTurn();
            return;
        }

        BeginCardAction();
    }

    static void EndCardAction(CharacterSlotView slot)
    {
        CharacterRuntime c = slot.Character;
        if (!c.BuffActive) return;

        c.BuffActive = false;
        c.DamageBuff = 1f;
        c.NotifyStatusChanged();
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

    // Efeitos que acontecem uma vez, no começo da ação de cada carta
    void BeginCardAction()
    {
        attacksByCard = 0;
        buyMode = false;
        inspireUsedThisAction = false;

        CharacterSlotView slot = slots[turnPlayer][cardIndex];
        CharacterRuntime c = slot.Character;
        string cardName = c.data.cardName;

        currentAttacker = slot;
        selectedTarget = null;

        if (c.AbilityCooldownLeft > 0) c.AbilityCooldownLeft--;

        // Dorminhoco: dormindo, perde a ação; ao acordar, recupera toda a vida
        if (c.IsAsleep)
        {
            c.SleepTurnsLeft--;
            if (c.SleepTurnsLeft <= 0)
            {
                int healed = c.HealToFull();
                floatingTexts.Show(slot.FloatAnchor, $"Acordou! +{healed}", HealColor, 60);
                PushLog($"{cardName} acordou e recuperou toda a vida!");
            }
            else
            {
                floatingTexts.Show(slot.FloatAnchor, "Zzz...", SleepColor, 64);
                PushLog($"{cardName} continua dormindo.");
            }
            c.NotifyStatusChanged();
            LockAndAdvance(1.1f);
            return;
        }

        // Rico: carta comprada perde esta ação
        if (c.SkipNextAction)
        {
            c.SkipNextAction = false;
            c.NotifyStatusChanged();
            floatingTexts.Show(slot.FloatAnchor, "Perdeu a vez", SleepColor, 56);
            PushLog($"{cardName} foi comprado e perdeu a vez.");
            LockAndAdvance(1.1f);
            return;
        }

        // Lindo: se cura no começo da vez (mais quanto menor a vida)
        if (c.data.ability == AbilityType.SelfHeal)
        {
            int healed = c.Heal(CombatRules.SelfHealAmount(c));
            if (healed > 0)
            {
                floatingTexts.Show(slot.FloatAnchor, $"+{healed}", HealColor, 72);
                PushLog($"{cardName} se curou em {healed}.");
            }
        }

        // Feliz: o bônus de dano recebido passa a valer nesta ação
        if (c.BuffPending)
        {
            c.BuffPending = false;
            c.BuffActive = true;
            c.NotifyStatusChanged();
        }

        BeginAttackPrompt();
    }

    void LockAndAdvance(float delay)
    {
        inputLocked = true;
        RefreshTurnUI();
        StartCoroutine(AdvanceAfter(delay));
    }

    IEnumerator AdvanceAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        AdvanceToNextCard();
    }

    // Prepara uma tentativa de ataque da carta atual
    void BeginAttackPrompt()
    {
        inputLocked = false;
        buyMode = false;
        currentAttacker = slots[turnPlayer][cardIndex];
        selectedTarget = null;

        CharacterRuntime c = currentAttacker.Character;

        // Chorão: carta triste tem chance de não atacar nesta tentativa
        if (c.IsSad && Random.value < c.SadSkipChance)
        {
            floatingTexts.Show(currentAttacker.FloatAnchor, "Triste...", SadColor, 60);
            PushLog($"{c.data.cardName} está triste e não atacou.");
            inputLocked = true;
            RefreshTurnUI();
            StartCoroutine(AfterFailedAttempt(currentAttacker));
            return;
        }

        var enemies = AliveSlots(1 - turnPlayer);
        if (enemies.Count == 1) selectedTarget = enemies[0];

        RefreshTurnUI();
    }

    IEnumerator AfterFailedAttempt(CharacterSlotView atk)
    {
        yield return new WaitForSeconds(0.9f);
        ContinueAfterAttempt(atk, true);
    }

    // Depois de cada tentativa: sorteia ataque extra (velocidade) ou passa para a próxima carta
    void ContinueAfterAttempt(CharacterSlotView atk, bool allowExtra)
    {
        attacksByCard++;
        bool again = allowExtra
                     && attacksByCard < CombatRules.MaxAttacksPerCard
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

    // Outra carta viva do mesmo time (a "carta aliada" do Feliz)
    CharacterSlotView Ally(CharacterSlotView s)
    {
        foreach (var other in slots[s.PlayerIndex])
            if (other != s && other.Character.IsAlive) return other;
        return null;
    }

    // ---------- Toques ----------
    void OnSlotClicked(CharacterSlotView slot)
    {
        if (inputLocked || !slot.Character.IsAlive) return;
        if (slot.PlayerIndex == turnPlayer) return;          // o atacante é automático
        if (buyMode && !IsValidBuyTarget(slot)) return;

        selectedTarget = slot;
        RefreshTurnUI();
    }

    // Botão (i): mostra os atributos de qualquer carta, a qualquer momento
    void OnSlotInfo(CharacterSlotView slot)
    {
        infoPopup.Show(slot.Character, slot.PlayerIndex);
    }

    void OnAbilityClicked(CharacterSlotView slot)
    {
        if (inputLocked || slot != currentAttacker) return;

        switch (slot.Character.data.ability)
        {
            case AbilityType.Accumulate: DoAccumulate(); break;
            case AbilityType.Sleep: DoSleep(); break;
            case AbilityType.Buy: ToggleBuyMode(); break;
            case AbilityType.Inspire: DoInspire(); break;
        }
    }

    // ---------- Habilidades ativas ----------
    bool CanUseAbility(CharacterSlotView s)
    {
        CharacterRuntime c = s.Character;
        switch (c.data.ability)
        {
            case AbilityType.Accumulate: return c.AccumulatedStacks < Mathf.Max(1, c.data.abilityTurns);
            case AbilityType.Sleep: return true;
            case AbilityType.Buy: return c.AbilityCooldownLeft <= 0 && HasBuyTarget();
            case AbilityType.Inspire:
                return !inspireUsedThisAction && c.AbilityCooldownLeft <= 0 && Ally(s) != null;
            default: return false;
        }
    }

    // Texto do botão da habilidade (mostra a recarga restante quando houver)
    string AbilityButtonLabel(CharacterSlotView s)
    {
        if (buyMode) return "Cancelar";

        CharacterRuntime c = s.Character;
        string name = c.data.AbilityName;
        bool hasCooldown = c.data.ability == AbilityType.Buy || c.data.ability == AbilityType.Inspire;
        return hasCooldown && c.AbilityCooldownLeft > 0 ? $"{name} ({c.AbilityCooldownLeft})" : name;
    }

    // Rico só pode comprar uma carta acordada que tenha outra carta viva no próprio time para ser atacada
    bool IsValidBuyTarget(CharacterSlotView s)
    {
        if (s.PlayerIndex == turnPlayer) return false;
        CharacterRuntime c = s.Character;
        if (!c.IsAlive || c.IsAsleep) return false;

        foreach (var other in slots[s.PlayerIndex])
            if (other != s && other.Character.IsAlive) return true;
        return false;
    }

    bool HasBuyTarget()
    {
        foreach (var s in slots[1 - turnPlayer])
            if (IsValidBuyTarget(s)) return true;
        return false;
    }

    void ToggleBuyMode()
    {
        if (!buyMode && !CanUseAbility(currentAttacker)) return;

        buyMode = !buyMode;
        selectedTarget = null;

        if (buyMode)
        {
            CharacterSlotView only = null;
            int count = 0;
            foreach (var s in slots[1 - turnPlayer])
                if (IsValidBuyTarget(s)) { only = s; count++; }
            if (count == 1) selectedTarget = only;
        }
        else
        {
            var enemies = AliveSlots(1 - turnPlayer);
            if (enemies.Count == 1) selectedTarget = enemies[0];
        }

        RefreshTurnUI();
    }

    // Chato: em vez de atacar, guarda um sorteio de dano para soltar depois
    void DoAccumulate()
    {
        CharacterSlotView slot = currentAttacker;
        CharacterRuntime c = slot.Character;
        if (!CanUseAbility(slot)) return;

        inputLocked = true;
        int roll = CombatRules.RollBaseDamage(c);
        c.Accumulate(roll, Mathf.Max(1, c.data.abilityTurns));

        floatingTexts.Show(slot.FloatAnchor, $"Acumulou +{roll}", ExtraColor, 56);
        PushLog($"{c.data.cardName} não atacou e acumulou dano (total {c.AccumulatedDamage}).");
        RefreshTurnUI();
        StartCoroutine(AdvanceAfter(1.0f));
    }

    // Dorminhoco: dorme (esta ação conta como a 1ª rodada) e acorda curado
    void DoSleep()
    {
        CharacterSlotView slot = currentAttacker;
        CharacterRuntime c = slot.Character;

        inputLocked = true;
        c.SleepTurnsLeft = Mathf.Max(1, c.data.abilityTurns - 1);
        c.NotifyStatusChanged();

        floatingTexts.Show(slot.FloatAnchor, "Zzz...", SleepColor, 64);
        PushLog($"{c.data.cardName} foi dormir e vai acordar curado.");
        RefreshTurnUI();
        StartCoroutine(AdvanceAfter(1.0f));
    }

    // Feliz: dá +X% de dano à carta aliada na próxima ação dela. Não gasta o ataque do Feliz
    void DoInspire()
    {
        CharacterSlotView slot = currentAttacker;
        CharacterSlotView ally = Ally(slot);
        if (ally == null || !CanUseAbility(slot)) return;

        CharacterRuntime c = slot.Character;
        inspireUsedThisAction = true;
        c.AbilityCooldownLeft = Mathf.Max(1, c.data.abilityTurns);

        ally.Character.DamageBuff = 1f + c.data.abilityAmount;     // não acumula: só renova o bônus
        ally.Character.BuffPending = true;
        ally.Character.NotifyStatusChanged();

        int percent = Mathf.RoundToInt(c.data.abilityAmount * 100f);
        floatingTexts.Show(ally.FloatAnchor, $"+{percent}% dano!", ExtraColor, 60);
        PushLog($"{c.data.cardName} inspirou {ally.Character.data.cardName}: +{percent}% de dano na próxima ação.");
        RefreshTurnUI();      // o botão fica indisponível (recarga) e o Feliz ainda ataca
    }

    // Rico: compra a carta escolhida, que ataca uma carta do próprio time e perde a próxima ação
    void ExecuteBuy()
    {
        CharacterSlotView rico = currentAttacker;
        CharacterSlotView bought = selectedTarget;

        CharacterSlotView victim = null;
        foreach (var s in slots[bought.PlayerIndex])
            if (s != bought && s.Character.IsAlive) { victim = s; break; }
        if (victim == null) return;

        inputLocked = true;
        rico.Character.AbilityCooldownLeft = Mathf.Max(1, rico.Character.data.abilityTurns);
        bought.Character.SkipNextAction = true;
        bought.Character.NotifyStatusChanged();

        floatingTexts.Show(bought.FloatAnchor, "Comprado!", ExtraColor, 64);
        PushLog($"{rico.Character.data.cardName} comprou {bought.Character.data.cardName}, " +
                $"que ataca {victim.Character.data.cardName}!");

        RefreshTurnUI();
        StartCoroutine(AttackRoutine(bought, victim, rico, false));
    }

    // ---------- Ataque ----------
    void OnAttack()
    {
        if (inputLocked || currentAttacker == null || selectedTarget == null) return;

        if (buyMode)
        {
            if (IsValidBuyTarget(selectedTarget)) ExecuteBuy();
            return;
        }

        inputLocked = true;
        RefreshTurnUI();
        StartCoroutine(AttackRoutine(currentAttacker, selectedTarget, currentAttacker, true));
    }

    // atk: carta que se move e ataca | actor: carta que continua o fluxo depois (no Rico, é o próprio Rico)
    IEnumerator AttackRoutine(CharacterSlotView atk, CharacterSlotView tgt, CharacterSlotView actor, bool allowExtra)
    {
        float multiplier = OpeningPenaltyActive ? CombatRules.FirstTurnDamageMultiplier : 1f;
        AttackResult result = default;

        // a carta voa até o alvo; o dano só é calculado no momento do impacto
        yield return cardFx.PlayAttack(atk, tgt, () =>
        {
            result = CombatRules.Resolve(atk.Character, tgt.Character, multiplier);
            RegisterAttack(result, atk, tgt);
            return result;
        });

        yield return new WaitForSeconds(result.targetDefeated ? 0.8f : 0.2f);

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

        ContinueAfterAttempt(actor, allowExtra);
    }

    void RegisterAttack(AttackResult r, CharacterSlotView atk, CharacterSlotView tgt)
    {
        attackCount++;
        GameSession.Instance.TotalDamage[turnPlayer] += r.damage;
        ShowFeedback(r, atk, tgt);
        AddLog(r);
    }

    void ShowFeedback(AttackResult r, CharacterSlotView atk, CharacterSlotView tgt)
    {
        // Etiquetas sobre o atacante (podem aparecer juntas)
        string tags = "";
        if (r.released > 0) tags += $"Liberou {r.released}! ";
        if (r.furyActive) tags += "Fúria! ";
        if (r.buffed && !r.missed && !r.dodged) tags += "Inspirado! ";
        if (tags.Length > 0)
            floatingTexts.Show(atk.FloatAnchor, tags.TrimEnd(), ExtraColor, 50);

        if (r.missed)
        {
            floatingTexts.Show(atk.FloatAnchor, r.feared ? "Medo! Errou" : "Errou!", MissColor, r.feared ? 60 : 72);
            return;
        }
        if (r.dodged)
        {
            floatingTexts.Show(tgt.FloatAnchor, "Esquivou!", DodgeColor, 64);
            return;
        }

        string shield = r.shielded ? "<size=50%> escudo</size>" : "";
        if (r.doubled)
            floatingTexts.Show(tgt.FloatAnchor, $"-{r.damage} DOBRO!{shield}", DoubleColor, 100);
        else if (r.critical)
            floatingTexts.Show(tgt.FloatAnchor, $"-{r.damage}!{shield}", CritColor, 120);
        else
            floatingTexts.Show(tgt.FloatAnchor, $"-{r.damage}{shield}", HitColor, 72);
    }

    void AddLog(AttackResult r)
    {
        string a = r.attacker.data.cardName;
        string t = r.target.data.cardName;

        string line;
        if (r.missed) line = r.feared ? $"{a} está com medo e errou o ataque!" : $"{a} errou o próprio ataque!";
        else if (r.dodged) line = $"{a} atacou, mas {t} esquivou!";
        else if (r.doubled) line = $"{a} deu DANO DOBRADO em {t}: {r.damage}!";
        else if (r.critical) line = $"{a} deu CRÍTICO em {t}: {r.damage} de dano!";
        else line = $"{a} causou {r.damage} de dano em {t}.";

        if (r.released > 0 && !r.missed) line += $" (inclui {r.released} acumulado)";
        if (r.buffed && !r.missed && !r.dodged) line += " (inspirado)";
        if (r.shielded && !r.missed && !r.dodged) line += " (escudo)";
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

    // ---------- Interface ----------
    void RefreshTurnUI()
    {
        string cardName = currentAttacker.Character.data.cardName;
        string hint = inputLocked ? "Aguarde..."
                    : buyMode ? "Escolha a carta para comprar"
                    : selectedTarget == null ? "Escolha o alvo"
                    : "Toque em atacar";

        string opening = "";
        if (OpeningPenaltyActive)
        {
            int percent = Mathf.RoundToInt((1f - CombatRules.FirstTurnDamageMultiplier) * 100f);
            opening = $"\n<size=80%>Primeiro turno: dano -{percent}%</size>";
        }

        for (int p = 0; p < 2; p++)
        {
            if (p == turnPlayer)
            {
                statusLabels[p].text =
                    $"<b>SUA VEZ</b> - Rodada {roundNumber}\n" +
                    $"{cardName} ({attacksByCard + 1}/{CombatRules.MaxAttacksPerCard})\n" +
                    $"<b>{hint}</b>{opening}";
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
            {
                s.SetHighlight(s == currentAttacker ? SlotHighlight.Attacker
                             : s == selectedTarget ? SlotHighlight.Target
                             : SlotHighlight.None);

                bool showAbility = s == currentAttacker
                                   && !inputLocked
                                   && s.Character.IsAlive
                                   && s.Character.data.HasActiveAbility;
                s.SetAbilityButton(showAbility, AbilityButtonLabel(s), buyMode || CanUseAbility(s));
            }

        bool targetOk = selectedTarget != null && (!buyMode || IsValidBuyTarget(selectedTarget));
        attackButton.interactable = targetOk && !inputLocked;
    }
}