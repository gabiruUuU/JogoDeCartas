using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Todas as animações das cartas (chegada, ataque e morte), feitas com DOTween
public class CardFx : MonoBehaviour
{
    public static CardFx Instance { get; private set; }

    [Header("Referências")]
    [SerializeField] RectTransform fxLayer;       // camada por cima de tudo (FxLayer)
    [SerializeField] RectTransform deckOrigin;    // de onde as cartas saem (botão de ataque do centro)
    [SerializeField] RectTransform shakeRoot;     // o que treme nos golpes fortes (Dynamic)
    [SerializeField] Image pieceTemplate;         // quadradinho branco desativado, molde das partículas

    [Header("Chegada das cartas")]
    [SerializeField] float dealDuration = 0.55f;
    [SerializeField] float dealStagger = 0.16f;

    [Header("Ataque")]
    [SerializeField] float windUpDuration = 0.10f;
    [SerializeField] float flyDuration = 0.22f;
    [SerializeField] float returnDuration = 0.28f;

    [Header("Morte")]
    [SerializeField] int crumbleColumns = 6;
    [SerializeField] int crumbleRows = 8;
    [SerializeField] float crumbleDuration = 0.9f;

    static readonly Color DustColor = new Color(1f, 1f, 1f, 0.8f);
    static readonly Color TrailColor = new Color(1f, 0.92f, 0.55f, 0.9f);
    static readonly Color HitColor = new Color(1f, 0.95f, 0.75f);
    static readonly Color CritColor = new Color(1f, 0.85f, 0.15f);
    static readonly Color DoubleColor = new Color(1f, 0.55f, 0.10f);
    static readonly Color ShieldColor = new Color(0.60f, 0.85f, 1f);
    static readonly Color DodgeColor = new Color(0.60f, 0.85f, 1f);
    static readonly Color MissColor = new Color(0.75f, 0.75f, 0.75f, 0.9f);

    readonly Queue<Image> freePieces = new();
    readonly List<RectTransform> ghosts = new();
    readonly Vector3[] corners = new Vector3[4];

    float CanvasScale => fxLayer.lossyScale.x;

