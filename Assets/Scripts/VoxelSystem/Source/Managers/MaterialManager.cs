using UnityEngine;
using UnityEngine.UI;

public class MaterialManager : MonoBehaviour
{
    [SerializeField] private Material targetMaterial;
    [SerializeField] private string propertyName = "_BooleanProperty"; // Replace with your shader's bool property name
    [SerializeField] private Toggle toggle;
    [SerializeField] private bool isOn = false;
    private void Start()
    {
        if (toggle == null || targetMaterial == null) return;

        toggle.isOn = isOn;
        targetMaterial.SetFloat(propertyName, isOn ? 1f : 0f);
        toggle.onValueChanged.AddListener(SetMaterialBoolean);
    }

    private void SetMaterialBoolean(bool isOn)
    {
        targetMaterial.SetFloat(propertyName, isOn ? 1f : 0f);
    }
}