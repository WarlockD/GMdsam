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

namespace betteribttest.GMAst
{


    public abstract class ILNode
    {
        // Wow, why havn't I been using Collection? seriously!
        public class ILList<T> : Collection<T> where T : ILNode
        {
            public ILNode Parent = null;
            public ILList(ILNode parent = null) : base()
            {
                this.Parent = parent;
            }
            static List<T> GetList(IList<T> check, bool copy = false)
            {
                ILList<T> testIL = check as ILList<T>;
                if (testIL != null) return GetList(testIL.Items, true); // evetualy we get to the list:P
                List<T> testList = check as List<T>;
                if (testList != null) return copy ? new List<T>(testList) : testList;
                T[] testArray = check as T[];
                if (testArray != null) return new List<T>(testArray);
                throw new Exception("Not supported IList type " + check.GetType().ToString());
            }
            public ILList(IList<T> nodes, ILNode parent = null) : base(GetList(nodes)) // make suer we have a clean list
            {
                this.Parent = parent;
                Relink();
            }
            // requires some carful thinking
            void Relink()
            {
                ILNode prev = null;
                foreach (var node in this)
                {
                    node._parent = Parent;
                    node._next = null;
                    if (prev != null)
                    {
                        node._previous = prev;
                        prev._next = node;
                    }
                    else node._previous = null;
                }
            }
            void ClearNode(ILNode node) { node._next = node._previous = node._parent = null; }
            protected override void ClearItems()
            {
                foreach (var node in this) ClearNode(node);
                base.ClearItems();
            }
            protected override void RemoveItem(int index)
            {
                ClearNode(this[index]);
                base.RemoveItem(index);
                Relink();
            }
            protected override void InsertItem(int index, T item)
            {
                base.InsertItem(index, item);
                Relink();
            }
            protected override void SetItem(int index, T item)
            {
                ClearNode(this[index]);
                base.SetItem(index, item);
                Relink();
            }
        }
        // The original idea was to use ILList<T> to handle the Parent as well, but you would
        // need make Inode fake a parrent in the case of condition or loops as you want the expression
        // and blocks to be the parrent.  You could also make the body have the list? humm
        // I will think of it latter, its easyer to modify GotoRemover to use the parrent
        // Howerver ILList DOES make sure that the collection class links the next and previous
        // lists
        protected ILNode _parent = null;
        ILNode _next = null;
        ILNode _previous = null;
        public virtual void SetParent(ILNode node)
        {
            _parent = node;
            foreach (var child in GetChildren()) child.SetParent(this);
        }

        public ILNode Parent { get { return _parent; } }
        public ILNode Next { get { return _next; } }
        public ILNode Previoius { get { return _previous; } }
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

