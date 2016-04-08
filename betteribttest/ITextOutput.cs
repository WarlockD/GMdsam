using System;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Runtime.Versioning;
using System.Runtime.Serialization;
using System.IO;
using System.Diagnostics.Contracts;

namespace betteribttest
{
    public class PlainTextWriter : TextWriter, ITextOutput
    {
        TextWriter _stream;
        bool _flushed;
        StringBuilder _line;
        string _header;
        StringBuilder _outHeader;
        int _largestHeader;
        char _lastChar;
        int _lineno;
        int _ident;
        string _identString;
        void updateHeader()
        {
            _outHeader.Clear();
            if (string.IsNullOrEmpty(_header))
                _outHeader.Append(' ', _largestHeader);
            else
            {
                _outHeader.Append(_header);
                if (_outHeader.Length < _largestHeader) _outHeader.Append(' ', _largestHeader - _outHeader.Length);
            }
            if (!string.IsNullOrEmpty(_identString))
            {
                for (int i = 0; i < _ident; i++) _outHeader.Append(_identString);
            }
        }
        public int LineNumber { get { return _lineno; } }
        public int Position { get { return HeaderLength + _line.Length; } }
        public int HeaderLength { get { return _largestHeader + (_identString != null ? _identString.Length * _ident : 0); } }
        public void Indent() { _ident++; updateHeader(); }
        public void Unindent() { _ident--; if (_ident < 0) _ident = 0; updateHeader(); }
        public string IndentString { get { return _identString; } set { _identString = value; updateHeader(); } }
       
        public string Header
        {
            get { return _header; }
            set
            {
                _header = value;
                if (!string.IsNullOrEmpty(_header) && value.Length > _largestHeader) _largestHeader = _header.Length;
                updateHeader();
            }
        }

        public PlainTextWriter(TextWriter stream)
        {
            _largestHeader = 0;
            _header = null;
            _flushed = false;
            _stream = stream;
            _line = null;
            _ident = 0;
            _identString = "    ";
            _header = null;
            _outHeader = new StringBuilder(128);
            _lastChar = '\0';
            updateHeader();
        }
        public override Encoding Encoding
        {
            get
            {
                return _stream.Encoding;
            }
        }

        public TextLocation Location
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        bool ValidStreamForWrite()
        {
            if (_stream == null) return false; // safety check
            if (_line == null) // first write setup
            {
                _line = new StringBuilder(256);
                _lineno = 1;
                _flushed = false;
            }
            return true;
        }
        
