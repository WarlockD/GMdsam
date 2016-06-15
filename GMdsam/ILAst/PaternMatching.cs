using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker.Ast
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
        public static void AddILRange(this ILExpression expr, IEnumerable<ILRange> ilranges)
        {
            expr.ILRanges.AddRange(ilranges);
        }
        public static void AddILRange(this ILExpression expr, int address)
        {
            expr.ILRanges.Add(new ILRange(address));
        }

        public static ILLabel GotoLabel(this ILBasicBlock bb)
        {
            ILLabel label = (bb.Body[bb.Body.Count - 1] as ILExpression).Operand as ILLabel;
            Debug.Assert(label != null);
            return label;
        }
        public static string GotoLabelName(this ILBasicBlock bb)
        {
            ILExpression end = bb.Body.Last() as ILExpression;
            switch (end.Code)
            {
                case GMCode.B:
                    return (end.Operand as ILLabel).Name;
                case GMCode.Ret:
                    return "Return";
                case GMCode.Exit:
                    return "Exit";
                    
            }
            Debug.Assert(false);
            return null;
        }
        public static ILLabel EntryLabel(this ILBasicBlock bb)
        {
            ILLabel label = bb.Body[0]  as ILLabel;
            Debug.Assert(label != null);
            return label;
        }
        // checks to see if the node can be used in an expression, or if it needs latter processing
        public static bool isExpressionResolved(this ILExpression e)
        {
            if (e == null) return false;
            if (e.Code == GMCode.Push) e = e.MatchSingleArgument(); // go into
            switch (e.Code)
            {
                case GMCode.Constant: return true; // always
                case GMCode.Var: return true;
                case GMCode.Call: return true;
                default:
                    if (e.Code.isExpression() && e.Arguments.Count != 0) return true;
                    break;
            }
            return false;
        }
        public static bool isExpressionResolved(this ILNode node)
        {
            if (node == null) return false;
            ILExpression e = node as ILExpression;
            if (e != null) return isExpressionResolved(e);
           else  return false;
        }
        public static bool isNodeResolved(this ILNode node)
        {
            if (node == null) return false;
            ILExpression e = node as ILExpression;
            if (e != null) return isExpressionResolved(e); 
            else return true; // true on any node that isn't an expressoin
        }
        public static bool isNodeResolved(this ILExpression e)
        {
            if (e == null) return false;
            return isExpressionResolved(e);
        }
        public static bool MatchType(this ILNode node, GM_Type type, out ILExpression expr)
        {
            ILExpression e = node as ILExpression;
            if(e != null && e.Type == type)
            {
                expr = e;
                return true;
            }
            expr = default(ILExpression);
            return false;
        }
        public static bool MatchType(this ILNode node, GM_Type type)
        {
            ILExpression call;
            return node.MatchType(type, out call);
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
                operand = (T)expr.Operand;
                return true;
            }
            operand = default(T);
            return false;
        }
        public static bool Match(this ILNode node, GMCode code, out IList<ILExpression> args)
        {
            ILExpression expr = node as ILExpression;
            if (expr != null && expr.Code == code && expr.Operand == null)
            {
                args = expr.Arguments;
                return true;
            }
            args = null;
            return false;
        }
        public static bool Match(this ILNode node, GMCode code, out ILExpression arg)
        {
            IList<ILExpression> args;
            if (node.Match(code, out args) && args.Count == 1)
            {
                arg = args[0];
                return true;
            }
            arg = null;
            return false;
        }

        public static bool Match<T>(this ILNode node, GMCode code, out T operand, out IList<ILExpression> args)
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
            IList<ILExpression> args;
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
            IList<ILExpression> args;
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
        public static bool MatchSingleAndBr<T>(this ILBasicBlock bb, GMCode code, out T operand, out IList<ILExpression> args, out ILLabel brLabel)
        {
            {
                if (bb.Body.Count == 3 &&
           bb.Body[0] is ILLabel &&
           bb.Body[1].Match(code, out operand, out args) &&
           bb.Body[2].Match(GMCode.B, out brLabel))
                    return true;
            }
            args = default(List<ILExpression>);
            brLabel = default(ILLabel);
            operand = default(T);
            return false;
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
        public static bool MatchLastAndBr<T>(this ILBasicBlock bb, GMCode code, out T operand, out IList<ILExpression> args, out ILLabel brLabel)
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
