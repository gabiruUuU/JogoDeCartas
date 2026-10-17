using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FloatingTextPool : MonoBehaviour
{
    [SerializeField] TMP_Text template;        // objeto desativado na cena, usado como molde
    [SerializeField] RectTransform container;  // normalmente o próprio FxLayer
    [SerializeField] float duration = 1.1f;
    [SerializeField] float riseDistance = 150f;

    readonly Queue<TMP_Text> free = new();

    public void Show(RectTransform anchor, string text, Color color, float fontSize)
    {
        TMP_Text t = free.Count > 0 ? free.Dequeue() : Instantiate(template, container, false);
        t.gameObject.SetActive(true);
        t.text = text;
        t.color = color;
        t.fontSize = fontSize;
        t.alpha = 1f;

        RectTransform rt = t.rectTransform;
        rt.position = anchor.position;
        rt.rotation = anchor.rotation;          // o texto acompanha a orientação do card
        StartCoroutine(Animate(t, anchor.up));  // e sobe na direção "de cima" do card
    }

    IEnumerator Animate(TMP_Text t, Vector3 direction)
    {
        RectTransform rt = t.rectTransform;
        Vector3 start = rt.position;
        float rise = riseDistance * container.lossyScale.y;   // converte unidades do Canvas em pixels
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float k = time / duration;
            rt.position = start + direction * (rise * k);
            t.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            yield return null;
        }

        t.gameObject.SetActive(false);
        free.Enqueue(t);
    }
}