using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.Dissasembler;
using System.Collections.Concurrent;

namespace GameMaker.Ast
{
    public static class NodeOperations
    {
      
        public static bool Append<T>(this StringBuilder sb, T node, int ident) where T : ILNode
        {
            if (node != null) return node.ToStringBuilder(sb, ident);
            sb.Append("?null?");
            return false;
        }
        public static bool Append<T>(this StringBuilder sb, T node) where T : ILNode
        {
            if (node != null) return node.ToStringBuilder(sb, 0);
            sb.Append("?null?");
            return false;
        }
        public static void AppendLines<T>(this StringBuilder sb, IEnumerable<T> body, int ident) where T : ILNode
        {
            ident++;
            foreach (var n in body)
            {
                sb.Ident(ident);
                if (!n.ToStringBuilder(sb, ident)) sb.AppendLine();
            }
            ident--;
        }
        public static List<ILRange> JoinILRangesFromTail(this ILBasicBlock block, int count)
        {
            List<ILRange> ranges = new List<ILRange>();
            int i = block.Body.Count - 1;
            while (count > 0)
            {
                ILExpression e = block.Body[i] as ILExpression;
                if (e != null) ranges.AddRange(e.ILRanges);
                i--;
                count--;
            }
            ILRange.OrderAndJoin(ranges);
            return ranges;
        }
        public static void AppendBlock(this StringBuilder sb, ILBlock block, int ident)
        {
            if (block == null || block.Body.Count == 0) sb.AppendLine("{}");
            else
            {
                sb.AppendLine("{");
                sb.AppendLines(block.Body, ident);
                sb.Ident(ident);
                sb.AppendLine("}");
            }

        }
        public static bool AppendArguments(this StringBuilder sb, IEnumerable<string> strings)
        {
            bool need_comma = false;
            foreach (var s in strings)
            {
                if (need_comma) sb.Append(',');
                else need_comma = true;
                sb.Append(s);
            }
            return need_comma;
        }
        public static bool AppendArguments<T>(this StringBuilder sb, IEnumerable<T> nodes) where T : ILNode
        {
            bool need_comma = false;
            foreach (var n in nodes)
            {
                if (need_comma) sb.Append(',');
                else need_comma = true;
                n.ToStringBuilder(sb, 0);
            }
            return false;
        }
        public static void DebugSave(this ILBlock block, string FileName)
        {
            using (var output = new Writers.BlockToCode(FileName))
                output.Write(block);
        }

    }

    public abstract class ILNode
    {
        public abstract bool hasChildren { get; }
        public string Comment = null;
        ILNode _parent = null;
        ILNode _next = null;
        public ILNode Parent { get { return _parent; } }
        public ILNode Next { get { return _next; } }
        public IEnumerable<ILNode> GetParents()
        {
            ILNode current = this;
            while (true)
            {
                current = current._parent;
                if (current == null)
                    yield break;
                yield return current;
            }
        }
        public void ClearAndSetAllParents(bool skipVarAndConstants = true)
        {
            List<ILNode> nodes = this.GetSelfAndChildrenRecursive<ILNode>().ToList(); // cause we do this twice
            foreach (var n in nodes) n._parent = n._next = null;
            foreach (ILNode node in nodes)
            {
                ILNode previousChild = null;
                foreach (ILNode child in node.GetChildren())
                {
                    if (skipVarAndConstants && (child is ILValue || child is ILVariable)) continue; // we want to skip these.
                    if (child._parent != null)
                    {
                        throw new Exception("The following expression is linked from several locations: " + child.ToString());
                    }
                    child._parent = node;
                    if (previousChild != null) previousChild._next = child;
                    previousChild = child;

                }
                if (previousChild != null) previousChild._next = null;
            }
        }
        // hack
        public static string EnviromentOverride = null;
        // After HOURS of tryign diffrent algorythms using tasks/threads
        // there is no way to optimize this.  To be frank, we don't have enough nodes
        // to really optimize it that much except for doing a first
        //trasversal Parrell task
        public static ConcurrentBag<TimeChecker> times = new ConcurrentBag<TimeChecker>();
        public class TimeChecker
        {
            public TimeSpan Time;
            public int Count;
        }
        public IEnumerable<T> GetSelfAndChildrenRecursive<T>() where T : ILNode
        {
            List<T> result = new List<T>(16);  /// standard
            var start = DateTime.Now;
            AccumulateSelfAndChildrenRecursive(result);
            var end = DateTime.Now;
            if (result.Count > 5000) times.Add(new TimeChecker() { Time = end -start , Count = result.Count });
            return result;
        }