    void Awake()
    {
        Instance = this;
        DOTween.SetTweensCapacity(500, 125);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Chamado no começo de cada batalha: limpa o que sobrou de animações anteriores
    public void ResetAll()
    {
        StopAllCoroutines();

        foreach (var g in ghosts)
            if (g != null)
            {
                g.DOKill();
                Destroy(g.gameObject);
            }
        ghosts.Clear();

        shakeRoot.DOKill();
        shakeRoot.anchoredPosition = Vector2.zero;
    }

    // ================= 1. CHEGADA DAS CARTAS =================
    public IEnumerator PlayDealIn(IList<CharacterSlotView> order)
    {
        Canvas.ForceUpdateCanvases();
        yield return null;                           // espera o layout posicionar as cartas

        Vector3 origin = deckOrigin.position;
        Sequence all = DOTween.Sequence();

        for (int i = 0; i < order.Count; i++)
        {
            CharacterSlotView s = order[i];
            RectTransform v = s.Visual;

            Vector3 landing = v.position;                                      // posição final
            Vector2 offset = v.parent.InverseTransformVector(origin - landing); // até o baralho (no espaço da carta)
            s.SetVisualPose(offset, Random.Range(-30f, 30f), 0.35f, 0f);

            float at = i * dealStagger;
            all.Insert(at, v.DOAnchorPos(Vector2.zero, dealDuration).SetEase(Ease.OutCubic));
            all.Insert(at, v.DOLocalRotate(Vector3.zero, dealDuration).SetEase(Ease.OutCubic));
            all.Insert(at, v.DOScale(1f, dealDuration).SetEase(Ease.OutBack));
            all.Insert(at, s.VisualGroup.DOFade(1f, dealDuration * 0.4f));
            all.InsertCallback(at + dealDuration * 0.85f, () => Burst(landing, DustColor, 6, 70f, 14f, 0.4f));
        }

        yield return all.WaitForCompletion();
    }

    // ================= 2. ATAQUE =================
    // resolve() é chamado no momento do impacto: calcula o dano e atualiza vida e textos
    public IEnumerator PlayAttack(CharacterSlotView atk, CharacterSlotView tgt, System.Func<AttackResult> resolve)
    {
        RectTransform ghost = CreateGhost(atk);
        atk.SetVisualVisible(false);

        Vector3 start = ghost.position;
        Vector3 end = tgt.Visual.position;

        // impulso + voo até o alvo, deixando um rastro de faíscas
        Sequence fly = DOTween.Sequence();
        fly.Append(ghost.DOScale(1.15f, windUpDuration));
        fly.Append(ghost.DOMove(end, flyDuration).SetEase(Ease.InCubic));
        while (fly.IsActive() && fly.IsPlaying())
        {
            Burst(ghost.position, TrailColor, 2, 45f, 10f, 0.35f);
            yield return new WaitForSeconds(0.04f);
        }

        // impacto
        AttackResult r = resolve();
        yield return PlayImpact(r, ghost, tgt);

        // volta para o lugar
        Sequence back = DOTween.Sequence();
        back.Append(ghost.DOMove(start, returnDuration).SetEase(Ease.OutQuad));
        back.Join(ghost.DOScale(1f, returnDuration));
        yield return back.WaitForCompletion();

        ghosts.Remove(ghost);
        Destroy(ghost.gameObject);
        atk.SetVisualVisible(true);
    }

    IEnumerator PlayImpact(AttackResult r, RectTransform ghost, CharacterSlotView tgt)
    {
        Vector3 point = tgt.Visual.position;

        if (r.dodged)
        {
            // o alvo desvia para o lado
            float side = Random.value < 0.5f ? -1f : 1f;
            tgt.Visual.DOKill();
            tgt.Visual.anchoredPosition = Vector2.zero;
            tgt.Visual.DOAnchorPos(new Vector2(side * 80f, 0f), 0.10f)
                .SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad);
            Burst(point, DodgeColor, 6, 90f, 12f);
            yield return new WaitForSeconds(0.32f);
        }
        else if (r.missed)
        {
            // a atacante tropeça
            float z = ghost.eulerAngles.z;
            Sequence wobble = DOTween.Sequence();
            wobble.Append(ghost.DORotate(new Vector3(0f, 0f, z + 18f), 0.08f));
            wobble.Append(ghost.DORotate(new Vector3(0f, 0f, z - 18f), 0.08f));
            wobble.Append(ghost.DORotate(new Vector3(0f, 0f, z), 0.08f));
            Burst(ghost.position, MissColor, 5, 70f, 12f);
            yield return wobble.WaitForCompletion();
        }
        else
        {
            bool big = r.critical || r.doubled;
            Color burst = r.doubled ? DoubleColor
                        : r.critical ? CritColor
                        : r.shielded ? ShieldColor
                        : HitColor;

            tgt.Flash();
            if (!r.targetDefeated)            // quem morre treme na animação de morte
            {
                tgt.Visual.DOKill();
                tgt.Visual.anchoredPosition = Vector2.zero;
                tgt.Visual.DOShakeAnchorPos(big ? 0.45f : 0.30f, big ? 55f : 28f, 28, 90f, false, true);
            }

            Burst(point, burst, big ? 22 : 11, big ? 190f : 120f, big ? 22f : 14f);
            if (big) ShakeScreen(0.35f, 26f);

            yield return new WaitForSeconds(big ? 0.45f : 0.30f);
        }
    }

    // Cópia do visual da carta, por cima de tudo, para voar sem ser cortada pelo layout
    RectTransform CreateGhost(CharacterSlotView s)
    {
        RectTransform src = s.Visual;
        RectTransform ghost = Instantiate(src, fxLayer, false);
        ghost.name = "AttackGhost";
        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.pivot = new Vector2(0.5f, 0.5f);
        ghost.sizeDelta = src.rect.size;
        ghost.localScale = Vector3.one;
        ghost.position = src.position;
        ghost.rotation = src.rotation;

        var cg = ghost.GetComponent<CanvasGroup>();
        if (cg == null) cg = ghost.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = false;     // a cópia nunca recebe toque
        cg.interactable = false;

        ghosts.Add(ghost);
        return ghost;
    }

    void ShakeScreen(float duration, float strength)
    {
        shakeRoot.DOKill();
        shakeRoot.anchoredPosition = Vector2.zero;
        shakeRoot.DOShakeAnchorPos(duration, strength, 30, 90f, false, true)
            .OnComplete(() => shakeRoot.anchoredPosition = Vector2.zero);
    }

    // ================= 3. MORTE =================
    public void PlayDeath(CharacterSlotView slot) => StartCoroutine(DeathRoutine(slot));

