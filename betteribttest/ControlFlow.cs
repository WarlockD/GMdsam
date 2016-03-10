﻿#define USE_BADDOM

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
  
    public class ControlFlowEdge :IEquatable<ControlFlowEdge>
    {
        public ControlFlowNode Source { get; private set; }
        public ControlFlowNode Target { get; private set; }
        public ControlFlowEdge(ControlFlowNode source, ControlFlowNode target)
        {
            Debug.Assert(source != null && target != null);
            Source = source;
            Target = target;
        }
        public  bool Equals(ControlFlowEdge edge)
        {
            return Source == edge.Source && Target == edge.Target;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            ControlFlowEdge edge = obj as ControlFlowEdge;
            return edge != null && Equals(edge);
        }
        public override string ToString()
        {
            return "#" + Target.BlockIndex;
        }
        public override int GetHashCode()
        {
            return Source.GetHashCode() ^ Target.GetHashCode();
        }
    }
    public class ControlFlowNode : IComparable<ControlFlowNode>, IEquatable<ControlFlowNode> , ITextOut
    {
        public BitArray BadDominatorArray;
        public override int GetHashCode()
        {
            return BlockIndex << 16 | Address;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            ControlFlowNode node = obj as ControlFlowNode;
            return node != null && Equals(node);
        }
        public bool Equals(ControlFlowNode node)
        {
            return BlockIndex == node.BlockIndex && Address == node.Address;
        }
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
        public int Address { get; private set; }
        public ControlFlowNode CopyFrom { get; set; }
        public bool Visited { get; set; }
        public bool isReachable { get { return ImmediateDominator != null || Address == 0; } }
        public Object UserData { get; set; }
        public ControlFlowNode(int blockIndex, int offset)
        {
            BlockIndex = blockIndex;
            Address = offset;
            Start = null;
            End = null;
        }
        public ControlFlowNode(int blockIndex, Instruction start, Instruction end)
        {
            BlockIndex = blockIndex;
            Debug.Assert(start != null && end != null);
            Start = start;
            End = end;
            Address = start.Address;
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
        public IEnumerable<ControlFlowNode> Predecessors { get { foreach (var i in _incoming) yield return i.Target; }  }
        public IEnumerable<ControlFlowNode> Successors { get { foreach (var i in _outgoing) yield return i.Target; } }
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
#if USE_BADDOM
            return BadDominatorArray[node.BlockIndex];
#else
            while (current != null)
            {
                if (current == this) return true;
                current = current.ImmediateDominator;
            }
            return false;
#endif
        }

        public int CompareTo(ControlFlowNode other)
        {
            return BlockIndex.CompareTo(other.BlockIndex);
        }
        public static Predicate<ControlFlowNode> REACHABLE_PREDICATE = node => node.isReachable;

        public int WriteTextLine(TextWriter wr)
        {
            int count = 0;
            System.CodeDom.Compiler.IndentedTextWriter sw = wr as System.CodeDom.Compiler.IndentedTextWriter;
            if(sw == null) sw = new System.CodeDom.Compiler.IndentedTextWriter(wr);

                    sw.Write("Block #{0}", BlockIndex);
                    if (Start != null) sw.Write(": {0} to {1}", Start.Address, End.Address);

            sw.Indent++;
            if (_dominanceFrontier.Count > 0)
            {
                sw.WriteLine(); count++;
                sw.Write("DominanceFrontier: ");
                int[] blockIndexes = new int[_dominanceFrontier.Count];
                int i = 0;
                foreach (var node in _dominanceFrontier) blockIndexes[i++] = node.BlockIndex;
                Array.Sort(blockIndexes);
                sw.Write(string.Join(",", blockIndexes));
            }
            foreach (var instruction in Instructions)
            {
                sw.WriteLine(); count++;
                sw.Write(instruction.ToString());
            }
            sw.Indent--;
            return count;
        }
        public override string ToString()
        {
            StringWriter ret = new StringWriter();
            WriteTextLine(ret);
            return ret.ToString();
        }
    };

    public class ControlFlowGraph : ITextOut
    {
        List<ControlFlowNode> _nodes;
        public ControlFlowNode EntryPoint { get { return _nodes[0]; } }
        public ControlFlowNode RegularExit { get { return _nodes.Last(); } }
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
        }
        public void ResetVisited() { foreach (var node in _nodes) node.Visited = false; }
        // I canno't get the old domintor to work.  I think its because I am missing bits of code, but I KNOW this works
        // even if it is a bit ineffecent
        void ComputeDominators2()
        {
            int size = _nodes.Count;
            foreach (var node in _nodes)
            {
                if (node.BadDominatorArray == null) node.BadDominatorArray = new BitArray(size);
                else node.BadDominatorArray.Length = size;
                node.BadDominatorArray.SetAll(true);

            }
            ControlFlowNode entryPoint = EntryPoint;

            entryPoint.BadDominatorArray.SetAll(false);
            entryPoint.BadDominatorArray.Set(entryPoint.BlockIndex, true);
            BitArray T = new BitArray(size);
            bool changed = false;
            do
            {
                changed = false;
                foreach (var node in _nodes)
                {
                    if (node == EntryPoint) continue;
                    foreach (var pred in node.Predecessors)
                    {
                        T.SetAll(false);
                        T.Or(node.BadDominatorArray);
                        node.BadDominatorArray.And(pred.BadDominatorArray);
                        node.BadDominatorArray.Set(node.BlockIndex, true);
                        if (!Enumerable.SequenceEqual(node.BadDominatorArray.Cast<bool>(), T.Cast<bool>())) changed = true;
                    }
                }
            } while (changed);

            entryPoint.ImmediateDominator = null;
            foreach (var node in _nodes)
            {
                for(int i =0;i< node.BadDominatorArray.Count; i++)
                {
                    bool b = node.BadDominatorArray[i];
                    if (b) node.DominatorTreeChildren.Add(_nodes[i]);
                }
            }
        }
        public void ComputeDomiance()
        {
            bool cancelled = false;
            ComputeDomiance(ref cancelled);
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
            foreach (var node in _nodes)
            {
                output.WriteLine("\"{0}\"", nodeName(node));
                output.Indent++;
                output.WriteLine("label=\"{");
                if (node.WriteTextLine(output) > 0) output.WriteLine();
                //  output.WriteLine("label = \"{0}\\l\"", escapeGraphViz(node.ToString()));
                output.WriteLine(", shape = \"box\"");
                output.Indent--;
                output.WriteLine("];");
                if (node.Incomming.Count > 0)
                {
                    output.WriteLine("Incomming=");
                    output.Indent++;
                    foreach (var edge in node.Incomming)
                    {
                        ControlFlowNode from = edge.Source;
                        ControlFlowNode to = edge.Target;
                        // we just have normal edges
                        output.WriteLine("\"{0}\" -> \"{1}\" []", nodeName(from), nodeName(to));
                    }
                    output.Indent--;
                }
                if (node.Outgoing.Count > 0)
                {
                    output.WriteLine("Outgoing=");
                    output.Indent++;
                    foreach (var edge in node.Outgoing)
                    {
                        ControlFlowNode from = edge.Source;
                        ControlFlowNode to = edge.Target;
                        // we just have normal edges
                        output.WriteLine("\"{0}\" -> \"{1}\" []", nodeName(from), nodeName(to));
                    }
                    output.Indent--;
                }
            }
            output.Indent--;

            output.WriteLine("}");
            return 40;
        }
    }
}