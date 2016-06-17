using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.FlowAnalysis;
using System.Diagnostics;

namespace GameMaker.Ast
{
    public class LoopsAndConditions
    {
        Dictionary<ILLabel, ControlFlowNode> labelToCfNode = new Dictionary<ILLabel, ControlFlowNode>();
        public ControlFlowNode LabelToNode(ILLabel l)
        {
            return labelToCfNode[l];
        }
        uint nextLabelIndex = 0;
        ErrorContext error;

        public LoopsAndConditions(ErrorContext error)//DecompilerContext context)
        {
            this.error = error;
        }

        public void FindLoops(ILBlock block)
        {
            if (block.Body.Count > 0)
            {
                ControlFlowGraph graph;
                graph = BuildGraph(block.Body, (ILLabel)block.EntryGoto.Operand);
                graph.ComputeDominance();
                graph.ComputeDominanceFrontier();
                block.Body = FindLoops(new HashSet<ControlFlowNode>(graph.Nodes.Skip(2)), graph.EntryPoint, false); 
            }
        }  
        public void FindConditions(ILBlock block)
        {
            if (block.Body.Count > 0)
            {
                ControlFlowGraph graph;
                graph = BuildGraph(block.Body, (ILLabel)block.EntryGoto.Operand);
                graph.ComputeDominance();
                graph.ComputeDominanceFrontier();
                block.Body = FindConditions(new HashSet<ControlFlowNode>(graph.Nodes.Skip(2)), graph.EntryPoint);
            }
        }
        void CreateEdge(ControlFlowNode source, ControlFlowNode destination)
        {
            ControlFlowEdge edge = new ControlFlowEdge(source, destination, JumpType.Normal);
            source.Outgoing.Add(edge);
            destination.Incoming.Add(edge);
        }
        public  ControlFlowGraph BuildGraph(IList<ILNode> nodes, ILLabel entryLabel)
        {

            int index = 0;
            List<ControlFlowNode> cfNodes = new List<ControlFlowNode>();
            ControlFlowNode entryPoint = new ControlFlowNode(index++, 0, ControlFlowNodeType.EntryPoint);
            cfNodes.Add(entryPoint);
            ControlFlowNode regularExit = new ControlFlowNode(index++, -1, ControlFlowNodeType.RegularExit);
            cfNodes.Add(regularExit);

            // Create graph nodes
            labelToCfNode = new Dictionary<ILLabel, ControlFlowNode>();
            Dictionary<ILNode, ControlFlowNode> astNodeToCfNode = new Dictionary<ILNode, ControlFlowNode>();
            foreach (ILBasicBlock node in nodes)
            {
                ControlFlowNode cfNode = new ControlFlowNode(index++, -1, ControlFlowNodeType.Normal);
                cfNodes.Add(cfNode);
                astNodeToCfNode[node] = cfNode;
                cfNode.UserData = node;

                // Find all contained labels
                foreach (ILLabel label in node.GetSelfAndChildrenRecursive<ILLabel>())
                {
                    labelToCfNode[label] = cfNode;
                }
            }

            // Entry endge
            ControlFlowNode entryNode = labelToCfNode[entryLabel];
            ControlFlowEdge entryEdge = new ControlFlowEdge(entryPoint, entryNode, JumpType.Normal);
            entryPoint.Outgoing.Add(entryEdge);
            entryNode.Incoming.Add(entryEdge);

            // Create edges
            foreach (ILBasicBlock node in nodes)
            {
                ControlFlowNode source = astNodeToCfNode[node];

                // Find all branches
                foreach (ILLabel target in node.GetSelfAndChildrenRecursive<ILExpression>(e => e.IsBranch()).SelectMany(e => e.GetBranchTargets()))
                {
                    ControlFlowNode destination;

                    // Labels which are out of out scope will not be in the collection
                    // Insert self edge only if we are sure we are a loop
                    if (labelToCfNode.TryGetValue(target, out destination) && (destination != source || target == node.Body.FirstOrDefault()))
                    {
                        ControlFlowEdge edge = new ControlFlowEdge(source, destination, JumpType.Normal);
                        source.Outgoing.Add(edge);
                        destination.Incoming.Add(edge);
                    }
                }
            }

            return new ControlFlowGraph(cfNodes.ToArray());
        }
      
