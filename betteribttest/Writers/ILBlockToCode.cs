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
        protected SortedList<string, ILBlock> methods = new SortedList<string, ILBlock>();
        protected ILBlock method { get; private set; }
        protected GMContext context { get; private set; }
        protected string MethodName { get; private set; }
        protected string FileName { get; private set; }
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
        public BlockToCode(GMContext context)
        {
            this.context = context;
            this.method = null;
            this.MethodName = null;
            this.FileName = null;
            this.writer = new StringWriter();
            this.ownedWriter = true;
        }
        public BlockToCode(GMContext context, TextWriter tw, string filename = null)
        {
            this.context = context;
            this.method = null;
            this.MethodName = null;
            this.FileName = Path.GetFileName(filename);
            this.writer = tw;
            this.ownedWriter = false;
        }
        public BlockToCode(GMContext context, string filename = null)
        {
            this.context = context;
            this.method = null;
            this.MethodName = null;
            this.FileName = Path.GetFileName(filename);
            this.writer = new StreamWriter(filename);
            this.ownedWriter = true;
        }
        protected void Indent()
        {
            ident++;
            currentIdent = FindIdent(ident);
        }
        protected void UnIndent()
        {
            ident--;
            currentIdent = FindIdent(ident);
        }
        protected void WriteLine()
        {
            needIdent = true;
            writer.WriteLine();
        }
        protected void Write(char c)
        {
            if(needIdent)
            {
                writer.Write(currentIdent);
                needIdent = false;
            }
            writer.Write(c);
        }
        protected void Write(string s)
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
        protected void WriteLine(string s)
        {
            Write(s);
            WriteLine();
        }
        protected void Write(string msg, params object[] o)
        {
            Write(string.Format(msg, o));
        }
        protected void WriteLine(string msg, params object[] o)
        {
            WriteLine(string.Format(msg, o));
        }
      
        protected abstract void Write(ILCall bb);
        protected abstract void Write(ILVariable bb);
        protected abstract void Write(ILValue bb);
        protected abstract void Write(ILLabel bb);

        protected abstract void Write(ILBasicBlock bb);
        protected abstract void Write(ILCondition bb);
        protected abstract void Write(ILAssign bb);
        protected abstract void Write(ILExpression bb);
        protected abstract void Write(ILWithStatement bb);
        protected abstract void Write(ILWhileLoop bb);
        protected abstract void Write(ILElseIfChain bb);

        protected virtual void Write(ILBlock block)
        {
            WriteNodes(block.Body, true, true);
        }
        protected void WriteNode(ILNode n, bool newline = false)
        {
            Write((dynamic)n);
            if (newline) WriteLine();
        }
        protected void WriteNodes<T>(IEnumerable<T> nodes,bool newline, bool ident) where T:ILNode
        {
            if (ident) Indent();
            foreach (var n in nodes)
            {
                Write((dynamic)n);
                if (newline) WriteLine();
            }
            if (ident) UnIndent();
        }
        
        protected virtual void WriteHeader()
        {

        }
        protected virtual void WriteFooter()
        {

        }
        protected virtual void WriteMethodHeader()
        {

        }
        protected virtual void WriteMethodFooter()
        {

        }
        public virtual void AddMethod(string MethodName, ILBlock Method, object userData=null)
        {
            methods.Add(MethodName, Method);
        }
        public virtual void WriteMethod(string MethodName, ILBlock Method)
        {
            this.MethodName = MethodName;
            this.method = Method;
            WriteMethodHeader();
            WriteNodes(method.Body, true, true);
            WriteMethodFooter();
            this.MethodName = null;
            this.method = null;
        }
        public virtual void WriteAllMethods()
        {
            if(methods.Count > 0)
            {
                WriteHeader();
                foreach (var m in methods)
                    WriteMethod(m.Key, m.Value);
                WriteFooter();
                methods.Clear();
            }
        }
        public override string ToString()
        {
            if (writer is StringWriter)
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
                if (disposing && this.ownedWriter)
                {
                    writer.Flush();
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
