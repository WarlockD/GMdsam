﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.FlowAnalysis;
using System.Diagnostics;

namespace GameMaker.Ast
{
    public static class ILAstOptimizerExtensionMethods
    {
        /// <summary>
        /// Perform one pass of a given optimization on this block.
        /// This block must consist of only basicblocks.
        /// </summary>
        public static bool RunOptimization(this ILBlock block, Func<IList<ILNode>, ILBasicBlock, int, bool> optimization)
        {
            bool modified = false;
            IList<ILNode> body = block.Body;
            for (int i = body.Count - 1; i >= 0; i--)
            {
                if (i < body.Count && optimization(body, (ILBasicBlock)body[i], i))
                {
                    modified = true;
                }
            }
            if (Context.Debug) block.FixParents();
            return modified;
        }
        static bool CheckBlockBody(IList<ILNode> body, ILExpression expr, int i, Func<IList<ILNode>, ILExpression, int, bool>[] optimizations)
        {
            bool modified = false;
            foreach (var optimization in optimizations) modified |= optimization(body, expr, i);
            return modified;
        }
        // special case.  Since we are modifying the block alot in one go, lets try to do the entire block at once
        public static bool RunOptimizationAndRestart(this ILBasicBlock bb, params Func<IList<ILNode>, ILExpression, int, bool>[] optimizations)
        {
            bool modified = false;
            IList<ILNode> body = bb.Body;
            for (int i = bb.Body.Count - 1; i >= 0; i--)
            {
                ILExpression expr = bb.Body.ElementAtOrDefault(i) as ILExpression;
                if (expr != null) // && optimization(bb.Body, expr, i))
                {
                    bool test = false;
                    while (CheckBlockBody(body, expr, i, optimizations))
                    {
                        test = true;
                    }
                    modified |= test;
                    if (test) i = bb.Body.Count;// backup
                }
            }
            return modified;
        }
        public static bool RunOptimizationAndRestart(this ILBlock block, params Func<IList<ILNode>, ILExpression, int, bool>[] optimizations)
        {
           
            bool modified = false;
            foreach (ILBasicBlock bb in block.Body) modified |= bb.RunOptimizationAndRestart(optimizations);
            if (Context.Debug) block.FixParents();
            return modified;
        }
        public static bool RunOptimization(this ILBlock block, Func<IList<ILNode>, ILExpression, int, bool> optimization)
        {
            bool modified = false;
            foreach (ILBasicBlock bb in block.Body)
            {
                for (int i = bb.Body.Count - 1; i >= 0; i--)
                {
                    ILExpression expr = bb.Body.ElementAtOrDefault(i) as ILExpression;
                    if (expr != null && optimization(bb.Body, expr, i))
                    {
                        modified = true;
                    }
                }
            }
            if (Context.Debug) block.FixParents();
            return modified;
        }
    
        public static bool IsConditionalControlFlow(this ILNode node)
        {
            ILExpression expr = node as ILExpression;
            return expr != null && expr.Code.IsConditionalControlFlow();
        }

        public static bool IsUnconditionalControlFlow(this ILNode node)
        {
            ILExpression expr = node as ILExpression;
            return expr != null && expr.Code.IsUnconditionalControlFlow();
        }
        public static ILExpression WithILRanges(this ILExpression expr, IEnumerable<ILExpression> exprs)
        {
            if (exprs != null) foreach(var e in exprs) if(e.ILRanges != null) expr.ILRanges.AddRange(e.ILRanges);
            return expr;
        }
        public static ILExpression WithILRanges(this ILExpression expr, params ILExpression[] exprs)
        {
            if (exprs != null) foreach (var e in exprs) if (e.ILRanges != null) expr.ILRanges.AddRange(e.ILRanges);
            return expr;
        }
        public static ILExpression WithILRanges(this ILExpression expr, IEnumerable<ILRange> ilranges)
        {
            if(ilranges!= null) expr.ILRanges.AddRange(ilranges);
            return expr;
        }

        public static ILExpression WithILRanges(this ILExpression expr, IEnumerable<ILRange> ilranges1, IEnumerable<ILRange> ilranges2)
        {
            if (ilranges1 != null) expr.ILRanges.AddRange(ilranges1);
            if (ilranges2 != null) expr.ILRanges.AddRange(ilranges2);
            return expr;
        }
        public static ILExpression WithILRangesAndJoin(this ILExpression expr, IEnumerable<ILRange> ilranges)
        {
            if (ilranges != null)
            {
                expr.ILRanges.AddRange(ilranges);
                ILRange.OrderAndJoin(expr.ILRanges);
            }
            return expr;
        }

