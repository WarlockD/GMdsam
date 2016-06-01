using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameMaker
{
    public struct PlainTextWriterSettings
    {
        public int LeftMargin;
        public int RightMargin;
        public int LineWidthMax;
        public int SpacesPerTab;
        public static readonly PlainTextWriterSettings Default = new PlainTextWriterSettings() { LeftMargin = 0, RightMargin = 0, LineWidthMax = 120, SpacesPerTab = 4 };
    }
    public class PlainTextWriter : System.IO.TextWriter
    {
        static Regex regex_newline = new Regex("(\r\n|\r|\n)", RegexOptions.Compiled);
        static Dictionary<int, string> identCache = new Dictionary<int, string>();
        static string FindIdent(int count)
        {
            if (count == 0) return "";
            string identString;
            if (!identCache.TryGetValue(count, out identString))
                identCache[count] = identString = new string(' ', count * 4);
            return identString;
        }
        static PlainTextWriter()
        {
            identCache[0] = "";
        }

        StringBuilder line;
        PlainTextWriterSettings _settings;
        TextWriter writer;
        bool ownWriter;
        char prev;

        PlainTextWriterSettings Settings { get { return _settings; } set { _settings = value; } }
        public int LineNumber { get; private set; }
        public int LineLength { get { return line.Length; } }
        public int RawLineLength { get { return currentIdent.Length + _lineheader.Length + line.Length; } }

        public string CurrentLine { get { return line.ToString(); } set { line.Clear(); line.Append(value??""); } }
        string currentIdent;
        int ident;
        void Init(PlainTextWriterSettings settings)
        {
            this.Settings = settings; // PlainTextWriterSettings.Default;
            this.line = new StringBuilder(this.Settings.LineWidthMax*2);
            this.ownWriter = false;
            this.Indent = 0;
            this.LineNumber = 1;
            this.prev = default(char);
        }
        public PlainTextWriter(TextWriter writer)
        {
            Init(PlainTextWriterSettings.Default);
            this.writer = writer;
            this.ownWriter = false;
        }
        public PlainTextWriter(string filename)
        {
            Init(PlainTextWriterSettings.Default);
            this.writer = new StreamWriter(filename);
            this.ownWriter = true;
        }
        public PlainTextWriter()
        {
            Init(PlainTextWriterSettings.Default);
            this.writer = new StringWriter();
            this.ownWriter = true;
        }
        string _lineheader;
        public string LineHeader
        {
            get { return _lineheader; }
            set
            {
                _lineheader = value ?? "";// make sure there is always something
            }
        }
        public int Indent
        {
            get { return ident; }
            set
            {
                ident = value;
                currentIdent = FindIdent(ident * Settings.SpacesPerTab);
            }
        }

        public override Encoding Encoding
        {
            get
            {
                return writer.Encoding;
            }
        }

        public override void WriteLine()
        {
            writer.Write(_lineheader);
            writer.Write(currentIdent);
            writer.Write(line.ToString());
            writer.WriteLine();
            LineNumber++;
            line.Clear();
        }
        
        public override void Write(char c)
        {
            switch (c)
            {
                case '\t':
                    line.Append(FindIdent(Settings.SpacesPerTab));
                    c = default(char);
                    break;
                case '\n':
                case '\r':
                    if (prev != c && (prev == '\n' || prev == '\r'))
                        c = default(char);
                    break;
            }
            if(c == default(char)) line.Append(c); // ignore it if zero
            prev = c;
        }
        public override void Write(string s)
        {
            foreach (var c in s) Write(c);
        }
        public override void Write(string msg, object o)
        {
            line.AppendFormat(msg, o);
        }
        public override void Write(string msg, object o, object o1)
        {
            line.AppendFormat(msg, o, o1);
        }
        public override void Write(string msg, object o, object o1, object o2)
        {
            line.AppendFormat(msg, o, o1, o2);
        }
        public override void Write(string msg, params object[] o)
        {
            line.AppendFormat(msg, o);
        }
        public override void WriteLine(string msg, object o)
        {
            line.AppendFormat(msg, o);
            WriteLine();
        }
        public override void WriteLine(string msg, object o, object o1)
        {
            line.AppendFormat(msg, o, o1);
            WriteLine();
        }
        public override void WriteLine(string msg, object o, object o1, object o2)
        {
            line.AppendFormat(msg, o, o1, o2);
            WriteLine();
        }
        public override void WriteLine(string msg, params object[] o)
        {
            line.AppendFormat(msg, o);
            WriteLine();
        }
        public override void WriteLine(string msg)
        {
            line.Append(msg);
            WriteLine();
        }
        public override void Flush()
        {
            writer.Flush();
        }
        public override string ToString()
        {
            if (ownWriter) return writer.ToString();
            else return "LineNumber=" + LineNumber + " CurrentLine=" + line.ToString();
        }
        ~PlainTextWriter()
        {
            Dispose(false);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (line.Length > 0) WriteLine();
                writer.Flush();
                if (ownWriter)
                {
                    writer.Dispose();
                    writer = null;
                }
                disposedValue = true;
            }
        }

        #endregion

    }
}
