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
        string FindInstance(int index)
        {
            Debug.Assert(index > 0);
            if (InstanceList != null && index < InstanceList.Count) return InstanceList[index];
            else return "Object(" + index + ")";
        }
        class VarInfo
        {
            public HashSet<string> ScriptsSeenAssignedIn = new HashSet<string>();
            public HashSet<string> ScriptesAccessedFrom = new HashSet<string>();
            public void SeenInScript(string scriptName)
            {
                ScriptesAccessedFrom.Add(scriptName);
            }
            public void AssignedInScript(string scriptName)
            {
                SeenInScript(scriptName);
                ScriptsSeenAssignedIn.Add(scriptName);
            }
            public string Name { get; private set; }
            public string Instance { get; private set; }
            public bool IsArray { get; private set; }
            int _index;
            public int MaxArraySizeSeen
            {
                get { return _index; }
                set
                {
                    if (!IsArray) throw new Exception("NOT AN ARRAY");
                    if (value > _index) _index = value;
                }
            }
            public HashSet<string> ScriptsAssignedIn = new HashSet<string>();
            public HashSet<string> ScriptsReadIn = new HashSet<string>();
            public VarInfo(string name, string instance) { Name = name; Instance = instance; IsArray = false; _index = 0; }
            public VarInfo(string name, string instance, int index) { Name = name; Instance = instance; IsArray = true; _index = index; }
            public override string ToString()
            {
                if (IsArray) return Instance + "." + Name + '(' + MaxArraySizeSeen + ')'; else return Instance + "." + Name;
            }
        }
        Dictionary<string, VarInfo> GlobalsInFilenames = new Dictionary<string, VarInfo>();
        public void SawGlobal(string var_name)
        {
            VarInfo info;
            if (!GlobalsInFilenames.TryGetValue(var_name, out info)) GlobalsInFilenames[var_name] = info = new VarInfo(var_name, "global");
            info.SeenInScript(ScriptName);
        }
        public void SawGlobal(string var_name, int index)
        {
            VarInfo info;
            if (!GlobalsInFilenames.TryGetValue(var_name, out info)) GlobalsInFilenames[var_name] = info = new VarInfo(var_name, "global", index);
            info.SeenInScript(ScriptName);
            info.MaxArraySizeSeen = index;
        }
        public void AssignedGlobal(string var_name, int index)
        {
            VarInfo info;
            if (!GlobalsInFilenames.TryGetValue(var_name, out info)) GlobalsInFilenames[var_name] = info = new VarInfo(var_name, "global", index);
            info.SeenInScript(ScriptName);
            info.MaxArraySizeSeen = index;
        }
        public void AssignedGlobal(string var_name)
        {
            VarInfo info;
            if (!GlobalsInFilenames.TryGetValue(var_name, out info)) GlobalsInFilenames[var_name] = info = new VarInfo(var_name, "global");
            info.SeenInScript(ScriptName);
        }

        // I could just pass a generic stream but if I am using BinaryReader eveywhere, might as well pass it
        static bool vPushIsStackAndArray(Instruction x) { return x.GMCode == GMCode.Push && x.FirstType == GM_Type.Var && x.Instance == 0 && GMCodeUtil.IsArrayPushPop(x.OpCode); }

        static bool ePushPredicate(Instruction x) { return x.GMCode == GMCode.Push && x.FirstType == GM_Type.Short; }
        static bool ePushPredicateValue(Instruction x, int v) { return x.GMCode == GMCode.Push && x.FirstType == GM_Type.Short && x.Instance == v; }
        string getConditionalString(Instruction i, bool invert = false)
        {
            switch (i.GMCode)
            {
                case GMCode.Seq: return invert ? "!=" : "==";
                case GMCode.Sne: return invert ? "==" : "!=";
                case GMCode.Sge: return invert ? "<" : ">=";
                case GMCode.Sle: return invert ? ">" : "<=";
                case GMCode.Sgt: return invert ? "<=" : ">";
                case GMCode.Slt: return invert ? ">=" : "<";
            }
            throw new Exception("something");
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
            if (i.OperandInt > 0) // if we are an array
            {
                //AstRValue indexAst = Expression(node.Previous); // the node should be removed
                Ast index = stack.Pop();
                ret = new AstVar(i, stack.Pop(), var_name, index);
            }
            else ret = new AstVar(i, stack.Pop(), var_name);
            return ret;
        }
        AstVar DoRValueSimple(Instruction i) // instance is != 0 or and not an array
        {
            Debug.Assert(i.Instance != 0);
            AstVar v = null;
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
        // branch search checks the branches to see if we have an issue
        AstStatement BranchSearch(Instruction i, Stack<Ast> stack, ref LinkedListNode<Instruction> node)
        {
            var list = node.List; // branch
            Label l = i.Operand as Label;
            AstStatement ret = null;
            if (stack.Count > 0) // something is wierd, lets see if we can resolve it
            {
                if (i.GMCode == GMCode.B)// something is wierd, lets see if we can resolve it
                {
                    var target = FindLabel(node, l);
                    Instruction ti = target.Value;
                    if (ti.GMCode.IsConditional())
                    {
                        // lets consume this stack and change it
                        ret = new IfStatement(i);
                        ret.Add(ti.GMCode == GMCode.Bf ? stack.Pop().Invert() : stack.Pop());
                        ret.Add(new GotoStatement(ti));
                    }
                    else throw new Exception("Patern we havn't seend before");
                } else
                {
                    Ast condition = stack.Pop();
                    int value;
                    if(condition.TryParse(out value))
                    {
                        // ok, this is bad some reason we have a constant for a jump, change it to a gotostatment
                        if(value != 0)
                        {
                            if ((value == 0 && i.GMCode == GMCode.Bf) || (value != 0 && i.GMCode == GMCode.Bt)) ret = new GotoStatement(i);
                            else new CommentStatement(i, "A value that is not 0 has a branch");// we don't want to add a statment here but sure as hell log it
                        }
                        
                    }
                    ret = new IfStatement(i);
                    ret.Add(i.GMCode == GMCode.Bf ? condition.Invert() : condition);
                    ret.Add(new GotoStatement(l));
 
                }
            } else
            {
                if(i.GMCode == GMCode.B) // normal branch
                {
                    ret = new GotoStatement(i);

                } else
                {
                    throw new Exception("No expression for an iff?");
                }
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
                if(i.Label != null) block.Add(new LabelStatement(i.Label)); // we add this so we know where they are, if that makes sense
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
                            stack.Push(DoPush(stack,ref node));
                            break;
                        case GMCode.Call:
                            stack.Push(DoCallRValue(stack, ref node));
                            break;
                        case GMCode.Popz:   // the call is now a statlemtn
                            node = node.Next;
                            block.Add(new CallStatement(i,stack.Pop() as AstCall));
                            break;
                        case GMCode.Pop:
                            block.Add(DoAssignStatment(stack,ref node));// assign statment
                            break;
                        case GMCode.B: // this is where the magic happens...woooooooooo
                        case GMCode.Bf:
                        case GMCode.Bt:
                            block.Add(BranchSearch(i, stack, ref node));
                            break;
                        case GMCode.BadOp:
                            node = node.Next; // skip
                            break; // skip those
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
                    for (; i < block.Count && ls == null; ++i, ls = block[i] as LabelStatement) statements.Add(block[i]);

                    if (ls.Target == gs.Target)
                    {
                        ls.Remove(); // remove the label, not needed anymore
                        if (statements.Count == 0) throw new Exception("Nothing? ugh");
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

        }
        public StatementBlock TryToDetectIfStatmentBlocks(StatementBlock block)
        {
            StatementBlock ret = new StatementBlock();
            HashSet<Label> labelsToRemove = new HashSet<Label>();
            LabelStatement labelStatement = null;
            IfStatement ifs = null;
            for (int i=0;i< block.Count;i++) { 
                
               
                ifs = block[i] as IfStatement;
                if (ifs != null)
                {
                    Debug.Assert(ifs.Then is GotoStatement);
                    Label target = (ifs.Then as GotoStatement).Target;
                    if (ifs.Instruction.Offset < target.Target && target.CallsTo.Count == 1 && ifs.Condition != null) // we have an if statment, is the label forwarding and only one call
                    {  // then this is a block statment, that simple to decode
                        
                        int index = block.IndexOfLabelStatement(target);
                        if (index > 0)
                        {
                            StatementBlock testBlock = new StatementBlock();
                            labelStatement = block[index] as LabelStatement;
                            labelsToRemove.Add(labelStatement.Target);
                            for (int j = i + 1; j < index; j++) testBlock.Add(block[j].Copy());
                            IfStatement nifs = new IfStatement(ifs.Instruction);
                            nifs.Add(ifs.Condition.Invert());
                            nifs.Add(TryToDetectIfStatmentBlocks(testBlock));// lets see if this works!
                            ret.Add(nifs);
                        }
                    }
                    else ret.Add(ifs); // else ignore
                    continue;
                }
                labelStatement = block[i] as LabelStatement;
                if (labelStatement != null)
                {
                    Label target = labelStatement.Target;
                    if (labelsToRemove.Contains(target)) continue;
                }
                ret.Add(block[i].Copy());
            }
            return ret;
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
        public void Disasemble(string scriptName, BinaryReader r, List<string> StringIndex)
        {
            // test
            this.ScriptName = scriptName;
            this.StringIndex = StringIndex;
            var instructions = Instruction.Create(r, StringIndex);
           
            RemoveAllConv(instructions); // mabye keep if we want to find types of globals and selfs but you can guess alot from context
                                         // foreach (var i in instructions) statements.Add(i);

            
            
            SaveOutput(instructions, scriptName + "_original.txt");
          //  DFS dfs = new DFS(instructions);
          //  dfs.CreateDFS();




            Stack<Ast> stack = new Stack<Ast>();
            StatementBlock decoded = DoStatements(stack,instructions); // DoBranchDecode(instructions);

          //  StatementBlock ifs_fixed = TryToDetectIfStatmentBlocks(decoded);
         //   SaveOutput(ifs_fixed, scriptName + "_forreals.txt");
            TryToDetectVerySimpleIfBlocks(decoded);
            SaveOutput(decoded, scriptName + "_forreals.txt");
            FillInGotoLabelMarkers(decoded, true);
        }
    }
}
