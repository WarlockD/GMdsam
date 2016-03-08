using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
//https://bitbucket.org/mstrobel/procyon/src/fa7db1e6bf42bfee84f6f4c58fd9668ee290802e/Procyon.CompilerTools/src/main/java/com/strobel/assembler/flowanalysis/ControlFlowGraph.java?at=default&fileviewer=file-view-default
namespace betteribttest
{
    public interface IBlock<T>
    {
        void Accept(T input);
    }
    public enum ControlFlowNodeType
    {
        Normal,
        EntryPoint,
        RegularExit,
        ExceptionalExit,
        CatchHandler,
        FinallyHandler,
        EndFinally
    }
    public enum JumpType
    {
        /**
         * A regular control flow edge.
         */
        Normal,

        /**
         * Jump to exception handler (an exception occurred).
         */
        JumpToExceptionHandler,

        /**
         * Jump from try block (not a real jump, as the finally handler executes first).
         */
        LeaveTry,

        /**
         * Jump at the end of a finally block.
         */
        EndFinally
    }
    public class ControlFlowEdge
    {
        public ControlFlowNode Source { get; private set; }
        public ControlFlowNode Target { get; private set; }
        public JumpType Type { get; private set; }

        public ControlFlowEdge(ControlFlowNode source, ControlFlowNode target, JumpType type)
        {
            Debug.Assert(source != null && target != null);
            Source = source;
            Target = target;
            Type = type;
        }
        public override bool Equals(object obj)
        {
            ControlFlowEdge edge = obj as ControlFlowEdge;
            if (edge == null) return false;
            return edge.Source == Source && edge.Target == Target;
        }
        public override string ToString()
        {
            switch (Type)
            {
                case JumpType.Normal: return "#" + Target.BlockIndex;
                default:
                    return Type.GetType().GetEnumNames()[(int)Type] + ":#" + Target.BlockIndex;
            }
        }
        public override int GetHashCode()
        {
            return Source.GetHashCode() ^ Target.GetHashCode();
        }
    }
    public class ControlFlowNode : IComparable<ControlFlowNode>
    {

        public ControlFlowNodeType NodeType { get; private set; }
        public ControlFlowNode EndFinallyNode { get; private set; }
        public List<ControlFlowNode> DominatorTreeChildren = new List<ControlFlowNode>();
        private LinkedHashSet<ControlFlowNode> _dominanceFrontier = new LinkedHashSet<ControlFlowNode>();
        public LinkedHashSet<ControlFlowNode> DomianceFrontier {  get { return _dominanceFrontier; } }
        private List<ControlFlowEdge> _incoming = new List<ControlFlowEdge>();
        private List<ControlFlowEdge> _outgoing = new List<ControlFlowEdge>();
        public List<ControlFlowEdge> Incomming { get { return _incoming; } }
        public List<ControlFlowEdge> Outgoing { get { return _outgoing; } }
        public ControlFlowNode ImmediateDominator { get; set; }
        public Instruction Start { get; set; }
        public Instruction End { get; set; }
        public int BlockIndex { get; private set; }
        public int Offset { get; private set; }
        public ControlFlowNode CopyFrom { get; set; }
        public bool Visited { get; set; }
        public bool isReachable { get { return ImmediateDominator != null || NodeType == ControlFlowNodeType.EntryPoint; } }
        public Object UserData { get; set; }
        public ControlFlowNode(int blockIndex, int offset, ControlFlowNodeType nodeType)
        {
            BlockIndex = blockIndex;
            Offset = offset;
            NodeType = nodeType;
            EndFinallyNode = null;
            Start = null;
            End = null;
        }
        public ControlFlowNode(int blockIndex, Instruction start, Instruction end)
        {
            BlockIndex = blockIndex;
            Debug.Assert(start != null && end != null);
            Start = start;
            End = end;
            Offset = start.Address;
            NodeType = ControlFlowNodeType.Normal;
            EndFinallyNode = null;
        }
        public bool Succeds(ControlFlowNode other)
        {
            if (other == null) return false;
            foreach (var i in _incoming) if (i.Source == other) return true;
            return false;
        }
        public bool Precedes(ControlFlowNode other)
        {
            if (other == null) return false;
            foreach (var i in _outgoing) if (i.Source == other) return true;
            return false;
        }

        public IEnumerable<ControlFlowNode> Predecessors { get { return _incoming as IEnumerable<ControlFlowNode>; } }
        public IEnumerable<ControlFlowNode> Successors { get { return _incoming as IEnumerable<ControlFlowNode>; } }
        public IEnumerable<Instruction> Instructions
        {
            get
            {
                var start = Start;
                while (start != null)
                {
                    yield return start;
                    if (start == End) break;
                    start = start.Next;
                }
            }
        }
        public void TraversePreOrder(Func<ControlFlowNode, IEnumerable<ControlFlowNode>> children, Action<ControlFlowNode> visitAction)
        {
            if (Visited) return;
            Visited = true;
            visitAction(this);
            foreach (var child in children(this)) child.TraversePreOrder(children, visitAction);
        }
        public void TraversePostOrder(Func<ControlFlowNode, IEnumerable<ControlFlowNode>> children, Action<ControlFlowNode> visitAction)
        {
            if (Visited) return;
            Visited = true;
            foreach (var child in children(this)) child.TraversePostOrder(children, visitAction);
            visitAction(this);
        }
        public bool Dominates(ControlFlowNode node)
        {
            ControlFlowNode current = node;
            while (current != null)
            {
                if (current == this) return true;
                current = current.ImmediateDominator;
            }
            return false;
        }

