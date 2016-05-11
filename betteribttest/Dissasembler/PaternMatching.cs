using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker.Dissasembler
{
    public static class PatternMatching {


        public static ILExpression NegateCondition(this ILExpression expr)
        {
            Debug.Assert(expr != null);
            Debug.Assert(expr.Code != GMCode.Push); // We don't handle pushes
            switch (expr.Code)
            {
                case GMCode.Not:
                    return expr.Arguments.Single(); // VERY simple, remove the negate
                case GMCode.Constant:
                case GMCode.Var:
                case GMCode.Call:
                    return new ILExpression(GMCode.Not, null, expr); // VERY simple, add a not

                case GMCode.Seq: expr.Code = GMCode.Sne; return expr;
                case GMCode.Sne: expr.Code = GMCode.Seq; return expr;
                case GMCode.Sgt: expr.Code = GMCode.Sle; return expr;
                case GMCode.Sge: expr.Code = GMCode.Slt; return expr;
                case GMCode.Slt: expr.Code = GMCode.Sge; return expr;
                case GMCode.Sle: expr.Code = GMCode.Sgt; return expr;
                // this is complcated as we have to negate the left and right side too
                case GMCode.LogicAnd:
                case GMCode.LogicOr:
                    expr.Code = expr.Code == GMCode.LogicOr ? GMCode.LogicAnd : GMCode.LogicOr;
                    expr.Arguments[0] = NegateCondition(expr.Arguments[0]);
                    expr.Arguments[1] = NegateCondition(expr.Arguments[1]);
                    return expr;
                case GMCode.Neg:
                    throw new Exception("Error, cannot logic negate a neg");
                default:
                    // might be math that assigns zero
                    if (expr.Code.isExpression()) // if it is then lets make it equal zero
                    {
                        return new ILExpression(GMCode.Seq, null, expr, new ILExpression(GMCode.Constant, new ILValue((short) 0)));
                    }
                    throw new Exception("Error, cannot logic negate a this code");
            }
        }
        public static void WriteLuaNodes<T>(this IList<T> nodes, ITextOutput output, int start, int count, bool ident = true) where T : ILNode
        {
            // output.WriteLine();
            if (ident) output.Indent();
            for (; start < count; start++)
            {
                ILNode n = nodes[start];
                n.WriteToLua(output);
                output.WriteLine();
            }
            if (ident) output.Unindent();
        }
        public static bool WriteNodes<T>(this IList<T> nodes, ITextOutput output, int start, int count, bool endingSemiColon, bool withBrackets) where T : ILNode
        {
            // Generic method to print a list of nodes
            if (nodes.Count == 0 || count == 0) return false;
            else if ((nodes.Count - start) == 1 || count == 1)
            {
                nodes[start].WriteTo(output);
                if (endingSemiColon) output.Write(';');
                return false;
            }
            else
            {
                if (withBrackets) output.Write('{');
                output.WriteLine();
                output.Indent();
                for (; start < count; start++)
                {
                    ILNode n = nodes[start];
                    n.WriteTo(output);
                    if (endingSemiColon && n is ILExpression) output.Write(';');
                    output.WriteLine();
                }
                output.Unindent();
                if (withBrackets) output.Write('}');
                return true; // we did a writeline, atleast one
            }
        }
        public static bool WriteNodes<T>(this IList<T> nodes, ITextOutput output, int start, bool endingSemiColon, bool withBrackets) where T : ILNode
        {
            return nodes.WriteNodes(output, start, nodes.Count - start, endingSemiColon, withBrackets);
        }
        public static bool WriteNodes<T>(this IList<T> nodes, ITextOutput output, bool endingSemiColon, bool withBrackets) where T : ILNode
        {
            return nodes.WriteNodes(output, 0, nodes.Count, endingSemiColon, withBrackets);
        }
        public static void WriteLuaNodes<T>(this IList<T> nodes, ITextOutput output, int start, bool ident = true) where T : ILNode
        {
            nodes.WriteLuaNodes(output, start, nodes.Count - start, ident);
        }
        public static void WriteLuaNodes<T>(this IList<T> nodes, ITextOutput output, bool ident = true) where T : ILNode
        {
            nodes.WriteLuaNodes(output, 0, nodes.Count, ident);
        }
        public static void CollectLabels(this ILExpression node, HashSet<ILLabel> labels)
        {
            ILLabel label = node.Operand as ILLabel;
            if (label != null) labels.Add(label);
            foreach (var e in node.Arguments) e.CollectLabels(labels);
        }
        public static void CollectLabels<T>(this IList<T> nodes, HashSet<ILLabel> labels) where T : ILNode
        {
            foreach (var n in nodes)
            {
                ILExpression e = n as ILExpression;
                if (e != null) { e.CollectLabels(labels); continue; }
                ILLabel label = n as ILLabel;
                if (label != null) { labels.Add(label); continue; }
            }
        }
        public static ILExpression WithILRanges(this ILExpression expr, IEnumerable<ILRange> ilranges)
        {
            expr.ILRanges.AddRange(ilranges);
            return expr;
        }
        public static bool MatchConstant(this ILNode node, GM_Type type, out ILValue value)
        {
            ILValue ret;
            if(node.MatchConstant(out ret) && ret.Type == type)
            {
                value = ret;
                return true;
            }
            value = default(ILValue);
            return false;
        }
        public static bool MatchConstant(this ILNode node,  out ILValue value)
        {
            ILValue ret = node as ILValue;
            if (ret == null && !node.Match(GMCode.Constant, out ret))
            {
                value = default(ILValue);
                return false;
            }
            value = ret;
            return true;
        }
        public static ILLabel GotoLabel(this ILBasicBlock bb)
        {
            ILLabel label = (bb.Body[bb.Body.Count - 1] as ILExpression).Operand as ILLabel;
            Debug.Assert(label != null);
            return label;
        }
        public static ILLabel EntryLabel(this ILBasicBlock bb)
        {
            ILLabel label = bb.Body[0]  as ILLabel;
            Debug.Assert(label != null);
            return label;
        }
        public static bool MatchCall(this ILNode node, GM_Type type, out ILExpression call)
        {
            ILCall c = node as ILCall;
            if(c != null && c.Type == type)
            {
                call = new ILExpression(GMCode.Call, c);
                return true;
            }
            ILExpression e = node as ILExpression;
            if(e != null && e.Code == GMCode.Call)
            {
                c = e.Operand as ILCall;
                if (c != null && c.Type == type)
                {
                    call = new ILExpression(GMCode.Call, c);
                    return true;
                } else if(e.Operand is string && e.InferredType == type)
                {
                    call = e;
                    return true;
                }
            }
            call = default(ILExpression);
            return false;
        }
        public static bool MatchCall(this ILNode node, GM_Type type)
        {
            ILExpression call;
            return node.MatchCall(type, out call);
        }
        public static bool MatchConstant(this ILNode node, GM_Type type)
        {
            ILValue ret;
            if(node.MatchConstant(type,out ret) || node.MatchCall(type)) // try to match it on a call
            {
                return true;
            }
            return false;
        }
        public static bool MatchConstant<T>(this ILNode node, GM_Type type, out T value)
        {
            ILValue ret;
            if (node.MatchConstant(type, out ret) && ret.Value is T)
            {
                value = (T) ret.Value;
                return true;
            }
            value = default(T);
            return false;
        }

        public static bool MatchIntConstant(this ILNode node, out ILValue value)
        {
            ILValue ret;
            if (node.MatchConstant(out ret) && (ret.Type == GM_Type.Short || ret.Type == GM_Type.Int))
            {
                value = ret;
                return true;
            }
            value = default(ILValue);
            return false;
        }
        public static bool MatchIntConstant(this ILNode node, out int value)
        {
            ILValue ret;
            if (node.MatchConstant(out ret) && (ret.Type == GM_Type.Short || ret.Type == GM_Type.Int))
            {
                value = (int)ret.Value;
                return true;
            }
            value = default(int);
            return false;
        }
        public static bool MatchConstant<T>(this ILNode node, out T value)
        {
            ILValue ret;
            if (node.MatchConstant(out ret) && ret.Value is T)
            {
                value = (T)ret.Value;
                return true;
            }
            value = default(T);
            return false;
        }
        public static bool Match(this ILNode node, GMCode code)
        {
            ILExpression expr = node as ILExpression;
            return expr != null && expr.Code == code;
        }

        public static bool Match<T>(this ILNode node, GMCode code, out T operand)
        {
            ILExpression expr = node as ILExpression;
            if (expr != null && expr.Code == code && expr.Arguments.Count == 0)
            {
                if (expr.Operand is T)
                {
                    operand = (T) expr.Operand;
                    return true;
                }
                // special case.  Tired of doing Match(code,ivalue) ivlaue is etc
                ILValue v = expr.Operand as ILValue;
                if (v.Value is T) 
                {
                    operand = (T) v.Value;
                    return true;
                }
            }
            operand = default(T);
            return false;
        }
        public static bool Match(this ILNode node, GMCode code, out List<ILExpression> args)
        {
            ILExpression expr = node as ILExpression;
            if (expr != null && expr.Code == code)
            {
                Debug.Assert(expr.Operand == null);
                args = expr.Arguments;
                return true;
            }
            args = null;
            return false;
        }

        public static bool Match(this ILNode node, GMCode code, out ILExpression arg)
        {
            List<ILExpression> args;
            if (node.Match(code, out args) && args.Count == 1)
            {
                arg = args[0];
                return true;
            }
            arg = null;
            return false;
        }

        public static bool Match<T>(this ILNode node, GMCode code, out T operand, out List<ILExpression> args)
        {
            ILExpression expr = node as ILExpression;
            if (expr != null && expr.Code == code)
            {
                operand = (T)expr.Operand;
                args = expr.Arguments;
                return true;
            }
            operand = default(T);
            args = null;
            return false;
        }

        public static bool Match<T>(this ILNode node, GMCode code, out T operand, out ILExpression arg)
        {
            List<ILExpression> args;
            if (node.Match(code, out operand, out args) && args.Count == 1)
            {
                arg = args[0];
                return true;
            }
            arg = null;
            return false;
        }

        public static bool Match<T>(this ILNode node, GMCode code, out T operand, out ILExpression arg1, out ILExpression arg2)
        {
            List<ILExpression> args;
            if (node.Match(code, out operand, out args) && args.Count == 2)
            {
                arg1 = args[0];
                arg2 = args[1];
                return true;
            }
            arg1 = null;
            arg2 = null;
            return false;
        }

        public static bool MatchSingle<T>(this ILBasicBlock bb, GMCode code, out T operand, out ILExpression arg)
        {
            if (bb.Body.Count == 2 &&
                bb.Body[0] is ILLabel &&
                bb.Body[1].Match(code, out operand, out arg))
            {
                return true;
            }
            operand = default(T);
            arg = null;
            return false;
        }
        public static bool MatchSingle<T>(this ILBasicBlock bb, GMCode code, out T operand)
        {
            if (bb.Body.Count == 2 &&
                bb.Body[0] is ILLabel &&
                bb.Body[1].Match(code, out operand))
            {
                return true;
            }
            operand = default(T);
            return false;
        }
        public static bool MatchAt<T>(this ILBasicBlock bb, int index, GMCode code, out T operand, out ILExpression arg)
        {
            if (bb.Body.ElementAtOrDefault(index).Match(code, out operand, out arg)) return true;
            operand = default(T);

            return false;
        }
        public static bool MatchAt<T>(this ILBasicBlock bb, int index, GMCode code, out T operand)
        {
            if (bb.Body.ElementAtOrDefault(index).Match(code, out operand)) return true;
            operand = default(T);
            return false;
        }
        public static bool MatchAt(this ILBasicBlock bb, int index, GMCode code, out ILExpression arg)
        {
            if (bb.Body.ElementAtOrDefault(index).Match(code, out arg)) return true;
            arg = default(ILExpression);
            return false;
        }
        public static bool MatchAt(this ILBasicBlock bb, int index, GMCode code)
        {
            if (bb.Body.ElementAtOrDefault(index).Match(code)) return true;
            return false;
        }
        public static bool MatchSingleAndBr(this ILBasicBlock bb, GMCode code, out ILExpression arg, out ILLabel brLabel)
        {
            object filler;
            return bb.MatchSingleAndBr<object>(code, out filler, out arg, out brLabel);
        }

        public static bool MatchSingleAndBr<T>(this ILBasicBlock bb, GMCode code, out T operand, out ILLabel brLabel)
        {
            {
                if (bb.Body.Count == 3 &&
           bb.Body[0] is ILLabel &&
           bb.Body[1].Match(code, out operand) &&
           bb.Body[2].Match(GMCode.B, out brLabel))
                    return true;
            }
            operand = default(T);
            brLabel = null;
            return false;
        }
        public static bool MatchSingleAndBr<T>(this ILBasicBlock bb, GMCode code, out T operand, out ILExpression arg, out ILLabel brLabel)
        {
            {
                if (bb.Body.Count == 3 &&
           bb.Body[0] is ILLabel &&
           bb.Body[1].Match(code, out operand, out arg) &&
           bb.Body[2].Match(GMCode.B, out brLabel))
                    return true;
            }
            operand = default(T);
            arg = null;
            brLabel = null;
            return false;
        }
        public static bool MatchLastAt<T>(this ILBasicBlock bb, int back, GMCode code, out T operand, out ILExpression arg)
        {
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - back).Match(code,  out operand, out arg)) return true;
            arg = default(ILExpression);
            operand = default(T);
            return false;
        }
        public static bool MatchLastAt<T>(this ILBasicBlock bb, int back, GMCode code, out T operand)
        {
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - back).Match(code, out operand)) return true;
            operand = default(T);
            return false;
        }
        public static bool MatchLastAt(this ILBasicBlock bb, int back, GMCode code, out ILExpression arg)
        {
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - back).Match(code, out arg)) return true;
            arg = default(ILExpression);
            return false;
        }
        public static bool MatchLastAt(this ILBasicBlock bb, int back, GMCode code)
        {
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - back).Match(code)) return true;
            return false;
        }
        public static bool MatchLastAndBr<T>(this ILBasicBlock bb, GMCode code, out T operand, out ILLabel brLabel)
        {
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - 2).Match(code, out operand) &&
              bb.Body.LastOrDefault().Match(GMCode.B, out brLabel))
            {
                return true;
            }
            operand = default(T);
            brLabel = null;
            return false;
        }
        public static bool MatchLastAndBr(this ILBasicBlock bb, GMCode code, out ILExpression arg, out ILLabel brLabel)
        {
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - 2).Match(code, out arg) &&
              bb.Body.LastOrDefault().Match(GMCode.B, out brLabel))
            {
                return true;
            }
            arg = default(ILExpression);
            brLabel = null;
            return false;
        }
        public static bool MatchLastAndBr<T>(this ILBasicBlock bb, GMCode code, out T operand, out ILExpression arg, out ILLabel brLabel)
        {
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - 2).Match(code, out operand, out arg) &&
            bb.Body.LastOrDefault().Match(GMCode.B, out brLabel))
                return true;
            operand = default(T);
            arg = null;
            brLabel = null;
            return false;
        }
        public static bool MatchLastAndBr<T>(this ILBasicBlock bb, GMCode code, out T operand, out List<ILExpression> args, out ILLabel brLabel)
        {
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - 2).Match(code, out operand, out args) &&
                bb.Body.LastOrDefault().Match(GMCode.B, out brLabel))
            {
                return true;
            }
            operand = default(T);
            args = null;
            brLabel = null;
            return false;
        }


        public static bool isConstant(this ILExpression n)
        {
            return (n.Code == GMCode.Push && (n.Arguments.Single().Code == GMCode.Constant)) || n.Code == GMCode.Call;
        }
        public static void RemoveLast<T>(this List<T> a)
        {
            if (a.Count == 0) return;
            a.RemoveAt(a.Count - 1);
        }
        public static void RemoveLast<T>(this List<T> a, int count)
        {
            if (a.Count < count) a.Clear();
            else a.RemoveRange(a.Count - count, count);
        }

        public static bool MatchCaseBlock(this ILBasicBlock bb, out ILExpression caseCondition, out ILLabel caseLabel, out ILLabel nextCase)
        {
            int dupType = 0;
            ILExpression pushSeq;
            ILExpression btExpresion;
            if (bb.Body.Count == 6 &&
                bb.Body[0] is ILLabel &&
                bb.Body[1].Match(GMCode.Dup, out dupType) &&
                dupType == 0 &&
                bb.Body[2].Match(GMCode.Push, out caseCondition) &&
                 bb.Body[3].Match(GMCode.Seq) &&
                bb.MatchLastAndBr(GMCode.Bt, out caseLabel, out btExpresion, out nextCase)
                ) return true;
            caseCondition = default(ILExpression);
            caseLabel = nextCase = default(ILLabel);
            return false;
        }
    }
}
