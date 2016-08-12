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
using System.Runtime.Serialization;

namespace GameMaker.Ast
{
    public static class NodeOperations
    {
        public static void RemoveRange<T>(this IList<T> list, int index, int count) where T : ILNode
        {
            if (index < 0) throw new IndexOutOfRangeException("Index less than 0");
            if (count < 0) throw new ArgumentOutOfRangeException("count less than 0");
            if (list.Count - index < count) throw new ArgumentOutOfRangeException("Len out of range");
            if (count > 0)
            { // ugh! I forgot I have to do this BACKWARDS
                if (count == 1) list.RemoveAt(index);
                else do list.RemoveAt(index); while (--count > 0); // slow but works
            }
        }
        public static bool isParent(this ILNode node, GMCode code)
        {
            return node != null && node.Parent != null && node.Parent.Match(code);
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
                n.ToStringBuilder(sb);
            }
            return false;
        }
        public static void DebugSave(this ILBlock block, string code_file, string filename,  bool ilranges = false, bool move = true)
        {
            filename = Context.MakeDebugFileName(code_file, filename,false);
            block.DebugSave(filename, ilranges, move); // moveing is handled here

        }
        public static void DebugSave(this ILNode node, string code_file, string filename, bool move = true)
        {
            filename = Context.MakeDebugFileName(code_file, filename, false);
            node.DebugSave(filename, move); // moveing is handled here

        }
        public static string DebugILRanges(IEnumerable<ILRange> ranges)
        {
            StringBuilder sb = new StringBuilder();
            bool need_comma = false;
            sb.Append('[');
            foreach(var r in ranges)
            {
                if (need_comma) sb.Append(','); else need_comma = true;
                sb.Append(r.ToString());
            }
            sb.Append(']');
            return sb.ToString();
        }
        public static void DebugSave(this ILBlock block, string filename, bool ilranges = false, bool move = true)
        {
            filename = Context.MoveFileToOldErrors(filename,move);
            using (PlainTextWriter sw = new PlainTextWriter(filename))
            {
                sw.Indent += 2;
                block.FixParents();
                ILNode root = block.Root;
                foreach (var n in root.GetChildren())
                {
                    if (ilranges)
                    {
                        ILExpression e = n as ILExpression;
                        if (e != null)
                        {
                            e.WithILRanges(e.Arguments);
                            ILRange.OrderAndJoin(e.ILRanges);
                            sw.LineHeader = DebugILRanges(e.ILRanges);
                        }
                        else sw.LineHeader = null;
                    }
                    sw.WriteLine(n);
                }
                sw.Flush();

            }
            Context.Message("DebugSave Block Saved '{0}'", filename);
        }
        public static void DebugSave(this ILNode node, string filename, bool move = true)
        {
            filename = Context.MoveFileToOldErrors(filename, move);
            using (PlainTextWriter sw = new PlainTextWriter(filename))
            {
                node.FixParents();
                sw.WriteLine("Node: ");
                sw.WriteLine(node.ToString());
                ILNode root = node.Root as ILBlock;
                if(root != null)
                {
                    sw.WriteLine("Parent Block: ");
                    foreach (var n in root.GetChildren()) sw.WriteLine(n);
                }
                sw.Flush();
            }
            Context.Message("DebugSave Node Saved '{0}'", filename);
        }
    }
    // Used to tell if its a code block or an expression, label, etc
    // mainly only used for pritty print

    public interface ILNodeBlock { } 

    public abstract class ILNode 
    {
        public object UserData = null;
        public string Comment = null;
        protected ILNode _parent = null;
        HashSet<ILNode> _children = new HashSet<ILNode>();

        // this works pritty well though the children part I never tested:P
        public ILNode Parent {
            get { return _parent; }
            set
            {
                if (value == _parent) return;
                if (_parent != null)  _parent._children.Remove(this);
                if (value != null && !value._children.Contains(this))
                    value._children.Add(this);
                _parent = value;
            }
        }
        public virtual bool Contains(ILNode n)
        {
            if (n == this) return true;
            else return _children.Contains(n);
        }
        public IEnumerable<ILNode> TestChildren {  get { return _children; } }
        public ILNode Root
        {
            get
            {
                ILNode current = this;
                while (current.Parent != null) current = current.Parent;
                return current;
            }
        }
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
        // stupid method to fix the parrents
        public void FixParents()
        {
            foreach (var child in GetChildren())
            {
                child._parent = this;
                child.FixParents();
            }
        }
        // takes the basic blocks, and links them all to parrents

        public IEnumerable<T> GetSelfAndChildrenRecursive<T>(Func<T, bool> predicate=null) where T : ILNode
        {
            List<T> result = new List<T>(16);
            AccumulateSelfAndChildrenRecursive(result, predicate);
            return result;
        }
        void AccumulateSelfAndChildrenRecursive<T>(List<T> list, Func<T, bool> predicate) where T : ILNode
        {
            // Note: RemoveEndFinally depends on self coming before children
            T thisAsT = this as T;
            if (thisAsT != null && (predicate== null || predicate(thisAsT))) list.Add(thisAsT);
            foreach (ILNode node in this.GetChildren())
            {
                if (node != null)
                    node.AccumulateSelfAndChildrenRecursive(list, predicate);
            }
        }
        public virtual IEnumerable<ILNode> GetChildren()
        {
            yield  break;
        }
        // returns true when ending with a new line
        public abstract void ToStringBuilder(StringBuilder sb);

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            ToStringBuilder(sb);
            return sb.ToString();
        }
      
    }
    public class NodeList<T> : Collection<T> where T : ILNode
    {
        public ILNode Parrnt { get; private set; }
        public NodeList(ILNode p) : base(new List<T>(4)) { Parrnt = p; }
        static IList<T> GetRootList(IList<T> start)
        {
            if (start == null) return new List<T>(4);
            NodeList<T> test;
            while ((test = start as NodeList<T>) != null)
                start = test.Items;
            if (start.IsReadOnly) start = new List<T>(start);// who'd a thought that arrays use an IList interface?
            return start;
        }
        public NodeList(ILNode p, IList<T> list) : base(GetRootList(list)) {
            Parrnt = p;
            foreach (var n in Items) n.Parent = this.Parrnt;
        }
        protected override void InsertItem(int i, T n)
        {
            n.Parent = this.Parrnt;
            base.InsertItem(i, n);
        }
        protected override void RemoveItem(int index)
        {
            Items[index].Parent = null;
            base.RemoveItem(index);
        }
        protected override void SetItem(int index, T n)
        {
            Items[index].Parent = null;
            n.Parent = this.Parrnt;
            base.SetItem(index, n);
        }
        protected override void ClearItems()
        {
            foreach (var n in Items) n.Parent = null;
        }
    }


    public class ILBasicBlock : ILNode, ILNodeBlock
    {
        NodeList<ILNode> _body=null;
        public IList<ILNode> Body
        {
            get { return _body; }
            set
            {
                if (_body.Count > 0) _body.Clear();
                _body = new NodeList<ILNode>(this, value);
            }
        }
        public ILBasicBlock() { _body = new NodeList<ILNode>(this); }
        public override IEnumerable<ILNode> GetChildren()
        {
            return _body;
        }
        public override void ToStringBuilder(StringBuilder sb)
        {
            foreach (var n in Body)
            {
                sb.Append('\t');
                n.ToStringBuilder(sb);
                sb.AppendLine();
            }
        }
    }
    public class ILBlock : ILNode, ILNodeBlock
    {
        public ILExpression EntryGoto;
        NodeList<ILNode> _body = null;
        public ILBlock() { _body = new NodeList<ILNode>(this); }
        public IList<ILNode> Body
        {
            get { return _body; }
            set
            {
                if (_body.Count > 0) _body.Clear();
                _body = new NodeList<ILNode>(this, value);
            }
        }
        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.EntryGoto != null) yield return this.EntryGoto;
            if(_body != null) foreach(ILNode child in _body) yield return child;
        }
        public void DebugSaveFile(string filename)
        {
            this.DebugSave(filename);
        }
        public override void ToStringBuilder(StringBuilder sb)
        {
            if(this.EntryGoto != null)
            {
                sb.Append('\t');
                this.EntryGoto.ToStringBuilder(sb);
                sb.AppendLine();
            }
            foreach (var n in Body)
            {
                sb.Append('\t');
                n.ToStringBuilder(sb);
                sb.AppendLine();
            }
        }
    }

    public class ILCall : IEquatable<ILCall>
    {
        [DataMember] string _name = null;
        [DataMember] GM_Type _returnType = GM_Type.Int;
        [DataMember] int _argumentCount;
        public string Name { get { return _name; } }
        public GM_Type ReturnType { get { return _returnType; } }
        public int ArgumentCount { get { return _argumentCount; } }
        ILCall() { }
        public static Dictionary<string, ILCall> _cache = new Dictionary<string, ILCall>();
        public static ILCall CreateCall(string name, int argumentCount)
        {
            name = string.Intern(name);
            ILCall c;
            if(!_cache.TryGetValue(name, out c))
            {
                c = new ILCall() { _name = name, _argumentCount = argumentCount, _returnType = GM_Type.NoType };
                lock (_cache) _cache.Add(name,c);
                if (c.isBuiltin) c._returnType = Constants.GetFunctionType(name);
            } // humm, forgot about var arg stuff like choose
          //  Debug.WriteLineIf(name != "choise" && name != "script_execute" && argumentCount != c.ArgumentCount, "Call '" + name + "' diffrent args need " + argumentCount + " but original " + c.ArgumentCount);
            //Debug.Assert(c.ArgumentCount == argumentCount);
            return c;
        }
        public bool isBuiltin
        {
            get
            {
                return Constants.IsDefined(_name);
            }
        }
        public override string ToString()
        {
            return ReturnType.ToString() + ' ' + Name + '(' + ArgumentCount + ')'; ;
        }

        public bool Equals(ILCall other)
        {
            if (object.ReferenceEquals(other, null)) return false;
            if (object.ReferenceEquals(other, this)) return true;
            return this.Name == other.Name;
        }
        public override int GetHashCode()
        {
            return _name.GetHashCode();
        }
    }

    public class ILValue : IEquatable<ILValue>, IComparable<ILValue>
    {
        public ILExpression ToExpresion()
        {
            return new ILExpression(GMCode.Constant, this);
        }
        public int? DataOffset = null;
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
        public int ByteSize
        {
            get
            {
                switch (Type)
                {
                    case GM_Type.Bool:
                    case GM_Type.Var:
                    case GM_Type.String:
                    case GM_Type.Float:
                    case GM_Type.Double:
                        return 4;
                    case GM_Type.Long:
                        return 8;
                    case GM_Type.Short:
                        return 2;
                    default:
                        return -1;
                }
            }
        }
   
        public int? IntValue
        {
            get
            {
                if (Value is int) return (int)Value;
                else return null;
            }
        }
        public void ToStringBuilder(StringBuilder sb)
        {
            if (Value.GetType().IsPrimitive) sb.Append(Value.ToString());
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
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            ToStringBuilder(sb);
            return sb.ToString();
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
        static int generate_count = 0;
        // generates a label, gurntess unique
        public static ILLabel Generate(string name = "G")
        {
            System.Threading.Interlocked.Increment(ref generate_count);
            return Generate(name, generate_count);
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
            if (this.Offset != other.Offset) return false;
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
        public override void ToStringBuilder(StringBuilder sb)
        {
            sb.Append(Name);
            sb.Append(':');
        }
    }
    public class UnresolvedVar
    {
        public string Name;
        public int Extra;
        public int Operand;
        public override string ToString()
        {
            string str = '?' + Context.InstanceToString(Extra) + "." + Name;
            if (Operand > 0) str += "[]";
            str += '?';
            return str;
        }
   
    }
    [DataContract]
    public class ILVariable
    {
        [DataMember] string _name = null;
        [DataMember] int _instance = 0;
        [DataMember] int _arrayDim = 0;
        [DataMember] ILType _inferedType = new ILType();
        [DataMember] string _instanceName = null;
        static Dictionary<string, ILVariable> _globals = new Dictionary<string, ILVariable>();
        const string AllVarsCacheFileName = "GlobalVarTypeCache.xml";
        static ILVariable()
        {
            if (System.IO.File.Exists(AllVarsCacheFileName))
            {
                try
                {
                    using (FileStream fs = new FileStream(AllVarsCacheFileName, FileMode.Open))
                    {
                        DataContractSerializer ser = new DataContractSerializer(typeof(Dictionary<string, ILVariable>));
                        _globals = ser.ReadObject(fs) as Dictionary<string, ILVariable>;
                    }
                }
                catch (Exception ex)
                {
                    Context.Info("Cache file '{0}' Corrupted Ex:{1}", AllVarsCacheFileName, ex.Message);
                }
            }
            if (_globals == null) _globals = new Dictionary<string, ILVariable>();
        }
        public static void SaveAllVarRefs()
        {
            using (FileStream fs = new FileStream(AllVarsCacheFileName, FileMode.Create))
            {
                DataContractSerializer ser = new DataContractSerializer(typeof(Dictionary<string, ILVariable>));
                ser.WriteObject(fs, _globals);
            }
        }
        public ILExpression ToExpresion()
        {
            return new ILExpression(GMCode.Var, this);
        }
        //Crappy type system

       
        public bool isBuiltin
        {
            get
            {
                return Constants.IsDefined(_name);
            }
        }
        public string InstanceName { get
            {
                if (_instanceName == null) {
                    _instanceName = Context.InstanceToString(_instance);
                }
                return _instanceName;
            }
        }
        public bool isGlobal
        {
            get
            {
                return _instance == -5;
            }
        }
        public bool isSelf
        {
            get
            {
                return _instance == -1;
            }
        }
        public int ArrayDimension { get { return _arrayDim; } set { _arrayDim = value; } }
        public string Name { get { return _name; } }
        public int Instance { get { return _instance; } }
        public GM_Type Type { get { return _inferedType; }  set { _inferedType.InferedType = value; } }
        public override string ToString()
        {
            return '(' + Type.ToString() + ')' + InstanceName + "." + Name;
        }

        public bool Equals(ILVariable other)
        {
            if (object.ReferenceEquals(other, null)) return false;
            if (object.ReferenceEquals(other, this)) return true;
            return this._instance == other._instance && this.Name == other.Name;
        }
        public override int GetHashCode()
        {
            return _name.GetHashCode() ^ _instance;
        }

        public static ILVariable CreateVariable(string name, int instance, Dictionary<string,ILVariable> locals)
        {
            if (name == null) throw new ArgumentNullException("name");
          //  if (instance == 0) throw new ArgumentException("instance cannot be 0", "instnace");
            ILVariable v=null;
            switch (instance)
            {
                case -7: // negitive 7? This is a pure temp value, used in returns and passing stuff beetween instances?
                    v = new ILVariable() { _name = string.Intern(name), _instance = -7, _arrayDim = 0 };
                    break;
                case -5:
                    if (!_globals.TryGetValue(name, out v))
                    {
                        v = new ILVariable() { _name = string.Intern(name), _instance = instance, _arrayDim = 0 };
                        lock (_globals) _globals.Add(name, v);
                    }
                    break;
                case -1:
                    if (!locals.TryGetValue(name, out v))
                    {
                        v = new ILVariable() { _name = string.Intern(name), _instance = instance, _arrayDim = 0 };
                        lock (locals) locals.Add(name, v);
                    }
                    break;
                case -2: // ? other instance, how does this work?
                  //  Debug.Assert(false);
                    v = new ILVariable() { _name = string.Intern(name), _instance = -2, _arrayDim = 0 };
                    break;
                case 0: // zero is stack but in this case, its a local var that has the instance
                    v = new ILVariable() { _name = string.Intern(name), _instance = 0, _arrayDim = 0 };
                    break;
                default:
                    Debug.Assert(instance >= 0);
                        if (instance < 0 || instance >= File.Objects.Count) throw new ArgumentOutOfRangeException("instance out of range of objets", "instance");
                    v = new ILVariable() { _name = string.Intern(name), _instance = instance, _arrayDim = 0 };
                    break;
            }
            Debug.Assert(v != null);
            return v;
        }

        public bool isArray
        {
            get
            {
                return ArrayDimension > 0;
            }
        }
        ILVariable() { }

        static int static_gen = 0;
        // generates a variable, gurntees it unique
        public static ILVariable GenerateTemp(string name = "gen")
        {
            name = string.Format("{0}_{1}", name, static_gen++);
            var v = new ILVariable();
            v._name = name;
            v.Type = GM_Type.Int;
            v.isGenerated = true;
            return v;
        }



        public bool isGenerated = false;

    }
    [DataContract]
    public class ILType : IEquatable<ILType>
    {
        [DataMember]
        HashSet<GM_Type> _typesSeen = new HashSet<GM_Type>();
        [DataMember]
        GM_Type _inferedType = GM_Type.NoType;
        public GM_Type InferedType { get { return _inferedType; } set { _inferedType = _inferedType.ConvertType(value); if (value != GM_Type.NoType) _typesSeen.Add(value); } }
        public IReadOnlyCollection<GM_Type> TypesSeen {  get { return _typesSeen; } }
        public void AddTypes(IEnumerable<GM_Type> types)
        {
            _typesSeen.UnionWith(types);
        }
        public override string ToString()
        {
            return _inferedType.ToString();
        }
        public override int GetHashCode()
        {
            return _inferedType.GetHashCode();
        }
        public bool Equals(ILType other)
        {
            return _inferedType == other._inferedType;
        }
        public override bool Equals(object obj)
        {
            return obj!= null && (object.ReferenceEquals(this,obj) || (obj is ILType && Equals((ILType)obj)));
        }
        public static bool operator == (ILType t0, ILType t1) { return t0.InferedType == t1.InferedType; }
        public static bool operator != (ILType t0, ILType t1) { return t0.InferedType != t1.InferedType; }
        public static bool operator == (ILType t0, GM_Type t1) { return t0.InferedType == t1; }
        public static bool operator != (ILType t0, GM_Type t1) { return t0.InferedType != t1; }

        public static implicit operator GM_Type(ILType d) { return d.InferedType; }
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
                return '(' + From.ToString() + "-" + To.ToString() + ')';
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
        // Use this to find how complex the expression tree is
        public int ExpressionDepth
        {
            get
            {
                switch (Code)
                {
                    case GMCode.Var:
                    case GMCode.Call:
                    case GMCode.Constant:
                        return 1; // leafs
                    default:
                        int i = 1;
                        if (Code != GMCode.Call) foreach (var e in Arguments) i += e.ExpressionDepth;
                        return i;
                }
            }
        }
        NodeList<ILExpression> _arguments;
        public IList<ILExpression> Arguments { get { return _arguments; } set { if (_arguments.Count > 0) _arguments.Clear(); _arguments = new NodeList<ILExpression>(this, value); } }
        public override IEnumerable<ILNode> GetChildren() { return _arguments; }
        ILType _typesSeen = new ILType();

        public GMCode Code { get; set; }
        public int Extra { get; set; }
        public object Operand { get; set; }
        // Mapping to the original instructions (useful for debugging)
        public List<ILRange> ILRanges { get; set; }
        
        public ILType TypesSeen { get { return _typesSeen; } }
        public IEnumerable<GM_Type> Types { get { return _typesSeen.TypesSeen; } set { if(value != null)_typesSeen.AddTypes(value); } }

        GM_Type _type = GM_Type.NoType;
        public GM_Type Type {
            get
            {
                if (_type == GM_Type.NoType)
                {
                    GM_Type t = GM_Type.NoType;
                    switch (Code)
                    {
                        case GMCode.Call: t = (Operand as ILCall).ReturnType; break;
                        case GMCode.Var: t = (Operand as ILVariable).Type; break;
                        case GMCode.Constant: t = (Operand as ILValue).Type; break;
                        default:
                            foreach (var e in Arguments) t = t.ConvertType(e.Type);
                            break;
                    }
                    return t;
                }
                else return _type;
            }
        }

        public static readonly object AnyOperand = new object();

        public ILExpression(ILExpression i) // copy it
        {
            this.Code = i.Code;
            this.Operand = i.Operand; // don't need to worry about this
            this._arguments = new NodeList<ILExpression>(this);
            if (i.Arguments.Count != 0) foreach (var n in i.Arguments) this.Arguments.Add(new ILExpression(n));
            this.ILRanges = new List<ILRange>(i.ILRanges);
        }
        public ILExpression(GMCode code, object operand)
        {
            if (operand is ILExpression) throw new ArgumentException("operand");
            this.Code = code;
            this.Operand = operand;
            this._arguments = new NodeList<ILExpression>(this) ;
            this.ILRanges = new List<ILRange>(1);
        }
        public ILExpression(GMCode code, object operand, params ILExpression[] args)
        {
            if (operand is ILExpression) throw new ArgumentException("operand");
            this.Code = code;
            this.Operand = operand;
            this._arguments = args == null ? new NodeList<ILExpression>(this) : new NodeList<ILExpression>(this, args);
            this.ILRanges = new List<ILRange>(1);
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
        bool WriteLeaf(StringBuilder sb, string op)
        {
            if (Arguments.ElementAtOrDefault(0) == null) return false;
            sb.Append(op);
            sb.Append('(');
            Arguments[0].ToStringBuilder(sb);
            sb.Append(')');
            return true;
        }
        bool WriteTree(StringBuilder sb, string op)
        {
            if (Arguments.ElementAtOrDefault(0) == null || Arguments.ElementAtOrDefault(1) == null) return false;
            sb.Append('(');
            Arguments[0].ToStringBuilder(sb);
            sb.Append(op);
            Arguments[1].ToStringBuilder(sb);
            sb.Append(')');
            return true;
        }
        void WriteOperand(StringBuilder sb)
        {
            sb.Append(Operand.ToString());
        }

        public override void ToStringBuilder(StringBuilder sb)
        {
            switch (Code)
            {
                case GMCode.Not: if (WriteLeaf(sb, "!")) break; else goto default;
                case GMCode.Neg: if (WriteLeaf(sb, "-")) break; else goto default;
                case GMCode.Mul: if (WriteTree(sb, "*")) break; else goto default;
                case GMCode.Div: if(WriteTree(sb,"/")) break; else goto default; 
                case GMCode.Mod: if(WriteTree(sb,"%")) break; else goto default; 
                case GMCode.Add: if(WriteTree(sb,"+")) break; else goto default; 
                case GMCode.Sub: if(WriteTree(sb,"-")) break; else goto default; 
                case GMCode.Concat: if(WriteTree(sb,"..")) break; else goto default; 
                case GMCode.Sne: if(WriteTree(sb,"!=")) break; else goto default; 
                case GMCode.Sge: if(WriteTree(sb,">=")) break; else goto default; 
                case GMCode.Slt: if(WriteTree(sb,"<")) break; else goto default; 
                case GMCode.Sgt: if(WriteTree(sb,">")) break; else goto default; 
                case GMCode.Seq: if(WriteTree(sb,"==")) break; else goto default; 
                case GMCode.Sle: if(WriteTree(sb,"<=")) break; else goto default; 
                case GMCode.LogicAnd: if(WriteTree(sb,"&&")) break; else goto default; 
                case GMCode.LogicOr: if(WriteTree(sb,"||")) break; else goto default; 
                case GMCode.Constant:
                    {
                        ILValue v = Operand as ILValue;
                        v.ToStringBuilder(sb);
                    }
                    break;
                case GMCode.Var:
                    {
                        ILVariable v = Operand as ILVariable;
                        if (v.Instance != 0)
                            sb.Append(v.InstanceName);
                        else
                            Arguments[0].ToStringBuilder(sb);
                        sb.Append('.');
                        sb.Append(v.Name);
                        if(Arguments.Count > 1)
                        {
                            sb.Append('[');
                            if(Arguments[1].Code == GMCode.Array2D)
                            {
                                Arguments[1].Arguments[0].ToStringBuilder(sb);
                                sb.Append(',');
                                Arguments[1].Arguments[1].ToStringBuilder(sb);
                            } else  Arguments[1].ToStringBuilder(sb);
                            sb.Append(']');
                        }
                    }
                    break;
                case GMCode.Assign:
                    if (Arguments.Count != 2) goto default;
                    Arguments[0].ToStringBuilder(sb);
                    sb.Append(" = ");
                    Arguments[1].ToStringBuilder(sb);
                    break;
                case GMCode.Call:
                    {
                        ILCall call = Operand as ILCall;
                        sb.Append(call.Name);
                        sb.Append('(');
                        if (Arguments.Count != call.ArgumentCount)
                        {
                            sb.Append('?');
                            sb.Append(call.ArgumentCount);
                            sb.Append('?');
                        } else
                        {
                            sb.AppendArguments(Arguments);
                        }
                        sb.Append(')');
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
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('(');
            sb.Append(Type);
            sb.Append(')');
            ToStringBuilder(sb);
            return sb.ToString();
        }
    }
   
    public class ILCondition : ILNode, ILNodeBlock
    {
        ILExpression _condition = null;
        ILBlock _trueBlock = null;
        ILBlock _falseBlock = null;
        public ILExpression Condition
        {
            get { return _condition; }
            set
            {
                if (_condition != null) _condition.Parent = null;
                if (value != null) value.Parent = this;
                _condition = value;
            }
        }
     
        public ILBlock TrueBlock
        {
            get { return _trueBlock; }
            set
            {
                if (_trueBlock != null) _trueBlock.Parent = null;
                if (value != null) value.Parent = this;
                _trueBlock = value;
            }
        }
        public ILBlock FalseBlock// Fall-though
        {
            get { return _falseBlock; }
            set
            {
                if (_falseBlock != null) _falseBlock.Parent = null;
                if (value != null) value.Parent = this;
                _falseBlock = value;
            }
        }

        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Condition != null)
                yield return this.Condition;
            if (this.TrueBlock != null)
                yield return this.TrueBlock;
            if (this.FalseBlock != null)
                yield return this.FalseBlock;
        }

        public override void ToStringBuilder(StringBuilder sb)
        {
            sb.Append("if(");
            sb.Append(Condition);
            sb.AppendLine(")");
            sb.AppendLine("{");
            TrueBlock.ToStringBuilder(sb);
            sb.AppendLine("}");
            if(FalseBlock != null && FalseBlock.Body.Count > 0)
            {
                sb.AppendLine("else");
                sb.AppendLine("{");
                TrueBlock.ToStringBuilder(sb);
                sb.AppendLine("}");
            }
        }
    }
   
    public class ILSwitch : ILNode, ILNodeBlock
    {

        public class ILCase : ILBlock
        {
            NodeList<ILExpression> _values;
            public IList<ILExpression> Values { get { return _values; } set { if (_values != null && _values.Count > 0) _values.Clear(); _values = new NodeList<ILExpression>(this, value); } }
            public override void ToStringBuilder(StringBuilder sb)
            {
                foreach(var v in Values)
                {
                    sb.Append("case ");
                    v.ToStringBuilder(sb);
                    sb.AppendLine(":");
                }
                base.ToStringBuilder(sb);
            }
        }
        ILExpression _condition = null;
        ILBlock _default = null;
        NodeList<ILCase> _cases = null;
        public IList<ILCase> Cases { get { return _cases; } set { if (_cases != null && _cases.Count > 0) _cases.Clear(); _cases = new NodeList<ILCase>(this, value); } }
        public ILSwitch() { _cases = new NodeList<ILCase>(this); }
        public ILExpression Condition
        {
            get { return _condition; }
            set
            {
                if (_condition != null) _condition.Parent = null;
                if (value != null) value.Parent = this;
                _condition = value;
            }
        }

        public ILBlock Default
        {
            get { return _default; }
            set
            {
                if (_default != null) _default.Parent = null;
                if (value != null) value.Parent = this;
                _default = value;
            }
        }

        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Condition != null) yield return this.Condition;
            foreach(var c in this.Cases) yield return c;
            if (this.Default != null) yield return this.Default;
        }
        public override void ToStringBuilder(StringBuilder sb)
        {
            sb.Append("switch(");
            Condition.ToStringBuilder(sb);
            sb.AppendLine(")");
            sb.AppendLine("{");
            foreach(var c in Cases)
            {
                sb.Append('\t');
                c.ToStringBuilder(sb);
                sb.AppendLine();
            }
            if(Default !=null && Default.Body.Count > 0)
            {
                sb.AppendLine("default:");
                sb.Append('\t');
                Default.ToStringBuilder(sb);
                sb.AppendLine();
            }
            sb.AppendLine("}");
        }
    }
    public class ILWhileLoop : ILNode, ILNodeBlock // ... these are all the practicaly same struture, so why not make them inharenent another
    {
        ILExpression _condition = null;
        ILBlock _body = null;
        public ILExpression Condition
        {
            get { return _condition; }
            set
            {
                if (_condition != null) _condition.Parent = null;
                if (value != null) value.Parent = this;
                _condition = value;
            }
        }

        public ILBlock Body
        {
            get { return _body; }
            set
            {
                if (_body != null) _body.Parent = null;
                if (value != null) value.Parent = this;
                _body = value;
            }
        }

        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Condition != null)
                yield return this.Condition;
            if (this.Body != null)
                yield return this.Body;
        }

        public override void ToStringBuilder(StringBuilder sb)
        {
            sb.Append("while(");
            sb.Append(Condition);
            sb.AppendLine(")");
            sb.AppendLine("{");
            Body.ToStringBuilder(sb);
            sb.AppendLine("}");
        }
    }
    public class ILWithStatement : ILWhileLoop
    {
        public override void ToStringBuilder(StringBuilder sb)
        {
            sb.Append("with(");
            sb.Append(Condition);
            sb.AppendLine(")");
            Body.ToStringBuilder(sb);
        }
    }
    // Oh god damnit, I need to look at the documentation more
    public class ILRepeat : ILWhileLoop
    {
        public override void ToStringBuilder(StringBuilder sb)
        {
            sb.Append("repeat(");
            sb.Append(Condition);
            sb.AppendLine(")");
            Body.ToStringBuilder(sb);
        }
    }

}

