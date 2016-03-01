using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections;

namespace betteribttest
{
    using Opcode = GM_Disam.Opcode;
    using OffsetOpcode = GM_Disam.OffsetOpcode;
    interface Visitor
    {
        void visit(Constant node);
        void visit(MathOp node);
        void visit(ASTLabel node);
        void visit(LabelStart node);
        void visit(UntaryOp node);
        void visit(ObjectVariable node);
        void visit(ArrayAccess node);
        void visit(Statements node);
        void visit(Call node);
        void visit(Conditional node);
        void visit(Enviroment node);

        void visit(Conv node);
        void visit(Assign node);
    }
    public enum AstKind
    {
        Invalid=0,
        Constant,
        Variable,
        Label,
        Branch,
        BranchTrue,
        BranchFalse,
        IFStatment,
        ElseStatment,
        Expresson,
        Conditional // Expresson that evals to bool
    }
    // lets make value seperate as well
    class AstValue
    {
        public enum eKind { None, Number, String, Constant }
        public eKind Kind { get; private set; }
        public double ValueI { get; set; }
        public string ValueS { get; set; }
        public AstValue() { Kind = eKind.None; }
        public AstValue(double value) { ValueI = value; Kind = eKind.Number; }
        public AstValue(string value) { ValueS = value; Kind = eKind.String; }
        public AstValue(AstValue value)
        {
            Kind = value.Kind;
            ValueS = value.ValueS;
            ValueI = value.ValueI;
        }
        public override string ToString()
        {
            string str;
            if (Kind == eKind.None) str= "none"; else str = (this.Kind == eKind.Number ? this.ValueI.ToString() : this.ValueS.ToString());
            return String.Format("[ kind={0:G}, value={1}]", Kind, str);
        }
    }
    // I shouldof thought of this a while ago, use children, GAK
    class AST
    {
        public Opcode Opcode { get;  set; }
        public GM_Type Type { get;  set; }
        public AstKind Kind { get; set; }
        public List<AST> Children;
        public AST() { Children = new List<AST>(); Kind = AstKind.Invalid; Opcode = null; }

        /// <summary>
        /// Attemts to find if we have an int somewhere, this is mainly because
        /// 80% of the time we want to know an instance number of some type
        /// </summary>
        public virtual Constant EvalInt() { return null; }
        public virtual void accept(Visitor v) { /* v.visit(this);*/ }
        public bool CanEval(out int value)
        {
            
            Constant c = EvalInt();
            if (c == null)
            {
                value = 0;
                return false;
            } else
            {
                value = (int)c.IValue;
                return true;
            }
        }
    }
    interface TypeInterface
    {
        GM_Type Type { get; }
    }
    interface StringValueInterface
    {
        string Value { get; }
    }
    interface VariableInterface  : TypeInterface, StringValueInterface
    {

    }
    class Constant : AST, VariableInterface, IEquatable<StringValueInterface>
    {
        double dvalue;
        long ivalue;
        string svalue;
        public GM_Type Type { get; protected set; }
        public string Value { get; protected set; }

#if false
        // We don't use this interface anymore for creating vars
        public static Constant CreatePsudoVariable(string str)
        {
            return new Constant(str, GM_Type.Var);
        }
         public static Constant CreateString(string str)
        {
            return new Constant(str, GM_Type.String);
        }
#endif
     
