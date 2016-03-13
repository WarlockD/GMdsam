using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Specialized;

namespace betteribttest
{
    public class NullNode : Node
    {
        public override void WriteTo(TextWriter r) { r.Write("nullNode"); }
    }
    public abstract class Expression
    {
        public abstract Instruction Code { get; }
    }

    public abstract class Node
    {
        public override string ToString()
        {
            StringWriter sr = new StringWriter();
            WriteTo(sr);
            return sr.ToString();
        }

        public bool isUnconditionalControlFlow()
        {
            ///return this instanceof Expression &&
            //     ((Expression)this).getCode().isUnconditionalControlFlow();
            return false;
        }
        public abstract void WriteTo(TextWriter r);
        void accumulateSelfAndChildrenRecursive<T>(List<T> list, Predicate<T> predicate, bool childrenFirst) where T : Node
        {
            T test = this as T;
            if (!childrenFirst) if (test != null && (predicate == null || predicate(test))) list.Add(test);
            foreach (var child in getChildren()) child.accumulateSelfAndChildrenRecursive<T>(list, predicate, childrenFirst);
            if (childrenFirst) if (test != null && (predicate == null || predicate(test))) list.Add(test);
        }

        public virtual List<Node> getChildren() { return new List<Node>(); } // empty list
        public List<Node> getSelfAndChildrenRecursive()
        {
            List<Node> results = new List<Node>();
            accumulateSelfAndChildrenRecursive(results, null, false);
            return results;
        }

        public List<Node> getSelfAndChildrenRecursive(Predicate<Node> predicate)
        {
            List<Node> results = new List<Node>();
            accumulateSelfAndChildrenRecursive(results, predicate, false);
            return results;
        }

        public List<T> getSelfAndChildrenRecursive<T>() where T : Node
        {
            List<T> results = new List<T>();
            accumulateSelfAndChildrenRecursive(results, null, false);
            return results;
        }
        public List<T> getSelfAndChildrenRecursive<T>(Predicate<T> predicate) where T : Node
        {
            List<T> results = new List<T>();
            accumulateSelfAndChildrenRecursive(results, null, false);
            return results;
        }
        public List<Node> getChildrenAndSelfRecursive()
        {
            List<Node> results = new List<Node>();
            accumulateSelfAndChildrenRecursive(results, null, true);
            return results;
        }

        public List<Node> getChildrenAndSelfRecursive(Predicate<Node> predicate)
        {
            List<Node> results = new List<Node>();
            accumulateSelfAndChildrenRecursive(results, predicate, true);
            return results;
        }

