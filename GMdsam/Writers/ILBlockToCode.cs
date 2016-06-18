using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GameMaker.Ast;
using System.Diagnostics;

namespace GameMaker.Writers
{
  
    public class BlockToCode : PlainTextWriter, IMessages
    {
        public class NodeInfo : IEquatable<NodeInfo>
        {
            public readonly ILNode Node;
            public readonly int Line;
            public readonly int Col;
            public NodeInfo(ILNode node, int Line, int Col)
            {
                this.Node = node;
                this.Line = Line;
                this.Col = Col;
            }
            public override string ToString()
            {
                return "(" + Line + "," + Col + "):" + Node.ToString();
            }
            public override int GetHashCode()
            {
                return Line << 16 | (Col & 0xFFFF);
            }
            public override bool Equals(object obj)
            {
                if (object.ReferenceEquals(obj, null)) return false;
                if (object.ReferenceEquals(obj, this)) return true;
                NodeInfo test = obj as NodeInfo;
                return test != null && Equals(test);
            }
            public bool Equals(NodeInfo other)
            {
                return Line == other.Line && Col == other.Col;
            }
        }
        Dictionary<ILNode, NodeInfo> node_infos;
        public IReadOnlyDictionary<ILNode,NodeInfo> NodeInfos {  get { return node_infos; } }
        IMessages error = null;

        void Init(IMessages error,bool catch_infos)
        {
            if (error == null) throw new ArgumentNullException("error");
            this.error = error;
            if (catch_infos) node_infos = new Dictionary<ILNode, NodeInfo>();
        }
        #region IMessages Interface
        public void Message(string msg) { error.Message(msg); }
        public void Message(string msg, ILNode node) { error.Message(msg, node); }
        public void Error(string msg) { error.Error(msg); }
        public void Error(string msg, ILNode node) { error.Error(msg, node); }
        public void Warning(string msg) { error.Warning(msg); }
        public void Warning(string msg, ILNode node) { error.Warning(msg, node); }
        public void Info(string msg) { error.Info(msg); }
        public void Info(string msg, ILNode node) { error.Info(msg, node); }
      //  public string Name { get { return error.Name; } }
        public void FatalError(string msg) { error.FatalError(msg); }
        public void FatalError(string msg, ILNode node) { error.FatalError(msg, node); }
        #endregion

        public BlockToCode(IMessages error,bool catch_infos = false) : base()
        {
            Init( error, catch_infos);
        }
        public BlockToCode(IMessages error, string filename, bool catch_infos = false) : base(filename)
        {
            Init( error, catch_infos);
        }
        public BlockToCode(string filename, bool catch_infos = false) : base(filename)
        {
            Init( new ErrorContext(Path.GetFileNameWithoutExtension(filename)), catch_infos);
        }
        public BlockToCode(IMessages error, TextWriter writer, bool catch_infos = false) : base(writer)
        {
            Init( error, catch_infos);
        }
        public void WriteNodesComma<T>(IEnumerable<T> nodes) where T : ILNode
        {
            this.WriteCommaDelimited(nodes, n => { Write(n); return false; });
        }
        protected void InsertLineInfo<T>(T n) where T : ILNode
        {
            if (node_infos != null) node_infos.Add(n, new NodeInfo(n, this.LineNumber, this.RawColumn));
        }
        #region The Code Writing Virtuals and helpers

        public virtual string LineComment { get { return "//"; } }
        public virtual string NodeEnding { get { return ";"; } }
        public virtual string Extension { get { return "js"; } }
        public virtual string BlockCommentStart { get { return "/*"; } }
        public virtual string BlockCommentEnd { get { return "*/"; } }
        protected virtual int Precedence(GMCode code)
        {
            switch (code)
            {
                case GMCode.Not:
                case GMCode.Neg:
                    return 7;
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                    return 6;
                case GMCode.Add:
                case GMCode.Sub:
                // case GMCode.Pow: // not in gm
                case GMCode.Concat: // add goes here
                    return 5;

                case GMCode.Sge:
                case GMCode.Slt:
                case GMCode.Sgt:
                case GMCode.Sle:
                    return 4;
                case GMCode.Sne:
                case GMCode.Seq:
                    return 3;
                case GMCode.LogicAnd:
                    return 2;
                case GMCode.LogicOr:
                    return 1;
                default:
                    return 8;
            }
        }