        public double DValue { get { return dvalue; } }
        public long IValue { get { return ivalue; } }
        public string SValue { get { return svalue; } }
        public Constant(Constant value) { ivalue = value.ivalue; Value = value.Value; dvalue = value.DValue ; Type = value.Type; }
        public Constant(Constant value, GM_Type convertTo) {
            Type = convertTo;
            if(value.Type == GM_Type.String && convertTo != GM_Type.String)
            {
                dvalue = double.Parse(value.svalue);
                ivalue = long.Parse(value.svalue);
            } else
            {
                ivalue = value.ivalue;
                Value = value.Value;
                dvalue = value.DValue;
            }
            Value = value.Value;
        }
        public Constant(string value) { dvalue = 0.0d; ivalue = 0 ; value = value.ToString(); Type = GM_Type.String; }
        public Constant(int value)  { dvalue  = ivalue = value; Value = value.ToString(); Type = GM_Type.Int;   }
        public Constant(long value) { dvalue = ivalue = value; Value = value.ToString(); Type = GM_Type.Long;  }
        public Constant(ushort value)  { dvalue = ivalue = value; Value = value.ToString(); Type = GM_Type.Short;  }
        public Constant(float value)  { dvalue = value; ivalue = (long)value; Value = value.ToString(); Type = GM_Type.Float;  }
        public Constant(double value)  { dvalue = value; ivalue = (long)value; Value = value.ToString(); Type = GM_Type.Double; }
        public Constant(bool value) { ivalue = value ? 1 : 0; dvalue = (long)ivalue; Value = value.ToString(); Type = GM_Type.Bool;  }
        public override Constant EvalInt() { return Type == GM_Type.Int || Type == GM_Type.Long || Type == GM_Type.Short ? this : null; }
        public override string ToString()
        {
            return Value;
        }
        public override bool Equals(object obj)
        {
            StringValueInterface test = obj as StringValueInterface;
            if (test == null) return false;
            return Equals(test);
        }
        public bool Equals(StringValueInterface other)
        {
            return this.Value == other.Value;
        }
        public override int GetHashCode()
        {
            return (int)ivalue;
        }
        public override void accept(Visitor v) { v.visit(this); }
    }
    class ObjectVariable : AST, VariableInterface, IEquatable<StringValueInterface>
    {
        public GM_Type Type { get; protected set; }
        public virtual string Value { get { return this.Instance + "." + this.Name; } }
        public string Instance { get; protected set; }
        public string Name { get; protected set; }
        public ObjectVariable(string instance, string name) { this.Instance = instance; this.Name = name;  this.Type = GM_Type.Var; }
        public ObjectVariable(ObjectVariable v, GM_Type convertTo) { this.Instance = v.Instance; this.Name = v.Name; this.Type = convertTo; }
        protected ObjectVariable(ObjectVariable v) { this.Instance = v.Instance; this.Name = v.Name; this.Type = v.Type; }
        public override Constant EvalInt() { return  null; }
        public override void accept(Visitor v) { v.visit(this); }
        public override string ToString()
        {
            return Value;
        }
        public override bool Equals(object obj)
        {
            StringValueInterface test = obj as StringValueInterface;
            if (test == null) return false;
            return Equals(test);
        }
        public bool Equals(StringValueInterface other)
        {
            return this.Value == other.Value; // lot of string compares, meh but this makes it so much easyer for all these abstract classes
        }
        public override int GetHashCode()
        {
            return this.Instance.GetHashCode() & 0xFFF0000 | this.Name.GetHashCode() & 0x0000FFFF;
        }
    }
    class Conv : AST, VariableInterface, IEquatable<StringValueInterface>
    {
        public AST ToConvert { get; private set; }
        public string Value
        {
            get
            {
                return ToConvert.ToString(); // mabey do some fancy converting here?
            }
        }
        public GM_Type Type { get { return To; } }
        public GM_Type From { get; private set; }