        public List<T> getChildrenAndSelfRecursive<T>() where T : Node
        {
            List<T> results = new List<T>();
            accumulateSelfAndChildrenRecursive(results, null, true);
            return results;
        }
        public List<T> getChildrenAndSelfRecursive<T>(Predicate<T> predicate) where T : Node
        {
            List<T> results = new List<T>();
            accumulateSelfAndChildrenRecursive(results, null, true);
            return results;
        }
    }
    enum BlockStatementType
    {
        Goto,
        DoWhile,
        Continue,
        Break,
        If
    }
    class BlockStatement
    {
        public Block Destination;
        public BlockStatementType Type;
        public List<Block> elseBlocks;
        public List<Block> trueBlocks;
        public List<Block> falseBlocks;
        public bool NegateStatement;
        public bool RemoveLastGotoInTrueBlock;
        public bool RemoveLastGotoInFalseBlock;
        public BlockStatement(BlockStatementType t)
        {
            NegateStatement = false;
            RemoveLastGotoInTrueBlock = false;
            RemoveLastGotoInFalseBlock = false;
            this.Type = t;
            elseBlocks = new List<Block>();
            trueBlocks = new List<Block>();
            falseBlocks = new List<Block>();
            Destination = null;
        }
    }
    /// <summary>
    /// Created this block to make finding equality between code blocks easyer when they have diffrent target 
    /// Address
    /// </summary>
    public class CodeBlock : IEquatable<List<Instruction>>, IEquatable<CodeBlock>, IReadOnlyList<Instruction>, ITextOut
    {
        public static int MakeHash(int start,int end)
        {
            unchecked
            {
                return (start & 0xFFFF) | (end << 16); // this works
                //  _hash = 0x2D2816FE;
                //  foreach (var i in _list) _hash = _hash * 31 + i.GetHashCode();
            }
        }
        List<Instruction> _list;
        int _hash;
        public int AddressStart { get; private set; }
        public int AddressEnd { get; private set; }
        public CodeBlock(int start, int end, List<Instruction> list) : base()
        {
            _list = list;
            _list.Sort(); // we want to be %100 sure that they line up
            AddressStart = start;
            AddressEnd = end;
            _hash = MakeHash(start, end);
        }
        public CodeBlock(int start, int end, IEnumerable<Instruction> list) 
        {
            _list = new List<Instruction>(list);
            _list.Sort(); // we want to be %100 sure that they line up
            AddressStart = start;
            AddressEnd = end;
            _hash = MakeHash(start, end);
        }
        public CodeBlock(IEnumerable<Instruction> list)
        {
            _list = new List<Instruction>(list);
            _list.Sort(); // we want to be %100 sure that they line up
            AddressStart = _list.First().Address;
            AddressEnd = _list.Last().Address;
            _hash = MakeHash(AddressStart, AddressEnd);
        }
        public bool Equals(CodeBlock other)
        {
            if (object.ReferenceEquals(other, null)) return false;
            if (object.ReferenceEquals(other, this)) return true;
            return other._hash == this._hash && other.AddressStart == this.AddressStart && other.AddressEnd == this.AddressEnd;
        }
        public bool Equals(List<Instruction> other)
        {
            if (object.ReferenceEquals(other, null)) return false;
            if (object.ReferenceEquals(other, this)) return true;
            return other.SequenceEqual(_list);
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            CodeBlock codeblockTest = obj as CodeBlock;
            if (codeblockTest != null) return Equals(codeblockTest);
            List<Instruction> listTest = obj as List<Instruction>;
            if (listTest != null) return Equals(listTest);
            return false;
        }
        public override int GetHashCode() { return _hash; } // hash codes are essencaly equality
        public int Count { get { return _list.Count; } }
        public Instruction this[int index] { get { return _list[index]; } }
        public IEnumerator<Instruction> GetEnumerator() { return _list.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return _list.GetEnumerator(); }
        public int WriteTextLine(TextWriter wr)
        {
            foreach (var i in _list)
            {
                i.WriteTextLine(wr);
                wr.WriteLine();
            }
            return _list.Count;
        }
        public override string ToString()
        {
            StringWriter sw =new StringWriter();
            this.WriteTextLine(sw);
            return sw.ToString();
        }
    }

    /// <summary>
    ///  I got this from http://www.backerstreet.com/decompiler/basic_blocks.php
    ///  We are going to see if I can get this work
    /// </summary>
    public class Block : IEquatable<Block>, IComparable<Block>
    {
        public bool ExitLabel = false;
        public Label LabelEntry { get; set; }
        public StatementBlock AstBlock { get; set; }
        public int Id { get; set; }
        public BitArray dominators { get;  set; }
        public int Address { get; private set; }
        public bool Visited = false;
        public LinkedHashSet<Block> preds { get; private set; }
        public LinkedHashSet<Block> succs { get; private set; }
        public void AddPre(Block pre)
        {
             preds.Add(pre);

        }
        public void AddSucc(Block succ)
        {
             succs.Add(succ);
        }
        public IEnumerable<Instruction> Code
        {
            get
            {
                var start = First;
                while(start != null)
                {
                    yield return start;
                    if (start == Last) break;
                    start = start.Next;
                }

            }
        }
        public Instruction First;
        public Instruction Last;
     //   public int Length { get { return Code == null ? 0 : Code.Count; } }
        public Block(int address)
        {
            Address = address;
            preds = new LinkedHashSet<Block>();
            succs = new LinkedHashSet<Block>();
            First = null;
            Last = null;
           // Code = null;
            Visited = false;
            Id = -1;
            dominators = null;
            AstBlock = null;
            LabelEntry = null;
        }
        public void ClearVisits()
        {
            if(Visited == true)
            {
                Visited = false;
                foreach (var p in preds) p.ClearVisits();
                foreach (var s in succs) s.ClearVisits();
            }
        }

