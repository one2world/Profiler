using System;

namespace Unity.MemoryProfiler.Editor.UI.PathsToRoot
{
    internal static class PathsToRootDetailView
    {
        static readonly char[] k_GenericBracesChars = { '<', '>' };

        public static string TruncateTypeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            name = name.Replace(" ", string.Empty);

            if (name.Contains('.'))
            {
                if (name.Contains('<'))
                {
                    var pos = 0;
                    while (pos < name.Length && name.IndexOf('<', pos + 1) != -1)
                    {
                        pos = name.IndexOf('<', pos + 1);
                        var next = name.IndexOfAny(k_GenericBracesChars, pos + 1);
                        var replacee = name.Substring(pos + 1, next - (pos + 1));

                        if (replacee.Contains(','))
                        {
                            var split = replacee.Split(',');
                            foreach (var part in split)
                            {
                                var truncated = TruncateTypeName(part);
                                pos += truncated.Length;
                                name = name.Replace(part, truncated);
                            }
                            continue;
                        }

                        if (!string.IsNullOrEmpty(replacee))
                        {
                            var truncatedReplacee = TruncateTypeName(replacee);
                            pos += truncatedReplacee.Length;
                            name = name.Replace(replacee, truncatedReplacee);
                        }
                    }
                }

                var nameParts = name.Split('.');
                name = string.Empty;
                int offset = 1;
                while (offset < nameParts.Length &&
                       (string.IsNullOrEmpty(nameParts[^offset]) || nameParts[^offset].StartsWith("<", StringComparison.Ordinal)))
                {
                    offset++;
                }

                var mainNamePart = nameParts.Length - offset;
                for (int i = 0; i < nameParts.Length; i++)
                {
                    if (i < mainNamePart)
                    {
                        if (nameParts[i].IndexOfAny(k_GenericBracesChars, 0) != -1)
                        {
                            if (!string.IsNullOrEmpty(name))
                                name += '.';
                            name += nameParts[i];
                        }
                        continue;
                    }

                    if (!string.IsNullOrEmpty(name))
                        name += '.';
                    name += nameParts[i];
                }

                return name;
            }

            if (name.Contains('<'))
            {
                var split = name.Split('<');
                if (split.Length > 0)
                {
                    var prefix = split[0];
                    var suffix = name.Substring(prefix.Length);
                    return prefix + suffix;
                }
            }

            return name;
        }
    }
}

