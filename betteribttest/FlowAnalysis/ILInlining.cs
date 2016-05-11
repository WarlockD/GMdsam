using GameMaker.FlowAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.Dissasembler;
using GameMaker;

namespace betteribttest.FlowAnalysis
{
    /// <summary>
    /// Performs inlining transformations.
    /// </summary>
    public class ILInlining
    {
        readonly ILBlock method;
        internal Dictionary<ILVariable, int> numStloc = new Dictionary<ILVariable, int>();
        internal Dictionary<ILVariable, int> numLdloc = new Dictionary<ILVariable, int>();

        public ILInlining(ILBlock method)
        {
            this.method = method;
            AnalyzeMethod();
        }
        void AnalyzeMethod()
        {
            numStloc.Clear();
            numLdloc.Clear();

            // Analyse the whole method
            AnalyzeNode(method);
        }
        /// <summary>
		/// For each variable reference, adds <paramref name="direction"/> to the num* dicts.
		/// Direction will be 1 for analysis, and -1 when removing a node from analysis.
		/// </summary>
		void AnalyzeNode(ILNode node, int direction = 1)
        {
            ILExpression expr = node as ILExpression;
            if (expr != null)
            {
                ILVariable locVar = expr.Operand as ILVariable;
                if (locVar != null)
                {
                    if (expr.Code == GMCode.Pop)
                        numStloc[locVar] = numStloc.GetOrDefault(locVar) + direction;
                    else if (expr.Code == GMCode.Var || expr.Code == GMCode.Push)
                        numLdloc[locVar] = numLdloc.GetOrDefault(locVar) + direction;
                    else throw new NotSupportedException(expr.Code.ToString());
                }
                foreach (ILExpression child in expr.Arguments)
                    AnalyzeNode(child, direction);
                return;
            }
            else
            {
                foreach (ILNode child in node.GetChildren())
                    AnalyzeNode(child, direction);
            }
        }
        bool CanPerformCopyPropagation(ILExpression expr, ILVariable copyVariable)
        {
            switch (expr.Code)
            {
                case GMCode.Push:
                    ILVariable v = (ILVariable) expr.Operand;

                    // Variables are be copied only if both they and the target copy variable are generated,
                    // and if the variable has only a single assignment
                    return v != null && v.isGenerated && copyVariable.isGenerated && numStloc.GetOrDefault(v) == 1;
                default:
                    return false;
            }
        }
        /// <summary>
		/// Runs a very simple form of copy propagation.
		/// Copy propagation is used in two cases:
		/// 1) assignments from arguments to local variables
		///    If the target variable is assigned to only once (so always is that argument) and the argument is never changed (no ldarga/starg),
		///    then we can replace the variable with the argument.
		/// 2) assignments of address-loading instructions to local variables
		/// </summary>
		public void CopyPropagation()
        {
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                for (int i = 0; i < block.Body.Count; i++)
                {
                    ILVariable v;
                    ILExpression copiedExpr;
                    if (block.Body[i].Match(GMCode.Pop, out v, out copiedExpr)
                        && numStloc.GetOrDefault(v) == 1 && numLdloc.GetOrDefault(v) == 0
                        && CanPerformCopyPropagation(copiedExpr, v))
                    {
                        // un-inline the arguments of the ldArg instruction
                        ILVariable[] uninlinedArgs = new ILVariable[copiedExpr.Arguments.Count];
                        for (int j = 0; j < uninlinedArgs.Length; j++)
                        {
                            uninlinedArgs[j] = new ILVariable { isGenerated = true, Name = v.Name + "_cp_" + j };
                            block.Body.Insert(i++, new ILExpression(GMCode.Pop, uninlinedArgs[j], copiedExpr.Arguments[j]));
                        }

                        // perform copy propagation:
                        foreach (var expr in method.GetSelfAndChildrenRecursive<ILExpression>())
                        {
                            if (expr.Code == GMCode.Push && v.Equals(expr.Operand))
                            {
                                expr.Code = copiedExpr.Code;
                                expr.Operand = copiedExpr.Operand;
                                for (int j = 0; j < uninlinedArgs.Length; j++)
                                {
                                    expr.Arguments.Add(new ILExpression(GMCode.Push, uninlinedArgs[j]));
                                }
                            }
                        }

                        block.Body.RemoveAt(i);
                        if (uninlinedArgs.Length > 0)
                        {
                            // if we un-inlined stuff; we need to update the usage counters
                            AnalyzeMethod();
                        }
                        //  InlineInto(block.Body, i, aggressive: false); // maybe inlining gets possible after the removal of block.Body[i]
                        i -= uninlinedArgs.Length + 1;
                    }
                }
            }
        }