        List<ILNode> FindLoops(HashSet<ControlFlowNode> scope, ControlFlowNode entryPoint, bool excludeEntryPoint)
        {
            List<ILNode> result = new List<ILNode>();

            // Do not modify entry data
            scope = new HashSet<ControlFlowNode>(scope);

            Queue<ControlFlowNode> agenda = new Queue<ControlFlowNode>();
            agenda.Enqueue(entryPoint);
            while (agenda.Count > 0)
            {
                ControlFlowNode node = agenda.Dequeue();

                // If the node is a loop header
                if (scope.Contains(node)
                    && node.DominanceFrontier.Contains(node)
                    && (node != entryPoint || !excludeEntryPoint))
                {
                    HashSet<ControlFlowNode> loopContents = FindLoopContent(scope, node);

                    // If the first expression is a loop condition
                    ILBasicBlock basicBlock = (ILBasicBlock) node.UserData;
                    ILExpression condExpr = null;
                    ILLabel trueLabel;
                    ILLabel falseLabel;
                    // It has to be just brtrue - any preceding code would introduce goto
                    if (basicBlock.MatchLastAndBr(GMCode.Bt, out trueLabel, out condExpr, out falseLabel) ||
                         basicBlock.MatchLastAndBr(GMCode.Pushenv, out falseLabel, out condExpr, out trueLabel) || // built it the same way from the dissasembler, this needs inverted
                        basicBlock.MatchLastAndBr(GMCode.Repeat, out trueLabel, out condExpr, out falseLabel) ) // repeate loop is built like a while
                       {
                        GMCode loopType = (basicBlock.Body.ElementAt(basicBlock.Body.Count - 2) as ILExpression).Code;
                        ControlFlowNode trueTarget;
                        labelToCfNode.TryGetValue(trueLabel, out trueTarget);
                        ControlFlowNode falseTarget;
                        labelToCfNode.TryGetValue(falseLabel, out falseTarget);

                        // If one point inside the loop and the other outside
                        if ((!loopContents.Contains(trueTarget) && loopContents.Contains(falseTarget)) ||
                            (loopContents.Contains(trueTarget) && !loopContents.Contains(falseTarget)))
                        {
                            loopContents.RemoveOrThrow(node);
                            scope.RemoveOrThrow(node);

                            bool mustNegate = false;
                            if (loopContents.Contains(falseTarget) || falseTarget == node)
                            {
                                // Negate the condition
                                mustNegate = true;
                                //  condExpr = new ILExpression(GMCode.Not, null, condExpr);
                                ILLabel tmp = trueLabel;
                                trueLabel = falseLabel;
                                falseLabel = tmp;
                            }
                            // HACK
                          
                            ControlFlowNode postLoopTarget;
                            labelToCfNode.TryGetValue(falseLabel, out postLoopTarget);
                            if (postLoopTarget != null)
                            {
                                // Pull more nodes into the loop
                                HashSet<ControlFlowNode> postLoopContents = FindDominatedNodes(scope, postLoopTarget);
                                var pullIn = scope.Except(postLoopContents).Where(n => node.Dominates(n));
                                loopContents.UnionWith(pullIn);
                            }


                            //Debug.Assert(false);
                            Debug.Assert(condExpr != null);
                            switch (loopType)
                            {
                                case GMCode.Pushenv:
                                    basicBlock.Body.RemoveTail(GMCode.Pushenv, GMCode.B);
                                    basicBlock.Body.Add(new ILWithStatement()
                                    {
                                        Condition = condExpr, // we never negate 
                                        Body = new ILBlock()
                                        {
                                            EntryGoto = new ILExpression(GMCode.B, trueLabel),
                                            Body = FindLoops(loopContents, node, false)
                                        }
                                    });
                                    break;
                                case GMCode.Bt: 
                                    basicBlock.Body.RemoveTail(GMCode.Bt, GMCode.B);
                                    basicBlock.Body.Add(new ILWhileLoop()
                                    {
                                        Condition = mustNegate ? condExpr : condExpr.NegateCondition(),
                                        Body = new ILBlock()
                                        {
                                            EntryGoto = new ILExpression(GMCode.B, trueLabel),
                                            Body = FindLoops(loopContents, node, false)
                                        }
                                    });
                                   
                                    break;
                                case GMCode.Repeat:
                                    basicBlock.Body.RemoveTail(GMCode.Repeat, GMCode.B);
                                    basicBlock.Body.Add(new ILRepeat()
                                    {
                                        Condition = condExpr, // we never negate 
                                        Body = new ILBlock()
                                        {
                                            EntryGoto = new ILExpression(GMCode.B, trueLabel),
                                            Body = FindLoops(loopContents, node, false)
                                        }
                                    });
                                   
                                    break;
                                    
                            }
                            basicBlock.Body.Add(new ILExpression(GMCode.B, falseLabel));


                            result.Add(basicBlock);

                            scope.ExceptWith(loopContents);
                        }
                    }

                    // Fallback method: while(true)
                    if (scope.Contains(node))
                    {
                        Debug.Assert(false);
                        result.Add(new ILBasicBlock()
                        {
                            Body = new List<ILNode>() {
                                ILLabel.Generate("Loop"),
                                new ILWhileLoop() {
                                    Condition = new ILExpression(GMCode.Constant, new ILValue(true)),
                                    Body = new ILBlock() {
                                        EntryGoto = new ILExpression(GMCode.B, (ILLabel)basicBlock.Body.First()),
                                        Body = FindLoops(loopContents, node, true)
                                    }
                                },
                            },
                        });

                        scope.ExceptWith(loopContents);
                    }
                }

                // Using the dominator tree should ensure we find the the widest loop first
                foreach (var child in node.DominatorTreeChildren)
                {
                    agenda.Enqueue(child);
                }
            }

            // Add whatever is left
            foreach (var node in scope)
            {
                result.Add((ILNode) node.UserData);
            }
            scope.Clear();

            return result;
        }

