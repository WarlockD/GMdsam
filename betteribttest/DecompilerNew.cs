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
        AstRValue DoConstant(LinkedListNode<Instruction> node)
        {
            // evaluate a constant, we KNOW the instruction comming in is a constant
            Instruction i = node.Value;
            Debug.Assert(i.GMCode == GMCode.Push && i.FirstType != GM_Type.Var);
            AstConstant con = null;
            switch (i.FirstType)
            {
                case GM_Type.Double:
                case GM_Type.Float:
                case GM_Type.Int:
                case GM_Type.Long:
                case GM_Type.String:
                    con = new AstConstant(i, i.Operand, i.FirstType); break;
                case GM_Type.Short:
                    con = new AstConstant(i, i.Instance); break;
                default:
                    throw new Exception("Bad Type");
            }
            node.List.Remove(node); // we remove this node
            return con;
        }

        AstVar DoRValueComplex(Stack<AstClass> stack, LinkedListNode<Instruction> node) // instance is  0 and may be an array
        {
            Instruction i = node.Value;
            string var_name = i.Operand as string;
            Debug.Assert(i.Instance == 0);
            // since we know the instance is 0, we hae to look up a stack value
            Debug.Assert(stack.Count > 1);
            AstVar ret = null;
            if (i.OperandInt > 0) // if we are an array
            {
                //AstRValue indexAst = Expression(node.Previous); // the node should be removed
                AstClass index = stack.Pop();
                ret = new AstVar(i, stack.Pop(), var_name, index);
            }
            else ret = new AstVar(i, stack.Pop(), var_name);
            return ret;
        }
        AstVar DoRValueSimple(LinkedListNode<Instruction> node) // instance is != 0 or and not an array
        {
            Instruction i = node.Value;
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
        AstVar DoRValueVar(Stack<AstClass> stack, LinkedListNode<Instruction> node, bool remove_node)
        {
            Instruction i = node.Value;
            AstVar v = null;
            if (i.Instance != 0) v = DoRValueSimple(node);
            else v = DoRValueComplex(stack,node);
            if (remove_node) node.List.Remove(node); // remove this node
            return v;
        }
        AstRValue DoPush(Stack<AstClass> stack, LinkedListNode<Instruction> node)
        {
            Instruction i = node.Value;
            if (i.FirstType == GM_Type.Var) return DoRValueVar(stack,node, true);
            else return DoConstant(node);
        }
        AstCall DoCallRValue(Stack<AstClass> stack, LinkedListNode<Instruction> node)
        {
            Instruction i = node.Value;
            int arguments = i.Instance;
            AstCall call = new AstCall(i, i.Operand as string);
            for (int a = 0; a < arguments; a++) call.Children.Add(stack.Pop());
            node.List.Remove(node);
            return call;
        }
        AstStatement DoAssignStatment(Stack<AstClass> stack, LinkedListNode<Instruction> node)
        {
            Instruction i = node.Value;
            AssignStatment assign = new AssignStatment(i);
            if (stack.Count < 1) throw new Exception("Stack size issue");
            assign.Variable = DoRValueVar(stack,node, false);
            Debug.Assert(assign.Variable != null);
            assign.Expression = stack.Pop();
            node.List.Remove(node);
            return assign;
        }
        // convert into general statments and find each group of statements between labels
        KeyValuePair<Label, LinkedList<Instruction>> FindLeaf(LinkedList<Instruction> nodes)
        {
            LinkedList<Instruction> list = new LinkedList<Instruction>();
            Label l = nodes.First.Value.Label;
            nodes.First.Value.Label = null;
            while (nodes.First != null)
            {
                Instruction i = nodes.First.Value;
                if (i.Label != null) break;
                list.AddLast(i);
                nodes.RemoveFirst();
            }
            return new KeyValuePair<Label, LinkedList<Instruction>>(l, list);
        }
        StatementBlock DoBranchDecode(LinkedList<Instruction> list)
        {
            
            SortedList<Label, LinkedList<Instruction>> iblocks = new SortedList<Label, LinkedList<Instruction>>();
            if (list.First.Value.Label == null) // fix the label in case we don't have a starting label
            {
                Instruction i = list.First.Value;
                if (i.Offset != 0) throw new Exception("Expected no label on offset 0");
                i.Label = new Label(i.Offset);
            }
            while (list.First != null)
            {
                var leaf = FindLeaf(list);
                iblocks.Add(leaf.Key, leaf.Value);
            }
            Stack<AstClass> stack = new Stack<AstClass>();
            StatementBlock testAll = new StatementBlock();
            SortedList<Label, LinkedList<Instruction>> blocks = new SortedList<Label, LinkedList<Instruction>>();
            SortedList<Label, StatementBlock> resolvedblocks = new SortedList<Label, StatementBlock>();
            SortedList<Label, StatementBlock> unresolvedBlocks = new SortedList<Label, StatementBlock>();
            SortedList<Label, Stack<AstClass>> blockState = new SortedList<Label, Stack<AstClass>>();

            // we are just printing them all here


            foreach (var k in iblocks)
            {
                LinkedList<Instruction> blist = k.Value;
                Label l = k.Key;
                LabelStatement lstatement = new LabelStatement(l);
                testAll.Add(lstatement);     // we insert the label here
               
                StatementBlock block = new StatementBlock();
                foreach(var statement in DoStatements(stack,blist)) block.Add(statement);

                lstatement.block = block;
                Debug.WriteLine("Label: {0} Stack Size is = {1}", k.Key, stack.Count);
#if DEBUG
                using (StreamWriter debug_writer = new StreamWriter("temp_statement.txt"))
                {
                    testAll.DecompileToText(debug_writer);
                }
#endif
                if (stack.Count ==0)
                {
                    // its been resolved, all the statements work
                    resolvedblocks.Add(l, block);
                    blockState.Add(l, stack);
                    stack = new Stack<AstClass>();
                } else  // we have a funkey branch we have to correct or something, the stack isn't adding up.  Might be an else
                {
                    unresolvedBlocks.Add(l, block);
                    blockState.Add(l, stack);
                    stack = new Stack<AstClass>();
                    // Debug.Assert(l.CallsTo.Count == 1);
                }

            }

            return testAll;
        }
        
        StatementBlock DoStatements(Stack<AstClass> stack, LinkedList<Instruction> list)
        {
            Instruction last = null;
            StatementBlock block = new StatementBlock();
            while (list.First != null)
            {
#if DEBUG
                using (StreamWriter debug_writer = new StreamWriter("temp_statement.txt"))
                {
                    block.DecompileToText(debug_writer);
                }
#endif
                LinkedListNode<Instruction> node = list.First;
                Instruction i = node.Value;
                Debug.Assert(!object.ReferenceEquals(last, i)); // this shouldn't happen
                last = i;
                int count = i.GMCode.getOpTreeCount(); // not a leaf
                if (count != 0)
                {
                    if (count > stack.Count) throw new Exception("Stack issue");
                    AstTree ast = new AstTree(i, i.GMCode);
                    while (count-- != 0) { ast.Children.Add(stack.Pop()); }
                    stack.Push(ast);
                    node.List.Remove(node);
                }
                else
                {
                    AstStatement ret = null;
                    switch (i.GMCode)
                    {
                        case GMCode.Push:
                            stack.Push(DoPush(stack,node));
                            break;
                        case GMCode.Call:
                            stack.Push(DoCallRValue(stack, node));
                            break;
                        case GMCode.Popz:   // the call is now a statlemtn
                            node.List.Remove(node);
                            block.Add(new CallStatement(stack.Pop() as AstCall));
                            break;
                        case GMCode.Pop:
                            block.Add(DoAssignStatment(stack,node));// assign statment
                            break;
                        case GMCode.B: // this is where the magic happens...woooooooooo
                            {
                                list.Remove(node); // remove it so we don't have to worry about it
                                // be sure the stack is cleared on EACH JUMP
                                if (stack.Count > 0) foreach (var s in stack) block.Add(new PushStatement(s));
                                IfStatement ifs = ret as IfStatement;
                                if (ifs == null) ifs = new IfStatement(i); // just a goto
                                ifs.Label = i.Operand as Label;
                                block.Add(ifs);
                            }
                            break;
                        case GMCode.Bf:
                        case GMCode.Bt:
                            {
                                Debug.Assert(stack.Count > 0);
                                IfStatement ifs = new IfStatement(i);
                                ifs.Expression = i.GMCode == GMCode.Bf ? stack.Pop().Invert() : stack.Pop();
                                ret = ifs;
                            }
                            goto case GMCode.B;
                        case GMCode.BadOp:
                            node.List.Remove(node);
                            break; // skip those
                        default:
                            throw new Exception("Not Implmented! ugh");
                    }
                }
            }
            return block;
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
        public void SaveOutput(ICollection<AstStatement> statements, string filename)
        {
            using (System.IO.StreamWriter tw = new System.IO.StreamWriter(filename))
            {
                foreach (var s in statements)
                {
                    //  s.FormatHeadder(0);
                    s.DecompileToText(tw);
                    tw.WriteLine(); 
                }
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
            List<AstStatement> statements = new List<AstStatement>();
            RemoveAllConv(instructions); // mabye keep if we want to find types of globals and selfs but you can guess alot from context
            foreach (var i in instructions) statements.Add(i);
            SaveOutput(statements, scriptName + "_original.txt");
            Stack<AstClass> stack = new Stack<AstClass>();
            StatementBlock decoded = DoStatements(stack,instructions); // DoBranchDecode(instructions);


            SaveOutput(decoded, scriptName + "_forreals.txt");
        }
    }
}