    public class ILBlock : ILNode
    {
        public ILExpression EntryGoto;
        ILList<ILNode> _body;
        public IList<ILNode> Body
        {
            get { return _body; }
            set
            {
                if (_body != null) _body.Clear();
                _body = new ILList<ILNode>(value,this);
            }
        }
        public ILBlock(params ILNode[] body)
        {
            this.Body = new ILList<ILNode>(body.ToList(),this);
        }
        public ILBlock(List<ILNode> body)
        {
            this.Body = new ILList<ILNode>(body, this);
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
            foreach (ILNode child in this.GetChildren())
            {
                child.WriteTo(output);
                output.Write(';');
                output.WriteLine();
            }
        }
    }
    public class ILValue : IEquatable<ILValue>
    {
        public object Value { get; private set; }
        public GM_Type Type { get; private set; }
        public string ValueText = null;
        public ILValue(bool i) { this.Value = i; Type = GM_Type.Bool; }
        public ILValue(int i) { this.Value = i; Type = GM_Type.Int; }
        public ILValue(object i) { this.Value = i; Type = GM_Type.Var; }
        public ILValue(string i) { this.Value = i; Type = GM_Type.String; this.ValueText =  GMCodeUtil.EscapeString(i as string); }
        public ILValue(float i) { this.Value = i; Type = GM_Type.Float; }
        public ILValue(double i) { this.Value = i; Type = GM_Type.Double; }
        public ILValue(long i) { this.Value = i; Type = GM_Type.Long; }
        public ILValue(short i) { this.Value = i; Type = GM_Type.Short; }
        public ILValue(object o, GM_Type type) { this.Value = o; Type = type; }
        public ILValue(ILVariable v) { this.Value = v; Type = GM_Type.Var; }
        public ILValue(ILValue v) { this.Value = v.Value; Type = v.Type; this.ValueText = v.ValueText; }
        public ILValue(ILExpression e) { this.Value = e;  Type = GM_Type.ConstantExpression;  }
        public static ILValue FromInstruction(Instruction i)
        {
            Debug.Assert(i.Code == GMCode.Push && i.FirstType != GM_Type.Var);
            switch (i.FirstType)
            {
                case GM_Type.Double:
                case GM_Type.Float:
                case GM_Type.Int:
                case GM_Type.Long:
                case GM_Type.String:
                    return new ILValue(i.Operand, i.FirstType);
                case GM_Type.Short:
                    return new ILValue(i.Instance);
                default:
                    throw new Exception("Bad Type");
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
    }
    public static class ILNode_Helpers
    {
        public static bool TryParse(this ILValue node, out int value)
        {
            ILValue valueNode = node as ILValue;
            if (valueNode != null && (valueNode.Type == GM_Type.Short || valueNode.Type == GM_Type.Int))
            {
                value = valueNode.Type == GM_Type.Short ? (short)valueNode.Value : (int)valueNode.Value;
                return true;
            }
            value = 0;
            return false;
        }
    }
    public class ILBasicBlock : ILNode
    {
        /// <remarks> Body has to start with a label and end with unconditional control flow </remarks>
        ILList<ILNode> _body = new ILList<ILNode>();
        public IList<ILNode> Body
        {
            get { return _body; }
            set
            {
                _body.Clear();
                _body = new ILList<ILNode>(value,this);
            }
        }
        public ILBasicBlock()
        {
            _body = new ILList<ILNode>(this);
        }
        public override IEnumerable<ILNode> GetChildren()
        {
            return this.Body;
        }

        public override void WriteTo(ITextOutput output)
        {
            foreach (ILNode child in this.GetChildren())
            {
                child.WriteTo(output);
                output.Write(';');
                output.WriteLine();
            }
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
    public class ILVariable
    {
        // Side note, we "could" make this a node
        // but in reality this is isolated 
        // Unless I ever get a type/var anyisys system up, its going to stay like this
        public string Name;
        public ILExpression Instance = null; // We don't NEED this
        public string InstanceName;
        public ILExpression Index = null; // not null if we have an index
        public bool IsGenerated = false;
        public Instruction Pinned = null;
        public GM_Type Type = GM_Type.NoType;
        public override string ToString()
        {
            string ret = InstanceName + "." + Name;
            if (Index != null) ret += '[' + Index.ToString() + ']';
            return ret;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            ILVariable test = obj as ILVariable;
            return test != null && test.Name == Name && test.InstanceName == InstanceName;
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ InstanceName.GetHashCode();
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
        public object Operand { get; set; }
        ILList<ILExpression> _args;
        public IList<ILExpression> Arguments
        {
            get { return _args; }
            set
            {
                _args.Clear();
                _args = new ILList<ILExpression>(value,this);
            }
        }
        // Mapping to the original instructions (useful for debugging)
        public List<ILRange> ILRanges { get; set; }

        public GM_Type ExpectedType { get; set; }
        public GM_Type InferredType { get; set; }

        public static readonly object AnyOperand = new object();
        public ILExpression(ILExpression i) // copy it
        {
            this.Code = i.Code;
            this.Operand = i.Operand; // don't need to worry about this
            this._args = new ILList<ILExpression>(this);
            if(i.Arguments.Count!=0) foreach (var n in i.Arguments) this._args.Add(new ILExpression(n));
            this.ILRanges = i.ILRanges;
        }
        public ILExpression(GMCode code, object operand, List<ILExpression> args)
        {
            if (operand is ILExpression)
                throw new ArgumentException("operand");

            this.Code = code;
            this.Operand = operand;
            this._args = new ILList<ILExpression>(args, this);
            this.ILRanges = new List<ILRange>(1);
        }

        public ILExpression(GMCode code, object operand, params ILExpression[] args)
        {
            if (operand is ILExpression)
                throw new ArgumentException("operand");

            this.Code = code;
            this.Operand = operand;
            this._args = new ILList<ILExpression>(args, this);
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

        public IEnumerable<ILLabel> GetBranchTargets()
        {
            if (this.Operand is ILLabel)
            {
                yield return this.Operand as ILLabel ;
            }
            else if(Code == GMCode.Switch)
            {
                var list =  Arguments.Skip(1).Select(x => x.Operand as ILLabel).ToList();
                foreach (var l in list) yield return l;
            }
        }
        bool ArgumentWriteTo(ITextOutput output)
        {
            output.Write('(');
            bool first = true;

            foreach (ILExpression arg in this.Arguments)
            {
                if (!first) output.Write(", ");
                arg.WriteTo(output);
                first = false;
            }
            output.Write(')');
            return first;
        }
        bool OperandWriteTo(ITextOutput output)
        {
            bool first = true;
            if (Operand != null)
            {
                if (Operand is ILLabel)
                {
                    output.WriteReference(((ILLabel)Operand).Name, Operand);
                }
                else if (Operand is ILLabel[])
                {
                    ILLabel[] labels = (ILLabel[])Operand;
                    for (int i = 0; i < labels.Length; i++)
                    {
                        if (i > 0)
                            output.Write(", ");
                        output.WriteReference(labels[i].Name, labels[i]);
                    }
                }
                else {
                    output.Write(Operand.ToString());
                    //   DisassemblerHelpers.WriteOperand(output, Operand);
                }
                first = false;
            }
            return first;
        }
        static bool CheckParm(ILExpression node)
        {
            switch (node.Code)
            {
                case GMCode.Call:
                case GMCode.Var:
                case GMCode.Constant:
                    return false;
                default:
                    return true;
            }
        }
        void WriteOperand(ITextOutput output,bool escapeString=true)
        {
            if (Operand is ILLabel) output.Write((Operand as ILLabel).Name);
            else if (escapeString)
            {
                if (Operand is string)
                    output.Write(GMCodeUtil.EscapeString((string)Operand));
                else if (Operand is ILValue)
                {
                    ILValue val = Operand as ILValue;
                    if (val.Type == GM_Type.String) output.Write(GMCodeUtil.EscapeString((string)val.Value));
                    else output.Write(val.Value.ToString());
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
        void WriteArgument(ITextOutput output, int index, bool escapeString = true)
        {
            ILExpression arg = Arguments[index];
            if (arg.Code == GMCode.Constant) arg.WriteOperand(output, escapeString);
            else arg.WriteTo(output); // don't know what it is
        }
        static readonly string POPDefaultString = "%POP%";
        void WriteArgumentOrPop(ITextOutput output, int index, bool escapeString = true)
        {
            if (index < Arguments.Count) WriteArgument(output, index, escapeString);
            else output.Write(POPDefaultString);    
        }
        public override void WriteTo(ITextOutput output)
        {
            int count = Code.getOpTreeCount(); // not a leaf
            if (count == 1)
            {
                if (Arguments.Count != 0)
                {
                    output.Write(Code.getOpTreeString());
                    bool needParm = CheckParm(Arguments[0]);
                    if (needParm) output.Write('(');
                    WriteArgument(output, 0);
                    if (needParm) output.Write(')');
                } else
                {
                    output.Write(Code.GetName());
                    output.Write(' ');
                    output.Write(POPDefaultString); // fake
                }
               
            }
            else if (count == 2)
            {
                if(Arguments.Count != 0)
                {
                    bool needParm = CheckParm(Arguments[0]);
                    if (needParm) output.Write('(');
                    WriteArgument(output, 0);
                    if (needParm) output.Write(')');
                    output.Write(' ');
                    output.Write(Code.getOpTreeString());
                    output.Write(' ');
                    needParm = CheckParm(Arguments[1]);
                    if (needParm) output.Write('(');
                    WriteArgument(output, 1);
                    if (needParm) output.Write(')');
                }
                else
                {
                    output.Write(Code.GetName());
                    output.Write(' ');
                    output.Write(POPDefaultString); 
                    output.Write(", ");
                    output.Write(POPDefaultString); 
                }
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
                        ArgumentWriteTo(output);
                        break;
                    case GMCode.Pop:
                        if (Arguments.Count > 0) Arguments[0].WriteTo(output);
                        else output.Write(POPDefaultString);
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
                        Debug.Assert(Operand == null);
                        WriteArgumentOrPop(output, 0);
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
                        WriteArgumentOrPop(output, 0);
                        output.Write(")");
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
                    case GMCode.Case:
                        output.Write("case ");
                        WriteArgumentOrPop(output, 0);
                        Arguments.Single().WriteTo(output); // second bit
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
            output.WriteLine("");
            output.Write("while (");
            if (this.Condition != null)
                this.Condition.WriteTo(output);
            output.WriteLine(") {");
            output.Indent();
            this.BodyBlock.WriteTo(output);
            output.Unindent();
            output.WriteLine("}");
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
            output.Write("if (");
            Condition.WriteTo(output);
            if (TrueBlock.Body.Count == 0) output.Write("{ /* Empty Block */ }");
            else if (TrueBlock.Body.Count != 1 || TrueBlock.Body[0] is ILCondition)
            {
                output.WriteLine(") {");
                output.Indent();
                TrueBlock.WriteTo(output);
                output.Unindent();
                output.Write("}");
            }
            else
            {
                output.Write(") ");
                TrueBlock.Body[0].WriteTo(output);
            }

            if (FalseBlock != null)
            {
                if (FalseBlock.Body.Count == 0) output.Write("{ /* Empty Block */ }");
                else if (FalseBlock.Body.Count != 1 && FalseBlock.Body[0] is ILCondition)
                {
                    output.WriteLine(" else {");
                    output.Indent();
                    FalseBlock.WriteTo(output);
                    output.Unindent();
                    output.Write("}");
                }
                else
                {
                    output.Write(" else ");
                    FalseBlock.Body[0].WriteTo(output);
                }
            }
        }
    }

    public class ILSwitch : ILNode
    {
        public class CaseBlock : ILBlock
        {
            public List<int> Values;  // null for the default case

            public override void WriteTo(ITextOutput output)
            {
                if (this.Values != null)
                {
                    foreach (int i in this.Values)
                    {
                        output.WriteLine("case {0}:", i);
                    }
                }
                else {
                    output.WriteLine("default:");
                }
                output.Indent();
                base.WriteTo(output);
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

        public override void WriteTo(ITextOutput output)
        {
            output.Write("switch (");
            Condition.WriteTo(output);
            output.WriteLine(") {");
            output.Indent();
            foreach (CaseBlock caseBlock in this.CaseBlocks)
            {
                caseBlock.WriteTo(output);
            }
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
           // if (Enviroment != null) yield return Enviroment;
            if (this.Body != null)
                yield return this.Body;
        }

        public override void WriteTo(ITextOutput output)
        {
            output.Write("with (");
            Enviroment.WriteTo(output);
            output.WriteLine(") {");
            output.Indent();
            this.Body.WriteTo(output);
            output.Unindent();
            output.Write("}");
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