        public GM_Type To { get; private set; }
        public Conv(AST ast, GM_Type from, GM_Type to) { this.From = from; this.To = to; this.ToConvert = ast; }
        public override Constant EvalInt()
        {
            Constant eval = ToConvert.EvalInt();
            if (eval != null)
            {
                eval = new Constant(eval, To);
                return eval.EvalInt();
            }
            return null;
        }
        public override bool Equals(object obj)
        {
            StringValueInterface test = obj as Conv;
            if (test == null) return false;
            return Equals(test);
        }
        public bool Equals(StringValueInterface other)
        {
            return this.Value == other.Value;
        }
        public override int GetHashCode()
        {
            return ToConvert.GetHashCode(); // converts get no love
        }
        public override void accept(Visitor v) { v.visit(this); }
    }
    abstract class TreeOp : AST, IEquatable<TreeOp>
    {
        public readonly static Dictionary<GMCode, GMCode> invertOpType = new Dictionary<GMCode, GMCode>()
        {
            { GMCode.Slt, GMCode.Sge },
            { GMCode.Sge, GMCode.Slt },
            { GMCode.Sle, GMCode.Sgt },
            { GMCode.Sgt, GMCode.Sle },
            { GMCode.Sne, GMCode.Seq },
            { GMCode.Seq, GMCode.Sne },
        };
        public readonly static Dictionary<GMCode, string> opMathOperation = new Dictionary<GMCode, string>()  {
            {  (GMCode)0x03, "conv" },
            {  (GMCode)0x04, "*" },
            {  (GMCode)0x05, "/" },
            { (GMCode) 0x06, "rem" },
            {  (GMCode)0x07, "%" },
            {  (GMCode)0x08, "+" },
            {  (GMCode)0x09, "-" },
            {  (GMCode)0x0a, "&" },
            {  (GMCode)0x0b, "|" },
            { (GMCode) 0x0c, "^" },
            { (GMCode) 0x0d, "~" },
            {  (GMCode)0x0e, "!" },

            {  (GMCode)0x0f, "<<" },
            {  (GMCode)0x10, ">>" },
            { (GMCode) 0x11, "<" },
            { (GMCode) 0x12, "<=" },
            { (GMCode) 0x13, "==" },
            {  (GMCode)0x14, "!=" },
            {  (GMCode)0x15, ">=" },
            {  (GMCode)0x16, ">" },
        };
        public GMCode Op { get; protected set; }
        protected TreeOp(GMCode op) { this.Op = op;  }
        public abstract AST Invert();
        public override bool Equals(object obj)
        {
            TreeOp test = obj as TreeOp;
            if (test == null) return false;
            return Equals(test);
        }
        public bool Equals(TreeOp other)
        {
            return this.Op == other.Op;
        }
        public override int GetHashCode()
        {
            return this.Op.GetHashCode();
        }
    }
    class MathOp : TreeOp, IEquatable<MathOp>
    {
        public AST Left { get { return Children[0]; } }
        public AST Right { get { return Children[1]; } }

        public MathOp(AST left, GMCode op, AST right) : base(op) { Children.Add(left); Children.Add(right); }
        public override AST Invert()
        {
            switch (Op)
            {
                case GMCode.Slt: return new MathOp(Left, GMCode.Sge, Right);
                case GMCode.Sge: return new MathOp(Left, GMCode.Slt, Right);
                case GMCode.Sle: return new MathOp(Left, GMCode.Sgt, Right);
                case GMCode.Sgt: return new MathOp(Left, GMCode.Sle, Right);
                case GMCode.Sne: return new MathOp(Left, GMCode.Seq, Right);
                case GMCode.Seq: return new MathOp(Left, GMCode.Sne, Right);
                default:
                    return null;
            }
        }
        public override bool Equals(object obj)
        {
            MathOp test = obj as MathOp;
            if (test == null) return base.Equals(obj);
            return Equals(test);
        }
        public bool Equals(MathOp other)
        {
            return this.Op == other.Op && this.Left.Equals(other.Left) && this.Right.Equals(other.Right);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ this.Left.GetHashCode() ^ this.Right.GetHashCode();
        }
        public override void accept(Visitor v) { v.visit(this); }
        public override string ToString()
        {
            string sop;
            if (opMathOperation.TryGetValue(Op, out sop))
            {
                return "(" + Left.ToString() + " " + sop + " " + Right.ToString() + ")";
            }
            throw new ArgumentException("Cannot find math operation");
        }
    }
    class UntaryOp : TreeOp, IEquatable<UntaryOp>
    {
        public AST Right { get { return Children[0]; } }

        public UntaryOp(GMCode op, AST right) : base(op) { Children.Add( right); }
        public override AST Invert()
        {
            if (Op == GMCode.Not) return Right;
            return null;
        }
        public override string ToString()
        {
            if (Op == GMCode.Neg) return "-(" + Right.ToString() + ")";
            else if (Op == GMCode.Not) return "!(" + Right.ToString() + ")";
            else if (Op == GMCode.Ret) return "return " + Right.ToString();
            else throw new Exception("Bad UnitaryOp To string");
        }
        public override bool Equals(object obj)
        {
            UntaryOp test = obj as UntaryOp;
            if (test == null) return base.Equals(obj);
            return Equals(test);
        }
        public bool Equals(UntaryOp other)
        {
            return this.Op == other.Op && this.Right.Equals(other.Right);
        }
        public override int GetHashCode()
        {
            return this.Op.GetHashCode() ^ this.Right.GetHashCode();
        }
        public override void accept(Visitor v) { v.visit(this); }
    }

