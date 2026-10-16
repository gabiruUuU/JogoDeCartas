using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public enum ScreenId { Menu, Credits, CardSelection, Description, PointDistribution, Battle, Result }

public class ScreenManager : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public ScreenId id;
        public Canvas root;
    }

    public static ScreenManager Instance { get; private set; }

    [SerializeField] List<Entry> screens = new();
    [SerializeField] ScreenId startScreen = ScreenId.Menu;

    readonly Dictionary<ScreenId, Canvas[]> canvases = new();
    readonly Dictionary<ScreenId, GraphicRaycaster[]> raycasters = new();

    void Awake()
    {
        Instance = this;
        foreach (var e in screens)
        {
            canvases[e.id] = e.root.GetComponentsInChildren<Canvas>(true);
            raycasters[e.id] = e.root.GetComponentsInChildren<GraphicRaycaster>(true);
        }
    }

    void Start() => Show(startScreen);

    public event Action<ScreenId> ScreenShown;
    public event Action<ScreenId, bool> VisibilityChanged;

    public void Show(ScreenId id)
    {
        if (!canvases.ContainsKey(id))
        {
            Debug.LogWarning($"ScreenManager: tela {id} ainda não cadastrada.");
            return;
        }
        foreach (var e in screens) SetVisible(e.id, e.id == id);
        ScreenShown?.Invoke(id);
    }

    // Permite sobrepor uma tela (ex: Descrição por cima da Seleção)

    public void SetVisible(ScreenId id, bool visible)
    {
        if (!canvases.ContainsKey(id)) return;
        foreach (var c in canvases[id]) c.enabled = visible;
        foreach (var r in raycasters[id]) r.enabled = visible;
        VisibilityChanged?.Invoke(id, visible);
    }

    // Para ligar em botões do Inspector (OnClick só aceita int/string/etc.)
    public void ShowByIndex(int id) => Show((ScreenId)id);
}