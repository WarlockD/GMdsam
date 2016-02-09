using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web.UI;
using System.Web;
using System.Runtime.InteropServices;
/*
    So why SortedList?  When using the peekhole optimiazer (or deoptimizer?) We will need to get the next instruction 
    rather than the next pc, while OTHEr parts of the decompiler needs the next pc
*/

namespace betteribttest
{
    using Opcode = GM_Disam.Opcode;
    class Disam
    {
        Opcode o;
        ChunkReader cr;
        public static Dictionary<int, string> instanceLookup = new Dictionary<int, string>()
        {
            {  0 , "stack" },
            {  -1, "self" },
            {  -2, "other" },
            {  -3, "all" },
            {  -4, "noone" },
            {  -5, "global" },
        };
        static string lookupInstance(int instance)
        {
            string ret;
            if (Disam.instanceLookup.TryGetValue(instance, out ret)) return ret;
            else return String.Format("Instance({0})", instance);
        }
      
       
        static Dictionary<OpType, string> opMathOperation = new Dictionary<OpType, string>()  {
            {  (OpType)0x03, "conv" },
            {  (OpType)0x04, "*" },
            {  (OpType)0x05, "/" },
            { (OpType) 0x06, "rem" },
            {  (OpType)0x07, "%" },
            {  (OpType)0x08, "+" },
            {  (OpType)0x09, "-" },
            {  (OpType)0x0a, "&" },
            {  (OpType)0x0b, "|" },
            { (OpType) 0x0c, "^" },
            { (OpType) 0x0d, "~" },
            {  (OpType)0x0e, "!" },

            {  (OpType)0x0f, "<<" },
            {  (OpType)0x10, ">>" },
            { (OpType) 0x11, "<" },
            { (OpType) 0x12, "<=" },
            { (OpType) 0x13, "==" },
            {  (OpType)0x14, "!=" },
            {  (OpType)0x15, ">=" },
            {  (OpType)0x16, ">" },
        };
        static Dictionary<OpType, OpType> invertOpType = new Dictionary<OpType, OpType>()
        {
            { OpType.Slt, OpType.Sge },
            { OpType.Sge, OpType.Slt },
            { OpType.Sle, OpType.Sgt },
            { OpType.Sgt, OpType.Sle },
            { OpType.Sne, OpType.Seq },
            { OpType.Seq, OpType.Sne },
        };

        public Disam(ChunkReader cr)
        {
            this.cr = cr;
        }
     
        public string TestStreamOutput(string code_name)
        {
            foreach (GMK_Code c in cr.codeList)
            {
                if (c.Name.IndexOf(code_name) != -1)
                {
                    System.Diagnostics.Debug.WriteLine("Processing script {0}", c.Name);
                    //System.Diagnostics.Debug.Assert("gml_Object_obj_finalfroggit_Alarm_6" != c.Name);
                    ChunkStream ms = cr.getReturnStream();
                    var lines = processStream2(ms, c.startPosition, c.size);

                    StreamWriter s = new StreamWriter(c.Name + ".txt");
                    foreach (var line in lines)
                    {
                        string text = line.DecompileLine();
                        if (!string.IsNullOrEmpty(text)) s.WriteLine(text);
                    }
                    s.Close();
                }
            }
            return null;
        }
        public void writeFile(string code_name)
        {
            bool found = false;
            foreach (GMK_Code c in cr.codeList)
            {
                if (c.Name.IndexOf(code_name) != -1)
                {

                    //  writeHTMLFile(c.Name);
                }
            }
            if (!found) throw new Exception(code_name + " not found");
        }

      




        class AST
        {
            public GM_Type Type { get; private set; }
            public AST(GM_Type type) { this.Type = type; }
        }
        class Variable : AST
        {
            double dvalue;
            long ivalue;
            string svalue;
            public Variable Dup()
            {
                return (Variable)this.MemberwiseClone();
            }
            public string Value { get; private set; }
            public double DValue { get { return dvalue; } }
            public double IValue { get { return ivalue; } }
            public Variable(int value) : base(GM_Type.Int) { ivalue = value; Value = value.ToString(); }
            public Variable(long value) : base(GM_Type.Long) { ivalue = value; Value = value.ToString(); }
            public Variable(ushort value) : base(GM_Type.Short) { ivalue = value; Value = value.ToString(); }
            public Variable(float value) : base(GM_Type.Float) { dvalue = value; Value = value.ToString(); }
            public Variable(double value) : base(GM_Type.Double) { dvalue = value; Value = value.ToString(); }
            public Variable(bool value) : base(GM_Type.Bool) { ivalue = value ? 1 : 0; Value = value.ToString(); }
            public Variable(string value) : base(GM_Type.String) { Value = svalue = value; }
            public Variable(string value, GM_Type type) : base(type) { Value = svalue = value; }
            public Variable(int value, string instance) : base(GM_Type.Instance) {
                ivalue = value;
                Value = instance;
            }
            public override string ToString()
            {
                    return Value;
            }
        }
        class Conv : AST
        {
            public AST next;
            public Conv(AST ast, GM_Type type) : base(type) { this.next = ast; }


