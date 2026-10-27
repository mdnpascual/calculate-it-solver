#nullable disable
using System.Collections.Generic;

namespace CalcHook
{
    /// <summary>Passive name and description list from a game passive text asset ("1. Near1: You win ...;").
    /// Mirrors TextsLoader.ReadTextAsset: line i is passive index i (empty lines dropped). Plain C# so the test project can link it.</summary>
    public static class PassiveTexts
    {
        /// <summary>One entry per non-empty line; (null, null) for a line the game's parser can't read, so indexes stay aligned.</summary>
        public static List<(string Name, string Description)> Parse(string text)
        {
            var list = new List<(string Name, string Description)>();
            if (string.IsNullOrEmpty(text)) return list;
            text = text.TrimStart('﻿');
            foreach (string raw in text.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;   // a "\r" line counts as empty
                int dot = line.IndexOf('.');
                int semi = line.IndexOf(';');
                if (dot >= 0 && semi > dot)
                {
                    string[] parts = line.Substring(dot + 1, semi - dot - 1).Trim().Split(':');
                    if (parts.Length >= 2)
                    {
                        list.Add((parts[0].Trim(), parts[1].Trim()));
                        continue;
                    }
                }
                list.Add((null, null));
            }
            return list;
        }
    }
}