        public IEnumerable<T> GetSelfAndChildrenRecursive<T>(Func<T, bool> predicate) where T : ILNode
        {
            if (predicate == null) throw new ArgumentNullException("predicate");
            List<T> result = new List<T>(16);
            AccumulateSelfAndChildrenRecursive(result, predicate);
            return result;
        }

        void AccumulateSelfAndChildrenRecursive<T>(List<T> list) where T : ILNode
        {
            T thisAsT = this as T;
            if (thisAsT != null) list.Add(thisAsT);
            foreach (ILNode node in this.GetChildren())
            {
                if (node != null) node.AccumulateSelfAndChildrenRecursive(list);
            }
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
        // returns true when ending with a new line
        public abstract bool ToStringBuilder(StringBuilder sb,int ident);
       
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            ToStringBuilder(sb,0);
            return sb.ToString();
        }
        // I KNEW SOMEDAY I would have to implment this, I just should of done it earlyer
        // Only bother with this on expresions for now
        protected virtual bool InternalTreeEqual(ILNode node)
        {
            return false;
        }
        public bool TreeEqual(ILNode node)
        {
            if (object.ReferenceEquals(node, null)) return false;
            if (object.ReferenceEquals(node, this)) return true; // simple cases, no node should be the same though
            return InternalTreeEqual(node);
        }
    }
    public class ILBasicBlock : ILNode
    {
        /// <remarks> Body has to start with a label and end with unconditional control flow </remarks>
        public List<ILNode> Body = new List<ILNode>();
        public override bool hasChildren { get { return Body.Count > 0; } }
        public override IEnumerable<ILNode> GetChildren()
        {
            return this.Body;
        }
        public override bool ToStringBuilder(StringBuilder sb,int ident)
        {
            sb.AppendLine("ILBasicBlock: ");
            sb.AppendLines(Body, ident);
            return true;
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
        public override bool hasChildren { get { return Body.Count > 0; } }
        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.EntryGoto != null)
                yield return this.EntryGoto;
            foreach (ILNode child in this.Body)
            {
                yield return child;
            }
        }
        public void DebugSaveFile(string filename)
        {
            this.DebugSave(filename);
        }
        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            sb.Append("ILBlock ");
            sb.Append("EntryGoto=");
            sb.Append(EntryGoto);
            sb.AppendLine(": ");
            sb.AppendBlock(this,ident);
            return true;
        }
    }
  
    public class ILCall : ILNode
    {
        public string Name;
        public string Enviroment = null;
        public string FunctionNameOverride = null;
        public string FullTextOverride = null;
        public List<ILExpression> Arguments = new List<ILExpression>();
        public override bool hasChildren { get { return Arguments.Count > 0; } }
        public GM_Type Type = GM_Type.NoType; // return type
        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            sb.Append("(");
            sb.Append("Name=");
            sb.Append(Name);
            sb.Append(" Arguments=");
            sb.AppendArguments(Arguments);
            sb.Append(" )");
            return false;
        }
        public override IEnumerable<ILNode> GetChildren()
        {
            return Arguments;
        }
        protected override bool InternalTreeEqual(ILNode node)
        {
            ILCall call = node as ILCall;
            if (call == null) return false;
            if (call.Arguments.Count != Arguments.Count) return false;
            for (int i = 0; i < call.Arguments.Count; i++)
                if (!Arguments[i].TreeEqual(call.Arguments[i])) return false;
            return true;
        }
    }

    public class ILValue : ILNode, IEquatable<ILValue>, IComparable<ILValue>
    {
        public override bool hasChildren { get { return false; } }
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
        public int? IntValue
        {
            get
            {
                if (Value is int) return (int) Value;
                else return null;
            }
        }
        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
         
            if (Value is ILNode) ((ILNode)Value).ToStringBuilder(sb, ident);
            else if (Value.GetType().IsPrimitive) sb.Append(Value.ToString());
            else if (Value is string) sb.EscapeAndAppend(Value as string);
            else
            {
                sb.Append("(");
                sb.Append("Type=");
                sb.Append(Value.GetType().ToString());
                sb.Append(' ');
                sb.Append(Value.ToString());
                sb.Append(')');
            }
          
            return false;
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
            else return Value.Equals(other.Value);
        }
        protected override bool InternalTreeEqual(ILNode node)
        {
            ILValue v = node as ILValue;
            if (v == null) return false;
            if (v.Type != this.Type) return false;
            return v.Value.Equals(Value);
        }
        public int CompareTo(ILValue other)
        {
            // all the operands are comparatlable to one another
            return ((IComparable)Value).CompareTo(other.Value);
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
        public override bool hasChildren { get { return false; } }
        static int generate_count = 0;
        // generates a label, gurntess unique
        public static ILLabel Generate(string name = "G")
        {
            return Generate(name, generate_count++);
        }
        public static ILLabel Generate(string name, int labelIndex)
        {
            name = string.Format("{0}_{1}", name, labelIndex);
            return new ILLabel(name);
        }
        public readonly string Name;
        public readonly int Offset;
        public ILLabel(int offset)
        {
            this.Name = "L" + offset;
            this.Offset = offset;
        }
        ILLabel(string name)
        {
            this.Offset = -1;
            this.Name = name;
        }
        public bool Equals(ILLabel other)
        {
            if (this.Offset != -1 && this.Offset == other.Offset) return true;
            if(this.Offset != other.Offset) return false;
            return Offset == -1 && other.Name == this.Name;
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
            return this.Offset == -1 ? this.Name.GetHashCode() : this.Offset;
        }

        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            sb.Append(":");
            sb.Append(Name);
            sb.Append(":");
            return false;
        }
    }
    public class ILVariable : ILNode, IEquatable<ILVariable>
    {
        public override bool hasChildren { get { return Index != null || _instance == 0; } }
        public override IEnumerable<ILNode> GetChildren()
        {
            if(!(Instance is ILValue)) yield return Instance;
            if (Index != null)
            {
                if (Index.Code == GMCode.Array2D)
                {
                    yield return Index.Arguments[0];
                    yield return Index.Arguments[1]; // special case, meh.  I need to shove this in Expressions the more I think about it
                }
                else
                    yield return Index;
            }   
        }
        static int static_gen = 0;
        // generates a variable, gurntees it unique
        public static ILVariable GenerateTemp(string name = "gen")
        {
            name = string.Format("{0}_{1}", name, static_gen++);
            var v =  new ILVariable(name, -1);
            Debug.Assert(v.isResolved);
            v.isLocal = true;
            return v;
        }
        // hack
        // Side note, we "could" make this a node
        // but in reality this is isolated 
        // Unless I ever get a type/var anyisys system up, its going to stay like this
        public bool isLocal = false; // used when we 100% know self is not used
        public string Name;
        
        int _instance;
        ILNode _instance_node = null; // We NEED this, unless its local or generated
        public ILNode Instance
        {
            get { return _instance_node; }
            set
            { // simplify 
                ILExpression e = value as ILExpression;
                if (e != null && (e.Code == GMCode.Constant || e.Code == GMCode.Var))
                    _instance_node = e.Operand as ILNode;
                else
                    _instance_node = value;
                ILValue v = _instance_node as ILValue;
                if (v != null) _instance = (int)v;
            }
        }
        public string InstanceName
        {
            get
            {
                if (isLocal) return null;
                StringBuilder sb = new StringBuilder();
                if (_instance != 0)
                    sb.Append(Context.InstanceToString(_instance));
                else
                    Instance.ToStringBuilder(sb, 0);
                return sb.ToString();
            }
        }
        public ILVariable(string name, int instance, bool isarray = false)
        {
            Debug.Assert(name != null);
            this._instance = instance;
            this._instance_node = new ILValue(instance);
            this.isResolved = !isarray && instance != 0;
            this.Name = name;
            this.Index = null;
            this.isArray = isarray;
        }
        ILVariable()
        {

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
                if (!isLocal) {
                    if (_instance != 0)
                        sb.Append(Context.InstanceToString(_instance));
                    else
                        Instance.ToStringBuilder(sb, 0);
                    sb.Append('.');
                }
                sb.Append(Name);
                if(isArray)
                {
                    sb.Append('[');
                    if (Index != null && Index.Code == GMCode.Constant) Index.ToStringBuilder(sb,0);    
                    sb.Append(']');
                }
                return sb.ToString();
            }
        }
        public bool isGlobal
        {
            get
            {
                return !isLocal && _instance == -7;
            }
        }
        public bool isSelf
        {
            get
            {
                return isLocal ||  _instance == -1;
            }
        }
        public bool Equals(ILVariable obj)
        {
            if (obj.isArray != this.isArray || obj.isLocal != this.isLocal) return false; // easy
            if (isLocal) return obj.Name == Name;
            if (obj._instance == 0 || _instance == 0) return false; // stack values are not resolved and not equal
            else return obj.Name == Name && obj._instance == _instance;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            ILVariable test = obj as ILVariable;
            return Equals(test);
        }
        // The semi tricky one
        protected override bool InternalTreeEqual(ILNode node)
        {
            ILVariable v = node as ILVariable;
            if (v == null) return false;
            if (!v.isResolved || !isResolved) throw new Exception("Must be resolved to get here");
            if (isArray != v.isArray) return false;
            if (v.Index == null)
                return v.Name == Name && Instance.TreeEqual(v.Instance);
            else
                return v.Name == Name && Instance.TreeEqual(v.Instance) && Index.TreeEqual(v.Index);
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            if (!isLocal)
            {
                if (_instance != 0)
                    sb.Append(Context.InstanceToString(_instance));
                else
                    Instance.ToStringBuilder(sb, 0);
                sb.Append('.');
            }
           
         
            if (!isResolved) sb.Append('?');
            sb.Append(Name);
            if (!isResolved) sb.Append('?');
            if (isArray)
            {
                sb.Append('[');
                sb.Append(Index);
                sb.Append(']');
            }
            return false;
        }
    }
    public class ILRange : IComparable<ILRange>
    {
        public int From;
        public int To;   // Exlusive

        public ILRange(int @from, int to)
        {
            this.From = @from;
            this.To = to;
        }
        public ILRange(int @from)
        {
            this.To = this.From = @from;
        }
        public override string ToString()
        {
            if (From == To)
                return To.ToString();
            else
                return From.ToString() + "-" + To.ToString();
        }
        public bool Contains(int value) { return value == From || value == To || (value > From && value < To); }
        public static void OrderAndJoin(List<ILRange> input)
        {
            if (input == null) throw new ArgumentNullException("Input is null!");
            input.Sort(); // sort it
            bool modified;
            do
            {
                modified = false;
                for (int i = 0; i < input.Count - 1;)
                {
                    ILRange curr = input[i];
                    ILRange next = input[i + 1];
                    // Merge consequtive ranges if they intersect
                    if (curr.From <= next.From && next.From <= curr.To)
                    {
                        curr.To = Math.Max(curr.To, next.To);
                        input.RemoveAt(i + 1);
                        modified = true;
                    }
                    else if ((curr.To + 1) == next.From) // if the two are just touching, we add this cause our byte code is not in bytes
                    {
                        curr.To = next.To;
                        input.RemoveAt(i + 1);
                        modified = true;
                    }
                    else
                    {
                        i++;
                    }
                }
            } while (modified);
            List<ILRange> ranges = input.Where(r => r != null).OrderBy(r => r.From).ToList();
        }
        public static List<ILRange> OrderAndJoin(IEnumerable<ILRange> input)
        {
            if (input == null)
                throw new ArgumentNullException("Input is null!");

            List<ILRange> ranges = input.Where(r => r != null).ToList();
            OrderAndJoin(ranges);
            return ranges;
        }

        public static IEnumerable<ILRange> Invert(IEnumerable<ILRange> input, int codeSize)
        {
            if (input == null)
                throw new ArgumentNullException("Input is null!");

            if (codeSize <= 0)
                throw new ArgumentException("Code size must be grater than 0");

            var ordered = OrderAndJoin(input);
            if (ordered.Count == 0)
            {
                yield return new ILRange( 0, codeSize );
            }
            else {
                // Gap before the first element
                if (ordered.First().From != 0)
                    yield return new ILRange( 0, ordered.First().From) ;

                // Gaps between elements
                for (int i = 0; i < ordered.Count - 1; i++)
                    yield return new ILRange(ordered[i].To,  ordered[i + 1].From );

                // Gap after the last element
                Debug.Assert(ordered.Last().To <= codeSize);
                if (ordered.Last().To != codeSize)
                    yield return new ILRange( ordered.Last().To,  codeSize );
            }
        }

        public int CompareTo(ILRange other)
        {
            return From.CompareTo(other.From);
        }
        /*
void ReportUnassignedILRanges(ILBlock method)
{
   var unassigned = ILRange.Invert(method.GetSelfAndChildrenRecursive<ILExpression>().SelectMany(e => e.ILRanges), context.CurrentMethod.Body.CodeSize).ToList();
   if (unassigned.Count > 0)
       Debug.WriteLine(string.Format("Unassigned ILRanges for {0}.{1}: {2}", this.context.CurrentMethod.DeclaringType.Name, this.context.CurrentMethod.Name, string.Join(", ", unassigned.Select(r => r.ToString()))));
}
*/
    }

    public class ILExpression : ILNode
    {
        public override bool hasChildren { get { return Operand is ILNode || Arguments.Count > 0; } }
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
        public static ILExpression MakeVariable(string name)
        {
            return new ILExpression(GMCode.Var, ILVariable.GenerateTemp(name));
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
        public ILExpression(GMCode code, object operand, List<ILRange> ranges, params ILExpression[] args)
        {
            if (operand is ILExpression)
                throw new ArgumentException("operand");

            this.Code = code;
            this.Operand = operand;
            this.Arguments = new List<ILExpression>(args);

            if (ranges != null)
                this.ILRanges = ranges;
            else
                this.ILRanges = new List<ILRange>(1);
            InferredType = GM_Type.NoType;
            ExpectedType = GM_Type.NoType;
        }
        public ILExpression(GMCode code, object operand, params ILExpression[] args) : this(code, operand, null, args) { }


        public override IEnumerable<ILNode> GetChildren()
        {
            if (Operand is ILVariable) yield return Operand as ILNode;
            if (Arguments.Count > 0) foreach (var e in Arguments) yield return e;
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
        // prints a formated line Header
        const int headerSize = 10;
        public void AppendHeader(StringBuilder sb)
        {
            int len = sb.Length+ headerSize;

            sb.Append('(');
            if (ILRanges != null && ILRanges.Count > 0)
            {
                var range = ILRange.OrderAndJoin(GetSelfAndChildrenRecursive<ILExpression>().SelectMany(x => x.ILRanges));
                sb.AppendArguments(range.Select(x => x.ToString()));
            }
            sb.Append(")");
            if (len > sb.Length) sb.Append(' ', len-sb.Length);
            sb.Append(':');
        }
        void WriteLeaf(StringBuilder sb, string op)
        {
            sb.Append(op);
            sb.Append('(');
            sb.Append(Arguments.ElementAtOrDefault(0));
            sb.Append(')');
        }
        void WriteTree(StringBuilder sb, string op)
        {
            sb.Append('(');
            sb.Append(Arguments.ElementAtOrDefault(0));
            sb.Append(op);
            sb.Append(Arguments.ElementAtOrDefault(1));
            sb.Append(')');
        }
        void WriteOperand(StringBuilder sb)
        {
            if (Operand is ILValue) (Operand as ILValue).ToStringBuilder(sb,0);
            else if (Operand is ILVariable) (Operand as ILVariable).ToStringBuilder(sb, 0);
            else if (Operand is ILCall) (Operand as ILCall).ToStringBuilder(sb, 0);
            else if (Operand is ILLabel) (Operand as ILLabel).ToStringBuilder(sb, 0);
            else
            {
                sb.Append('?');
                sb.Append(Operand.ToString());
                sb.Append('?');
            }
        }
        protected override bool InternalTreeEqual(ILNode node)
        {
            if (node is ILValue && Code == GMCode.Constant)
                return (Operand as ILValue).TreeEqual(node as ILValue);
            else if (node is ILVariable && Code == GMCode.Var)
                return (Operand as ILVariable).TreeEqual(node as ILVariable);
            else
            {
                ILExpression e = node as ILExpression;
                if (e == null) return false;
                if (Code != e.Code) return false;
                if (e.Operand != null || Operand != null) return false;
                // operands have to be cleared, we only compare arguments
                if (e.Arguments.Count != Arguments.Count) return false;
                for (int i = 0; i < e.Arguments.Count; i++)
                    if (!Arguments[i].TreeEqual(e.Arguments[i])) return false;
                return true;
            }
        }
        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            switch (Code)
            {
                case GMCode.Not:
                    if (Arguments.Count != 1) goto default;
                    WriteLeaf(sb, "!"); break;
                case GMCode.Neg:
                    if (Arguments.Count != 1) goto default;
                    WriteLeaf(sb, "-"); break;
                case GMCode.Mul: if (Arguments.Count != 2) goto default; WriteTree(sb, "*"); break;
                case GMCode.Div: if (Arguments.Count != 2) goto default; WriteTree(sb, "/"); break;
                case GMCode.Mod: if (Arguments.Count != 2) goto default; WriteTree(sb, "%"); break;
                case GMCode.Add: if (Arguments.Count != 2) goto default; WriteTree(sb, "+"); break;
                case GMCode.Sub: if (Arguments.Count != 2) goto default; WriteTree(sb, "-"); break;
                case GMCode.Concat: if (Arguments.Count != 2) goto default; WriteTree(sb, ".."); break;
                case GMCode.Sne: if (Arguments.Count != 2) goto default; WriteTree(sb, "!="); break;
                case GMCode.Sge: if (Arguments.Count != 2) goto default; WriteTree(sb, ">="); break;
                case GMCode.Slt: if (Arguments.Count != 2) goto default; WriteTree(sb, "<"); break;
                case GMCode.Sgt: if (Arguments.Count != 2) goto default; WriteTree(sb, ">"); break;
                case GMCode.Seq: if (Arguments.Count != 2) goto default; WriteTree(sb, "=="); break;
                case GMCode.Sle: if (Arguments.Count != 2) goto default; WriteTree(sb, "<="); break;
                case GMCode.LogicAnd: if (Arguments.Count != 2) goto default; WriteTree(sb, "&&"); break;
                case GMCode.LogicOr: if (Arguments.Count != 2) goto default; WriteTree(sb, "||"); break;
                case GMCode.Constant:
                case GMCode.Var:
                    WriteOperand(sb);
                    break;
                case GMCode.Assign:
                    WriteOperand(sb);
                    sb.Append(" = ");
                    Arguments.Single().ToStringBuilder(sb, 0);
                    break;
                case GMCode.Call:
                    if (Operand is ILCall) WriteOperand(sb);
                    else // not processed
                    {
                        sb.Append('(');
                        sb.Append("Name=?");
                        sb.Append(Operand.ToString());
                        sb.Append("? ArgCount=");
                        sb.Append(Extra);
                        sb.Append(" )");
                    }
                    break;
                case GMCode.LoopOrSwitchBreak:
                    sb.Append("break");
                    break;
                case GMCode.LoopContinue:
                    sb.Append("continue");
                    break;
                default:
                    // specal code for extra processing down the road
                  
                    sb.Append(" Code=");
                    sb.Append(Code.ToString());
                    if (Operand != null)
                    {
                        sb.Append(" Operand=");
                        WriteOperand(sb);
                    }
                    if (Arguments != null && Arguments.Count > 0)
                    {
                        sb.Append(" Arguments=");
                        sb.AppendArguments(Arguments);
                    }
                    break;
            }
            return false;
        }
    }

    public class ILWhileLoop : ILNode
    {
        public ILExpression Condition;
        public ILBlock BodyBlock;
        public override bool hasChildren {get { return Condition != null || BodyBlock != null;}}
        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Condition != null)
                yield return this.Condition;
            if (this.BodyBlock != null)
                yield return this.BodyBlock;
        }

        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            sb.Append("while(");
            sb.Append(Condition);
            sb.Append(")");
            sb.AppendBlock(BodyBlock, ident);
            return true;
        }
    }
    // mainly for lua, we chain if statements together to make an ElseIf chain
    public class ILElseIfChain : ILNode
    {
        public List<ILCondition> Conditions = new List<ILCondition>();
        public ILBlock Else = null;
        public override bool hasChildren { get { return Conditions.Count > 0 || Else != null; } }
        public override IEnumerable<ILNode> GetChildren()
        {
            foreach (var c in Conditions) yield return c;
            if (Else != null) yield return Else;
        }
        public override bool ToStringBuilder(StringBuilder sb,int ident)
        {
            bool elseif = false;
            foreach(var c in Conditions)
            {
                if (!elseif)
                {
                    sb.Append("if(");
                    elseif = true;
                } else sb.Append("elseif(");
                sb.Append(c.Condition);
                sb.Append(")");
                sb.AppendBlock(c.TrueBlock, ident);
            }
            if (Else != null && Else.Body.Count >0) 
            {
                sb.Append("else ");
                sb.AppendBlock(Else, ident);
            }
            return true;
        }
    }
    public class ILCondition : ILNode
    {
        public ILExpression Condition;
        public ILBlock TrueBlock;   // Branch was taken
        public ILBlock FalseBlock;  // Fall-though
        public override bool hasChildren { get { return true; } }
        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Condition != null)
                yield return this.Condition;
            if (this.TrueBlock != null)
                yield return this.TrueBlock;
            if (this.FalseBlock != null)
                yield return this.FalseBlock;
        }

        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            sb.Append("if(");
            sb.Append(Condition);
            sb.Append(")");
            sb.AppendBlock(TrueBlock, ident);
            if(FalseBlock != null && FalseBlock.Body.Count > 0)
            {
                sb.Append("else ");
                sb.AppendBlock(FalseBlock, ident);
            }
            return true;
        }
    }
   
    public class ILSwitch : ILNode
    {
        public override bool hasChildren { get { return true; } }

        public class ILCase : ILBlock
        {
            public List<ILExpression> Values = new List<ILExpression>();
            public override string ToString() // only really need this for debug as the writers don't use this
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{ ILSwitch.ILCase Values=");
                if(Values.Count > 0)
                {
                    sb.Append(Values[0]);
                    for(int i=1; i < Values.Count; i++)
                    {
                        sb.Append(", ");
                        sb.Append(Values[1]);
                    }
                }
                sb.Append("}");
                return sb.ToString();
            }
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
        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            sb.AppendLine("ILSwitch");
            return true;
        }
    }

    public class ILWithStatement : ILNode
    {
        public override bool hasChildren { get { return true; } }
        public static int withVars = 0;
        public ILBlock Body = new ILBlock();
        public ILExpression Enviroment;
        public override IEnumerable<ILNode> GetChildren()
        {
            if (Enviroment != null) yield return Enviroment;
            if (this.Body != null) yield return this.Body;
        }
        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            sb.Append("with(");
            sb.Append(Enviroment);
            sb.Append(")");
            sb.AppendBlock(Body, ident);
            return true;
        }
    }


    public class ILTryCatchBlock : ILNode
    {
        public override bool hasChildren { get { return true; } }
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
        public override bool ToStringBuilder(StringBuilder sb, int ident)
        {
            sb.AppendLine("ILTryCatchBlock");
            return true;
        }
    }
}