        public bool InlineAllVariables()
        {
            bool modified = false;
            ILInlining i = new ILInlining(method);
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
                modified |= i.InlineAllInBlock(block);
            return modified;
        }
        public bool InlineAllInBlock(ILBlock block)
        {
            bool modified = false;
            List<ILNode> body = block.Body;
            for (int i = 0; i < body.Count - 1;)
            {
                ILVariable locVar;
                ILExpression expr;
                if (body[i].Match(GMCode.Pop, out locVar, out expr) && InlineOneIfPossible(block.Body, i, aggressive: false))
                {
                    modified = true;
                    i = Math.Max(0, i - 1); // Go back one step
                }
                else
                {
                    i++;
                }
            }
            foreach (ILBasicBlock bb in body.OfType<ILBasicBlock>())
            {
                modified |= InlineAllInBasicBlock(bb);
            }
            return modified;
        }
        public bool InlineAllInBasicBlock(ILBasicBlock bb)
        {
            bool modified = false;
            List<ILNode> body = bb.Body;
            for (int i = 0; i < body.Count;)
            {
                ILVariable locVar;
                ILExpression expr;
                if (body[i].Match(GMCode.Pop, out locVar, out expr) && InlineOneIfPossible(bb.Body, i, aggressive: false))
                {
                    modified = true;
                    i = Math.Max(0, i - 1); // Go back one step
                }
                else
                {
                    i++;
                }
            }
            return modified;
        }
        /// <summary>
		/// Inlines instructions before pos into block.Body[pos].
		/// </summary>
		/// <returns>The number of instructions that were inlined.</returns>
		public int InlineInto(List<ILNode> body, int pos, bool aggressive)
        {
            if (pos >= body.Count)
                return 0;
            int count = 0;
            while (--pos >= 0)
            {
                ILExpression expr = body[pos] as ILExpression;
                if (expr == null || expr.Code != GMCode.Pop)
                    break;
                if (InlineOneIfPossible(body, pos, aggressive))
                    count++;
                else
                    break;
            }
            return count;
        }
        /// <summary>
		/// Aggressively inlines the stloc instruction at block.Body[pos] into the next instruction, if possible.
		/// If inlining was possible; we will continue to inline (non-aggressively) into the the combined instruction.
		/// </summary>
		/// <remarks>
		/// After the operation, pos will point to the new combined instruction.
		/// </remarks>
		public bool InlineIfPossible(List<ILNode> body, ref int pos)
        {
            if (InlineOneIfPossible(body, pos, true))
            {
                pos -= InlineInto(body, pos, false);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Inlines the stloc instruction at block.Body[pos] into the next instruction, if possible.
        /// </summary>
        public bool InlineOneIfPossible(List<ILNode> body, int pos, bool aggressive)
        {
            ILVariable v;
            ILExpression inlinedExpression;
            if (body[pos].Match(GMCode.Pop, out v, out inlinedExpression))
            {
                if (InlineIfPossible(v, inlinedExpression, body.ElementAtOrDefault(pos + 1), aggressive))
                {
                    // Assign the ranges of the stloc instruction:
                    inlinedExpression.ILRanges.AddRange(((ILExpression) body[pos]).ILRanges);
                    // Remove the stloc instruction:
                    body.RemoveAt(pos);
                    return true;
                }
                else if (numLdloc.GetOrDefault(v) == 0 && numLdloc.GetOrDefault(v) == 0)
                {
                    // Remove completely
                    AnalyzeNode(body[pos], -1);
                    body.RemoveAt(pos);
                    return true;
                }
            }
            return false;
        }
        /// <summary>
		/// Inlines 'expr' into 'next', if possible.
		/// </summary>
		bool InlineIfPossible(ILVariable v, ILExpression inlinedExpression, ILNode next, bool aggressive)
        {
            // ensure the variable is accessed only a single time
            if (numStloc.GetOrDefault(v) != 1)
                return false;
            int ldloc = numLdloc.GetOrDefault(v);
            if (ldloc > 1 || ldloc + numLdloc.GetOrDefault(v) != 1)
                return false;

            if (next is ILCondition)
                next = ((ILCondition) next).Condition;
            else if (next is ILWhileLoop)
                next = ((ILWhileLoop) next).Condition;

            ILExpression parent;
            int pos;
            if (FindLoadInNext(next as ILExpression, v, inlinedExpression, out parent, out pos) == true)
            {
                if (ldloc == 0)
                {
                    return false;
                }
                else
                {
                    if (!aggressive && !v.isGenerated && !NonAggressiveInlineInto((ILExpression) next, parent, inlinedExpression))
                        return false;
                }

                // Assign the ranges of the ldloc instruction:
                inlinedExpression.ILRanges.AddRange(parent.Arguments[pos].ILRanges);

                parent.Arguments[pos] = inlinedExpression;
                return true;
            }
            return false;
        }
        /// <summary>
		/// Determines whether a variable should be inlined in non-aggressive mode, even though it is not a generated variable.
		/// </summary>
		/// <param name="next">The next top-level expression</param>
		/// <param name="parent">The direct parent of the load within 'next'</param>
		/// <param name="inlinedExpression">The expression being inlined</param>
		bool NonAggressiveInlineInto(ILExpression next, ILExpression parent, ILExpression inlinedExpression)
        {
            if (inlinedExpression.Code.isExpression())
                return true;

            switch (next.Code)
            {
                case GMCode.Ret:
                case GMCode.Bt:
                    return parent == next;
                case GMCode.Switch:
                    return parent == next || (parent.Code == GMCode.Sub && parent == next.Arguments[0]);
                default:
                    return false;
            }
        }
        /// <summary>
		/// Gets whether 'expressionBeingMoved' can be inlined into 'expr'.
		/// </summary>
		public bool CanInlineInto(ILExpression expr, ILVariable v, ILExpression expressionBeingMoved)
        {
            ILExpression parent;
            int pos;
            return FindLoadInNext(expr, v, expressionBeingMoved, out parent, out pos) == true;
        }
        /// <summary>
		/// Finds the position to inline to.
		/// </summary>
		/// <returns>true = found; false = cannot continue search; null = not found</returns>
		bool? FindLoadInNext(ILExpression expr, ILVariable v, ILExpression expressionBeingMoved, out ILExpression parent, out int pos)
        {
            parent = null;
            pos = 0;
            if (expr == null)
                return false;
            for (int i = 0; i < expr.Arguments.Count; i++)
            {
                // Stop when seeing an opcode that does not guarantee that its operands will be evaluated.
                // Inlining in that case might result in the inlined expresion not being evaluted.
                if (i == 1 && (expr.Code == GMCode.LogicAnd || expr.Code == GMCode.LogicOr))
                    return false;

                ILExpression arg = expr.Arguments[i];

                if ((arg.Code == GMCode.Push || arg.Code == GMCode.Var) && arg.Operand == v)
                {
                    parent = expr;
                    pos = i;
                    return true;
                }
                bool? r = FindLoadInNext(arg, v, expressionBeingMoved, out parent, out pos);
                if (r != null)
                    return r;
            }
            if (IsSafeForInlineOver(expr, expressionBeingMoved))
                return null; // continue searching
            else
                return false; // abort, inlining not possible
        }
        /// <summary>
		/// Determines whether it is safe to move 'expressionBeingMoved' past 'expr'
		/// </summary>
		bool IsSafeForInlineOver(ILExpression expr, ILExpression expressionBeingMoved)
        {
            switch (expr.Code)
            {

                case GMCode.Push:
                    ILVariable loadedVar = (ILVariable) expr.Operand;
                    if (loadedVar != null)
                    {

                        if (numLdloc.GetOrDefault(loadedVar) != 0)
                        {
                            // abort, inlining is not possible
                            return false;
                        }
                        foreach (ILExpression potentialStore in expressionBeingMoved.GetSelfAndChildrenRecursive<ILExpression>())
                        {
                            if (potentialStore.Code == GMCode.Pop && potentialStore.Operand == loadedVar)
                                return false;
                        }
                    }
                    // the expression is loading a non-forbidden variable
                    return true;
                case GMCode.Var:
                case GMCode.Concat:
                    // address-loading instructions are safe if their arguments are safe
                    foreach (ILExpression arg in expr.Arguments)
                    {
                        if (!IsSafeForInlineOver(arg, expressionBeingMoved))
                            return false;
                    }
                    return true;
                default:
                    // instructions with no side-effects are safe (except for Ldloc and Ldloca which are handled separately)
                    return true;
            }
        }


    }
}