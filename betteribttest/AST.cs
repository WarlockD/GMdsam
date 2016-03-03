using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections;
using System.IO;

namespace betteribttest
{
    public static class AstIListExtinson
    {
        public static bool ContainsType<T>(this List<Ast> list) where T : Ast
        {
            foreach (var ast in list) if (ast.HasType<T>()) return true;
            return false;
        }
    }
    public abstract class Ast : IEquatable<Ast>
    {
        public void SaveToFile(string filename)
        {
            using (StreamWriter wr = new StreamWriter("temp_statement.txt"))
            {
                this.DecompileToText(wr);
            }
        }
        static protected void AddParm(TextWriter wr, string s)
        {
            wr.Write('(');
            wr.Write(s);
            wr.Write(')');
        }/*
        protected void WriteChildParms(TextWriter wr, int index)
        {
            bool needParns0 = this[index] is AstTree;
            if (needParns0) wr.Write('(');
            wr.Write(this[index].ToString());
            if (needParns0) wr.Write(')');
        }
        */
        static protected void AddParm(TextWriter wr, Ast a)
        {
            if (a is AstBinary)
            {
                wr.Write('(');
                a.DecompileToText(wr);
                wr.Write(')');
            }
            else a.DecompileToText(wr);
        }
        /// <summary>
        /// Makes a copy of this Ast
        /// </summary>
        /// <returns>Deap copy of this ast</returns>
        public abstract Ast Copy();
        public Ast Parent { get; private set; }
        public virtual bool TryParse(out int value) { value = 0; return false; }
        public Instruction Instruction { get; private set; }
        protected Ast(Instruction i) { Instruction = i; Parent = null;  }
        protected Ast() { Instruction = null; Parent = null;  }

