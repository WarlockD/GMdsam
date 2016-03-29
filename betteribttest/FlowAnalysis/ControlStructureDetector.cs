using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace betteribttest.FlowAnalysis
{
    public enum ControlStructureType
    {
        /// <summary>
        /// The root block of the method
        /// </summary>
        Root,
        /// <summary>
        /// A nested control structure representing a loop.
        /// </summary>
        Loop,
        /// <summary>
        /// A nested control structure representing a try block.
        /// </summary>
        Try,
        /// <summary>
        /// A nested control structure representing a catch, finally, or fault block.
        /// </summary>
        Handler,
        /// <summary>
        /// A nested control structure representing an exception filter block.
        /// </summary>
        Filter
    }

    /// <summary>
    /// Represents the structure detected by the <see cref="ControlStructureDetector"/>.
    /// 
    /// This is a tree of ControlStructure nodes. Each node contains a set of CFG nodes, and every CFG node is contained in exactly one ControlStructure node.
    /// </summary>
    public class ControlStructure
    {
        public readonly ControlStructureType Type;
        public readonly List<ControlStructure> Children = new List<ControlStructure>();

        /// <summary>
        /// The nodes in this control structure.
        /// </summary>
        public readonly HashSet<ControlFlowNode> Nodes;

        /// <summary>
        /// The nodes in this control structure and in all child control structures.
        /// </summary>
        public readonly HashSet<ControlFlowNode> AllNodes;

        /// <summary>
        /// The entry point of this control structure.
        /// </summary>
        public readonly ControlFlowNode EntryPoint;

        public ControlStructure(HashSet<ControlFlowNode> nodes, ControlFlowNode entryPoint, ControlStructureType type)
        {
            if (nodes == null)
                throw new ArgumentNullException("nodes");
            this.Nodes = nodes;
            this.EntryPoint = entryPoint;
            this.Type = type;
            this.AllNodes = new HashSet<ControlFlowNode>(nodes);
        }
    }
    class ControlStructureDetector
    {
        public static ControlStructure DetectStructure(ControlFlowGraph g,  CancellationToken cancellationToken)
        {
            ControlStructure root = new ControlStructure(new HashSet<ControlFlowNode>(g.Nodes), g.EntryPoint, ControlStructureType.Root);
            // First build a structure tree out of the exception table
           // DetectExceptionHandling(root, g, exceptionHandlers);
            // Then run the loop detection.
            DetectLoops(g, root, cancellationToken);
            return root;
        }
        #region Loop Detection
        // Loop detection works like this:
        // We find a top-level loop by looking for its entry point, which is characterized by a node dominating its own predecessor.
        // Then we determine all other nodes that belong to such a loop (all nodes which lead to the entry point, and are dominated by it).
        // Finally, we check whether our result conforms with potential existing exception structures, and create the substructure for the loop if successful.

        // This algorithm is applied recursively for any substructures (both detected loops and exception blocks)

        // But maybe we should get rid of this complex stuff and instead treat every backward jump as a loop?
        // That should still work with the IL produced by compilers, and has the advantage that the detected loop bodies are consecutive IL regions.

        static void DetectLoops(ControlFlowGraph g, ControlStructure current, CancellationToken cancellationToken)
        {
            if (!current.EntryPoint.IsReachable)
                return;
            g.ResetVisited();
            cancellationToken.ThrowIfCancellationRequested();
            FindLoops(current, current.EntryPoint);
            foreach (ControlStructure loop in current.Children)
                DetectLoops(g, loop, cancellationToken);
        }

        static void FindLoops(ControlStructure current, ControlFlowNode node)
        {
            if (node.Visited)
                return;
            node.Visited = true;
            if (current.Nodes.Contains(node)
                && node.DominanceFrontier.Contains(node)
                && !(node == current.EntryPoint && current.Type == ControlStructureType.Loop))
            {
                HashSet<ControlFlowNode> loopContents = new HashSet<ControlFlowNode>();
                FindLoopContents(current, loopContents, node, node);
                List<ControlStructure> containedChildStructures = new List<ControlStructure>();
                bool invalidNesting = false;
                foreach (ControlStructure childStructure in current.Children)
                {
                    if (childStructure.AllNodes.IsSubsetOf(loopContents))
                    {
                        containedChildStructures.Add(childStructure);
                    }
                    else if (childStructure.AllNodes.Intersect(loopContents).Any())
                    {
                        invalidNesting = true;
                    }
                }
                if (!invalidNesting)
                {
                    current.Nodes.ExceptWith(loopContents);
                    ControlStructure ctl = new ControlStructure(loopContents, node, ControlStructureType.Loop);
                    foreach (ControlStructure childStructure in containedChildStructures)
                    {
                        ctl.Children.Add(childStructure);
                        current.Children.Remove(childStructure);
                        ctl.Nodes.ExceptWith(childStructure.AllNodes);
                    }
                    current.Children.Add(ctl);
                }
            }
            foreach (var edge in node.Outgoing)
            {
                FindLoops(current, edge.Target);
            }
        }

        static void FindLoopContents(ControlStructure current, HashSet<ControlFlowNode> loopContents, ControlFlowNode loopHead, ControlFlowNode node)
        {
            if (current.AllNodes.Contains(node) && loopHead.Dominates(node) && loopContents.Add(node))
            {
                foreach (var edge in node.Incoming)
                {
                    FindLoopContents(current, loopContents, loopHead, edge.Source);
                }
            }
        }
        #endregion
    }
}
