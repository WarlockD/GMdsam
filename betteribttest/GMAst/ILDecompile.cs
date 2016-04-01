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
        List<Label> exitLabels;
        List<Label> labelStatementsPrinted;
        List<Label> branchesSeen;
        public void SetUpDecompiler()
        {
            exitLabels = new List<Label>();
            labelStatementsPrinted = new List<Label>();
            branchesSeen = new List<Label>();
        }
        public void AddLabel(Label label, List<ILNode> e)
        {
            if(labelStatementsPrinted.Contains(label)) throw new Exception("Duplicate label!");
            e.Add(new ILLabel() { Name = label.ToString(), OldLabel = label });
            labelStatementsPrinted.Add(label);
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
        public List<ILNode> ConvertManyStatements(Instruction start, Instruction end, Stack<ILExpression> stack)
        {
            Debug.Assert(start != null && end != null);
            List<ILNode> ret = new List<ILNode>();
            Instruction endNext = end.Next;
            while (start != null && start != endNext)
            {
                if (exitLabels != null && start.Label != null) AddLabel(start.Label, ret);
                ILExpression stmt = ConvertOneStatement(ref start, stack);
                if (stmt == null) break; // we done?  No statements?
                else ret.Add(stmt);
            }
            return ret;
        }

        public List<ILNode> DecompileInternal(Instruction start, Instruction end)
        {
            SetUpDecompiler();
            int last_pc = end.Address;
            List<ILNode> nodes = new List<ILNode>();
            ILLabel exitLabel = null;
            Dictionary<Instruction, Stack<ILExpression>> stacks = new Dictionary<Instruction, Stack<ILExpression>>();
            Instruction i = start;
            // side not, I don't think labels apper in wierd parts of the code
            while(i!=null)
            {
                Stack<ILExpression> stack;
                if (!stacks.TryGetValue(i, out stack)) stack = new Stack<ILExpression>();

                if (i.Label != null) nodes.Add(new ILLabel() { Name = i.Label.ToString(), OldLabel = i.Label });
                ILExpression stmt = ConvertOneStatement(ref i, stack);
                nodes.Add(stmt);
                if (!stmt.Code.IsUnconditionalControlFlow() && i != null && !stacks.ContainsKey(i)) stacks.Add(i, stack);

                if (stmt.Code.IsConditionalControlFlow() || stmt.Code == GMCode.B)
                {
                    ILLabel ilabel = stmt.Operand as ILLabel;
                    if (ilabel.OldLabel.Address > last_pc)
                    {
                        // its an exit label, modfy it
                        ilabel.isExit = true;
                        exitLabel = ilabel;
                    }
                    else if(!stacks.ContainsKey(ilabel.OldLabel.InstructionOrigin))  stacks.Add(ilabel.OldLabel.InstructionOrigin, new Stack<ILExpression>(stack));
                }
            }
            if(exitLabel != null)
            {
                nodes.Add(exitLabel);
                nodes.Add(new ILExpression(GMCode.Ret, null));
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
                            throw new Exception("Not sure what to do here");
                        // i = i.Next;
                        // break;
                        case GMCode.Popz:   // the call is now a statlemtn
                            Debug.Assert(stack.Peek().Code == GMCode.Call);
                            ret = stack.Pop();
                            i = i.Next;
                            break;
                        case GMCode.Pop:
                            ret = DoAssignStatment(stack, ref i);// assign statment
                            break; // it handles the next instruction
                        case GMCode.Bf:
                        case GMCode.Bt:
                        case GMCode.B: // this is where the magic happens...woooooooooo
                            ret = new ILExpression(i.Code, new ILLabel() { Name = label.ToString(), OldLabel = label });
                            if (i.Code != GMCode.B && stack.Count > 0)
                            {
                                ILExpression condition = stack.Pop();
                                ret.Arguments.Add(new ILExpression(GMCode.Push, new ILValue(condition,GM_Type.Var)));
                            }
                            i = i.Next;
                            if (branchesSeen != null) branchesSeen.Add(label);
                            break;
                        case GMCode.BadOp:
                            i = i.Next; // skip
                            break; // skip those
                        case GMCode.Pushenv:
                        case GMCode.Popenv:
                            {
                                Label temp = new Label(i.BranchDesitation);
                                ret = new ILExpression(i.Code, new ILLabel() { Name = temp.ToString(), OldLabel = temp }, stack.Pop());
                            }
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
