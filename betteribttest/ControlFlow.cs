#define USE_BADDOM

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
  
    public class ControlFlowEdgeOld :IEquatable<ControlFlowEdgeOld>
    {
        public ControlFlowNodeOld Source { get;  set; }
        public ControlFlowNodeOld Target { get;  set; }
        public ControlFlowEdgeOld(ControlFlowNodeOld source, ControlFlowNodeOld target)
        {
            Debug.Assert(source != null && target != null);
            Source = source;
            Target = target;
        }
        public  bool Equals(ControlFlowEdgeOld edge)
        {
            return Source == edge.Source && Target == edge.Target;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            ControlFlowEdgeOld edge = obj as ControlFlowEdgeOld;
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
    public class ControlFlowNodeOld : IComparable<ControlFlowNodeOld>, IEquatable<ControlFlowNodeOld> , ITextOut
    {
        public BitArray DebugDominators;
        public StatementBlock block;
        public override int GetHashCode()
        {
            return BlockIndex << 16 | Address;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            ControlFlowNodeOld node = obj as ControlFlowNodeOld;
            return node != null && Equals(node);
        }
        public bool Equals(ControlFlowNodeOld node)
        {
            return BlockIndex == node.BlockIndex && Address == node.Address;
        }
        public List<ControlFlowNodeOld> DominatorTreeChildren = new List<ControlFlowNodeOld>();
        private LinkedHashSet<ControlFlowNodeOld> _dominanceFrontier = new LinkedHashSet<ControlFlowNodeOld>();
        public LinkedHashSet<ControlFlowNodeOld> DomianceFrontier {  get { return _dominanceFrontier; } }
        private List<ControlFlowEdgeOld> _incoming = new List<ControlFlowEdgeOld>();
        private List<ControlFlowEdgeOld> _outgoing = new List<ControlFlowEdgeOld>();
        public List<ControlFlowEdgeOld> Incomming { get { return _incoming; } set { _incoming = value; } }
        public List<ControlFlowEdgeOld> Outgoing { get { return _outgoing; } set { _outgoing = value; } }
        public ControlFlowNodeOld ImmediateDominator { get; set; }
        public Instruction Start { get; set; }
        public Instruction End { get; set; }
        public int BlockIndex { get; internal set; }
        public int Address { get; private set; }
        public ControlFlowNodeOld CopyFrom { get; set; }
        public bool Visited { get; set; }
        public bool isReachable { get { return ImmediateDominator != null || Address == 0; } }
        public Object UserData { get; set; }
        public ControlFlowNodeOld(int blockIndex, int offset)
        {
            BlockIndex = blockIndex;
            Address = offset;
            Start = null;
            End = null;
        }
        public ControlFlowNodeOld(int blockIndex, Instruction start, Instruction end)
        {
            BlockIndex = blockIndex;
            Debug.Assert(start != null && end != null);
            Start = start;
            End = end;
            Address = start.Address;
        }
        public bool Succeds(ControlFlowNodeOld other)
        {
            if (other == null) return false;
            foreach (var i in _incoming) if (i.Source == other) return true;
            return false;
        }
        public bool Precedes(ControlFlowNodeOld other)
        {
            if (other == null) return false;
            foreach (var i in _outgoing) if (i.Source == other) return true;
            return false;
        }
        public IEnumerable<ControlFlowNodeOld> Predecessors { get { foreach (var i in _incoming) yield return i.Source; }  }
        public IEnumerable<ControlFlowNodeOld> Successors { get { foreach (var i in _outgoing) yield return i.Target; } }
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
        public void TraversePreOrder(Func<ControlFlowNodeOld, IEnumerable<ControlFlowNodeOld>> children, Action<ControlFlowNodeOld> visitAction)
        {
            if (Visited) return;
            Visited = true;
            visitAction(this);
            foreach (var child in children(this)) child.TraversePreOrder(children, visitAction);
        }
        public void TraversePostOrder(Func<ControlFlowNodeOld, IEnumerable<ControlFlowNodeOld>> children, Action<ControlFlowNodeOld> visitAction)
        {
            if (Visited) return;
            Visited = true;
            foreach (var child in children(this)) child.TraversePostOrder(children, visitAction);
            visitAction(this);
        }
        public bool Dominates(ControlFlowNodeOld node)
        {
            return DebugDominators.Get(node.BlockIndex);
            ControlFlowNodeOld current = node;
            while (current != null)
            {
                if (current == this)
                {
                    Debug.Assert(DebugDominators.Get(node.BlockIndex));
                    return true;
                }
                current = current.ImmediateDominator;
            }
            return false;
        }

        public int CompareTo(ControlFlowNodeOld other)
        {
            return BlockIndex.CompareTo(other.BlockIndex);
        }
        public static Predicate<ControlFlowNodeOld> REACHABLE_PREDICATE = node => node.isReachable;

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
#if false
            if(DebugDominators != null)
            {
                List<int> doms = new List<int>();
                for (int i = 0; i < DebugDominators.Count; i++) if (DebugDominators[i]) doms.Add(i);
                if(doms.Count > 0)
                {
                    sw.WriteLine(); count++;
                    sw.Write("Dominates: ");
                    sw.Write(string.Join(",", doms));
                }
            }
#endif
            if (block != null)
            {
                sw.WriteLine(); count++;
                count+= block.DecompileToText(sw);
            }
            else {
                foreach (var instruction in Instructions)
                {
                    sw.WriteLine(); count++;
                    sw.Write(instruction.ToString());
                }
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

    public class ControlFlowGraphOld : ITextOut
    {

        List<ControlFlowNodeOld> _nodes;
        public ControlFlowNodeOld EntryPoint { get { return _nodes[0]; } }
        public ControlFlowNodeOld RegularExit { get { return _nodes[1]; } }
        public List<ControlFlowNodeOld> Nodes { get { return _nodes; } }
        public ControlFlowGraphOld(params ControlFlowNodeOld[] nodes)
        {
            _nodes = new List<ControlFlowNodeOld>();
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

        public void ComputeDomiance()
        {
            bool cancelled = false;
            ComputeDomiance(ref cancelled);
        }
        public void DebugDominance()
        {
            ControlFlowNodeOld entryPoint = EntryPoint;
            int size = _nodes.Count;
            for (int i = 0; i < size; i++) _nodes[i].BlockIndex = i; // Re index
            foreach (var node in _nodes)
            {
                node.DebugDominators = new BitArray(size);
                node.DebugDominators.SetAll(true);
            }
            entryPoint.DebugDominators.SetAll(false);
            entryPoint.DebugDominators.Set(entryPoint.BlockIndex,true);
            bool changed = true;

            while (changed)
            {
                changed = false;
                ResetVisited();
                BitArray T = new BitArray(size);
                foreach (var node in _nodes)
                {
                    if (node == entryPoint) continue;
                    foreach (var pred in node.Predecessors)
                    {
                        T.SetAll(false);
                        T.Or(node.DebugDominators);
                        node.DebugDominators.And(pred.DebugDominators);
                        node.DebugDominators.Set(node.BlockIndex,true);
                        if(!T.Cast<bool>().SequenceEqual(node.DebugDominators.Cast<bool>())) changed = true;
                    }
                }
            }
        }
    
 
        public void ComputeDomiance(ref bool cancelled)
        {
            DebugDominance();

            ControlFlowNodeOld entryPoint = EntryPoint;
            entryPoint.ImmediateDominator = entryPoint;
            bool changed = true;
            while (changed)
            {
                changed = false;
                ResetVisited();
               if (cancelled) throw new Exception("Cancelled");
                entryPoint.TraversePreOrder(input => input.Successors, delegate (ControlFlowNodeOld b) {
                    if (b == entryPoint) return;
                    ControlFlowNodeOld newImmediateDominator = null;
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

            foreach (ControlFlowNodeOld node in _nodes)
            {
                ControlFlowNodeOld immediateDominator = node.ImmediateDominator;

                if (immediateDominator != null) immediateDominator.DominatorTreeChildren.Add(node);
            }

        }
        void RemoveStatementsAndEndingGotos(StatementBlock block)
        {
            if (block.Last() is GotoStatement) block.Remove(block.Last());
            for (int i = 0; i < block.Count; i++) if (block[i] is LabelStatement) block.RemoveAt(i);
        }
        StatementBlock NodeToBlock(Decompile dn, ControlFlowNodeOld node, Stack<Ast> stack) { return new StatementBlock(dn.ConvertManyStatements(node.Start, node.End, stack)); }
        int NodeToAst(Decompile dn, ControlFlowNodeOld node, Stack<Ast> stack, bool removeStatementsAndGotos)
        {
            StatementBlock block = new StatementBlock();
            if (node.block == null && node.Address != -1)
            {
                if (node.Address == -1) block.Add(new ExitStatement(null)); // fake exit
                else block = NodeToBlock(dn,node, stack);
            }
            if (removeStatementsAndGotos) RemoveStatementsAndEndingGotos(block);
            node.block = block;
            return block.Count;
        }
        public void BuildAllAst(Decompile dn, Dictionary<ControlFlowNodeOld, Stack<Ast>> stackMap)
        {
            foreach(var node in _nodes)
            {
                if (node == EntryPoint || node == RegularExit) continue;
                if (node == null && node.Address != -1) continue;
                Stack<Ast> stack; // = new Stack<Ast>();
                if(!stackMap.TryGetValue(node,out stack)) {
                    stack = new Stack<Ast>();
                    stackMap.Add(node, stack);
                    
                }
                node.block = NodeToBlock(dn, node, stack);
                foreach (var succ in node.Successors) stackMap[succ]= new Stack<Ast>(stack);
                // if (stack.Count > 0) throw new Exception("Node stack error");
            }
        }
        public void computeDominanceFrontier()
        {
        /*
        for all nodes, b
            if the number of predecessors of b ≥ 2
                for all predecessors, p, of b
                runner ← p
                while runner 6 = doms[b]
                    add b to runner’s dominance frontier set
                    runner = doms[runner]
*/

            ResetVisited();
            EntryPoint.TraversePostOrder(o => o.DominatorTreeChildren, delegate (ControlFlowNodeOld n)
             {
                 ISet<ControlFlowNodeOld> dominanceFrontier = n.DomianceFrontier;

                 dominanceFrontier.Clear();
                 foreach (var s in n.Successors) if (s.ImmediateDominator != n) dominanceFrontier.Add(s);
                 foreach (var child in n.DominatorTreeChildren)
                 {
                     foreach (var p in child.DomianceFrontier) if (p.ImmediateDominator != n) dominanceFrontier.Add(p);
                 }
             }
             );
        }

        public void ExportGraph(string name)
        {
            StringWriter fwriter = new StringWriter();
            System.CodeDom.Compiler.IndentedTextWriter output = new System.CodeDom.Compiler.IndentedTextWriter(fwriter);

            output.WriteLine("digraph g {");
            output.Indent++;


            LinkedHashSet<ControlFlowEdgeOld> edges = new LinkedHashSet<ControlFlowEdgeOld>();
            foreach (ControlFlowNodeOld node in _nodes)
            {
                output.WriteLine("\"{0}\" [", nodeName(node));
                output.Indent++;

                output.WriteLine(
                    "label = \"{0}\\l\"",
                    node.block !=null ? escapeGraphViz(node.ToString()) : escapeGraphViz(node.ToString())

                );

                output.WriteLine(", shape = \"box\"");

                output.Indent--;
                output.WriteLine("];");
                edges.UnionWith(node.Incomming);
                edges.UnionWith(node.Outgoing);
             

            }
        //    output.Indent;

            foreach (ControlFlowEdgeOld edge in edges)
            {
                ControlFlowNodeOld from = edge.Source;
                ControlFlowNodeOld to = edge.Target;

                output.WriteLine("\"{0}\" -> \"{1}\" []", nodeName(from), nodeName(to));
                //      output.Indent++;

                //  output.unindent();
              //  output.WriteLine("];");
            }

            output.Indent--;
            output.WriteLine("}");
            output.Flush();
            
            using (StreamWriter file = new StreamWriter(name))
            {
                file.Write(fwriter.ToString());
            }
                
        }

        public static ControlFlowNodeOld findCommonDominator(ControlFlowNodeOld a, ControlFlowNodeOld b)
        {
            ISet<ControlFlowNodeOld> path1 = new LinkedHashSet<ControlFlowNodeOld>();

            ControlFlowNodeOld node1 = a;
            ControlFlowNodeOld node2 = b;

            while (node1 != null && path1.Add(node1)) node1 = node1.ImmediateDominator;

            while (node2 != null)
            {
                if (path1.Contains(node2)) return node2;
                node2 = node2.ImmediateDominator;
            }
            throw new Exception("No common dominator found!");
        }
        private string nodeName(ControlFlowNodeOld node)
        {
            if (node == EntryPoint) return "init";
            else if (node == RegularExit) return "exit";
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
            HashSet<ControlFlowNodeOld> children = new HashSet<ControlFlowNodeOld>();
            foreach (var node in _nodes)
            {
                output.WriteLine("\"{0}\"", nodeName(node));
                output.Indent++;
                output.WriteLine("label=\"");
                //if (node.WriteTextLine(output) > 0) output.WriteLine();
                output.WriteLine("label = \"{0}\\l\"", escapeGraphViz(node.ToString()));
                output.WriteLine(", shape = \"box\"");
                output.Indent--;
                output.WriteLine("];");
                if (node.Incomming.Count > 0)
                {
                    output.WriteLine("Incomming=");
                    output.Indent++;
                    foreach (var edge in node.Incomming)
                    {
                        ControlFlowNodeOld from = edge.Source;
                        ControlFlowNodeOld to = edge.Target;
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
                        ControlFlowNodeOld from = edge.Source;
                        ControlFlowNodeOld to = edge.Target;
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
        public ControlFlowNodeOld FindNode(Instruction instruction)
        {
            int address = instruction.Address;
            foreach(var node in _nodes)
            {
                if (address >= node.Start.Address && address < node.End.Address) return node;
            }
            return null;
        }
        public ISet<ControlFlowNodeOld> findDominatedNodes(ControlFlowNodeOld head, ISet<ControlFlowNodeOld> terminals)
        {
            LinkedHashSet<ControlFlowNodeOld> visited = new LinkedHashSet<ControlFlowNodeOld>();
            Queue<ControlFlowNodeOld> agenda = new Queue<ControlFlowNodeOld>();
            LinkedHashSet<ControlFlowNodeOld> result = new LinkedHashSet<ControlFlowNodeOld>();
            agenda.Enqueue(head);
            visited.Add(head);
            while (agenda.Count >0)
            {
                ControlFlowNodeOld addNode = agenda.Dequeue();

                if (terminals.Contains(addNode)) continue;

                // worry about entry point?   if (addNode == null || addNode.getNodeType() != ControlFlowNodeType.Normal) continue;


                if (!head.Dominates(addNode)) continue; // && !shouldIncludeExceptionalExit(cfg, head, addNode)) continue;

                if (!result.Add(addNode)) continue;


                foreach (ControlFlowNodeOld successor in addNode.Successors)
                {
                    if (visited.Add(successor)) agenda.Enqueue(successor);
                }
            }
            return result;
        }
        public Dictionary<Instruction, ControlFlowNodeOld> CreateNodeMap()
        {
            Dictionary<Instruction, ControlFlowNodeOld> map = new Dictionary<Instruction, ControlFlowNodeOld>();
            foreach(var node in _nodes)
            {
                for (Instruction p = node.Start; p != null && p.Address < node.End.Address; p = p.Next) map.Add(p, node);
            }
            return map;
        }
    }
}