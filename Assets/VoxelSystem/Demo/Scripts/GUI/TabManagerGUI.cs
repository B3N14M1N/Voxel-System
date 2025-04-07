using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TabManagerGUI : MonoBehaviour
{
    public Color SelectedColor;
    public Color UnselectedColor;
    private List<TabGUI> Tabs;


    public delegate void TabChanged();
    public event TabChanged OnTabChanged;

    public void Awake()
    {
        OnTabChanged += CloseTabs;
        Tabs = GetComponentsInChildren<TabGUI>().ToList();
    }

    public void OnEnable()
    {
        if (Tabs.Count > 0)
            OnTabChange(Tabs[0]);
    }

    private void CloseTabs()
    {
        foreach (var tab in Tabs)
        {
            tab.Active = false;
            tab.TabColor = UnselectedColor;
        }
    }

    public void OnTabChange(TabGUI tab)
    {
        OnTabChanged?.Invoke();

        tab.Active = true;
        tab.TabColor = SelectedColor;
    }
}
