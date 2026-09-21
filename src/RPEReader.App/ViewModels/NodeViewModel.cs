using System.Collections.ObjectModel;
using RPEReader.App.Mvvm;
using RPEReader.Core.Models;

namespace RPEReader.App.ViewModels;

/// <summary>Tree-view wrapper around an <see cref="RpeNode"/>.</summary>
public sealed class NodeViewModel : ObservableObject
{
    private bool _isExpanded;
    private bool _isSelected;

    public NodeViewModel(RpeNode node, NodeViewModel? parent = null)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Parent = parent;
        Children = new ObservableCollection<NodeViewModel>(node.Children.Select(c => new NodeViewModel(c, this)));
    }

    public RpeNode Node { get; }

    public NodeViewModel? Parent { get; }

    public ObservableCollection<NodeViewModel> Children { get; }

    public string Name => Node.Name;

    public string? Category => Node.Category;

    public string? Value => Node.Value;

    public bool HasFields => Node.Fields.Count > 0;

    public string Path
    {
        get
        {
            var segments = new List<string>();
            var current = this;
            while (current is not null)
            {
                segments.Add(current.Name);
                current = current.Parent;
            }

            segments.Reverse();
            return string.Join(" / ", segments);
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value)
            {
                Parent?.ExpandToRoot();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void ExpandToRoot()
    {
        IsExpanded = true;
        Parent?.ExpandToRoot();
    }

    /// <summary>Finds the wrapper for a given model node, expanding nothing.</summary>
    public NodeViewModel? Find(RpeNode target)
    {
        if (ReferenceEquals(Node, target))
        {
            return this;
        }

        foreach (var child in Children)
        {
            var found = child.Find(target);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
