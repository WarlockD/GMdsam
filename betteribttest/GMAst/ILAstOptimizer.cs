using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using betteribttest.FlowAnalysis;

namespace betteribttest.GMAst
{
    public enum ILAstOptimizationStep
    {
        RemoveRedundantCode,
        ReduceBranchInstructionSet,
        InlineVariables,
        CopyPropagation,
        YieldReturn,
        AsyncAwait,
        PropertyAccessInstructions,
        SplitToMovableBlocks,
        TypeInference,
        HandlePointerArithmetic,
        SimplifyShortCircuit,
        SimplifyTernaryOperator,
        SimplifyNullCoalescing,
        JoinBasicBlocks,
        SimplifyLogicNot,
        SimplifyShiftOperators,
        TypeConversionSimplifications,
        SimplifyLdObjAndStObj,
        SimplifyCustomShortCircuit,
        SimplifyLiftedOperators,
        TransformArrayInitializers,
        TransformMultidimensionalArrayInitializers,
        TransformObjectInitializers,
        MakeAssignmentExpression,
        IntroducePostIncrement,
        InlineExpressionTreeParameterDeclarations,
        InlineVariables2,
        FindLoops,
        FindConditions,
        FlattenNestedMovableBlocks,
        RemoveEndFinally,
        RemoveRedundantCode2,
        GotoRemoval,
        DuplicateReturns,
        GotoRemoval2,
        ReduceIfNesting,
        InlineVariables3,
        CachedDelegateInitialization,
        IntroduceFixedStatements,
        RecombineVariables,
        TypeInference2,
        RemoveRedundantCode3,
        None
    }
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


        public static ILExpression WithILRanges(this ILExpression expr, IEnumerable<ILRange> ilranges)
        {
            expr.ILRanges.AddRange(ilranges);
            return expr;
        }

