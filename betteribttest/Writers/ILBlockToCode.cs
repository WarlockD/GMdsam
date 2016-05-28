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
    public abstract class ICodeFormater
    {
        protected BlockToCode writer;
        public void SetStream(BlockToCode s) { writer = s; }
        protected abstract int Precedence(GMCode c);
        public abstract string GMCodeToString(GMCode c);
        protected bool CheckParm(ILExpression expr, int index)
        {
            int ours = Precedence(expr.Code);
            ILExpression e = expr.Arguments.ElementAtOrDefault(index);
            if (e == null) return false;
            int theirs = Precedence(e.Code);
            if (theirs == 8) return false; // its a constant or something dosn't need a parm
            if (theirs < ours) return true;
            else return false;
        }
        protected void WriteParm(ILExpression expr, int index)
        {
            bool needParm = CheckParm(expr, index);
            if (needParm) writer.Write('(');
            writer.Write(expr.Arguments[index]);
            if (needParm) writer.Write(')');
        }
        public abstract void Write(ILSwitch f);
        public abstract void Write(ILFakeSwitch f);
        public abstract void Write(ILVariable v);
        public abstract void Write(ILCall bb);
        public abstract void Write(ILValue bb);
        public abstract void Write(ILLabel bb);
        public abstract void Write(ILBasicBlock bb);
        public abstract void Write(ILCondition bb);
        public abstract void Write(ILExpression bb);
        public abstract void Write(ILWithStatement bb);
        public abstract void Write(ILWhileLoop bb);
        public abstract void Write(ILElseIfChain bb);
        //  void Write(ILBlock block);
        // What is used to start a line comment
        public abstract string LineComment { get; }
        public abstract string BlockCommentStart { get; }
        public abstract string BlockCommentEnd { get; }
        public abstract string NodeEnding { get; }
        public abstract string Extension { get; }
    }
    public interface INodeMutater
    {
        INodeMutater Clone();
        void SetStream(BlockToCode s);
        ILCall MutateCall(ILCall call);
        ILVariable MutateVar(ILVariable v);
    }
    public class EmptyNodeMutater : INodeMutater // does nothing
    {
  

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
    public class BlockToCode : IDisposable, IMessages
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

        ICodeFormater formater = null;
        INodeMutater mutater = null;
        StringBuilder buffer = null;
        StringBuilder line = null;
        IMessages error = null;
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
            if (buffer == null) buffer = new StringBuilder(4096);
            else buffer.Clear();
            Line = 1;
            Indent = 0;
        }
        public int Line { get; private set; }
        public int Column { get { return  line.Length; } }
        public int ColumnWithIdent { get { return currentIdent.Length + line.Length; } }
        string currentIdent;
        int ident = 0;      
        void Init(ICodeFormater formater, IMessages error)
        {
            if (formater == null) throw new ArgumentNullException("Formater");
            if (error == null) throw new ArgumentNullException("error");
            this.error = error;
            this.formater = formater;
            formater.SetStream(this);
            Clear();
        }
        #region IMessages Interface
        public void Error(string msg) { error.Error(msg); }
        public void Error(string msg, ILNode node) { error.Error(msg, node); }
        public void Warning(string msg) { error.Warning(msg); }
        public void Warning(string msg, ILNode node) { error.Warning(msg, node); }
        public void Info(string msg) { error.Info(msg); }
        public void Info(string msg, ILNode node) { error.Info(msg, node); }
        public string Name {  get { return error.Name; } }
        public void FatalError(string msg) { error.FatalError(msg); }
        public void FatalError(string msg, ILNode node) { error.FatalError(msg,node); }
        #endregion

        public BlockToCode(ICodeFormater formater, IMessages error)
        {
            Init(formater,error);
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


        List<NodeInfo<ILCall>> call_infos = new List<NodeInfo<ILCall>>();
        public IReadOnlyList<NodeInfo<ILCall>> CallsUsed { get { return call_infos; } }
        public void StartBlock(ILBlock block)
        {
            block.ClearAndSetAllParents();
            Write(block);
        }

        public void Write(ILVariable v)
        {
            if (formater == null) throw new NullReferenceException("Formatter is null");
            if (mutater != null) v = mutater.MutateVar(v);
            varinfos.Add(new NodeInfo<ILVariable>(v,Line, Column,v.FullName));
            formater.Write(v);
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
            block.ClearAndSetAllParents();
            Write(block);
            using (StreamWriter sw = new StreamWriter(filename)) sw.Write(buffer.ToString());
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
        public void WriteNode(ILNode n, bool newline = false, bool supressStatmentEnding=false)
        {
            _WriteNode(n);
            if (newline)
            {
                if (!supressStatmentEnding && formater.NodeEnding != null) line.Append(formater.NodeEnding);
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
        public void WriteAsyncToFile(ILBlock block, string filePath)
        {
            Clear();
            Write(block);
            WriteAsyncToFile(filePath);
        }
        public void WriteAsyncToFile(string filePath)
        {
            string data = buffer.ToString();
            Task.Factory.StartNew(() =>
            {
                if (string.IsNullOrWhiteSpace(Path.GetExtension(filePath))) 
                    filePath = Path.ChangeExtension(filePath, formater.Extension);
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
            Clear();
        }
        public string LineComment { get { return formater.LineComment; } }
        public string BlockCommentStart { get { return formater.BlockCommentStart; } }
        public string BlockCommentEnd { get { return formater.BlockCommentEnd; } }
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