            public override string ToString()
            {
                Variable isVar = next as Variable;
                if (this.Type == next.Type) return next.ToString();

                switch (this.Type)
                {
                    case GM_Type.Int:
                        switch (next.Type)
                        {
                            case GM_Type.Double:
                            case GM_Type.Float:
                            case GM_Type.String:
                                return "(Int)(" + next.ToString() + ")";
                            default:
                                return next.ToString();
                        }
                    case GM_Type.Long:
                        switch (next.Type)
                        {
                            case GM_Type.Double:
                            case GM_Type.Float:
                            case GM_Type.String:
                                return "(Long)(" + next.ToString() + ")";
                            default:
                                return next.ToString();
                        }
                    case GM_Type.Short:
                        switch (next.Type)
                        {
                            case GM_Type.Double:
                            case GM_Type.Float:
                            case GM_Type.String:
                                return "(Short)(" + next.ToString() + ")";
                            default:
                                return next.ToString();
                        }
                    case GM_Type.Float:
                        switch (next.Type)
                        {
                            case GM_Type.Int:
                            case GM_Type.Long:
                            case GM_Type.Short:
                            case GM_Type.String:
                                return "(Float)(" + next.ToString() + ")";
                            default:
                                return next.ToString();
                        }
                    case GM_Type.Double:
                        switch (next.Type)
                        {
                            case GM_Type.Int:
                            case GM_Type.Long:
                            case GM_Type.Short:
                            case GM_Type.String:
                                return "(Double)(" + next.ToString() + ")";
                            default:
                                return next.ToString();
                        }
                    case GM_Type.String: // might have an issue with a double string conversion but should be fine
                        if (isVar != null && isVar.Type == GM_Type.Instance)
                            return "(String)(" + next.ToString() + ")";
                        else
                            return "(String)(" + next.ToString() + ")";
                    case GM_Type.Var:
                        if (isVar != null && next.Type == GM_Type.String) return isVar.ToString(); // might be a normal string
                        else return next.ToString(); // dosn't matter
                    case GM_Type.Instance:
                        if (isVar != null && next.Type == GM_Type.Short || next.Type == GM_Type.Int)
                        {
                            // we have to look up the int type here, need to have a static list somewhere or throw?
                            string instance;
                            if (instanceLookup.TryGetValue((int)isVar.IValue, out instance)) return instance;
                        }
                        return "(Instance)(" + isVar.ToString() + ")";
                    default:
                        return next.ToString();
                }
            }
        }
        class MathOp : AST
        {
            public AST Left { get; private set; }
            public AST Right { get; private set; }

            public OpType Op { get; private set; }

            public MathOp(AST left, OpType op, AST right) : base(GM_Type.NoType) { this.Left = left; this.Op = op; this.Right = right; }
            public override string ToString()
            {
                string sop;
                if (opMathOperation.TryGetValue(Op, out sop))
                {
                    return "(" + Left.ToString() + " " + sop + " " + Right.ToString() + ")";
                }
                throw new ArgumentException("Cannot find math operation");
            }
        }
        class Assign : AST
        {
            public string Variable { get; private set; }
            public AST Value { get; private set; }
            public Assign(string variable, GM_Type type, AST value) : base(type) { this.Variable = variable; this.Value = value; }
            public override string ToString()
            {
                return Variable + " = " + Value.ToString();
            }
        }
        class Call : AST
        {
            public string FunctionName { get; private set; }
            public int ArgumentCount { get; private set; }
            public AST[] Arguments { get; private set; }
            public Call(string functionname, params AST[] args) : base(GM_Type.Int)
            {
                this.FunctionName = functionname;
                ArgumentCount = args.Length;
                Arguments = args;
            }

            public override string ToString()
            {
                string ret = FunctionName + "(";
                if (ArgumentCount > 0)
                {
                    for (int i = 0; i < ArgumentCount - 1; i++)
                        ret += Arguments[i].ToString() + ",";
                    ret += Arguments.Last();
                }
                ret += ")";
                return ret;
            }
        }

