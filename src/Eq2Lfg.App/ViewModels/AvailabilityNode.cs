using CommunityToolkit.Mvvm.ComponentModel;

namespace Eq2Lfg.App.ViewModels;

/// <summary>
/// One node of the account → server → character availability tree. Parents show
/// tri-state (null = mixed); toggling a parent applies to all descendants.
/// </summary>
public sealed partial class AvailabilityNode : ObservableObject
{
    private bool suppress;

    public required string Label { get; init; }

    /// <summary>Depth 0 = account, 1 = server, 2 = character.</summary>
    public required int Depth { get; init; }

    public List<AvailabilityNode> Children { get; init; } = [];
    public AvailabilityNode? Parent { get; set; }

    /// <summary>Called with (node, isChecked) whenever a leaf-effective state changes.</summary>
    public Action<AvailabilityNode, bool>? StateChanged { get; set; }

    [ObservableProperty]
    private bool? isChecked = true;

    partial void OnIsCheckedChanged(bool? value)
    {
        if (suppress)
        {
            return;
        }

        // WPF tri-state checkboxes cycle to null on click; treat that as "check all".
        if (value is null && Children.Count > 0)
        {
            SetSilently(true);
            value = true;
        }

        if (value is { } state)
        {
            foreach (var child in Children)
            {
                child.ApplyFromParent(state);
            }

            StateChanged?.Invoke(this, state);
        }

        Parent?.RecomputeFromChildren();
    }

    private void ApplyFromParent(bool state)
    {
        SetSilently(state);
        foreach (var child in Children)
        {
            child.ApplyFromParent(state);
        }

        StateChanged?.Invoke(this, state);
    }

    private void RecomputeFromChildren()
    {
        var states = Children.Select(c => c.IsChecked).Distinct().ToList();
        SetSilently(states.Count == 1 ? states[0] : null);
        Parent?.RecomputeFromChildren();
    }

    private void SetSilently(bool? value)
    {
        suppress = true;
        IsChecked = value;
        suppress = false;
    }

    /// <summary>Set initial state without firing callbacks.</summary>
    public void Initialize(bool state) => SetSilently(state);

    public void RecomputeAfterInitialize()
    {
        foreach (var child in Children)
        {
            child.RecomputeAfterInitialize();
        }

        if (Children.Count > 0)
        {
            var states = Children.Select(c => c.IsChecked).Distinct().ToList();
            SetSilently(states.Count == 1 ? states[0] : null);
        }
    }
}
