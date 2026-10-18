using TMPro;
using UnityEngine;

public class ResultScreenController : MonoBehaviour
{
    [SerializeField] TMP_Text winnerLabel;
    [SerializeField] TMP_Text turnsLabel;
    [SerializeField] TMP_Text damageLabel;

    void Start() => ScreenManager.Instance.ScreenShown += OnScreenShown;

    void OnDestroy()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.ScreenShown -= OnScreenShown;
    }

    void OnScreenShown(ScreenId id)
    {
        if (id == ScreenId.Result) Populate();
    }

    // Só roda quando a tela abre (sem Update)
    void Populate()
    {
        var s = GameSession.Instance;
        int w = s.Winner;

        winnerLabel.text = w >= 0 ? $"Jogador {w + 1} venceu!" : "Fim da batalha";
        winnerLabel.color = w >= 0 ? PlayerPalette.All[w] : Color.white;

        turnsLabel.text = $"Turnos jogados: {s.TurnsPlayed}";

        string c1 = ColorUtility.ToHtmlStringRGB(PlayerPalette.All[0]);
        string c2 = ColorUtility.ToHtmlStringRGB(PlayerPalette.All[1]);
        damageLabel.text =
            "<b>Dano total causado</b>\n" +
            $"<color=#{c1}>Jogador 1: {s.TotalDamage[0]}</color>\n" +
            $"<color=#{c2}>Jogador 2: {s.TotalDamage[1]}</color>";
    }
}