        List<ILNode> FindConditions(HashSet<ControlFlowNode> scope, ControlFlowNode entryNode)
        {
            List<ILNode> result = new List<ILNode>();

            // Do not modify entry data
            scope = new HashSet<ControlFlowNode>(scope);

            Stack<ControlFlowNode> agenda = new Stack<ControlFlowNode>();
            agenda.Push(entryNode);
            while (agenda.Count > 0)
            {
                ControlFlowNode node = agenda.Pop();

                // Find a block that represents a simple condition
                if (scope.Contains(node))
                {

                    ILBasicBlock block = (ILBasicBlock)node.UserData;
                   
                    {
                        ILLabel[] caseLabels;
                        IList<ILExpression> conditions;
                        ILLabel fallLabel;
                        // Switch

                        if (block.MatchLastAndBr(GMCode.Switch, out caseLabels, out conditions, out fallLabel))
                        {
                            ILSwitch ilSwitch = new ILSwitch() { Condition = conditions[0].Arguments[0].Arguments[1] };
                            block.Body[block.Body.Count - 2] = ilSwitch; // replace it, nothing else needs to be done!
                            result.Add(block); // except add it to the result, DOLT
                           
                            scope.RemoveOrThrow(node);// Remove the item so that it is not picked up as content

                            // Pull in code of cases
                            ControlFlowNode fallTarget = null;
                            labelToCfNode.TryGetValue(fallLabel, out fallTarget);

                            HashSet<ControlFlowNode> frontiers = new HashSet<ControlFlowNode>();
                            if (fallTarget != null)
                                frontiers.UnionWith(fallTarget.DominanceFrontier.Except(new[] { fallTarget }));

                            foreach (ILLabel condLabel in caseLabels)
                            {
                                ControlFlowNode condTarget = null;
                                labelToCfNode.TryGetValue(condLabel, out condTarget);
                                if (condTarget != null)
                                    frontiers.UnionWith(condTarget.DominanceFrontier.Except(new[] { condTarget }));
                            }
                            for (int i = 0; i < caseLabels.Length; i++)
                            {
                                ILLabel condLabel = caseLabels[i];

                                // Find or create new case block
                                ILSwitch.ILCase caseBlock = ilSwitch.Cases.FirstOrDefault(b => b.EntryGoto.Operand == condLabel);
                                if (caseBlock == null)
                                {
                                    caseBlock = new ILSwitch.ILCase()
                                    {
                                        Values = new List<ILExpression>(),
                                        EntryGoto = new ILExpression(GMCode.B, condLabel)
                                    };
                                    ilSwitch.Cases.Add(caseBlock);

                                    ControlFlowNode condTarget = null;
                                    labelToCfNode.TryGetValue(condLabel, out condTarget);
                                    if (condTarget != null && !frontiers.Contains(condTarget))
                                    {
                                        HashSet<ControlFlowNode> content = FindDominatedNodes(scope, condTarget);
                                        scope.ExceptWith(content);
                                        foreach (var con in FindConditions(content, condTarget)) caseBlock.Body.Add(con);
                                        //   caseBlock.Body.AddRange(FindConditions(content, condTarget));
                                        // Add explicit break which should not be used by default, but the goto removal might decide to use it
                                        caseBlock.Body.Add(new ILBasicBlock()
                                        {
                                            Body = {
                                                ILLabel.Generate("SwitchBreak" ,(int)nextLabelIndex++),
                                                new ILExpression(GMCode.LoopOrSwitchBreak, null)
                                            }
                                        });
                                    }
                                }
                                caseBlock.Values.Add(conditions[i].Arguments[0].Arguments[0]);
                            }

                            // Heuristis to determine if we want to use fallthough as default case
                            if (fallTarget != null && !frontiers.Contains(fallTarget))
                            {
                                HashSet<ControlFlowNode> content = FindDominatedNodes(scope, fallTarget);
                                if (content.Any())
                                {
                                    var caseBlock = new ILSwitch.ILCase() { EntryGoto = new ILExpression(GMCode.B, fallLabel) };
                                    ilSwitch.Cases.Add(caseBlock);
                                    block.Body.RemoveTail(GMCode.B);

                                    scope.ExceptWith(content);
                                    foreach (var con in FindConditions(content, fallTarget)) caseBlock.Body.Add(con);
                                    
                                    // Add explicit break which should not be used by default, but the goto removal might decide to use it
                                    caseBlock.Body.Add(new ILBasicBlock()
                                    { 
                                        Body = {
                                            ILLabel.Generate("SwitchBreak" ,(int)nextLabelIndex++),
                                            new ILExpression(GMCode.LoopOrSwitchBreak, null)
                                        }
                                    });
                                }
                            }
                        }
                     //   Debug.Assert((block.Body.First() as ILLabel).Name != "L1938");
                        // Two-way branch
                        ILLabel trueLabel;
                        ILLabel falseLabel;
                        IList<ILExpression> condExprs;
                        if (block.MatchLastAndBr(GMCode.Bt, out trueLabel, out condExprs, out falseLabel) // be sure to invert this condition
                            && condExprs.Count > 0)  // its resolved
                        {
 
                            ILExpression condExpr = condExprs[0];
                            IList<ILNode> body = block.Body;
                            // this is a simple condition, skip anything short curiket for now
                            // Match a condition patern
                            // Convert the brtrue to ILCondition
                            ILCondition ilCond = new ILCondition()
                            {
                                Condition = condExpr, //code == GMCode.Bf ? condExpr : condExpr.NegateCondition(),
                                TrueBlock = new ILBlock() { EntryGoto = new ILExpression(GMCode.B, trueLabel) },
                                FalseBlock = new ILBlock() { EntryGoto = new ILExpression(GMCode.B, falseLabel) }
                            };
                            block.Body.RemoveTail(GMCode.Bt, GMCode.B);
                            block.Body.Add(ilCond);
                            result.Add(block);

                            // Remove the item immediately so that it is not picked up as content
                            scope.RemoveOrThrow(node);

                            ControlFlowNode trueTarget = null;
                            labelToCfNode.TryGetValue(trueLabel, out trueTarget);
                            ControlFlowNode falseTarget = null;
                            labelToCfNode.TryGetValue(falseLabel, out falseTarget);
   
                            // Pull in the conditional code
                            if (trueTarget != null && HasSingleEdgeEnteringBlock(trueTarget))
                            {
                                HashSet<ControlFlowNode> content = FindDominatedNodes(scope, trueTarget);
                                scope.ExceptWith(content);
                                foreach (var con in FindConditions(content, trueTarget)) ilCond.TrueBlock.Body.Add(con);
                            }
                            if (falseTarget != null && HasSingleEdgeEnteringBlock(falseTarget))
                            {
                                HashSet<ControlFlowNode> content = FindDominatedNodes(scope, falseTarget);
                                scope.ExceptWith(content);
                                foreach (var con in FindConditions(content, falseTarget)) ilCond.FalseBlock.Body.Add(con);
                            }
                       


                        }
                    }

                    // Add the node now so that we have good ordering
                    if (scope.Contains(node))
                    {
                        result.Add((ILNode)node.UserData);
                        scope.Remove(node);
                    }
                }

                // depth-first traversal of dominator tree
                for (int i = node.DominatorTreeChildren.Count - 1; i >= 0; i--)
                {
                    agenda.Push(node.DominatorTreeChildren[i]);
                }
            }

            // Add whatever is left
            foreach (var node in scope)
            {
                result.Add((ILNode)node.UserData);
            }

            return result;
        }

