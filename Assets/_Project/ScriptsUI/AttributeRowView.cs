using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AttributeRowView : MonoBehaviour
{
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text valueLabel;
    [SerializeField] TMP_Text previewLabel;
    [SerializeField] Button minusButton;
    [SerializeField] Button plusButton;

    public Attribute Attribute { get; private set; }
    public event System.Action<Attribute, int> Changed;   // -1 ou +1

    void Awake()
    {
        minusButton.onClick.AddListener(() => Changed?.Invoke(Attribute, -1));
        plusButton.onClick.AddListener(() => Changed?.Invoke(Attribute, +1));
    }

    public void Setup(Attribute attribute, string displayName)
    {
        Attribute = attribute;
        nameLabel.text = displayName;
    }

    public void Refresh(int value, bool canAdd, bool canRemove, string preview)
    {
        valueLabel.text = $"{value}/{AttributePoints.MaxPerAttribute}";
        previewLabel.text = preview;
        plusButton.interactable = canAdd;
        minusButton.interactable = canRemove;
    }
}