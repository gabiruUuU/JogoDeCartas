using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    [SerializeField] Image art;
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] Image stateOverlay;
    [SerializeField] TMP_Text stateLabel;
    [SerializeField] Button selectButton;
    [SerializeField] Button infoButton;

    public CardData Data { get; private set; }
    public int Index { get; private set; }

    public event Action<CardView> Selected;
    public event Action<CardView> InfoRequested;

    void Awake()
    {
        selectButton.onClick.AddListener(() => Selected?.Invoke(this));
        infoButton.onClick.AddListener(() => InfoRequested?.Invoke(this));
    }

    public void Setup(CardData data, int index)
    {
        Data = data;
        Index = index;
        nameLabel.text = data.cardName;
        art.preserveAspect = true;

        if (data.art != null)
        {
            art.sprite = data.art;
            art.color = Color.white;
        }
        else
        {
            art.sprite = null;                                   // placeholder
            art.color = Color.HSVToRGB(index / 10f, 0.45f, 0.85f);
        }
        SetState(-1, Color.clear, true);
    }

    // owner: -1 = livre, 0 = Jogador 1, 1 = Jogador 2
    public void SetState(int owner, Color ownerColor, bool canClick)
    {
        bool taken = owner >= 0;
        stateOverlay.enabled = taken;
        stateLabel.enabled = taken;
        if (taken)
        {
            ownerColor.a = 0.55f;
            stateOverlay.color = ownerColor;
            stateLabel.text = "J" + (owner + 1);
        }
        selectButton.interactable = canClick;
    }
}