        public static ILExpression WithILRangesAndJoin(this ILExpression expr, IEnumerable<ILRange> ilranges1, IEnumerable<ILRange> ilranges2)
        {
            expr.WithILRanges(ilranges1, ilranges2);
            if (ilranges1 !=null || ilranges2 != null) ILRange.OrderAndJoin(expr.ILRanges);
            return expr;
        }
        public static void RemoveTail(this IList<ILNode> body, params GMCode[] codes)
        {
            int bodyIndex = body.Count - codes.Length;
            for (int codeIndex = codes.Length - 1; codeIndex >= 0; codeIndex--)
            {
                ILExpression node = body.Last() as ILExpression;
                if (node.Code != codes[codeIndex])
                    throw new Exception("Tailing code does not match expected.");
                body.RemoveAt(body.Count - 1);
            }
        }

        public static V GetOrDefault<K, V>(this Dictionary<K, V> dict, K key)
        {
            V ret;
            dict.TryGetValue(key, out ret);
            return ret;
        }

        public static void RemoveOrThrow<T>(this ICollection<T> collection, T item)
        {
            if (!collection.Remove(item))
                throw new Exception("The item was not found in the collection");
        }
        public static void RemoveOrThrow<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            foreach (var i in items) collection.RemoveOrThrow(i);
        }
        public static void RemoveOrThrow<K, V>(this IDictionary<K, V> collection, K key)
        {
            if (!collection.Remove(key))
                throw new Exception("The key was not found in the dictionary");
        }

        public static bool ContainsReferenceTo(this ILExpression expr, ILVariable v)
        {
            if (expr.Operand == v)
                return true;
            foreach (var arg in expr.Arguments)
            {
                if (ContainsReferenceTo(arg, v))
                    return true;
            }
            return false;
        }