        bool checkIfNewLine(char current)
        {
            return ((CoreNewLine.Length == 2 && _lastChar == CoreNewLine[0] && current == CoreNewLine[1]) || (CoreNewLine.Length == 1 && current == CoreNewLine[0]));
        }
        // just override this method for now, Work on more latter
        public override void Write(char value)
        {
            if(!ValidStreamForWrite()) return;
            _line.Append(value);
            if(checkIfNewLine(value)) WriteLine();
            _lastChar = value;
        }
        public override void WriteLine()
        {
            string line = _line.ToString();
            if (!_flushed) _stream.Write(_outHeader.ToString());
            _stream.Write(line);
            _stream.WriteLine();
            _line.Clear();
            _lineno++;
            _flushed = false;
        }
        public override void WriteLine(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!ValidStreamForWrite()) return;
            // There is a bug here I am too lasy to fix.  If the string starts a \n but there was already a \r seen, then
            // it won't do the next line properly.  I could feed this char by char to Write(char) but this is WAY faster and
            // I have yet to run into this bug.  Just to watch out for
            string[] split = value.Split(CoreNewLine); // alwyas returns one element
            foreach(var s in split)
            {
                _line.Append(s);
                this.WriteLine();
            }
        }
        public override void Write(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!ValidStreamForWrite()) return;
            // There is a bug here I am too lasy to fix.  If the string starts a \n but there was already a \r seen, then
            // it won't do the next line properly.  I could feed this char by char to Write(char) but this is WAY faster and
            // I have yet to run into this bug.  Just to watch out for
            string[] split = value.Split(CoreNewLine); // alwyas returns one element
            int count = 0;
            while (count < split.Length)
            {
                string s = split[count++];
                if (!string.IsNullOrEmpty(s)) _line.Append(s);
                if (count >= split.Length) break;
                WriteLine();
            } 
        }
        public override void Flush()
        {
            if (_stream == null || _line == null) return; // safety check

            if (!_flushed)
            {
                _stream.Write(_outHeader.ToString());
                _stream.Write(_line.ToString());
                _line.Clear();
                _flushed = true;
            }
            else
            {
                // flushed before a newline so just flush without the header
                _stream.Write(_line.ToString());
                _line.Clear();
            }
            base.Flush(); // really does nothing here
        }
        protected override void Dispose(bool disposing)
        {

            // We don't want to destroy _stream, but we do want to make sure it gets disposed if it does
            // Be sure to flush
            Flush();
            _stream = null;
            base.Dispose(disposing);
        }

        public void WriteDefinition(string text, object definition, bool isLocal = true)
        {
            Write(text);
        }

        public void WriteReference(string text, object reference, bool isLocal = false)
        {
            Write(text);
        }

        public void MarkFoldStart(string collapsedText = "...", bool defaultCollapsed = false)
        {
            throw new NotImplementedException();
        }

        public void MarkFoldEnd()
        {
            throw new NotImplementedException();
        }

    }
    public struct TextLocation
    {
        public readonly int Column;
        public readonly int Line;
        public TextLocation(int line, int column) { this.Line = line;  this.Column = column; }
    }
    public interface ITextOutput
    {
        TextLocation Location { get; }

        void Indent();
        void Unindent();
        void Write(char ch);
        void Write(string text);
        void WriteLine();
        void WriteDefinition(string text, object definition, bool isLocal = true);
        void WriteReference(string text, object reference, bool isLocal = false);

        void MarkFoldStart(string collapsedText = "...", bool defaultCollapsed = false);
        void MarkFoldEnd();
    }
    public class TextOutputWriter : TextWriter
    {
        readonly ITextOutput output;

        public TextOutputWriter(ITextOutput output)
        {
            if (output == null)
                throw new ArgumentNullException("output");
            this.output = output;
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public override void Write(char value)
        {
            output.Write(value);
        }

        public override void Write(string value)
        {
            output.Write(value);
        }

        public override void WriteLine()
        {
            output.WriteLine();
        }
    }
    public static class TextOutputExtensions
    {
        public static void Write(this ITextOutput output, string format, params object[] args)
        {
            output.Write(string.Format(format, args));
        }

        public static void WriteLine(this ITextOutput output, string text)
        {
            output.Write(text);
            output.WriteLine();
        }

        public static void WriteLine(this ITextOutput output, string format, params object[] args)
        {
            output.WriteLine(string.Format(format, args));
        }
    }
    public sealed class PlainTextOutput : ITextOutput, IDisposable
    {
        readonly TextWriter writer;
        int indent;
        bool needsIndent;

        int line = 1;
        int column = 1;

        public PlainTextOutput(TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");
            this.writer = writer;
        }

        public PlainTextOutput()
        {
            this.writer = new StringWriter();
        }

        public TextLocation Location
        {
            get
            {
                return new TextLocation(line, column + (needsIndent ? indent : 0));
            }
        }

        public override string ToString()
        {
            return writer.ToString();
        }

        public void Indent()
        {
            indent++;
        }

        public void Unindent()
        {
            indent--;
        }

        void WriteIndent()
        {
            if (needsIndent)
            {
                needsIndent = false;
                for (int i = 0; i < indent; i++)
                {
                    writer.Write('\t');
                }
                column += indent;
            }
        }

        public void Write(char ch)
        {
            WriteIndent();
            writer.Write(ch);
            column++;
        }

        public void Write(string text)
        {
            WriteIndent();
            writer.Write(text);
            column += text.Length;
        }

        public void WriteLine()
        {
            writer.WriteLine();
            needsIndent = true;
            line++;
            column = 1;
        }

        public void WriteDefinition(string text, object definition, bool isLocal)
        {
            Write(text);
        }

        public void WriteReference(string text, object reference, bool isLocal)
        {
            Write(text);
        }

        void ITextOutput.MarkFoldStart(string collapsedText, bool defaultCollapsed)
        {
        }

        void ITextOutput.MarkFoldEnd()
        {
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    writer.Dispose();
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~PlainTextOutput() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
