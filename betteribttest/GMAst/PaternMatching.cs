using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.GMAst
{
    public static class PatternMatching
    {
        public static bool Match(this ILNode node, GMCode code)
        {
            ILExpression expr = node as ILExpression;
            return expr != null  && expr.Code == code;
        }

        public static bool Match<T>(this ILNode node, GMCode code, out T operand)
        {
            ILExpression expr = node as ILExpression;
            if (expr != null  && expr.Code == code && expr.Arguments.Count == 0)
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
            if (expr != null  && expr.Code == code)
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

        public static bool MatchSingleAndBr<T>(this ILBasicBlock bb, GMCode code, out T operand, out ILExpression arg, out ILLabel brLabel)
        {
            if (bb.Body.Count == 3 &&
                bb.Body[0] is ILLabel &&
                bb.Body[1].Match(code, out operand, out arg) &&
                bb.Body[2].Match(GMCode.B, out brLabel))
            {
                return true;
            }
            operand = default(T);
            arg = null;
            brLabel = null;
            return false;
        }

        public static bool MatchLastAndBr<T>(this ILBasicBlock bb, GMCode code, out T operand, out ILExpression arg, out ILLabel brLabel)
        {
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - 2).Match(code, out operand, out arg) &&
                bb.Body.LastOrDefault().Match(GMCode.B, out brLabel))
            {
                return true;
            }
            operand = default(T);
            arg = null;
            brLabel = null;
            return false;
        }
        public static bool MatchLastFrom<T>(this IList<ILNode> bb, ref int from, out T e, Predicate<T> pred) where T : ILNode
        {
            if (bb.Count != 0)
            {
                Debug.Assert(from >= 0 && from < bb.Count);
                for (int i = from; i >= 0; i++)
                {
                    if (bb[i] is ILLabel) continue; // skip labels
                    T test = bb[i] as T;
                    if (test != null && pred(test))
                    {
                        e = test;
                        return true;
                    }
                    break;
                }
            }
            e = default(T);
            return false;
        }
        public static bool MatchLast<T>(this IList<ILNode> bb, out T e, Predicate<T> pred) where T : ILNode
        {
            if (bb.Count != 0)
            {
                int from = bb.Count - 1;
                return MatchLastFrom(bb, ref from, out e, pred);
            }
            e = default(T);
            return false;
        }
        public static bool isConstant(this ILExpression n)
        {
            return (n.Code == GMCode.Push && (n.Operand is ILValue || n.Operand is ILVariable)) || n.Code == GMCode.Call;
        }
        public static bool MatchLastConstant(this IList<ILNode> bb, out ILExpression e)
        {
            return bb.MatchLast(out e, n => (n != null && n.isConstant()));
        }
        public static bool MatchLastConstants(this IList<ILNode> bb, int count, out ILExpression[] constants)
        {
            ILExpression[] ret = new ILExpression[count];
            int from = bb.Count - 1;
            bool noMatch = false;
            for(int i=0;i < count; i++)
            {
                if (bb.MatchLastFrom(ref from, out ret[i], n => (n != null && n.isConstant()))) from--;// skip the node
                else {
                    noMatch = true;
                    break;
                }
            }
            if(noMatch)
            {
                constants = default(ILExpression[]);
                return false;
            } else
            {
                constants = ret;
                return true;
            }
        }
        public static bool MatchSwitchBlock(this ILBasicBlock bb,  out IList<ILExpression> arg, out ILLabel fallLabel)
        {

            ILLabel brLabel;
            if (bb.Body.ElementAtOrDefault(bb.Body.Count - 2).Match(GMCode.Switch, out fallLabel, out arg) &&
                bb.Body.LastOrDefault().Match(GMCode.B, out brLabel))
            {
                if (brLabel.Name == fallLabel.Name) return true;
            }
            arg = null;
            fallLabel = null;
            return false;
        }
    }
}