        public void ForwardVisitBlocks(Action<Block> pre, Action<Block> post)
        {
            Visited = true;
            if (pre != null) pre(this);
            // code
            foreach (var succ in succs)
                if (!succ.Visited) ForwardVisitBlocks(pre, post);
            // post visit
            if (post != null) post(this);
        }
        public override int GetHashCode()
        {
            return Address ;
        }
        public bool Equals(Block b)
        {
            return Address == b.Address; 
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            Block b = obj as Block;
            return b != null && Equals(b);
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Address=");
            sb.Append(Address);
            if(preds != null && preds.Count > 0)
            {
                sb.Append(" Preds=");
                foreach (var p in preds) {
                    sb.Append(p.Address);
                    sb.Append(' ');
                } 
            }
            if (succs != null && succs.Count > 0)
            {
                sb.Append(" Succss=");
                foreach (var p in succs)
                {
                    sb.Append(p.Address);
                    sb.Append(' ');
                }
            }
            return sb.ToString();
        }
        public void WriteIT(TextWriter tw)
        {
            tw.Write("----- ");
            tw.WriteLine(ToString());
            if (First != null)
            {
                for (Instruction i = First; i != Last.Next; i = i.Next)
                {
                    
                    i.WriteTextLine(tw);
                    tw.WriteLine();
                }
            }
        }

        public int CompareTo(Block other)
        {
            return Address.CompareTo(other.Address);
        }
    }

    class BasicBlocks
    {
        public List<Block> BlockList = new List<Block>();
        public Dictionary<int,CodeBlock> CodeBlocks = new Dictionary<int, CodeBlock>();
        public Instruction First;
        public Instruction Last;
        public static void DebugWrite(IEnumerable<Block> blocks,  string filename)
        {
            StreamWriter tw = new StreamWriter(filename);
            foreach (var b in blocks) b.WriteIT(tw);
            tw.Close();
        }
        public Block EntryBlock { get; private set; }
        int _last_pc;
        SortedList<int, Instruction> code;