        public static bool HasSingleEdgeEnteringBlock(ControlFlowNode node)
        {
            return node.Incoming.Count(edge => !node.Dominates(edge.Source)) == 1;
        }

        public static HashSet<ControlFlowNode> FindDominatedNodes(HashSet<ControlFlowNode> scope, ControlFlowNode head)
        {
            HashSet<ControlFlowNode> agenda = new HashSet<ControlFlowNode>();
            HashSet<ControlFlowNode> result = new HashSet<ControlFlowNode>();
            agenda.Add(head);

            while (agenda.Count > 0)
            {
                ControlFlowNode addNode = agenda.First();
                agenda.Remove(addNode);

                if (scope.Contains(addNode) && head.Dominates(addNode) && result.Add(addNode))
                {
                    foreach (var successor in addNode.Successors)
                    {
                        agenda.Add(successor);
                    }
                }
            }

            return result;
        }

        public static HashSet<ControlFlowNode> FindLoopContent(HashSet<ControlFlowNode> scope, ControlFlowNode head)
        {
            var viaBackEdges = head.Predecessors.Where(p => head.Dominates(p));
            HashSet<ControlFlowNode> agenda = new HashSet<ControlFlowNode>(viaBackEdges);
            HashSet<ControlFlowNode> result = new HashSet<ControlFlowNode>();

            while (agenda.Count > 0)
            {
                ControlFlowNode addNode = agenda.First();
                agenda.Remove(addNode);

                if (scope.Contains(addNode) && head.Dominates(addNode) && result.Add(addNode))
                {
                    foreach (var predecessor in addNode.Predecessors)
                    {
                        agenda.Add(predecessor);
                    }
                }
            }
            if (scope.Contains(head))
                result.Add(head);

            return result;
        }
    }
}