        void WriteAssign(ILExpression left, ILExpression right)
        {
            // Lets make assign nice here, instead of having to
            // make another rule to fix the ast
            WriteVariable(left);
            ILVariable v = left.Operand as ILVariable;
            ILExpression ver = right.Arguments.ElementAtOrDefault(0);
            ILVariable v2;
            if (ver != null && ver.Code == GMCode.Var && (v2 = ver.Operand as ILVariable).Name == v.Name)
            {
                // There are a few ways to do this, but the simplest way?  Turn them both into strings and compare them
                // ... silly I know but its either that or build, from scrach, a equality system
                if (ver.ToString() == left.ToString())
                {
                    ILValue cv;
                    switch (right.Code)
                    {
                        case GMCode.Add:
                            if (right.Arguments[1].Match(GMCode.Constant, out cv) && cv.IntValue == 1)
                            {
                                Write("++");
                                return;
                            }
                            else
                            {
                                Write("+");
                                right = right.Arguments[1];
                            }
                            break;
                        case GMCode.Sub:
                            if (right.Arguments[1].Match(GMCode.Constant, out cv) && cv.IntValue == 1)
                            {
                                Write("--");
                                return;
                            }
                            else
                            {
                                Write("-");
                                right = right.Arguments[1];
                            }
                            break;
                        case GMCode.Mul:
                            Write("*");
                            right = right.Arguments[1];
                            break;
                        case GMCode.Div:
                            Write("/");
                            right = right.Arguments[1];
                            break;
                        case GMCode.Mod:
                            Write("%");
                            right = right.Arguments[1];
                            break;
                    }
                }
            }
            Write("= "); // default
            Write(right);
        }
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
            if (needParm) Write('(');
            Write(expr.Arguments[index]);
            if (needParm) Write(')');
        }
 
        public virtual void Write(ILValue v) {
            Write(v.ToString());
            bool inblockComment = false;
            
            if (!(v.Value is string) && v.ValueText != null)
            {
                Write(BlockCommentStart);
                inblockComment = true;
                Write(' ');
                Write(v.ValueText);
            }
            if (Context.doAssigmentOffsets)
            {
                if (!inblockComment) { Write(BlockCommentStart); Write(' '); inblockComment = true; }
                else Write(", ");
                Write("([0x{0:X8}],{1})", (int)v.DataOffset, v.ByteSize);
            }
            if (inblockComment)
            {
                Write(' ');
                Write(BlockCommentEnd);
            }
        }

