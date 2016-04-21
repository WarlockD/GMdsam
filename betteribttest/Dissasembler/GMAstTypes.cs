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

namespace betteribttest.Dissasembler
{


    public abstract class ILNode
    {
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
            Body.WriteNodes(output, true,true);
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
            Body.WriteNodes(output, true,true);
        }
        public void WriteBlock(ITextOutput output, string BlockTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(BlockTitle)) output.WriteLine(BlockTitle);
            WriteTo(output);
            output.WriteLine(); // extra line
        }
        public void DebugSave(string filename,string fileHeader=null)
        {
            using (PlainTextOutput pto = new PlainTextOutput(new System.IO.StreamWriter(filename)))
            {
                WriteBlock(pto, fileHeader);
            }
        }
    }
    // We make this a seperate node so that it dosn't interfere with anything else
    // We are not trying to optimize game maker code here:P
    public class ILAssign : ILNode
    {
        public ILVariable Variable;
        public ILNode Expression;

        public override void WriteTo(ITextOutput output)
        {
            Variable.WriteTo(output);
            output.Write(" = ");
            Expression.WriteTo(output);
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
        public List<ILNode> Arguments = new List<ILNode>();
        public GM_Type Type = GM_Type.NoType; // return type
        public override void WriteTo(ITextOutput output)
        {
            output.Write(Name);
            output.Write('(');
            if (Arguments.Count > 0) Arguments[0].WriteTo(output);
            if(Arguments.Count > 1) {
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
        public ILValue(object o, GM_Type type) {
            if (o is short) this.Value = (int)((short)o);
            else if(type == GM_Type.String) this.ValueText = GMCodeUtil.EscapeString(o as string);
            else this.Value = o;
            Type = type;
        }
        public ILValue(ILVariable v) { this.Value = v; Type = GM_Type.Var; }
        public ILValue(ILValue v) { this.Value = v.Value; Type = v.Type; this.ValueText = v.ValueText; }
        public ILValue(ILExpression e) { this.Value = e; Type = GM_Type.ConstantExpression; }
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
        public static bool operator!=(ILValue c, int v)
        {
            return !(c == v);
        }
        public static bool operator==(ILValue c, int v)
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
        public string Name;
        public Label OldLabel = null;
        public object UserData = null; // usally old dsam label
        public bool isExit = false;
        public override void WriteTo(ITextOutput output)
        {
            output.WriteDefinition(Name + ":", this);
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
        // Side note, we "could" make this a node
        // but in reality this is isolated 
        // Unless I ever get a type/var anyisys system up, its going to stay like this
        public string Name;
        public ILNode Instance = null; // We NEED this
        public string InstanceName;
        public ILNode Index = null; // not null if we have an index
        public bool isArray;
        public bool isResolved = false; // resolved expresion, we don't have to do anything to it anymore
        public GM_Type Type = GM_Type.NoType;
        public override IEnumerable<ILNode> GetChildren()
        {
            if (Instance != null) yield return Instance;
            if (Index != null) yield return Index;
        }
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
            return Name.GetHashCode() ^ InstanceName.GetHashCode();
        }

        public override void WriteTo(ITextOutput output)
        {
            StringBuilder sb = new StringBuilder();
            if (InstanceName != null) output.Write(InstanceName);
            else if (Instance != null) Instance.WriteTo(output);
            else output.Write("stack");
            output.Write(".");
            output.Write(Name);
            if(isArray)
            {
                output.Write('[');
                if (Index != null) Index.WriteTo(output);
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
        public GMCode Code { get; set; }
        public int Extra { get; set; }
        public object Operand { get; set; }
        public List<ILExpression> Arguments;
        // Mapping to the original instructions (useful for debugging)
        public List<ILRange> ILRanges { get; set; }

        public GM_Type ExpectedType { get; set; }
        public GM_Type InferredType { get; set; }

        public static readonly object AnyOperand = new object();
        public ILExpression(ILExpression i, List<ILRange> range = null) // copy it
        {
            this.Code = i.Code;
            this.Operand = i.Operand; // don't need to worry about this
            this.Arguments = new List<ILExpression>(i.Arguments.Count);
            if(i.Arguments.Count!=0) foreach (var n in i.Arguments) this.Arguments.Add(new ILExpression(n));
            this.ILRanges = new List<ILRange>(range ?? i.ILRanges);
        }
        public ILExpression(GMCode code, object operand, List<ILExpression> args)
        {
            if (operand is ILExpression)
                throw new ArgumentException("operand");

            this.Code = code;
            this.Operand = operand;
            this.Arguments = new List<ILExpression>(args);
            this.ILRanges = new List<ILRange>(1);
        }

        public ILExpression(GMCode code, object operand, params ILExpression[] args)
        {
            if (operand is ILExpression)
                throw new ArgumentException("operand");

            this.Code = code;
            this.Operand = operand;
            this.Arguments = new List<ILExpression>(args);
            this.ILRanges = new List<ILRange>(1);
        }


        public override IEnumerable<ILNode> GetChildren()
        {
            return Arguments;
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
            for(int i=0;i < Arguments.Count; i++)
            {
                if (i!=0) output.Write(", ");
                WriteArgument(output, i, true);
            }
            output.Write(')');
        }
        bool CheckParm(int index)
        {
            ILExpression e = Arguments.ElementAtOrDefault(index);
            if (e == null) return false;
            switch (e.Code)
            {
                case GMCode.Call:
                case GMCode.Var:
                case GMCode.Constant:
                    return false;
                case GMCode.LogicOr: // hack to remove extra () around a bunch of logic ors
                    if (e.Code == GMCode.LogicAnd) return true;
                    else return false;
                case GMCode.LogicAnd:
                    if (e.Code == GMCode.LogicOr) return true;
                    else return false;
                default:
                    return true;
            }
        }
        void WriteOperand(ITextOutput output,bool escapeString=true)
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
            } else output.Write(Operand.ToString());
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
            if (arg.Code == GMCode.Constant|| arg.Code == GMCode.Var) arg.WriteOperand(output, escapeString);
            else arg.WriteTo(output); // don't know what it is
        }
        public void WriteArguments(ITextOutput output, int start)
        {
            Arguments.WriteNodes(output, start,true,true);
        }
        static readonly string POPDefaultString = "%POP%";
        public void WriteArgumentOrPop(ITextOutput output, int index, bool escapeString = true)
        {
            if (index < Arguments.Count) WriteArgument(output, index, escapeString);
            else output.Write(POPDefaultString);    
        }
        public void WriteParm(ITextOutput output, int index) {
            bool needParm = CheckParm(index);
            if (needParm) output.Write('(');
            WriteArgumentOrPop(output, index);
            if (needParm) output.Write(')');
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
        public override void WriteTo(ITextOutput output)
        {
            if(Code.isExpression())
            {
                WriteExpression(output);
             
            }
            else
            {
                switch (Code)
                {
                    case GMCode.Constant: // primitive c# type
                        WriteOperand(output);
                        break;
                    case GMCode.Var:  // should be ILVariable
                        if (Arguments.Count > 0) WriteArgument(output, 0, false);
                        else output.Write("stack");
                        output.Write(".");
                        WriteOperand(output, false);// generic, string name
                        if (Arguments.Count > 1) // its an array
                        {
                            output.Write('[');
                            WriteArgument(output, 1, false);
                            output.Write(']');
                        }
                        break;
                    case GMCode.Call:
                        output.Write(Operand.ToString());
                         WriteCommaArguments(output);

                        break;
                    case GMCode.Pop:
                        if(Operand == null) output.Write(POPDefaultString);
                        else {
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
                        if(Operand != null)
                        {
                            output.Write("(Operand=");
                            WriteOperand(output, true);
                            output.Write(")");
                        }
                        if (Arguments.Count >0)
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
                        output.Write("goto ");
                        WriteOperand(output);
                        break;
                    case GMCode.Bf:
                        if (Arguments.Count > 0)
                        {
                            output.Write("Push(");
                            Arguments[0].WriteTo(output);
                            output.Write(")");
                        }
                        output.Write("Branch IfFalse ");
                        WriteOperand(output);
                        break;
                    case GMCode.Bt:
                        if (Arguments.Count > 0)
                        {
                            output.Write("Push(");
                            Arguments[0].WriteTo(output);
                            output.Write(")");
                        }
                        output.Write("BranchIfTrue ");
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

        public override void WriteTo(ITextOutput output)
        {
            output.Write("if(");
            Condition.WriteTo(output);
            output.Write(") ");
            if (TrueBlock.Body.Count == 0) output.Write("{ /* Empty Block */ }");
            else TrueBlock.Body.WriteNodes(output,true,true);
            if (FalseBlock != null)
            {
                output.WriteLine(); // some of these conditions get to long so put a line here
                output.Write("else ");
                if (FalseBlock.Body.Count == 0) output.Write("{ /* Empty Block */ }");
                else FalseBlock.Body.WriteNodes(output,true,true);
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
                else {
                    output.Write("default: ");
                }
                // make sure there is a writeline
                if (!base.Body.WriteNodes(output, true, false)) output.WriteLine();
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
        public ILBlock Body = new ILBlock();
        public ILExpression Enviroment;
        public override IEnumerable<ILNode> GetChildren()
        {
            if (Enviroment != null) yield return Enviroment;
            if (this.Body != null)
                yield return this.Body;
        }

        public override void WriteTo(ITextOutput output)
        {
            output.Write("with(");
            Enviroment.WriteTo(output);
            output.Write(") ");
            Body.Body.WriteNodes(output,true,true);
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
    }
}