        public int CompareTo(ControlFlowNode other)
        {
            return BlockIndex.CompareTo(other.BlockIndex);
        }
        public static Predicate<ControlFlowNode> REACHABLE_PREDICATE = node => node.isReachable;
        public override string ToString()
        {
            StringWriter ret = new StringWriter();
            System.CodeDom.Compiler.IndentedTextWriter sw = new System.CodeDom.Compiler.IndentedTextWriter(ret);
            switch (NodeType)
            {
                case ControlFlowNodeType.Normal:
                    sw.Write("Block #{0}", BlockIndex);
                    if (Start != null) sw.Write(": {0} to {1}", Start.Address, End.Address);
                    break;
                default:
                    sw.Write("Block #{0}: {1}", BlockIndex, NodeType.GetType().GetEnumName(NodeType));
                    break;
            }
            sw.Indent++;
            if (_dominanceFrontier.Count > 0)
            {
                sw.WriteLine();
                sw.Write("DominanceFrontier: ");
                int[] blockIndexes = new int[_dominanceFrontier.Count];
                int i = 0;
                foreach (var node in _dominanceFrontier) blockIndexes[i++] = node.BlockIndex;
                Array.Sort(blockIndexes);
                sw.Write(string.Join(",", blockIndexes));
            }
            foreach (var instruction in Instructions)
            {
                sw.WriteLine();
                sw.Write(instruction.ToString());
            }
            sw.Indent--;
            return ret.ToString();
        }
    };

    public class ControlFlowGraph : ITextOut
    {
        List<ControlFlowNode> _nodes;
        public ControlFlowNode EntryPoint { get { return _nodes[0]; } }
        public ControlFlowNode RegularExit { get { return _nodes[1]; } }
        public ControlFlowNode ExceptionalExit { get { return _nodes[2]; } }
        public List<ControlFlowNode> Nodes { get { return _nodes; } }
        public ControlFlowGraph(params ControlFlowNode[] nodes)
        {
            _nodes = new List<ControlFlowNode>();
            foreach (var node in nodes)
            {
                Debug.Assert(node != null);
                _nodes.Add(node);
            }
            //   _nodes = ArrayUtilities.asUnmodifiableList(VerifyArgument.noNullElements(nodes, "nodes"));
            Debug.Assert(_nodes.Count >= 3);
            Debug.Assert(EntryPoint.NodeType == ControlFlowNodeType.EntryPoint);
            Debug.Assert(EntryPoint.NodeType == ControlFlowNodeType.RegularExit);
            Debug.Assert(EntryPoint.NodeType == ControlFlowNodeType.ExceptionalExit);
        }
        public void ResetVisited() { foreach (var node in _nodes) node.Visited = false; }

        bool DomianceVisitorAction(ControlFlowNode b)
        {
            if (b == EntryPoint) return false;
            ControlFlowNode newImmediateDominator = null;
            foreach (var p in b.Predecessors)
            {
                if (p.Visited && p != b)
                {
                    newImmediateDominator = p;
                    break;
                }
            }
            if (newImmediateDominator == null) throw new Exception("Could not compute new immediate dominator!");
            foreach (var p in b.Predecessors)
            {
                if (p != b && p.ImmediateDominator != null)
                {
                    newImmediateDominator = findCommonDominator(p, newImmediateDominator);
                }
            }

            if (b.ImmediateDominator != newImmediateDominator)
            {
                b.ImmediateDominator = newImmediateDominator;
                return true;
            }
            else return false;
        }
        public void ComputeDomiance(ref bool cancelled)
        {
            ControlFlowNode entryPoint = EntryPoint;
            entryPoint.ImmediateDominator = entryPoint;
            bool changed = true;
            while (changed)
            {
                changed = false;
                ResetVisited();
                if (cancelled) throw new Exception("Cancelled");
                entryPoint.TraversePreOrder(input => input.Successors, delegate (ControlFlowNode b) {
                    if (b == entryPoint) return;
                    ControlFlowNode newImmediateDominator = null;
                    foreach (var p in b.Predecessors)
                    {
                        if (p.Visited && p != b)
                        {
                            newImmediateDominator = p;
                            break;
                        }
                    }
                    if (newImmediateDominator == null) throw new Exception("Could not compute new immediate dominator!");
                    foreach (var p in b.Predecessors)
                    {
                        if (p != b && p.ImmediateDominator != null)
                        {
                            newImmediateDominator = findCommonDominator(p, newImmediateDominator);
                        }
                    }

                    if (b.ImmediateDominator != newImmediateDominator)
                    {
                        b.ImmediateDominator = newImmediateDominator;
                        changed= true;
                    }
                }); 
            }
            entryPoint.ImmediateDominator = null;
            foreach (var node in _nodes)
            {
                ControlFlowNode immediateDominator = node.ImmediateDominator;

                if (immediateDominator != null) immediateDominator.DominatorTreeChildren.Add(node);
            }
        }
        public void computeDominanceFrontier()
        {
            ResetVisited();
            EntryPoint.TraversePostOrder(o => o.DominatorTreeChildren, delegate (ControlFlowNode n)
             {
                 ISet<ControlFlowNode> dominanceFrontier = n.DomianceFrontier;

                 dominanceFrontier.Clear();
                 foreach (var s in n.Successors) if (s.ImmediateDominator != n) dominanceFrontier.Add(s);
                 foreach (var child in n.DominatorTreeChildren)
                 {
                     foreach (var p in child.DomianceFrontier) if (p.ImmediateDominator != n) dominanceFrontier.Add(p);
                 }
             }
             );
        }

