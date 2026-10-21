using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DescriptionScreenController : MonoBehaviour
{
    [SerializeField] Image art;
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text statsLabel;
    [SerializeField] TMP_Text storyLabel;
    [SerializeField] ScrollRect scroll;
    [SerializeField] Button closeButton;

    void Start()
    {
        closeButton.onClick.AddListener(Close);
        ScreenManager.Instance.VisibilityChanged += OnVisibilityChanged;
    }

    void OnDestroy()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.VisibilityChanged -= OnVisibilityChanged;
    }

    void OnVisibilityChanged(ScreenId id, bool visible)
    {
        if (id == ScreenId.Description && visible) Populate();
    }

    void Populate()
    {
        CardData c = GameSession.Instance.ViewedCard;
        if (c == null) return;

        art.preserveAspect = true;
        art.sprite = c.art;
        art.color = c.art != null ? Color.white : new Color(0.5f, 0.55f, 0.65f);   // placeholder

        nameLabel.text = c.cardName;
        statsLabel.text = $"Vida {c.health}   •   Dano {c.minDamage} a {c.maxDamage}";

        string ability = c.ability != AbilityType.None
            ? $"<b>Habilidade - {c.AbilityName}</b>\n{c.AbilityDescription}\n\n"
            : "";
        storyLabel.text = ability + c.story;

        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;      // sempre começa no topo
    }

    void Close() => ScreenManager.Instance.SetVisible(ScreenId.Description, false);
}