        // Returns argument off a an expression or throws.  This is WAY to common so I made it an extension
        public static ILExpression MatchSingleArgument(this ILNode n)
        {
            ILExpression e = n as ILExpression;
            if (e == null) throw new Exception("Not an expression");
            if (e.Code == GMCode.Push) return e.Arguments.Single();
            else return e;
        }

    }
    class Optimize
    {
       
      
        /// <summary>
        /// Group input into a set of blocks that can be later arbitraliby schufled.
        /// The method adds necessary branches to make control flow between blocks
        /// explicit and thus order independent.
        /// </summary>
        ///  
        public static void SplitToBasicBlocks(ILBlock block,bool reducebranches=false)
        {
            int nextLabelIndex = 0;
            List<ILNode> basicBlocks = new List<ILNode>();

            ILLabel entryLabel = block.Body.FirstOrDefault() as ILLabel ?? ILLabel.Generate("Block_" ,nextLabelIndex++);
            ILBasicBlock basicBlock = new ILBasicBlock();
            basicBlocks.Add(basicBlock);
            basicBlock.Body.Add(entryLabel);
            block.EntryGoto = new ILExpression(GMCode.B, entryLabel);

            if (block.Body.Count > 0)
            {
                if (block.Body[0] != entryLabel) basicBlock.Body.Add(block.Body[0]);
                for (int i = 1; i < block.Body.Count; i++)
                {
                    ILNode lastNode = block.Body[i - 1];
                    ILNode currNode = block.Body[i];

                    // Start a new basic block if necessary
                    if (currNode is ILLabel ||
                        lastNode.IsConditionalControlFlow() ||
                        lastNode.IsUnconditionalControlFlow())
                    {
                        // Try to reuse the label
                        ILLabel label = currNode as ILLabel ?? ILLabel.Generate("Block_", nextLabelIndex++);

                        // Terminate the last block
                        if (!lastNode.IsUnconditionalControlFlow())
                        {
                            // Explicit branch from one block to other
                            basicBlock.Body.Add(new ILExpression(GMCode.B, label));
                        }

                        // Start the new block
                        basicBlock = new ILBasicBlock();
                        basicBlocks.Add(basicBlock);
                        basicBlock.Body.Add(label);

                        // Add the node to the basic block
                        if (currNode != label)
                            basicBlock.Body.Add(currNode);
                    }
                    else {
                        basicBlock.Body.Add(currNode);
                    }
                }
            }

            block.Body = basicBlocks;

            if (reducebranches)
            {
                if (basicBlocks.Count > 0)
                {
                    for (int i = 0; i < block.Body.Count; i++)
                    {
                        ILBasicBlock bb = block.Body[i] as ILBasicBlock;
                        if (bb == null) continue;
                        ILLabel trueLabel;
                        ILLabel falseLabel;
                        if (bb.MatchLastAndBr(GMCode.Bf, out falseLabel, out trueLabel))
                        {
                            ILExpression bf = bb.Body[bb.Body.Count - 2] as ILExpression;
                            ILExpression b = bb.Body[bb.Body.Count - 1] as ILExpression;
                            bf.Code = GMCode.Bt;
                            b.Operand = falseLabel;
                            bf.Operand = trueLabel;
                        }
                    }
                }
            }
            return;
        }

        public static void RemoveRedundantCode(ILBlock method)
        {
            Dictionary<ILLabel, int> labelRefCount = new Dictionary<ILLabel, int>();
            foreach (ILLabel target in method.GetSelfAndChildrenRecursive<ILExpression>(e => e.IsBranch()).SelectMany(e => e.GetBranchTargets()))
            {
                labelRefCount[target] = labelRefCount.GetOrDefault(target) + 1;
            }

            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                IList<ILNode> body = block.Body;
                List<ILNode> newBody = new List<ILNode>(body.Count);
                for (int i = 0; i < body.Count; i++)
                {
                    ILLabel target;
                    if (body[i].Match(GMCode.B, out target) && i + 1 < body.Count && body[i + 1] == target)
                    {
                        // Ignore the branch
                        if (labelRefCount[target] == 1) i++;  // Ignore the label as well

                    }
                    else if (body[i].Match(GMCode.BadOp))
                    {
                        // Ignore nop
                        
                    } 
                    else {
                        ILLabel label = body[i] as ILLabel;
                        if (label != null)
                        {
                            if (labelRefCount.GetOrDefault(label) > 0)
                                newBody.Add(label);
                        }
                        else {
                            newBody.Add(body[i]);
                        }
                    }
                }
                block.Body = newBody;
            }

#if false
            // 'dup' removal
            foreach (ILExpression expr in method.GetSelfAndChildrenRecursive<ILExpression>())
            {
                if (expr.Code == GMCode.Dup) throw new Exception("Dups shoul be removed at this stage");
            }
#endif
        }
    
            /// <summary>
            /// Flattens all nested basic blocks, except the the top level 'node' argument
            /// </summary>
            void FlattenBasicBlocks(ILNode node)
        {
            ILBlock block = node as ILBlock;
            if (block != null)
            {
                List<ILNode> flatBody = new List<ILNode>();
                foreach (ILNode child in block.GetChildren())
                {
                    FlattenBasicBlocks(child);
                    ILBasicBlock childAsBB = child as ILBasicBlock;
                    if (childAsBB != null)
                    {
                        if (!(childAsBB.Body.FirstOrDefault() is ILLabel))
                            throw new Exception("Basic block has to start with a label. \n" + childAsBB.ToString());
                        if (childAsBB.Body.LastOrDefault() is ILExpression && !childAsBB.Body.LastOrDefault().IsUnconditionalControlFlow())
                            throw new Exception("Basci block has to end with unconditional control flow. \n" + childAsBB.ToString());
                        flatBody.AddRange(childAsBB.GetChildren());
                    }
                    else {
                        flatBody.Add(child);
                    }
                }
                block.EntryGoto = null;
                block.Body = flatBody;
            }
            else if (node is ILExpression)
            {
                // Optimization - no need to check expressions
            }
            else if (node != null)
            {
                // Recursively find all ILBlocks
                foreach (ILNode child in node.GetChildren())
                {
                    FlattenBasicBlocks(child);
                }
            }
        }
     
        // GM uses 1 and 0 as bool but uses conv to convert them so lets fix calls
        // like check() == 1 and change them to just check()

        public static bool SimplifyBoolTypes(ILExpression expr, out ILExpression nexpr)
        {
            ILExpression call;
            ILValue constant;
            if ((expr.Code == GMCode.Seq || expr.Code == GMCode.Sne) &&
                ((expr.Arguments.ElementAtOrDefault(0).MatchType(GM_Type.Bool, out call) && expr.Arguments.ElementAtOrDefault(1).Match(GMCode.Constant, out constant)) ||
                (expr.Arguments.ElementAtOrDefault(1).MatchType(GM_Type.Bool, out call) && expr.Arguments.ElementAtOrDefault(2).Match(GMCode.Constant, out constant))) &&
                constant.IntValue != null)
            {
                if ((expr.Code == GMCode.Seq && constant == 0) || constant == 1) // have to invert it
                    call = new ILExpression(GMCode.Not, null, call);
                nexpr = call;
                return true;
            }
            nexpr = default(ILExpression);
            return false;
        }
        public static bool SimplifyBoolTypes(IList<ILExpression> args)
        {
            if (args.Count == 0) return false;
            bool modified = false;
            for(int i=0;i< args.Count; i++)
            {
                ILExpression expr = args[i];
                modified |= SimplifyBoolTypes(expr.Arguments);
                ILExpression nexpr;
                if (SimplifyBoolTypes(expr, out nexpr)) {
                    args[i] = nexpr;
                    modified |= true;
                }
            }
            return modified;
        }
        public static bool SimplifyBoolTypes(IList<ILNode> body, ILExpression expr, int pos)
        {
            if (expr.Code == GMCode.Push || expr.Code.isBranch())
            {
                return SimplifyBoolTypes(expr.Arguments);
            }
            return false;
        }

        /// <summary>
        /// Removes redundatant Br, Nop, Dup, Pop
        /// Ignore arguments of 'leave'
        /// </summary>
        /// <param name="method"></param>

        #region SimplifyLogicNot
        public static bool SimplifyLogicNot(IList<ILNode> body, ILExpression expr, int pos)
        {
            bool modified = false;
            expr = SimplifyLogicNot(expr, ref modified);
            Debug.Assert(expr == null);
            return modified;
        }
     
        // This will negate a condition and optimize it
      
            // Tis will simplify negates that get out of control
            static ILExpression SimplifyLogicNot(ILExpression expr, ref bool modified)
        {
            ILExpression a;
#if false
            // not sure we need this
            // "ceq(a, ldc.i4.0)" becomes "logicnot(a)" if the inferred type for expression "a" is boolean
            if (expr.Code == GMCode.Seq && TypeAnalysis.IsBoolean(expr.Arguments[0].InferredType) && (a = expr.Arguments[1]).Code == ILCode.Ldc_I4 && (int)a.Operand == 0)
            {
                expr.Code = ILCode.LogicNot;
                expr.ILRanges.AddRange(a.ILRanges);
                expr.Arguments.RemoveAt(1);
                modified = true;
            }
#endif
            if(expr.Code == GMCode.Push && expr.Arguments.Count > 0)
                return SimplifyLogicNot(expr.Arguments[0], ref modified);

            ILExpression res = null;
            while (expr.Code == GMCode.Not && expr.Arguments.Count >0)
            {
                Debug.Assert(expr.Arguments.Count == 1);
                a = expr.Arguments[0];
                // remove double negation
                if (a.Code == GMCode.Not)
                {
                    res = a.Arguments[0];
                    res.ILRanges.AddRange(expr.ILRanges);
                    res.ILRanges.AddRange(a.ILRanges);
                    expr = res;
                }
                else {
                    if (SimplifyLogicLogicArguments(expr)) res = expr = a;
                    break;
                }
            }

            for (int i = 0; i < expr.Arguments.Count; i++)
            {
                a = SimplifyLogicNot(expr.Arguments[i], ref modified);
                if (a != null)
                {
                    expr.Arguments[i] = a;
                    modified = true;
                }
            }
           // Debug.Assert(res != null);
            return res;
        }
        static bool SimplifyLogicLogicArguments(ILExpression expr)
        {
            var a = expr.Arguments[0];
          
            switch (a.Code)
            {
                case GMCode.LogicAnd: // this is complcated
                case GMCode.LogicOr: // this is complcated
                    a.Code = a.Code == GMCode.LogicOr ? GMCode.LogicAnd : GMCode.LogicOr;
                    if (!SimplifyLogicNotArgument(a.Arguments[0])) a.Arguments[0] = new ILExpression(GMCode.Not, null, a.Arguments[0]);
                    if (!SimplifyLogicNotArgument(a.Arguments[1])) a.Arguments[1] = new ILExpression(GMCode.Not, null, a.Arguments[1]);
                    a.ILRanges.AddRange(expr.ILRanges);
                    return true;
                default:
                    return SimplifyLogicNotArgument(expr);
            }      
        }
        /// <summary>
        /// If the argument is a binary comparison operation then the negation is pushed through it
        /// </summary>
        static bool SimplifyLogicNotArgument(ILExpression expr)
        {
            var a = expr.Arguments[0];
            GMCode c;
            switch (a.Code)
            {
                case GMCode.Seq: c = GMCode.Sne; break;
                case GMCode.Sne: c = GMCode.Seq; break;
                case GMCode.Sgt: c = GMCode.Sle; break;
                case GMCode.Sge: c = GMCode.Slt; break;
                case GMCode.Slt: c = GMCode.Sge; break;
                case GMCode.Sle: c = GMCode.Sgt; break;
                default: return false;
            }
            a.Code = c;
            a.ILRanges.AddRange(expr.ILRanges);
            return true;
        }
#endregion
    }
}
