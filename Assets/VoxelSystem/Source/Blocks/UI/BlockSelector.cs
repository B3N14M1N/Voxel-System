using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class BlockSelector : MonoBehaviour
{

    [Inject] private readonly VoxelBlockManager _voxelManager;
    [field: SerializeField] private Button Button { get; set; }
    [field: SerializeField] public VoxelType Type { get; private set; }
    [field: SerializeField] private Image Backgroud { get; set; }
    [field: SerializeField] private Color SelectedState { get; set; } = Color.white;

    private Color _defaultState;
    private bool _selected = false;

    private void Awake()
    {
        if (_voxelManager == null || Button == null) return;

        Button.onClick.AddListener(OnSelected);

        _voxelManager.OnSelected += SetUnSelectedState;

        if (Backgroud == null) return;

        _defaultState = Backgroud.color;
    }

    private void OnSelected()
    {
        if (_voxelManager == null) return;

        if (_selected)
        {
            _voxelManager.SetIdleState();
            return;
        }

        _voxelManager.SetPlacingState(this);
    }

    public void SetSelectedState()
    {
        _selected = true;

        if (Backgroud != null)
            Backgroud.color = SelectedState;
    }

    private void SetUnSelectedState()
    {
        _selected = false;

        if (Backgroud != null)
            Backgroud.color = _defaultState;
    }

    private void OnDestroy()
    {
        if (_voxelManager != null)
            _voxelManager.OnSelected -= SetUnSelectedState;

        if (Button != null)
            Button.onClick.RemoveListener(OnSelected);
    }
}
