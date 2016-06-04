using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GameMaker.Ast;
using static GameMaker.File;
using System.Runtime.Serialization.Json;
using System.Diagnostics;

namespace GameMaker.Writers
{
    public class ResourceFormater : PlainTextWriter
    {
        #region Pritty JSON Print Parser
        static Regex string_match = new Regex(@"""[^ ""\\] * (?:\\.[^ ""\\] *)*""", RegexOptions.Compiled);
        static Regex number_match = new Regex(@"\d+", RegexOptions.Compiled);
        static Regex float_match = new Regex(@"(?:^|(?<=\s))[0-9]*\.?[0-9](?=\s|$)");

        internal abstract class TokenStream
        {
            public abstract void Restart();
            protected abstract char _Next();
            public char Next()
            {
                char c;
                while (char.IsWhiteSpace(c = _Next())) ;
                if (c == 0) return Current = c;
                Prev = Current;
                return Current = char.ToLower(c);
            }
            public char Prev { get; private set; }
            public char Current { get; private set; }
        }
        class FromStream : TokenStream
        {
            StreamReader _reader;
            public FromStream(Stream stream) { this._reader = new StreamReader(stream); Restart(); }
            public override void Restart() { _reader.BaseStream.Position = 0; }
            protected override char _Next()
            {
                int c = _reader.Read();
                return c == -1 ? default(char) : (char) c;
            }
        }
        class FromString : TokenStream
        {
            string _string;
            int _pos;
            public FromString(string str) { this._string = str; Restart(); }
            public override void Restart() { this._pos = 0; }
            protected override char _Next()
            {
                return _pos < _string.Length ? _string[_pos++] : default(char);
            }
        }

        // cause I am lazy


        // Really simple state machine.  I guess I could work on it to reduce the need for 
        // last and from, but I don't want thie recursive decent parser to need more than 4 
        // functions:P
        char ParseValue(TokenStream stream, int level, char from = default(char))
        {
            char ch = default(char);
            char last = default(char);
            do
            {
                last = ch;
                ch = stream.Next();
                switch (ch)
                {
                    case '"':
                        Write(ch);
                        while (ch != 0)
                        {
                            ch = stream.Next();
                            if (stream.Prev != '\\' && ch == '"') break;
                            Write(ch);
                        }
                        Write(ch);
                        break;
                    case '{':
                        Indent++;
                        if (from == '[')// object array
                        {
                            if (last != ',') // first start
                            {

                                WriteLine();
                                Write(ch);
                                Write(' ');
                                ch = ParseValue(stream, level++, '('); // object inline
                            }
                            else
                            {
                                Write(ch);
                                Write(' ');
                                ch = ParseValue(stream, level++, '('); // object inline
                            }
                        }
                        else if (last == ',')
                        {
                            Write(ch);
                            WriteLine();// usally first level
                            ch = ParseValue(stream, level++, '{');
                        }
                        else WriteLine();// usally first level
                        Indent--;
                        Write(' '); // final space
                        Write(ch); // write the ending bracket
                        break;
                    case '[':

                        Write(ch);

                        ch = ParseValue(stream, level++, '[');
                        Write(ch);
                        break;
                    case '}': return '}';
                    case ']':
                        //writer.Write(ch);
                        if (!char.IsNumber(last)) WriteLine();
                        return ']';
                    case ',':
                        Write(ch);
                        if (from == '[')
                        {
                            if (!char.IsNumber(last)) WriteLine();
                            else if (last == '}') WriteLine();
                            else Write(' ');
                        }
                        else if (from != '(') WriteLine(); // its an object but we want it inline
                        else Write('\t');
                        break;
                    case ':':
                        Write(ch);
                        Write(' ');
                        break;
                    default:
                        Write(ch);
                        break;
                }

            } while (ch != 0);
            return default(char);
        }

        // I just gave up here and set up the first level
        void WriteStart(TokenStream stream, int level = 0) // test if we are starting on an object or on an array
        {
            char ch = stream.Next();
            if (ch == '{')
            {
                WriteLine('{');
                Indent++;

                ParseValue(stream, level);
                WriteLine();
                Indent--;
                Write('}');
            }
            else if (ch == '[')
            {
                WriteLine('[');
                while (ch != ']')
                {
                    Indent++;

                    ParseValue(stream, 0);
                    WriteLine();
                    Indent--;
                    ch = stream.Next();
                    if (ch == ',')
                    {
                        Write(',');
                        ch = stream.Next();
                    }
                }
                Indent--;
                WriteLine(']');
            }
        }
        #endregion
        bool make_pretty;
        public ResourceFormater(TextWriter writer, bool make_pretty = true) : base(writer) { this.make_pretty = make_pretty; }
        public ResourceFormater(bool make_pretty = true) : base() { this.make_pretty = make_pretty; }
        public ResourceFormater(string filename, bool make_pretty = true) : base(Path.ChangeExtension(filename, "json")) { this.make_pretty = make_pretty; }

