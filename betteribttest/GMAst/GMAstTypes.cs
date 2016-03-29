using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.GMAst
{
    public abstract class ILNode
    {
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

        public List<ILNode> Body;

        public ILBlock(params ILNode[] body)
        {
            this.Body = new List<ILNode>(body);
        }

        public ILBlock(List<ILNode> body)
        {
            this.Body = body;
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
                output.WriteLine();
            }
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

        public override void WriteTo(ITextOutput output)
        {
            foreach (ILNode child in this.GetChildren())
            {
                child.WriteTo(output);
                output.WriteLine();
            }
        }
    }

    public class ILLabel : ILNode
    {
        public string Name;

        public override void WriteTo(ITextOutput output)
        {
            output.WriteDefinition(Name + ":", this);
        }
    }
    

    public class ILVariable
    {
        public string Name;
        public bool IsGenerated;


        public override string ToString()
        {
            return Name;
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
    public enum ILCode
    {
        Assign,
        Push,
        Call,
        Expression,
        Branch,
        BranchTrue,
        BranchFalse
    }

    public class ILExpression : ILNode
    {
        public ILCode Code { get; set; }
        public object Operand { get; set; }
        public List<ILExpression> Arguments { get; set; }
        // Mapping to the original instructions (useful for debugging)
        public List<ILRange> ILRanges { get; set; }

        public GM_Type ExpectedType { get; set; }
        public GM_Type InferredType { get; set; }

        public static readonly object AnyOperand = new object();

        public ILExpression(ILCode code, object operand, List<ILExpression> args)
        {
            if (operand is ILExpression)
                throw new ArgumentException("operand");

            this.Code = code;
            this.Operand = operand;
            this.Arguments = new List<ILExpression>(args);
            this.ILRanges = new List<ILRange>(1);
        }

        public ILExpression(ILCode code, object operand, params ILExpression[] args)
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
        public override void WriteTo(ITextOutput output)
        {
            switch (Code)
            {
                case ILCode.Call:
                    output.Write(((ILVariable)Operand).Name);
                    ArgumentWriteTo(output);
                    break;
                case ILCode.Assign:
                    output.Write(((ILVariable)Operand).Name);
                    output.Write(" = ");
                    Arguments.First().WriteTo(output);
                    break;
                case ILCode.Push:
                    output.Write("Push ");
                    output.Write(Operand.ToString()); // generic, should cover all cases
                    break;
                case ILCode.Expression:
                    output.Write(Operand.ToString()); // generic, should cover all cases
                    break;
                case ILCode.Branch:
                    output.Write("Branch ");
                    OperandWriteTo(output);
                    break;
                case ILCode.BranchFalse:
                    output.Write("BranchFalse ");
                    OperandWriteTo(output);
                    break;
                case ILCode.BranchTrue:
                    output.Write("BranchTrue ");
                    OperandWriteTo(output);
                    break;
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
            output.Write("loop (");
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
            output.WriteLine(") {");
            output.Indent();
            TrueBlock.WriteTo(output);
            output.Unindent();
            output.Write("}");
            if (FalseBlock != null)
            {
                output.WriteLine(" else {");
                output.Indent();
                FalseBlock.WriteTo(output);
                output.Unindent();
                output.WriteLine("}");
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
        public ILBlock BodyBlock;
        public ILExpression Enviroment;
        public override IEnumerable<ILNode> GetChildren()
        {
            if(Enviroment != null) yield return Enviroment;
            if (this.BodyBlock != null)
                yield return this.BodyBlock;
        }

        public override void WriteTo(ITextOutput output)
        {
            output.Write("with (");
            Enviroment.WriteTo(output);
            output.WriteLine(") {");
            output.Indent();
            this.BodyBlock.WriteTo(output);
            output.Unindent();
            output.WriteLine("}");
        }
    }
}
