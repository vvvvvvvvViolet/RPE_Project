namespace RPEReader.Core.Models;

/// <summary>
/// One node of the logical tree a parser produces. Deliberately format-agnostic
/// so that the UI, search and exporters work against any parser implementation.
/// </summary>
public sealed class RpeNode
{
    private readonly List<RpeNode> _children = new();
    private readonly List<RpeField> _fields = new();

    public RpeNode(string name, string? category = null, string? value = null)
    {
        Name = string.IsNullOrEmpty(name) ? "(unnamed)" : name;
        Category = category;
        Value = value;
    }

    /// <summary>
    /// Display name of the node. Derived from a child value for some node
    /// kinds, so the editor updates it when that value changes.
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>Coarse kind, e.g. "Section", "Connection", "TransferObject", "Item".</summary>
    public string? Category { get; }

    /// <summary>Short inline value shown next to the name, when meaningful.</summary>
    public string? Value { get; set; }

    public IReadOnlyList<RpeNode> Children => _children;

    public IReadOnlyList<RpeField> Fields => _fields;

    /// <summary>True when a limit stopped this node from being fully populated.</summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Opaque handle to whatever the parser built this node from, used to
    /// re-derive the label when an underlying value changes.
    /// </summary>
    public object? Source { get; init; }

    public RpeNode AddChild(RpeNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Add(child);
        return child;
    }

    public RpeNode AddField(string name, string? value, string? typeHint = null, string? note = null) =>
        AddField(new RpeField(name, value, typeHint, note));

    public RpeNode AddField(RpeField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        field.Owner = this;
        _fields.Add(field);
        return this;
    }

    /// <summary>Depth-first enumeration of this node and all descendants.</summary>
    public IEnumerable<RpeNode> DescendantsAndSelf()
    {
        var stack = new Stack<RpeNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            for (var i = n._children.Count - 1; i >= 0; i--)
            {
                stack.Push(n._children[i]);
            }
        }
    }

    public override string ToString() => Value is null ? Name : $"{Name}: {Value}";
}
