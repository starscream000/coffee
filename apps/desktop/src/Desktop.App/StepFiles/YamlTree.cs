// A small tree of a YAML document with the position of every node in the
// text, read with YamlDotNet's parser (ADR D0007). The step list needs
// positions, not values: it finds each step's lines and rewrites only those,
// so the comments and layout of the rest of the file stay as they are.

using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Desktop.App.StepFiles;

/// <summary>A node of a YAML document and where it is in the text.</summary>
/// <param name="Start">Offset of its first character.</param>
/// <param name="End">Offset just after its last character.</param>
/// <param name="Line">1-based line of its first character.</param>
public abstract record YamlNode(int Start, int End, int Line);

/// <summary>A scalar.</summary>
/// <param name="Start">Offset of its first character (the opening quote of a quoted scalar).</param>
/// <param name="End">Offset just after its last character.</param>
/// <param name="Line">1-based line of its first character.</param>
/// <param name="Value">Its value, unquoted.</param>
/// <param name="IsPlain">True when written without quotes or block indicators.</param>
public sealed record YamlScalar(int Start, int End, int Line, string Value, bool IsPlain) : YamlNode(Start, End, Line)
{
    /// <summary>True for an empty plain scalar, which YAML reads as null (<c>key:</c> with nothing after it).</summary>
    public bool IsNull => IsPlain && (Value.Length == 0 || Value is "~" or "null" or "Null" or "NULL");
}

/// <summary>A mapping with scalar keys.</summary>
/// <param name="Start">Offset of its first character.</param>
/// <param name="End">Offset just after its last character.</param>
/// <param name="Line">1-based line of its first character.</param>
/// <param name="IsFlow">True when written in braces.</param>
/// <param name="Entries">Its keys and values, in order.</param>
public sealed record YamlMapping(int Start, int End, int Line, bool IsFlow, IReadOnlyList<KeyValuePair<YamlScalar, YamlNode>> Entries) : YamlNode(Start, End, Line)
{
    /// <summary>The value of a key, or null when the key is absent.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value node.</returns>
    public YamlNode? Get(string key) => Entries.FirstOrDefault(e => e.Key.Value == key).Value;

    /// <summary>The key node of a key, or null when absent.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The key's scalar.</returns>
    public YamlScalar? KeyOf(string key) => Entries.FirstOrDefault(e => e.Key.Value == key).Key;
}

/// <summary>A sequence.</summary>
/// <param name="Start">Offset of its first character.</param>
/// <param name="End">Offset just after its last character.</param>
/// <param name="Line">1-based line of its first character.</param>
/// <param name="IsFlow">True when written in brackets.</param>
/// <param name="Items">Its items, in order.</param>
public sealed record YamlSequence(int Start, int End, int Line, bool IsFlow, IReadOnlyList<YamlNode> Items) : YamlNode(Start, End, Line);

/// <summary>Why a text could not be read as a tree.</summary>
public sealed class YamlTreeException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong, for people.</param>
    /// <param name="line">The 1-based line, when known.</param>
    public YamlTreeException(string message, int? line = null)
        : base(message) => LineNumber = line;

    /// <summary>Creates the exception.</summary>
    public YamlTreeException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong.</param>
    public YamlTreeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong.</param>
    /// <param name="innerException">The cause.</param>
    public YamlTreeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The 1-based line of the problem, when known.</summary>
    public int? LineNumber { get; }
}

