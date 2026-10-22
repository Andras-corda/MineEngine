using System.Collections.Immutable;
using System.Globalization;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Flow nodes with several execution outputs, like Unreal's Blueprints: Switch on a number or a
/// text (one output per case, then Default), Sequence (outputs run one after the other, their
/// number is chosen with "Add pin") and Flip Flop (A, then B, then A...). The pins of Switch and
/// Sequence depend on the values of the node: they are described here, not by the catalog.
/// </summary>
public static class FlowNodes
{
    /// <summary>Cases of a Switch, one per line.</summary>
    public const string CasesKey = "cases";

    /// <summary>Number of outputs of a Sequence.</summary>
    public const string CountKey = "count";

    public const string ValueInput = "value";

    public const string DefaultPin = "default";

    public const string FlipFlopA = "a";

    public const string FlipFlopB = "b";

    public const string IsAOutput = "isA";

    public const int MaxPins = 16;

    /// <summary>Nodes whose pins depend on their values.</summary>
    public static bool IsDynamic(GraphNodeKind kind) => kind is GraphNodeKind.SwitchOnNumber or GraphNodeKind.SwitchOnText or GraphNodeKind.Sequence;

    public static bool IsSwitch(GraphNodeKind kind) => kind is GraphNodeKind.SwitchOnNumber or GraphNodeKind.SwitchOnText;

    /// <summary>Output of the case number <paramref name="index"/> of a Switch.</summary>
    public static string CasePin(int index) => "case" + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>Output number <paramref name="index"/> of a Sequence.</summary>
    public static string SequencePin(int index) => "then" + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>New node of the catalog, with its default pins (two cases, two outputs).</summary>
    public static ImmutableDictionary<string, string> DefaultValues(GraphNodeKind kind) => kind switch
    {
        GraphNodeKind.SwitchOnNumber => ImmutableDictionary<string, string>.Empty.Add(CasesKey, "0\n1").Add(ValueInput, "0"),
        GraphNodeKind.SwitchOnText => ImmutableDictionary<string, string>.Empty.Add(CasesKey, "a\nb").Add(ValueInput, string.Empty),
        GraphNodeKind.Sequence => ImmutableDictionary<string, string>.Empty.Add(CountKey, "2"),
        _ => ImmutableDictionary<string, string>.Empty,
    };

    /// <summary>Cases of a Switch: one per line, without empty or repeated ones (a number Switch keeps whole numbers only).</summary>
    public static IReadOnlyList<string> Cases(GraphNode node)
    {
        string text = node.Values.GetValueOrDefault(CasesKey) ?? DefaultValues(node.Kind).GetValueOrDefault(CasesKey) ?? string.Empty;
        IEnumerable<string> cases = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (node.Kind == GraphNodeKind.SwitchOnNumber)
        {
            cases = cases.Where(c => int.TryParse(c, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
        }

        return [.. cases.Distinct(StringComparer.Ordinal).Take(MaxPins)];
    }

    /// <summary>Number of outputs of a Sequence (1 to <see cref="MaxPins"/>).</summary>
    public static int Count(GraphNode node) =>
        int.TryParse(node.Values.GetValueOrDefault(CountKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
            ? Math.Clamp(count, 1, MaxPins)
            : 2;

    /// <summary>Pins of a Switch or a Sequence placed in a graph.</summary>
    public static NodeInfo Describe(GraphNode node) => node.Kind switch
    {
        GraphNodeKind.Sequence => Sequence(Count(node)),
        _ => Switch(node.Kind, Cases(node)),
    };

    /// <summary>Description of the catalog (default pins), shown in the add menu.</summary>
    public static NodeInfo Template(GraphNodeKind kind) =>
        Describe(new GraphNode(Guid.Empty, kind, 0, 0, DefaultValues(kind)));

    public static NodeInfo FlipFlop() =>
        new(GraphNodeKind.FlipFlop, NodeCategory.Condition, Tr.T("Flip Flop"),
            Tr.T("Runs A the first time, B the next time, then A again, and so on. Is A tells which one runs."),
            outputs: [new PinInfo(IsAOutput, Tr.T("Is A"), PinType.Boolean)],
            flow: NodeFlow.Multi,
            execOutputs: [new PinInfo(FlipFlopA, "A", DataType.Exec), new PinInfo(FlipFlopB, "B", DataType.Exec)]);

    /// <summary>One more case (the next number, or "case N") or one more output; the pins keep their wires.</summary>
    public static GraphNode AddPin(GraphNode node)
    {
        if (node.Kind == GraphNodeKind.Sequence)
        {
            return node.WithValue(CountKey, Math.Min(MaxPins, Count(node) + 1).ToString(CultureInfo.InvariantCulture));
        }

        IReadOnlyList<string> cases = Cases(node);
        if (cases.Count >= MaxPins)
        {
            return node;
        }

        string next = node.Kind == GraphNodeKind.SwitchOnNumber
            ? ((cases.Count == 0 ? -1 : cases.Max(c => int.Parse(c, CultureInfo.InvariantCulture))) + 1).ToString(CultureInfo.InvariantCulture)
            : Tr.F("case {0}", cases.Count + 1);
        return node.WithValue(CasesKey, string.Join("\n", cases.Append(next)));
    }

    /// <summary>The last case or output removed, and the name of its pin (its wires are to be removed), or null if only one is left.</summary>
    public static (GraphNode Node, string Pin)? RemovePin(GraphNode node)
    {
        if (node.Kind == GraphNodeKind.Sequence)
        {
            int count = Count(node);
            return count <= 1 ? null : (node.WithValue(CountKey, (count - 1).ToString(CultureInfo.InvariantCulture)), SequencePin(count - 1));
        }

        IReadOnlyList<string> cases = Cases(node);
        return cases.Count <= 1 ? null : (node.WithValue(CasesKey, string.Join("\n", cases.SkipLast(1))), CasePin(cases.Count - 1));
    }

    private static NodeInfo Sequence(int count) =>
        new(GraphNodeKind.Sequence, NodeCategory.Action, Tr.T("Sequence"),
            Tr.T("Runs its outputs one after the other, from top to bottom (Add pin for more)."),
            flow: NodeFlow.Multi,
            execOutputs: [.. Enumerable.Range(0, count).Select(i => new PinInfo(SequencePin(i), Tr.F("Then {0}", i), DataType.Exec))]);

    private static NodeInfo Switch(GraphNodeKind kind, IReadOnlyList<string> cases)
    {
        bool number = kind == GraphNodeKind.SwitchOnNumber;
        return new NodeInfo(kind, NodeCategory.Condition, number ? Tr.T("Switch on number") : Tr.T("Switch on text"),
            number
                ? Tr.T("Runs the output of the case equal to the number, otherwise Default. The cases are set in Properties (one per line), or added with Add pin.")
                : Tr.T("Runs the output of the case equal to the text (case counts), otherwise Default. The cases are set in Properties (one per line), or added with Add pin."),
            inputs: [new PinInfo(ValueInput, Tr.T("Value"), number ? PinType.Number : PinType.Text, number ? "0" : string.Empty)],
            flow: NodeFlow.Multi,
            execOutputs:
            [
                .. cases.Select((c, i) => new PinInfo(CasePin(i), c, DataType.Exec)),
                new PinInfo(DefaultPin, Tr.T("Default"), DataType.Exec),
            ]);
    }
}
