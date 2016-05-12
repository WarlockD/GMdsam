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


    public abstract class ILNode
    {
        public virtual ILNode Dup()
        {
            throw new Exception("Not implmented"); // We need this for dups
        }
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
            StringWriter w = new StringWriter();
            WriteTo(new PlainTextOutput(w));
            return w.ToString().Replace("\r\n", "; ");
        }

        public abstract void WriteTo(ITextOutput output);
        public abstract void WriteToLua(ITextOutput output);
    }
    public class ILBasicBlock : ILNode
    {
        /// <remarks> Body has to start with a label and end with unconditional control flow </remarks>
        public List<ILNode> Body = new List<ILNode>();
        public override IEnumerable<ILNode> GetChildren()
        {
            return this.Body;
        }

        public override void WriteTo(ITextOutput output)
        {
            //  output.Write("/* Basic Block */");
            Body.WriteNodes(output, true, true);
        }
        public override void WriteToLua(ITextOutput output)
        {
            //  output.Write("/* Basic Block */");
            Body.WriteLuaNodes(output, true);
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

        public override void WriteTo(ITextOutput output)
        {
            Body.WriteNodes(output, true, true);
        }
        public override void WriteToLua(ITextOutput output)
        {
            //  output.Write("/* Basic Block */");
            Body.WriteLuaNodes(output, true);
        }
        public void WriteBlock(ITextOutput output, string BlockTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(BlockTitle)) output.WriteLine("\\" + BlockTitle);
            WriteTo(output);
            output.WriteLine(); // extra line
        }
        public void WriteBlockLua(ITextOutput output, string BlockTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(BlockTitle)) output.WriteLine("--" + BlockTitle);
            Body.WriteLuaNodes(output, false);
        }
        public void DebugSave(TextWriter tw)
        {
            using (PlainTextOutput pto = new PlainTextOutput(tw))
            {
               
                ILLabel last = null;
                for (int i = 0; i < Body.Count; i++)
                {
                    ILNode n = Body[i];
                    ILLabel l = n as ILLabel;
                    if (l != null) { last = l; pto.Header = ""; }
                    else if (last != null) pto.Header = last.Name;

                    ILExpression e = n as ILExpression;
                    if (e != null)
                        pto.Write(e.ToString()); // want to make sure we are using the debug
                    else
                        n.WriteTo(pto);
                    pto.WriteLine();
                }
            }
        }
        public void DebugSave(string filename, string fileHeader = null)
        {
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(filename))
            {
                if (fileHeader != null) sw.WriteLine("Headder   : " + fileHeader);
                sw.WriteLine("Filename : " + filename);
                DebugSave(sw);
            }
        }
        public void DebugSaveLua(string filename, string fileHeader = null)
        {
            using (PlainTextOutput pto = new PlainTextOutput(new System.IO.StreamWriter(filename)))
            {
                WriteBlockLua(pto, fileHeader);
            }
        }
    }
    // We make this a seperate node so that it dosn't interfere with anything else
    // We are not trying to optimize game maker code here:P
    public class ILAssign : ILNode
    {
        public string TextToReplace = null;
        public ILVariable Variable;
        public ILNode Expression;

        public override void WriteTo(ITextOutput output)
        {
            if (TextToReplace == null)
            {
                Variable.WriteTo(output);
                output.Write(" = ");
                Expression.WriteTo(output);
            }
            else output.Write(TextToReplace);
        }
        public override void WriteToLua(ITextOutput output)
        {
            if (TextToReplace == null)
            {
                Variable.WriteToLua(output);
                output.Write(" = ");
                Expression.WriteToLua(output);
            }
            else output.Write(TextToReplace);
            
        }
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
        public List<ILNode> Arguments = new List<ILNode>();
        public GM_Type Type = GM_Type.NoType; // return type
        public override void WriteTo(ITextOutput output)
        {
            if (EnviromentOverride != null)
            {
                output.Write(EnviromentOverride);
                output.Write('.');
            } else if (Enviroment != null)
            {
                output.Write(Enviroment);
                output.Write('.');
            }
            if (FunctionNameOverride != null)
                output.Write(FunctionNameOverride);
            else
                output.Write(Name);
            output.Write('(');
            if (Arguments.Count > 0) Arguments[0].WriteTo(output);
            if (Arguments.Count > 1)
            {
                foreach (var a in Arguments.Skip(1))
                {
                    output.Write(',');
                    a.WriteTo(output);
                }
            }
            output.Write(')');
        }
        public override IEnumerable<ILNode> GetChildren()
        {
            return Arguments;
        }

        public override void WriteToLua(ITextOutput output)
        {
            if (EnviromentOverride != null)
            {
                output.Write(EnviromentOverride);
                output.Write(':');
            }
            else if (Enviroment != null)
            {
                output.Write(Enviroment);
                output.Write(':');
            }
            if (FunctionNameOverride != null)
                output.Write(FunctionNameOverride);
            else
                output.Write(Name);
            output.Write('(');
            if (Arguments.Count > 0) Arguments[0].WriteToLua(output);
            if (Arguments.Count > 1)
            {
                foreach (var a in Arguments.Skip(1))
                {
                    output.Write(',');
                    a.WriteToLua(output);
                }
            }
            output.Write(')');
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
        public override ILNode Dup()
        {
            return new ILValue(this);
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

        public override void WriteTo(ITextOutput output)
        {
            output.Write(ToString());
        }
        public override void WriteToLua(ITextOutput output)
        {
            output.Write(ToString());
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

    public static class ILNode_Helpers
    {
        public static bool TryParse(this ILValue node, out int value)
        {
            ILValue valueNode = node as ILValue;
            if (valueNode != null && (valueNode.Type == GM_Type.Short || valueNode.Type == GM_Type.Int))
            {
                value = (int)valueNode;
                return true;
            }
            value = 0;
            return false;
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
        public override void WriteTo(ITextOutput output)
        {
            output.WriteDefinition(Name + ":", this);
        }
        public override void WriteToLua(ITextOutput output)
        {
            WriteTo(output);
        }
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
        public ILNode Instance = null; // We NEED this
        public string InstanceName=null;
        public bool isGenerated = false;
        public ILNode Index = null; // not null if we have an index
        public bool isArray=false;
        public bool isResolved = false; // resolved expresion, we don't have to do anything to it anymore
        public GM_Type Type = GM_Type.NoType;

        public string FullName
        {
            get
            {
                if (isLocal || isGenerated) return Name;
                else return  (InstanceName ?? Instance.ToString()) + '.' +   Name;
            }
        }

        public bool isFixedVar {  get
            {
                return Index is ILValue;
            }
        }
        /*
        public override IEnumerable<ILNode> GetChildren()
        {
            //
            if (Instance != null) yield return Instance;
            if (Index != null) yield return Index;
        }
        */
        public bool Equals(ILVariable obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            return obj.Name == Name && obj.InstanceName == InstanceName;
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

        public override void WriteTo(ITextOutput output)
        {
          
            if (!isLocal && !isGenerated)
            {
                Debug.Assert(Instance != null);
                do
                {
                    if (EnviromentOverride != null)
                    {
                        int instance;
                        if (InstanceName == "self" || Instance.Match(GMCode.Constant, out instance) &&
                            instance == -1)
                        {
                            output.Write(EnviromentOverride);
                            output.Write(".");
                            break;
                        }
                    }
                    if (InstanceName != null) output.Write(InstanceName);
                    else if (Instance != null) Instance.WriteTo(output);
                    else output.Write("stack");
                    output.Write(".");
                } while (false);
            }
            if (!isResolved) output.Write('?');
            output.Write(Name);
            if (isArray)
            {
                output.Write('[');
                if (Index != null) Index.WriteTo(output);
                output.Write(']');
            }
        }
        public override void WriteToLua(ITextOutput output)
        {
            if (!isLocal && !isGenerated)
            {
                Debug.Assert(Instance != null);
                do
                {
                    if (EnviromentOverride != null)
                    {
                        int instance;
                        if (InstanceName == "self" || Instance.Match(GMCode.Constant, out instance) &&
                            instance == -1)
                        {
                            output.Write(EnviromentOverride);
                            output.Write(".");
                            break;
                        }
                    }
                    if (InstanceName != null) output.Write(InstanceName);
                    else if (Instance != null) Instance.WriteToLua(output);
                    else output.Write("stack");
                    output.Write(".");
                } while (false);
            }
            if (!isResolved) output.Write('?');
            output.Write(Name);
            if (isArray)
            {
                output.Write('[');
                if (Index != null) Index.WriteToLua(output);
                output.Write(']');
            }

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
    
        public string Comment = null;
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
                return new ILLabel[] { (ILLabel)this.Operand };
            }
            else if (this.Operand is ILLabel[])
            {
                return (ILLabel[])this.Operand;
            }
            else {
                return new ILLabel[] { };
            }
        }
        void WriteCommaArguments(ITextOutput output)
        {
            output.Write('(');
            for (int i = 0; i < Arguments.Count; i++)
            {
                if (i != 0) output.Write(", ");
                WriteArgument(output, i, true);
            }
            output.Write(')');
        }
      
        // all this does is just check to see if the next tree is equal to the last tree of precidence
        // that is (4- 3) +3, the parms don't matter so don't print them, otherwise we need them
        static int Precedence(GMCode code)
        {
            switch(code){
                case GMCode.Not:
                case GMCode.Neg:
                    return 7;
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                    return 6;
                case GMCode.Add:
                case GMCode.Sub:
                    return 5;
                // case GMCode.Pow: // not in gm
                case GMCode.Concat:
                    return 4;
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Slt:
                case GMCode.Sgt:
                case GMCode.Sne:
                case GMCode.Sle:
                    return 3;
                case GMCode.LogicAnd:
                    return 2;
                case GMCode.LogicOr:
                    return 1;
                default:
                    return 8;
            }
        }
        bool CheckParm(int index)
        {
            int ours = Precedence(Code);
            ILExpression e = Arguments.ElementAtOrDefault(index);
            if (e == null) return false;
            int theirs = Precedence(e.Code);
            if (theirs == 8) return false; // its a constant or something dosn't need a parm
            if (theirs < ours) return true;
            else return false;
        }
        void ToStringOperand(StringBuilder sb,bool escapeString = true)
        {
            if (Operand != null)
            {
                if (Operand is ILLabel) sb.Append((Operand as ILLabel).Name);
                else if (escapeString)
                {
                    if (Operand is string)
                        sb.Append(GMCodeUtil.EscapeString((string) Operand));
                    else if (Operand is ILValue)
                    {
                        ILValue val = Operand as ILValue;
                        if (escapeString && val.Type == GM_Type.String) sb.Append(val.ValueText);
                        else sb.Append(val.ToString());
                    }
                    else sb.Append(Operand.ToString());
                }
                else sb.Append(Operand.ToString());
            }
        }
        string ToStringOperand(bool escapeString = true)
        {
            StringBuilder sb = new StringBuilder();
            ToStringOperand(sb, escapeString);
            return sb.ToString();
        }
        void WriteOperand(ITextOutput output, bool escapeString = true)
        {
            if (Operand == null) output.Write("%NULL_OPERAND%");
            else if (Operand is ILLabel) output.Write((Operand as ILLabel).Name);
            else if (escapeString)
            {
                if (Operand is string)
                    output.Write(GMCodeUtil.EscapeString((string)Operand));
                else if (Operand is ILValue)
                {
                    ILValue val = Operand as ILValue;
                    if (escapeString && val.Type == GM_Type.String) output.Write(val.ValueText);
                    else output.Write(val.ToString());
                }
                else output.Write(Operand.ToString());
            }
            else output.Write(Operand.ToString());
        }
        /// <summary>
        /// This makes sure when we write an argument, the string looks right
        /// </summary>
        /// <param name="output"></param>
        /// <param name="index"></param>
        /// <param name="escapeString"></param>
        public void WriteArgument(ITextOutput output, int index, bool escapeString = true)
        {
            ILExpression arg = Arguments[index];
            if (arg.Code == GMCode.Constant || arg.Code == GMCode.Var) arg.WriteOperand(output, escapeString);
            else arg.WriteTo(output); // don't know what it is
        }
        public void WriteArgumentLua(ITextOutput output, int index, bool escapeString = true)
        {
            ILExpression arg = Arguments[index];
            if (arg.Code == GMCode.Constant || arg.Code == GMCode.Var) arg.WriteOperand(output, escapeString);
            else arg.WriteToLua(output); // don't know what it is
        }
        public void WriteArguments(ITextOutput output, int start)
        {
            Arguments.WriteNodes(output, start, true, true);
        }
        public void WriteArgumentsLua(ITextOutput output, int start)
        {
            Arguments.WriteLuaNodes(output,false);
        }
        static readonly string POPDefaultString = "%POP%";
        public void WriteArgumentOrPop(ITextOutput output, int index, bool escapeString = true)
        {
            if (index < Arguments.Count) WriteArgument(output, index, escapeString);
            else output.Write(POPDefaultString);
        }
        public void WriteParm(ITextOutput output, int index)
        {
            bool needParm = CheckParm(index);
            if (needParm) output.Write('(');
            WriteArgumentOrPop(output, index);
            if (needParm) output.Write(')');
        }
        public void WriteParmLua(ITextOutput output, int index)
        {
            bool needParm = CheckParm(index);
            if (needParm) output.Write('(');
            WriteArgumentLua(output, index);
            if (needParm) output.Write(')');
        }
        public void WriteExpressionLua(ITextOutput output)
        {
            int count = Code.getOpTreeCount(); // not a leaf
            string operation = Code.getOpTreeString();
            if (count == 1)
            {
                if (Code == GMCode.Not) operation = "not ";
                output.Write(operation);
                WriteParmLua(output, 0);
            }
            else if (count == 2)
            {
                if (Code == GMCode.LogicAnd) operation = " and ";
                else if (Code == GMCode.LogicOr) operation = " or ";
                else if (Code == GMCode.Sne) operation = "~=";
                WriteParmLua(output, 0);
                output.Write(' ');
                output.Write(Code.getOpTreeString());
                output.Write(' ');
                WriteParmLua(output, 1);
            }
        }
        public void WriteExpression(ITextOutput output)
        {
            int count = Code.getOpTreeCount(); // not a leaf
            if (count == 1)
            {
                output.Write(Code.getOpTreeString());
                WriteParm(output, 0);
            }
            else if (count == 2)
            {
                WriteParm(output, 0);
                output.Write(' ');
                output.Write(Code.getOpTreeString());
                output.Write(' ');
                WriteParm(output, 1);
            }
        }
        // have to override due to issues with diffrent WriteTos
        public void ToString(StringBuilder sb)
        {
            sb.Append(Code.ToString());
            sb.Append(" ");
            if (Operand != null)
            {
                sb.Append("(Operand= ");
                ToStringOperand(sb, true);
                sb.Append(") ");
            }
            if (Arguments.Count > 0)
            {
                sb.Append("(Arguments= ");
                for (int i = 0; i < Arguments.Count; i++)
                {
                    if (i != 0) sb.Append(", ");
                    Arguments[i].ToString(sb);
                }
                sb.Append(") ");
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            ToString(sb);
            return sb.ToString();
        }
        public void DebugWriteTo(ITextOutput output)
        {
            output.Write(Code.ToString());
            output.Write(" ");
            if (Operand != null) WriteOperand(output, true);
            if (Arguments.Count > 0) WriteCommaArguments(output);
        }
        void InternalWriteTo(ITextOutput output)
        {
            switch (Code)
            {
                case GMCode.Constant: // primitive c# type
                    WriteOperand(output);
                    break;
                case GMCode.Var:  // should be ILVariable
                    {
                        ILVariable v = Operand as ILVariable;
                         v.WriteTo(output);
                    }
                    break;
            
                case GMCode.Pop:
                    if (Operand == null) output.Write(POPDefaultString);
                    else
                    {
                        output.Write("Pop(");
                        WriteOperand(output, false);// generic, string name
                        output.Write(" = ");
                        output.Write(POPDefaultString);
                        output.Write(")");
                    }

                    break;
                case GMCode.Assign:
                    WriteArgumentOrPop(output, 0, false);
                    output.Write(" = ");
                    WriteArgumentOrPop(output, 1, true);
                    break;
                case GMCode.Popz:
                    output.Write("Popz");
                    break;
                case GMCode.Push:
                    output.Write("Push ");
                    if (Operand != null)
                    {
                        output.Write("(Operand=");
                        WriteOperand(output, true);
                        output.Write(")");
                    }
                    if (Arguments.Count > 0)
                    {
                        output.Write("(Arguments=");
                        WriteArgumentOrPop(output, 0);
                        output.Write(")");
                    }
                    break;
                case GMCode.Dup:
                    output.Write("Dup ");
                    output.Write(Operand.ToString());
                    break;
                case GMCode.B: // this is where the magic happens...woooooooooo
                    output.Write("(B) ");
                    WriteOperand(output);
                    break;
                case GMCode.Bf:
                    output.Write("(Bf) ");
                    if (Arguments.Count > 0)
                        Arguments[0].WriteTo(output); 
                    else
                        output.Write(" POP ");
                    output.Write(" :: ");
                    WriteOperand(output);
                    break;
                case GMCode.Bt:
                    output.Write("(Bt) ");
                    if (Arguments.Count > 0)
                        Arguments[0].WriteTo(output);
                    else
                        output.Write(" POP ");
                    output.Write(" :: ");
                    WriteOperand(output);
                    break;
                case GMCode.Pushenv:
                    output.Write("PushEnviroment(");
                    WriteArgumentOrPop(output, 0, false);
                    output.Write(") : ");
                    WriteOperand(output, false);
                    break;
                case GMCode.Popenv:
                    output.Write("PopEnviroment ");
                    WriteOperand(output);
                    break;
                case GMCode.Exit: // exit without
                    output.Write("return; // exit");
                    return;
                case GMCode.Ret:
                    output.Write("return ");
                    WriteArgumentOrPop(output, 0);
                    break;
                case GMCode.LoopOrSwitchBreak:
                    output.Write("break");
                    break;
                case GMCode.LoopContinue:
                    output.Write("continue");
                    break;
                case GMCode.DefaultCase:
                    output.Write("default: goto ");
                    WriteOperand(output);
                    break;
                case GMCode.Case:
                    output.Write("case ");
                    WriteArgumentOrPop(output, 0);
                    output.Write(": goto ");
                    WriteOperand(output);
                    break;
                case GMCode.Switch: // debug print of the created switch statement
                    output.Write("switch(");
                    WriteArgument(output, 0);
                    output.Write(") {");
                    output.WriteLine();
                    output.Indent();
                    for (int i = 1; i < Arguments.Count; i++)
                    {
                        WriteArgument(output, i);
                        output.Write(';');
                        output.WriteLine();
                    }
                    output.Unindent();
                    output.Write("}");
                    output.WriteLine();
                    break;
             
                default:
                    throw new Exception("Not Implmented! ugh");
            }
        }
        public override void WriteToLua(ITextOutput output)
        {
            if (Code.isExpression())
                WriteExpressionLua(output);
            else
            {
                switch(Code)
                {
                    case GMCode.Call:
                        {
                            ILCall call = Operand as ILCall;
                            if (call != null) call.WriteToLua(output);
                            else
                            {
                                output.Write(Operand.ToString());
                                WriteCommaArguments(output);
                            }
                        }
                            break;
                    case GMCode.Constant: // primitive c# type
                        WriteOperand(output);
                        break;
                    case GMCode.Var:  // should be ILVariable
                        {
                            ILVariable v = Operand as ILVariable;
                            v.WriteToLua(output);
                        }
                        break;
                    case GMCode.Exit:
                        output.Write("return -- Exit");
                        break;
                    case GMCode.Ret:
                        output.Write("return ");
                        WriteArgumentsLua(output, 0);
                        break;
                    case GMCode.LoopOrSwitchBreak:
                        output.Write("break");
                        break;
                    case GMCode.LoopContinue:
                        output.Write("continue");
                        break;
                    default:
                        throw new Exception("Not Implmented! ugh");
                }
            }
            if (Comment != null) output.Write("--[[ " + Comment + "--]]");
        }
        public override void WriteTo(ITextOutput output)
        {
            if (Code.isExpression())
                WriteExpression(output);
            else
            {
                switch (Code)
                {
                    case GMCode.Call:
                        {
                            ILCall call = Operand as ILCall;
                            if (call != null) call.WriteTo(output);
                            else
                            {
                                output.Write(Operand.ToString());
                                WriteCommaArguments(output);
                            }
                        }
                        break;
                    default:
                        InternalWriteTo(output);
                        break;
                }
            }
 
            if (Comment != null) output.Write("/* " + Comment + " */");
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
        public override void WriteToLua(ITextOutput output)
        {
            output.Write("while ");
            if (this.Condition != null)
                this.Condition.WriteToLua(output);
            else
                output.Write("true");
            output.WriteLine(" do");
            this.BodyBlock.WriteToLua(output);
            output.Write("end");
        }
        public override void WriteTo(ITextOutput output)
        {
            output.Write("while(");
            if (this.Condition != null)
                this.Condition.WriteTo(output);
            else
                output.Write("true");
            output.Write(") ");
            this.BodyBlock.WriteTo(output);
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

        public override void WriteTo(ITextOutput output)
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                ILCondition condition = Conditions[i];
                if (i == 0) output.Write("if("); // starting if
                else output.Write("else if( ");
                condition.WriteToLua(output);
                output.WriteLine(" )");
                if (condition.TrueBlock.Body.Count == 0) output.WriteLine(" /* Empty Block */ ");
                else condition.TrueBlock.Body.WriteNodes(output,true,true);
                Debug.Assert(condition.FalseBlock == null); // if not we fucked up somewhere
            }
            if (Else != null)
            {
                output.WriteLine("else");
                if (Else.Body.Count == 0) output.WriteLine(" /* Empty Block */ ");
                else Else.Body.WriteNodes(output, true, true);
            }
          //  output.WriteLine("end");
        }

        public override void WriteToLua(ITextOutput output)
        {
            for(int i=0;i < Conditions.Count; i++)
            {
                ILCondition condition = Conditions[i];
                if (i==0) output.Write("if "); // starting if
                else output.Write("elseif ");
                // have to manualy write the condition
                condition.Condition.WriteToLua(output);
                output.WriteLine(" then");
                if (condition.TrueBlock.Body.Count == 0) output.WriteLine(" [[-- Empty Block --]] ");
                else condition.TrueBlock.Body.WriteLuaNodes(output, true);
                Debug.Assert(condition.FalseBlock == null); // if not we fucked up somewhere
            }
            if(Else != null)
            {
                output.WriteLine("else");
                if (Else.Body.Count == 0) output.WriteLine(" [[-- Empty Block --]] ");
                else Else.Body.WriteLuaNodes(output, true);
            }
            output.WriteLine("end");
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
        public override void WriteToLua(ITextOutput output)
        {
            output.Write("if ");
            Condition.WriteToLua(output);
            output.WriteLine(" then ");
            if (TrueBlock.Body.Count == 0) output.WriteLine(" [[-- Empty Block --]] ");
            else TrueBlock.Body.WriteLuaNodes(output, true);
            if (FalseBlock != null && FalseBlock.Body.Count > 0)
            {
                output.WriteLine("else");
                FalseBlock.Body.WriteLuaNodes(output, true);
            }
            output.Write("end");
        }

        public override void WriteTo(ITextOutput output)
        {
            output.Write("if (");
            Condition.WriteTo(output);
            output.Write(" ) ");
            if (TrueBlock.Body.Count == 0) output.Write(" [[-- Empty Block --]] ");
            else TrueBlock.Body.WriteNodes(output, true, true);
            if (FalseBlock != null && FalseBlock.Body.Count >0)
            {
                output.Write("else");
                if (FalseBlock.Body.Count == 0) output.Write("{ /* Empty Block */ }");
                else FalseBlock.Body.WriteNodes(output, true, true);
            }
        }
    }

    public class ILSwitch : ILNode
    {
        public class CaseBlock : ILBlock
        {
            public List<ILExpression> Values;  // null for the default case

            public override void WriteTo(ITextOutput output)
            {
                if (this.Values != null)
                {
                    foreach (var i in this.Values)
                    {
                        output.Write("case ");
                        i.WriteTo(output);
                        output.Write(": ");
                    }
                }
                else
                {
                    output.Write("default: ");
                }
                // make sure there is a writeline
                if (!base.Body.WriteNodes(output, true, false)) output.WriteLine();


            }
            public override void WriteToLua(ITextOutput output)
            {
                output.Indent();

                for (int i = 0; i < Body.Count - 1; i++)
                {
                    ILNode n = Body[i];
                    n.WriteToLua(output);
                    output.WriteLine();
                }
                ILExpression e = Body.Last() as ILExpression;
                if (e == null || e.Code != GMCode.LoopOrSwitchBreak) // don't print the last break
                {
                    Body.Last().WriteToLua(output);
                    output.WriteLine();
                }
                output.Unindent();

            }
        }

        public ILExpression Condition;
        public List<CaseBlock> CaseBlocks = new List<CaseBlock>();

        public override IEnumerable<ILNode> GetChildren()
        {
            if (this.Condition != null)
                yield return this.Condition;
            foreach (ILBlock caseBlock in this.CaseBlocks)
            {
                yield return caseBlock;
            }
        }

        public override void WriteToLua(ITextOutput output)
        {
            for(int i=0; i< CaseBlocks.Count;i++)
            {
                CaseBlock c = CaseBlocks[i];
                if (c.Values == null) output.WriteLine("else"); // default
                else
                {
                    if (i == 0) output.Write("if ");
                    else output.Write("elseif ");
                    Condition.WriteToLua(output);
                    output.Write(" == ");
                    c.Values[0].WriteToLua(output);
                    if(c.Values.Count > 1)
                    {
                        for (int j = 1; j < c.Values.Count; j++)
                        {
                            output.Write(" and ");
                            Condition.WriteToLua(output);
                            output.Write(" == ");
                            c.Values[j].WriteToLua(output);
                        }
                    }
                    output.WriteLine(" then");
                }
                c.WriteToLua(output);
            }
            output.WriteLine("end");
        }
        public override void WriteTo(ITextOutput output)
        {
            output.Write("switch (");
            Condition.WriteTo(output);
            output.WriteLine(") {");
            output.Indent();
            foreach (CaseBlock caseBlock in this.CaseBlocks) caseBlock.WriteTo(output);
            output.Unindent();
            output.WriteLine("}");
        }
    }

    public class ILWithStatement : ILNode
    {
        public static int withVars = 0;
        public ILBlock Body = new ILBlock();
        public ILExpression Enviroment;
        public string EnviromentName = null;
        public override IEnumerable<ILNode> GetChildren()
        {
            if (Enviroment != null) yield return Enviroment;
            if (this.Body != null)
                yield return this.Body;
        }
        // workaround.  Its hacky but I don't have ot modify much
        public override void WriteToLua(ITextOutput output)
        {
            // UGH Now I see why you use withs
            // This cycles though EACH object named in this instance, so it really IS a loop
            string local_value = "w_" + withVars++;
            string enviromentName;
            if (EnviromentName != null) enviromentName = EnviromentName;
            else
            {
                using (StringWriter w = new StringWriter())
                {
                    Enviroment.WriteToLua(new PlainTextOutput(w)); // hackery because of the ToString expression override
                    enviromentName = w.ToString();
                }
            }
            output.WriteLine("for _,{0} in with({1}) do", local_value, enviromentName);
            output.Indent();
            string old_enviroment = EnviromentOverride;
            EnviromentOverride = local_value; // override the enviroment name
            Body.Body.WriteLuaNodes(output, false);
            EnviromentOverride = old_enviroment;
            output.Unindent();
            output.WriteLine("end -- with({0}) end", enviromentName);           
        }
        public override void WriteTo(ITextOutput output)
        {
            output.Write("with(");
            Enviroment.WriteTo(output);
            output.Write(") ");
            Body.Body.WriteNodes(output, true, true);
        }
    }


    public class ILTryCatchBlock : ILNode
    {
        public class CatchBlock : ILBlock
        {
            public ILVariable ExceptionVariable;


            public override void WriteTo(ITextOutput output)
            {
                output.Write("catch ");
                if (ExceptionVariable != null)
                {
                    output.Write(' ');
                    output.Write(ExceptionVariable.Name);
                }
                output.WriteLine(" {");
                output.Indent();
                base.WriteTo(output);
                output.Unindent();
                output.WriteLine("}");
            }
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

        public override void WriteTo(ITextOutput output)
        {
            output.WriteLine(".try {");
            output.Indent();
            TryBlock.WriteTo(output);
            output.Unindent();
            output.Write("}");
            foreach (CatchBlock block in CatchBlocks)
            {
                block.WriteTo(output);
            }
            if (FaultBlock != null)
            {
                output.WriteLine("fault {");
                output.Indent();
                FaultBlock.WriteTo(output);
                output.Unindent();
                output.Write("}");
            }
            if (FinallyBlock != null)
            {
                output.WriteLine("finally {");
                output.Indent();
                FinallyBlock.WriteTo(output);
                output.Unindent();
                output.Write("}");
            }
        }

        public override void WriteToLua(ITextOutput output)
        {
            throw new NotImplementedException();
        }
    }
}

