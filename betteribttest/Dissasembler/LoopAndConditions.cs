using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.FlowAnalysis;
using System.Diagnostics;

namespace GameMaker.Dissasembler
{
    public class LoopsAndConditions
    {
        Dictionary<ILLabel, ControlFlowNode> labelToCfNode = new Dictionary<ILLabel, ControlFlowNode>();
        public ControlFlowNode LabelToNode(ILLabel l)
        {
            return labelToCfNode[l];
        }
        readonly GMContext context;

        uint nextLabelIndex = 0;

        public LoopsAndConditions(GMContext context)//DecompilerContext context)
        {
            this.context = context;
        }

        public void FindLoops(ILBlock block)
        {
            if (block.Body.Count > 0)
            {
                ControlFlowGraph graph;
                graph = BuildGraph(block.Body, (ILLabel)block.EntryGoto.Operand);
                graph.ComputeDominance();
                graph.ComputeDominanceFrontier();
                if (context.Debug) graph.ExportGraph().Save(context.MakeDebugFileName("graph_loop.dot"));
                block.Body = FindLoops(new HashSet<ControlFlowNode>(graph.Nodes.Skip(2)), graph.EntryPoint, false);
               
            }
        }
        public void FindWiths(ILBlock block)
        {
            if (block.Body.Count > 0)
            {
                List<ILBasicBlock> pushEnv = new List<ILBasicBlock>();
                List<ILBasicBlock> popEnv = new List<ILBasicBlock>();
                foreach (var bb in block.GetSelfAndChildrenRecursive<ILBasicBlock>())
                {
                    foreach(var n in bb.GetChildren())
                    {
                        ILExpression e = n as ILExpression;
                        if(e != null)
                        {
                            if (e.Code == GMCode.Pushenv) pushEnv.Add(bb);
                            if (e.Code == GMCode.Popenv) popEnv.Add(bb);
                        }
                    }
                }
                if (pushEnv.Count == 0 && popEnv.Count == 0) return;
                Debug.WriteLine("WOOO");
               
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
                if (context.Debug) graph.ExportGraph().Save(context.MakeDebugFileName("graph_condition.dot"));
                block.Body = FindConditions(new HashSet<ControlFlowNode>(graph.Nodes.Skip(2)), graph.EntryPoint);
            }
        }
        void CreateEdge(ControlFlowNode source, ControlFlowNode destination)
        {
            ControlFlowEdge edge = new ControlFlowEdge(source, destination, JumpType.Normal);
            source.Outgoing.Add(edge);
            destination.Incoming.Add(edge);
        }
        public ControlFlowGraph BuildGraph(IList<ILNode> nodes, ILLabel entryLabel)
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
                        basicBlock.MatchLastAndBr(GMCode.Bf, out falseLabel, out condExpr, out trueLabel)||
                         basicBlock.MatchLastAndBr(GMCode.Pushenv, out falseLabel, out condExpr, out trueLabel) )// built it the same way from the dissasembler
                       {
                        bool ispushEnv = (basicBlock.Body.ElementAt(basicBlock.Body.Count - 2) as ILExpression).Code == GMCode.Pushenv;
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


                        
                            Debug.Assert(condExpr != null);
                            

                          
                            if (ispushEnv)
                            {
                                // Use loop to implement the brtrue
                                basicBlock.Body.RemoveTail(GMCode.Pushenv, GMCode.B);
                                basicBlock.Body.Add(new ILWithStatement()
                                {
                                    Enviroment = condExpr, // we never negate 
                                    EnviromentName = context.InstanceToString(condExpr),
                                    Body = new ILBlock()
                                    {
                                        EntryGoto = new ILExpression(GMCode.B, trueLabel),
                                        Body = FindLoops(loopContents, node, false)
                                    }
                                });
                            }
                            else
                            {
                                // Use loop to implement the brtrue
                                basicBlock.Body.RemoveRange(basicBlock.Body.Count - 2, 2);
                               // basicBlock.Body.RemoveTail(GMCode.Bt, GMCode.B);

                                basicBlock.Body.Add(new ILWhileLoop()
                                {
                                    Condition = mustNegate ? condExpr : condExpr.NegateCondition(),
                                    BodyBlock = new ILBlock()
                                    {
                                        EntryGoto = new ILExpression(GMCode.B, trueLabel),
                                        Body = FindLoops(loopContents, node, false)
                                    }
                                });

                               
                            }

                            basicBlock.Body.Add(new ILExpression(GMCode.B, falseLabel));
                            result.Add(basicBlock);

                            scope.ExceptWith(loopContents);
                        }
                    }

                    // Fallback method: while(true)
                    if (scope.Contains(node))
                    {
                        result.Add(new ILBasicBlock()
                        {
                            Body = new List<ILNode>() {
                                new ILLabel() { Name = "Loop_" + (nextLabelIndex++) },
                                new ILWhileLoop() {
                                    BodyBlock = new ILBlock() {
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
                        // Switch
                        List<ILExpression> cases;
                        ILLabel[] caseLabels;
                        ILExpression switchCondition;
                        ILLabel fallLabel;
                        //   IList<ILExpression> cases; out IList<ILExpression> arg, out ILLabel fallLabel)
                        // matches a switch statment, not sure how the hell I am going to do this
                        if (block.MatchLastAndBr(GMCode.Switch,out caseLabels, out cases, out fallLabel))
                        {
                        //    Debug.Assert(fallLabel == endBlock); // this should be true
                            switchCondition = cases[0]; // thats the switch arg
                            cases.RemoveAt(0); // remove the switch condition
                        
                            // Replace the switch code with ILSwitch
                            ILSwitch ilSwitch = new ILSwitch() { Condition = switchCondition };
                            block.Body.RemoveTail(GMCode.Switch, GMCode.B);
                            block.Body.Add(ilSwitch);
                            block.Body.Add(new ILExpression(GMCode.B, fallLabel));
                            result.Add(block);

                            // Remove the item so that it is not picked up as content
                            scope.RemoveOrThrow(node);

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
                                ILSwitch.CaseBlock caseBlock = ilSwitch.CaseBlocks.FirstOrDefault(b => b.EntryGoto.Operand == condLabel);
                                if (caseBlock == null)
                                {
                                    caseBlock = new ILSwitch.CaseBlock()
                                    {
                                        Values = new List<ILExpression>(),
                                        EntryGoto = new ILExpression(GMCode.B, condLabel)
                                    };
                                    ilSwitch.CaseBlocks.Add(caseBlock);

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
                                                new ILLabel() { Name = "SwitchBreak_" + (nextLabelIndex++) },
                                                new ILExpression(GMCode.LoopOrSwitchBreak, null)
                                            }
                                        });
                                    }
                                }
                                caseBlock.Values.Add(cases[i].Arguments[0]);
                            }

                            // Heuristis to determine if we want to use fallthough as default case
                            if (fallTarget != null && !frontiers.Contains(fallTarget))
                            {
                                HashSet<ControlFlowNode> content = FindDominatedNodes(scope, fallTarget);
                                if (content.Any())
                                {
                                    var caseBlock = new ILSwitch.CaseBlock() { EntryGoto = new ILExpression(GMCode.B, fallLabel) };
                                    ilSwitch.CaseBlocks.Add(caseBlock);
                                    block.Body.RemoveTail(GMCode.B);

                                    scope.ExceptWith(content);
                                    foreach (var con in FindConditions(content, fallTarget)) caseBlock.Body.Add(con);

                                    // Add explicit break which should not be used by default, but the goto removal might decide to use it
                                    caseBlock.Body.Add(new ILBasicBlock()
                                    {
                                        Body = {
                                            new ILLabel() { Name = "SwitchBreak_" + (nextLabelIndex++) },
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
                        List<ILExpression> condExprs;
                        if (block.MatchLastAndBr(GMCode.Bf, out falseLabel, out condExprs, out trueLabel)
                            || block.MatchLastAndBr(GMCode.Bt, out falseLabel, out condExprs, out trueLabel) // be sure to invert this condition
                            && condExprs[0].Code != GMCode.Pop)  // its resolved
                        {
                            GMCode code = (block.Body[block.Body.Count - 2] as ILExpression).Code;
                            ILExpression condExpr = condExprs[0];
                            IList<ILNode> body = block.Body;
                            // this is a simple condition, skip anything short curiket for now
                            // Match a condition patern
                            // Convert the brtrue to ILCondition
                            ILCondition ilCond = new ILCondition()
                            {
                                Condition = code == GMCode.Bf ? condExpr : condExpr.NegateCondition(),
                                TrueBlock = new ILBlock() { EntryGoto = new ILExpression(GMCode.B, trueLabel) },
                                FalseBlock = new ILBlock() { EntryGoto = new ILExpression(GMCode.B, falseLabel) }
                            };
                            block.Body.RemoveTail(code, GMCode.B);
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
