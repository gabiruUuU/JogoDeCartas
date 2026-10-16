using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class GoToScreenButton : MonoBehaviour
{
    [SerializeField] ScreenId target;

    void Awake() =>
        GetComponent<Button>().onClick.AddListener(() => ScreenManager.Instance.Show(target));
}