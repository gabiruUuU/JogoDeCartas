using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SlotHighlight { None, Attacker, Target }

public class CharacterSlotView : MonoBehaviour
{
    [SerializeField] Image frame;
    [SerializeField] Image background;      // a Image do objeto "Bg"
    [SerializeField] Image art;
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] RectTransform hpFill;
    [SerializeField] Image hpFillImage;
    [SerializeField] TMP_Text hpLabel;
    [SerializeField] TMP_Text furyLabel;
    [SerializeField] TMP_Text statusLabel;  // ZZZ, ESCUDO, TRISTE, CARGA, COMPRADO
    [SerializeField] Button button;         // o Button do objeto "Bg"
    [SerializeField] Button infoButton;     // botão (i)
    [SerializeField] Button abilityButton;  // botão da habilidade ativa
    [SerializeField] TMP_Text abilityLabel;
    [SerializeField] CanvasGroup group;
    [SerializeField] RectTransform floatAnchor;

    static readonly Color AttackerColor = new Color(0.30f, 1f, 0.40f);
    static readonly Color TargetColor = new Color(1f, 0.55f, 0.15f);
    static readonly Color BarNormal = new Color(0.30f, 0.85f, 0.40f);
    static readonly Color BarFury = new Color(1f, 0.45f, 0.10f);
    static readonly Color FuryTint = new Color(1f, 0.55f, 0.55f);

    public CharacterRuntime Character { get; private set; }
    public int PlayerIndex { get; private set; }
    public RectTransform FloatAnchor => floatAnchor;

    public event System.Action<CharacterSlotView> Clicked;
    public event System.Action<CharacterSlotView> InfoClicked;
    public event System.Action<CharacterSlotView> AbilityClicked;

    Color baseArtColor;

    void Awake()
    {
        button.onClick.AddListener(() => Clicked?.Invoke(this));
        infoButton.onClick.AddListener(() => InfoClicked?.Invoke(this));
        abilityButton.onClick.AddListener(() => AbilityClicked?.Invoke(this));
    }

    void OnDestroy() => Unsubscribe();

    void Unsubscribe()
    {
        if (Character == null) return;
        Character.HealthChanged -= OnHealthChanged;
        Character.Defeated -= OnDefeated;
        Character.StatusChanged -= OnStatusChanged;
    }

    public void Setup(CharacterRuntime c, int playerIndex)
    {
        StopAllCoroutines();
        Unsubscribe();

        Character = c;
        PlayerIndex = playerIndex;
        c.HealthChanged += OnHealthChanged;
        c.Defeated += OnDefeated;
        c.StatusChanged += OnStatusChanged;

        nameLabel.text = c.data.cardName;
        background.color = PlayerPalette.CardBackground[playerIndex];

        art.preserveAspect = true;
        art.sprite = c.data.art;
        baseArtColor = c.data.art != null ? Color.white : new Color(0.5f, 0.55f, 0.65f);

        group.alpha = 1f;
        group.blocksRaycasts = true;
        transform.localScale = Vector3.one;
        button.interactable = true;

        SetHighlight(SlotHighlight.None);
        SetAbilityButton(false, "", false);
        Refresh();
        RefreshStatus();
    }

    public void SetHighlight(SlotHighlight h)
    {
        switch (h)
        {
            case SlotHighlight.Attacker: frame.color = AttackerColor; break;
            case SlotHighlight.Target: frame.color = TargetColor; break;
            default: frame.color = Color.clear; break;
        }
    }

    // Mostra o botão da habilidade só na carta que está agindo
    public void SetAbilityButton(bool visible, string label, bool interactable)
    {
        if (abilityButton.gameObject.activeSelf != visible)
            abilityButton.gameObject.SetActive(visible);
        if (!visible) return;

        abilityLabel.text = label;
        abilityButton.interactable = interactable;
    }

    void OnHealthChanged(CharacterRuntime c) => Refresh();
    void OnStatusChanged(CharacterRuntime c) => RefreshStatus();
    void OnDefeated(CharacterRuntime c) => StartCoroutine(DefeatRoutine());

    // Só roda quando a vida muda (sem Update)
    void Refresh()
    {
        hpFill.anchorMax = new Vector2(Mathf.Clamp01(Character.HealthPercent), 1f);
        hpLabel.text = $"{Character.CurrentHealth}/{Character.MaxHealth}";

        bool fury = Character.IsFurious && Character.points.Raiva > 0;
        furyLabel.enabled = fury;
        art.color = fury ? baseArtColor * FuryTint : baseArtColor;
        hpFillImage.color = fury ? BarFury : BarNormal;
    }

    void RefreshStatus() => statusLabel.text = Character.StatusText;

    IEnumerator DefeatRoutine()
    {
        button.interactable = false;
        SetHighlight(SlotHighlight.None);
        SetAbilityButton(false, "", false);

        const float duration = 0.6f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            group.alpha = 1f - k;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, k);
            yield return null;
        }
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }
}