        protected void WritePreExpresion(string op, ILExpression expr) {
            Write(op);
            WriteParm(expr, 0);
        }
        protected void WriteTreeExpression(string op,ILExpression expr )
        {
            WriteParm(expr, 0);
            Write(' ');
            Write(op); // incase concat gets in there?
            Write(' ');
            WriteParm(expr, 1);
        }
        public void Write(ILNode n)
        {
            Write((dynamic) n);
        }
        public virtual void Write(ILLabel label)
        {
            Write(label.Name);
            Write(':');
        }
        public virtual void WriteVariable(ILExpression ve)
        {
            Debug.Assert(ve.Code == GMCode.Var);
            ILVariable v = ve.Operand as ILVariable;
            if (v.Instance == 0)
                Write(ve.Arguments[0]);
            else
                Write(v.InstanceName);
            Write('.');
            Write(v.Name);
            if (ve.Arguments.Count > 1)
            {
                Write('[');
                if (ve.Arguments[1].Code == GMCode.Array2D)
                {
                    Write(ve.Arguments[1].Arguments[0]);
                    Write(',');
                    Write(ve.Arguments[1].Arguments[1]);
                } else Write(ve.Arguments[1]);
                Write(']');
            }
        }
        public virtual void Write(ILExpression expr)
        {
            InsertLineInfo(expr);

            //  if (Code.isExpression())
            //      WriteExpressionLua(output);
            // ok, big one here, important to get this right
            switch (expr.Code)
            {
                case GMCode.Not: WritePreExpresion("!", expr); break;
                case GMCode.Neg: WritePreExpresion("-", expr); break;
                case GMCode.Mul: WriteTreeExpression("*", expr); break;
                case GMCode.Div: WriteTreeExpression("/", expr); break;
                case GMCode.Mod: WriteTreeExpression("%", expr); break;
                case GMCode.Add: WriteTreeExpression("+", expr); break;
                case GMCode.Sub: WriteTreeExpression("-", expr); break;
                case GMCode.Concat: WriteTreeExpression("+", expr); break;
                // in lua, these have all the same prec
                case GMCode.Sne: WriteTreeExpression("!=", expr); break;
                case GMCode.Sge: WriteTreeExpression(">=", expr); break;
                case GMCode.Slt: WriteTreeExpression("<", expr); break;
                case GMCode.Sgt: WriteTreeExpression(">", expr); break;
                case GMCode.Seq: WriteTreeExpression("==", expr); break;
                case GMCode.Sle: WriteTreeExpression("<=", expr); break;
                case GMCode.LogicAnd: WriteTreeExpression("&&", expr); break;
                case GMCode.LogicOr: WriteTreeExpression("||", expr); break;
                case GMCode.Call:
                    {
                        ILCall call = expr.Operand as ILCall;
                        Write(call.Name);
                        Write('('); // self is normaly the first of eveything
                        WriteNodesComma(expr.Arguments);
                        Write(')');
                    }
                    break;
                case GMCode.Constant: // primitive c# type
                    Write(expr.Operand as ILValue);
                    break;
                case GMCode.Var:  // should be ILVariable
                    WriteVariable(expr);
                    break;
                case GMCode.Exit:
                    Write("return // exit");
                    break;
                case GMCode.Ret:
                    Write("return ");
                    Write(expr.Arguments.Single());
                    break;
                case GMCode.LoopOrSwitchBreak:
                    Write("break");
                    break;
                case GMCode.LoopContinue:
                    Write("continue");
                    break;
                case GMCode.Assign:
                    WriteAssign(expr.Arguments[0], expr.Arguments[1]);

                    break;
                // These shouldn't print, debugging
                default:
                    Write("ExprError: ");
                    Write(expr.ToString());
                    Flush();
                    error.Error("Expression error on line " + this.LineNumber);
                    /*
                  //  Close(); // we die here
                    var root = expr.Root;
                    using (StreamWriter sw = new StreamWriter("bad_write.txt"))
                    {
                        sw.Write(root.ToString());
                        sw.Flush();
                    }
                    throw new Exception("Not Implmented! ugh");
                    */
                    break;
            }
            if(expr.Comment != null)
            {
                Write(BlockCommentStart);
                Write(' ');
                Write(expr.Comment);
                Write(' ');
                Write(BlockCommentEnd);
            } 
        }
        public virtual void Write(ILCondition condition) {
            InsertLineInfo(condition);
            Write("if(");
            Write(condition.Condition); // want to make sure we are using the debug
            Write(")");
            WriteSingleLineOrBlock(condition.TrueBlock);
            if (condition.FalseBlock != null && condition.FalseBlock.Body.Count > 0)
            {
                if (this.Column > 0 && this.CurrentLine != "}") WriteLine();
                Write(" else ");
                WriteSingleLineOrBlock(condition.FalseBlock);
            }
        }

