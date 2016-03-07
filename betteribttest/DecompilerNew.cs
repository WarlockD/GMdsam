using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Collections;
using Antlr4;

namespace betteribttest
{

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
        Ast DoRValueComplex(Stack<Ast> stack, Instruction i) // instance is  0 and may be an array
        {
            string var_name = i.Operand as string;
            Debug.Assert(i.Instance == 0);
            // since we know the instance is 0, we hae to look up a stack value
            Debug.Assert(stack.Count > 1);
            AstVar ret = null;
            Ast index = i.OperandInt > 0 ? stack.Pop() : null;// if we are an array
            Ast objectInstance = stack.Pop();
            int value;
            if (objectInstance.TryParse(out value))
                ret = new AstVar(i, value, GMCodeUtil.lookupInstance(value, InstanceList), var_name);
            else
                throw new Exception("Have some instance issue");
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
                return new AstVar(i, i.Instance, GMCodeUtil.lookupInstance(i.Instance, InstanceList), i.Operand as string);
            }
            // Here is where it gets wierd.  iOperandInt could have two valus 
            // I think 0xA0000000 means its local and
            // | 0x4000000 means something too, not sure  however I am 50% sure it means its an object local? Room local, something fuky goes on with the object ids at this point
            if ((i.OperandInt & 0xA0000000) != 0) // this is always true here
            {

                if ((i.OperandInt & 0x40000000) != 0)
                {
                    // Debug.Assert(i.Instance != -1); // built in self?
                    v = new AstVar(i, i.Instance, GMCodeUtil.lookupInstance(i.Instance, InstanceList), i.Operand as string);
                }
                else {
                    v = new AstVar(i, i.Instance, GMCodeUtil.lookupInstance(i.Instance, InstanceList), i.Operand as string);
                    //v = new AstVar(i, i.Instance, i.Operand as string);
                }
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
        Ast DoPush(Stack<Ast> stack, ref Instruction node)
        {
            Instruction i = node;
            Ast ret = null;
            if (i.FirstType == GM_Type.Var) ret = DoRValueVar(stack, i);
            else ret = AstConstant.FromInstruction(i);
            node = node.Next;
            return ret;
        }
        AstCall DoCallRValue(Stack<Ast> stack, ref Instruction node)
        {
            Instruction i = node;
            int arguments = i.Instance;
            List<Ast> args = new List<Ast>();
            for (int a = 0; a < arguments; a++) args.Add(stack.Pop());
            AstCall call = new AstCall(i, i.Operand as string, args);
            node = node.Next;
            return call;
        }
        AstStatement DoAssignStatment(Stack<Ast> stack, ref Instruction node)
        {
            Instruction i = node;
            if (stack.Count < 1) throw new Exception("Stack size issue");
            Ast v = DoRValueVar(stack, i);
            Ast value = stack.Pop();
            AssignStatment assign = new AssignStatment(i, v, value);
            node = node.Next;
            return assign;
        }
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
  

        public StatementBlock DoStatements(Stack<Ast> stack, Instruction start, Instruction end)
        {
            StatementBlock block = new StatementBlock();
            Instruction i = start;
            while (i != null)
            {
                
                if (i.Label != null)
                {
                    if (!ignoreLabels.Contains(i.Label))
                        block.Add(new LabelStatement(i.Label)); // we add this so we know where they are, if that makes sense
                    else
                        block.Add(new CommentStatement(i,"Ignoreing this label: " + i.Label.ToString())); // we add this so we know where they are, if that makes sense
                }
                int count = i.GMCode.getOpTreeCount(); // not a leaf
                if (count == 2)
                {
                    if (count > stack.Count) throw new Exception("Stack issue");
                    AstTree ast = new AstTree(i, i.GMCode, stack.Pop(), stack.Pop());
                    stack.Push(ast);
                     i= i.Next;
                }
                else
                {
                    switch (i.GMCode)
                    {
                        case GMCode.Conv:
                            i = i.Next; // ignore
                            break;
                        case GMCode.Not:
                            stack.Push(new AstNot(i, stack.Pop()));
                            break;
                        case GMCode.Neg:
                            stack.Push(new AstNegate(i, stack.Pop()));
                            break;
                        case GMCode.Push:
                            stack.Push(DoPush(stack, ref i));
                            break;
                        case GMCode.Call:
                            stack.Push(DoCallRValue(stack, ref i));
                            break;
                        case GMCode.Popz:   // the call is now a statlemtn
                            i = i.Next;
                            block.Add(new CallStatement(i, stack.Pop() as AstCall));
                            break;
                        case GMCode.Pop:
                            block.Add(DoAssignStatment(stack, ref i));// assign statment
                            break;
                        case GMCode.B: // this is where the magic happens...woooooooooo
                            block.Add(new GotoStatement(i));
                            Debug.Assert(i == end);
                            return block;
                        case GMCode.Bf:
                        case GMCode.Bt:
                            {
                                Ast condition = stack.Pop();
                                block.Add(new IfStatement(i, i.GMCode == GMCode.Bf ? condition.Invert() : condition, i.Operand as Label));
                            }
                            
                            Debug.Assert(i == end);
                            return block;
                        case GMCode.BadOp:
                            i = i.Next; // skip
                            break; // skip those
                        case GMCode.Exit:
                            block.Add(new ExitStatement(i));
                            i = i.Next;
                            break;
                        default:
                            throw new Exception("Not Implmented! ugh");
                    }
                }
                if (i == null || i == end.Next)
                    break;
            }

            return block;
        }
        /// <summary>
        /// If we goto an ifstatment
        /// </summary>
        /// <param name="block"></param>
        public IfStatement CheckConstantJump(StatementBlock block)
        {
            return null;
        }
        public void FillInGotoLabelMarkers(StatementBlock block, bool remove_unused = false)
        {
            List<GotoStatement> gotoStatements = block.FindType<GotoStatement>(true).ToList();
            List<LabelStatement> labelStatements = block.FindType<LabelStatement>(true).ToList();
            foreach (var ls in labelStatements) ls.CallsHere = new List<GotoStatement>();
            Dictionary<Label, LabelStatement> lookupLabelStatement = labelStatements.ToDictionary(o => o.Target);
            foreach (var g in gotoStatements)
            {
                LabelStatement ls = lookupLabelStatement[g.Target];
                ls.CallsHere.Add(g);
                g.LabelLinkTo = ls;
            }
            if (remove_unused) foreach (var ls in labelStatements) block.Remove(ls);
        }


        public void RemoveAllConv(Instruction.Instructions instructions)
        {
#if DEBUG
            // if we got a label on a conv, something is funky
            var e = instructions.Where(x => x.GMCode == GMCode.Conv && x.Label != null).ToList();
            Debug.Assert(e != null);
#endif
            instructions.RemoveAll(x => x.GMCode == GMCode.Conv);
        }
        public void SaveOutput(ITextOut ITextOutObject, string filename)
        {
            using (System.IO.StreamWriter tw = new System.IO.StreamWriter(filename))
            {
                ITextOutObject.WriteTextLine(tw);
            }
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
             RemoveAllConv(instructions); // mabye keep if we want to find types of globals and selfs but you can guess alot from context
                                         // foreach (var i in instructions) statements.Add(i);

            
            
            SaveOutput(instructions, scriptName + "_original.txt");
            //  DFS dfs = new DFS(instructions);
            //  dfs.CreateDFS();

            BasicBlocks basic = new BasicBlocks(instructions,this);

            // var testi=  AstInstruction.DoThisBackwards(instructions.First);
   
            SaveOutput(basic.EntryBlock.AstBlock, scriptName + "_forreals.txt");
      
            Stack<Ast> stack = new Stack<Ast>();
           // StatementBlock decoded = DoStatements(stack,instructions); // DoBranchDecode(instructions);

          
            //FillInGotoLabelMarkers(decoded, true);
            //  StatementBlock ifs_fixed = TryToDetectIfStatmentBlocks(decoded);
            //   SaveOutput(ifs_fixed, scriptName + "_forreals.txt");
         //   SaveOutput(decoded, scriptName + "_forreals.txt");
            //  TryToDetectIfStatmentBlocks(decoded);
          //  LinkAllGotosAndStatements visitor = new LinkAllGotosAndStatements();
          //  visitor.Run(decoded);
         //     SaveOutput(decoded, scriptName + "_forreals.txt");
        //    FillInGotoLabelMarkers(decoded, true);
          //  SaveOutput(decoded, scriptName + "_forreals.txt");
        }
    }
}
