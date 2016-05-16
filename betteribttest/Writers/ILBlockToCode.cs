using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker.Writers
{
    public abstract class BlockToCode : IDisposable
    {
        static Dictionary<int, string> identCache = new Dictionary<int, string>();
        static string FindIdent(int count)
        {
            string identString;
            if (!identCache.TryGetValue(count, out identString))
                identCache[count] = identString = new string(' ', count * 4);
            return identString;
        }
        protected ILBlock method { get; private set; }
        protected GMContext context { get; private set; }
        protected string MethodName { get; private set; }
        public string FileName { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }
        bool needIdent = false;
        string currentIdent;
        TextWriter writer;
        bool ownedWriter;
        int ident = 0;
        string toStringCache = null;
        public TextWriter Writer
        {
            get { return writer; }
            set
            {
                if (value == null) throw new ArgumentNullException("Writer");
                writer = value;
                this.ownedWriter = true;
            }
        }
        public abstract string LineComment { get;}

        public BlockToCode(GMContext context, TextWriter tw, string filename = null)
        {
            this.context = context;
            this.Ident = 0;
            this.method = null;
            this.MethodName = null;
            this.FileName = Path.GetFileName(filename);
            this.writer = tw;
            this.ownedWriter = false;
            this.Column = 0;
            this.Line = 1;
        }
        public BlockToCode(GMContext context, string filename = null)
        {
            this.context = context;
            this.Ident = 0;
            this.method = null;
            this.MethodName = null;
            this.Column = 0;
            this.Line = 1;
            if(filename == null)
            {
                this.FileName = null;
                this.writer = new StringWriter();
            } else
            {
                this.FileName = Path.GetFileName(filename);
                this.writer = new StreamWriter(filename);
               
            }
            this.ownedWriter = true;

        }
        public int Ident
        {
            get { return ident; }
            set
            {
                ident = value;
                currentIdent = FindIdent(ident);
            }
        }
        public void WriteLine()
        {
            needIdent = true;
            Column = 0;
            Line++;
            writer.WriteLine();
        }
        public void Write(char c)
        {
            if(needIdent)
            {
                writer.Write(currentIdent);
                Column += currentIdent.Length;
                needIdent = false;
            }
            Column++;
            writer.Write(c);
        }
        public void Write(string s)
        {
            for(int i=0;i < s.Length; i++)
            {
                char c = s[i];
                if(c == '\r' || c == '\n')
                {
                    char p = s.ElementAtOrDefault(i + 1);
                    if (p != c && (c == '\r' || c == '\n')) i++;
                    WriteLine();
                    continue;
                }
                Write(c);
            }
        }
        public void WriteLine(string s)
        {
            Write(s);
            WriteLine();
        }
        public void Write(string msg, params object[] o)
        {
            Write(string.Format(msg, o));
        }
        public void WriteLine(string msg, params object[] o)
        {
            WriteLine(string.Format(msg, o));
        }

        public abstract void Write(ILCall bb);
        public abstract void Write(ILVariable bb);
        public abstract void Write(ILValue bb);
        public abstract void Write(ILLabel bb);

        public abstract void Write(ILBasicBlock bb);
        public abstract void Write(ILCondition bb);
        public abstract void Write(ILAssign bb);
        public abstract void Write(ILExpression bb);
        public abstract void Write(ILWithStatement bb);
        public abstract void Write(ILWhileLoop bb);
        public abstract void Write(ILElseIfChain bb);

        public virtual void Write(ILBlock block)
        {
            WriteNodes(block.Body, true, true);
        }
        public void WriteNode(ILNode n, bool newline = false)
        {
            Write((dynamic)n);
            if (newline) WriteLine();
        }
        public void WriteNodes<T>(IEnumerable<T> nodes,bool newline, bool ident) where T:ILNode
        {
            if (ident) Ident++;
            foreach (var n in nodes)
            {
                Write((dynamic)n);
                if (newline) WriteLine();
            }
            if (ident) Ident--;
        }

        public virtual void WriteMethod(string MethodName, ILBlock Method, bool ident=true)
        {
            this.MethodName = MethodName;
            this.method = Method;
            WriteNodes(method.Body, true, ident);
            this.MethodName = null;
            this.method = null;
        }

        public override string ToString()
        {
            if (this.ownedWriter && writer is StringWriter)
                return (writer as StringWriter).ToString();
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{ ");
                sb.AppendFormat("Type={0} ", this.GetType().ToString());
                if (FileName != null) sb.AppendFormat("Filename=\"{0}\" ", FileName);
                if (MethodName != null) sb.AppendFormat("MethodName=\"{0}\" ", MethodName);
                sb.Append("}");
                return sb.ToString();
            }
        }
        ~BlockToCode()
        {
            Dispose(false);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                writer.Flush(); // flush either way
                if (disposing && this.ownedWriter)
                {
                    writer.Dispose();
                    writer = null;
                    this.ownedWriter = false;
                }
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~BlockToCode() {
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
