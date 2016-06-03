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
        public int SpacesPerIdent;
        public int TabMarks;
        public bool HeaderOvewritesIdent;
        public static readonly PlainTextWriterSettings Default = new PlainTextWriterSettings() { LeftMargin = 0, RightMargin = 0, LineWidthMax = 120, SpacesPerIdent = 4, TabMarks = 8, HeaderOvewritesIdent = false };
    }
    public class PlainTextWriter : System.IO.TextWriter
    {
        static Regex regex_newline = new Regex("(\r\n|\r|\n)", RegexOptions.Compiled);
        static Dictionary<int, string> identCache = new Dictionary<int, string>();
        static string FindIdent(int count)
        {
            if (count == 0) return "";
            if (count < 0) throw new Exception("Cannot have a space count less than 0");
            string identString;
            if (!identCache.TryGetValue(count, out identString))
                identCache[count] = identString = new string(' ', count);
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
        bool startwritten;
        public int Indent { get; set; }

        public string CurrentLine { get { return line.ToString(); } set { line.Clear(); line.Append(value ?? ""); } }
        public TextWriter BaseStream {  get { return writer; } }
        PlainTextWriterSettings Settings { get { return _settings; } set { _settings = value; } }
        public int LineNumber { get; private set; }
        public int Column { get { return line.Length; } }
        public int RawColumn
        { get {
                int size = 0;
                if (Indent != 0) size += Indent * _settings.SpacesPerIdent;
                if (_lineheader != null && _lineheader.Length > size) size += (size - _lineheader.Length);
                size += Column;
                return size;
            }
        }
        public virtual void WriteToFile(string filename)
        {
            if (!isStringWriter) throw new Exception("Cannot write a non string writer");
            string data = ToString();
            using (StreamWriter sw = new StreamWriter(filename)) sw.Write(ToString());
        }
        public  virtual async Task AsyncWriteToFile(string filename)
        {
            if (!isStringWriter) throw new Exception("Cannot write a non string writer");
            string data = ToString();
            using (StreamWriter sw = new StreamWriter(filename)) await sw.WriteAsync(data);
        }
        public bool isStringWriter {  get { return writer is StringWriter; } }

        public void Clear()
        {
            StringWriter swriter = writer as StringWriter;
            if (swriter == null) throw new Exception("Cannot clear a non string writer");
            swriter.GetStringBuilder().Clear();
            LineNumber = 1;
            prev = default(char);
            Indent = 0;
            _lineheader = null;
            startwritten = false;
        }
        public void ClearLine()
        {
            line.Clear();
        }
        public PlainTextWriter Clone()
        {
            if (!isStringWriter) throw new Exception("Cannot clone a non string writer");
            // we can only clone it when we are using a string writer, not sure I will ever use this feature anyway
            PlainTextWriter clone = new PlainTextWriter();
            clone.GetStringBuilder().Append(this.GetStringBuilder().ToString());
            clone.line.Append(this.line.ToString());
            clone.LineNumber = this.LineNumber;
            clone.prev = this.prev; // just in case
            clone._settings = this._settings;
            clone.startwritten = this.startwritten;
            return clone;
        }
        public StringBuilder GetStringBuilder()
        {
            StringWriter swriter = writer as StringWriter;
            if (swriter == null) throw new Exception("Cannot get string builder a non string writer");
            return swriter.GetStringBuilder();
        }
    
        void Init(PlainTextWriterSettings settings)
        {
            this.Settings = settings; // PlainTextWriterSettings.Default;
            this.line = new StringBuilder(this.Settings.LineWidthMax*2);
            this.ownWriter = false;
            this.Indent = 0;
            this.LineNumber = 1;
            this.prev = default(char);
            this.startwritten = false;
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
                _lineheader = string.IsNullOrEmpty(value) ? null : value;
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
            writer.Write(line.ToString());
            writer.WriteLine();
            LineNumber++;
            line.Clear();
            startwritten = false;
        }
        // Since eveything pipes though here and even if I override
        // alot I STILL have to check for new lines, better to just use this
        public override void Write(char c)
        {
            // fix newlines with a simple state machine
            if (prev == '\n' || prev == '\r')
            {
                if (prev != c && (c == '\n' || c == '\r'))
                {
                    prev = default(char);
                    return;// skip
                }
            }
            prev = c;
            if (!startwritten)
            {
                if (Indent > 0 && c == ' ') return; // if we are using ident, we skip the starting spaces
                int space_to_skip = Indent * _settings.SpacesPerIdent;
                if (_lineheader != null)
                {
                    if (_settings.HeaderOvewritesIdent)
                        if (_lineheader.Length < space_to_skip)
                            space_to_skip -= _lineheader.Length;
                        else
                            space_to_skip = 0;
                    writer.Write(_lineheader);
                }
                if (space_to_skip > 0) writer.Write(FindIdent(space_to_skip));
                startwritten = true;
            }
            switch (c)
            {
                case '\t':
                    {
                        int spaces_needed = line.Length == 0 ? Settings.TabMarks : Settings.TabMarks% line.Length ;
                        line.Append(FindIdent(spaces_needed));
                    }
                    c = default(char);
                    break;
                case '\n':
                case '\r':
                    WriteLine();
                    break;
                default:
                    line.Append(c); // ignore it if zero
                    break;
            }
        }
        public override void Flush()
        {
            // carful with flush, we clear the line WITHOUT doing a new line
            writer.Write(line.ToString());
            line.Clear();
            writer.Flush();
        }
        public override string ToString()
        {
            if (writer is StringWriter)
            {
                StringBuilder sb = new StringBuilder(100);
                sb.Append(GetStringBuilder());
                sb.Append(line.ToString());
                return sb.ToString();
            }
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
                
                if (ownWriter)
                {
                    writer.Flush();
                    writer.Dispose();
                    writer = null;
                }
                disposedValue = true;
            }
        }

        #endregion

    }
}
