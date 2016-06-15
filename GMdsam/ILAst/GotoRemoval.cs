using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;


namespace GameMaker.Ast
{
    public class GotoRemoval
    {
        Dictionary<ILNode, ILNode> parent = new Dictionary<ILNode, ILNode>();
        Dictionary<ILNode, ILNode> nextSibling = new Dictionary<ILNode, ILNode>();

        public GotoRemoval()
        {
        }
        public void RemoveGotos(ILBlock method)
        {
            // Build the navigation data
            parent[method] = null;
            foreach (ILNode node in method.GetSelfAndChildrenRecursive<ILNode>())
            {
                ILNode previousChild = null;
                foreach (ILNode child in node.GetChildren())
                {
                    ILExpression e = child as ILExpression;
                    if (e != null && (e.Operand is ILValue || e.Operand is ILVariable)) continue; // This should fix alot of issues
                     if (child is ILValue || child is ILVariable) continue; // we want to skip these.
                    // Added them as nodes so I don't have to dick with them latter with another AST
                    if (parent.ContainsKey(child))
                    { // this throws on one single file and I don't know why the hell it does
                        // its on obj_screen_Step_2  not sure why but its on an expression, so I am putting 
                        // a hack to skip expressions that don't have any gotos in it.  Meh
                        // debug, where the fuck is it
                        var nodes = parent.Keys.Where(x => x == child);
                        throw new Exception("The following expression is linked from several locations: " + child.ToString());

                    }
                    parent[child] = node;
                    if (previousChild != null)
                        nextSibling[previousChild] = child;
                    previousChild = child;
                }
                if (previousChild != null)
                    nextSibling[previousChild] = null;
            }

            // Simplify gotos
            bool modified;
            do
            {
                modified = false;
                foreach (ILExpression gotoExpr in method.GetSelfAndChildrenRecursive<ILExpression>(e => e.Code == GMCode.B))
                {
                    modified |= TrySimplifyGoto(gotoExpr);
                }
            } while (modified);

            RemoveRedundantCode(method);
        }

        public static void RemoveRedundantCode(ILBlock method)
        {
            // Remove dead lables and nops and any popzs left
            HashSet<ILLabel> liveLabels = new HashSet<ILLabel>(method.GetSelfAndChildrenRecursive<ILExpression>(e => e.IsBranch()).SelectMany(e => e.GetBranchTargets()));
           foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                block.Body = block.Body.Where(n => !n.Match(GMCode.BadOp)  && !(n is ILLabel && !liveLabels.Contains((ILLabel)n))).ToList();
            }

            // Remove redundant continue
            foreach (ILWhileLoop loop in method.GetSelfAndChildrenRecursive<ILWhileLoop>())
            {
                var body = loop.Body.Body;
                if (body.Count > 0 && body.Last().Match(GMCode.LoopContinue))
                {
                    body.RemoveAt(body.Count - 1);
                }
            }
            // Remove redundant continue
            foreach (ILWithStatement with in method.GetSelfAndChildrenRecursive<ILWithStatement>())
            {
                var body = with.Body.Body;
                if (body.Count > 0 && body.Last().Match(GMCode.LoopContinue))
                {
                    body.RemoveAt(body.Count - 1);
                }
            }
            // Remove redundant break at the end of case
            // Remove redundant case blocks altogether
            foreach (ILSwitch ilSwitch in method.GetSelfAndChildrenRecursive<ILSwitch>())
            {
                foreach (ILBlock ilCase in ilSwitch.Cases)
                {
                    Debug.Assert(ilCase.EntryGoto == null);

                    int count = ilCase.Body.Count;
                    if (count >= 2)
                    {
                        if (ilCase.Body[count - 2].IsUnconditionalControlFlow() &&
                            ilCase.Body[count - 1].Match(GMCode.LoopOrSwitchBreak))
                        {
                            ilCase.Body.RemoveAt(count - 1);
                        }
                    }
                }
                // fix case block
                

                var defaultCase = ilSwitch.Default ?? ilSwitch.Cases.SingleOrDefault(cb => cb.Values == null);
                // If there is no default block, remove empty case blocks
                if (defaultCase == null || (defaultCase.Body.Count == 1 && defaultCase.Body.Single().Match(GMCode.LoopOrSwitchBreak)))
                {
                    ilSwitch.Cases.RemoveAll(b => b.Body.Count == 1 && b.Body.Single().Match(GMCode.LoopOrSwitchBreak));
                }
            }
 
            // Remove redundant return at the end of method
            if (method.Body.Count > 0 && (method.Body.Last().Match(GMCode.Ret)|| method.Body.Last().Match(GMCode.Exit) )&& ((ILExpression)method.Body.Last()).Arguments.Count == 0)
            {
                method.Body.RemoveAt(method.Body.Count - 1);
            }

            // Remove unreachable return statements
            bool modified = false;
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                for (int i = 0; i < block.Body.Count - 1;)
                {
                    if (block.Body[i].IsUnconditionalControlFlow() && (block.Body[i + 1].Match(GMCode.Ret) || block.Body[i + 1].Match(GMCode.Exit)))
                    {
                        modified = true;
                        block.Body.RemoveAt(i + 1);
                    }
                    else {
                        i++;
                    }
                }
            }
            // Remove empty falseBlocks
            foreach (ILCondition condition in method.GetSelfAndChildrenRecursive<ILCondition>().Where(x => x.FalseBlock != null && x.FalseBlock.Body.Count == 0))
            {
                condition.FalseBlock = null;
                modified = true;
            }
                