        public static ControlFlowNode findCommonDominator(ControlFlowNode a, ControlFlowNode b)
        {
            ISet<ControlFlowNode> path1 = new LinkedHashSet<ControlFlowNode>();

            ControlFlowNode node1 = a;
            ControlFlowNode node2 = b;

            while (node1 != null && path1.Add(node1)) node1 = node1.ImmediateDominator;

            while (node2 != null)
            {
                if (path1.Contains(node2)) return node2;
                node2 = node2.ImmediateDominator;
            }
            throw new Exception("No common dominator found!");
        }
        private static string nodeName(ControlFlowNode node)
        {
            String name = "node" + node.BlockIndex;

            if (node.NodeType == ControlFlowNodeType.EndFinally) name += "_ef";
            return name;
        }
        private static Regex SAFE_PATTERN = new Regex("^[\\w\\d]+$", RegexOptions.Compiled);
        private static string escapeGraphViz(string text, bool quote = false)
        {
            var matches = SAFE_PATTERN.Matches(text);
            if (SAFE_PATTERN.IsMatch(text)) return quote ? "\"" + text + "\"" : text;
            else {
                return (quote ? "\"" : "") +
                       text.Replace("\\", "\\\\")
                           .Replace("\r", "")
                           .Replace("\n", "\\l")
                           .Replace("|", "\\|")
                           .Replace("{", "\\{")
                           .Replace("}", "\\}")
                           .Replace("<", "\\<")
                           .Replace(">", "\\>")
                           .Replace("\"", "\\\"") + (quote ? "\"" : "");
            }
        }

        public int WriteTextLine(TextWriter wr)
        {
            System.CodeDom.Compiler.IndentedTextWriter output = new System.CodeDom.Compiler.IndentedTextWriter(wr);
            output.WriteLine("digraph g {");
            output.Indent++;
            ISet<ControlFlowEdge> edges = new LinkedHashSet<ControlFlowEdge>();
            foreach (var node in _nodes)
            {
                output.WriteLine("\"{0}\"", nodeName(node));
                output.Indent++;
                output.WriteLine("label = \"{0}\\l\"", escapeGraphViz(node.ToString()));
                output.WriteLine(", shape = \"box\"");
                output.Indent--;
                output.WriteLine("];");
                foreach (var e in node.Incomming) edges.Add(e);
                foreach (var e in node.Outgoing) edges.Add(e);
                ControlFlowNode endFinallyNode = node.EndFinallyNode;
                if (endFinallyNode != null)
                {
                    output.WriteLine("\"{0}\" [", nodeName(endFinallyNode));
                    output.Indent++;

                    output.WriteLine(
                        "label = \"{0}\"",
                        escapeGraphViz(endFinallyNode.ToString())
                    );

                    output.WriteLine("shape = \"box\"");

                    output.Indent--;
                    output.WriteLine("];");
                    foreach (var e in endFinallyNode.Incomming) edges.Add(e);
                    foreach (var e in endFinallyNode.Outgoing) edges.Add(e);
                    //                edges.add(new ControlFlowEdge(node, endFinallyNode, JumpType.EndFinally));
                }
                foreach (var edge in edges)
                {
                    ControlFlowNode from = edge.Source;
                    ControlFlowNode to = edge.Target;

                    output.WriteLine("\"%s\" -> \"%s\" [", nodeName(from), nodeName(to));
                    output.Indent++;

                    switch (edge.Type)
                    {
                        case JumpType.Normal:
                            break;

                        case JumpType.LeaveTry:
                            output.WriteLine("color = \"blue\"");
                            break;

                        case JumpType.EndFinally:
                            output.WriteLine("color = \"red\"");
                            break;

                        case JumpType.JumpToExceptionHandler:
                            output.WriteLine("color = \"gray\"");
                            break;

                        default:
                            output.WriteLine("label = \"{0}\"", edge.Type);
                            break;
                    }

                    output.Indent--;
                    output.WriteLine("];");
                }

                
               
            }
            output.Indent--;

            output.WriteLine("}");
            return 40;
        }
    }
}