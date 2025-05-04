using System;
using UnityEngine;
using Zenject;

public class PlayerStateManager : IInitializable
{
    // State properties that can be controlled externally
    public bool CanControl { get; private set; } = true;
    public bool CanFly { get; private set; } = true;
    public PlayerState ViewState { get; private set; } = PlayerState.TPS;

    // Events for state changes that components can subscribe to
    public event Action<bool> OnControlStateChanged;
    public event Action<bool> OnFlyStateChanged;
    public event Action<PlayerState> OnViewStateChanged;

    public void Initialize()
    {
        // Default initialization
        CanControl = true;
        CanFly = true;
        ViewState = PlayerState.TPS;
    }

    // Methods to change states externally
    public void SetControlState(bool canControl)
    {
        if (CanControl == canControl) return;
        
        CanControl = canControl;
        OnControlStateChanged?.Invoke(canControl);
    }

    public void SetFlyState(bool canFly)
    {
        if (CanFly == canFly) return;
        
        CanFly = canFly;
        OnFlyStateChanged?.Invoke(canFly);
    }

    public void SetViewState(PlayerState viewState)
    {
        if (ViewState == viewState) return;
        
        ViewState = viewState;
        OnViewStateChanged?.Invoke(viewState);
    }

    public void ToggleViewState()
    {
        SetViewState(ViewState == PlayerState.FPS ? PlayerState.TPS : PlayerState.FPS);
    }

    public void ToggleFlyState()
    {
        SetFlyState(!CanFly);
    }
}