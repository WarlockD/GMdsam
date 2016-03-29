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

        public static bool Match(this ILNode node, GMCode code, out List<ILExpression> args)
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
    }
}