        string FormatAssign(string sinstance, string var_name, int index, AST value)
        {
            return String.Format("{0}.{1}[{2}] = {3}", sinstance, var_name, index, value.ToString());
        }
        string FormatAssign(string sinstance, string var_name, AST index, AST value)
        {
            return String.Format("{0}.{1}[{2}] = {3}", sinstance, var_name, index.ToString(), value.ToString());
        }
        string FormatAssign(string sinstance, string var_name, int index, string value)
        {
            return String.Format("{0}.{1}[{2}] = {3}", sinstance, var_name, index, value);
        }
        string FormatAssign(string sinstance, string var_name, string value)
        {
            return String.Format("{0}.{1} = {2}", sinstance, var_name, value);
        }

        // This is an offset
        public string decodeCallName(int operand)
        {
            int string_ref = (int)(operand & 0x0FFFFFFF); // this COULD be 24 bits?

            if (string_ref < cr.stringList.Count)
            {
                return cr.stringList[string_ref].str;
            }
            else throw new Exception("Function not found!"); // return "NOT FOUND: " + string_ref.ToString();
        }

        // The new Fangled method

        void ProcessVarPush(Stack<AST> tree, uint op, int operand) {
            int instance = (short)(op & 0xFFFF);
            string sinstance = lookupInstance(instance);
            int load_type = (operand >> 24) & 0xFF;
            string var_name = decodeCallName(operand & 0x00FFFFFF);
            if (load_type == 0xA0) // simple varable, usally an int 
            {
                if (sinstance == "stack")
                {
                    AST value = tree.Pop();
                    Variable valueVar = value as Variable;
                    if (valueVar != null)
                        sinstance = lookupInstance((int)valueVar.IValue);
                    else
                        sinstance = value.ToString();
                    tree.Push(new Variable(sinstance + "." + var_name, GM_Type.Var));
                    return;
                } else //if(sinstance == "self" || sinstance == "global")
                {
                    //   AST value = tree.Pop();
                    tree.Push(new Variable(sinstance + "." + var_name, GM_Type.Var));
                    return;
                }
            }
            if (load_type == 0 && sinstance == "stack") // instance is on the stack and its an array
            {
                AST index = tree.Pop();
                AST value = tree.Pop();
                Variable valueVar = value as Variable;
                if (valueVar != null)
                    sinstance = lookupInstance((int)valueVar.IValue);
                else
                    sinstance = value.ToString();
                tree.Push(new Variable(sinstance + "." + var_name + "[" + index.ToString() + "]", GM_Type.Var));
                return;
            }
            if (load_type == 0x80) // not sure what this is
            {
                if (sinstance == "stack")
                {
                    AST ainstance = tree.Pop();
                    //   AST value = tree.Pop(); // Mabye.... mabye this is only one value, instance in a var?
                    sinstance = ainstance.ToString();
                    tree.Push(new Variable(sinstance + "." + var_name, GM_Type.Var));
                    return;
                }
                // ret = sinstance + "." + var_name + " (Load: 0x80)";
                //  opcode.value = new StackValue(instance, ret);
            }
            throw new NotImplementedException("Push.V convert not implmented"); // we are going to handle it all, no exceptions this time around
        }
        string ProcessAssignPush(Stack<AST> tree, uint op, int operand)
        {
            GM_Type convertFrom = (GM_Type)(int)((op >> 20) & 0xF);
            GM_Type convertTo = (GM_Type)(int)((op >> 16) & 0xF);
            int iinstance = (short)(op & 0xFFFF);
            string sinstance = lookupInstance(iinstance);
            int load_type = operand >> 24 & 0xFF; // I think this might only be 4 bits
            string var_name = decodeCallName(operand & 0x00FFFFFF);

            //Queue<StackValue> valueStack = new Queue<StackValue>(); // its a queue as we have to reverse the order
            // Lets do the simplest conversion first  int -> ref ( integer being assigned to a value of an object aka global.bob = 4;
            if (load_type == 0xA0 && (sinstance == "global" || sinstance == "self") && convertFrom != GM_Type.Var && convertTo == GM_Type.Var)
            {
                AST value = tree.Pop();
                return FormatAssign(sinstance, var_name, value.ToString());
            }
            if (load_type == 0xA0 && (sinstance == "global" || sinstance == "self") && convertFrom == GM_Type.Var && convertTo == GM_Type.Var) // usally a function return, assigning to an object
            {
                AST value = tree.Pop();
                return FormatAssign(sinstance, var_name, value.ToString());
            }
            // load_type 0 is an array and instance is on the stack
            if (load_type == 0 && (sinstance == "stack"))
            {
                AST index = tree.Pop();
                AST instance = tree.Pop();
                AST value = tree.Pop();
                if (int.TryParse(instance.ToString(), out iinstance))
                {
                    sinstance = lookupInstance(iinstance);
                }
                else sinstance = instance.ToString();
                return FormatAssign(sinstance, var_name, index, value);
            }
            return null;
            // zero seems to be an array assign
        }
        void ProcessMathOp(Stack<AST> tree, uint op)
        {
            OpType code = (OpType)((byte)(op >> 24));
            GM_Type fromType = (GM_Type)(int)((op >> 16) & 0xF);
            GM_Type tooType = (GM_Type)(int)((op >> 20) & 0xF);
            AST right = tree.Pop();
            AST left = tree.Pop();
            tree.Push(new MathOp(left, code, right));
        }
        delegate void FixFunctionDel(Disam disam, AST[] args);
        static Dictionary<string, FixFunctionDel> fix_function = new Dictionary<string, FixFunctionDel>()
        {
            { "instance_create",
                delegate (Disam disam, AST[] args) {
                 int objIndex;
                if(int.TryParse(args.Last().ToString(), out objIndex)) // if its a numerical instance, find the object name
                {
                    var obj = disam.cr.objList[objIndex];
                    args[args.Length - 1] = new Variable(obj.Name);
                    }
                }
            },
            { "snd_play",
                delegate (Disam disam, AST[] args) {
                 int sndIndex;
                if(int.TryParse(args[0].ToString(), out sndIndex)) // if its a numerical instance, find the object name
                {
                    var obj = disam.cr.audioList[sndIndex];
                    args[args.Length - 1] = new Variable(obj.Name);
                    }
                }
            },


        };
        void ProcessCall(Stack<AST> tree, uint op, int operand)
        {
            //  byte return_type = (byte)((op >> 16) & 0xFF); // always i
            // string return_type_string = typeLookup[return_type];
            int arg_count = (ushort)(op & 0xFFFF);
            string func_name = decodeCallName(operand);

            AST[] args = new AST[arg_count];
            for (int i = 0; i < arg_count; i++) args[i] = tree.Pop();
            // Call special cases
            FixFunctionDel fix;
            if (fix_function.TryGetValue(func_name, out fix)) fix(this, args);

            tree.Push(new Call(func_name, args.ToArray()));
        }
        public bool invertBranchLogic = true;
        string ProcessBranch(Stack<AST> tree, string gotoLabel, bool testIfTrue)
        {
            string s = "if ";
            AST value = tree.Pop();
            MathOp mathOp = value as MathOp;
            if (mathOp == null)
            {
                if (testIfTrue) s += value.ToString();
                else s += "!" + value.ToString();
            }
            else
            {
                if (invertBranchLogic && !testIfTrue)
                {
                    OpType inverted;
                    if (invertOpType.TryGetValue(mathOp.Op, out inverted))
                    {
                        mathOp = new MathOp(mathOp.Left, inverted, mathOp.Right);
                        testIfTrue = true;
                    }
                }
                if (testIfTrue)
                    s += mathOp.ToString();
                else
                    s += "!(" + mathOp.ToString() + ")";
            }
            s += " then goto " + gotoLabel;
            return s;
        }
        class CodeLine
        {
            public string Text = null;
            public string Label = null;
            public GM_Disam.Opcode code;
            public int pc = 0;
            public long startPc = 0;
            public long endPC = 0;

