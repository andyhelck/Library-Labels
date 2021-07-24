using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Library_Labels_Namespace
{
    public class CsvParser
    {
        string delimiter;
        string textQualifier;
        string repeatFieldDelimiter;
        int columnCount;
        //public bool wellFormed;
        // poop we have 2 mechanisms that conflict overlap, a big try catch around this whole thingy, and a flag wellformed. which do we want?
        // I commented out well formed just to see what would havppen 7/28/2020
        // maybe all I need is a way to make the exception polite, like return a nicer string.
        SuperRegex Parser { get; set; }

        public CsvParser(string delimiter, string textQualifier, string repeatFieldDelimiter, ref string headerLine)
        {
            this.delimiter = delimiter;
            this.textQualifier = textQualifier;
            this.repeatFieldDelimiter = repeatFieldDelimiter;

            fixHeaderString(ref headerLine);
            makeRegex(headerLine);
        }

        // so poop when we throw exceptions it is a bit abrupt for the user. What we want to do is issue some kind of polite error message
        // and recover gracefully. what does graceful recovery  look like?
        // do we return like a null CsvParser? some dummy version that doesnt do aything?
        // well the only thing this class does is ParseLine, which is already willing to return null in place of string array.
        // Is anybody checking for that possibility?
        // so all of this is properly inside of a try...catch. so maybe what I really need is to address things at that level. perhaps we don't
        // need to 

        public CsvParser(string firstColumnName, string RFD, ref string headerLine)
        {
            //wellFormed = false;
            columnCount = 0;
            Parser = null;

            if (firstColumnName == "") throw new Exception("First Column Name cannot be empty string!");

            int index = headerLine.IndexOf(firstColumnName);
            if (index < 0) throw new Exception("Cannot Match First Column Name with file Header");

            if (index == 0)
            {
                textQualifier = "";
                delimiter = headerLine.Substring(firstColumnName.Length, 1);
            }

            else if (index == 1) // there is a text qualifier. probably a quote but we cant assume
            {
                textQualifier = headerLine.Substring(0, 1);
                delimiter = headerLine.Substring(firstColumnName.Length + 2, 1);
            }
            else throw new Exception("Data matching First Column must be in the first column!");

            repeatFieldDelimiter = RFD;
            fixHeaderString(ref headerLine);
            makeRegex(headerLine);
        }

        private void fixHeaderString(ref string headerLine) // this seems to work fine, but the results are not showing up in the DGV
        {
            // "CALL #(ITEM)","BARCODE","245|a","TITLE"

            if (delimiter == "|")
            {
                Regex fixMarcTag = new Regex(@"(?<tag>\d\d\d)\|(?<indicator>[a-z])");
                headerLine = fixMarcTag.Replace(headerLine, "${tag}_${indicator}");
            }
        }

        private void makeRegex(string headerLine)
        {

            //wellFormed = false;

            if (delimiter == "")
            {
                "Delimiter cannot be empty string".MsgBox();
                return;
            }

            if (repeatFieldDelimiter == "")
            {
                "repeatFieldDelimiter cannot be empty string".MsgBox();
                return;
            }

            columnCount = 0;
            Parser = null;

            if (textQualifier == "")
            {
                string repeatField = @"(?<repeatfield>[^{0}{1}]*)"
                    .WithArgs(repeatFieldDelimiter, delimiter);
                string field = @"(?<field>{0}({1}{0})*)"
                    .WithArgs(repeatField, Regex.Escape(repeatFieldDelimiter));
                string pattern = @"^{0}({1}{0})*$".WithArgs(field, Regex.Escape(delimiter));
                Parser = new SuperRegex(pattern);
            }

            else
            {
                string qualifiedRepeatField = @"({0}(?<repeatfield>[^{1}]*){0})"
                    .WithArgs(Regex.Escape(textQualifier), textQualifier);
                string qualifiedField = @"(?<field>{0}([^{1}]{0})*)"	//  grab the RFD
                    .WithArgs(qualifiedRepeatField, delimiter);
                string pattern = @"^{0}({1}{0})*$"
                    .WithArgs(qualifiedField, Regex.Escape(delimiter));
                Parser = new SuperRegex(pattern);

            }

            Match m = Parser.Match(headerLine);
            if (!m.Success)
                return;

            columnCount = m.Groups["field"].Captures.Count;
            //wellFormed = true;
        }


        public string[] ParseLine(string line, bool firstRepeatFieldOnly = false)
        {
            Match m = Parser.Match(line);
            if (!m.Success) return null;

            List<NamedCapture> nameCaps = Parser.ToNamedCaptures();
            StringBuilder field = null;
            List<StringBuilder> fields = new List<StringBuilder>();
            bool isFirst = false;

            foreach (NamedCapture nc in nameCaps)
            {
                switch (nc.name)
                {
                    case "field":
                        field = new StringBuilder();
                        fields.Add(field);
                        isFirst = true;
                        break;
                    case "repeatfield":
                        if (isFirst || !firstRepeatFieldOnly)
                            field.AppendLine(nc.capture.ToString());
                        isFirst = false;
                        break;
                    default:
                        break;
                }

            }
            // okay, we should have n stringbuilders
            string[] results = new string[fields.Count];
            for (int i = 0; i < fields.Count; i++) results[i] = fields[i].ToString().TrimEnd();
            return results;
        }

    }
}
