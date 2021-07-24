
// this file shared between MarcPlugin and MarcZilla and ConsoleTester
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;


namespace Library_Labels_Namespace

{
    // poop we could add some setting flags like "LogRankingResults" and "LogRuleExceptions" and the like
    // put these into the settings form, and use them in the different classes to filter what gets logged.
    // no change here of course

    public static class Log
    {
        private class LogEntry
        {
            public string text;
            public Color color;
            public LogEntry(string s, Color c)
            {
                text = s;
                color = c;
            }
        }

        public static RichTextBox RichTextBox { get; }
        public static int MaxLines { get; set; }
        public static bool MessageBoxErrors { get; set; }
        public static bool MessageBoxEverything { get; set; }
        private static Queue<LogEntry> LogQueue;
        private static bool Suspend;

        static Log()
        {
            RichTextBox = new RichTextBox();
            MessageBoxErrors = false;
            MessageBoxEverything = false;
            MaxLines = 10000;
            LogQueue = new Queue<LogEntry>();
            Suspend = false;
        }

        public static void Pause()
        {
            Suspend = true;
        }
        public static void Resume()
        {
            Suspend = false;
            while (LogQueue.Count > 0) updateRichTextBox(LogQueue.Dequeue());
        }

        public static void AppendError(string s, bool messageBoxThis = false)
        {
            if (messageBoxThis || MessageBoxErrors || MessageBoxEverything) s.MsgBox(MessageBoxIcon.Error);
            formatLogEntryAndPush(s, Color.Red);
        }

        public static void AppendWarning(string s)
        {
            if (MessageBoxEverything) s.MsgBox(MessageBoxIcon.Warning);
            formatLogEntryAndPush(s, Color.DarkOrange);
        }

        public static void AppendSuccess(string s)
        {
            if (MessageBoxEverything) s.MsgBox();
            formatLogEntryAndPush(s, Color.Green);
        }

        public static void AppendInformation(string s)
        {
            if (MessageBoxEverything) s.MsgBox(MessageBoxIcon.Information);
            formatLogEntryAndPush(s, Color.Black);
        }

        private static void formatLogEntryAndPush(string s, Color color)
        {
            string timeStamp = DateTime.Now.ToLocalTime().ToString();
            string text;
            string[] lines = s.Trim().Replace("\r", "").Split(new char[] { '\n' });


            if (lines.Length == 0) text = "";
            else if (lines.Length == 1) text = $"{timeStamp}     {lines[0]}";
            else text = $"{timeStamp}\n{lines.Prefix("----").ToText()}";
            LogEntry entry = new LogEntry(text, color);
            processFormattedEntry(entry);
        }

        public static void AppendRaw(string s, int n = 4)
        {
            if (MessageBoxEverything) s.MsgBox(MessageBoxIcon.Information);
            processFormattedEntry(new LogEntry(s + new string('\n', n), Color.Black));
        }


        public static void AppendBlankLines(int n = 2)
        {
            LogEntry entry = new LogEntry(new string('\n', n), Color.Black);
            processFormattedEntry(entry);
        }

        private static void processFormattedEntry(LogEntry entry)
        {
            if (Suspend) LogQueue.Enqueue(entry);
            else updateRichTextBox(entry);
        }

        // ..........................................................
        //      thread safe access to the rich text box
        // ..............................................................




        private static void updateRichTextBox(LogEntry entry)
        {
            if (RichTextBox.InvokeRequired)
            {
                RichTextBox.BeginInvoke(new Action(delegate
                {
                    updateRichTextBox(entry);
                }));
                return;
            }

            int userStart = 0; int userLength = 0;

            if (RichTextBox.SelectionStart != RichTextBox.TextLength)
            {
                userStart = RichTextBox.SelectionStart;
                userLength = RichTextBox.SelectionLength;
            }
            else
                userStart = userLength = -1;

            string text = entry.text;
            int textLines = text.LineCount();

            if (RichTextBox.Lines.Length + textLines >= MaxLines)
            {
                RichTextBox.Lines = RichTextBox.Lines.Skip(textLines).ToArray();
                userStart -= entry.text.Length; // and if its negative thats okay, will leave cursor at end!
            }



            RichTextBox.SelectionStart = RichTextBox.TextLength;
            RichTextBox.SelectionLength = 0;
            RichTextBox.SelectionColor = entry.color;

            RichTextBox.AppendText(text + "\n");
            RichTextBox.SelectionColor = RichTextBox.ForeColor;


            if (userStart >= 0)
            {
                RichTextBox.SelectionStart = userStart;
                RichTextBox.SelectionLength = userLength;
            }

        }

        public static string ToText()
        {
            return RichTextBox.Text.Replace("\r", "");
        }


    }

}