        bool SingleExpressionInBlock(ILBlock block, out ILExpression e)
        {
            if (block.Body.Count == 1 && (e = block.Body.SingleOrDefault() as ILExpression) != null) return true;
            e = default(ILExpression);
            return false;
        }
        protected void WriteSingleLineOrBlock(ILBlock block)
        {
            ILExpression e = null;
            if (SingleExpressionInBlock(block, out e))
            {
                if (e.ToString().Length + this.Column > 80) WriteLine();
                this.Indent++;
                Write(' ');
                Write(e);
                Write(';');
                this.Indent--;
            } 
            else
            {
                WriteLine(" {");
                Write(block, true);
                Write("}");
            }
        }
        public virtual void Write(ILRepeat loop)
        {
            InsertLineInfo(loop);
            Write("repeat(");
            Write(loop.Condition); // want to make sure we are using the debug
            Write(") ");
            WriteSingleLineOrBlock(loop.Body);
        }
        public virtual void Write(ILWhileLoop loop) {
            InsertLineInfo(loop);
            Write("while(");
            Write(loop.Condition); // want to make sure we are using the debug
            Write(")");
            WriteSingleLineOrBlock(loop.Body);
        }
        public virtual void Write(ILWithStatement with) {
            InsertLineInfo(with);
            string env = with.Condition.Code == GMCode.Constant ? Context.InstanceToString((int)(with.Condition.Operand as ILValue)) : null;
            if (env != null) WriteLine("// {0}", env);
            Write("with(");
            Write(with.Condition); // want to make sure we are using the debug
            Write(")");
            WriteSingleLineOrBlock(with.Body);
        }
        public virtual void Write(ILSwitch f) {
            InsertLineInfo(f);
            Write("switch(");
            Write(f.Condition);
            WriteLine(") {");
            Indent++;
            foreach (var c in f.Cases)
            {
                if (c.Values == null)
                {
                    WriteLine("default:"); 
                }
                else
                {
                    foreach (var v in c.Values)
                    {
                        Write("case ");
                        Write(v);
                        WriteLine(":");
                    }
                }
                Write((ILBlock) c); // write it as a block
            }
            if (f.Default != null && f.Default.Body.Count > 0)
            {
                Write("default:");
                Write(f.Default);
            }
            Indent--;
            Write("}");
        }
        #endregion
        public void Write(ILBlock block)
        { // this is why we feed eveythign though this thing
            WriteNodeList(block.Body, true);
        }

        public virtual void WriteNodeList(IList<ILNode> body, bool ident_block)
        {
            if (ident_block) Indent++;
            if (body.Count > 0)
            {
                for (int i = 0; i < body.Count; i++)
                {
                    ILNode n = body[i];
           //         ILExpression e = n as ILExpression;
          //          if (e != null) Write('(' + e.Type.ToString() + ')');
                    Write(n);
                    // prevents the semi-colen appering after the '}'  Soo annyed me
                    if (NodeEnding != null && !this.CurrentLine.isEndingEqual('}', ';')) Write(NodeEnding);
                    WriteLine();
                }
            }
            if (ident_block) Indent--;
        }
        public void Write(ILBasicBlock bb)
        {
            InsertLineInfo(bb);
            LineHeader = LineComment;
            WriteLine("---- BasicBlock ----");
            WriteNodeList(bb.Body, true);
            LineHeader = null;
        }
        // quick way to write a block
        public void Write(ILBlock block, bool ident_block)
        {
            InsertLineInfo(block);
            WriteNodeList(block.Body, ident_block);
        }
        public override void WriteToFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(Path.GetExtension(filename)))
                filename = Path.ChangeExtension(filename, Extension);
            base.WriteToFile(filename);
        }
        public override async Task AsyncWriteToFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(Path.GetExtension(filename)))
                filename = Path.ChangeExtension(filename, Extension);
            await base.AsyncWriteToFile(filename);
        }

        ~BlockToCode()
        {
            Dispose(false);
        }
    }
}