            public string DecompileLine()
            {
                if (this.Text == null && this.Label == null) return null;
                string s = String.Format("{0,-10} {1}", this.Label == null ? "" : this.Label + ":", this.Text == null ? "" : this.Text);
                return s;
            }
            public override string ToString()
            {
                string s = DecompileLine();
                return s == null ? "Nothing" : s;
            }
        }
        Dictionary<int, string> gotos = new Dictionary<int, string>();
        HashSet<int> markedCode = new HashSet<int>();
        string GetLabel(int pc)
        {
            string label;
            if (gotos.TryGetValue(pc, out label)) return label;
            return gotos[pc] = ("Label_" + gotos.Count);
        }
        int CorrectBranchOffset(int offset, int pc, ChunkStream r)
        {
            /*

            r.PushPosition();
            r.Position += offset * 4 - 4;
            GM_Disam.Opcode opcode = new GM_Disam.Opcode(r, -1);
            if (opcode.Kind == OpType.Push && opcode.opernadType == GM_Type.Short && opcode.OperandValue == 0)
            {
                OpCode checkBranch = new OpCode(r, -1);
                if (checkBranch.Kind == OpType.Bf)
                {
                    markedCode.Add(offset + pc);
                    markedCode.Add(offset + pc + 1); // mark to ignore

                    // we have a hit return this offset to correct the jump
                    offset = checkBranch.Offset;
                }
            }
            r.PopPosition();
            */
            return offset + pc;
        }

