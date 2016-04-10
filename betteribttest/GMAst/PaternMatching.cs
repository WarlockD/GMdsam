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
        public static void WriteNodes<T>(this IList<T> nodes, ITextOutput output) where T : ILNode
        {
            if (nodes.Count == 0) return;
            else if(nodes.Count == 1) nodes[0].WriteTo(output);
            else
            {
                output.Write('{');
                output.WriteLine();
                output.Indent();
                foreach (var n in nodes)
                {
                    n.WriteTo(output);
                    output.WriteLine();
                }
                output.Unindent();
                output.WriteLine();
                output.Write('}');
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
            foreach(var n in nodes)
            {
                ILExpression e = n as ILExpression;
                if(e != null) { e.CollectLabels(labels); continue; }
                ILLabel label = n as ILLabel;
                if (label != null) { labels.Add(label); continue; }
            }
        }
        public static ILExpression WithILRanges(this ILExpression expr, IEnumerable<ILRange> ilranges)
        {
            expr.ILRanges.AddRange(ilranges);
            return expr;
        }
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
        public static int FindLastIndexOf(this IList<ILNode> ast, GMCode code, int from)
        {
            if (ast.Count == 0 || from < 0 || from > (ast.Count-1)) return -1;
            for (int i = from; i >= 0; i--) if (ast[i].Match(code)) return i;
            return -1;
        }
        public static int FindLastIndexOf(this IList<ILNode> ast, GMCode code)
        {
            return ast.FindLastIndexOf(code, ast.Count - 1);
        }
        public static bool MatchLastCount<T>(this IList<T> ast, GMCode code,int count, out List<ILExpression> match) where T : ILNode
        {
            do
            {
                if (ast.Count == 0 && ast.Count < count) break;
                int i = ast.Count - 1, j = 0;
                List<ILExpression> ret = new List<ILExpression>();
                for (; i >= 0 && j < count; i--, j++)
                {
                    ILExpression test;
                    if (ast.ElementAtOrDefault(i).Match(code, out test)) ret.Add(test); else break;
                }
                if (j != count) break; // bad match or not enough match
                match = ret;
                return true;
            } while (false);
            match = default(List<ILExpression>);
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
        public static void RemoveLast<T>(this List<T> a,int count)
        {
            if (a.Count < count) a.Clear();
            else a.RemoveRange(a.Count - count, count);
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
