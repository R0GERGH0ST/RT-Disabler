using System.Text;

namespace RTFreedom.GameDetection;

/// <summary>
/// Represents a node within a Valve Data Format (VDF / ACF) tree hierarchy.
/// </summary>
public sealed class VdfNode
{
    /// <summary>
    /// Gets or sets the key or property name of this node.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the string scalar value, or null if this node represents an object with children.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets the dictionary of child nodes, indexed case-insensitively.
    /// </summary>
    public Dictionary<string, VdfNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether this node has children.
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// Gets the child node with the specified key, or null if not found.
    /// </summary>
    /// <param name="key">The child key.</param>
    /// <returns>The matching child node or null.</returns>
    public VdfNode? this[string key] => Children.TryGetValue(key, out var node) ? node : null;

    /// <summary>
    /// Retrieves a nested scalar value using path navigation separated by '/' or '.'.
    /// </summary>
    /// <param name="path">The slash or dot-separated path (e.g., "AppState/installdir").</param>
    /// <returns>The scalar value, or null if the path does not exist or has no scalar value.</returns>
    public string? GetValue(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Value;
        }

        var segments = path.Split(new[] { '/', '\\', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var current = this;

        // If root has children and first segment matches one of them, navigate down
        foreach (var segment in segments)
        {
            if (current == null || !current.Children.TryGetValue(segment, out var next))
            {
                // Fallback: check if the current node itself is already the container or if
                // path was searched ignoring top-level container (e.g. searching "installdir" directly)
                if (current != null && current.Children.Count == 1)
                {
                    var singleChild = current.Children.Values.First();
                    if (singleChild.Children.TryGetValue(segment, out var altNext))
                    {
                        current = altNext;
                        continue;
                    }
                }
                return null;
            }
            current = next;
        }

        return current?.Value;
    }
}

/// <summary>
/// Lightweight parser for Steam KeyValues / VDF / ACF format files.
/// </summary>
public static class VdfParser
{
    /// <summary>
    /// Parses a VDF/ACF file from disk into a root <see cref="VdfNode"/>.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the VDF/ACF file.</param>
    /// <returns>A root VdfNode containing the parsed structure.</returns>
    public static VdfNode Parse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new VdfNode { Key = "root" };
        }

        try
        {
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            return ParseText(content);
        }
        catch
        {
            return new VdfNode { Key = "root" };
        }
    }

    /// <summary>
    /// Parses a VDF/ACF formatted string into a root <see cref="VdfNode"/>.
    /// </summary>
    /// <param name="text">The raw VDF/ACF string content.</param>
    /// <returns>A root VdfNode containing the parsed structure.</returns>
    public static VdfNode ParseText(string text)
    {
        var root = new VdfNode { Key = "root" };
        if (string.IsNullOrWhiteSpace(text))
        {
            return root;
        }

        var tokens = Tokenize(text);
        var stack = new Stack<VdfNode>();
        stack.Push(root);

        int i = 0;
        while (i < tokens.Count)
        {
            var token = tokens[i];

            if (token == "}")
            {
                if (stack.Count > 1)
                {
                    stack.Pop();
                }
                i++;
                continue;
            }

            if (token == "{")
            {
                // Unexpected open brace without preceding key
                i++;
                continue;
            }

            // At this point, token is a key. Look ahead to next token.
            string key = token;
            i++;

            if (i >= tokens.Count)
            {
                // Key with no value at end of input
                var keyOnlyNode = new VdfNode { Key = key };
                stack.Peek().Children[key] = keyOnlyNode;
                break;
            }

            var nextToken = tokens[i];

            if (nextToken == "{")
            {
                // This is a nested object
                var childNode = new VdfNode { Key = key };
                stack.Peek().Children[key] = childNode;
                stack.Push(childNode);
                i++; // consume '{'
            }
            else if (nextToken == "}")
            {
                // Empty value followed by close brace
                var childNode = new VdfNode { Key = key, Value = string.Empty };
                stack.Peek().Children[key] = childNode;
                // do not consume '}' so outer loop pops
            }
            else
            {
                // Key-value pair
                var childNode = new VdfNode { Key = key, Value = nextToken };
                stack.Peek().Children[key] = childNode;
                i++; // consume value token
            }
        }

        return root;
    }

    /// <summary>
    /// Parses a VDF/ACF file and looks up a specific value path.
    /// </summary>
    /// <param name="filePath">Path to the VDF/ACF file.</param>
    /// <param name="path">Dot or slash-delimited property path.</param>
    /// <returns>The string value, or null if not found.</returns>
    public static string? GetValue(string filePath, string path)
    {
        var root = Parse(filePath);
        return root.GetValue(path);
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        int length = text.Length;
        int i = 0;

        while (i < length)
        {
            char c = text[i];

            // Skip whitespace
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Skip single-line comments //
            if (c == '/' && i + 1 < length && text[i + 1] == '/')
            {
                i += 2;
                while (i < length && text[i] != '\r' && text[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            // Braces
            if (c == '{' || c == '}')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            // Quoted string
            if (c == '"')
            {
                i++; // skip opening quote
                var sb = new StringBuilder();
                while (i < length)
                {
                    char ch = text[i];
                    if (ch == '\\' && i + 1 < length)
                    {
                        char next = text[i + 1];
                        if (next == '"' || next == '\\')
                        {
                            sb.Append(next);
                            i += 2;
                            continue;
                        }
                        if (next == 'n')
                        {
                            sb.Append('\n');
                            i += 2;
                            continue;
                        }
                        if (next == 't')
                        {
                            sb.Append('\t');
                            i += 2;
                            continue;
                        }
                        sb.Append(ch);
                        i++;
                        continue;
                    }

                    if (ch == '"')
                    {
                        i++; // skip closing quote
                        break;
                    }

                    sb.Append(ch);
                    i++;
                }
                tokens.Add(sb.ToString());
                continue;
            }

            // Unquoted token
            var unquotedSb = new StringBuilder();
            while (i < length)
            {
                char ch = text[i];
                if (char.IsWhiteSpace(ch) || ch == '{' || ch == '}' || ch == '"')
                {
                    break;
                }
                unquotedSb.Append(ch);
                i++;
            }

            if (unquotedSb.Length > 0)
            {
                tokens.Add(unquotedSb.ToString());
            }
        }

        return tokens;
    }
}
