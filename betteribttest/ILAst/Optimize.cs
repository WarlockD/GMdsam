using System;
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
        public static bool RunOptimization(this ILBlock block, Func<List<ILNode>, ILBasicBlock, int, bool> optimization)
        {
            bool modified = false;
            List<ILNode> body = block.Body;
            for (int i = body.Count - 1; i >= 0; i--)
            {
                if (i < body.Count && optimization(body, (ILBasicBlock)body[i], i))
                {
                    modified = true;
                }
            }
            return modified;
        }

        public static bool RunOptimization(this ILBlock block, Func<List<ILNode>, ILExpression, int, bool> optimization)
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
        // Clones the node to another expression or makes an expresson based off it
        public static ILExpression ToExpresion(this ILNode n)
        {
            if (n == null) return null;
            else if (n is ILValue) return new ILExpression(GMCode.Constant, n as ILValue);
            else if (n is ILVariable) return new ILExpression(GMCode.Var, n as ILVariable);
            else if (n is ILExpression) return new ILExpression(n as ILExpression);
            else if (n is ILCall) return new ILExpression(GMCode.Call, n);
            else throw new Exception("Should not happen here");
        }


        /// <summary>
        /// Fixes an expression so that any extra Operand data is converted to an expresion
        /// </summary>
        /// <param name="n"></param>
        /// <returns>True if its a push</returns>
        public static bool FixPushExpression(this ILNode n)
        {
            ILExpression e = n as ILExpression;
            if (e == null || e.Code != GMCode.Push) return false; // not an expresson or null
            if (e.Operand != null) // already processed
            {
                ILExpression arg = null;
                object o = e.Operand;
                if (o is ILValue) arg = new ILExpression(GMCode.Constant, o as ILValue);
                else if (o is ILVariable) arg = new ILExpression(GMCode.Var, o as ILVariable);
                else if (o is ILCall) arg = new ILExpression(GMCode.Call, o);
                Debug.Assert(arg != null);
                e.Arguments.Clear(); // make sure
                e.Arguments.Add(arg);
                e.Operand = null;
            }
            return true;
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
        public static void SplitToBasicBlocks(ILBlock block,bool reducebranches=false)
        {
            int nextLabelIndex = 0;
            List<ILNode> basicBlocks = new List<ILNode>();

            ILLabel entryLabel = block.Body.FirstOrDefault() as ILLabel ?? new ILLabel() { Name = "Block_" + (nextLabelIndex++) };
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
                List<ILNode> body = block.Body;
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
        public static bool FixLuaStringAddExpression(ILNode expr)
        {
            bool haveString = false; // check if we have a single string in the expression
            var allNonAdds = expr.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code.isExpression() && x.Code != GMCode.Add).ToList();
            if (allNonAdds.Count ==0)
            {
                var allAdds = expr.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Add).ToList();
                for (int i = 0; i < allAdds.Count; i++)
                {
                    var e = allAdds[i];
                    if (haveString)
                        e.Code = GMCode.Concat;
                    else if (e.Arguments[0].MatchConstant(GM_Type.String) || e.Arguments[1].MatchConstant(GM_Type.String))
                    {// if we match a string, they all have to be converted
                        haveString = true;
                        i = -1; // reset list
                    }
                }
            }
            return haveString;
        } // 
        public static bool FixLuaStringAddExpression(IList<ILNode> body, ILExpression expr, int pos)
        {
            bool haveString = false; // check if we have a single string in the expression
            var allNonAdds = expr.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code.isExpression() && x.Code != GMCode.Add).ToList();
            if (allNonAdds.Count > 0)
            {
                var allAdds = expr.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Add).ToList();
                for (int i = 0; i < allAdds.Count; i++)
                {
                    var e = allAdds[i];
                    if (haveString)
                        e.Code = GMCode.Concat;
                    else if (e.MatchConstant(GM_Type.String))
                    {// if we match a string, they all have to be converted
                        haveString = true;
                        i = 0; // reset list
                    }
                }
            }
            return haveString;
        }
        public static bool FixLuaStringAdd(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            bool modified = false; // may need to optimzie this, can we just go though all the nodes?
            // it makes sure where a concat can exisit it should, but we need to do a lot of refactoring
            // on this latter
            foreach(var a in head.GetSelfAndChildrenRecursive<ILAssign>())
            {
                modified |= FixLuaStringAddExpression(a);
            }
            foreach (var a in head.GetSelfAndChildrenRecursive<ILCall>())
            {
                foreach(var e in a.Arguments) modified |= FixLuaStringAddExpression(e);
            }
            foreach (var a in head.GetSelfAndChildrenRecursive<ILExpression>(x=> x.Code == GMCode.Call))
            {
                foreach (var e in a.Arguments) modified |= FixLuaStringAddExpression(e);
            }
            return modified;
        }

     
        // GM uses 1 and 0 as bool but uses conv to convert them so lets fix calls
        // like check() == 1 and change them to just check()

        public static bool SimplifyBoolTypes(ILExpression expr)
        {
            ILExpression call;
            int constant;
            if ((expr.Code == GMCode.Seq || expr.Code == GMCode.Sne) &&
                (expr.Arguments[0].MatchCall(GM_Type.Bool, out call) ||
                expr.Arguments[1].MatchCall(GM_Type.Bool, out call)) &&
                (expr.Arguments[0].MatchIntConstant(out constant) ||
                expr.Arguments[1].MatchIntConstant(out constant))
                )
            {
                if ((expr.Code == GMCode.Seq && constant == 0) || constant == 1) // have ot invert it
                    call = new ILExpression(GMCode.Not, null, call);
                expr.Replace(call);
                return true;
            }
            return false;
        }

        public static bool SimplifyBoolTypes(List<ILNode> body, ILExpression expr, int pos)
        {
            bool modified = false;
            if (expr.Code == GMCode.Push || expr.Code.isBranch())
            {
                foreach(var e in expr.GetSelfAndChildrenRecursive<ILExpression>(x=>x.Code == GMCode.Seq || x.Code == GMCode.Sne))
                {
                    modified |= SimplifyBoolTypes(e);
                }
            }
            return modified;
        }

        /// <summary>
        /// Removes redundatant Br, Nop, Dup, Pop
        /// Ignore arguments of 'leave'
        /// </summary>
        /// <param name="method"></param>

        #region SimplifyLogicNot
        public static bool SimplifyLogicNot(List<ILNode> body, ILExpression expr, int pos)
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
