using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GameMaker.Ast;

namespace GameMaker.Writers
{
    public class ResourceFormater : PlainTextWriter
    {
        public ResourceFormater(TextWriter writer) : base(writer) { }
        public ResourceFormater() : base() { }
        public ResourceFormater(string filename) : base(filename) { }
        public virtual void WriteAll<T>(IEnumerable<T> all) where T: File.GameMakerStructure
        {
            WriteLine("{");
            foreach(var a in all)
            {
                Write((dynamic)a);
            }
            WriteLine("}");
        }
        public virtual void Write(File.Room room)
        {
            SerializerHelper help = new SerializerHelper(room);
            SimpleWrite(help);
        }
        bool HelpLine(SerializerHelper.SerizlierObject o) {
            if (o.isSimple)
            {
                string line = o.ToString();
                Write(line);
            } else
            {
                if (o.isArray) // array of objects
                {
                    Write(o.Name);
                    Write(" = ");
                    WriteLine("{");
                    Indent++;
                    this.WriteCommaDelimited(o.GetComplexArray(), ao =>
                    {
                        Write(ao.ToString());
                        return true;
                    });
                    Indent--;
                    Write("}");
                } else // just an object
                {
                    string line = o.ToString();
                    Write(line);
                }
            }
            return true;
        }
        protected void SimpleWrite(SerializerHelper help)
        {
            WriteLine("{");
            Indent++;
            this.WriteCommaDelimited(help, HelpLine);
            Indent--;
            WriteLine("}");
        }
        public virtual void Write(File.GObject o)
        {
            SerializerHelper help = new SerializerHelper(o);
            SimpleWrite(help);
        }
        public virtual void Write(File.Font font)
        {
            SerializerHelper help = new SerializerHelper(font);
            SimpleWrite(help);
        }
        public virtual void Write(File.Sprite sprite)
        {
            SerializerHelper help = new SerializerHelper(sprite);
            SimpleWrite(help);
        }
        public virtual void Write(File.AudioFile o)
        {
            SerializerHelper help = new SerializerHelper(o);
            SimpleWrite(help);
        }
        public virtual void Write(File.Background b)
        {
            SerializerHelper help = new SerializerHelper(b);
            SimpleWrite(help);
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
