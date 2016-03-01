using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Collections;

namespace betteribttest
{
    public abstract class CodeInfo 
    {
        public CodeInfo CopyOf { get; private set; }
        public Label Label { get; set; }
        public int Offset { get; private set; }
        protected CodeInfo(int offset) { Offset = offset; this.Label = null; CopyOf = null; }
        protected CodeInfo(CodeInfo i)
        {
            if (i != null)
            {
                Offset = i.Offset;
                Label = i.Label;
                CopyOf = i;
            }
            else
            {
                Offset = -1;
                this.Label = null;
                CopyOf = null;
            }

        }
        protected abstract bool PrintHeader { get; }

        private const string header_format = "{0:d8} {1,-10}   ";
        private readonly string empty_header_string = string.Format(header_format, "", "");
        public void FormatHeadder(TextWriter wr)
        {
            if (PrintHeader)
                wr.Write(header_format, Offset, Label == null ? "" : Label.ToString());
            else
                wr.Write(empty_header_string);
        }
        public void FormatHeadder(System.CodeDom.Compiler.IndentedTextWriter wr)
        {
            int current_ident = wr.Indent;
            wr.Indent = 0;
            FormatHeadder(wr as TextWriter);
            wr.Indent = current_ident;
        }
        /// <summary>
        /// So this is the main function that decompiles a statment to text.  override this instead of
        /// ToString in ALL inherted functions
        /// </summary>
        /// <param name="indent">Amount of spaces to indent</param>
        /// <param name="sb">String bulder that the line gets added to</param>
        /// <returns>Lenght of line or longest line, NOT the amount of text added</returns>
        public abstract void DecompileToText(TextWriter wr);
        public virtual void DecompileToText(System.CodeDom.Compiler.IndentedTextWriter wr)
        {
            FormatHeadder(wr);
            DecompileToText(wr as TextWriter);
        }
        public virtual void Copy(CodeInfo c) { Offset = c.Offset; Label = c.Label; }
        public override string ToString()
        {
            StringWriter wr = new StringWriter();
            // FormatHeadder(0, sb);
            DecompileToText(wr);
            return wr.ToString();
        }
        public override bool Equals(object that)
        {
            if(object.ReferenceEquals(that, null)) return false;
            if(object.ReferenceEquals(that, this)) return true;
            return false; // unless the refrence equals this thing, its ALWAYS not equal
        }
        public override int GetHashCode()
        {
            return Offset > 0 ? Offset :  Offset ^ base.GetHashCode();
        }
    }
    public abstract class AstClass : CodeInfo
    {
        public List<AstClass> Children { get; private set; }
        protected AstClass(int offset) : base(offset) { Children = new List<AstClass>(); }
        protected AstClass(CodeInfo i) : base(i) { Children = new List<AstClass>(); }
        protected override bool PrintHeader { get { return false; } } 
        public AstClass Invert()
        {
            AstTree tree = this as AstTree;
            if (tree != null && tree.op.getInvertedOp() != GMCode.BadOp)
            {
                AstTree ntree = new AstTree(this, tree.op.getInvertedOp());
                ntree.Children.Add(tree.Children[0]);
                ntree.Children.Add(tree.Children[1]);
                return ntree;
            }
            else {
                AstTree ntree = new AstTree(this, GMCode.Not);
                ntree.Children.Add(this);
                return ntree;
            }
        }
        public override bool Equals(object that)
        {
            if(base.Equals(that)) return true; // if the refrences equal, don't worry about it
            AstClass is_that = that as AstClass;
            if (is_that == null) return false;
            if (is_that.Children.Count != this.Children.Count) return false;
            for (int i = 0; i < this.Children.Count; i++) if(!(this.Children[i].Equals(is_that.Children[i]))) return false;
            return true; // the stars align
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public abstract class AstStatement : CodeInfo
    {
        public AstStatement(int offset) : base(offset) { }
        public AstStatement(CodeInfo i) : base(i) { }
    }
    // This class is used as filler leaf on an invalid stack
    public class AstPop : AstClass
    {
        protected override bool PrintHeader { get { return false; } }
        public AstPop() : base(-1) {  }
        public override bool Equals(object that)
        {
            if (base.Equals(that)) return true; // if the refrences equal, don't worry about it
            if (that is AstPop) return true; // we are just checking types on pop
            else return false;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public override void DecompileToText(TextWriter wr)
        {
            wr.Write("Pop()");
        }
    }
    // This class is used in filler when the stack is an odd shape between labels
    public class PushStatement : AstStatement
    {
        public AstClass ast { get; protected set; }
        protected override bool PrintHeader { get { return true; } }
        public PushStatement(AstClass ast) : base(ast) { this.ast = ast; }
        public override void DecompileToText(TextWriter wr)
        {
            wr.Write("Push(");
            if (ast != null) ast.DecompileToText(wr); else wr.Write("NullPush");
            wr.Write(")");
        }
        public override bool Equals(object that)
        {
            if (base.Equals(that)) return true; // if the refrences equal, don't worry about it
            PushStatement is_that = that as PushStatement;
            if (is_that == null) return false;
            return ast.Equals(is_that.ast);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public abstract class AstRValue : AstClass
    {
        public AstRValue(int offset) : base(offset) { }
        public AstRValue(CodeInfo i) : base(i) { }
        public abstract string Value { get; }
        public GM_Type Type = GM_Type.NoType;
        public override void DecompileToText(TextWriter wr)
        {
            wr.Write(Value.ToString());
        }
        public override bool Equals(object that)
        {
            if (base.Equals(that)) return true; // if the refrences equal, don't worry about it
            AstRValue is_that = that as AstRValue;
            if (is_that == null) return false;
            return Value.Equals(is_that.Value); // GM_Type equality dosn't matter when searching treess
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public class AstTree : AstClass
    {
        public GMCode op;
        public AstTree(int offset, GMCode op) : base(offset) { this.op = op; }
        public AstTree(CodeInfo i, GMCode op) : base(i) { this.op = op; }
        void AddParm(TextWriter wr,string s)
        {
            wr.Write('(');
            wr.Write(s);
            wr.Write(')');
        }
        void WriteChild(TextWriter wr, int index)
        {
            wr.Write(' ');
            bool needParns0 = Children[index] is AstTree;
            if (needParns0) wr.Write('(');
            wr.Write(Children[index].ToString());
            if (needParns0) wr.Write(')');
        }
        public override void DecompileToText(TextWriter wr)
        {
            int count = op.getOpTreeCount();
            if (count == 0 || count != Children.Count) wr.Write("Bad Op '" + op.GetName() + "'");
            else
            {
                string s = op.getOpTreeString();
                if (Children.Count > 1) { WriteChild(wr, 1); wr.Write(' '); }
                wr.Write(s);
                WriteChild(wr, 0);
            }
        }
    }
    public abstract class VarInstance : IEquatable<VarInstance>
    {
        public abstract int ObjectIndex { get; }
        public abstract string ObjectName { get; }
        public override string ToString()
        {
            return ObjectName;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, this)) return true;
            VarInstance i = obj as VarInstance;
            if (i == null) return false;
            return Equals(i);
        }
        public override int GetHashCode()
        {
            return ObjectIndex;
        }
        public bool Equals(VarInstance other)
        {
            return other.ObjectIndex == this.ObjectIndex;
        }
        static VarInstance s_global = new GlobalInstance();
        static VarInstance s_self = new SelfInstance();
        static VarInstance s_builtin = new BuiltInInstance();
        public static VarInstance getInstance(int value)
        {
            if (value == -5) return s_global;
            else if (value == -1) return s_self;
            else if (value == -80) return s_builtin;
            else return new AstInstance(value);
        }
        public static VarInstance getInstance(AstClass ast)
        {
            if (ast is AstConstant)
            {
                // if its a simple constant its simple to do
                int value = Convert.ToInt32((ast as AstConstant).Value);
                return getInstance(value);
            }
            else return new AstInstance(ast);
        }
    }
    public class GlobalInstance : VarInstance
    {
        public override int ObjectIndex { get { return -5; } }
        public override string ObjectName { get { return "global"; } }
    }
    public class SelfInstance : VarInstance
    {
        public override int ObjectIndex { get { return -1; } }
        public override string ObjectName { get { return "self"; } }
    }
    public class BuiltInInstance : VarInstance // this is just for test for right now
    {
        public override int ObjectIndex { get { return -80; } }
        public override string ObjectName { get { return "builtin"; } }
    }
    public class LocalInstance : VarInstance
    {
        public override int ObjectIndex { get { return -7; } }
        public override string ObjectName { get { return "temp"; } }
    }
    public class AstInstance : VarInstance
    {
        int _instance;
        AstClass _ast;
        public override int ObjectIndex { get { return _instance; } }
        public override string ObjectName { get { return _ast.ToString(); } }
        public AstClass ObjectValue { get { return _ast; } }
        public AstInstance(AstClass ast)
        {
            _ast = ast;
            _instance = ObjectName.GetHashCode();
            if (_instance > 0) _instance = -_instance;
        }
        public AstInstance(int instance)
        {
            Debug.Assert(instance > 0);
            _instance = instance;
            _ast = new AstConstant(null, _instance);
        }
    }
    public class AstCall : AstRValue
    {
        public int Arguments { get { return Children.Count; }}
        public string Name { get; protected set; }
        string _name;
        public override string Value
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(_name);
                bool need_comma = false;
                sb.Append('(');
                foreach(var child in Children)
                {
                    if (need_comma) sb.Append(',');
                    else need_comma = true;
                    sb.Append(Children[0].ToString());
                }
                sb.Append(')');
                return sb.ToString();
            }
        }
        public AstCall(CodeInfo i, string name) : base(i) { Name = name; }
    }
    public class AstConstant : AstRValue
    {
        string _value;
        public override string Value { get { return _value; } }
        public AstConstant(CodeInfo i, short s) : base(i) { _value = s.ToString(); Type = GM_Type.Short; }
        public AstConstant(CodeInfo i, double s) : base(i) { _value = s.ToString(); Type = GM_Type.Double; }
        public AstConstant(CodeInfo i, long s) : base(i) { _value = s.ToString(); Type = GM_Type.Long; }
        public AstConstant(CodeInfo i, int s) : base(i) { _value = s.ToString(); Type = GM_Type.Int; }
        public AstConstant(CodeInfo i, float s) : base(i) { _value = s.ToString(); Type = GM_Type.Float; }
        public AstConstant(CodeInfo i, string s) : base(i) { _value = s.ToString(); Type = GM_Type.String; }
        public AstConstant(CodeInfo i, object v, GM_Type type) : base(i) { _value = v.ToString(); Type = type; }
    }
    public class AstVar : AstRValue
    {
        public string Name { get; set; }
        public VarInstance Instance { get; private set; }
        public AstClass ArrayIndex { get { return Children.Count > 1 ? Children[1] : null; } }
        public int VarMetadate = 0;


        public AstVar(CodeInfo i, int instance, string name) : base(i)
        {
            Name = name;
            Type = GM_Type.Var;
            Children.Add(new AstConstant(null, instance));
            Instance = VarInstance.getInstance(Children[0]);
        }
        public AstVar(CodeInfo i, AstClass instance, string name) : base(i)
        {
            Name = name;
            Type = GM_Type.Var;
            Children.Add(instance);
            Instance = VarInstance.getInstance(instance);
        }
        public AstVar(CodeInfo i, int instance, string name, AstClass Index) : this(i, instance, name)
        {
            Children.Add(Index);
        }
        public AstVar(CodeInfo i, AstClass instance, string name, AstClass Index) : this(i, instance, name)
        {
            Children.Add(Index);
        }
        public override string Value
        {
            get
            {
                string ret;
                if (Instance.ObjectIndex == -1 && Name[0] == '%')
                    ret = Name;
                else
                    ret = Instance.ToString() + '.' + Name;
                if (ArrayIndex != null) ret += '[' + ArrayIndex.ToString() + ']';
                return ret;
            }
        }
    }
    public class AssignStatment : AstStatement
    {
        public AstVar Variable = null;
        public AstClass Expression = null;
        protected override bool PrintHeader { get { return true; } }
        public AssignStatment(CodeInfo i) : base(i) { }
        public AssignStatment(int offset) : base(offset) { }
        public override void DecompileToText(TextWriter wr)
        {
            if (Variable != null) Variable.DecompileToText(wr); else wr.Write("NullVariable");
            wr.Write(" = ");
            if (Expression != null)  Expression.DecompileToText(wr); else wr.Write("NullExpression");
        }
        public override bool Equals(object that)
        {
            if (base.Equals(that)) return true; // if the refrences equal, don't worry about it
            AssignStatment is_that = that as AssignStatment;
            if (is_that == null) return false;
            return Variable.Equals(is_that.Variable) && Expression.Equals(is_that.Expression); // GM_Type equality dosn't matter when searching treess
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public class CallStatement : AstStatement
    {
        public AstCall Call { get; protected set; }
        protected override bool PrintHeader { get { return true; } }
        public CallStatement(AstCall call) : base(call)
        {
            Debug.Assert(call != null);
            this.Call = call;
        }
        public override void DecompileToText(TextWriter wr)
        {
            wr.Write("void ");
            if (Call != null) Call.DecompileToText(wr); else wr.Write("NullCall()");
        }
        public override bool Equals(object that)
        {
            if (base.Equals(that)) return true; // if the refrences equal, don't worry about it
            CallStatement is_that = that as CallStatement;
            if (is_that == null) return false;
            return Call.Equals(is_that.Call); // GM_Type equality dosn't matter when searching treess
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public class StatementBlock : AstStatement, ICollection<AstStatement>
    {
        protected override bool PrintHeader { get { return false; } }
        public LinkedList<AstStatement> statements = null;  
        public StatementBlock() : base(null) { statements = new LinkedList<AstStatement>(); }
        public StatementBlock(StatementBlock block) : base(null) {

        }
        public void Copy(StatementBlock c)
        {
            base.Copy(c);
            if (c is StatementBlock) statements = new LinkedList<AstStatement>((c as StatementBlock));
        }
        public override void Copy(CodeInfo c)
        {
            if (c is StatementBlock) Copy(c as StatementBlock); 
            else base.Copy(c);
        }
        public LinkedList<AstStatement> Block {  get { return statements; } }
        public LinkedListNode<AstStatement> First {  get { return statements.First; } }
        public LinkedListNode<AstStatement> Last { get { return statements.Last; } }
        public LinkedList<AstStatement> List { get { return statements; } }
        public int Count
        {
            get
            {
                return statements.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public void Add(AstStatement item)
        {
            statements.AddLast(item);
        }
        public void Add(LinkedListNode<AstStatement> item)
        {
            statements.AddLast(item.Value);
        }
        public void AddTheUnlink(LinkedList<AstStatement> s) {
            while (s.First != null) AddTheUnlink(s.First);
        }
        public void AddTheUnlink(LinkedListNode<AstStatement> s)
        {
            if(s.List != null)
            {
                statements.AddLast(s.Value);
                s.List.Remove(s);
            }
        }
        public void Clear()
        {
            statements.Clear();
        }

        public bool Contains(AstStatement item)
        {
            return statements.Contains(item);
        }
        public bool Contains(LinkedListNode<AstStatement> item)
        {
            return object.ReferenceEquals(item.List,statements) ||   statements.Contains(item.Value);
        }
        public void CopyTo(AstStatement[] array, int arrayIndex)
        {
            statements.CopyTo(array, arrayIndex);
        }

        public override void DecompileToText(TextWriter wr)
        {
            if (statements.Count == 0) wr.Write("{ Empty Statment Block }");
            else if (statements.Count == 1) statements.First.Value.DecompileToText(wr);
            else
            {
                System.CodeDom.Compiler.IndentedTextWriter ident_wr = wr as System.CodeDom.Compiler.IndentedTextWriter;
                if (ident_wr == null) // we are NOT in a statment block so we need to make this
                    ident_wr = new System.CodeDom.Compiler.IndentedTextWriter(wr);
                this.FormatHeadder(ident_wr);
                wr.WriteLine('{');
                ident_wr.Indent++;
                foreach (var statement in statements)
                {
                    statement.DecompileToText(ident_wr);
                    wr.WriteLine();
                }
                ident_wr.Indent--;
                this.FormatHeadder(ident_wr);
                wr.WriteLine('}');
            }
        }
        public IEnumerator<AstStatement> GetEnumerator()
        {
            return statements.GetEnumerator();
        }

        public bool Remove(AstStatement item)
        {
            return statements.Remove(item);
        }
        public bool Remove(LinkedListNode<AstStatement> item)
        {
            if (!Contains(item)) throw new ArgumentOutOfRangeException("Not in statment list");
            statements.Remove(item);
            return true;
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public override bool Equals(object that)
        {
            if (base.Equals(that)) return true; // if the refrences equal, don't worry about it
            StatementBlock is_that = that as StatementBlock;
            if (is_that == null) return false;
            if (is_that.Count != is_that.Count) return false;
            var start = this.First;
            var start2 = is_that.First;
            while (start != null)
            {
                if (!start.Value.Equals(start2.Value)) return false;
                start = start.Next;
                start2 = start2.Next;
            }
            return true; // ugh.  Mabey I should make a single link list chains for statments?
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    // Used for decoding other statements
    public class IfStatement : AstStatement
    {
        protected override bool PrintHeader {  get { return true; } }
        public AstClass Expression { get; set; }
        public AstStatement Statement { get;  set; }
        public IfStatement(CodeInfo info) : base(info) { Statement = null; Expression = null; }
        public override void DecompileToText(TextWriter wr)
        {
            
            if (Expression != null) {
                wr.Write(" if "); 
                Expression.DecompileToText(wr);
                wr.Write(" then ");
            } else {
                wr.Write("goto ");
            }
            if (Statement != null)
                Statement.DecompileToText(wr);
            else if (this.Label != null) /// HAACK
            {
                wr.Write(this.Label.ToString());
            }
            else wr.Write("NullStatement");
        }
        public override bool Equals(object that)
        {
            if (base.Equals(that)) return true; // if the refrences equal, don't worry about it
            IfStatement is_that = that as IfStatement;
            if (is_that == null) return false;
            return Expression.Equals(is_that.Expression) && Statement.Equals(is_that.Statement);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public class LabelStatement : AstStatement
    {
        protected override bool PrintHeader { get { return true; } }
        public StatementBlock block;
        public LabelStatement(Label label) : base(label.Target) { this.Label = label; block = null; }

        public override void DecompileToText(TextWriter wr)
        {
            wr.Write(this.Label);
            wr.WriteLine(": ");
            if (block != null) block.DecompileToText(wr);

        }
        public override bool Equals(object that)
        {
            if (base.Equals(that)) return true; // if the refrences equal, don't worry about it
            LabelStatement is_that = that as LabelStatement;
            if (is_that == null) return false;
            return this.Label.Equals(is_that.Label) && block.Equals(is_that.block);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public class DecompilerNew
    {
     
        public string ScriptName { get; private set; }
        public Instruction.Instructions Instructions { get; private set; }
        public List<string> StringIndex { get; set; }
        public List<string> InstanceList { get; set; }
        string FindInstance(int index)
        {
            Debug.Assert(index > 0);
            if (InstanceList != null && index < InstanceList.Count) return InstanceList[index];
            else return "Object(" + index + ")";
        }
        class VarInfo
        {
            public HashSet<string> ScriptsSeenAssignedIn = new HashSet<string>();
            public HashSet<string> ScriptesAccessedFrom = new HashSet<string>();
            public void SeenInScript(string scriptName)
            {
                ScriptesAccessedFrom.Add(scriptName);
            }
            public void AssignedInScript(string scriptName)
            {
                SeenInScript(scriptName);
                ScriptsSeenAssignedIn.Add(scriptName);
            }
            public string Name { get; private set; }
            public string Instance { get; private set; }
            public bool IsArray { get; private set; }
            int _index;
            public int MaxArraySizeSeen
            {
                get { return _index; }
                set
                {
                    if (!IsArray) throw new Exception("NOT AN ARRAY");
                    if (value > _index) _index = value;
                }
            }
            public HashSet<string> ScriptsAssignedIn = new HashSet<string>();
            public HashSet<string> ScriptsReadIn = new HashSet<string>();
            public VarInfo(string name, string instance) { Name = name; Instance = instance; IsArray = false; _index = 0; }
            public VarInfo(string name, string instance, int index) { Name = name; Instance = instance; IsArray = true; _index = index; }
            public override string ToString()
            {
                if (IsArray) return Instance + "." + Name + '(' + MaxArraySizeSeen + ')'; else return Instance + "." + Name;
            }
        }
        Dictionary<string, VarInfo> GlobalsInFilenames = new Dictionary<string, VarInfo>();
        public void SawGlobal(string var_name)
        {
            VarInfo info;
            if (!GlobalsInFilenames.TryGetValue(var_name, out info)) GlobalsInFilenames[var_name] = info = new VarInfo(var_name, "global");
            info.SeenInScript(ScriptName);
        }
        public void SawGlobal(string var_name, int index)
        {
            VarInfo info;
            if (!GlobalsInFilenames.TryGetValue(var_name, out info)) GlobalsInFilenames[var_name] = info = new VarInfo(var_name, "global", index);
            info.SeenInScript(ScriptName);
            info.MaxArraySizeSeen = index;
        }
        public void AssignedGlobal(string var_name, int index)
        {
            VarInfo info;
            if (!GlobalsInFilenames.TryGetValue(var_name, out info)) GlobalsInFilenames[var_name] = info = new VarInfo(var_name, "global", index);
            info.SeenInScript(ScriptName);
            info.MaxArraySizeSeen = index;
        }
        public void AssignedGlobal(string var_name)
        {
            VarInfo info;
            if (!GlobalsInFilenames.TryGetValue(var_name, out info)) GlobalsInFilenames[var_name] = info = new VarInfo(var_name, "global");
            info.SeenInScript(ScriptName);
        }

        // I could just pass a generic stream but if I am using BinaryReader eveywhere, might as well pass it
        static bool vPushIsStackAndArray(Instruction x) { return x.GMCode == GMCode.Push && x.FirstType == GM_Type.Var && x.Instance == 0 && GMCodeUtil.IsArrayPushPop(x.OpCode); }

        static bool ePushPredicate(Instruction x) { return x.GMCode == GMCode.Push && x.FirstType == GM_Type.Short; }
        static bool ePushPredicateValue(Instruction x, int v) { return x.GMCode == GMCode.Push && x.FirstType == GM_Type.Short && x.Instance == v; }
        string getConditionalString(Instruction i, bool invert = false)
        {
            switch (i.GMCode)
            {
                case GMCode.Seq: return invert ? "!=" : "==";
                case GMCode.Sne: return invert ? "==" : "!=";
                case GMCode.Sge: return invert ? "<" : ">=";
                case GMCode.Sle: return invert ? ">" : "<=";
                case GMCode.Sgt: return invert ? "<=" : ">";
                case GMCode.Slt: return invert ? ">=" : "<";
            }
            throw new Exception("something");
        }
        // from what I have dug down, all streight assign statments are simple and the compiler dosn't do any
        // wierd things like branching with uneven stack values unless its in loops, so if we find all the assigns
        // we make life alot easyer down the road
        AstRValue DoConstant(LinkedListNode<Instruction> node)
        {
            // evaluate a constant, we KNOW the instruction comming in is a constant
            Instruction i = node.Value;
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
            node.List.Remove(node); // we remove this node
            return con;
        }

        AstVar DoRValueComplex(Stack<AstClass> stack, LinkedListNode<Instruction> node) // instance is  0 and may be an array
        {
            Instruction i = node.Value;
            string var_name = i.Operand as string;
            Debug.Assert(i.Instance == 0);
            // since we know the instance is 0, we hae to look up a stack value
            Debug.Assert(stack.Count > 1);
            AstVar ret = null;
            if (i.OperandInt > 0) // if we are an array
            {
                //AstRValue indexAst = Expression(node.Previous); // the node should be removed
                AstClass index = stack.Pop();
                ret = new AstVar(i, stack.Pop(), var_name, index);
            }
            else ret = new AstVar(i, stack.Pop(), var_name);
            return ret;
        }
        AstVar DoRValueSimple(LinkedListNode<Instruction> node) // instance is != 0 or and not an array
        {
            Instruction i = node.Value;
            Debug.Assert(i.Instance != 0);
            AstVar v = null;
            // Here is where it gets wierd.  iOperandInt could have two valus 
            // I think 0xA0000000 means its local and
            // | 0x4000000 means something too, not sure  however I am 50% sure it means its an object local? Room local, something fuky goes on with the object ids at this point
            if ((i.OperandInt & 0xA0000000) != 0) // this is always true here
            {

                if ((i.OperandInt & 0x40000000) != 0)
                {
                    // Debug.Assert(i.Instance != -1); // built in self?
                    v = new AstVar(i, i.Instance, i.Operand as string);
                }
                else {
                    v = new AstVar(i, i.Instance, i.Operand as string);
                }
                return v;
            }
            else throw new Exception("UGH check this");
        }
        AstVar DoRValueVar(Stack<AstClass> stack, LinkedListNode<Instruction> node, bool remove_node)
        {
            Instruction i = node.Value;
            AstVar v = null;
            if (i.Instance != 0) v = DoRValueSimple(node);
            else v = DoRValueComplex(stack,node);
            if (remove_node) node.List.Remove(node); // remove this node
            return v;
        }
        AstRValue DoPush(Stack<AstClass> stack, LinkedListNode<Instruction> node)
        {
            Instruction i = node.Value;
            if (i.FirstType == GM_Type.Var) return DoRValueVar(stack,node, true);
            else return DoConstant(node);
        }
        AstCall DoCallRValue(Stack<AstClass> stack, LinkedListNode<Instruction> node)
        {
            Instruction i = node.Value;
            int arguments = i.Instance;
            AstCall call = new AstCall(i, i.Operand as string);
            for (int a = 0; a < arguments; a++) call.Children.Add(stack.Pop());
            node.List.Remove(node);
            return call;
        }
        AstStatement DoAssignStatment(Stack<AstClass> stack, LinkedListNode<Instruction> node)
        {
            Instruction i = node.Value;
            AssignStatment assign = new AssignStatment(i);
            if (stack.Count < 1) throw new Exception("Stack size issue");
            assign.Variable = DoRValueVar(stack,node, false);
            Debug.Assert(assign.Variable != null);
            assign.Expression = stack.Pop();
            node.List.Remove(node);
            return assign;
        }
        // convert into general statments and find each group of statements between labels
        KeyValuePair<Label, LinkedList<Instruction>> FindLeaf(LinkedList<Instruction> nodes)
        {
            LinkedList<Instruction> list = new LinkedList<Instruction>();
            Label l = nodes.First.Value.Label;
            nodes.First.Value.Label = null;
            while (nodes.First != null)
            {
                Instruction i = nodes.First.Value;
                if (i.Label != null) break;
                list.AddLast(i);
                nodes.RemoveFirst();
            }
            return new KeyValuePair<Label, LinkedList<Instruction>>(l, list);
        }
        StatementBlock DoBranchDecode(LinkedList<Instruction> list)
        {
            
            SortedList<Label, LinkedList<Instruction>> iblocks = new SortedList<Label, LinkedList<Instruction>>();
            if (list.First.Value.Label == null) // fix the label in case we don't have a starting label
            {
                Instruction i = list.First.Value;
                if (i.Offset != 0) throw new Exception("Expected no label on offset 0");
                i.Label = new Label(i.Offset);
            }
            while (list.First != null)
            {
                var leaf = FindLeaf(list);
                iblocks.Add(leaf.Key, leaf.Value);
            }
            Stack<AstClass> stack = new Stack<AstClass>();
            StatementBlock testAll = new StatementBlock();
            SortedList<Label, LinkedList<Instruction>> blocks = new SortedList<Label, LinkedList<Instruction>>();
            SortedList<Label, StatementBlock> resolvedblocks = new SortedList<Label, StatementBlock>();
            SortedList<Label, StatementBlock> unresolvedBlocks = new SortedList<Label, StatementBlock>();
            SortedList<Label, Stack<AstClass>> blockState = new SortedList<Label, Stack<AstClass>>();

            // we are just printing them all here


            foreach (var k in iblocks)
            {
                LinkedList<Instruction> blist = k.Value;
                Label l = k.Key;
                LabelStatement lstatement = new LabelStatement(l);
                testAll.Add(lstatement);     // we insert the label here
               
                StatementBlock block = new StatementBlock();
                foreach(var statement in DoStatements(stack,blist)) block.Add(statement);

                lstatement.block = block;
                Debug.WriteLine("Label: {0} Stack Size is = {1}", k.Key, stack.Count);
#if DEBUG
                using (StreamWriter debug_writer = new StreamWriter("temp_statement.txt"))
                {
                    testAll.DecompileToText(debug_writer);
                }
#endif
                if (stack.Count ==0)
                {
                    // its been resolved, all the statements work
                    resolvedblocks.Add(l, block);
                    blockState.Add(l, stack);
                    stack = new Stack<AstClass>();
                } else  // we have a funkey branch we have to correct or something, the stack isn't adding up.  Might be an else
                {
                    unresolvedBlocks.Add(l, block);
                    blockState.Add(l, stack);
                    stack = new Stack<AstClass>();
                    // Debug.Assert(l.CallsTo.Count == 1);
                }

            }

            return testAll;
        }
        
        StatementBlock DoStatements(Stack<AstClass> stack, LinkedList<Instruction> list)
        {
            Instruction last = null;
            StatementBlock block = new StatementBlock();
            while (list.First != null)
            {
#if DEBUG
                using (StreamWriter debug_writer = new StreamWriter("temp_statement.txt"))
                {
                    block.DecompileToText(debug_writer);
                }
#endif
                LinkedListNode<Instruction> node = list.First;
                Instruction i = node.Value;
                Debug.Assert(!object.ReferenceEquals(last, i)); // this shouldn't happen
                last = i;
                int count = i.GMCode.getOpTreeCount(); // not a leaf
                if (count != 0)
                {
                    if (count > stack.Count) throw new Exception("Stack issue");
                    AstTree ast = new AstTree(i, i.GMCode);
                    while (count-- != 0) { ast.Children.Add(stack.Pop()); }
                    stack.Push(ast);
                    node.List.Remove(node);
                }
                else
                {
                    AstStatement ret = null;
                    switch (i.GMCode)
                    {
                        case GMCode.Push:
                            stack.Push(DoPush(stack,node));
                            break;
                        case GMCode.Call:
                            stack.Push(DoCallRValue(stack, node));
                            break;
                        case GMCode.Popz:   // the call is now a statlemtn
                            node.List.Remove(node);
                            block.Add(new CallStatement(stack.Pop() as AstCall));
                            break;
                        case GMCode.Pop:
                            block.Add(DoAssignStatment(stack,node));// assign statment
                            break;
                        case GMCode.B: // this is where the magic happens...woooooooooo
                            {
                                list.Remove(node); // remove it so we don't have to worry about it
                                // be sure the stack is cleared on EACH JUMP
                                if (stack.Count > 0) foreach (var s in stack) block.Add(new PushStatement(s));
                                IfStatement ifs = ret as IfStatement;
                                if (ifs == null) ifs = new IfStatement(i); // just a goto
                                ifs.Label = i.Operand as Label;
                                block.Add(ifs);
                            }
                            break;
                        case GMCode.Bf:
                        case GMCode.Bt:
                            {
                                Debug.Assert(stack.Count > 0);
                                IfStatement ifs = new IfStatement(i);
                                ifs.Expression = i.GMCode == GMCode.Bf ? stack.Pop().Invert() : stack.Pop();
                                ret = ifs;
                            }
                            goto case GMCode.B;
                        case GMCode.BadOp:
                            node.List.Remove(node);
                            break; // skip those
                        default:
                            throw new Exception("Not Implmented! ugh");
                    }
                }
            }
            return block;
        }

        public void RemoveAllConv(Instruction.Instructions instructions)
        {
#if DEBUG
            // if we got a label on a conv, something is funky
            var e = instructions.Where(x => x.GMCode == GMCode.Conv && x.Label != null).ToList();
            Debug.Assert(e != null);
#endif
            instructions.RemoveAll(x => x.GMCode == GMCode.Conv);
        }
        public void SaveOutput(ICollection<AstStatement> statements, string filename)
        {
            using (System.IO.StreamWriter tw = new System.IO.StreamWriter(filename))
            {
                foreach (var s in statements)
                {
                    //  s.FormatHeadder(0);
                    s.DecompileToText(tw);
                    tw.WriteLine(); 
                }
            }
        }
        public void SaveOutput(StatementBlock block, string filename)
        {
            using (System.IO.StreamWriter tw = new System.IO.StreamWriter(filename))
            {
                block.DecompileToText(tw);
            }
        }
        public void Disasemble(string scriptName, BinaryReader r, List<string> StringIndex)
        {
            // test
            this.ScriptName = scriptName;
            this.StringIndex = StringIndex;
            var instructions = Instruction.Create(r, StringIndex);
            List<AstStatement> statements = new List<AstStatement>();
            RemoveAllConv(instructions); // mabye keep if we want to find types of globals and selfs but you can guess alot from context
            foreach (var i in instructions) statements.Add(i);
            SaveOutput(statements, scriptName + "_original.txt");
            Stack<AstClass> stack = new Stack<AstClass>();
            StatementBlock decoded = DoStatements(stack,instructions); // DoBranchDecode(instructions);


            SaveOutput(decoded, scriptName + "_forreals.txt");
        }
    }
}