    class ArrayAccess : ObjectVariable, IEquatable<ArrayAccess>
    {
        public AST Index { get { return Children[0]; } }
        public override string Value { get { return base.Value + "[" + Index.ToString() + "]"; } }
        public ArrayAccess(ObjectVariable o, AST index) : base(o) {
            Debug.Assert(index != null);
            Children.Add(index);
        }
        public ArrayAccess(string instance, string var_name, AST index) : base(instance, var_name) {
            Debug.Assert(index != null);
            Children.Add(index);
        }
        public override bool Equals(object obj)
        {
            ArrayAccess test = obj as ArrayAccess;
            if (test == null) return base.Equals(obj);
            return Equals(test);
        }
        public bool Equals(ArrayAccess other)
        {
            return base.Equals(other) && other.Index == Index;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ Index.GetHashCode();
        }
        public override void accept(Visitor v) { v.visit(this); }
    }
    class Assign : AST, IEquatable<Assign>
    {
        public AST Object { get { return Children[1]; } }
        public AST Value { get { return Children[1]; } }
        public Assign(AST o, AST value) { Children.Add(o); Children.Add(value); }
        public override string ToString()
        {
            return Object.ToString() + '=' + Value.ToString();
        }
        public override bool Equals(object obj)
        {
            Assign test = obj as Assign;
            if (test == null) return base.Equals(obj);
            return Equals(test);
        }
        public bool Equals(Assign other)
        {
            return Object.Equals(other.Object) && Value.Equals(other.Value);
        }
        public override int GetHashCode()
        {
            return Object.GetHashCode() ^ Value.GetHashCode();
        }
        public override void accept(Visitor v) { v.visit(this); }
    }
    class ASTLabel : AST, IEquatable<ASTLabel>
    {
        public static string FormatWithLabel(ASTLabel label, AST statment)
        {
            return String.Format("{0,-15} {1}", label == null ? "" : label.ToString(), statment == null ? "" : statment.ToString());
        }
        public static string FormatWithLabel(AST statment)
        {
            LabelStart start = statment as LabelStart;
            if (start != null)
                return FormatWithLabel(start, start.Right);
            else return FormatWithLabel(null, statment);
        }
        public int Target { get; private set; }
        public int Offset {  get { return Target - Pc; } }
        public int Pc { get; private set; }
        public ASTLabel(int value, int pc) { this.Target = value; this.Pc = pc; }
        public ASTLabel(ASTLabel label) : this(label.Target, label.Pc) { }
        public override void accept(Visitor v) { v.visit(this); }

