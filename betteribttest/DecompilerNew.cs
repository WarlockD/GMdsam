using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Collections;

namespace betteribttest
{
    public class StackException : Exception {
        public List<AstStatement> Block { get; private set; }
        public Instruction Last { get; private set; }
        public StackException(Instruction i, string message, List<AstStatement> block) : base(message) { Last = i; Block = block; }
    }
    public class Decompile
    {
        ControlFlowGraphOld graph;
        // Antlr4.Runtime.
        public string ScriptName { get; private set; }
        public Instruction.Instructions Instructions { get; private set; }

        int _last_pc;
        SortedList<int, Instruction> code;

        public List<string> InstanceList { get; set; }
        public List<string> StringIndex { get; set; }


        class Loop : IEquatable<Loop>
        {
            public ControlFlowNodeOld Header { get; private set; }
            public List<ControlFlowNodeOld> Blocks { get; private set; }
            public Loop(ControlFlowNodeOld header)
            {
                Header = header;
                Blocks = new List<ControlFlowNodeOld>();
                Blocks.Add(header);
            }
            public override int GetHashCode()
            {
                int hash = Header.GetHashCode();
                // foreach (var i in Blocks) hash = hash * 31 + i.GetHashCode();
                return hash;
            }
            public bool Equals(Loop other)
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
        HashSet<Loop> loopList = new HashSet<Loop>();
        Loop NatrualLoopForEdge(ControlFlowNodeOld header, ControlFlowNodeOld tail)
        {

            Loop loop = null;
            foreach (var t in loopList) if (t.Header == header) { loop = t; break; }
            if (loop == null)
            {
                loop = new Loop(header);
                loopList.Add(loop);
            }
            Stack<ControlFlowNodeOld> workList = new Stack<ControlFlowNodeOld>();
            if (header != tail)
            {
                loop.Blocks.Add(tail);
                workList.Push(tail);
            }
            while (workList.Count > 0)
            {
                ControlFlowNodeOld node = workList.Pop();
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
        void ComputeNatrualLoops()
        {
            loopList = new HashSet<Loop>();
            ControlFlowNodeOld entryPoint = graph.EntryPoint;
            foreach (var node in graph.Nodes)
            {
                if (node == entryPoint) continue;
                foreach (var succ in node.Successors)
                {
                    // Every successor that dominates its predecessor
                    // must be the header of a loop.
                    // That is, block -> succ is a back edge.
                    // if(block.ContainsDominator(succ))
                    if (node.Dominates(succ))
                        loopList.Add(NatrualLoopForEdge(succ, node));
                }
            }
        }
        // http://www.backerstreet.com/decompiler/creating_statements.php
        void RemoveGotos(StatementBlock block, bool RemoveLabelsToo = false)
        {
            if (block.Count > 0 && block.Last() is GotoStatement) block.Remove(block.Last());
            if (block.Count > 0 && block.First() is LabelStatement) block.Remove(block.First());
        }
        void ReLinkNodes(ControlFlowNodeOld from, ControlFlowNodeOld to, bool clearNodes = false)
        {
            if (clearNodes)
            {
                from.Outgoing.Clear();
                to.Incomming.Clear();
            }
            var edge = new ControlFlowEdgeOld(from, to);
            from.Outgoing.Add(edge);
            to.Incomming.Add(edge);
        }
        IfStatement GetBranchNodes(ControlFlowNodeOld node, ref ControlFlowNodeOld trueNode, ref ControlFlowNodeOld falseNode)
        {
            if (node.Outgoing.Count != 2) return null;
            trueNode = node.Outgoing[0].Target;
            falseNode = node.Outgoing[1].Target;
            IfStatement ifs = node.block.Last() as IfStatement;
            if (ifs == null)
            {
                throw new Exception("Really expected an ifstatement");
            }
            GotoStatement gotos = ifs.Then as GotoStatement;
            if (gotos == null) throw new Exception("REALLY needed a goto here");
            int target = gotos.Target.Address;
            if(target > _last_pc)
            {
                if (falseNode.Address != -1)
                {
                    trueNode = node.Outgoing[1].Target;
                    falseNode = node.Outgoing[0].Target;
                }
                if (falseNode.Address != -1) throw new Exception("So the labels don't match? exit node ugh");
            } else
            {
                if (falseNode.Address != gotos.Target.Address)
                {
                    trueNode = node.Outgoing[1].Target;
                    falseNode = node.Outgoing[0].Target;
                }
                if (falseNode.Address != gotos.Target.Address) throw new Exception("So the labels don't match? ugh");
            }
         
            return ifs;
        }
        void RemoveStatementsAndEndingGotos(StatementBlock block)
        {
            if (block.Count == 0) return;
            if (block.Last() is GotoStatement) block.Remove(block.Last());
            for (int i = 0; i < block.Count; i++) if (block[i] is LabelStatement) block.RemoveAt(i);
        }
        StatementBlock NodeToBlock(ControlFlowNodeOld node, Stack<Ast> stack) { return new StatementBlock(ConvertManyStatements(node.Start, node.End, stack)); }
        int NodeToAst(ControlFlowNodeOld node, Stack<Ast> stack, bool removeStatementsAndGotos)
        {
            StatementBlock block = new StatementBlock();
            if (node.block == null && node.Address != -1)
            {
                if (node.Address == -1) block.Add(new ExitStatement(null)); // fake exit
                else block = NodeToBlock(node, stack);
            }
            if (removeStatementsAndGotos) RemoveStatementsAndEndingGotos(block);
            node.block = block;
            return block.Count;
        }
        bool CombineNodes(ControlFlowNodeOld node) // reduces redundent nodes
        {
            if (node == null || node == graph.EntryPoint || node == graph.RegularExit) return false;
            if (node.Outgoing.Count == 0) return false; // or we are at the exit statment
            if (node.Outgoing.Count != 1) return false;  // Not an iff statement

            var nextNode = node.Outgoing[0].Target;
            if (nextNode == graph.EntryPoint || nextNode == graph.RegularExit) return false;
            if (nextNode.Incomming.Count != 1) return false;  // not a simple connection
            //Debug.Assert(node.BlockIndex != 85 && node.BlockIndex != 90);
            //     Debug.Assert(node.BlockIndex == 90);
            RemoveStatementsAndEndingGotos(node.block);
            foreach (var statement in nextNode.block) node.block.Add(statement.Copy() as AstStatement);
            RemoveStatementsAndEndingGotos(node.block); // run it again to check for label statments
            node.Outgoing = nextNode.Outgoing;
            foreach (var target in node.Outgoing) target.Source = node; // NOW I get why we have a seperate class for edges
            graph.Nodes.Remove(nextNode);
            graph.ExportGraph("export.txt");
            return true; // we found one
        }
        bool ConvertAllSimpleIfStatements(ControlFlowNodeOld node)
        {
            if (node == null || node == graph.EntryPoint || node == graph.RegularExit) return false;
            if (node.Outgoing.Count == 0) return false; // or we are at the exit statment
            if (node.Outgoing.Count != 2) return false; // Not an iff statement
            ControlFlowNodeOld trueNode = null;
            ControlFlowNodeOld falseNode = null;
            IfStatement ifs = GetBranchNodes(node, ref trueNode, ref falseNode);
            if (trueNode.Outgoing.Count != 1 || trueNode.Outgoing[0].Target != falseNode) return false; // not a simple if statment, but we still got stuff after
            var continueNode = falseNode;
            node.block.Remove(ifs);
            RemoveStatementsAndEndingGotos(trueNode.block); // run it again to check for label statments
            RemoveStatementsAndEndingGotos(node.block); // run it again to check for label statments
            node.block.Add(new IfStatement(ifs.Instruction, ifs.Condition.Invert(), trueNode.block.Copy() as AstStatement));
            ReLinkNodes(node, continueNode, true);
            graph.Nodes.Remove(trueNode);
            graph.ExportGraph("export.txt");
            return true; // we found one
        }
        bool ConvertIfElseStatements(ControlFlowNodeOld node)
        {
            if (node == null || node == graph.EntryPoint || node == graph.RegularExit) return false;
            if (node.Outgoing.Count == 0) return false; // or we are at the exit statment
            if (node.Outgoing.Count != 2) return false; // Not an iff statement
            ControlFlowNodeOld trueNode = null;
            ControlFlowNodeOld falseNode = null;
            IfStatement ifs = GetBranchNodes(node, ref trueNode, ref falseNode);
            if (trueNode.Outgoing.Count != 1 || falseNode.Outgoing.Count != 1 || trueNode.Outgoing[0].Target != falseNode.Outgoing[0].Target) return false; ; // both don't end up at the same place
            var continueNode = trueNode.Outgoing[0].Target;
            node.block.Remove(ifs);
            RemoveStatementsAndEndingGotos(trueNode.block);
            RemoveStatementsAndEndingGotos(node.block);
            RemoveStatementsAndEndingGotos(falseNode.block);
            node.block.Add(new IfStatement(ifs.Instruction, ifs.Condition.Invert(), trueNode.block.Copy() as AstStatement, falseNode.block.Copy() as AstStatement));

            ReLinkNodes(node, continueNode, true);
            graph.Nodes.Remove(trueNode);
            graph.Nodes.Remove(falseNode);
            graph.ExportGraph("export.txt");
            return true; // we found one
        }
        bool ConvertWhileLoops(ControlFlowNodeOld node)
        {
            if (node == null || node == graph.EntryPoint || node == graph.RegularExit) return false;
            if (node.Outgoing.Count == 0) return false; // or we are at the exit statment
            if (node.Outgoing.Count != 2) return false; // Not a while statement
            ControlFlowNodeOld trueNode = null;
            ControlFlowNodeOld falseNode = null;
            IfStatement ifs = GetBranchNodes(node, ref falseNode, ref trueNode);
            // here is the trick.  The trueNode dosn't matter how many outgoing it is
            // the only one that does matter is if the falseNood (loop body) outgoing ONLY goes back to node
            // I need to be able to handle breaks so I will figure that out latter
            if (!node.Successors.Contains(falseNode) || !node.Predecessors.Contains(falseNode)) return false;
            if (falseNode.Outgoing.Count != 1 || falseNode.Incomming.Count != 1) throw new Exception("We need to handle breaks and continues");
            // We have a loop.  Could it contain true? humm mabye.  The dissasembler "fixes" the branchTrue/branchFalse so
            // does that mean it automaticly converts it to a while loop? humm
        
            node.Outgoing.Remove(falseNode.Incomming[0]);
            node.Incomming.Remove(falseNode.Incomming[0]);
            node.Outgoing.Remove(falseNode.Outgoing[0]);
            node.Incomming.Remove(falseNode.Outgoing[0]);
            falseNode.Outgoing.Clear();
            falseNode.Incomming.Clear();
            graph.Nodes.Remove(falseNode);
            if (ifs == null) throw new Exception("We NEED there to be an if statement here");
            node.block.Remove(ifs);
            if (!(ifs.Then is GotoStatement)) throw new Exception("The if statment is screwy here");
            var whileLoop = new WhileLoop(ifs.Condition.Invert(), falseNode.block.Copy() as StatementBlock);
            node.block.Add(whileLoop);
            return true; // we found one
        }
        void DoJustIfs()
        {
            bool changed = false;
            int count = 0;
            int i = 0;
            do
            {
                changed = false;
                var node = graph.Nodes[i];
                if (node != graph.EntryPoint && node != graph.RegularExit)
                {
                    if (CombineNodes(node)) changed = true;
                    if (ConvertAllSimpleIfStatements(node)) changed = true;
                    if (ConvertIfElseStatements(node)) changed = true;
                    if (ConvertWhileLoops(node)) changed = true;
                }
                if (changed) { i = 0; count++; } else i++;
            } while (i < graph.Nodes.Count);
            graph.ExportGraph("export.txt");
        }
        void FixStatementBlockWithAnds(StatementBlock block)
        {
            bool changed; // We loop because we might have more than 1 and
            do
            {
                changed = false;
                List<IfStatement> all = new List<IfStatement>();
                block.FindType(all);
                foreach (var ifs in all)
                {
                    IfStatement then = ifs.Then as IfStatement;
                    if (then != null && then.Else == null)
                    {
                        ifs.Condition = new LogicalAnd(ifs.Condition.Copy(), then.Condition.Copy());
                        ifs.Then = then.Then.Copy() as AstStatement;
                        changed = true;
                    }
                }
            } while (changed);
        }
        void FixIfsWithAnds()
        {
            for(int i=0;i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (node == graph.EntryPoint || node == graph.RegularExit) continue;
                FixStatementBlockWithAnds(node.block);
            }
        }
        Dictionary<ControlFlowNodeOld, Stack<Ast>> stackMap;
        void BuildTheWorld()
        {
            stackMap = new Dictionary<ControlFlowNodeOld, Stack<Ast>>();
           
            graph.BuildAllAst(this,stackMap);
            graph.ExportGraph("start_ast.txt");
            //  var node = graph.EntryPoint.Outgoing[0].Target; // start
            DoJustIfs();
            graph.ExportGraph("beforeands.txt");
            FixIfsWithAnds();
            // regraph to mabye use DominaceFrontier...sigh I really need to figure it out more
            // be cause of the idiotic way I am rebuilding these trees, we have to make sure
            // the if statments are all processed first
            // If anyone is reading this, this is the WRONG way to rebuild basic blocks, seriously, this is 
            // retarded.  But since the bytecode is very simple and the compiler dosn't do a lot of wierd things
            // we can kind of get away with it...mostly
            // as a side note, I should use ComputeNatrualLoops() and do some better recursive stuff
            // however.. screw it.

            graph.ExportGraph("final.txt");
            //var testBlock = ConvertIfStatements(node);
        }
        void SaveTheWorld(string script_name)
        {
            if (graph.Nodes.Count == 3) // should only have 3 nodes, entry, body, exit
            {
                using (StreamWriter wr = new StreamWriter(script_name + "_decompiled.txt")) graph.Nodes[2].block.DecompileToText(wr);

            }
            else {
                graph.ExportGraph("error_graph.txt");
                throw new Exception("Something went nutty");
            }
        }
     




        public string LookupInstance(Ast ast)
        {
            int value;
            if (ast.TryParse(out value)) return LookupInstance(value);
            else return ast.ToString(); // might be a variable.  We can trace this maybe
        }
        public string LookupInstance(int value)
        {
            return GMCodeUtil.lookupInstance(value, InstanceList);
        }
        public Decompile(List<string> stringIndex, List<string> objectList)
        {
            InstanceList = objectList;
            StringIndex = stringIndex;
        }
        Ast DoRValueComplex(Stack<Ast> stack, Instruction i) // instance is  0 and may be an array
        {
            string var_name = i.Operand as string;
            Debug.Assert(i.Instance == 0);
            // since we know the instance is 0, we hae to look up a stack value
          //  Debug.Assert(!(i.OperandInt > 0 && stack.Count > 1));
        //    Debug.Assert(stack.Count != 0);
            AstVar ret = null;
            Ast index = i.OperandInt > 0 ? stack.Pop() : null;// if we are an array
            Ast objectInstance = stack.Pop();
            ret = new AstVar(i, objectInstance, LookupInstance(objectInstance), var_name);
            if (index != null)
                return new AstArrayAccess(ret, index);
            else
                return ret;
        }
        AstVar DoRValueSimple(Instruction i) // instance is != 0 or and not an array
        {
            Debug.Assert(i.Instance != 0);
            AstVar v = null;
            if (i.Instance != 0) // we are screwing with a specifice object
            {
                return new AstVar(i, LookupInstance(i.Instance), i.Operand as string);
            }
            // Here is where it gets wierd.  iOperandInt could have two valus 
            // I think 0xA0000000 means its local and
            // | 0x4000000 means something too, not sure  however I am 50% sure it means its an object local? Room local, something fuky goes on with the object ids at this point
            if ((i.OperandInt & 0xA0000000) != 0) // this is always true here
            {
                v = new AstVar(i, LookupInstance(i.Instance), i.Operand as string);
#if false
                if ((i.OperandInt & 0x40000000) != 0)
                {
                    // Debug.Assert(i.Instance != -1); // built in self?
                    v = new AstVar(i, LookupInstance(i.Instance), i.Operand as string);
                }
                else {
                    v = new AstVar(i, i.Instance, GMCodeUtil.lookupInstance(i.Instance, InstanceList), i.Operand as string);
                    //v = new AstVar(i, i.Instance, i.Operand as string);
                }
#endif
                return v;
            }
            else throw new Exception("UGH check this");
        }
        Ast DoRValueVar(Stack<Ast> stack, Instruction i)
        {
            Ast v = null;
            if (i.Instance != 0) v = DoRValueSimple(i);
            else v = DoRValueComplex(stack, i);
            return v;
        }
        Ast DoPush(Stack<Ast> stack, ref Instruction i)
        {
            Ast ret = null;
            if (i.FirstType == GM_Type.Var) ret = DoRValueVar(stack, i);
            else ret = AstConstant.FromInstruction(i);
            i = i.Next;
            return ret;
        }
        AstCall DoCallRValue(Stack<Ast> stack, ref Instruction i)
        {
            int arguments = i.Instance;
           List<Ast> args = new List<Ast>();
           for (int a = 0; a < arguments; a++) args.Add(stack.Pop());
            AstCall call = new AstCall(i, i.Operand as string, args);
            i = i.Next;
            return call;
        }
        AstStatement DoAssignStatment(Stack<Ast> stack, ref Instruction i)
        {
            if (stack.Count < 1) throw new Exception("Stack size issue");
            Ast v = DoRValueVar(stack, i);
            Ast value = stack.Pop();
            AssignStatment assign = new AssignStatment(i, v, value);
            i = i.Next;
            return assign;
        }
        public List<AstStatement> ConvertManyStatements(Instruction start, Instruction end, Stack<Ast> stack)
        {
            List<AstStatement> ret = new List<AstStatement>();
            Stack<List<AstStatement>> envStack = new Stack<List<AstStatement>>();
            Instruction next = end.Next;
            while (start != null && start != next)
            {
                AstStatement stmt = ConvertOneStatement(ref start, stack);
                if (stmt == null) break; // we done?  No statements?
                if(stmt is PushEnviroment)
                {
                    ret.Add(stmt);
                    envStack.Push(ret);
                    ret = new List<AstStatement>();
                } else if(stmt is PopEnviroment)
                {
                    var last = envStack.Peek().Last();
                    var push = last as PushEnviroment;
                    if (push == null) throw new Exception("Last instruction should of been a push");
                    push.Block = new StatementBlock(ret);
                    ret = envStack.Pop();
                    // we don't need the pop in here now so don't add it ret.Add(stmt)
                }
                else ret.Add(stmt);
            }
            if (envStack.Count > 0) throw new Exception("We are still in an enviroment stack");
            return ret;
        }
        public AstStatement ConvertOneStatement(ref Instruction i,Stack<Ast> stack)
        {
            AstStatement ret = null;
            while (i != null && ret == null)
            {
                 int count = i.Code.getOpTreeCount(); // not a leaf
                if (count == 2)
                {
                    if (count > stack.Count) throw new StackException(i, "Needed " + count + " on stack", null);
                    Ast right = stack.Pop().Copy();
                    Ast left = stack.Pop().Copy();
                    AstTree ast = new AstTree(i, i.Code, left, right);
                    stack.Push(ast);
                    i = i.Next;
                }
                else
                {
                    switch (i.Code)
                    {
                        case GMCode.Conv:
                            i = i.Next; // ignore
                            break;
                        case GMCode.Not:
                            if (stack.Count==0) throw new StackException(i, "Needed 1 on stack",null);
                            stack.Push(new AstNot(i, stack.Pop()));
                            i = i.Next;
                            break;
                        case GMCode.Neg:
                            if (stack.Count == 0) throw new StackException(i, "Needed 1 on stack",null);
                            stack.Push(new AstNegate(i, stack.Pop()));
                            i = i.Next;
                            break;
                        case GMCode.Push:
                            stack.Push(DoPush(stack, ref i));
                            break;
                        case GMCode.Call:
                            stack.Push(DoCallRValue(stack, ref i));
                            break;
                        case GMCode.Dup:
                            //Debug.Assert((i.OpCode & 0xFFFF) == 0);
                            if((i.OpCode & 0xFFFF) == 0)
                            {
                                stack.Push(stack.Peek().Copy());
                            } else
                            {
                                // hacky lets just copy the whole thing
                                Stack<Ast> copy = new Stack<Ast>(stack);
                                foreach (var ast in copy) stack.Push(ast);
                            }
                            
                            i = i.Next;
                            break;
                        case GMCode.Popz:   // the call is now a statlemtn
                            {
                                Ast call = stack.Pop();
                                if(call is AstCall) ret = new CallStatement(i, call as AstCall);
                                else { ret = new CommentStatement("Popz on non-call : " + call.ToString()); }

                            }
                            i = i.Next;
                            break;
                        case GMCode.Pop:
                            ret =  DoAssignStatment(stack, ref i);// assign statment
                            break; // it handles the next instruction
                        case GMCode.B: // this is where the magic happens...woooooooooo
                            ret = new GotoStatement(i);
                            i = i.Next;
                            break;
                        case GMCode.Bf:
                        case GMCode.Bt:
                            {
                                Ast condition = stack.Pop();
                                ret = new IfStatement(i, i.Code == GMCode.Bf ? condition.Invert() : condition, i.Operand as Label);
                            }
                            i = i.Next;
                            break;
                        case GMCode.BadOp:
                            i = i.Next; // skip
                            break; // skip those
                        case GMCode.Pushenv:
                            {
                                Ast env = stack.Pop();
                                ret = new PushEnviroment(i, env, LookupInstance(env));
                                
                            }
                            i = i.Next;
                            break;
                        case GMCode.Popenv:
                            ret = new PopEnviroment(i,null);
                            i = i.Next;
                            break;
                        case GMCode.Exit:
                            ret = new ExitStatement(i);
                            i = i.Next;
                            break;
                        default:
                            throw new Exception("Not Implmented! ugh");
                    }
                }
            }
            return ret; // We shouldn't end here unless its the end of the instructions
        }

      
        string FindInstance(int index)
        {
            Debug.Assert(index > 0);
            if (InstanceList != null && index < InstanceList.Count) return InstanceList[index];
            else return "Object(" + index + ")";
        }
        // from what I have dug down, all streight assign statments are simple and the compiler dosn't do any
        // wierd things like branching with uneven stack values unless its in loops, so if we find all the assigns
        // we make life alot easyer down the road
      
        // convert into general statments and find each group of statements between labels

        // makes a statment blocks that fixes the stack before the call
        // Emulation is starting to look REALLY good about now
        AstStatement EmptyStack(Stack<Ast> stack, AstStatement statment, int top = 0)
        {
            if (stack.Count > top)
            {
                var items = stack.ToArray();
                StatementBlock block = new StatementBlock();
                block.Add(new CommentStatement("Had to fix the stack"));
                for (int i = top; i < items.Length; i++) block.Add(new PushStatement(items[i].Copy()));
                if (statment is StatementBlock)
                {
                    foreach (var s in statment as StatementBlock) block.Add(s.Copy() as AstStatement);
                }
                block.Add(statment);
                return block;
            }
            else return statment;
        }
        AstStatement EmptyStack(Stack<Ast> stack, Label target, int top = 0)
        {
            if (stack.Count > top)
            {
                var items = stack.ToArray();
                StatementBlock block = new StatementBlock();
                block.Add(new CommentStatement("Had to fix the stack"));
                for (int i = top; i < items.Length; i++) block.Add(new PushStatement(items[i].Copy()));
                block.Add(new GotoStatement(target));
                return block;
            }
            else return new GotoStatement(target);
        }
  
        
        public void SaveOutput(StatementBlock block, string filename)
        {
            using (System.IO.StreamWriter tw = new System.IO.StreamWriter(filename))
            {
                block.DecompileToText(tw);
            }
        }
        public void Disasemble(string scriptName, BinaryReader r, List<string> StringIndex, List<string> InstanceList)
        {
            // test
            this.ScriptName = scriptName;
            this.InstanceList = InstanceList;
            this.StringIndex = StringIndex;

            var instructions = Instruction.Create(r, StringIndex,InstanceList);
            if(instructions.Count == 0)
            {
                Debug.WriteLine("No instructions in script '" + scriptName + "'");
                return;
            }
            //    RemoveAllConv(instructions); // mabye keep if we want to find types of globals and selfs but you can guess alot from context
            code = new SortedList<int, Instruction>(200);
            _last_pc = instructions.Last().Address;
            graph = ControlFlowGraphBuilderOld.Build(instructions.ToList());
            graph.ComputeDomiance();
            graph.computeDominanceFrontier();
            graph.ExportGraph("start.txt");
            BuildTheWorld();
            SaveTheWorld(scriptName);

        }
    }
}
