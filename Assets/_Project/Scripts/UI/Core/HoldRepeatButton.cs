using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Dispara Tick ao tocar e continua disparando enquanto o botão é mantido pressionado
[RequireComponent(typeof(Button))]
public class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] float initialDelay = 0.35f;   // espera antes de começar a repetir
    [SerializeField] float startInterval = 0.10f;  // intervalo entre repetições no começo
    [SerializeField] float minInterval = 0.04f;    // intervalo mais rápido (aceleração)
    [SerializeField] float acceleration = 0.9f;    // a cada repetição, o intervalo é multiplicado por isto

    public event System.Action Tick;

    Button button;
    Coroutine routine;

    void Awake() => button = GetComponent<Button>();

    void OnDisable() => Stop();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable) return;

        Tick?.Invoke();                       // primeiro ponto assim que toca
        Stop();
        routine = StartCoroutine(Repeat());
    }

    public void OnPointerUp(PointerEventData eventData) => Stop();

    public void OnPointerExit(PointerEventData eventData) => Stop();

    void Stop()
    {
        if (routine == null) return;
        StopCoroutine(routine);
        routine = null;
    }

    IEnumerator Repeat()
    {
        yield return new WaitForSeconds(initialDelay);

        float interval = startInterval;
        while (button.interactable)           // para sozinho quando chega no limite
        {
            Tick?.Invoke();
            yield return new WaitForSeconds(interval);
            interval = Mathf.Max(minInterval, interval * acceleration);
        }
        routine = null;
    }
}