/// <summary>Reads YAML text into a <see cref="YamlNode"/> tree.</summary>
public static class YamlTree
{
    /// <summary>Reads the single document of a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The document's root node; null for an empty document.</returns>
    /// <exception cref="YamlTreeException">The text is not valid YAML, has several documents, or uses anchors, aliases, tags or complex keys, which the step list does not handle.</exception>
    /// <example><c>YamlTree.Read("steps:\n  - back\n")</c> is a mapping whose <c>steps</c> value is a sequence of one scalar.</example>
    public static YamlNode? Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parser = new Parser(new StringReader(text));
        try
        {
            Expect<StreamStart>(parser);
            if (parser.Accept<StreamEnd>(out _))
            {
                return null;
            }

            Expect<DocumentStart>(parser);
            var root = parser.Accept<DocumentEnd>(out _) ? null : ReadNode(parser, text);
            Expect<DocumentEnd>(parser);
            if (!parser.Accept<StreamEnd>(out _))
            {
                throw new YamlTreeException("The file holds more than one YAML document.", Line(parser.Current!.Start));
            }

            return root;
        }
        catch (YamlException ex)
        {
            throw new YamlTreeException($"The file is not valid YAML: {ex.Message}", (int)ex.Start.Line);
        }
    }

    private static T Expect<T>(IParser parser)
        where T : ParsingEvent =>
        parser.Consume<T>();

    private static YamlNode ReadNode(IParser parser, string text)
    {
        var current = parser.Current ?? throw new YamlTreeException("The YAML ends unexpectedly.");
        switch (current)
        {
            case AnchorAlias alias:
                throw new YamlTreeException("The file uses a YAML alias (*name), which the step list cannot show.", Line(alias.Start));
            case NodeEvent { Anchor.IsEmpty: false } anchored:
                throw new YamlTreeException("The file uses a YAML anchor (&name), which the step list cannot show.", Line(anchored.Start));
            case NodeEvent { Tag.IsEmpty: false } tagged when tagged is not Scalar { Tag.Value: "!" }:
                throw new YamlTreeException("The file uses a YAML tag (!name), which the step list cannot show.", Line(tagged.Start));
            case Scalar scalar:
                parser.MoveNext();
                return new YamlScalar(Offset(scalar.Start), Offset(scalar.End), Line(scalar.Start), scalar.Value, scalar.Style == ScalarStyle.Plain);
            case SequenceStart start:
                {
                    parser.MoveNext();
                    var items = new List<YamlNode>();
                    while (!parser.Accept<SequenceEnd>(out _))
                    {
                        items.Add(ReadNode(parser, text));
                    }

                    var end = parser.Consume<SequenceEnd>();
                    var isFlow = start.Style == SequenceStyle.Flow;
                    return new YamlSequence(Offset(start.Start), isFlow ? FlowEnd(text, end, ']') : BlockEnd(items, start), Line(start.Start), isFlow, items);
                }

            case MappingStart start:
                {
                    parser.MoveNext();
                    var entries = new List<KeyValuePair<YamlScalar, YamlNode>>();
                    while (!parser.Accept<MappingEnd>(out _))
                    {
                        if (parser.Current is not Scalar)
                        {
                            throw new YamlTreeException("The file uses a complex mapping key, which the step list cannot show.", Line(parser.Current!.Start));
                        }

                        var key = (YamlScalar)ReadNode(parser, text);
                        entries.Add(new(key, ReadNode(parser, text)));
                    }

                    var end = parser.Consume<MappingEnd>();
                    var isFlow = start.Style == MappingStyle.Flow;
                    var ends = entries.Count == 0 ? [] : new List<YamlNode> { entries[^1].Value.End >= entries[^1].Key.End ? entries[^1].Value : entries[^1].Key };
                    return new YamlMapping(Offset(start.Start), isFlow ? FlowEnd(text, end, '}') : BlockEnd(ends, start), Line(start.Start), isFlow, entries);
                }

            default:
                throw new YamlTreeException($"Unexpected YAML ({current.GetType().Name}).", Line(current.Start));
        }
    }

    /// <summary>A block collection ends where its last child ends (its end event points at the next token).</summary>
    private static int BlockEnd(List<YamlNode> children, ParsingEvent start) =>
        children.Count == 0 ? Offset(start.End) : Math.Max(children[^1].End, Offset(start.Start));

    /// <summary>A flow collection ends just after its closing bracket (its end event points at the bracket).</summary>
    private static int FlowEnd(string text, ParsingEvent end, char bracket)
    {
        var at = text.IndexOf(bracket, Math.Min(Offset(end.Start), text.Length));
        return at < 0 ? Offset(end.End) : Math.Max(at + 1, Offset(end.End));
    }

    private static int Offset(Mark mark) => checked((int)mark.Index);

    private static int Line(Mark mark) => checked((int)mark.Line);
}
