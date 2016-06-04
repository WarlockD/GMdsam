using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public static readonly PlainTextWriterSettings Default = new PlainTextWriterSettings() { LeftMargin = 0, RightMargin = 0, LineWidthMax = 120, SpacesPerIdent = 4, TabMarks = 6, HeaderOvewritesIdent = false };
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

        StringBuilder line ;     // Current line
        StringBuilder buffer;   // buffer for eveything
        // Ok, so there are three ways this thing can write too
        Stream _stream ;  // first is a raw stream, memory stream, file stream, etc
        TextWriter _writer ; // A text writer, it could be anything, sefaults to StreamWriter so we can have a base stream
        bool ownedWriter ;
        PlainTextWriterSettings _settings;
        char prev;
        bool startwritten;
        string _lineheader;
        int _flushedPosition;
        int tabPos;
        public int[] TabStops { get; set; }
        public int Indent { get; set; }

        public string CurrentLine { get { return line.ToString(); } set { line.Clear(); line.Append(value ?? ""); } }
        public Stream BaseStream {
            get {
                return _stream;
            }
            set {
                ownedWriter = false;
                _stream = value;
            }
        } 

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
            string data = ToString();
            using (StreamWriter sw = new StreamWriter(filename)) sw.Write(ToString());
        }
        public  virtual async Task AsyncWriteToFile(string filename)
        {
            string data = ToString();
            using (StreamWriter sw = new StreamWriter(filename)) await sw.WriteAsync(data);
        }


        public void Clear()
        {
            buffer.Clear();
            line.Clear();
            LineNumber = 1;
            prev = default(char);
            Indent = 0;
            startwritten = false;
            this._flushedPosition = 0;
            this.tabPos = 0;
        }
        public void ClearLine()
        {
            line.Clear();
        }

        public StringBuilder GetStringBuilder()
        {
            return buffer;
        }
    
        void Init(PlainTextWriterSettings settings,  Encoding encoding)
        {
            this.Settings = settings; // PlainTextWriterSettings.Default;
            this._encoding = encoding ?? Encoding.Default;
            this._stream = null;
            this._writer = null;
            this.line = new StringBuilder(this.Settings.LineWidthMax*2);
            this.buffer = new StringBuilder(this.Settings.LineWidthMax * 50); // default for 50 lines of data
            this.Indent = 0;
            this.ownedWriter = false;
            this.LineNumber = 1;
            this.prev = default(char);
            this.startwritten = false;
            this._lineheader = null;
            this._flushedPosition = 0;
            this.tabPos = 0;
            this.TabStops = null;
        }
        public PlainTextWriter(TextWriter writer)
        {
            Init(PlainTextWriterSettings.Default, writer.Encoding);
            this._writer = writer;
        }
        public PlainTextWriter(Stream stream, Encoding encoding=null)
        {
            Init(PlainTextWriterSettings.Default, encoding);
            this._stream = stream;
        }
        public PlainTextWriter(string filename, Encoding encoding = null)
        {
            Init(PlainTextWriterSettings.Default, encoding);
            FileStream fs = new FileStream(filename, FileMode.Create);
            this._stream = fs;
            this.ownedWriter = true;
        }
        public PlainTextWriter()
        {
            Init(PlainTextWriterSettings.Default,null);
        }
       
        public string LineHeader
        {
            get { return _lineheader; }
            set
            {
                _lineheader = string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        Encoding _encoding;
        public override Encoding Encoding
        {
            get
            {
                return _encoding;
            }
        }
        public override void WriteLine()
        {
            buffer.Append(line);
            buffer.AppendLine();
            LineNumber++;
            tabPos = 0;
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
                    buffer.Append(_lineheader);
                }
                if (space_to_skip > 0) buffer.Append(' ', space_to_skip);
                startwritten = true;
            }
            switch (c)
            {
                case '\t':
                    {
                        if(TabStops != null && tabPos < TabStops.Length)
                        {
                            int tab = TabStops[tabPos++];
                            if (tab < line.Length) line.Append(' ', line.Length - tab);
                        } else
                        {
                            int div = (line.Length / Settings.TabMarks) * Settings.TabMarks + Settings.TabMarks;
                            int mod = line.Length % Settings.TabMarks;
                            int spaces_needed = div - line.Length;
                            line.Append(' ', spaces_needed);
                        }
                        
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
        /// <summary>
        /// Flushes the buffer to the streams. 
        /// </summary>
        public override void Flush()
        {
            if (_stream == null && _writer == null) return; // drop out otherwise
            if (this._flushedPosition < buffer.Length)
            {
                int size = buffer.Length - this._flushedPosition;
                string data = buffer.ToString(this._flushedPosition, size);
                if (_stream != null)
                {
                    byte[] bytes = _encoding.GetBytes(data);
                    _stream.Write(bytes, 0, data.Length);
                    buffer.Clear();
                }
                if (_writer != null)
                {
                    _writer.Write(data);
                    buffer.Clear();
                }
                this._flushedPosition = buffer.Length;
            }
        }
        public override string ToString()
        {
            return buffer.ToString();
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
                if(ownedWriter)
                {
                    Flush();
                    if (_writer != null)
                    {
                        _writer.Flush();
                        _writer.Dispose();
                        _writer = null;
                    }
                    if (_stream != null)
                    {
                        _stream.Flush();
                        _stream.Dispose();
                        _stream = null;
                    }
                }
                disposedValue = true;
            }
        }

        #endregion

    }
}
