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
        public List<string> InstanceList { get; set; }
        public List<string> StringIndex { get; set; }

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
            Debug.Assert(stack.Count > 1);
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
            Instruction next = end.Next;
            while (start != null && start != next)
            {
                var block = ConvertOneStatement(ref start, stack, false);
                if (block.Count == 0) break; // we done?  No statements?
                ret.AddRange(block);
            }
            return ret;
        }
        public List<AstStatement> ConvertOneStatement(ref Instruction i,Stack<Ast> stack, bool dontCodeLabelStatements)
        {
            List<AstStatement> ret = new List<AstStatement>();
            while (i != null)
            {
                if (!dontCodeLabelStatements &&i.Label != null) ret.Add(new LabelStatement(i.Label)); // we add this so we know where they are, if that makes sense
                int count = i.GMCode.getOpTreeCount(); // not a leaf
                if (count == 2)
                {
                    if (count > stack.Count) throw new StackException(i, "Needed " + count + " on stack", ret);
                    AstTree ast = new AstTree(i, i.GMCode, stack.Pop(), stack.Pop());
                    stack.Push(ast);
                    i = i.Next;
                }
                else
                {
                    switch (i.GMCode)
                    {
                        case GMCode.Conv:
                            i = i.Next; // ignore
                            break;
                        case GMCode.Not:
                            if (stack.Count==0) throw new StackException(i, "Needed 1 on stack", ret);
                            stack.Push(new AstNot(i, stack.Pop()));
                            i = i.Next;
                            break;
                        case GMCode.Neg:
                            if (stack.Count == 0) throw new StackException(i, "Needed 1 on stack", ret);
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
                            Debug.Assert((i.OpCode & 0xFFFF) == 0);
                            stack.Push(stack.Peek().Copy());
                            i = i.Next;
                            break;
                        case GMCode.Popz:   // the call is now a statlemtn
                            ret.Add(new CallStatement(i, stack.Pop() as AstCall));
                            i = i.Next;
                            return ret;
                        case GMCode.Pop:
                            ret.Add(DoAssignStatment(stack, ref i));// assign statment
                            return ret; // it handles the next instruction
                        case GMCode.B: // this is where the magic happens...woooooooooo
                            ret.Add(new GotoStatement(i));
                            i = i.Next;
                            return ret;
                        case GMCode.Bf:
                        case GMCode.Bt:
                            {
                                Ast condition = stack.Pop();
                                ret.Add(new IfStatement(i, i.GMCode == GMCode.Bf ? condition.Invert() : condition, i.Operand as Label));
                            }
                            i = i.Next;
                            return ret;
                        case GMCode.BadOp:
                            i = i.Next; // skip
                            break; // skip those
                        case GMCode.Exit:
                            ret.Add(new ExitStatement(i));
                            i = i.Next;
                            return ret;
                        default:
                            throw new Exception("Not Implmented! ugh");
                    }
                }
            }
            return ret; // We shouldn't end here unless its the end of the instructions
        }
    }
    public class DecompilerNew
    {
        // Antlr4.Runtime.
        public string ScriptName { get; private set; }
        public Instruction.Instructions Instructions { get; private set; }
        public List<string> StringIndex { get; set; }
        public List<string> InstanceList { get; set; }

        private HashSet<Label> ignoreLabels;
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
            ignoreLabels = new HashSet<Label>();
            //    RemoveAllConv(instructions); // mabye keep if we want to find types of globals and selfs but you can guess alot from context
            // foreach (var i in instructions) statements.Add(i);



            //   SaveOutput(instructions, scriptName + "_original.txt");
            //  DFS dfs = new DFS(instructions);
            //  dfs.CreateDFS();
            //    List<Instruction> ilist = instructions.ToList();
            // ControlFlowGraph graph = ControlFlowGraphBuilder.Build(ilist);


            // graph.ComputeDominators2();
            //   graph.computeDominanceFrontier();

            // graph.ComputeDomiance();
            //   graph.computeDominanceFrontier();
            Decompile decompile = new Decompile(StringIndex, InstanceList);
            BasicBlocks basic = new BasicBlocks(instructions, decompile);


        }
    }
}