        string DefaultJSONSerilizeToString<T>(T o) where T : GameMakerStructure
        {
            using (MemoryStream ssw = new MemoryStream())
            {
                DataContractJsonSerializerSettings set = new DataContractJsonSerializerSettings();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));

                ser.WriteObject(ssw, o);
                ssw.Position = 0;
                StreamReader sr = new StreamReader(ssw);
                return sr.ReadToEnd();
            }
        }
        static Regex match_value = new Regex(@"\""[^\,]\,", RegexOptions.Compiled);

        public static string FormatLine(string line, int field_size)
        {
            StringBuilder sb = new StringBuilder();
            int start = -1;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                sb.Append(c);
                if (start >= 0) start++;
                if (c == '"' && start == -1) start = 0;
                else if (c == ',' && start >0)
                {
                    int extra = field_size-start;
                    if (extra > 0) sb.Append(' ', extra);
                    start = -1;
                }
            }
            return sb.ToString();
        }
        public virtual void WriteAll<T>(IEnumerable<T> all) where T : File.GameMakerStructure
        {
            WriteLine('[');
            this.Indent++;
            bool need_comma = false;
            foreach (var a in all)
            {
                if (need_comma) WriteLine(',');
                else need_comma = true;
                using (MemoryStream ssw = new MemoryStream())
                {
                    DataContractJsonSerializerSettings set = new DataContractJsonSerializerSettings();
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
                    ser.WriteObject(ssw, a);
                    ssw.Position = 0;

                    StreamReader sw = new StreamReader(ssw);
                    string line = sw.ReadToEnd();
                    line = FormatLine(line, 20);
                   // line = line.Replace("{", "{ ").Replace(",", ",\t").Replace(",\t\"audio_type\"", ",\t\t\"audio_type\"").Replace(":", ": ");
                    Write(line);
                }
            }
            WriteLine();
            this.Indent--;
            WriteLine(']');
            Flush(); // flush it
        }
   
        public void WriteAny<T>(T o) where T : File.GameMakerStructure
        {
            using (MemoryStream ssw = new MemoryStream())
            {
                DataContractJsonSerializerSettings set = new DataContractJsonSerializerSettings();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
                ser.WriteObject(ssw, o);
                if (make_pretty)
                {
                    var token_stream = new FromStream(ssw);
                    WriteStart(token_stream);
                } else
                {
                    StreamReader sw = new StreamReader(ssw);
                    Write(sw.ReadToEnd());
                }
                Flush(); // flush it
            }
        }
        public virtual void Write(File.Room room)
        {
            if (room.code_offset > 0 && room.Room_Code == null) // fill in room init
            {
                room.Room_Code = AllWriter.QuickCodeToLine(File.Codes[room.code_offset]);
            }
            foreach (var oi in room.Objects) // fill in instance init
            {
                if (oi.Code_Offset > 0 && oi.Room_Code == null)
                {
                    oi.Room_Code = AllWriter.QuickCodeToLine(File.Codes[oi.Code_Offset]);
                }
                if(oi.Object_Index > -1 && oi.Object_Name == null)
                {
                    oi.Object_Name = File.Objects[oi.Object_Index].Name;
                }
                
            }
            WriteAny(room);
        }
  
        public virtual void Write(File.GObject o)
        {
            WriteAny(o);
        }
        public virtual void Write(File.Font font)
        {
            WriteAny(font);
        }
        public virtual void Write(File.Sprite sprite)
        {
            WriteAny(sprite);
        }
        public virtual void Write(File.AudioFile o)
        {
            WriteAny(o);
        }

        public virtual void Write(File.Background b)
        {
            WriteAny(b);
        }
        public virtual void Write(File.Code code)
        {
            using (var output = new BlockToCode(new Context.ErrorContext(code.Name), this))
            {
                new GameMaker.Writer(output).WriteCode(code);
                Write(output.ToString());
            }
        }
    }
}