        public override int GetHashCode()
        {
            return (Target << 16) | (Pc & 0xFFFF);
        }
        public override bool Equals(object obj)
        {
            ASTLabel l = obj as ASTLabel;
            if (l != null) return Equals(l);
            else return false;
        }
        public bool Equals(ASTLabel other)
        {
            return other.Target == Target;
        }
        public override string ToString()
        {
            return "Label_" + Target + "(" + Pc + ")";
        }
    }
    // this is for if statments and goto statments
    class GotoLabel : ASTLabel,IEquatable<GotoLabel>
    {
        public GotoLabel(ASTLabel value) : base(value) { }
        public override string ToString()
        {
            return "goto " + base.ToString();
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            GotoLabel l = obj as GotoLabel;
            if (l != null) return Equals(l);
            else return base.Equals(obj); // its also equal to a label
        }
        public bool Equals(GotoLabel other)
        {
            return other.Target == Target;
        }
        public override void accept(Visitor v) { v.visit(this); }
    }
    // This is if the statment starts with a label
    class LabelStart : ASTLabel, IEquatable<LabelStart>
    {
        public AST Right { get; private set; }
        public LabelStart(ASTLabel value, AST right) : base(value) { this.Right = right; }
        public override string ToString()
        {
            return base.ToString() + ":";
        }
        public override bool Equals(object obj)
        {
            LabelStart l = obj as LabelStart;
            if (l != null) return Equals(l);
            else return false; // its also equal to a label
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public bool Equals(LabelStart other)
        {
            return other.Target == Target && Right.Equals(Right);
        }
        public override void accept(Visitor v) { v.visit(this); }
    }
        class Conditional : AST
    {
        public AST Condition { get; private set; }
        public AST ifTrue { get; private set; }
        public AST ifFalse { get; private set; }
        public Conditional(AST Condition, AST ifTrue, AST ifFalse = null) 
        {
            this.Condition = Condition;
            this.ifTrue = ifTrue;
            Debug.Assert(this.ifTrue != null);
            this.ifFalse = ifFalse;
        }
        public override string ToString()
        {
            string ret = "if " + Condition.ToString() + " then " + ifTrue.ToString();
            if (ifFalse != null) ret += " else " + ifFalse.ToString();
            return ret;
        }
        // Flip targets to false is useful if your converting the BF to a BT but don't want to rewrite a bunch of code
        public Conditional Invert(bool flipTargets = true)
        {
            AST invertedCondition = null;
            TreeOp uop = Condition as TreeOp;
            if (uop == null) invertedCondition = new UntaryOp(GMCode.Not, Condition); // if its not a condition, throw a Not op in front of it
            else {
                invertedCondition = uop.Invert();
            }
            Debug.Assert(invertedCondition != null); // we shouldn't get here
            return flipTargets ? new Conditional(invertedCondition, ifFalse, ifTrue) : new Conditional(invertedCondition, ifTrue, ifFalse);
        }
        public override void accept(Visitor v) { v.visit(this); }
    }
    class Enviroment : AST
    {
        public string Env { get; private set; }
        public AST Statements { get; private set; }
        public Enviroment(string env, AST statements)  { this.Env = env; this.Statements = statements; }
        public override string ToString()
        {
            Call test = Statements as Call;
            if (test == null) return "using(" + Env + "){ " + Statements + " }";
            else return Env + "." + test.ToString();
        }
        public override void accept(Visitor v) { v.visit(this); }
    }
    class Statements : AST, ICollection<AST>
    {
        List<AST> _statements;
        public ASTLabel Label { get; set; }
        public int Count
        {
            get
            {
                return _statements.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public Statements() { _statements = new List<AST>(); Label = null; }
        public Statements(IEnumerable<AST> en) { _statements = new List<AST>(en); Label = null; }
        public Statements(params AST[] en)  { _statements = new List<AST>(en); Label = null; }
        public override string ToString()
        {
            if (this.Count == 0) return null;
            else if (this.Count == 1) return ASTLabel.FormatWithLabel(Label, this[0]);
            else
            {
                StringBuilder sb = new StringBuilder();
                if (Label != null) { sb.Append(Label.ToString()); sb.Append(":  "); }
                sb.Append(' ', 5);
                sb.AppendLine("{");
                foreach (var o in this)
                {
                    sb.Append(' ', 5);
                    sb.AppendLine(o.ToString());
                }
                sb.Append(' ', 5);
                sb.AppendLine("}");
                return sb.ToString();
            }
        }
        public void Add(AST ast) { _statements.Add(ast); }

        public void Clear()
        {
            _statements.Clear();
        }

        public bool Contains(AST item)
        {
            return _statements.Contains(item);
        }

        public void CopyTo(AST[] array, int arrayIndex)
        {
            _statements.CopyTo(array, arrayIndex);
        }

        public bool Remove(AST item)
        {
            return _statements.Remove(item);
        }

        public IEnumerator<AST> GetEnumerator()
        {
            return _statements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _statements.GetEnumerator();
        }

        public AST this[int i] { get { return _statements[i]; } }
        public override void accept(Visitor v) { v.visit(this); }
    }
    class Call : AST
    {
        Constant constant;

        public string FunctionName { get; private set; }
        public int ArgumentCount { get; private set; }
        public AST[] Arguments { get; private set; }
        public Call(string functionname, params AST[] args) 
        {
            this.FunctionName = functionname;
            ArgumentCount = args.Length;
            Arguments = args;
            constant = null;
            // special case for a constant going though a real function
            if (functionname == "real" && args.Length == 1) {
                Constant c = args[0].EvalInt();
                if (c != null) constant = c;
            }
            // some sepcial cases for evaling
        }
        public override Constant EvalInt()
        {
            return constant;
        }

        public override string ToString()
        {
            string ret = FunctionName + "(";
            if (ArgumentCount > 0)
            {
                for (int i = 0; i < ArgumentCount - 1; i++)
                    ret += Arguments[i].ToString() + ",";
                ret += Arguments.Last();
            }
            ret += ")";
            return ret;
        }
        public override void accept(Visitor v) { v.visit(this); }
    }
}