        void ComputeDominators()
        {
            int id = 0;
            int size = BlockList.Count;
            Block block;
            foreach(var kv in BlockList)
            {
                block = kv;
                block.Id = id++;
                if (block.dominators == null) block.dominators = new BitArray(size);
                else block.dominators.Length = size;
                block.dominators.SetAll(true);
            }
            
            block = BlockList[0]; // aways the first entry point anyway
            block.dominators.SetAll(false);
            block.dominators.Set(block.Id,true);
            
            BitArray T = new BitArray(size);
            bool changed = false;
            do
            {
                changed = false;
                foreach (var kv in BlockList)
                {
                    block = kv;
                    if (block == EntryBlock) continue;
                    foreach (var pred in block.preds)
                    {
                        T.SetAll(false);
                        T.Or(block.dominators);
                        block.dominators.And(pred.dominators);
                        block.dominators.Set(block.Id, true);
                        if (!Enumerable.SequenceEqual(block.dominators.Cast<bool>(), T.Cast<bool>())) changed = true;
                    }
                }
            } while (changed);
        }
        class Loop : IEquatable<Loop>
        {
            public Block Header { get; private set; }
            public List<Block> Blocks { get; private set; }
            public Loop(Block header)
            {
                Header = header;
                Blocks = new List<Block>();
                Blocks.Add(header);
            }
            public override int GetHashCode()
            {
                int hash = Header.GetHashCode();
                foreach (var i in Blocks) hash = hash * 31 + i.GetHashCode();
                return hash;
            }
            public bool Equals(Loop other)
            {
                if (other.Header != this.Header) return false;
                if (Blocks.SequenceEqual(other.Blocks)) return true;
                return false;
            }
            public override bool Equals(object obj)
            {
                if (object.ReferenceEquals(obj, null)) return false;
                if (object.ReferenceEquals(obj, this)) return true;
                Loop test = obj as Loop;
                return test == null ? Equals(test) : false;
            }
        }
        HashSet<Loop> loopList; // should be a set?
        Loop NatrualLoopForEdge(Block header, Block tail)
        {
            Stack<Block> workList = new Stack<Block>();
            Loop loop = new Loop(header);
            if(header != tail)
            {
                loop.Blocks.Add(tail);
                workList.Push(tail);
            }
            while(workList.Count > 0)
            {
                Block block = workList.Pop();
                foreach(var pred in block.preds)
                {
                    if(!loop.Blocks.Contains(pred))
                    {
                        loop.Blocks.Add(pred);
                        workList.Push(pred);
                    }
                }
            }
            return loop;
        }
        void ComputeNatrualLoops()
        {
            loopList = new HashSet<Loop>();
            foreach(var kv in BlockList)
            {
                Block block = kv;
                if (block == EntryBlock) continue;
                foreach(var succ in block.succs)
                {
                    // Every successor that dominates its predecessor
                    // must be the header of a loop.
                    // That is, block -> succ is a back edge.
                    // if(block.ContainsDominator(succ))
                    bool test = block.dominators.Get(succ.Id);
                    if (test)
                        loopList.Add(NatrualLoopForEdge(succ, block));
                }
            }
        }
        class Loop2 : IEquatable<Loop2>
        {
            public ControlFlowNode Header { get; private set; }
            public List<ControlFlowNode> Blocks { get; private set; }
            public Loop2(ControlFlowNode header)
            {
                Header = header;
                Blocks = new List<ControlFlowNode>();
                Blocks.Add(header);
            }
            public override int GetHashCode()
            {
                int hash = Header.GetHashCode();
               // foreach (var i in Blocks) hash = hash * 31 + i.GetHashCode();
                return hash;
            }
            public bool Equals(Loop2 other)
            {
                if (other.Header == this.Header) return true;
              //  if (Blocks.SequenceEqual(other.Blocks)) return true;
                return false;
            }
            public override bool Equals(object obj)
            {
                if (object.ReferenceEquals(obj, null)) return false;
                if (object.ReferenceEquals(obj, this)) return true;
                Loop test = obj as Loop;
                return test != null ? Equals(test) : false;
            }
        }
        HashSet<Loop2> LoopList2 = new HashSet<Loop2>();
        Loop2 NatrualLoopForEdge2(ControlFlowNode header, ControlFlowNode tail)
        {
         
            Loop2 loop = null;
            foreach(var t in LoopList2) if(t.Header == header) { loop = t; break; }
            if(loop == null)
            {
                loop = new Loop2(header);
                LoopList2.Add(loop);
            }
            Stack<ControlFlowNode> workList = new Stack<ControlFlowNode>();
            if (header != tail)
            {
                loop.Blocks.Add(tail);
                workList.Push(tail);
            }
            while (workList.Count > 0)
            {
                ControlFlowNode node = workList.Pop();
                foreach (var pred in node.Predecessors)
                {
                    if (!loop.Blocks.Contains(pred))
                    {
                        loop.Blocks.Add(pred);
                        workList.Push(pred);
                    }
                }
            }
            return loop;
        }
        void ComputeNatrualLoops2()
        {
            LoopList2 = new HashSet<Loop2>();
            ControlFlowNode entryPoint = graph.EntryPoint;
            foreach(var node in graph.Nodes)
            {
                if (node == entryPoint) continue;
                foreach (var succ in node.Successors)
                {
                    // Every successor that dominates its predecessor
                    // must be the header of a loop.
                    // That is, block -> succ is a back edge.
                    // if(block.ContainsDominator(succ))
                    if(node.Dominates(succ))
                        LoopList2.Add(NatrualLoopForEdge2(succ, node));
                }
            }
        }
        void StructureBreakContinue(BlockStatement stmt, Block contBlock, Block breakBlock)
        {
            switch (stmt.Type)
            {
                case BlockStatementType.Goto:
                    if (stmt.Destination == contBlock) stmt.Type = BlockStatementType.Continue;
                    else if (stmt.Destination == breakBlock) stmt.Type = BlockStatementType.Break;
                    break;
                case BlockStatementType.If:
            //        if (stmt.elseBlocks != null) stmt.elseBlocks.ForEach(s => StructureBreakContinue(s, contBlock, breakBlock));
           //         if (stmt.trueBlocks != null) stmt.trueBlocks.ForEach(s => StructureBreakContinue(s, contBlock, breakBlock));
                    break;

            }
        }
        void WorkOnLoopSet()
        {
            // http://www.backerstreet.com/decompiler/creating_statements.php
            foreach (var loop in loopList)
            {
                var doWhile = new BlockStatement(BlockStatementType.DoWhile);

            }
        }
        IfStatement StructureIfElse(Block block, Stack<Ast> stack)
        {
            if (block.succs.Count != 2) return null;
            var trueBlock = block.succs[0];
            var falseBlock = block.succs[1];
            if (trueBlock.succs.Count != 1 || falseBlock.succs.Count != 1) return null;
            if (falseBlock.succs[0] != trueBlock.succs[0]) return null;

            Ast condition = stack.Pop();
            var trueAst = TryToMakeStatements(trueBlock, new Stack<Ast>(stack));
            var falseAst = TryToMakeStatements(trueBlock, new Stack<Ast>(stack));
            Instruction i = block.Code.Last();
            Debug.Assert(i.isBranch);
            return new IfStatement(i, i.GMCode == GMCode.Bf ? condition.Invert() : condition, trueAst, falseAst);
        }
        IfStatement StructureIfs(Block block, Stack<Ast> stack)
        {
            if (block.succs.Count != 2) return null;
            var trueBlock = block.succs[0];
            var falseBlock = block.succs[1];
            if (trueBlock.succs.Count == 1 && trueBlock.succs[0] == falseBlock)
            {
                Ast condition = stack.Pop();
                var trueAst = TryToMakeStatements(trueBlock, new Stack<Ast>(stack));
                Instruction i = block.Code.Last();
                Debug.Assert(i.isBranch);
                return new IfStatement(i, i.GMCode == GMCode.Bf ? condition.Invert() : condition, trueAst);
            }
            return null;
        }
        AstStatement root;
        DecompilerNew dn;
        StatementBlock TryToMakeStatements(Block block, Stack<Ast> stack)
        {
            if (block.AstBlock != null) return block.AstBlock;
          //  StatementBlock astBlock = new StatementBlock();
            block.AstBlock = dn.DoStatements(stack, block.Code.First(), block.Code.Last());
            if (block.succs.Count == 2)
            {
                IfStatement oldIf = block.AstBlock.Last() as IfStatement;
                StatementBlock then = TryToMakeStatements(block.succs[0], new Stack<Ast>(stack));
                block.AstBlock.Remove(oldIf);
                IfStatement ifs = new IfStatement(oldIf.Instruction, oldIf.Condition, then, new GotoStatement(new Label(block.succs[1].Address)));
                block.AstBlock.Add(ifs);
            } else if(block.succs.Count == 1) { 
                // the goto should already be in there

            }
#if DEBUG
            block.AstBlock.SaveToFile("temp_statement.txt");
#endif
            return block.AstBlock;
        }
        int[] _offsets;
        bool[] _hasIncomingJumps;
        ControlFlowGraph graph;
        public BasicBlocks(IEnumerable<Instruction> list, DecompilerNew dn)
        {
            this.dn = dn;
            code = new SortedList<int, Instruction>(200);
            List<Instruction> ilist = list.ToList();
            graph = ControlFlowGraphBuilder.Build(ilist);
            graph.ComputeDomiance();
            graph.computeDominanceFrontier();
            graph.ExportGraph("Testdot.txt");
            ComputeNatrualLoops2();
            
            foreach (var l in list) code.Add(l.Address, l);
            First = code.Values.First().First;
            Last = code.Values.First().Last;
            Instruction lasti = code.Values.Last();
            _last_pc = lasti.Address;
            EntryBlock = null;
            CodeBlocks = new Dictionary<int, CodeBlock>();
            _offsets = new int[code.Count];
            _hasIncomingJumps = new bool[code.Count];

            for (int i = 0; i < code.Count; i++)
            {
                Instruction inst = code.Values[i];
                _offsets[i] = inst.Address;
                _hasIncomingJumps[i] = inst.Label != null;
            }
            createNodes();

        //    CreateBasicBlocks();
            
            CreateBasicBlocks2();
            ComputeDominators();
            DebugWrite(BlockList, "0_tree.txt");
            ComputeNatrualLoops();
            Stack<Ast> stack = new Stack<Ast>();
            EntryBlock.ClearVisits();
            root = TryToMakeStatements(EntryBlock,stack);
        }
        void createNodes()
        {
            for (int i = 0, n = code.Values.Count; i < n; i++)
            {
                Instruction blockStart = code.Values[i];
                for (; i + 1 < n; i++)// See how big we can make that block...
                {
                    Instruction instruction = code.Values[i];
                    if (instruction.isBranch || _hasIncomingJumps[i + 1]) break;
                    //Instruction next = instruction.Next;

                }
                
            }
        }
        Block exitBlock;
        Block GetBlockAt(Instruction i) {
            // only time we are null is if we are trying to go to he last instruction
            if(i == null)
            {
                if (exitBlock == null)
                {
                    exitBlock = new Block(_last_pc + 1);
                    exitBlock.Id = BlockList.Count;
                    BlockList.Add(exitBlock);
                }
                return exitBlock;
            }
            foreach (Block block in BlockList) if (block.Address == i.Address) return block;
            Block b = new Block(i.Address);
            b.First = i;
            b.Id = BlockList.Count;
            BlockList.Add(b);
            return b;
        }

