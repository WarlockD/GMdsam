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
    public interface ICopy<T>
    {
        T Copy();
    }
  
    public class Ast : IEquatable<Ast>
    {
        public virtual IEnumerable<Ast> AstEnumerator(bool includeSelf=true) { if (includeSelf) yield return this; }
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
        public virtual Ast Copy()
        {
            throw new Exception("Canoot create Ast copy");
        }
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
        public virtual int DecompileToText(TextWriter wr)
        {
            throw new Exception("Canoot use base cals to decompile");
        }
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
  
    // Made a helper class
    public abstract class AstBinary : Ast
    {
        public override IEnumerable<Ast> AstEnumerator(bool includeSelf = true)
        {
            if (includeSelf) yield return this;
            foreach (var a in Left.AstEnumerator()) yield return a;
            foreach (var a in Right.AstEnumerator()) yield return a;
        }
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
        protected AstUinary(Instruction i, Ast right) : base(i) { Debug.Assert(right != null);  Right = right; ParentSet(Right); }
        protected AstUinary(Ast right) : base() { Debug.Assert(right != null); Right = right;ParentSet(Right); }
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
        public override IEnumerable<Ast> AstEnumerator(bool includeSelf = true)
        {
            if (includeSelf) yield return this;
            foreach (var a in Right.AstEnumerator()) yield return a;
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
            return new AstTree(this.Instruction, this.Op, Left.Copy(), Right.Copy());
        }
        public override Ast Invert() // Never call base.Invert as it will recursive loop for resons
        {
            GMCode io = Op.getInvertedOp();
            Debug.Assert(io != GMCode.BadOp);
            return new AstTree(this.Instruction, io, this.Left.Copy(), this.Right.Copy());
        }
    }
    public class AstCall : Ast, ICopy<Ast>
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
    public class AstArrayAccess : Ast
    {
        public AstVar Variable { get; protected set; }
        public Ast ArrayIndex { get; protected set; }
        public AstArrayAccess(AstVar v, Ast index) :base() {
            Debug.Assert(v != null && index != null);
            Variable = v;
            ArrayIndex = index;
            ParentSet(Variable);
            ParentSet(ArrayIndex);
        }
        ~AstArrayAccess()
        {
            ParentClear(Variable);
            ParentClear(ArrayIndex);
        }
        public override Ast Copy()
        {
            return new AstArrayAccess(this.Variable.Copy() as AstVar,this.ArrayIndex.Copy());
        }
        public override int DecompileToText(TextWriter wr)
        {
            Variable.DecompileToText(wr);
            wr.Write('[');
            ArrayIndex.DecompileToText(wr);
            wr.Write(']');
            return 0;
        }
    }
    public class AstVar : Ast
    {
        public int ExtraData { get; set; }
        public string Name { get; protected set; }
        public GM_Type Type { get { return GM_Type.Var; } }
        public string Instance { get; protected set;  }
        public Ast InstanceValue { get; protected set; }
        public AstVar(Instruction i,  Ast instanceValue, string instance,  string name) : base(i)
        {
            Name = name;
            Instance = instance;
            ExtraData = i.OperandInt;
            InstanceValue = instanceValue;
        }
        public AstVar(Instruction i,  string instance, string name) : base(i)
        {
            Name = name;
            Instance = instance;
            ExtraData = i.OperandInt;
            InstanceValue = null;
        }
        public override Ast Copy()
        {
           return new AstVar(this.Instruction, this.InstanceValue, this.Instance, this.Name);
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
            return 0;
        }
    }
 
}
