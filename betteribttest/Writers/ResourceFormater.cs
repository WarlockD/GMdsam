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
    public class ResourceFormater : IDisposable // I like using using
    {
        protected PlainTextWriter writer { get; private set; }
        bool disposed = false;
        public void Dispose()
        {
            if (!disposed)
            {
                writer.Dispose();
                disposed = true;
            }
            throw new NotImplementedException();
        }
        ~ResourceFormater() { Dispose(); }
        public ResourceFormater(TextWriter writer)
        {
            this.writer = writer as PlainTextWriter;
            if (this.writer == null) this.writer = new PlainTextWriter(writer);
        }
        public ResourceFormater()
        {
            this.writer = new PlainTextWriter();
        }
        public ResourceFormater(string filename)
        {
            this.writer = new PlainTextWriter(filename);
        }
        public virtual void Write<T>(IEnumerable<T> all) where T: File.GameMakerStructure
        {
            writer.WriteLine("{");
            foreach(var a in all)
            {
                Write((dynamic)a);
            }
            writer.WriteLine("}");
        }
        public virtual void Write(File.Room room)
        {
            SerializerHelper help = new SerializerHelper(room);
            help.DebugSave(writer);
        }
        public virtual void Write(File.GObject room)
        {
            SerializerHelper help = new SerializerHelper(room);
            help.DebugSave(writer);
        }
        public virtual void Write(File.Font font)
        {

        }
        public virtual void Write(File.Sprite font)
        {

        }
        public virtual void Write(File.AudioFile font)
        {

        }
        public virtual void Write(File.Background font)
        {

        }
        public virtual void Write(File.Code code)
        {

        }
    }
}
