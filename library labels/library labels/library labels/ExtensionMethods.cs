using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Library_Labels_Namespace
{
    public static class ExtensionMethods
    {
        #region working with Fonts
        public static Font Resize(this Font font, float newFontSize)
        {
            return new Font(font.FontFamily, newFontSize, font.Style);
        }

        public static string ToText(this Font font)
        {
            return "{0} {1} {2}".WithArgs(font.Name, font.Size, font.Style);
            //StringBuilder sb = new StringBuilder();
            //sb.AppendLine(font.Name);
            //sb.AppendLine(font.Size.ToString());

            //sb.AppendLine(font.Bold.ToString());
            //sb.AppendLine(font.Italic.ToString());
            //sb.AppendLine(font.Strikeout.ToString());
            //sb.AppendLine(font.Underline.ToString());
            //// those were booleans

            //sb.AppendLine(font.Style.ToString());

            //sb.AppendLine(font.SystemFontName.ToString());
            //sb.AppendLine(font.FontFamily.ToString());
            //return sb.ToString();
        }
        #endregion

        #region working with Points, Sizes and Rectangles
        // Rectangle and Size are both Structures, and are passed by value
        // so they cannot usefully modify 'this' but must return
        // a modified or unmodified version of themselves!

        public static string ToText(this Point p)
        {
            return "({0}, {1})".WithArgs(p.X, p.Y);
        }

        public static Point Negate(this Point p)
        {
            return new Point(-p.X, -p.Y);
        }

        public static Size Negate(this Size s)
        {
            return new Size(-s.Width, -s.Height);
        }

        public static Size Divide(this Size s, int d)
        {
            return new Size(s.Width / d, s.Height / d);
        }

        public static Size Subtract(this Size s, Point p)
        {
            return new Size(s.Width - p.X, s.Height - p.Y);
        }
        public static Point Plus(this Point p, Point q)
        {
            return new Point(p.X + q.X, p.Y + q.Y);
        }
        public static Point Rotate(this Point p)
        {
            return new Point(p.Y, p.X);
        }
        public static Size Rotate(this Size s)
        {
            return new Size(s.Height, s.Width);
        }
        public static Size Scale(this Size s, float z)
        {
            return new Size((int)(s.Width * z), (int)(s.Height * z));
        }
        public static Rectangle Rotate(this Rectangle r)
        {
            return new Rectangle(r.Location.Rotate(), r.Size.Rotate());
        }
        public static bool IsTall(this Size s)
        {
            return s.Height >= s.Width;
        }

        public static bool IsWide(this Size s)
        {
            return s.Width >= s.Height;
        }
        public static Rectangle Deflate(this Rectangle rectangle, int value)
        {
            int twice = 2 * value;
            if (rectangle.Width > twice && rectangle.Height > twice)
                return new Rectangle(rectangle.X + value, rectangle.Y + value, rectangle.Width - twice, rectangle.Height - twice);
            return rectangle;
        }

        public static Size ExpandToAtLeast(this Size s, Size t) // modify p to be at least as large as t in both dimensions
        {
            bool tooThin = (s.Width < t.Width);
            bool tooShort = (s.Height < t.Height);

            if (tooThin || tooShort) s = new Size(
                    tooThin ? t.Width : s.Width,
                    tooShort ? t.Height : s.Height);

            return s;
        }

        public static Size RestrictInWidth(this Size s, Size t) // modify p to be no wider than t
        {
            bool tooWide = (s.Width > t.Width);

            if (tooWide) s = new Size(
                    tooWide ? t.Width : s.Width, s.Height);

            return s;
        }
        public static Rectangle TrimToFitContainer(this Rectangle rectangle, Size container)
        {
            // note these may not be actually in a child parent relationship, but that is how we
            // interpret the coordinate system. So the child p'p location is relative to a parent
            // of the indicated size. We return the child p, still in parent coordinates,
            // but trimmed to fit within the parent.

            if (rectangle.X < 0)
            {
                rectangle.Width += rectangle.X;
                rectangle.X = 0;
            }
            if (rectangle.Y < 0)
            {
                rectangle.Height += rectangle.Y;
                rectangle.Y = 0;
            }

            Size available = container.Subtract(rectangle.Location);
            if (rectangle.Width > available.Width) rectangle.Width = available.Width;
            if (rectangle.Height > available.Height) rectangle.Height = available.Height;
            return rectangle;
        }
        //return pixelBounds.GreaterThan(panelLimits);
        public static bool GreaterThan(this Rectangle a, Rectangle b)
        {
            // return a > b
            return a.Left < b.Left || a.Right > b.Right || a.Top < b.Top || a.Bottom > b.Bottom;
        }

        public static bool GreaterThan(this Size a, Size b)
        {
            // return a > b
            return a.Width > b.Width || a.Height > b.Height;
        }
        #endregion

        #region detect scrollbars in RichTextBox - unused

        //[System.Runtime.InteropServices.DllImport("user32.dll")]
        //private extern static int GetWindowLong(IntPtr hWnd, int index);

        //public static bool VerticalScrollBarVisible(this RichTextBox richTextBox)
        //{
        //    int style = GetWindowLong(richTextBox.Handle, -16);
        //    return (style & 0x200000) != 0;
        //}





        #endregion

        #region string methods
        public static string ToNuked(this StringBuilder sb)
        {
            return sb.ToString().Replace("\r", "");
        }

        public static bool HasDarkChars(this string sample)
        {
            char[] walkin = sample.ToCharArray();
            foreach (char c in walkin) if (!Char.IsWhiteSpace(c)) return true;
            return false;
        }

        public static IEnumerable<string> RemoveEmptyEntries(this IEnumerable<string> lines)
        {
            return lines.Where(s => !string.IsNullOrWhiteSpace(s));
        }

        public static string ExpandEscapeChars(this string raw)
        {
            return raw.Replace(@"\n", "\n");
        }

        public static string ToCsvString(this IEnumerable<int> list)
        {
            return string.Join<int>(", ", list);
        }

        public static string ToCsvString(this MatchCollection mm, int index)
        {
            List<string> matches = new List<string>();
            foreach (Match m in mm)
                matches.Add(m.Groups[index].Value);
            return string.Join(", ", matches);
        }
        public static string ToCsvString(this MatchCollection mm, string name = "datum")
        {
            List<string> matches = new List<string>();
            foreach (Match m in mm)
                matches.Add(m.Groups[name].Value);
            return string.Join(", ", matches);
        }

        public static string ToCsvString(this Match m, string name = "datum")
        {
            // poop this time one Match
            List<string> data = new List<string>();
            foreach (Capture c in m.Groups[name].Captures)
                data.Add(c.Value);
            return string.Join(", ", data);
        }


        public static string GetDatum(this Match m, string name = "datum")
        {
            return (m.Success) ? m.Groups[name].Value : string.Empty;
        }




        public static string SplitCamelCase(this string input)
        {
            return Regex.Replace(input, "([A-Z])", " $1").Replace('_', ' ').Trim();
        }

        public static string WithArgs(this string format, params object[] args)
        {
            return string.Format(format, args);
        }

        public static int CountSubstring(this string s, string ss)
        {
            return (s.Length - s.Replace(ss, "").Length) / ss.Length;
        }

        public static string DoubleQuoted(this string s)
        {
            return "\"" + s + "\"";
        }
        public static string SquareBracketed(this string s)
        {
            return "[" + s + "]";
        }

        public static string PrePend(this string s, string prefix)
        {
            return prefix + s;
        }
        public static List<string> Suffix(this IEnumerable<string> lines, string suffix)
        {
            List<string> results = new List<string>();
            foreach (string s in lines) results.Add(s + suffix);
            return results;
        }

        public static string ConvertUriToPath(this string fileName)
        {
            Uri uri = new Uri(fileName);
            return uri.LocalPath;

            // Some people have indicated that uri.LocalPath doesn't 
            // always return the corret path. If that'p the case, use
            // the following line:
            // return uri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
        }
        public static string TruncateSierraTitle(this string title)
        {
            string[] breakers = { " / ", "[", ", by ", ". Edited by", ". General editor,", ". " };
            foreach (string b in breakers)
            {
                int n = title.IndexOf(b);
                if (n > 0) title = title.Substring(0, n);
            }
            return title;

        }


        public static string RemoveSubstrings(this string s, params string[] toRemove)
        {
            if (toRemove != null)
                foreach (string x in toRemove) s = s.Replace(x, string.Empty);
            return s;
        }

        public static string Nuke(this string s)
        {
            return s.Replace("\r", "");
        }
        #endregion

        #region split join
        public static string[] split_lines(this String s, StringSplitOptions o = StringSplitOptions.None)
        {
            return s.Split(new string[] { "\n" }, o);
        }

        public static string[] splitTokens(this String s)
        {
            return s.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static String joinTokens(params String[] tokens)
        {
            return string.Join("|", tokens);
        }

        public static string ToText(this IEnumerable<String> s)
        {
            return string.Join("\n", s);
        }
        #endregion

        #region message box
        const string msg_title = "Library Labels";
        public static DialogResult MsgBox(this string prompt)
        {
            return MessageBox.Show(prompt, msg_title, MessageBoxButtons.OK);
        }

        public static DialogResult MsgBox(this string prompt, MessageBoxButtons buttons)
        {
            return MessageBox.Show(prompt, msg_title, buttons);
        }

        public static DialogResult MsgBox(this string prompt, MessageBoxIcon icons)
        {
            return MessageBox.Show(prompt, msg_title, MessageBoxButtons.OK, icons);
        }

        public static DialogResult MsgBox(this string prompt, MessageBoxButtons buttons, MessageBoxIcon icons)
        {
            return MessageBox.Show(prompt, msg_title, buttons, icons);
        }

        public static DialogResult ErrorBox(this string prompt)
        {
            return MessageBox.Show(prompt, msg_title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static DialogResult QuestionBox(this string prompt)
        {
            return MessageBox.Show(prompt, msg_title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }




        #endregion

        #region clipboard
        public static string clipthis(this string s)
        {
            IDataObject iData = Clipboard.GetDataObject();

            iData.SetData(DataFormats.Text, true, s);
            Clipboard.SetDataObject(s, true);
            return s;
        }
        #endregion

        #region Directory Service Wrappers
        //..................................................................
        //
        //                  Directory Service Wrappers
        //..................................................................

        public static string SafeGetDirectoryName(this string path)
        {
            try { return Path.GetDirectoryName(path); }
            catch { return ""; }
        }

        public static string SafeGetFileName(this string path)
        {
            try { return Path.GetFileName(path); }
            catch { return ""; }
        }
        #endregion


        public const string labelPrefix = "    #";
        public const string summary_blab = "#" + vb4tab + "Summary ";
        public const RegexOptions regops = RegexOptions.IgnoreCase | RegexOptions.Multiline;
        public const string reg_callnum_092 = @"^ (082|092)  .*\$a(?<datum>[0-9.]+)"; // leading space added 11/12/16

        public const string reg_nonfiction = @"^ 655  .*Nonfiction"; // leading space added 11/12/16
        public const string reg_oversize = @"^ 300  .*\$c(?<datum>[0-9]+) cm"; // leading space added 11/12/16
        public const int Oversize_threshold = 35;      // poop guess...

        public const string reg_mystery = @"^ (650|655).*Mystery"; // leading space added 11/12/16, verified OK
        public const string reg_series = @"^ 490  .*"; // leading space added 11/12/16, verified OK

        public const string reg_graphic = @"^ 65\d  .*(Graphic novels|Comic books, strips, etc|COMICS & GRAPHIC NOVELS)";

        public const string reg_publisher_year = @"^ 260  .*\$b(?<publisher>.*)\$c(?<year>[0-9]{4})"; // leading space added 11/12/16



        public static string[] remove_hyphen_space = { "-", " " };
        public static string[] remove_title_junk = { "-", ":", ";", "'", ",", ".", "/", "{acute}" };
        public static string[] remove_leading_dot = { "." };



        public const string record_blab = vb4tab + "Record ";


        public const string section_bar = "\n-----------------------------------------------------------------------------------------\n";
        public const string RecordBar = "------------------------------------------------------------------------------------------------";
        public const string vb4tab = "\t\t\t\t";

        public static void richTextBoxSettings(this RichTextBox rtb)
        {
            rtb.Multiline = true;
            rtb.HideSelection = false;
            rtb.WordWrap = false;
            rtb.DetectUrls = true;
            rtb.ShortcutsEnabled = true;
            rtb.AutoWordSelection = false;
            rtb.BorderStyle = BorderStyle.FixedSingle;
            rtb.ScrollBars = RichTextBoxScrollBars.ForcedBoth; // must use forced if we are showing the selection margin http://msdn.microsoft.com/en-us/library/system.windows.forms.richtextbox.showselectionmargin(v=vs.110).aspx
            rtb.ShowSelectionMargin = true;
            rtb.EnableAutoDragDrop = true;
        }









        public static string AngleBracketed(this string s)
        {
            return "<" + s + ">";
        }

        public static string CurlyBraced(this string s)
        {
            return "{" + s + "}";
        }
        public static string Parenthesized(this string s)
        {
            return "(" + s + ")";
        }


        public static string RemoveCheckDigit(this string recordNumber)
        {
            return recordNumber.Substring(0, 8); // o1234567A, lose the A. a record number is seven digits!
        }
        public static List<string> Prefix(this IEnumerable<string> lines, string prefix)
        {
            List<string> results = new List<string>();
            foreach (string s in lines) results.Add(prefix + s);
            return results;
        }
        public static string AddCheckDigit(this string s, int nDataDigits)
        // bib and order records 7 data digits + the checkdigit for 8
        // item records          8 data digits + the checkdigit for 9
        // poop if we took strings with the prefix in place we could check this.
        // BTW this is likely to be different outside of Marmot.
        {
            if (s.Length == nDataDigits)
            {
                char[] charsPlusOne = (s + " ").ToCharArray(); // extra space at the end is for the check digit

                int x = 0;
                int m = 2;
                for (int i = 0; i < nDataDigits; i++)
                {
                    int a = charsPlusOne[(nDataDigits - 1) - i] - '0'; // 6,5,4,3,2,1,0
                    x += a * m;
                    m++;
                }
                int r = x % 11;
                char cd = (r == 10) ? 'x' : (char)(r + '0');
                charsPlusOne[7] = cd;

                return new string(charsPlusOne);
            }
            else return s + "a";
        }

        public static string ParseAndAddCheckDigit(this string s) // uses regex
        {
            // allow for .b or anything else, then take the 7 digits. allow that there might already be a checkdigit, specific or universal
            Regex reggie = new Regex(@"^(?<prefix>\.?[a-z]?)(?<digits>\d{7})[0-9xa]?$"); // poop do I already have this defined somewhere?
            Match ma = reggie.Match(s);
            if (ma.Success)
            {
                string digits7 = ma.GetDatum("digits");
                char[] chars8 = (digits7 + " ").ToCharArray(); // extra space at the end is for the check digit

                int x = 0;
                int m = 2;
                for (int i = 0; i < 7; i++)
                {
                    int a = chars8[6 - i] - '0';
                    x += a * m;
                    m++;
                }
                int r = x % 11;
                char cd = (r == 10) ? 'x' : (char)(r + '0');
                chars8[7] = cd;

                return ma.GetDatum("prefix") + new string(chars8);
            }
            return s + "a";
        }




        public static string[] SplitLines(this String s, StringSplitOptions options = StringSplitOptions.None)
        {
            return s.Split(new string[] { "\n", "\r\n" }, options);
        }

        public static string[] SplitAndTrimTokens(this String s, string delimiter = " ")
        {
            string[] tokens = s.Split(new string[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++) tokens[i] = tokens[i].Trim();
            return tokens;
        }

        public static string FirstWord(this String s)
        {
            string[] tokens = s.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            string[] skippers = new string[] { "A", "The" };
            foreach (string token in tokens)
                if (!skippers.Contains(token)) return token;
            return "FIRSTWORD";
        }
        public static String JoinTokens(params String[] tokens)
        {
            return string.Join("|", tokens);
        }
        public static String AppendToken(this string s, string t)
        {
            return s + "|" + t;
        }

        public static int LineCount(this string s)
        {
            int len = s.Length;
            int c = 0;
            for (int i = 0; i < len; i++)
            {
                if (s[i] == '\n') c++;
            }
            return c + 1;

        }



        public static string ClipThis(this string s) // poop does not work if called from the background worker
                                                     // https://stackoverflow.com/questions/4685237/how-can-i-make-a-background-worker-thread-set-to-single-thread-apartment
                                                     // must operate in a single thread apartment
        {
            IDataObject iData = Clipboard.GetDataObject();
            if (iData != null)
            {
                iData.SetData(DataFormats.Text, true, s);
                Clipboard.SetDataObject(s, true);
            }
            else Log.AppendError("Error: string.clipthis called on MT Thread");
            return s;
        }


        public static string ConvertSubfieldsToSpaces(this string s)
        {
            // take a call number with possible sub field
            // indicators, and make it print ready by stripping these out
            // so legal values in a call number are capital letters A-Z, digits, space and .
            // everything else is trash..but may need to be replaced with a space.

            Regex r = new Regex(@"\$[a-z]", regops);
            return r.Replace(s, " ").Trim();
        }

        public static string ExtractCallnum(this string fields) // multiline string should contain a 949 field
        {
            // okay..this is a complicated regex. a real call number comes with 
            // $dprefix$anumber$bname is the proper call number
            // sometimes it just has a leading $a or a leading $d
            string callnum = string.Empty;
            Regex regex949callnum = new Regex(@"^.949  ..(?<datum>(\$[abd][^$\n]+)+)", regops);
            Match m = regex949callnum.Match(fields);
            if (m.Success) callnum = m.Groups["datum"].Value.ConvertSubfieldsToSpaces();
            return callnum;

            //949  \1$aB JACKSON$h87$i1230002353964$ltlwnb$p26.99$sq$t7$z092
            //949  \\$dDVD 791.437 CUBAN$h87$i1230002495549$p24.98$z092
            //949  \\$dDVD 791.437 14$h87$i1230002495531$p24.98$z092

            // poop I think this is being called in places where we could not possibly have a call number
            // like we already know the fields are 000-800 and no 9xx fields.
        }

        public static string ExtractSubfield(this string fields, string indicator)
        {
            string subfield = string.Empty;
            string pattern = @"^.949  ..+\${0}(?<datum>[^$\n]+)".WithArgs(indicator);
            Regex r = new Regex(pattern);
            Match m = r.Match(fields);
            if (m.Success)
            {
                subfield = m.Groups["datum"].Value;
            }
            return subfield;
        }




    }

}

