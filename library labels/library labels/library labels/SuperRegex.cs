using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Library_Labels_Namespace
{
    public class SuperRegex
    {
        string pattern;
        Regex regex;
        Match match = null;
        List<NamedCapture> creature;

        public SuperRegex(string pattern)
        {
            this.pattern = pattern;
            regex = new Regex(pattern);
            creature = null;
        }

        public Match Match(string input)
        {
            match = regex.Match(input);
            return match;
        }
        public bool Success { get { return (match == null) ? false : match.Success; } }
        public string Pattern { get { return pattern; } }


        public List<NamedCapture> ToNamedCaptures()
        {
            string[] names = regex.GetGroupNames(); // this will include bogus groups that I was too lazy to weed out...
            int n = names.Length;

            int[] indices = new int[n];
            int[] counts = new int[n];
            CaptureCollection[] captures = new CaptureCollection[n];

            for (int i = 0; i < n; i++)
            {
                indices[i] = 0;
                captures[i] = match.Groups[names[i]].Captures;
                counts[i] = captures[i].Count;
            }
            creature = new List<NamedCapture>();

            while (true)
            {
                // find the lowest starting capture in our array of captures
                int besti = 0;
                int bestStart = int.MaxValue;
                bool noMoreCaptures = true;     // assume the worst, prove me wrong

                for (int i = 0; i < n; i++)
                {
                    if (indices[i] == counts[i]) continue;
                    noMoreCaptures = false;

                    int start = captures[i][indices[i]].Index;
                    if (start < bestStart)
                    {
                        bestStart = start;
                        besti = i;
                    }
                }
                if (noMoreCaptures) break;

                NamedCapture nc = new NamedCapture(names[besti], captures[besti][indices[besti]]);
                creature.Add(nc);
                indices[besti]++;
            }
            return creature;
        }

    }
    public class NamedCapture
    {
        public string name;
        public Capture capture;
        public NamedCapture(string name, Capture capture)
        {
            this.name = name;
            this.capture = capture;
        }
        public string ToText()
        {
            return "{0} {1}...{2}".WithArgs(name, capture.Index, capture.Index + capture.Length);
        }
    }

}