        /// <summary>
        /// So this is the main function that decompiles a statment to text.  override this instead of
        /// ToString in ALL inherted functions
        /// </summary>
        /// <param name="indent">Amount of spaces to indent</param>
        /// <param name="sb">String bulder that the line gets added to</param>
        /// <returns>Lenght of line or longest line, NOT the amount of text added</returns>
        public abstract int DecompileToText(TextWriter wr);
        public virtual IEnumerable<T> FindType<T>(bool recursive = true) where T : Ast
        {
            T a = this as T;
            if (a != null) yield return a;
        }
        public virtual bool HasType<T>() where T : Ast
        {
            T a = this as T;
            if (a != null) return true;
            else return false;
        }
        public override string ToString()
        {
            StringWriter wr = new StringWriter();
            DecompileToText(wr);
            return wr.ToString();
        }
        public bool Equals(Ast obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (obj.GetType() != this.GetType()) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            return false;
        }
        /// <summary>
        /// Sealed Equals, eveything below this is sealed as all leafs use children
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (obj.GetType() != this.GetType()) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            return false;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            if (this.Parent != null) hash ^= this.Parent.GetHashCode();
            return hash;
        }
        public virtual Ast Invert() 
        {
            return new AstNot(this);
        }
        /// <summary>
        /// Built in sainty checks for when we set the parent of a node
        /// </summary>
        /// <param name="child"></param>
        protected void ParentSet(Ast child)
        {
            if (!object.ReferenceEquals(child, null))
            {
                if (object.ReferenceEquals(child, this)) throw new ArgumentNullException("Child cannot be own Parent", "child");
                if (object.ReferenceEquals(child.Parent, this)) throw new ArgumentNullException("Item is already set for this", "child");
                if (child.Parent != null) throw new ArgumentNullException("Item is owned by somone else", "child");
                child.Parent = this;
            }
        }
        /// <summary>
        /// Built in sainty checks for when we clear the parent of a node
        /// </summary>
        /// <param name="child"></param>
        protected void ParentClear(Ast child)
        {
            if (!object.ReferenceEquals(child, null))
            {
                if (object.ReferenceEquals(child, this)) throw new ArgumentNullException("Child cannot be own Parent", "child");
                if (!object.ReferenceEquals(child.Parent, this)) throw new ArgumentNullException("Child parrent is not this", "child");
                if (child.Parent == null) throw new ArgumentNullException("Child parrent is already", "child");
                child.Parent = null;
            }
        }
    }
    /// <summary>
    /// Statment Class is a statment.  It seperates this between tree values
    /// </summary>
    public abstract class AstStatement : Ast
    {
        public AstStatement(Instruction i) : base(i) { }
        public AstStatement() : base() { }
        public override Ast Invert()
        {
            throw new Exception("Cannot invert a statement");
        }
        public virtual bool ContainsType<T>(bool recursive = false) where T : AstStatement { return false; }
        public virtual List<T> FindAllType<T>(bool recursive = false) where T : AstStatement { return null; }
    }
    // This class is used as filler leaf on an invalid stack
    public class AstPop : Ast
    {
        public AstPop() : base(null) { }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write("Pop()");
            return 0;
        }
        public override Ast Copy()
        {
            AstPop copy = new AstPop();
            return copy;
        }
    }
    // This class is used in filler when the stack is an odd shape between labels
    public class PushStatement : AstStatement
    {
        Ast _value = null;
        public Ast Value {get;protected set;}
        ~PushStatement()
        {
            ParentClear(_value);
        }
        public PushStatement(Ast ast) : base(ast.Instruction) { Value = ast; ParentSet(ast); }
        PushStatement(Instruction i) : base(i) { Value = null; }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write("Push(");
            if (_value != null) _value.DecompileToText(wr); else wr.Write("NullPush");
            wr.Write(")");
            return 0;
        }
        public override Ast Copy()
        {
            PushStatement copy = new PushStatement(Value.Copy());
            return copy;
        }
    }
    // Made a helper class
    public abstract class AstBinary : Ast
    {
        public abstract string Operation { get; }
        public Ast Left { get; protected set; }
        public Ast Right { get; protected set; }
        protected AstBinary(Instruction i, Ast left, Ast right) : base(i) {
            Debug.Assert(right != null && left != null);
            Left = left;
            Right = right;
            ParentSet(Left);
            ParentSet(Right);
        }
        protected AstBinary(Ast left, Ast right) : base() {
            Debug.Assert(right != null && left != null);
            Left = left;
            Right = right;
            ParentSet(Left);
            ParentSet(Right);
        }
        ~AstBinary()
        {
            ParentClear(Left);
            ParentClear(Right);
        }

        public override int DecompileToText(TextWriter wr)
        {
            Ast.AddParm(wr, Left);  
            wr.Write(Operation);
            Ast.AddParm(wr, Right); 
            return 0;
        }
    }
    public abstract class AstUinary : Ast {
        public abstract string Operation { get; }
        public Ast Right { get; protected set; }
        protected AstUinary(Instruction i, Ast right) : base(i) { Debug.Assert(Right != null);  Right = right; ParentSet(Right); }
        protected AstUinary(Ast right) : base() { Debug.Assert(Right != null); Right = right;ParentSet(Right); }
        ~AstUinary()
        {
            ParentClear(Right);
        }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write(Operation);
            Ast.AddParm(wr, Right);  
            return 0;
        }
    }
    public class AstNegate : AstUinary
    {
        public override string Operation { get { return "-"; } }
        public AstNegate(Instruction i, Ast right) : base(i,right) { }
        public override Ast Copy()
        {
            return new AstNegate(this.Instruction, Right.Copy());
        }
    }
    public class AstNot : AstUinary
    {
        public override string Operation { get { return "!"; } }
        public AstNot(Instruction i,Ast right) : base(i, right) { }
        public AstNot(Ast right) : base(right) { }
        public override Ast Copy()
        {
            return new AstNot(this.Instruction, Right.Copy());
        }
        public override Ast Invert()
        {
            return Right.Copy();
        }
    }
    public class LogicalOr : AstBinary
    {
        public override string Operation { get { return " || "; } }
        public LogicalOr(Ast left, Ast right) : base(left,right) { }
        public override Ast Copy()
        {
            return new LogicalOr(Left.Copy(),Right.Copy());
        }
        public override Ast Invert()
        {
            return new LogicalAnd(Left.Invert(), Right.Invert());
        }
    }
    public class LogicalAnd : AstBinary
    {
        public override string Operation { get { return " && "; } }
        public LogicalAnd(Ast left, Ast right) : base(left,right) { }
        public override Ast Copy()
        {
            return new LogicalAnd(Left.Copy(), Right.Copy());
        }
        public override Ast Invert()
        {
            return new LogicalOr(Left.Invert(), Right.Invert());
        }
    }
    public class AstTree : AstBinary
    {
        public GMCode Op { get; protected set; }
        string _opString;
        public override string Operation { get { return _opString; } }
        public AstTree(Instruction i, GMCode op, Ast left, Ast right) : base(i,left,right) {
            _opString = " "+ op.getOpTreeString()+ " ";
            this.Op = op;
        }
        public override Ast Copy()
        {
            return new LogicalAnd(Left.Copy(), Right.Copy());
        }
        public override Ast Invert() // Never call base.Invert as it will recursive loop for resons
        {
            GMCode io = Op.getInvertedOp();
            Debug.Assert(io != GMCode.BadOp);
            return new AstTree(this.Instruction, io, this.Left.Copy(), this.Right.Copy());
        }
    }
    public class AstCall : Ast
    {
        List<Ast> _arguments;
        public string Name { get; protected set; }
        public IReadOnlyList<Ast> Arguments {  get { return _arguments; } }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write(Name);
            wr.Write('(');
            if (_arguments.Count == 1) _arguments[0].DecompileToText(wr);
            else
            {
                foreach (var child in _arguments)
                {
                    wr.Write(',');
                    child.DecompileToText(wr);
                }
            }
            wr.Write(')');
            return 0;
        }
        ~AstCall()
        {
            _arguments.ForEach(o => ParentClear(o));
        }
        IEnumerable<Ast> CopyArguments()
        {
            foreach (var ast in _arguments) yield return ast.Copy();
        }
        public AstCall(Instruction i, string name, IEnumerable<Ast> arguments) : base(i) {
            Name = name;
            _arguments = arguments.ToList();
            _arguments.ForEach(o => ParentSet(o));
        }
        public override Ast Copy()
        { 
            AstCall copy = new AstCall(this.Instruction,this.Name, CopyArguments());
            return copy;
        }
    }
    public class AstConstant : Ast
    {
        int _parsedValue;
        public string Value { get; private set; }
        public GM_Type Type { get; private set; }
        public override bool TryParse(out int value)
        {
            if(Type ==  GM_Type.Int || Type == GM_Type.Short || Type == GM_Type.Long || Type == GM_Type.Bool)
            {
                value = _parsedValue;
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }
        private AstConstant(Instruction i, string value, GM_Type type, int parsedValue) :base(i)
        {
            Value = value;
            Type = type;
            _parsedValue = parsedValue;
        }
        public override Ast Copy()
        {
            AstConstant copy = new AstConstant(this.Instruction, this.Value, this.Type,_parsedValue);
            return copy;
        }
        public AstConstant(int i) : base() { Value = i.ToString(); Type = GM_Type.Int; _parsedValue = i; }
        public AstConstant(string value) : base() { Value = value; Type = GM_Type.String; _parsedValue = 0; }
        public AstConstant(Instruction i, short s) : base(i) { Value = s.ToString(); Type = GM_Type.Short; _parsedValue = s; }
        private AstConstant(Instruction i, object v, GM_Type type) : base(i) {
            Value = v.ToString();
            Type = type;
            if (Type == GM_Type.Int || Type == GM_Type.Short || Type == GM_Type.Long) _parsedValue = int.Parse(Value);
            else if (Type == GM_Type.Bool) _parsedValue = (bool)(v) ? 1 : 0;
            else _parsedValue = 0;
        }
        public static AstConstant FromInstruction(Instruction i)
        {
            Debug.Assert(i.GMCode == GMCode.Push && i.FirstType != GM_Type.Var);
            AstConstant con = null;
            switch (i.FirstType)
            {
                case GM_Type.Double:
                case GM_Type.Float:
                case GM_Type.Int:
                case GM_Type.Long:
                case GM_Type.String:
                    con = new AstConstant(i, i.Operand, i.FirstType); break;
                case GM_Type.Short:
                    con = new AstConstant(i, i.Instance); break;
                default:
                    throw new Exception("Bad Type");
            }
            return con;
        }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write(Value);
            return 0;
        }
    }

    public class AstVar : Ast
    {
        Ast _arrayIndex;
        public int ExtraData { get; set; }
        public string Name { get; protected set; }
        public GM_Type Type { get { return GM_Type.Var; } }
        public string Instance { get; protected set;  }
        public int InstanceValue { get; protected set; }
        public Ast ArrayIndex {get { return _arrayIndex; }}
        ~AstVar()
        {
            ParentClear(_arrayIndex);
        }
        public AstVar(Instruction i,  int instanceValue, string instance,  string name) : base(i)
        {
            Name = name;
            Instance = instance;
            _arrayIndex = null;
            ExtraData = i.OperandInt;
            InstanceValue = instanceValue;
        }

        public AstVar(Instruction i, int instanceValue, string instance, string name, Ast Index) : base(i)
        {
            Name = name;
            Instance = instance;
            _arrayIndex = Index;
            ParentSet(_arrayIndex);
            ExtraData = i.OperandInt;
            InstanceValue = instanceValue;
        }
        public override Ast Copy()
        {
            AstVar copy = new AstVar(this.Instruction,this.InstanceValue, this.Instance,this.Name, _arrayIndex.Copy());
            return copy;
        }
        public static bool _showSelf = false;
        public static bool Debug_DisplaySelf { get { return _showSelf; } set { _showSelf = false; } }
        public override int DecompileToText(TextWriter wr)
        {
            if (_showSelf || this.Instance != "self")
            {
                wr.Write(this.Instance);
                wr.Write('.');
            }
            wr.Write(Name);
            if(_arrayIndex != null)
            {
                wr.Write('[');
                _arrayIndex.DecompileToText(wr);
                wr.Write(']');
            }
            return 0;
        }
    }
    public class AssignStatment : AstStatement
    {
        public AstVar Variable { get; protected set; }
        public Ast Value { get; protected set; }
        public AssignStatment(Instruction i, AstVar variable, Ast value) : base(i) {
            Debug.Assert(variable != null && value != null);
            Variable = variable;
            Value = value;
            ParentSet(Variable);
            ParentSet(Value);
        }
        ~AssignStatment()
        {
            ParentClear(Variable);
            ParentClear(Value);
        }
        public override int DecompileToText(TextWriter wr)
        {
            Variable.DecompileToText(wr);
            wr.Write(" = ");
            Value.DecompileToText(wr);
            wr.WriteLine();
            return 1;
        }
        public override Ast Copy()
        {
            AssignStatment n = new AssignStatment(this.Instruction, this.Variable.Copy() as AstVar, this.Value.Copy());
            return n;
        }
    }
    public class CallStatement : AstStatement
    {
        public AstCall Call { get; protected set; }
        public CallStatement(Instruction popz, AstCall call) : base(popz)
        {
            Debug.Assert(call != null);
            ParentSet(call);
            Call = call;
        }
        ~CallStatement()
        {
            ParentClear(Call);
        }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write("void ");
            Call.DecompileToText(wr); 
            wr.WriteLine();
            return 1;
        }
        public override Ast Copy()
        {
            CallStatement copy = new CallStatement(this.Instruction,this.Call.Copy() as AstCall);
            return copy;
        }
    }
    public class StatementBlock : AstStatement,  IList<AstStatement>
    {
        List<AstStatement> _statements;
        public StatementBlock() : base() { _statements = new List<AstStatement>(); }
        public StatementBlock(IEnumerable<AstStatement> list) : this() {
            foreach(var a in list)
            {
                Ast copy = a.Copy();
                ParentSet(copy);
                _statements.Add(a);
            }
        }
        // In case we build the block outside of this function, we can use this to assign it
        public StatementBlock(List<AstStatement> list) : base()
        {
            _statements = list;
            _statements.ForEach(o => ParentSet(o));
        }
        ~StatementBlock()
        {
            Clear();
        }
        public int DecompileToText(System.CodeDom.Compiler.IndentedTextWriter wr)
        {
            int count = 2; // the two {}
            wr.WriteLine('{');
            wr.Indent++;
            foreach (var statement in _statements)
            {
#if DEBUG
                int line_count = statement.DecompileToText(wr);
                Debug.Assert(line_count != 0); // all statments should return atleast 1
                count += line_count;
#else
                count+= statement.DecompileToText(wr);
#endif
            }
            wr.Indent--;
            wr.WriteLine('}');
            wr.Flush();
            return count;
        }
        public override int DecompileToText(TextWriter wr)
        {
            if (_statements.Count == 0) { wr.WriteLine("{ Empty Statment Block }"); return 1; }
            else if (_statements.Count == 1) { return _statements[0].DecompileToText(wr); }
            else
            {
                System.CodeDom.Compiler.IndentedTextWriter ident_wr = wr as System.CodeDom.Compiler.IndentedTextWriter;
                if (ident_wr == null) ident_wr = new System.CodeDom.Compiler.IndentedTextWriter(wr); // we are NOT in a statment block so we need to make this
                return DecompileToText(ident_wr);
            }
        }
        public override bool ContainsType<T>(bool recursive = false) {
            foreach(var a in _statements)
            {
                if (a is T) return true;
                if (recursive && a.ContainsType<T>(recursive)) return true;
            }
            return false;
        }
        public override List<T> FindAllType<T>(bool recursive = false)  {
            List<T> list = new List<T>();
            foreach(var a in _statements)
            {
                T test = a as T;
                if (test != null) list.Add(test);
                if (recursive) {
                    var test_list = test.FindAllType<T>(recursive);
                    if (test_list != null) list.AddRange(test_list);
                }
            }
            if (list.Count == 0) return null;
            else return list;
        }
        public override Ast Copy()
        {
            StatementBlock copy = new StatementBlock(this);
            return copy;
        }
        #region IList Interface
        public AstStatement this[int index]
        {
            get
            {
                return _statements[index];
            }

            set
            {
                ParentClear(_statements[index]);
                ParentSet(value);
                _statements[index] = value;
            }
        }

        public int Count { get { return _statements.Count; } }

        public bool IsReadOnly { get { return false; } }

        public void Add(AstStatement item)
        {
            ParentSet(item);
            _statements.Add(item);
        }

        public void Clear()
        {
            _statements.ForEach(o => ParentClear(o));
            _statements.Clear();
        }

        public bool Contains(AstStatement item)
        {
            return item.Parent == this &&  _statements.Contains(item);
        }
       

        public void CopyTo(AstStatement[] array, int arrayIndex)
        {
            for(int i= arrayIndex;i<array.Length;i++) ParentSet(array[i]);
            _statements.CopyTo(array, arrayIndex);
        }
        public IEnumerator<AstStatement> GetEnumerator()
        {
            return _statements.GetEnumerator();
        }
        public int IndexOf(AstStatement item)
        {
            return _statements.IndexOf(item);
        }

        public void Insert(int index, AstStatement item)
        {
            ParentSet(item);
            _statements.Insert(index, item);
        }

        public bool Remove(AstStatement item)
        {
            if (this.Parent != this) return false;
            ParentClear(item);
            return _statements.Remove(item);
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index > _statements.Count - 1) throw new ArgumentOutOfRangeException("index", "Index out of Range");
            ParentClear(_statements[index]);
            _statements.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _statements.GetEnumerator();
        }
        #endregion
    }
    // This is filler for a blank statement or a message statment for debugging
    public class CommentStatement : AstStatement
    {
        List<string> _lines;
        public IReadOnlyList<string> Message {  get { return _lines; } }
        public CommentStatement(Instruction info, string message) : base(info) { _lines = new List<string>(); AddLine(message); }
        public CommentStatement(string message) : base() { _lines = new List<string>(); AddLine(message); }
        public CommentStatement(IEnumerable<string> message) : base() { _lines = message.ToList(); }
        public override Ast Copy()
        {
            CommentStatement copy = new CommentStatement(_lines);
            return copy;
        }
        public void AddLine(string message)
        {
            _lines.Add(message);
        }
        public int DecompileToText(System.CodeDom.Compiler.IndentedTextWriter wr)
        {
            int count = 1 + _lines.Count;
            wr.Indent++;
            wr.Write("/* ");
            foreach (var line in _lines) wr.WriteLine(line);
            wr.WriteLine(" */");
             wr.Indent--;
            wr.Flush();
            return count;
        }
        public override int DecompileToText(TextWriter wr)
        {
            if (_lines.Count == 0) { wr.WriteLine("// No Comments"); return 1; }
            else if (_lines.Count == 1) {
                wr.Write("// ");
                wr.WriteLine(_lines[0]);
                return 1;
            }
            else
            {
                System.CodeDom.Compiler.IndentedTextWriter ident_wr = wr as System.CodeDom.Compiler.IndentedTextWriter;
                if (ident_wr == null) ident_wr = new System.CodeDom.Compiler.IndentedTextWriter(wr); // we are NOT in a statment block so we need to make this
                return DecompileToText(ident_wr);
            }
        }

    }
    public class ExitStatement : AstStatement
    {
        public override Ast Copy()
        {
            ExitStatement copy = new ExitStatement(this.Instruction);
            return copy;
        }
        public ExitStatement(Instruction i) : base(i) { }
        public override int DecompileToText(TextWriter wr)
        {
            wr.WriteLine("return // Exit Statement ");
            return 1;
        }
    }
    public class GotoStatement : AstStatement
    {
        public LabelStatement LabelLinkTo=null;
        public override Ast Copy()
        {
            GotoStatement copy = new GotoStatement(this.Instruction, Target);
            return copy;
        }
        public Label Target { get; protected set; }
        public GotoStatement(Instruction info) : base(info) {
           // Debug.Assert(info.GMCode == GMCode.B);
            Target = info.Operand as Label;
        }
        public GotoStatement(Label target) : base() {
            Debug.Assert(target != null);
            Target = target;
        }
        public GotoStatement(Instruction info,Label target) : base(info)
        {
            Debug.Assert(target != null);
            Target = target;
        }
        public override int DecompileToText(TextWriter wr)
        {
#if TEST_LATTER
            Debug.Assert(Target != null && Count == 1 && Goto != null,"No Linking Label Statement"); // no linking Label Statement?
#endif
            if (LabelLinkTo != null) wr.Write("/* Linked */");
            wr.Write("goto ");
            wr.Write(Target);
            if (LabelLinkTo != null) wr.WriteLine("/* Linked */");
            else wr.WriteLine();
            return 1;
        }
    }
    // Used for decoding other statements
    public class IfStatement : AstStatement
    {
        public Ast Condition { get; protected set; }
        public AstStatement Then { get; protected set; }
        public AstStatement Else { get; protected set; }
        public override bool ContainsType<T>(bool recursive = false)
        {
            if(Then is T || Else is T) return true;
            if (recursive) if (Then.ContainsType<T>(recursive) || Then.ContainsType<T>(recursive)) return true;
            return false;
        }
        public override List<T> FindAllType<T>(bool recursive = false)
        {
            T ThenT = Then as T;
            T ElseT = Else as T;
            if (ThenT == null && ElseT == null && !recursive) return null;
            List<T> list = new List<T>();
            if (ThenT != null) list.Add(ThenT);
            if (ElseT != null) list.Add(ElseT);
            if (recursive)
            {
                var ThenList = Then.FindAllType<T>(recursive);
                if (ThenList != null) list.AddRange(ThenList);
                if(Else != null)
                {
                    var ElseList = Else.FindAllType<T>(recursive);
                    if (ThenList != null) list.AddRange(ThenList);
                }
            }
            if (list.Count != 0) return list;
            else return null;
        }
        public override Ast Copy()
        {
            IfStatement copy;
            if (Else == null)
                copy = new IfStatement(this.Instruction, Condition.Copy(), Then.Copy() as AstStatement);
            else
                copy = new IfStatement(this.Instruction, Condition.Copy(), Then.Copy() as AstStatement, Else.Copy() as AstStatement);
            return copy;
        }
 
        public IfStatement(Instruction info, Ast Condition, AstStatement Then) : base(info) {
            this.Condition = Condition;
            this.Then = Then;
            ParentSet(this.Condition);
            ParentSet(this.Then);
            Else = null;
        }
        ~IfStatement()
        {
            ParentClear(this.Else);
            ParentClear(this.Then);
            ParentClear(this.Condition);
        }
        public IfStatement(Instruction info, Ast Condition, AstStatement Then, AstStatement Else) : this(info, Condition, Then)
        {
            this.Else = Else;
            ParentSet(this.Else);
        }
        public IfStatement(Instruction info, Ast Condition, Label target) : base(info)
        {
            this.Condition = Condition;
            this.Then = new GotoStatement(target);
            Else = null;
        }
        public override int DecompileToText(TextWriter wr)
        {
            int count=1;
            wr.Write("if ");
            this.Condition.DecompileToText(wr);
            wr.Write(" then ");
            count+=this.Then.DecompileToText(wr);
            if(this.Else != null)
            {
                wr.Write(" else ");
                count += Else.DecompileToText(wr);
            }
            wr.WriteLine();
            return count;
        }
    }
    /// <summary>
    /// All Children of this calss are calls TO this statement so we can find where they all are
    /// </summary>
    public class LabelStatement : AstStatement
    {
        public List<GotoStatement> CallsHere = null;
       
        public override Ast Copy()
        {
            LabelStatement copy = new LabelStatement(this.Instruction,Target);
            return copy;
        }
        public Label Target { get;  set; }
        
        public LabelStatement(Label label) : base() { this.Target = label; }
        public LabelStatement(Instruction i) : base(i) { this.Target = i.Label; }
        public LabelStatement(Instruction i, Label label) : base(i) { this.Target = label; }
        public override int DecompileToText(TextWriter wr)
        {
            wr.Write(this.Target);
            wr.Write(": ");
            if(CallsHere != null) wr.WriteLine("/* Calls to Here: " + CallsHere.Count + " */");
            else wr.WriteLine();
            return 1;
        }
    }
}