    IEnumerator DeathRoutine(CharacterSlotView slot)
    {
        slot.BeginDefeat();
        RectTransform v = slot.Visual;

        // 1) treme antes de se desfazer
        yield return v.DOShakeAnchorPos(0.35f, 22f, 30, 90f, false, true).WaitForCompletion();
        v.anchoredPosition = Vector2.zero;

        // 2) esfarela: vira pedaços que caem de cima para baixo (na direção do dono da carta)
        v.GetWorldCorners(corners);
        Vector3 bl = corners[0], tl = corners[1], tr = corners[2], br = corners[3];
        Vector3 down = -v.up;
        float k = CanvasScale;
        Vector2 size = v.rect.size;
        float cellW = size.x / crumbleColumns;
        float cellH = size.y / crumbleRows;
        Color c1 = slot.BackgroundColor;
        Color c2 = slot.ArtColor;

        for (int row = 0; row < crumbleRows; row++)
        {
            for (int col = 0; col < crumbleColumns; col++)
            {
                float u = (col + 0.5f) / crumbleColumns;
                float t = (row + 0.5f) / crumbleRows;                        // 0 = topo, 1 = base
                Vector3 pos = Vector3.Lerp(Vector3.Lerp(tl, tr, u), Vector3.Lerp(bl, br, u), t);

                Image p = GetPiece();
                RectTransform rt = p.rectTransform;
                rt.sizeDelta = new Vector2(cellW * 1.05f, cellH * 1.05f);
                rt.position = pos;
                rt.rotation = v.rotation;

                Color col0 = Color.Lerp(c1, c2, Random.Range(0f, 0.7f)) * Random.Range(0.8f, 1.05f);
                col0.a = 0.95f;
                p.color = col0;

                float delay = t * 0.40f + Random.Range(0f, 0.12f);
                Vector3 end = pos + down * (k * Random.Range(80f, 220f))
                                  + (Vector3)(Random.insideUnitCircle * (40f * k));

                Sequence seq = DOTween.Sequence();
                seq.Append(rt.DOMove(end, crumbleDuration).SetEase(Ease.InQuad));
                seq.Join(rt.DORotate(new Vector3(0f, 0f, v.eulerAngles.z + Random.Range(-120f, 120f)),
                                     crumbleDuration, RotateMode.FastBeyond360));
                seq.Join(p.DOFade(0f, crumbleDuration).SetEase(Ease.InQuad));
                seq.Join(rt.DOScale(0.4f, crumbleDuration));
                seq.PrependInterval(delay);
                seq.OnComplete(() => Recycle(p));
            }
        }

        // 3) a carta de verdade some enquanto os pedaços caem
        yield return slot.VisualGroup.DOFade(0f, 0.15f).WaitForCompletion();
        slot.EndDefeat();
    }

    // ================= Partículas =================
    void Burst(Vector3 worldPos, Color color, int count, float distance, float size, float duration = 0.45f)
    {
        float k = CanvasScale;
        for (int i = 0; i < count; i++)
        {
            Image p = GetPiece();
            RectTransform rt = p.rectTransform;

            float s = size * Random.Range(0.6f, 1.2f);
            rt.sizeDelta = new Vector2(s, s);
            rt.position = worldPos;
            rt.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            p.color = color;

            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 end = worldPos + (Vector3)(dir * (distance * k * Random.Range(0.5f, 1f)));
            float d = duration * Random.Range(0.8f, 1.2f);

            Sequence seq = DOTween.Sequence();
            seq.Append(rt.DOMove(end, d).SetEase(Ease.OutCubic));
            seq.Join(rt.DORotate(new Vector3(0f, 0f, Random.Range(-300f, 300f)), d, RotateMode.FastBeyond360));
            seq.Join(p.DOFade(0f, d).SetEase(Ease.InQuad));
            seq.Join(rt.DOScale(0.2f, d));
            seq.OnComplete(() => Recycle(p));
        }
    }

    Image GetPiece()
    {
        Image p = freePieces.Count > 0 ? freePieces.Dequeue() : Instantiate(pieceTemplate, fxLayer, false);
        p.gameObject.SetActive(true);
        p.rectTransform.localScale = Vector3.one;
        return p;
    }

    void Recycle(Image p)
    {
        p.DOKill();
        p.rectTransform.DOKill();
        p.gameObject.SetActive(false);
        freePieces.Enqueue(p);
    }
}