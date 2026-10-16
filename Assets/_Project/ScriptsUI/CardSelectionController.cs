using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardSelectionController : MonoBehaviour
{
    [SerializeField] CardView cardPrefab;
    [SerializeField] RectTransform gridParent;
    [SerializeField] TMP_Text headerLabel;
    [SerializeField] TMP_Text counterLabel;
    [SerializeField] Button confirmButton;

    static readonly Color[] PlayerColors = PlayerPalette.All;

    readonly List<CardView> views = new();
    int[] owner;          // dono de cada carta (-1 = livre)
    int currentPlayer;

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
        if (id == ScreenId.CardSelection) Begin();
    }

    void Begin()
    {
        var session = GameSession.Instance;
        session.ResetSession();
        BuildGridOnce();

        owner = new int[session.AllCards.Count];
        Array.Fill(owner, -1);
        currentPlayer = 0;
        Refresh();
    }

    void BuildGridOnce()
    {
        if (views.Count > 0) return;
        var cards = GameSession.Instance.AllCards;
        for (int i = 0; i < cards.Count; i++)
        {
            CardView v = Instantiate(cardPrefab, gridParent, false);
            v.Setup(cards[i], i);
            v.Selected += OnCardSelected;
            v.InfoRequested += OnInfoRequested;
            views.Add(v);
        }
    }

    void OnCardSelected(CardView view)
    {
        var picks = GameSession.Instance.picks[currentPlayer];
        int i = view.Index;

        if (owner[i] == currentPlayer)                       // desmarcar
        {
            picks.Remove(view.Data);
            owner[i] = -1;
        }
        else if (owner[i] == -1 && picks.Count < GameSession.CardsPerPlayer)
        {
            picks.Add(view.Data);
            owner[i] = currentPlayer;
        }
        Refresh();
    }

    void OnInfoRequested(CardView view)
    {
        GameSession.Instance.ViewedCard = view.Data;
        ScreenManager.Instance.SetVisible(ScreenId.Description, true);   // sobrepõe a seleção
    }

    void OnConfirm()
    {
        if (currentPlayer < GameSession.PlayersCount - 1)
        {
            currentPlayer++;
            Refresh();
        }
        else
        {
            ScreenManager.Instance.Show(ScreenId.PointDistribution);
        }
    }

    // Atualiza só quando algo muda (sem Update)
    void Refresh()
    {
        int count = GameSession.Instance.picks[currentPlayer].Count;

        headerLabel.text = $"Jogador {currentPlayer + 1}: escolha {GameSession.CardsPerPlayer} cartas";
        headerLabel.color = PlayerColors[currentPlayer];
        counterLabel.text = $"{count}/{GameSession.CardsPerPlayer} escolhidas";
        confirmButton.interactable = count == GameSession.CardsPerPlayer;

        for (int i = 0; i < views.Count; i++)
        {
            int o = owner[i];
            bool canClick = o == -1 ? count < GameSession.CardsPerPlayer : o == currentPlayer;
            views[i].SetState(o, o >= 0 ? PlayerColors[o] : Color.clear, canClick);
        }
    }
}