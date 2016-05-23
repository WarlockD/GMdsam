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
    public interface ICodeFormater
    {
        ICodeFormater Clone();
        void SetStream(BlockToCode s);
        void Write(ILSwitch f);
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
        ILCall MutateCall(ILCall call);
        ILAssign MutateAssign(ILAssign assign);
        ILVariable MutateVar(ILVariable v);
    }
    public class EmptyNodeMutater : INodeMutater // does nothing
    {
        public ILAssign MutateAssign(ILAssign assign)
        {
            return assign;
        }

        public ILCall MutateCall(ILCall call)
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

        public ILVariable MutateVar(ILVariable v)
        {
            return v;
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
        ICodeFormater formater = null;
        INodeMutater mutater = null;
        StringBuilder buffer = null;
        StringBuilder line = null;
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
            if (line == null) line = new StringBuilder(80);
            else line.Clear();
            if (buffer == null) buffer = new StringBuilder(max_seen_buffer);
            else
            {
                if (this.buffer.Length > max_seen_buffer) max_seen_buffer = this.buffer.Length * 2;
                buffer.Clear();
            }
            Line = 1;
            Indent = 0;
        }
        public int Line { get; private set; }
        public int Column { get { return currentIdent.Length + line.Length; } }
        string currentIdent;
        int ident = 0;
        static int max_seen_buffer = 512;
      
        void Init(ICodeFormater formater)
        {
            if (formater == null) throw new ArgumentNullException("Formater");
            this.formater = formater;
            formater.SetStream(this);
            Clear();
        }

        public BlockToCode(ICodeFormater formater)
        {
            Init(formater);
        }

        public string CreateFileName(string filename)
        {
            return Path.ChangeExtension(filename, formater.Extension);
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
  
        bool supressWriteLine = false;
        public void WriteLine()
        {
            if (supressWriteLine)
            {
                line.Append("; ");
            } else
            {
                buffer.Append(currentIdent);
                buffer.AppendLine(line.ToString());
                Line++;
                line.Clear();
            }
        }
        public void Write(char c)
        {
            line.Append(c);
        }
        public void Write(string s)
        {
            line.Append(s);
        }
        public void Write(string msg, object o)
        {
            line.AppendFormat(msg, o);
        }
        public void Write(string msg, object o, object o1)
        {
            line.AppendFormat(msg, o, o1);
        }
        public void Write(string msg, object o, object o1, object o2)
        {
            line.AppendFormat(msg, o, o1, o2);
        }
        public void Write(string msg, params object[] o)
        {
            line.AppendFormat(msg, o);
        }
        public void WriteLine(string msg, object o)
        {
            line.AppendFormat(msg, o);
            WriteLine();
        }
        public void WriteLine(string msg, object o, object o1)
        {
            line.AppendFormat(msg, o, o1);
            WriteLine();
        }
        public void WriteLine(string msg, object o, object o1, object o2)
        {
            line.AppendFormat(msg, o, o1, o2);
            WriteLine();
        }
        public void WriteLine(string msg, params object[] o)
        {
            line.AppendFormat(msg, o);
            WriteLine();
        }
        public void WriteLine(string msg)
        {
            line.Append(msg);
            WriteLine();
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
            if (mutater != null) v = mutater.MutateVar(v);
            varinfos.Add(new NodeInfo<ILVariable>(v,Line, Column,v.FullName));
            formater.Write(v);
        }
        public void Write(ILAssign a)
        {
            if (mutater != null) a = mutater.MutateAssign(a);
            if (formater == null) throw new NullReferenceException("Formatter is null");
            assign_infos.Add(new NodeInfo<ILAssign>(a, Line, Column, a.Variable.FullName));
            formater.Write(a);
        }
        public void Write(ILCall c)
        {
            if (mutater != null) c = mutater.MutateCall(c);
            if (formater == null) throw new NullReferenceException("Formatter is null");
            call_infos.Add(new NodeInfo<ILCall>(c, Line, Column, c.Name));
            formater.Write(c);
        }
        public void Write<T>(T n) where T: ILNode
        {
            _WriteNode(n);
        }
        public string NodeToString<T>(T n) where T : ILNode
        {
            StringBuilder sb = new StringBuilder();
            supressWriteLine = true;
            var backup = line;
            line = sb;
            _WriteNode(n);
            line = backup;
            supressWriteLine = false;
            return sb.ToString();
        }
  
        public void Write(ILBlock block)
        { // this is why we feed eveythign though this thing
            Write(block, false, true);
        }
        // quick way to write a block
        public void WriteFile(ILBlock block, string filename)
        {
            Write(block);
            WriteAsyncToFile(filename);
            Clear();
        }
        public void Write(ILBlock block, bool skip_last_newline, bool ident)
        {
            if(ident) Indent++;
            for (int i = 0; i < block.Body.Count; i++)
            {
                if(skip_last_newline && (block.Body.Count -1) == i)
                    WriteNode(block.Body[i], false);//    WriteNodes(block.Body, true, true);
                else
                    WriteNode(block.Body[i], true);//    WriteNodes(block.Body, true, true);
            }
            if (ident) Indent--;
        }
        public void Write(ILBlock block, bool skip_last_newline)
        {
            Write(block, skip_last_newline, true);
        }
        public void WriteNode(ILNode n, bool newline = false)
        {
            _WriteNode(n);
            if (newline)
            {
                if (formater.NodeEnding != null) Write(formater.NodeEnding);
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
        // this is hevey so watch it
      
        public override string ToString()
        {
            return buffer.ToString();
        }
        public void WriteAsyncToFile(string filePath)
        {
            Task.Factory.StartNew(() =>
            {
                if (string.IsNullOrWhiteSpace(Path.GetExtension(filePath))) 
                    filePath = Path.ChangeExtension(filePath, formater.Extension);
                string data = buffer.ToString();
            using (StreamWriter sw = new StreamWriter(filePath))
                sw.Write(data);
            /*
             * 
                    byte[] encodedText = Encoding.Unicode.GetBytes(this.buffer.ToString());
                using (FileStream sourceStream = new FileStream(filePath,
               FileMode.Append, FileAccess.Write, FileShare.None,
               bufferSize: 4096, useAsync: true))
                {
                    sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
                }
                */
            });
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
                if (disposing)
                {
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
