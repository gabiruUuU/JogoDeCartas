using UnityEngine;

public static class PlayerPalette
{
    // Cores de destaque (textos, painéis, molduras)
    public static readonly Color[] All =
    {
        new Color(0.25f, 0.55f, 1f),   // Jogador 1 (azul)
        new Color(1f, 0.40f, 0.40f)    // Jogador 2 (vermelho)
    };

    // Fundo dos cards na batalha (pouco saturado, para o texto continuar legível)
    public static readonly Color[] CardBackground =
    {
        new Color(0.18f, 0.27f, 0.42f),   // Jogador 1
        new Color(0.42f, 0.20f, 0.22f)    // Jogador 2
    };
}