        int CorrectBranchOffset(SortedDictionary<int, Opcode> codes, int pc)
        {
            /*
            Its so bad I am using MULTI LINE COMMENTS ARRRRG
            Ok, so this must be a case of LVVM, but it looks like the compiler optimizes out bf
            That is to say it will throw something on the stack, and use bf OR it wil jump to the bf with push.e0 on it so it
            dosn't have to put in another B.  Makes sence but annoying to work with
            We have to test to see if this is a coded B ( Push.E 0; BF label } = ( B label ) and ANY link that goes to this has to be
            fixed.  meh

            
            // hacking till I ileterate over ALL the reverses ARG

            OpCode opcode = codes[pc]; // THIS must exist, so we must fix all the codes in here.
            if (opcode.Kind == OpType.Push && opcode.opernadType == GM_Type.Short && opcode.OperandValue == 0)
            {

                OpCode checkBranch = codes[pc + 1];
                if (checkBranch.Kind == OpType.Bf)
                {
                    codes.Remove(pc);
                    codes.Remove(pc + 1);
                    OpCode branchBefore = codes[pc - 1]; // this jumps over these two staments
                    int newpc = checkBranch.Offset;
                    foreach(var p in codes)
                    {
                        OpCode o = p.Value;
                        if (o.Kind == OpType.B && (o.Offset) == (pc + 1)) // oh god, I figured out whats happening
                        {
                            branchBefore.ChangeOffset(newpc);
                            branchBefore.SoftChangKind(OpType.Bf);
                        } else if
                    }
                    pc = newpc;   
                }
            }
            return pc;
              */
            return pc;
        }