        Label FindLAabelAfter(Instruction i)
        {
            i = i.Next;
            while(i!= null)
            {
                if (i.Label != null) return i.Label;
                i = i.Next;
            }
            return null;//  new Label(_last_pc);
        }
        struct Branch
        {
            public Instruction Instruction;
            public int Address { get; private set; }
            public bool isConditional { get { return this.Instruction == null ? false : this.Instruction.GMCode.IsConditional(); } }
            public bool isReturn { get { return this.Instruction == null ? true : this.Instruction.GMCode == GMCode.Exit; } }
            public int BranchDesitation
            {
                get
                {
                    Debug.Assert(this.Instruction != null);
                    return this.Instruction.BranchDesitation;
                }
            }
            public Instruction BranchDesitationInstruction
            {
                get
                {
                    Debug.Assert(this.Instruction != null);
                    return (this.Instruction.Operand as Label).InstructionOrigin;
                }
            }
            public Branch(Instruction i) { this.Instruction = i; this.Address = i.Address; }
            public Branch(int address) { this.Instruction = null; this.Address = address; }
        }
        Branch FindBranchAfter(Instruction i)
        {
            i = i.Next;
            while (i != null)
            {
                if (i.isBranch || i.GMCode == GMCode.Exit) return new Branch(i);
                i = i.Next;
            }
            return new Branch(_last_pc); //  new Label(_last_pc);
        }
        Block LinkBlock(Block block, Instruction i)
        {
            Block next_block = GetBlockAt(i);
            next_block.AddPre(block);
            block.AddSucc(next_block);
            return next_block;
        }

 
        void CreateBasicBlocks2()
        {
            List<Block> blocklist = new List<Block>();
            
            for (int i = 0, n = code.Values.Count; i < n; i++)
            {
                Instruction blockStart = code.Values[i];

                //
                // See how big we can make that block...
                //
                for (; i + 1 < n; i++)
                {
                    Instruction instruction = code.Values[i];
                    if (instruction.isBranch || (instruction.Next != null && instruction.Next.Label != null)) break;///*|| opCode.canThrow()*/ || _hasIncomingJumps[i + 1]) break;


                    //    Instruction next = instruction.Next;
                }
                Block b = new Block(blockStart.Address);
                b.Id = blocklist.Count;
                blocklist.Add(b);
                b.First = blockStart;
                b.Last = code.Values[i];
              //  _nodes.Add(new ControlFlowNode(_nodes.Count, blockStart, instructions[i]));
            }
            Dictionary<Label, Block> labelLookup = new Dictionary<Label, Block>();
            Dictionary<Instruction, Block> instructionLookup = new Dictionary<Instruction, Block>();
            foreach (var b in blocklist) 
            {
                if (b.First.Label != null) labelLookup.Add(b.First.Label, b);
                instructionLookup.Add(b.First, b);
            }
            var search = First;
            while(search != null)
            {
                Label l = search.Operand as Label;
                if (l != null && !labelLookup.ContainsKey(l)) // out of scope label
                {
                    if(l.Address > _last_pc)
                    {
                        // We will make a new block for exit node
                        Block block = new Block(l.Address);
                        block.Id = blocklist.Count;
                        block.ExitLabel = true;
                        blocklist.Add(block);
                        block.LabelEntry = l;

                        labelLookup.Add(l, block);
                    }
                }
                search = search.Next;
            }

            foreach (Block node in blocklist)
            {
                if (node.ExitLabel) continue; // skip exit labels, no code
                Instruction end = node.Last;
                Label label = end.Operand as Label;
                if (end == null || end.Address >= _last_pc) continue;

                //
                // Create normal edges from one instruction to the next.
                //
                if(!end.isBranch) // falls though
                {
                    Block lookup = instructionLookup[end.Next];
                    node.AddSucc(lookup);
                    lookup.AddPre(node);
                } else if (end.GMCode == GMCode.B) // unconditional branch
                {
                    Block lookup = labelLookup[label];
                    node.AddSucc(lookup);
                    lookup.AddPre(node);
                } else // conditional branch
                {    // Create edges for branch instructions.
                    Block lookup = labelLookup[label];
                    node.AddSucc(lookup);
                    lookup.AddPre(node);

                    lookup = instructionLookup[end.Next];
                    node.AddSucc(lookup);
                    lookup.AddPre(node);
                }
             //   Label end_label = end.Operand as Label;
            //    if (end.GMCode == GMCode.Exit || end.GMCode == GMCode.Ret || (end_label != null && end_label.Address > _last_pc)) LinkBlock(node, end);
            }
            DebugWrite(blocklist, "1_tree.txt");
            BlockList = blocklist;
            EntryBlock = blocklist[0];
        }
      Instruction BlockPump(Instruction current)
        {
            HashSet<Instruction> toProcess = new HashSet<Instruction>();
            while (true)
            {
                Block block = GetBlockAt(current);
                label_next:
                Label label = FindLAabelAfter(current);
                Branch branch = FindBranchAfter(current);
                if (label != null && label.Address < (branch.Address + 1))
                {
                    block.Last = label.InstructionOrigin.Prev;
                    block.LabelEntry = label;
                    block = LinkBlock(block, label.InstructionOrigin);
                    current = label.InstructionOrigin;
                    goto label_next;
                }
                block.Last = branch.Instruction;

                if (branch.isReturn) continue;

                LinkBlock(block, branch.BranchDesitationInstruction);

                toProcess.Add(branch.BranchDesitationInstruction);

                if (!branch.isConditional) continue;  // will resume from branch 
                Instruction next = branch.Instruction.Next;
                Debug.Assert(next != null);
                block = LinkBlock(block, next);
                Debug.Assert(block.Address < 92);
                current = block.First;
            }
        }
       void CreateBasicBlocks()
        {
            //    BlockList = new SortedDictionary<int, Block>();

            _last_pc =code.Values.Last().Address + 1;
            Instruction current = First;
            Block block = GetBlockAt(current);
            EntryBlock = block;
            Stack<Instruction> workList = new Stack<Instruction>();
            workList.Push(current);
            while (workList.Count != 0)
            {
                current = workList.Pop();
               
            }
            // ok lets see about adding code
        }
    }
}