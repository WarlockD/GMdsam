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
    public class PlainTextWriter : TextWriter
    {
        TextWriter _stream;
        bool _flushed;
        StringBuilder _line;
        string _header;
        StringBuilder _outHeader;
        int _largestHeader;
        char _lastChar;
        byte[] _byteBuffer;
        int _lineno;
        int _position;
        int _ident;
        int _identWidth;
        char _identChar;
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
            _outHeader.Append(_identChar, _identChar * _ident);
        }
        public int LineNumber { get { return _lineno; } }
        public int Position { get { return HeaderLength + _line.Length; } }
        public int HeaderLength { get { return _largestHeader + _identChar * _ident; } }
        public int Indent { get { return _ident; } set { _ident = value; updateHeader(); } }
        public int IdentWidth { get { return _identWidth; } set { _identWidth = value; updateHeader(); } }
        public char IdentChar { get { return _identChar; } set { _identChar = value; updateHeader(); } }
       
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
            _header = null;
            _flushed = false;
            _stream = stream;
            _line = null;
            Indent = 0;
            IdentWidth = 4;// 4 charaters
            IdentChar = ' '; // spaces.  1, '\t' is an option if you like tabs
            Header = null;
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
        // just override this method for now, Work on more latter
        public override void Write(char value)
        {
            if (_stream == null) return; // safety check
            if (_line == null) // first write setup
            {
                _line = new StringBuilder(256);
                _lineno = 1;
                _flushed = false;
            }
            _line.Append(value);
            if ((CoreNewLine.Length == 2 && _lastChar == CoreNewLine[0] && value == CoreNewLine[1]) || (CoreNewLine.Length == 1 && value == CoreNewLine[0]))
            { // new line
                string line = _line.ToString();
                if (!_flushed) _stream.Write(_outHeader.ToString());
                _stream.Write(line);
                _line.Clear();
                _lineno++;
                _flushed = false;
            }
            _lastChar = value;
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
        ~PlainTextWriter()
        {
            Flush(); // make sure it flushes though I thought Dispose will do this humm
        }
    }
}
