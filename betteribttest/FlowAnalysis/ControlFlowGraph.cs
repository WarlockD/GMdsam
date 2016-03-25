using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace betteribttest.FlowAnalysis
{
    /// <summary>
	/// Contains the control flow graph.
	/// </summary>
	/// <remarks>Use ControlFlowGraph builder to create instances of the ControlFlowGraph.</remarks>
	public sealed class ControlFlowGraph
    {
        readonly ReadOnlyCollection<ControlFlowNode> nodes;

        public ControlFlowNode EntryPoint
        {
            get { return nodes[0]; }
        }

        public ControlFlowNode RegularExit
        {
            get { return nodes[1]; }
        }

        public ControlFlowNode ExceptionalExit
        {
            get { return nodes[2]; }
        }

        public ReadOnlyCollection<ControlFlowNode> Nodes
        {
            get { return nodes; }
        }

        internal ControlFlowGraph(ControlFlowNode[] nodes)
        {
            this.nodes = new ReadOnlyCollection<ControlFlowNode>(nodes);
            Debug.Assert(EntryPoint.NodeType == ControlFlowNodeType.EntryPoint);
            Debug.Assert(RegularExit.NodeType == ControlFlowNodeType.RegularExit);
        }

        public GraphVizGraph ExportGraph()
        {
            GraphVizGraph graph = new GraphVizGraph();
            foreach (ControlFlowNode node in nodes)
            {
                graph.AddNode(new GraphVizNode(node.BlockIndex) { label = node.ToString(), shape = "box" });
            }
            foreach (ControlFlowNode node in nodes)
            {
                foreach (ControlFlowEdge edge in node.Outgoing)
                {
                    GraphVizEdge e = new GraphVizEdge(edge.Source.BlockIndex, edge.Target.BlockIndex);
                    switch (edge.Type)
                    {
                        case JumpType.Normal:
                            break;
                        default:
                            e.color = "gray";
                            //e.constraint = false;
                            break;
                    }
                    graph.AddEdge(e);
                }
                if (node.ImmediateDominator != null)
                {
                    graph.AddEdge(new GraphVizEdge(node.ImmediateDominator.BlockIndex, node.BlockIndex) { color = "green", constraint = false });
                }
            }
            return graph;
        }

        /// <summary>
        /// Resets "Visited" to false for all nodes in this graph.
        /// </summary>
        public void ResetVisited()
        {
            foreach (ControlFlowNode node in nodes)
            {
                node.Visited = false;
            }
        }

        /// <summary>
        /// Computes the dominator tree.
        /// </summary>
        public void ComputeDominance(CancellationToken cancellationToken = default(CancellationToken))
        {
            // A Simple, Fast Dominance Algorithm
            // Keith D. Cooper, Timothy J. Harvey and Ken Kennedy

            EntryPoint.ImmediateDominator = EntryPoint;
            bool changed = true;
            while (changed)
            {
                changed = false;
                ResetVisited();

                cancellationToken.ThrowIfCancellationRequested();

                // for all nodes b except the entry point
                EntryPoint.TraversePreOrder(
                    b => b.Successors,
                    b =>
                    {
                        if (b != EntryPoint)
                        {
                            ControlFlowNode newIdom = b.Predecessors.First(block => block.Visited && block != b);
                            // for all other predecessors p of b
                            foreach (ControlFlowNode p in b.Predecessors)
                            {
                                if (p != b && p.ImmediateDominator != null)
                                {
                                    newIdom = FindCommonDominator(p, newIdom);
                                }
                            }
                            if (b.ImmediateDominator != newIdom)
                            {
                                b.ImmediateDominator = newIdom;
                                changed = true;
                            }
                        }
                    });
            }
            EntryPoint.ImmediateDominator = null;
            foreach (ControlFlowNode node in nodes)
            {
                if (node.ImmediateDominator != null)
                    node.ImmediateDominator.DominatorTreeChildren.Add(node);
            }
        }

        static ControlFlowNode FindCommonDominator(ControlFlowNode b1, ControlFlowNode b2)
        {
            // Here we could use the postorder numbers to get rid of the hashset, see "A Simple, Fast Dominance Algorithm"
            HashSet<ControlFlowNode> path1 = new HashSet<ControlFlowNode>();
            while (b1 != null && path1.Add(b1))
                b1 = b1.ImmediateDominator;
            while (b2 != null)
            {
                if (path1.Contains(b2))
                    return b2;
                else
                    b2 = b2.ImmediateDominator;
            }
            throw new Exception("No common dominator found!");
        }

        /// <summary>
        /// Computes dominance frontiers.
        /// This method requires that the dominator tree is already computed!
        /// </summary>
        public void ComputeDominanceFrontier()
        {
            ResetVisited();

            EntryPoint.TraversePostOrder(
                b => b.DominatorTreeChildren,
                n =>
                {
                    //logger.WriteLine("Calculating dominance frontier for " + n.Name);
                    n.DominanceFrontier = new HashSet<ControlFlowNode>();
                    // DF_local computation
                    foreach (ControlFlowNode succ in n.Successors)
                    {
                        if (succ.ImmediateDominator != n)
                        {
                            //logger.WriteLine("  local: " + succ.Name);
                            n.DominanceFrontier.Add(succ);
                        }
                    }
                    // DF_up computation
                    foreach (ControlFlowNode child in n.DominatorTreeChildren)
                    {
                        foreach (ControlFlowNode p in child.DominanceFrontier)
                        {
                            if (p.ImmediateDominator != n)
                            {
                                //logger.WriteLine("  DF_up: " + p.Name + " (child=" + child.Name);
                                n.DominanceFrontier.Add(p);
                            }
                        }
                    }
                });
        }
    }
}