        string DoBranch(SortedDictionary<int, Opcode> codes, Stack<AST> stack, OpType op, int pc, int offset)
        {
            string codeLine = null;
            var last = codes.Last();
            if (offset < last.Key) offset = CorrectBranchOffset(codes, offset);
            switch (op)
            {
                case OpType.Bf:
                    codeLine = ProcessBranch(stack, GetLabel(offset), false);
                    break;
                case OpType.Bt:
                    codeLine = ProcessBranch(stack, GetLabel(offset), true);
                    break;
                case OpType.B:
                    codeLine = "goto " + GetLabel(offset);
                    break;
                default:
                    throw new Exception("Can't be called here");
            }
            return codeLine;
        }
        string DoBranch(Stack<AST> stack, OpType op, int pc, int offset)
        {
            string codeLine = null;
            offset = CorrectBranchOffset(offset, pc);
            switch (op)
            {
                case OpType.Bf:
                    codeLine = ProcessBranch(stack, GetLabel(offset), false);
                    break;
                case OpType.Bt:
                    codeLine = ProcessBranch(stack, GetLabel(offset), true);
                    break;
                case OpType.B:
                    codeLine = "goto " + GetLabel(offset);
                    break;
                default:
                    throw new Exception("Can't be called here");
            }
            return codeLine;
        }
        void InsertLabels(List<CodeLine> lines, int pc)
        {
            CodeLine line;
            foreach (var label in gotos)
            {
                if (label.Key >= pc) // this is the end of the file
                {
                    line = new CodeLine();
                    line.pc = label.Key;
                    line.Label = label.Value;
                    lines.Add(line);
                    continue;
                }
                line = lines.Single(o => o.startPc == label.Key);
                if(line == null) throw new Exception("No more fucking around, fix this");
                line.Label = label.Value;
                continue;

              

            }

        }
        bool OffsetOpcodeTest(Opcode o, OpType op, int offset)
        {
            GM_Disam.OffsetOpcode branch = o as GM_Disam.OffsetOpcode;
            if (branch != null && branch.Op == op && branch.Offset == offset) return true;
            return false;
        }
        bool OffsetOpcodeTest(Opcode o, OpType op)
        {
            GM_Disam.OffsetOpcode branch = o as GM_Disam.OffsetOpcode;
            if (branch != null && branch.Op == op) return true;
            return false;
        }
        bool PushOpcodeTest(Opcode o,  GM_Type t, int value)
        {
            GM_Disam.PushOpcode push = o as GM_Disam.PushOpcode;
            if (push != null && push.OperandType == t && push.OperandValue == value) return true;
            return false;
        }
        SortedList<int, GM_Disam.Opcode> PeepholeFix(SortedList<int, GM_Disam.Opcode> codes)
        {
            // need to change this to a bunch of delgates, but right now lets find one patern match
            int found_branch = -1;
            for (int i = 0; i < codes.Count; i++)
            {
                if (OffsetOpcodeTest(codes[i], OpType.B, 2) && PushOpcodeTest(codes[i + 1], GM_Type.Short, 0) && OffsetOpcodeTest(codes[i + 3], OpType.Bf))
                {
                    found_branch = i; // ITS A MATCH
                }
            }
            return codes;
        }
        List<CodeLine> processStream2(ChunkStream r, int codeOffset, int code_size)
        {
            GM_Disam disam = new GM_Disam(r);
            SortedList<int, Opcode> codes = disam.ReadOpCode(codeOffset, code_size);
            gotos = new Dictionary<int, string>();
            markedCode = new HashSet<int>();
            int limit = (int)(codeOffset + code_size);

            Stack<AST> tree = new Stack<AST>(); // used to caculate stuff
            Stack<AST> enviroment = new Stack<AST>();
            List<CodeLine> lines = new List<CodeLine>();
            string codeLine = null;
            int pc = 0, start_pc = -1, last_pc = 0;
            int end_pc = codes.Last().Key;
            for(int it=0;it< end_pc;it++) {
                if (!codes.ContainsKey(it)) continue;
                GM_Disam.Opcode code = codes[it];
                last_pc = pc;
                
                pc = code.Pc;
                if (start_pc == -1) start_pc = pc;
                CodeLine line = new CodeLine();
                line.code = code;
                line.pc = pc;
                line.startPc = -1;
                lines.Add(line);

                if (markedCode.Contains(pc))
                    continue; // ignore this code, its already processed
                switch (code.Kind)
                {
                    // A hack for push enviroment right here.
                    // It SEEMS that push enviroment is set up so we can call a function that is in an instance
                    // aka self.myobject.instance_destroy()
                    // Sence we know the start and the end of the call, lets see if we can simplfiy this with 
                    // recording the start of a push env and back.
                    // This might not work well with recursive returns or calls though
                    case OpType.Pushenv:
                        enviroment.Push(tree.Pop());
                        // codeLine = "Push Enviroment " + tree.Pop().ToString() + "goto on error " + checkLabel(op);
                        break;
                    case OpType.Popenv:
                        {
                            AST env = enviroment.Pop();
                            // we need to find the last statment
                            foreach (CodeLine l in lines.Reverse<CodeLine>())
                            {
                                if (l.Text != null)
                                {
                                    l.Text = env.ToString() + "." + l.Text;
                                    break;
                                }
                            }
                        }
                        //    codeLine = "Pop Enviroment from " + checkLabelEnv((short)(op & 0xFFFF));
                        break;
                    case OpType.Exit:
                        codeLine = "Exit";
                        System.Diagnostics.Debug.Assert(tree.Count == 0);
                        break;
                    case OpType.Neg: // not really xNOR but not sure WHAT it is, one op?
                        {
                            AST value = tree.Pop();
                            tree.Push(new Variable("-(" + value.ToString() + ")", GM_Type.Var));
                        }
                        break;
                    case OpType.Mul:
                    case OpType.Div:
                    case OpType.Rem:
                    case OpType.Mod:
                    case OpType.Add:
                    case OpType.Sub:
                    case OpType.Or:
                    case OpType.And:
                    case OpType.Xor:
                    case OpType.Not:
                    case OpType.Sal:
                    case OpType.Slt:
                    case OpType.Sle:
                    case OpType.Seq:
                    case OpType.Sge:
                    case OpType.Sgt:
                    case OpType.Sne:

                        ProcessMathOp(tree, code.Op);
                        break;
                    case OpType.Bf: // branch if false
                    case OpType.Bt: // branch if true
                    case OpType.B:
                        codeLine = DoBranch(codes, tree, code.Kind, pc, code.Offset);
                        break;
                    case OpType.Conv:
                        {
                            //   GM_Type fromType = (GM_Type)(int)((op >> 16) & 0xF);
                            //   GM_Type tooType = (GM_Type)(int)((op >> 20) & 0xF);
                            tree.Push(new Conv(tree.Pop(), code.SecondType));
                        }
                        break;
                    case OpType.Push: // most common.  Used for all math operations
                        {
                            byte t = (byte)(code.Op >> 16); // for now
                            switch (t)
                            {
                                case 0x0: tree.Push(new Variable(code.OperandDouble)); break;
                                case 0x1: tree.Push(new Variable(code.OperandFloat)); break;
                                case 0x2: tree.Push(new Variable(code.OperandValue)); break;
                                case 0x3: tree.Push(new Variable(code.OperandLong)); break;
                                case 0x5: ProcessVarPush(tree, code.Op, code.OperandValue); break;
                                case 0x6:
                                    {
                                        string value = cr.stringList[code.OperandValue].str;
                                        tree.Push(new Variable(value));
                                    }
                                    break;
                                case 0xF: tree.Push(new Variable((short)(code.OperandValue))); break;
                                default:
                                    throw new Exception("Bad type");
                            }
                        }
                        break;
                    case OpType.Pop:
                        codeLine = ProcessAssignPush(tree, code.Op, code.Offset);
                        break;
                    case OpType.Popz: // usally on void funtion returns, so just pop the stack and print it
                        {
                            codeLine = tree.Pop().ToString();
                        }
                        break;
                    case OpType.Dup:
                        {
                            tree.Push(tree.First()); // will this work?
                                                     //   Variable v = tree.First() as Variable;
                                                     //   tree.First()
                                                     //   if (v == null) throw new ArgumentException("Dup didn't work, meh");
                                                     //  tree.Push(v.Dup());
                        }
                        break;
                    case OpType.Break:
                        {
                            codeLine = String.Format("break {0}", code.Op & 0xFFFF); // have yet to run into this
                        }
                        break;
                    case OpType.Call:
                        ProcessCall(tree, code.Op, code.Offset);
                        break;
                    default:
                        //      System.Diagnostics.Debug.WriteLine("Not implmented at {0} value {1} valuehex {1:X}", r.Position, op, op);
                        break;
                        //   throw new NotImplementedException("Opcode not implmented");
                }
                //  int startPosition = r.Position;
                if (codeLine != null)
                {
                    line.startPc = start_pc;
                    start_pc = -1;
                    line.endPC = pc;
                    line.Text = codeLine;
                    codeLine = null;
                }
            }
            InsertLabels(lines,pc);
            return lines;
        }
/*
        List<CodeLine> processStream2_old(ChunkStream r, int codeOffset, int code_size) {
            gotos = new Dictionary<int, string>();
            markedCode = new HashSet<int>();
            int limit = (int)(codeOffset + code_size);
            SortedDictionary<int, OpCode> codes = OpCode.SimpleDisam(r, codeOffset, limit);
            r.PushSeek((int)codeOffset);
           
            Stack<AST> tree = new Stack<AST>(); // used to caculate stuff
            Stack<AST> enviroment = new Stack<AST>();
            List<CodeLine> lines = new List<CodeLine>();
            
            SortedDictionary<int, CodeLine> pclookup = new SortedDictionary<int, CodeLine>();
            int len = (int)r.Length / 4;
            string codeLine = null;
            int pc = 0;
            int last_pc = 0;
            int start_pc = pc;
            uint op = 0;
            // some reason I just can't get r.Position to sync up right with the start of pc
            // 
            //     int shit_header = r.ReadInt32();
            while (r.Position != limit)
            {
                int startPosition = r.Position;
                CodeLine line = new CodeLine();
                line.startPc = -1;
                last_pc = pc;
                line.pc = pc = (startPosition - codeOffset) / 4;
                pclookup[pc] = line;
                lines.Add(line);

               
                System.Diagnostics.Debug.Assert(((startPosition - codeOffset) % 4) == 0); // are we aligned?
                line.op = op = r.ReadUInt32();
                OpType code = (OpType)((byte)(op >> 24));
                line.opType = code;
                if (markedCode.Contains(pc))
                    continue; // ignore this code, its already processed
                switch (code)
                {
                    // A hack for push enviroment right here.
                    // It SEEMS that push enviroment is set up so we can call a function that is in an instance
                    // aka self.myobject.instance_destroy()
                    // Sence we know the start and the end of the call, lets see if we can simplfiy this with 
                    // recording the start of a push env and back.
                    // This might not work well with recursive returns or calls though
                    case OpType.Pushenv:
                        enviroment.Push(tree.Pop());
                       // codeLine = "Push Enviroment " + tree.Pop().ToString() + "goto on error " + checkLabel(op);
                        break;
                    case OpType.Popenv:
                        {
                            AST env = enviroment.Pop();
                            // we need to find the last statment
                            foreach(CodeLine l in lines.Reverse<CodeLine>())
                            {
                                if(l.Text != null)
                                {
                                    l.Text = env.ToString() + "." + l.Text;
                                    break;
                                }
                            }
                        }
                    //    codeLine = "Pop Enviroment from " + checkLabelEnv((short)(op & 0xFFFF));
                        break;
                    case OpType.Exit:
                        codeLine = "Exit";
                        System.Diagnostics.Debug.Assert(tree.Count == 0);
                        break;
                    case OpType.Neg: // not really xNOR but not sure WHAT it is, one op?
                        {
                            GM_Type fromType = (GM_Type)(int)((op >> 16) & 0xF);
                            GM_Type tooType = (GM_Type)(int)((op >> 20) & 0xF);
                            AST value = tree.Pop();
                            tree.Push(new Variable("-(" + value.ToString() + ")", GM_Type.Var));
                        }
                        break;
                    case OpType.Mul:
                    case OpType.Div:
                    case OpType.Rem:
                    case OpType.Mod:
                    case OpType.Add:
                    case OpType.Sub:
                    case OpType.Or:
                    case OpType.And:
                    case OpType.Xor:
                    case OpType.Not:
                    case OpType.Sal:
                    case OpType.Slt:
                    case OpType.Sle:
                    case OpType.Seq:
                    case OpType.Sge:
                    case OpType.Sgt:
                    case OpType.Sne:
                        
                        ProcessMathOp(tree, op);
                        break;
                    case OpType.Bf: // branch if false
                    case OpType.Bt: // branch if true
                    case OpType.B:
                        codeLine = DoBranch(tree, code, pc, getBranchOffset(op), r);
                        break;
                    case OpType.Conv:
                        {
                            GM_Type fromType = (GM_Type)(int)((op >> 16) & 0xF);
                            GM_Type tooType = (GM_Type)(int)((op >> 20) & 0xF);
                            tree.Push(new Conv(tree.Pop(), tooType));
                        }
                        break;
                    case OpType.Push: // most common.  Used for all math operations
                        {
                            byte t = (byte)(op >> 16);
                            switch (t)
                            {
                                case 0x0: tree.Push(new Variable(r.ReadDouble())); break;
                                case 0x1: tree.Push(new Variable(r.ReadSingle())); break;
                                case 0x2: tree.Push(new Variable(line.operand = r.ReadInt32())); break;
                                case 0x3: tree.Push(new Variable(r.ReadInt64())); break;
                                case 0x5: ProcessVarPush(tree, op, line.operand = r.ReadInt32()); break;
                                case 0x6:
                                    {
                                        int operand = r.ReadInt32();
                                        line.operand = operand;
                                        string value = cr.stringList[(int)operand].str;
                                        tree.Push(new Variable(value));
                                    }
                                    break;
                                case 0xF: tree.Push(new Variable((short)(op & 0xFFFF))); break;
                                default:
                                    throw new Exception("Bad type");
                            }
                        }
                        break;
                    case OpType.Pop:
                        codeLine = ProcessAssignPush(tree, op, line.operand = r.ReadInt32());
                        break;
                    case OpType.Popz: // usally on void funtion returns, so just pop the stack and print it
                        {
                            codeLine = tree.Pop().ToString();
                        }
                        break;
                    case OpType.Dup:
                        {
                            tree.Push(tree.First()); // will this work?
                         //   Variable v = tree.First() as Variable;
                         //   tree.First()
                         //   if (v == null) throw new ArgumentException("Dup didn't work, meh");
                          //  tree.Push(v.Dup());
                        }
                        break;
                    case OpType.Break:
                        {
                            int brkint = (ushort)(op & 0xFFFF); // to break on
                            int alwayszero = r.ReadInt32();
                            int str_ref = r.ReadInt32(); // this is a goto ugh christ I have to download the entire file
                            int index_maybe = r.ReadInt32();
                            List<CodeLine> brkLines = processStream2(r, str_ref, index_maybe);
                            //  string s = decodeCallName(str_ref);

                            int value_maybe = r.ReadInt32();

                            // int offset = r.ReadInt32();
                            codeLine = String.Format("break {0}", brkint);
                        }
                        break;
                    case OpType.Call:
                        ProcessCall(tree, op, line.operand = r.ReadInt32());
                        break;
                    case (OpType)0:
                        System.Diagnostics.Debug.WriteLine("Zero at {0} value {1} valuehex {1:X}", r.Position, op, op);
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine("Not implmented at {0} value {1} valuehex {1:X}", r.Position, op, op);
                        break;
                        //   throw new NotImplementedException("Opcode not implmented");
                }
              //  int startPosition = r.Position;
                if (codeLine != null)
                {
                    line.startPc = start_pc;
                    start_pc = (r.Position - codeOffset) / 4;
                    line.endPC = pc;
                    line.Text = codeLine;
                    codeLine = null;
                }
                
            }
            r.PopPosition();
         
            return lines;
        }
        */
    }
}
