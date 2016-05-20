using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameMaker.Writers
{
    public interface IScriptWriter
    {
        void WriteScript(File.Script script, BlockToCode output);
    }

    public interface IObjectWriter
    {
        void WriteObject(File.GObject obj, BlockToCode output);
    }
    public interface ICodeFormater
    {
        ICodeFormater Clone();
        void SetStream(BlockToCode s);
        void Write(ILFakeSwitch f);
        void Write(ILVariable v);
        void Write(ILAssign a);
        void Write(ILCall bb);
        void Write(ILValue bb);
        void Write(ILLabel bb);
        void Write(ILBasicBlock bb);
        void Write(ILCondition bb);
        void Write(ILExpression bb);
        void Write(ILWithStatement bb);
        void Write(ILWhileLoop bb);
        void Write(ILElseIfChain bb);
      //  void Write(ILBlock block);
        // What is used to start a line comment
        string LineComment { get; }
        string NodeEnding { get; }
        string Extension { get; }
    }
    public interface INodeMutater
    {
        INodeMutater Clone();
        void SetStream(BlockToCode s);
        ILCall MutateCall(ILBlock block, int pos, ILCall call);
        ILAssign MutateAssign(ILBlock block, int pos, ILAssign assign);
    }
    public class EmptyNodeMutater : INodeMutater // does nothing
    {
        public ILAssign MutateAssign(ILBlock block, int pos, ILAssign assign)
        {
            return assign;
        }

        public ILCall MutateCall(ILBlock block, int pos, ILCall call)
        {
            return call;
        }

        public void SetStream(BlockToCode s)
        {
        
        }
        public INodeMutater Clone()
        {
            return new EmptyNodeMutater();
        }
    }

    
    public class NodeInfo<T> where T : ILNode
    {
        public readonly string Name;
        public readonly T Node;
        public readonly int Line;
        public readonly int Col;
        public NodeInfo(T node, int Line, int Col, string name)
        {
            this.Node = node;
            this.Line = Line;
            this.Col = Col;
            this.Name = name;
        }
        public override string ToString()
        {
            return Name + "(" + Line + "," + Col + ")";
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            NodeInfo<T> test = obj as NodeInfo<T>;
            return test != null && test.Name == Name;
        }
    }
    public class BlockToCode : IDisposable
    {
        static Regex regex_newline = new Regex("(\r\n|\r|\n)", RegexOptions.Compiled);
        static Dictionary<int, string> identCache = new Dictionary<int, string>();
        static string FindIdent(int count)
        {
            string identString;
            if (!identCache.TryGetValue(count, out identString))
                identCache[count] = identString = new string(' ', count * 4);
            return identString;
        }
        public void Flush()
        {
            writer.Flush();
        }

        static BlockToCode()
        {
        }
        public static string DebugNodeToString(ILNode node) 
        {
            string ret;
            using(var w = new BlockToCode(new DebugFormater()))
            {
                w.WriteNode(node);
                ret = w.ToString();
            }
            return ret;
        }
        public static string NiceNodeToString(ILNode node)
        {
            string ret;
            using (var w = new BlockToCode(new DebugFormater()))
            {
                w.WriteNode(node);
                ret = w.ToString();
            }
            return ret;
        }
      
        ILBlock currentBlock = null;
        int currentIndex = 0;
        ICodeFormater formater = null;
        INodeMutater mutater = null;
        public INodeMutater Mutater
        {
            get { return mutater; }
            set
            {
                if (mutater != null) mutater.SetStream(null);
                mutater = value;
                if (mutater != null) mutater.SetStream(this);
 
            }
        }
        public ICodeFormater Formater
        {
            get { return formater; }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                formater.SetStream(null);
                formater = value;
                formater.SetStream(this);
            }
        }
        public void Clear()
        {
            StringWriter sw = writer as StringWriter;
            if (sw == null) throw new ArgumentException("Cannot clear a non  string writer", "writer");
            sw.GetStringBuilder().Clear();
            this.Indent = 0;
            this.method = null;
            this.MethodName = null;
            this.Column = 0;
            this.Line = 1;
        }
        public ILBlock method { get; private set; }
        string method_name;

        public string MethodName {
            get { return method_name; }
            private set
            {
              //  if (mutater != null) mutater.SetMethodName(method_name);
                method_name = value;
            }
        }
        public int Line { get; private set; }
        public int Column { get; private set; }
        bool needIdent = false;
        string currentIdent;
        TextWriter writer;
        bool ownedWriter;
        int ident = 0;
        void Init(ICodeFormater formater)
        {
            if (formater == null) throw new ArgumentNullException("Formater");
            this.formater = formater;
            formater.SetStream(this);
            this.Indent = 0;
            this.method = null;
            this.MethodName = null;
            this.Column = 0;
            this.Line = 1;
        }

        public BlockToCode(ICodeFormater formater, TextWriter tw)
        {
            if (tw == null) throw new ArgumentNullException("tw");
            Init(formater);
            this.writer = tw;
            this.ownedWriter = false;
        }
        public BlockToCode(ICodeFormater formater)
        {
            Init(formater);
            this.writer = new StringWriter();
            this.ownedWriter = true;
        }
        public BlockToCode( ICodeFormater formater, string filename) {
            if (filename == null) throw new ArgumentNullException("filename");
            Init(formater);
            filename = Path.ChangeExtension(filename, formater.Extension);
            this.writer = new StreamWriter(filename);
            this.ownedWriter = true;
        }

        public int Indent
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
        void _WriteNode<T>(T n) where T: ILNode
        {

                formater.Write((dynamic) n);


        }
        public void WriteNodesComma<T>(IEnumerable<T> nodes, bool need_comma = false) where T : ILNode
        {
            foreach (var n in nodes)
            {
                if (need_comma) Write(',');
                _WriteNode(n);
                 need_comma = true;
            }
        }
        public void Write(char c)
        {
            if (needIdent)
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
        
      
        List<NodeInfo<ILVariable>> varinfos = new List<NodeInfo<ILVariable>>();
        public IReadOnlyList<NodeInfo<ILVariable>> VariablesUsed {  get { return varinfos; } }
        List<NodeInfo<ILAssign>> assign_infos = new List<NodeInfo<ILAssign>>();
        public IReadOnlyList<NodeInfo<ILAssign>> AssignsUsed { get { return assign_infos; } }
        List<NodeInfo<ILCall>> call_infos = new List<NodeInfo<ILCall>>();
        public IReadOnlyList<NodeInfo<ILCall>> CallsUsed { get { return call_infos; } }


        public void Write(ILVariable v)
        {
           
            if (formater == null) throw new NullReferenceException("Formatter is null");
            varinfos.Add(new NodeInfo<ILVariable>(v,Line, Column,v.FullName));
            formater.Write(v);
        }
        public void Write(ILAssign a)
        {
            if (mutater != null) a = mutater.MutateAssign(currentBlock, currentIndex, a);
            if (formater == null) throw new NullReferenceException("Formatter is null");
            assign_infos.Add(new NodeInfo<ILAssign>(a, Line, Column, a.Variable.FullName));
            formater.Write(a);
        }
        public void Write(ILCall c)
        {
            if (mutater != null) c = mutater.MutateCall(currentBlock, currentIndex, c);
            if (formater == null) throw new NullReferenceException("Formatter is null");
            call_infos.Add(new NodeInfo<ILCall>(c, Line, Column, c.Name));
            formater.Write(c);
        }
        public void Write<T>(T n) where T: ILNode
        {
            _WriteNode(n);
        }
        public void Write(ILBlock block)
        { // this is why we feed eveythign though this thing
            var backup = currentBlock;
            currentBlock = block;
            Indent++;
            for(int i=0;i < block.Body.Count; i++)
            {
                currentIndex = i;
                WriteNode(block.Body[i],true);//    WriteNodes(block.Body, true, true);
            }
            Indent--;
            currentBlock = backup;
        }
        public void WriteNode(ILNode n, bool newline = false)
        {
            _WriteNode(n);
            if (newline)
            {
                if (formater.NodeEnding != null) writer.Write(formater.NodeEnding);
                WriteLine();
            }
        }
        public void WriteNodes<T>(IEnumerable<T> nodes,bool newline, bool ident) where T:ILNode
        {
            if (ident) Indent++;
            foreach (var n in nodes)
            {
                WriteNode(n, newline);
            }
            if (ident) Indent--;
        }

        public virtual void WriteMethod(string MethodName, ILBlock Method, bool ident=true)
        {
            this.MethodName = MethodName;
            this.method = Method;
            WriteNodes(method.Body, true, ident);
            this.MethodName = null;
            this.method = null;
        }
        // this is hevey so watch it
      
        public override string ToString()
        {
            if (this.ownedWriter && writer is StringWriter)
                return (writer as StringWriter).ToString();
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{ ");
                sb.AppendFormat("Type={0} ", this.GetType().ToString());
                if (MethodName != null) sb.AppendFormat("MethodName=\"{0}\" ", MethodName);
                sb.Append("}");
                return sb.ToString();
            }
        }
        public string LineComment { get { return formater.LineComment; } }
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
