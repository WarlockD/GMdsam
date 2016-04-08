using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.GMAst
{
    public class StackException : Exception
    {
        public Stack<ILExpression> stack;
        public Instruction inst;
        public StackException(Instruction i, string message, Stack<ILExpression> stack) : base(message) { this.inst = i; this.stack = stack; }
        public StackException(Instruction i, string message) : this(i,message,null) {  }
    }
    class ILDecompile
    {
        public List<string> StringIndex;
        public List<string> InstanceList;
        Dictionary<int, ILLabel> labelToILabel;
        int _last_pc;
        ILLabel _exitLabel = null;
        Instruction.Instructions _instructions;
        Dictionary<Instruction, Stack<ILExpression>> _stacks;
        List<Label> exitLabels;
        List<Label> labelStatementsPrinted;
        List<Label> branchesSeen;
        public ILLabel GetILLabel(Label l)
        {
            ILLabel ret;
            if (!labelToILabel.TryGetValue(l.Address, out ret))
            {
                labelToILabel[l.Address] = ret = new ILLabel() { Name = l.ToString(), OldLabel = l };
            }
            return ret;
        }
        public string LookupInstance(ILExpression ast)
        {
            Debug.Assert(ast.Code == GMCode.Push);
            ILValue value = ast.Operand as ILValue;
            if (value != null)
            {
                int intvalue;
                if(value.TryParse(out intvalue)) return LookupInstance(intvalue);
            }
            return ast.Operand.ToString();
        }
        public string LookupInstance(int value)
        {
            return GMCodeUtil.lookupInstance(value, InstanceList);
        }
        public ILDecompile(List<string> stringIndex, List<string> objectList)
        {
            InstanceList = objectList;
            StringIndex = stringIndex;
        }
        ILVariable DoRValueComplex(Stack<ILExpression> stack, Instruction i) // instance is  0 and may be an array
        {
            string var_name = i.Operand as string;
            Debug.Assert(i.Instance == 0);
            ILExpression index = i.OperandInt > 0 ? stack.Pop() : null;// if we are an array
            ILExpression objectInstance = stack.Pop();
            return new ILVariable() { Name = var_name, Instance = objectInstance, InstanceName = LookupInstance(objectInstance) , Index = index };
        }
        ILVariable DoRValueSimple(Instruction i) // instance is != 0 or and not an array
        {
            Debug.Assert(i.Instance != 0);
            return  new ILVariable() { Name = i.Operand as string, InstanceName = LookupInstance(i.Instance) };
        }
        ILVariable DoRValueVar(Stack<ILExpression> stack, Instruction i)
        {
            if (i.Instance != 0) return  DoRValueSimple(i);
            else return DoRValueComplex(stack, i);
        }
        ILExpression DoPush(Stack<ILExpression> stack, ref Instruction i)
        {
            ILExpression ret = (i.FirstType == GM_Type.Var) ?  
                new ILExpression(GMCode.Push, DoRValueVar(stack, i))
              : new ILExpression(GMCode.Push, ILValue.FromInstruction(i));
            i = i.Next;
            return ret;
        }
        ILExpression DoCallRValue(Stack<ILExpression> stack, ref Instruction i)
        {
            int arguments = i.Instance;
            ILExpression call = new ILExpression(GMCode.Call, i.Operand as string);
            for (int a = 0; a < arguments; a++) call.Arguments.Add(stack.Pop());
            i = i.Next;
            return call;
        }
        ILExpression DoAssignStatment(Stack<ILExpression> stack, ref Instruction i)
        {
            if (stack.Count < 1) throw new Exception("Stack size issue");
            ILExpression assign = new ILExpression(GMCode.Pop, DoRValueVar(stack, i));
            assign.Arguments.Add(stack.Pop());
            i = i.Next;
            return assign;
        }
    

        void SetUpDecompiler(Instruction.Instructions instructions)
        {
            exitLabels = new List<Label>();
            labelStatementsPrinted = new List<Label>();
            branchesSeen = new List<Label>();
            labelToILabel = new Dictionary<int, ILLabel>();
            _instructions = new Instruction.Instructions(instructions); // clone it
            _exitLabel = null;
            _last_pc = instructions.Last().Address;
            _stacks = new Dictionary<Instruction, Stack<ILExpression>>();
        }
        public void AddLabel(Label label, List<ILNode> e)
        {
            if(labelStatementsPrinted.Contains(label)) throw new Exception("Duplicate label!");
            e.Add(GetILLabel(label));
            labelStatementsPrinted.Add(label);
            _stacks = new Dictionary<Instruction, Stack<ILExpression>>();
        }
        public void ExtraLabels(List<ILNode> ret)
        {
            if (exitLabels != null)
            {
                foreach (var elabel in exitLabels)
                {
                    if (!labelStatementsPrinted.Contains(elabel)) AddLabel(elabel, ret);
                }
            }
        }
     
        public List<ILNode> DecompileInternal(Instruction.Instructions instructions)
        {
            SetUpDecompiler(instructions);
      

           
            var nodes = DecompileInternal( 0, instructions.Count - 1, _stacks);
            if (_exitLabel != null)
            {
                nodes.Add(_exitLabel);
                nodes.Add(new ILExpression(GMCode.Exit, null));
            }
            ILExpression expr = nodes.Last() as ILExpression;
            if (expr != null && (expr.Code != GMCode.Exit || expr.Code != GMCode.Ret))
            {
                nodes.Add(new ILExpression(GMCode.Exit, null));
            }
            return nodes;
        }
        public Label FindNextBranchLabel(Instruction i)
        {
            while(i!= null)
            {
                if (i.Code == GMCode.B) return i.Operand as Label;
                i = i.Next;
            }
            throw new Exception("Could not find");
        }
        // This hack converts case statements into a series of if statements while removeing dup
        public List<ILNode> DecompileInternal(int to, int from, Dictionary<Instruction, Stack<ILExpression>> stacks)
        {
            List<ILNode> nodes = new List<ILNode>();
          
            Instruction i = _instructions[to];
            HashSet<Instruction> skip = new HashSet<Instruction>();
            // side not, I don't think labels apper in wierd parts of the code
            while (i != null && _instructions.IndexOf(i) <= from)
            {
                Stack<ILExpression> stack;
                if (!stacks.TryGetValue(i, out stack)) stack = new Stack<ILExpression>();

                if (i.Label != null) nodes.Add(GetILLabel(i.Label));
                if (skip.Contains(i)) // we want to skip these instructions
                {
                    i = i.Next; continue;
                }
                ILExpression stmt = ConvertOneStatement(ref i, stack);
                // Here is the switch case hack, we make a "switch" statement by collecting all the the switch
                // cases with the ending being the jump to fail
                // dup is exculsive used during this so I will have to check when its not?
                if (stmt.Code == GMCode.Dup)
                {
                    stack.Push(stmt.Arguments[0]); // put it back on the stack
                    Label failCase = FindNextBranchLabel(i);
                    stacks.Add(failCase.InstructionOrigin, new Stack<ILExpression>(stack)); // Add it to the stack for the popv
                    stmt.Code = GMCode.Switch;
                    stmt.Arguments.Clear();
                    stmt.Operand = GetILLabel(failCase);
                    do
                    {
                        stmt.Arguments.Add(ConvertOneStatement(ref i, new Stack<ILExpression>(stack))); // should be one condition with a jump
                        Debug.Assert(i.Code == GMCode.Dup || i.Code == GMCode.B);
                        if (i.Code == GMCode.Dup) i = i.Next; // skip the dup

                    } while (i.Code != GMCode.B); // first branch we see, its the fail
                    skip.Add(failCase.InstructionOrigin); // skip the popz
                }
                nodes.Add(stmt);
                if (!stmt.Code.IsUnconditionalControlFlow() && i != null && !stacks.ContainsKey(i)) stacks.Add(i, stack);

                if (stmt.Code.IsConditionalControlFlow() || stmt.Code == GMCode.B)
                {
                    ILLabel ilabel = stmt.Operand as ILLabel;
                    if (ilabel.OldLabel.Address > _last_pc)
                    {
                        // its an exit label, modfy it
                        ilabel.isExit = true;
                        _exitLabel = ilabel;
                    }
                    else if (!stacks.ContainsKey(ilabel.OldLabel.InstructionOrigin)) stacks.Add(ilabel.OldLabel.InstructionOrigin, new Stack<ILExpression>(stack));
                }

            }
           
            return nodes;
        }
        public ILExpression ConvertOneStatement(ref Instruction i, Stack<ILExpression> stack)
        {
            ILExpression ret = null;
            List<ILExpression> rets = new List<ILExpression>();
            while (i != null && ret == null)
            {
                int count = i.Code.getOpTreeCount(); // not a leaf
                Label label = i.Operand as Label;
                if (count == 2)
                {
                    if (count > stack.Count) throw new StackException(i, "Needed " + count + " on stack", null);
                    ILExpression right = stack.Pop();
                    ILExpression left = stack.Pop();
                    ILExpression ast = new ILExpression(i.Code, null, left, right);
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
                        case GMCode.Neg:
                            if (stack.Count == 0) throw new StackException(i, "Needed 1 on stack", null);
                            stack.Push(new ILExpression(i.Code, null, stack.Pop()));
                            i = i.Next;
                            break;
                        case GMCode.Push:
                            stack.Push(DoPush(stack, ref i));
                            break;
                        case GMCode.Call:
                            stack.Push(DoCallRValue(stack, ref i));
                            break;
                        case GMCode.Dup:    
                            if(i.FirstType == GM_Type.Var)
                            {
                                Debug.Assert(i.Instance == 0); // simple v dup
                                ret = new ILExpression(i.Code, null, stack.Pop());
                                Debug.WriteLine("Saw a dup");
                                // usally for a case statement
                            } else
                            {
                                if(i.Instance == 0)
                                {
                                    stack.Push(stack.Peek());
                                } else
                                {
                                    foreach(var e in stack.Reverse().ToList()) stack.Push(e);
                                }
                                
                                // otherwise just try a normal dup
                            }
                            //Debug.Assert(i.FirstType == GM_Type.Var);
                            // agenst a var                          
                            // Fall though for case detection
                            
                            i = i.Next;
                            break;
                        case GMCode.Popz:
                            // if it was a call, we want to pop it and return it as a statment
                            // otherwise it was part of a case statment that killed the dups
                            if (stack.Peek().Code == GMCode.Call)
                            {
                                ret = stack.Pop();
                            }
                            else {
                                stack.Pop();
                                Debug.WriteLine("poped a dup");
                            }

                          //  Debug.Assert(stack.Peek().Code == GMCode.Call);
                           
                            i = i.Next;
                            break;
                        case GMCode.Pop:
                            ret = DoAssignStatment(stack, ref i);// assign statment
                            break; // it handles the next instruction
                        case GMCode.Bf:
                        case GMCode.Bt:
                        case GMCode.B: // this is where the magic happens...woooooooooo
                            ret = new ILExpression(i.Code, GetILLabel(label));
                            if (i.Code != GMCode.B && stack.Count > 0)
                            {
                                ILExpression condition = stack.Pop();
                                ret.Arguments.Add(condition);
                            }
                            i = i.Next;
                            if (branchesSeen != null) branchesSeen.Add(label);
                            break;
                        case GMCode.BadOp:
                            i = i.Next; // skip
                            break; // skip those
                        case GMCode.Pushenv:
                            ret = new ILExpression(i.Code,  GetILLabel(label), stack.Pop());
                            i = i.Next;
                            break;
                        case GMCode.Popenv:
                            ret = new ILExpression(i.Code, GetILLabel(label));
                            i = i.Next;
                            break;

                        case GMCode.Exit:
                        case GMCode.Ret:
                            ret = new ILExpression(i.Code, null);
                            i = i.Next;
                            break;
                        default:
                            throw new Exception("Not Implmented! ugh");
                    }
                }
                if (label != null && ret == null)
                {
                    throw new Exception("Short here, label in the middle of an expresson?");
                   // if (label.Address > i.Last.Address) exitLabels.Add(label);
                }
            }
            return ret; // We shouldn't end here unless its the end of the instructions
        }
    }
}
