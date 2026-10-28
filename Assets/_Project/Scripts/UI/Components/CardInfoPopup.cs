using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardInfoPopup : MonoBehaviour
{
    [SerializeField] CanvasGroup group;
    [SerializeField] RectTransform panel;       // o objeto "Panel" (gira 180° no botão Virar)
    [SerializeField] Image art;
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text ownerLabel;
    [SerializeField] TMP_Text hpLabel;
    [SerializeField] TMP_Text statsLabel;
    [SerializeField] Button closeButton;
    [SerializeField] Button flipButton;
    [SerializeField] Button dimButton;          // fundo escuro: tocar fora fecha

    bool flipped;

    void Awake()
    {
        closeButton.onClick.AddListener(Hide);
        dimButton.onClick.AddListener(Hide);
        flipButton.onClick.AddListener(() => SetFlipped(!flipped));
        Hide();
    }

    public void Show(CharacterRuntime c, int playerIndex)
    {
        var p = c.points;

        nameLabel.text = c.data.cardName;
        ownerLabel.text = $"Jogador {playerIndex + 1}";
        ownerLabel.color = PlayerPalette.All[playerIndex];
        hpLabel.text = c.IsAlive ? $"Vida {c.CurrentHealth}/{c.MaxHealth}" : "Derrotado";

        art.preserveAspect = true;
        art.sprite = c.data.art;
        art.color = c.data.art != null ? Color.white : new Color(0.5f, 0.55f, 0.65f);

        var sb = new StringBuilder();
        AppendStat(sb, "Força", p.Forca,
            $"Dano {CombatRules.MinDamage(c)} a {CombatRules.MaxDamage(c)}");
        AppendStat(sb, "Velocidade", p.Velocidade,
            $"Chance de atacar de novo: {Pct(CombatRules.ExtraAttackChance(p))}%");
        AppendStat(sb, "Intelecto", p.Intelecto,
            $"Esquiva: {Pct(CombatRules.DodgeChance(p))}%");
        AppendStat(sb, "Destreza", p.Destreza,
            $"Crítico: {Pct(CombatRules.CritChance(p))}% (x{CombatRules.CritMultiplier:0.0})");
        AppendStat(sb, "Raiva", p.Raiva,
            $"+{Pct(CombatRules.FuryBonus(p))}% de dano com vida abaixo de 30%");

        if (c.data.ability != AbilityType.None)
        {
            sb.Append("<b>Habilidade: ").Append(c.data.AbilityName).Append("</b>\n");
            sb.Append("<size=80%><color=#B8C0D0>").Append(c.data.AbilityDescription).Append("</color></size>\n");
        }

        string status = c.StatusText;
        if (!string.IsNullOrEmpty(status))
            sb.Append("<color=#FFE066><b>").Append(status).Append("</b></color>\n");

        if (c.IsFurious && p.Raiva > 0)
            sb.Append("<color=#FF6A4D><b>FÚRIA ATIVA!</b></color>");

        statsLabel.text = sb.ToString().TrimEnd();

        SetFlipped(playerIndex == 1);       // abre virado para o dono da carta

        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
    }

    public void Hide()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    void SetFlipped(bool value)
    {
        flipped = value;
        panel.localRotation = Quaternion.Euler(0f, 0f, flipped ? 180f : 0f);
    }

    static void AppendStat(StringBuilder sb, string name, int value, string effect)
    {
        sb.Append("<b>").Append(name).Append("</b>  ")
          .Append(value).Append('/').Append(AttributePoints.MaxPerAttribute).Append('\n');
        sb.Append("<size=80%><color=#B8C0D0>").Append(effect).Append("</color></size>\n");
    }

    static int Pct(float f) => Mathf.RoundToInt(f * 100f);
}