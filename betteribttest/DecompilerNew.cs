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
   
    public class DecompilerNew
    {
     
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

        AstVar DoRValueComplex(Stack<Ast> stack, Instruction i) // instance is  0 and may be an array
        {
            string var_name = i.Operand as string;
            Debug.Assert(i.Instance == 0);
            // since we know the instance is 0, we hae to look up a stack value
            Debug.Assert(stack.Count > 1);
            AstVar ret = null;
            Ast index = i.OperandInt > 0?stack.Pop() : null;// if we are an array
            Ast objectInstance = stack.Pop();
            int value;
            if (InstanceList != null && objectInstance.TryParse(out value))
                ret = new AstVar(i, new AstConstant(InstanceList[value]), var_name, index);
            else
                ret = new AstVar(i, objectInstance, var_name, index);
            return ret;
        }
        AstVar DoRValueSimple(Instruction i) // instance is != 0 or and not an array
        {
            Debug.Assert(i.Instance != 0);
            AstVar v = null;
            if(InstanceList != null && i.Instance > 0) // we are screwing with a specifice object
            {
                return new AstVar(i, new AstConstant(InstanceList[i.Instance]), i.Operand as string);
            }
            // Here is where it gets wierd.  iOperandInt could have two valus 
            // I think 0xA0000000 means its local and
            // | 0x4000000 means something too, not sure  however I am 50% sure it means its an object local? Room local, something fuky goes on with the object ids at this point
            if ((i.OperandInt & 0xA0000000) != 0) // this is always true here
            {

                if ((i.OperandInt & 0x40000000) != 0)
                {
                    // Debug.Assert(i.Instance != -1); // built in self?
                    v = new AstVar(i, i.Instance, i.Operand as string);
                }
                else {
                    v = new AstVar(i, i.Instance, i.Operand as string);
                }
                return v;
            }
            else throw new Exception("UGH check this");
        }
        AstVar DoRValueVar(Stack<Ast> stack, Instruction i)
        {
            AstVar v = null;
            if (i.Instance != 0) v = DoRValueSimple(i);
            else v = DoRValueComplex(stack,i);
            return v;
        }
        Ast DoPush(Stack<Ast> stack, ref LinkedListNode<Instruction> node)
        {
            Instruction i = node.Value;
            Ast ret = null;
            if (i.FirstType == GM_Type.Var) ret= DoRValueVar(stack,i);
            else ret= AstConstant.FromInstruction(i);
            node = node.Next;
            return ret;
        }
        AstCall DoCallRValue(Stack<Ast> stack, ref LinkedListNode<Instruction> node)
        {
            Instruction i = node.Value;
            int arguments = i.Instance;
            AstCall call = new AstCall(i, i.Operand as string);
            for (int a = 0; a < arguments; a++) call.Add(stack.Pop());
            node = node.Next;
            return call;
        }
        AstStatement DoAssignStatment(Stack<Ast> stack, ref LinkedListNode<Instruction> node)
        {
            Instruction i = node.Value;
            AssignStatment assign = new AssignStatment(i);
            if (stack.Count < 1) throw new Exception("Stack size issue");
            assign.Add(DoRValueVar(stack, i));
            assign.Add(stack.Pop());
            node = node.Next;
            return assign;
        }
        // convert into general statments and find each group of statements between labels
       
        LinkedListNode<Instruction> FindLabel(LinkedListNode<Instruction> node, Label l)
        {
            var list = node.List;
            var start = list.First;
            while(start != null)
            {
                if (start.Value.Label == l) return start;
                start = start.Next;
            }
            throw new Exception("Can't find the label!");
        }
        // makes a statment blocks that fixes the stack before the call
        // Emulation is starting to look REALLY good about now
        AstStatement EmptyStack(Stack<Ast> stack,  AstStatement statment, int top=0)
        {
            if (stack.Count > top)
            {
                var items = stack.ToArray();
                StatementBlock block = new StatementBlock();
                block.Add(new CommentStatement("Had to fix the stack"));
                for (int i = top; i < items.Length; i++) block.Add(new PushStatement(items[i].Copy()));
                block.Add(statment);
                return block;
            }
            else return statment;
        }
        // branch search checks the branches to see if we have an issue
        AstStatement BranchSearch(Instruction i, Stack<Ast> stack, ref LinkedListNode<Instruction> node)
        {
            var list = node.List; // branch
            Label l = i.Operand as Label;
            AstStatement ret = null;
            switch (i.GMCode)
            {
                case GMCode.B:
                    if (stack.Count > 0) // might be an && or a ||
                    {
                        if (l.CallsTo.Count == 1) // simple case, 
                        {
                            ignoreLabels.Add(l); // we are handling this branch statment so we don't need a LabelStatement
                            Instruction ti = l.InstructionOrigin.Value;

                            if (ti.GMCode.IsConditional())
                            {
                                // lets consume this stack and change it
                                ret = new IfStatement(i);
                                ret.Add(ti.GMCode == GMCode.Bf ? stack.Pop().Invert() : stack.Pop());
                                ret.Add(EmptyStack(stack, new GotoStatement(ti)));
                                break;
                            }
                            else throw new Exception("Patern we havn't seend before");
                        }
                        else
                        {
                            ret = EmptyStack(stack, new GotoStatement(i));
                        }
                    }
                    else // normal goto, and resolved stack
                    {
                        ret = new GotoStatement(i);
                    }
                    break;
                case GMCode.Bf:
                case GMCode.Bt:
                    if (stack.Count > 0){ 
                        /* 
                            Need to test for this wierd condtion that crops up.
                            somewhere B L1
                            somewhere Bf L0
                            L0 push.E
                            l1 bf L50
                            When we have anything that goes to L0, we need the label that the bf uses
                            and anything that goes to l1, there must be something on the stack we can use
                            This appers to only happen on forward if statments so we should be good removeing them
                        */
                        Ast condition = stack.Pop();
                        // so first, lets get the target


                        if (condition is AstConstant) // Another wierd statment
                        {
                            int value;
                            Debug.Assert(condition.TryParse(out value)); // this should work
                            Debug.Assert(i.GMCode == GMCode.Bf); // normaly this is a push.e 0 and a bf label, if not..er humm.
                            ret = EmptyStack(stack, new GotoStatement(i));
                        }
                        else {
                            var target = l.InstructionOrigin;
                            if (target.Value.GMCode == GMCode.Push && target.Value.Instance == 0)
                            {
                                // ok, now we test the next one
                                var next_instruction = target.Next;
                                if (next_instruction.Value.GMCode == GMCode.Bf)
                                {
                                    // we have a winner!
                                    ignoreLabels.Add(l); // kill the label
                                    ret = new IfStatement(i);
                                    ret.Add(condition.Invert());
                                    ret.Add(EmptyStack(stack, new GotoStatement((next_instruction.Value))));
                                    break;
                                }

                            }
                            // eveything is normal, nothing to see here
                            ret = new IfStatement(i);
                            ret.Add(i.GMCode == GMCode.Bf ? condition.Invert() : condition);
                            ret.Add(EmptyStack(stack, new GotoStatement(i)));
                        } 
                    }
                    else throw new Exception("No expression for an iff?");
                    break;
                default:
                    throw new Exception("Bad op in branch search");
            }
            Debug.Assert(ret != null);
            node = node.Next;
            return ret;
        }

StatementBlock DoStatements(Stack<Ast> stack, LinkedList<Instruction> list)
        {
            Instruction last = null;
            StatementBlock block = new StatementBlock();
            LinkedListNode<Instruction> node = list.First;
            while (node != null)
            {
#if DEBUG
                block.SaveToFile("temp_statement.txt");
#endif
                Instruction i = node.Value;
                Debug.Assert(!object.ReferenceEquals(last, i)); // this shouldn't happen
                last = i;
                if (i.Label != null)
                {
                    if (!ignoreLabels.Contains(i.Label))
                        block.Add(new LabelStatement(i.Label)); // we add this so we know where they are, if that makes sense
                }
                int count = i.GMCode.getOpTreeCount(); // not a leaf
                if (count != 0)
                {
                    if (count > stack.Count) throw new Exception("Stack issue");
                    AstTree ast = new AstTree(i, i.GMCode);
                    while (count-- != 0) { ast.Add(stack.Pop()); }
                    stack.Push(ast);
                    node = node.Next;
                }
                else
                {
                    switch (i.GMCode)
                    {
                        case GMCode.Push:
                            stack.Push(DoPush(stack, ref node));
                            break;
                        case GMCode.Call:
                            stack.Push(DoCallRValue(stack, ref node));
                            break;
                        case GMCode.Popz:   // the call is now a statlemtn
                            node = node.Next;
                            block.Add(new CallStatement(i, stack.Pop() as AstCall));
                            break;
                        case GMCode.Pop:
                            block.Add(DoAssignStatment(stack, ref node));// assign statment
                            break;
                        case GMCode.B: // this is where the magic happens...woooooooooo
                        case GMCode.Bf:
                        case GMCode.Bt:
                            block.Add(BranchSearch(i, stack, ref node));
                            break;
                        case GMCode.BadOp:
                            node = node.Next; // skip
                            break; // skip those
                        case GMCode.Exit:
                            block.Add(new ExitStatement(i));
                            node = node.Next;
                            break;
                        default:
                            throw new Exception("Not Implmented! ugh");
                    }
                }
            }
#if DEBUG
            block.SaveToFile("temp_statement.txt");
#endif
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
            if (remove_unused) foreach (var ls in labelStatements)
                {
                    if (ls.CallsHere.Count == 0) ls.Remove();
                }
        }


        // We try to detect any if blocks that are blatently simple
        // like if(x>4) x+3;   No else blocks, loops or anything else
        // so its only if statments that have no other branchs inside of them
        public void TryToDetectVerySimpleIfBlocks(StatementBlock block)
        {
            FillInGotoLabelMarkers(block);
            List<IfStatement> ifstatements = block.FindType<IfStatement>(false).ToList();
            foreach (var ifs in ifstatements)
            {
                GotoStatement gs = ifs.Then as GotoStatement;
                if (gs != null && gs.LabelLinkTo.CallsHere.Count == 1) // simple call only one branch
                {
                    List<Ast> statements = new List<Ast>();
                    int i = ifs.ParentIndex + 1;
                    LabelStatement ls = block[i] as LabelStatement;
                    for (; i < block.Count; ++i)
                    {
                        ls = block[i] as LabelStatement;
                        if (ls != null) break;
                        statements.Add(block[i]);
                    }
                    if (ls.Target != gs.Target) continue; // found incorrect label statment, so found a bad if
                    if (statements.ContainsType<GotoStatement>()) continue; // we have a goto statment somewhere so not a simple if

                    Debug.Assert(statements.Count != 0);
                    ls.Remove(); // remove the label, not needed anymore
                    ifs[0] = ifs[0].Invert(); // change the conditional
                    if (statements.Count == 1)
                    {
                        statements[0].Remove();
                        ifs[1] = statements[0];
                    }
                    else ifs[1] = new StatementBlock(statements, false);
                }
            }

        }
        public StatementBlock GetBlockToLabel(StatementBlock block, int start, Label target, out LabelStatement ls)
        {
            Debug.Assert(block != null);
            ls = null;
            List<Ast> toRemove = new List<Ast>();
                
            for (int j = start; j < block.Count; j++)
            {
                ls = block[j] as LabelStatement;
                if (ls != null)
                {
                    if (ls.Target != target) return null; // another target is in here
                    else {
                        StatementBlock testBlock = new StatementBlock();
                        foreach (var o in toRemove)
                        {
                            block.Remove(o);
                            testBlock.Add(o);
                        }
                        return testBlock;
                    }
                }
                toRemove.Add(block[j]);
            }
            return null;
        }
        public void TryToDetectIfStatmentBlocks(StatementBlock block)
        {
            StatementBlock ret = new StatementBlock();

            LabelStatement labelStatement = null;
            IfStatement ifs = null;
            for (int i = 0; i < block.Count; i++)
            {
                ifs = block[i] as IfStatement;
                if (ifs != null)
                {
                    GotoStatement gos = ifs.Then as GotoStatement;
                    if (gos == null) continue;
                    Label target = gos.Target;
                    StatementBlock newBlock = GetBlockToLabel(block, ifs.ParentIndex + 1, target, out labelStatement);
                    if (newBlock == null) continue; // nope
                    if (newBlock.HasType<GotoStatement>()) continue; // Not handling any aditional gotos here
                    ignoreLabels.Add(target); // don't print this label
                    if (labelStatement.CallsHere.Contains(gos)) labelStatement.Remove(gos);
                    else throw new Exception("It SHOULD be in here");
                    if (labelStatement.CallsHere.Count == 0) block.Remove(labelStatement);
                    IfStatement nifs = new IfStatement(ifs.Instruction);
                    nifs.Add(ifs.Condition.Invert());
                    nifs.Add(newBlock);// lets see if this works!
                    block[ifs.ParentIndex] = nifs;
                }
            }
        }
        // This pattern should so up on simple and's that were pre processed 
        public void CheckForAndPatterns(StatementBlock block)
        {
            IfStatement if0, if1;
            GotoStatement go0, if0_go0, if1_go1;
            for (int i=0;i < block.Count-2;i++)
            {
                if0 = block[i] as IfStatement;
                if1 = block[i+1] as IfStatement;
                go0 = block[i+2] as GotoStatement;
                if (if0 != null && if1 != null && go0 != null) // we got a start
                {
                    if0_go0 = if0.Then as GotoStatement;
                    if1_go1 = if1.Then as GotoStatement;
                    if (if0_go0 == null && if1_go1 == null) continue; // nope
                    Label l = go0.Target;
                    if (if0_go0.Target != l && if1_go1.Target != l) continue; // nope again
                    // IF exp AND exp statment is verfied
                    int index = i;
                    block.Remove(if0);
                    block.Remove(if1);
                    block.Remove(go0);

                }
            }
        }
        public void RemoveUnusedLabels(StatementBlock block)
        {
            List<LabelStatement> labelStatements = block.FindType<LabelStatement>().ToList();
            Dictionary<Label, HashSet<GotoStatement>> callsTo = new Dictionary<Label, HashSet<GotoStatement>>();
            foreach(var labelStatement in labelStatements)
            {
                callsTo.Add(labelStatement.Target, new HashSet<GotoStatement>());
            }
            foreach (var gotoStatement in block.FindType<GotoStatement>())
            {
                if (!callsTo[gotoStatement.Target].Add(gotoStatement)) throw new Exception("How this happen");
            }
            // we can do some block checking or whatever but right now kill all the stupid extra labels
            foreach(var ls in labelStatements) if (callsTo[ls.Target].Count == 0) ls.Remove();
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




            Stack<Ast> stack = new Stack<Ast>();
            StatementBlock decoded = DoStatements(stack,instructions); // DoBranchDecode(instructions);
            FillInGotoLabelMarkers(decoded, true);
            //  StatementBlock ifs_fixed = TryToDetectIfStatmentBlocks(decoded);
            //   SaveOutput(ifs_fixed, scriptName + "_forreals.txt");
            SaveOutput(decoded, scriptName + "_forreals.txt");
            TryToDetectIfStatmentBlocks(decoded);
            SaveOutput(decoded, scriptName + "_forreals.txt");
            FillInGotoLabelMarkers(decoded, true);
            SaveOutput(decoded, scriptName + "_forreals.txt");
        }
    }
}
