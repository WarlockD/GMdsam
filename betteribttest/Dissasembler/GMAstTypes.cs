using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker.Dissasembler
{
    public static class NodeOperations
    {
        public static void DebugSave(this ILBlock block, string MethodName, string FileName)
        {
            using (var w = new Writers.BlockToCode( new Writers.DebugFormater(), FileName))
                w.WriteMethod(MethodName, block);
        }
        public static bool TryParse(this ILValue node, out int value)
        {
            ILValue valueNode = node as ILValue;
            if (valueNode != null && (valueNode.Type == GM_Type.Short || valueNode.Type == GM_Type.Int))
            {
                value = (int) valueNode;
                return true;
            }
            value = 0;
            return false;
        }
    }

    public abstract class ILNode
    {
        public string Comment = null;
        // hack
        public static string EnviromentOverride = null;
        // removed ILList<T>
        // I originaly wanted to make a list class that could handle parent and child nodes automaticly
        // but in the end it was adding to much managment where just a very few parts of the
        // decompiler needed
        public IEnumerable<T> GetSelfAndChildrenRecursive<T>(Func<T, bool> predicate = null) where T : ILNode
        {
            List<T> result = new List<T>(16);
            AccumulateSelfAndChildrenRecursive(result, predicate);
            return result;
        }

        void AccumulateSelfAndChildrenRecursive<T>(List<T> list, Func<T, bool> predicate) where T : ILNode
        {
            // Note: RemoveEndFinally depends on self coming before children
            T thisAsT = this as T;
            if (thisAsT != null && (predicate == null || predicate(thisAsT)))
                list.Add(thisAsT);
            foreach (ILNode node in this.GetChildren())
            {
                if (node != null)
                    node.AccumulateSelfAndChildrenRecursive(list, predicate);
            }
        }
        public virtual IEnumerable<ILNode> GetChildren()
        {
            yield break;
        }
        public override string ToString()
        {
            return Writers.BlockToCode.DebugNodeToString(this);
        }
    }
    public class ILBasicBlock : ILNode
    {
        /// <remarks> Body has to start with a label and end with unconditional control flow </remarks>
        public List<ILNode> Body = new List<ILNode>();
        public override IEnumerable<ILNode> GetChildren()
        {
            return this.Body;
        }
    }
    public class ILBlock : ILNode
    {
        public ILExpression EntryGoto;
        public List<ILNode> Body = new List<ILNode>();
        public ILBlock(params ILNode[] body)
        {
            this.Body = body.ToList();
        }
        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.EntryGoto != null)
                yield return this.EntryGoto;
            foreach (ILNode child in this.Body)
            {
                yield return child;
            }
        }
      
    }
    // We make this a seperate node so that it dosn't interfere with anything else
    // We are not trying to optimize game maker code here:P
    public class ILAssign : ILNode
    {
        public string TextToReplace = null;
        public ILVariable Variable;
        public ILExpression Expression;
        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Variable != null)
                yield return this.Variable;
            if (this.Expression != null)
                yield return this.Expression;
        }
    }

    public class ILCall : ILNode
    {
        public string Name;
        public string Enviroment = null;
        public string FunctionNameOverride = null;
        public string FullTextOverride = null;
        public List<ILExpression> Arguments = new List<ILExpression>();
        public GM_Type Type = GM_Type.NoType; // return type

        public override IEnumerable<ILNode> GetChildren()
        {
            return Arguments;
        }
    }

    public class ILValue : ILNode, IEquatable<ILValue>
    {
        public object Value { get; private set; }
        public GM_Type Type { get; private set; }
        public string ValueText = null;
        public ILValue(bool i) { this.Value = i; Type = GM_Type.Bool; }
        public ILValue(int i) { this.Value = i; Type = GM_Type.Int; }
        public ILValue(object i) { this.Value = i; Type = GM_Type.Var; }
        public ILValue(string i) { this.Value = i; Type = GM_Type.String; this.ValueText = GMCodeUtil.EscapeString(i as string); }
        public ILValue(float i) { this.Value = i; Type = GM_Type.Float; }
        public ILValue(double i) { this.Value = i; Type = GM_Type.Double; }
        public ILValue(long i) { this.Value = i; Type = GM_Type.Long; }
        public ILValue(short i) { this.Value = (int)i; Type = GM_Type.Short; }
        public ILValue(object o, GM_Type type)
        {
            if (o is short) this.Value = (int)((short)o);
            else if (type == GM_Type.String) this.ValueText = GMCodeUtil.EscapeString(o as string);
            else this.Value = o;
            Type = type;
        }
        public ILValue(ILVariable v) { this.Value = v; Type = GM_Type.Var; }
        public ILValue(ILValue v) { this.Value = v.Value; Type = v.Type; this.ValueText = v.ValueText; }
        public ILValue(ILExpression e) { this.Value = e; Type = GM_Type.ConstantExpression; }
        public int? IntValue
        {
            get
            {
                if (Value is int) return (int) Value;
                else return null;
            }
        }
        public override string ToString()
        {
            return ValueText ?? Value.ToString();
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            if (Value.Equals(Value)) return true;
            ILValue test = obj as ILValue;
            return test != null && Equals(test);
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public bool Equals(ILValue other)
        {
            if (object.ReferenceEquals(other, null)) return false;
            if (object.ReferenceEquals(other, this)) return true;
            if (other.Type != Type) return false;
            if (Type == GM_Type.NoType) return true; // consider this null or nothing
            if (Type == GM_Type.ConstantExpression)
            {
                if (object.ReferenceEquals(other.Value, Value)) return true;
                else return other.Value.ToString() == Value.ToString(); // a bit hacky, but true
            }
            else return Value.Equals(other.Value);
        }

        public static explicit operator int(ILValue c)
        {
            switch (c.Type)
            {
                case GM_Type.Short: return ((int)c.Value);
                case GM_Type.Int: return ((int)c.Value);
                default:
                    throw new Exception("Cannot convert type " + c.Type.ToString() + " to int ");
            }
        }
        public static bool operator !=(ILValue c, int v)
        {
            return !(c == v);
        }
        public static bool operator ==(ILValue c, int v)
        {
            switch (c.Type)
            {
                case GM_Type.Short: return ((int)c.Value) == v;
                case GM_Type.Int: return ((int)c.Value) == v;
                default: return false;
            }
        }
    }

    public class ILLabel : ILNode, IEquatable<ILLabel>
    {
        static int generate_count = 0;
        // generates a label, gurntess unique
        public static ILLabel Generate(string name = "G")
        {
            name = string.Format("{0}_L{1}", name, generate_count++);
            return new ILLabel() { Name = name };
        }
        public string Name;
        public Label OldLabel = null;
        public object UserData = null; // usally old dsam label
        public bool isExit = false;
        public bool Equals(ILLabel other)
        {
            return other.Name == Name;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            ILLabel test = obj as ILLabel;
            return test != null && Equals(test);
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
    public class ILVariable : ILNode, IEquatable<ILVariable>
    {
        static int static_gen = 0;
        // generates a variable, gurntees it unique
        public static ILVariable GenerateTemp(string name = "gen")
        {
            name = string.Format("{0}_{1}", name, static_gen++);
            return new ILVariable() { Name = name, isGenerated = true, isArray = false, isResolved = true };
        }
        // hack
        // Side note, we "could" make this a node
        // but in reality this is isolated 
        // Unless I ever get a type/var anyisys system up, its going to stay like this
        public bool isLocal = false; // used when we 100% know self is not used
        public string Name;
        public ILNode Instance = null; // We NEED this, unless its local or generated
        string instance_name = null;

        public string InstanceName
        {
            get {
                if (isLocal || Instance == null) return null;
                Debug.Assert(instance_name != null);
                return instance_name;
            }
            set { instance_name = value; }
        }
        public bool isGenerated = false;
        public ILExpression Index = null; // not null if we have an index
        public bool isArray=false;
        public bool isResolved = false; // resolved expresion, we don't have to do anything to it anymore
        public GM_Type Type = GM_Type.NoType;
        // Returns the full name of the variable, even if its an array as long as the array access is constant
        // This format is near universal, so you can use it anywhere
        public string FullName
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (Instance != null)  {
                    sb.Append((InstanceName ?? Instance.ToString()));
                    sb.Append('.');
                }
                sb.Append(Name);
                if(isArray && Index.Code == GMCode.Constant)
                {
                    sb.Append('[');
                    sb.Append(Index.Operand.ToString());
                    sb.Append(']');
                }
                return sb.ToString();
            }
        }
        public bool isGlobal
        {
            get
            {
                return InstanceName == "global"; // mabye check the instance number.
            }
        }
        public bool isFixedVar {  get
            {
                return Index.Code == GMCode.Constant || (Index.Code == GMCode.Array2D && Index.Arguments[0].Code == GMCode.Constant && Index.Arguments[1].Code == GMCode.Constant);
            }
        }
        public bool Equals(ILVariable obj)
        {
            if (obj.isArray != this.isArray || obj.isLocal != this.isLocal) return false; // easy
            if (isLocal) return obj.Name == Name;
            else return obj.Name == Name && obj.InstanceName == InstanceName;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            ILVariable test = obj as ILVariable;
            return Equals(test);
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
    public struct ILRange
    {
        public readonly int From;
        public readonly int To;   // Exlusive

        public ILRange(int @from, int to)
        {
            this.From = @from;
            this.To = to;
        }

        public override string ToString()
        {
            return string.Format("{0:X2}-{1:X2}", From, To);
        }

        public static List<ILRange> OrderAndJoin(IEnumerable<ILRange> input)
        {
            if (input == null)
                throw new ArgumentNullException("Input is null!");

            List<ILRange> result = new List<ILRange>();
            foreach (ILRange curr in input.OrderBy(r => r.From))
            {
                if (result.Count > 0)
                {
                    // Merge consequtive ranges if possible
                    ILRange last = result[result.Count - 1];
                    if (curr.From <= last.To)
                    {
                        result[result.Count - 1] = new ILRange(last.From, Math.Max(last.To, curr.To));
                        continue;
                    }
                }
                result.Add(curr);
            }
            return result;
        }

        public static List<ILRange> Invert(IEnumerable<ILRange> input, int codeSize)
        {
            if (input == null)
                throw new ArgumentNullException("Input is null!");

            if (codeSize <= 0)
                throw new ArgumentException("Code size must be grater than 0");

            List<ILRange> ordered = OrderAndJoin(input);
            List<ILRange> result = new List<ILRange>(ordered.Count + 1);
            if (ordered.Count == 0)
            {
                result.Add(new ILRange(0, codeSize));
            }
            else {
                // Gap before the first element
                if (ordered.First().From != 0)
                    result.Add(new ILRange(0, ordered.First().From));

                // Gaps between elements
                for (int i = 0; i < ordered.Count - 1; i++)
                    result.Add(new ILRange(ordered[i].To, ordered[i + 1].From));

                // Gap after the last element
                Debug.Assert(ordered.Last().To <= codeSize);
                if (ordered.Last().To != codeSize)
                    result.Add(new ILRange(ordered.Last().To, codeSize));
            }
            return result;
        }
    }

    public class ILExpression : ILNode
    {

        public GMCode Code { get; set; }
        public int Extra { get; set; }
        public object Operand { get; set; }
        public List<ILExpression> Arguments;
        // Mapping to the original instructions (useful for debugging)
        public List<ILRange> ILRanges { get; set; }
        public GM_Type[] Conv = null;
        public GM_Type ExpectedType { get; set; }
        public GM_Type InferredType { get; set; }

        public static readonly object AnyOperand = new object();

        // hacky but it works
        public void Replace(ILExpression i)
        {
            this.Code = i.Code;
            this.Operand = i.Operand; // don't need to worry about this
            this.Arguments = new List<ILExpression>(i.Arguments.Count);
            if (i.Arguments.Count != 0) foreach (var n in i.Arguments) this.Arguments.Add(new ILExpression(n));
            this.ILRanges = new List<ILRange>(i.ILRanges);
            InferredType = i.InferredType;
            ExpectedType = i.ExpectedType;
        }
        public static ILExpression MakeConstant(string s, string valuetext = null)
        {
            ILValue v = new ILValue(s);
            if (valuetext != null) v.ValueText = valuetext;
            return new ILExpression(GMCode.Constant, v);
        }
        public static ILExpression MakeConstant(int i, string valuetext = null)
        {
            ILValue v = new ILValue(i);
            if (valuetext != null) v.ValueText = valuetext;
            return new ILExpression(GMCode.Constant, v);
        }
        // used to make a temp self holder value
        public static ILExpression MakeVariable(string name, bool local = true)
        {
            ILVariable v = new ILVariable() { Name = name, isLocal = local, isResolved = true, isGenerated = true };
            if (!local)
            {
                v.Instance = new ILValue(-1);
                v.InstanceName = "self";
            }
            return new ILExpression(GMCode.Var, v);
        }
        public ILExpression(ILExpression i, List<ILRange> range = null) // copy it
        {
            this.Code = i.Code;
            this.Operand = i.Operand; // don't need to worry about this
            this.Arguments = new List<ILExpression>(i.Arguments.Count);
            if (i.Arguments.Count != 0) foreach (var n in i.Arguments) this.Arguments.Add(new ILExpression(n));
            this.ILRanges = new List<ILRange>(range ?? i.ILRanges);
            InferredType = i.InferredType;
            ExpectedType = i.ExpectedType;
        }
        public ILExpression(GMCode code, object operand, List<ILExpression> args)
        {
            if (operand is ILExpression)
                throw new ArgumentException("operand");

            this.Code = code;
            this.Operand = operand;
            this.Arguments = new List<ILExpression>(args);
            this.ILRanges = new List<ILRange>(1);
            InferredType = GM_Type.NoType;
            ExpectedType = GM_Type.NoType;
        }

        public ILExpression(GMCode code, object operand, params ILExpression[] args)
        {
            if (operand is ILExpression)
                throw new ArgumentException("operand");

            this.Code = code;
            this.Operand = operand;
            this.Arguments = new List<ILExpression>(args);
            this.ILRanges = new List<ILRange>(1);
            InferredType = GM_Type.NoType;
            ExpectedType = GM_Type.NoType;
        }


        public override IEnumerable<ILNode> GetChildren()
        {
            if (Operand is ILValue || Operand is ILVariable || Operand is ILCall) yield return Operand as ILNode;
            foreach (var e in Arguments) yield return e;
        }

        public bool IsBranch()
        {
            return this.Operand is ILLabel || this.Operand is ILLabel[];
        }
        // better preformance

        public IEnumerable<ILLabel> GetBranchTargets()
        {
            if (this.Operand is ILLabel)
            {
                return new ILLabel[] { (ILLabel) this.Operand };
            }
            else if (this.Operand is ILLabel[])
            {
                return (ILLabel[]) this.Operand;
            }
            else
            {
                return new ILLabel[] { };
            }
        }
    }

    public class ILWhileLoop : ILNode
    {
        public ILExpression Condition;
        public ILBlock BodyBlock;

        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Condition != null)
                yield return this.Condition;
            if (this.BodyBlock != null)
                yield return this.BodyBlock;
        }
    }
    // mainly for lua, we chain if statements together to make an ElseIf chain
    public class ILElseIfChain : ILNode
    {
        public List<ILCondition> Conditions = new List<ILCondition>();
        public ILBlock Else = null;
        public override IEnumerable<ILNode> GetChildren()
        {
            foreach (var c in Conditions) yield return c;
            if (Else != null) yield return Else;
        }
    }
    public class ILCondition : ILNode
    {
        public ILExpression Condition;
        public ILBlock TrueBlock;   // Branch was taken
        public ILBlock FalseBlock;  // Fall-though

        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Condition != null)
                yield return this.Condition;
            if (this.TrueBlock != null)
                yield return this.TrueBlock;
            if (this.FalseBlock != null)
                yield return this.FalseBlock;
        }

    }
    // Used as a place holder for the loop switch detection
    public class ILFakeSwitch : ILNode
    {
        public class ILCase : ILNode
        {
            public ILExpression Value = null;
            public ILLabel Goto = null;
            public override IEnumerable<ILNode> GetChildren()
            {
                if (Value != null) yield return this.Value;
                if (Goto != null) yield return this.Goto;
            }
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Value=");
                sb.Append(Writers.BlockToCode.DebugNodeToString(Value));
                sb.Append(" Goto=");
                sb.Append(Writers.BlockToCode.DebugNodeToString(Goto));
                return sb.ToString();
            }
        }
        public ILExpression Condition;      
        public List<ILCase> Cases = new List<ILCase>();
        public override IEnumerable<ILNode> GetChildren()
        {
            if (Condition != null) yield return Condition;
            foreach(var c in Cases) yield return c;
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Condition=");
            sb.Append(Writers.BlockToCode.DebugNodeToString(Condition));
            sb.Append(" Case Count=");
            sb.Append(Cases.Count);
            return sb.ToString();
        }
    }
    public class ILSwitch : ILNode
    {

        public class ILCase : ILBlock
        {
            public List<ILExpression> Values = new List<ILExpression>();
        }
        public ILExpression Condition;
        public List<ILCase> Cases = new List<ILCase>();
        public ILBlock Default;
        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Condition != null) yield return this.Condition;
            foreach(var c in this.Cases) yield return c;
            if (this.Default != null) yield return this.Default;
        }
    }

    public class ILWithStatement : ILNode
    {
        public static int withVars = 0;
        public ILBlock Body = new ILBlock();
        public ILExpression Enviroment;
        public override IEnumerable<ILNode> GetChildren()
        {
            if (Enviroment != null) yield return Enviroment;
            if (this.Body != null) yield return this.Body;
        }
    }


    public class ILTryCatchBlock : ILNode
    {
        public class CatchBlock : ILBlock
        {
            public ILVariable ExceptionVariable;
        }

        public ILBlock TryBlock;
        public List<CatchBlock> CatchBlocks;
        public ILBlock FinallyBlock;
        public ILBlock FaultBlock;

        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.TryBlock != null)
                yield return this.TryBlock;
            foreach (var catchBlock in this.CatchBlocks)
            {
                yield return catchBlock;
            }
            if (this.FaultBlock != null)
                yield return this.FaultBlock;
            if (this.FinallyBlock != null)
                yield return this.FinallyBlock;
        }
    }
}