        public static void RemoveTail(this IList<ILNode> body, params GMCode[] codes)
        {
            int codeIndex = codes.Length - 1;
            int bodyIndex = body.Count - 1;
            while(codeIndex>=0)
            {
                if (((ILExpression)body[bodyIndex]).Code != codes[codeIndex])
                    throw new Exception("Tailing code does not match expected.");
                body.RemoveAt(bodyIndex);
                codeIndex--;
                bodyIndex--;
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
    
    }
    public partial class ILAstOptimizer
    {
        int nextLabelIndex = 0;
        Dictionary<ILLabel, ILBasicBlock> basicBlockLookup = new Dictionary<ILLabel, ILBasicBlock>();
        void ReduceBranchInstructionSet(ILBlock block)
        {
            for (int i = 0; i < block.Body.Count; i++)
            {
                ILExpression expr = block.Body[i] as ILExpression;

                if (expr != null)
                {
                    switch (expr.Code)
                    {
                        case GMCode.Bf:
                            ILExpression condition = expr.Arguments.Single();
                            switch (condition.Code)
                            {
                                case GMCode.Seq: condition.Code = GMCode.Sne; break;
                                case GMCode.Sne: condition.Code = GMCode.Seq; break;
                                case GMCode.Slt: condition.Code = GMCode.Sge; break;
                                case GMCode.Sge: condition.Code = GMCode.Slt; break;
                                case GMCode.Sle: condition.Code = GMCode.Sgt; break;
                                case GMCode.Sgt: condition.Code = GMCode.Sle; break;
                                default:
                                    expr.Arguments[0] = new ILExpression(GMCode.Not, null, condition);
                                    break;
                            }
                            break;
                        default:
                            continue;
                    }
                    expr.Code = GMCode.Bt;
                }
            }
        }

        /// <summary>
        /// Group input into a set of blocks that can be later arbitraliby schufled.
        /// The method adds necessary branches to make control flow between blocks
        /// explicit and thus order independent.
        /// </summary>

        void SplitToBasicBlocks(ILBlock block)
        {
            List<ILNode> basicBlocks = new List<ILNode>();

            ILLabel entryLabel = block.Body.FirstOrDefault() as ILLabel ?? new ILLabel() { Name = "Block_" + (nextLabelIndex++) };
            ILBasicBlock basicBlock = new ILBasicBlock();
            basicBlocks.Add(basicBlock);
            basicBlock.Body.Add(entryLabel);
            block.EntryGoto = new ILExpression(GMCode.B, entryLabel);

            if (block.Body.Count > 0)
            {
                if (block.Body[0] != entryLabel)
                    basicBlock.Body.Add(block.Body[0]);

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
                        ILLabel label = currNode as ILLabel ?? new ILLabel() { Name = "Block_" + (nextLabelIndex++).ToString() };

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
            return;
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
        /// <summary>
		/// Removes redundatant Br, Nop, Dup, Pop
		/// Ignore arguments of 'leave'
		/// </summary>
		/// <param name="method"></param>
		internal static void RemoveRedundantCode(ILBlock method)
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
                        if (labelRefCount[target] == 1)
                            i++;  // Ignore the label as well
                    }
                    else if (body[i].Match(GMCode.BadOp))
                    {
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


            // 'dup' removal
            foreach (ILExpression expr in method.GetSelfAndChildrenRecursive<ILExpression>())
            {
                for (int i = 0; i < expr.Arguments.Count; i++)
                {
                    ILExpression child;
                    if (expr.Arguments[i].Match(GMCode.Dup, out child))
                    {
                        child.ILRanges.AddRange(expr.Arguments[i].ILRanges);
                        expr.Arguments[i] = child;
                    }
                }
            }
        }
        #region SimplifyLogicNot
        static bool SimplifyLogicNot(List<ILNode> body, ILExpression expr, int pos)
        {
            bool modified = false;
            expr = SimplifyLogicNot(expr, ref modified);
            Debug.Assert(expr == null);
            return modified;
        }
        static bool SimplifyLogicNot(ref ILExpression expr)
        {
            bool modified = false;
            var newExpr = SimplifyLogicNot(expr, ref modified);
            if (newExpr != null) expr = newExpr;
            return modified;
        }
        static void FixAllIfStatements(ILBlock expr)
        {
            bool modified=false;
            do
            {
                modified = false;
                var list = expr.GetSelfAndChildrenRecursive<ILCondition>().ToList();
                foreach (var ifs in list) modified |= SimplifyLogicNot(ref ifs.Condition);
            } while (modified);
            do
            {
                modified = false;
                var list = expr.GetSelfAndChildrenRecursive<ILWhileLoop>().ToList();
                foreach (var loop in list) modified |= SimplifyLogicNot(ref loop.Condition);
            } while (modified);
          //  return;
            do
            {
                modified = false;
              
                // combine ifstatements to logic ands
                List<ILCondition> test = expr.GetSelfAndChildrenRecursive<ILCondition>(x => x.FalseBlock == null && x.TrueBlock.Body.Count == 1 && x.TrueBlock.Body[0] is ILCondition).ToList();
                if (test == null || test.Count > 0) modified = true;
                foreach (var ifs in test)
                {
                    var leftIf = (ifs.TrueBlock.Body[0] as ILCondition);
                    ifs.TrueBlock = leftIf.TrueBlock;
                    ifs.Condition = new ILExpression(GMCode.LogicAnd, null, ifs.Condition, leftIf.Condition);
                }
            }
            while (modified);
        }
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
            ILExpression res = null;
            while (expr.Code == GMCode.Not)
            {
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

        // Have to be run after goto removal
        // This is a bit hacky though, we should put it in the simpleflow control but eh
        void CombineLogicAndStatements(ILBlock block)
        {
            
        }
        public void Optimize(ILBlock method)
        {
            ReduceBranchInstructionSet(method);
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                SplitToBasicBlocks(block);
            }
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                bool modified;
                do
                {
                    modified = false;

                 //   modified |= block.RunOptimization(new SimpleControlFlow(method).SimplifyShortCircuit);
                  modified |= block.RunOptimization(new SimpleControlFlow(method).JoinBasicBlocks);
             
                    //  modified |= block.RunOptimization(SimplifyLogicNot);
                    //  modified |= block.RunOptimization(MakeAssignmentExpression);
                } while (modified);
            }
            // handle the with statments
            
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions().FindLoops(block);
            }

            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions().FindConditions(block);
            }
           
            try
            {
                FlattenBasicBlocks(method);


                RemoveRedundantCode(method);
                new GotoRemoval().RemoveGotos(method);
                RemoveRedundantCode(method);
                new GotoRemoval().RemoveGotos(method);
                FixAllIfStatements(method);
                CombineLogicAndStatements(method);

                RemoveRedundantCode(method); // all these might be redudent
            } catch(Exception e)
            {
                Debug.WriteLine("Exception Goto: " + e.Message);
            }
            
        }
    }
}