            if (modified)
            {
                // More removals might be possible
                new GotoRemoval().RemoveGotos(method);
            }
        }

        IEnumerable<ILNode> GetParents(ILNode node)
        {
            ILNode current = node;
            while (true)
            {
                current = parent[current];
                if (current == null)
                    yield break;
                yield return current;
            }
        }

        bool TrySimplifyGoto(ILExpression gotoExpr)
        {
            Debug.Assert(gotoExpr.Code == GMCode.B);
            // Debug.Assert(gotoExpr.Prefixes == null);
            Debug.Assert(gotoExpr.Operand != null);

            ILNode target = Enter(gotoExpr, new HashSet<ILNode>());
            if (target == null)
                return false;

            // The gotoExper is marked as visited because we do not want to
            // walk over node which we plan to modify

            // The simulated path always has to start in the same try-block
            // in other for the same finally blocks to be executed.

            if (target == Exit(gotoExpr, new HashSet<ILNode>() { gotoExpr }))
            {
                gotoExpr.Code = GMCode.BadOp;
                gotoExpr.Operand = null;
                if (target is ILExpression)
                    ((ILExpression)target).ILRanges.AddRange(gotoExpr.ILRanges);
                gotoExpr.ILRanges.Clear();
                return true;
            }

            ILNode breakBlock = GetParents(gotoExpr).FirstOrDefault(n => n is ILWhileLoop || n is ILSwitch || n is ILWithStatement);
            if (breakBlock != null && target == Exit(breakBlock, new HashSet<ILNode>() { gotoExpr }))
            {
                gotoExpr.Code = GMCode.LoopOrSwitchBreak;
                gotoExpr.Operand = null;
                return true;
            }

            ILNode continueBlock = GetParents(gotoExpr).FirstOrDefault(n => n is ILWhileLoop || n is ILWithStatement);
            if (continueBlock != null && target == Enter(continueBlock, new HashSet<ILNode>() { gotoExpr }))
            {
                gotoExpr.Code = GMCode.LoopContinue;
                gotoExpr.Operand = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the first expression to be excecuted if the instruction pointer is at the start of the given node.
        /// Try blocks may not be entered in any way.  If possible, the try block is returned as the node to be executed.
        /// </summary>
        ILNode Enter(ILNode node, HashSet<ILNode> visitedNodes)
        {
            if (node == null)
                throw new ArgumentNullException();

            if (!visitedNodes.Add(node))
                return null;  // Infinite loop

            ILLabel label = node as ILLabel;
            if (label != null)
            {
                return Exit(label, visitedNodes);
            }
 

            ILExpression expr = node as ILExpression;
            if (expr != null)
            {
                if (expr.Code == GMCode.B)
                {
                    ILLabel target = (ILLabel)expr.Operand;
                    return Enter(target, visitedNodes);
                }
                else if (expr.Code == GMCode.BadOp  || expr.Code == GMCode.Constant || expr.Code == GMCode.Var)
                {
                    return Exit(expr, visitedNodes);
                }
                else if (expr.Code == GMCode.LoopOrSwitchBreak)
                {
                    ILNode breakBlock = GetParents(expr).First(n => n is ILWhileLoop || n is ILSwitch || n is ILWithStatement);
                    return Exit(breakBlock, new HashSet<ILNode>() { expr });
                }
                else if (expr.Code == GMCode.LoopContinue)
                {
                    ILNode continueBlock = GetParents(expr).First(n => n is ILWhileLoop || n is ILWithStatement);
                    return Enter(continueBlock, new HashSet<ILNode>() { expr });
                }
                else {
                    return expr;
                }
            }

            ILBlock block = node as ILBlock;
            if (block != null)
            {
                if (block.EntryGoto != null)
                {
                    return Enter(block.EntryGoto, visitedNodes);
                }
                else if (block.Body.Count > 0)
                {
                    return Enter(block.Body[0], visitedNodes);
                }
                else {
                    return Exit(block, visitedNodes);
                }
            }

            ILCondition cond = node as ILCondition;
            if (cond != null)
            {
                return cond.Condition;
            }
            ILWithStatement with = node as ILWithStatement;
            if (with != null)
            {
                if (with.Enviroment != null)
                {
                    return with.Enviroment;
                }
                else {
                    return Enter(with.Body, visitedNodes);
                }
            }

            ILWhileLoop loop = node as ILWhileLoop;
            if (loop != null)
            {
                if (loop.Condition != null)
                {
                    return loop.Condition;
                }
                else {
                    return Enter(loop.Body, visitedNodes);
                }
            }


            ILSwitch ilSwitch = node as ILSwitch;
            if (ilSwitch != null)
            {
                return ilSwitch.Condition;
            }

            throw new NotSupportedException(node.GetType().ToString());
        }

        /// <summary>
        /// Get the first expression to be excecuted if the instruction pointer is at the end of the given node
        /// </summary>
        ILNode Exit(ILNode node, HashSet<ILNode> visitedNodes)
        {
            if (node == null)
                throw new ArgumentNullException();

            ILNode nodeParent = parent[node];
            if (nodeParent == null)
                return null;  // Exited main body

            if (nodeParent is ILBlock)
            {
                ILNode nextNode = nextSibling[node];
                if (nextNode != null)
                {
                    return Enter(nextNode, visitedNodes);
                }
                else {
                    return Exit(nodeParent, visitedNodes);
                }
            }

            if (nodeParent is ILCondition)
            {
                return Exit(nodeParent, visitedNodes);
            }


            if (nodeParent is ILSwitch)
            {
                return null;  // Implicit exit from switch is not allowed
            }

            if (nodeParent is ILWhileLoop || nodeParent is ILWithStatement)
            {
                return Enter(nodeParent, visitedNodes);
            }

            throw new NotSupportedException(nodeParent.GetType().ToString());
        }
    }
}