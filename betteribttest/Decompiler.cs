using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web.UI;
using System.Web;
using System.Runtime.InteropServices;
using System.Diagnostics;
/*
    So why SortedList?  When using the peekhole optimiazer (or deoptimizer?) We will need to get the next instruction 
    rather than the next pc, while OTHEr parts of the decompiler needs the next pc
*/

namespace betteribttest
{
    using Opcode = GM_Disam.Opcode;
    using OffsetOpcode = GM_Disam.OffsetOpcode;
    using System.Text.RegularExpressions;
    using System.Collections;

    class Decompiler
    {
        string scriptName;
        ChunkReader cr;
        SortedList<int, GM_Disam.Opcode> codes;
        SortedList<int, OffsetOpcode> inlineBranches;
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
            if (Decompiler.instanceLookup.TryGetValue(instance, out ret)) return ret;
            else return String.Format("Instance({0})", instance);
        }


        
      
        // side not, AGAIN, if I wanted to spend the time, I could make an array for each object/script
        // and determan what the types are of all the variables and then act find the object names that way
        // hoewever this will take weeks and its just a few I need to look at

        static Regex RealConstantRegex = new Regex(@"real\((\d+)\)");
        static Dictionary<string, FixVariableDel> fix_variable = new Dictionary<string, FixVariableDel>()
        {
            {"self.sprite_index", delegate(Decompiler disam, AST toAssign, ref AST value) {
                Constant constant = value.EvalInt();
                    if(constant != null)
                    {
                    int i = (int)constant.IValue;
                        var obj = disam.cr.spriteList[i];
                        value =   new Constant(obj.Name);
                    }
                    
                    // sometimes we use the function "real" with a constant inside so lets find it
            }
            // This is a special case.  In all the obj_face files, there is an array of u where
            // all the face sprite names are kept, we detect if we are IN that and then change the
            // constants to numerical values
            },{ "self.u", delegate(Decompiler disam, AST toAssign, ref AST value) {
                if(disam.scriptName.ToLower().IndexOf("obj_face")!=-1)
                {
                    Constant constant = value.EvalInt();
                    if(constant != null)
                    {
                        int i = (int)constant.IValue;
                         var obj = disam.cr.spriteList[i];
                        value =   new Constant(obj.Name);
                    }
                }
            }
            }
        };
 
        static Dictionary<string, FixFunctionDel> fix_function = new Dictionary<string, FixFunctionDel>()
        {
            { "instance_create",
                delegate (Decompiler disam, AST[] args) {
                     Constant constant = args.Last().EvalInt();
                    if(constant != null)
                    {
                        int i = (int)constant.IValue;
                        var obj = disam.cr.objList[i];
                        args[args.Length - 1] = new Constant(obj.Name);
                        }
                    }
            },
            { "instance_exists",
                delegate (Decompiler disam, AST[] args) {
                      Constant constant = args[0].EvalInt();
                    if(constant != null)
                    {
                        int i = (int)constant.IValue;
                        var obj = disam.cr.objList[i];
                        args[0] = new Constant(obj.Name);
                    }
                }
            },
            { "snd_play",
                delegate (Decompiler disam, AST[] args) {
                     Constant constant = args[0].EvalInt();
                    if(constant != null)
                    {
                        int i = (int)constant.IValue;
                        var obj = disam.cr.audioList[i];
                        args[0] = new Constant("{ Name = " + obj.Name + " , filename = " + obj.filename + "}");
                    }
                }
            },
            { "script_execute",
                delegate (Decompiler disam, AST[] args) {
                      Constant constant = args[0].EvalInt();
                    if(constant != null)
                    {
                        int i = (int)constant.IValue;
                        var scr = disam.cr.scriptIndex[i];
                        args[0] = new Constant(scr.script_name);
                        FixFunctionDel script_execute_del;
                        if(script_execute_fix.TryGetValue(scr.script_name,out script_execute_del)) script_execute_del(disam,args);
                    }
                }
            }
        };
        public Decompiler(ChunkReader cr)
        {
            this.cr = cr;
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
  
            
        string GetInstance(Stack<AST> tree, int instance)
        {
            string sinstance = null;
            if (instance == 0) // in the stack
            {
                AST new_instance = tree.Pop();
                Constant c = new_instance.EvalInt();
                if (c==null) throw new Exception("Cannot eval this stack value");
                instance = (int)c.IValue;
                if (instance == 0) throw new Exception("Reading a stack, of the instance?");
            }
            if (instance < 0) sinstance = lookupInstance(instance);
            else sinstance = cr.objList[instance].Name;
            return sinstance;
        }
        string  GetInstance(Stack<AST> tree, string instance)
        {
            int instance_test;
            if (int.TryParse(instance, out instance_test)) return GetInstance(tree, instance_test);
            return null;
        }
        void ProcessVarPush(Stack<AST> tree, GM_Disam.PushOpcode op) {
            int instance = op.Instance;
            int operandValue = (int)op.OperandValue;
            int load_type = (operandValue >> 24) & 0xFF;
            string var_name = decodeCallName(operandValue & 0x00FFFFFF);

            AST index = load_type == 0 ? tree.Pop() : null;
            string instance_name = GetInstance(tree, instance); 
            AST value = null;
            if (index == null)
                value= new ObjectVariable(instance_name, var_name);
            else 
                value = new ArrayAccess(instance_name, var_name, index);
            tree.Push(value);
           // throw new NotImplementedException("Push.V convert not implmented"); // we are going to handle it all, no exceptions this time around
        }
        Assign ProcessAssignPop(Stack<AST> tree, GM_Disam.PopOpcode code)
        {
            GM_Type convertFrom = code.FirstType;
            GM_Type convertTo = code.SecondType;
            int instance = code.Instance;
            int load_type = code.Offset >> 24 & 0xFF; // I think this might only be 4 bits
            string var_name = decodeCallName(code.Offset & 0x00FFFFFF);

            AST index = load_type == 0 ? tree.Pop() : null;
            string instance_name = GetInstance(tree, instance);
            AST value = tree.Pop();
            FixVariable(this, var_name,ref value);
            Assign assign = null;
            if (index == null)
                if (index == null)
                    value = new Assign(new ObjectVariable(instance_name, var_name), value);
                else
                    value = new Assign(new ArrayAccess(instance_name, var_name, index), value);
            return assign;
        }
        void ProcessMathOp(Stack<AST> tree, uint op)
        {
            GMCode code = (GMCode)((byte)(op >> 24));
            GM_Type fromType = (GM_Type)(int)((op >> 16) & 0xF);
            GM_Type tooType = (GM_Type)(int)((op >> 20) & 0xF);
            AST right = tree.Pop();
            AST left = tree.Pop();
            FixVariable(this, left.ToString(), ref right);
            tree.Push(new MathOp(left, code, right));
        }
        delegate void FixFunctionDel(Decompiler disam, AST[] args);
        delegate void FixVariableDel(Decompiler disam, AST toAssign, ref AST value);
        static void FixVariable(Decompiler disam, string var_name, ref AST value)
        {
            FixVariableDel fixVar;
            if (fix_variable.TryGetValue(var_name, out fixVar)) fixVar(disam, null, ref value);
        }
        // takes the stuff from fix execute and repairs the names of the arguments
        static Dictionary<string, FixFunctionDel> script_execute_fix = new Dictionary<string, FixFunctionDel>()
        {
            { "SCR_TEXTSETUP",
              delegate (Decompiler disam, AST[] args) {
                  int i;
                    if(args[1].CanEval(out i)) args[0] =  new Constant(disam.cr.resFonts[i].Name);   // font index  
                    if(args[3].CanEval(out i)) args[2] =  new Constant(String.Format("Color(0x{0:X8})",i));  // color, just easyer to read
                    if(args[8].CanEval(out i))  // sound
                    {
                       var obj = disam.cr.audioList[i];
                        args[8] =  new Constant("{ Name = " + obj.Name + " , filename = " + obj.filename + "}");
                    }
              }
            },

        };
    
        void ProcessCall(Stack<AST> tree, GM_Disam.CallOpcode op)
        {
            //  byte return_type = (byte)((op >> 16) & 0xFF); // always i
            // string return_type_string = typeLookup[return_type];
            int arg_count = op.ArgumentCount;
            string func_name = decodeCallName(op.Offset);

            AST[] args = new AST[arg_count];
            for (int i = 0; i < arg_count; i++) args[i] = tree.Pop();
            // Call special cases
            FixFunctionDel fix;
            if (fix_function.TryGetValue(func_name, out fix)) fix(this, args);

            tree.Push(new Call(func_name, args.ToArray()));
        }
        AST ProcessBranch(Stack<AST> tree, AST target, bool testIfTrue)
        {
            AST value = tree.Pop();
            MathOp mathOp = value as MathOp;
            Conditional ifStatment = new Conditional(value, target);
            return testIfTrue ? ifStatment : ifStatment.Invert(false);
        }
        class CodeLine
        {
            AST _ast = null;
            ASTLabel _label = null;
            public GM_Disam.Opcode code;
            public int pc = 0;
            public int startPc = 0;
            public int endPC = 0;
            public ASTLabel label { get; set; }
            public AST ast
            {
                get { return _ast; }
                set
                {
                    _ast = value;
                }
            }
            public string DecompileLine()
            {
                if (ast == null) return null;
                else return ASTLabel.FormatWithLabel(ast);
            }
            public override string ToString()
            {
                string s = DecompileLine();
                return s == null ? String.Format("{0,-5} : No AST", pc) : String.Format("{0,-5} : {1}", startPc,s);
            }
        }
        Dictionary<int, ASTLabel> gotos = new Dictionary<int, ASTLabel>();
        HashSet<int> markedCode = new HashSet<int>();
        ASTLabel GetLabel(int pc)
        {
            ASTLabel label;
            if (gotos.TryGetValue(pc, out label)) return label;
            return gotos[pc] = new ASTLabel(gotos.Count,pc);     
        }


        void InsertLabels(List<CodeLine> lines, int pc)
        {
            // CodeLine line;
            ASTLabel label;
            foreach (var line in lines)
            {
                if (gotos.TryGetValue((int)line.startPc, out label))
                {
                    if (line.ast == null) throw new Exception("Missing Statment");
                    
                      //  Label test = line.ast as Label;
                      //  if(test != null) throw new Exception("Label '" + test + "' already assigned for pc = " + line.pc);
                    
                    line.label = label;
                    gotos.Remove(line.startPc);
                }
            }
            if (gotos.Count > 0)
            {
                List<int> keys = new List<int>();
                var line = lines.Last();
                foreach (var g in gotos)
                {
                    if (g.Key > line.pc)
                    {
                        CodeLine n = new CodeLine();
                        n.ast = new LabelStart(g.Value, null);
                        n.label = g.Value;
                        n.pc = g.Key;
                        n.startPc = g.Key;
                        n.endPC = g.Key;
                        lines.Add(n);
                        keys.Add(g.Key);

                    }
                }
                foreach (var k in keys) gotos.Remove(k);
            }
            foreach (var g in gotos)
            {
                CodeLine n = new CodeLine();
                n.pc = g.Key;
                n.ast = new LabelStart(g.Value, new Constant("Label target not found or used but in some branch"));
                lines.Insert(0, n);
            }
           
        }
        bool OffsetOpcodeTest(Opcode o, GMCode op, int offset)
        {
            GM_Disam.OffsetOpcode branch = o as GM_Disam.OffsetOpcode;
            if (branch != null && branch.Op == op && branch.Offset == offset) return true;
            return false;
        }
        bool OffsetOpcodeTest(Opcode o, GMCode op)
        {
            GM_Disam.OffsetOpcode branch = o as GM_Disam.OffsetOpcode;
            if (branch != null && branch.Op == op) return true;
            return false;
        }
        bool PushOpcodeTest(Opcode o, GM_Type t, int value)
        {
            GM_Disam.PushOpcode push = o as GM_Disam.PushOpcode;
            if (push != null && push.OperandType == t && push.OperandValue == value) return true;
            return false;
        }
        void ReplaceBranchAddress(int from, int too)
        {
            foreach (var toFix in codes.Where(o => o.Value.isBranch && o.Value.Address == from))
            {
                OffsetOpcode b = toFix.Value as OffsetOpcode;
                System.Diagnostics.Debug.Write("\t B Correct: " + b.ToString());
                System.Diagnostics.Debug.Write(" TO ");
                b.Address = too;
                System.Diagnostics.Debug.WriteLine(b.ToString());
            }
        }
        void ReplaceBranchAddress(int from, int too, GMCode fromType, GMCode toType)
        {
            foreach (var toFix in codes.Where(o => o.Value.Op == fromType && o.Value.Address == from))
            {
                OffsetOpcode b = toFix.Value as OffsetOpcode;
                System.Diagnostics.Debug.Write("\t B Correct: " + b.ToString());
                System.Diagnostics.Debug.Write(" TO ");
                b.Address = too;
                b.Op = toType;
                System.Diagnostics.Debug.WriteLine(b.ToString());
            }
        }

        List<Opcode> MatchPatern(bool remove, params Func<Opcode, bool>[] funcs)
        {
            var list = codes.Values;
            int pos = 0;
            while (pos < list.Count)
            {
                int match = 0;
                while (match < funcs.Length)
                {
                    if (funcs[match](list[pos + match])) match++;
                    else break;
                }
                if (match == funcs.Length)
                {
                    List<Opcode> ops = new List<Opcode>();
                    for (int i = 0; i < match; i++) ops.Add(list[pos + i]);
                    if (remove) foreach (Opcode o in ops) codes.Remove(o.Pc); // have to do this as the list numbers become flaky
                    return ops;
                }
                pos++;
            }
            return null;
        }

        // FIX THIS.  I think I will have to just start doing branch following and detection and to detect this kind of junk
        void PeepholeFix()
        {
            inlineBranches = new SortedList<int, OffsetOpcode>();
            // need to change this to a bunch of delgates, but right now lets find one patern match
            var list = codes.Values;
            int fix_count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var match = MatchPatern(false, o => o.Op == GMCode.B && o.Offset == 2, o => PushOpcodeTest(o, GM_Type.Short, 0), o => o.Op == GMCode.Bf);

                if (match != null)
                {
                    fix_count++;
                    // Ok, lets start this ride.  Since the first instruction is a B and it jumps over to BF, something is on the stack that we need to compare BF with
                    OffsetOpcode inlineBranch = new OffsetOpcode(match[2].Op, match[2].Offset, match[0].Pc);
                    int final_destnation = match[2].Address;
                    match[0].Op = GMCode.Bf;
                    match[0].Address = final_destnation; // fix the jump
                    match[1].Op = GMCode.BadOp;
                    match[2].Op = GMCode.BadOp;

                    // lets start this ball rolling, first lets find any and all branches to the three code we have here
                    // First, a jump to B 2 means its really a BF <somehwere> so both of these instructons are equilvelent

                    var indirectB = MatchPatern(false, o => o.isBranch && o.Address == match[1].Pc);
                    if (indirectB != null) foreach (Opcode o in indirectB) o.Address = final_destnation; // we don't care about the target branch type

                    // Ran into this on gml_Object_obj_dialogurer_Step  a push was done for a conditional, then a B
                 //   var stackIssue = MatchPatern(false, o => o.isConditional, o => o.Op == OpType.B && o.Pc == final_destnation); // This should be changed to BF humm
               //     if (stackIssue != null) foreach (Opcode o in indirectB) o.Op = OpType.Bf; // we don't care about the target branch type

                    // order matters since we could have braches at the top that might go here
                    var indirectBF = MatchPatern(false, o => o.isBranch && (o.Address == match[0].Pc || o.Address == match[2].Pc));
                    System.Diagnostics.Debug.Assert(indirectBF == null); // There is NO reason why this shouldn't be nul.  No reason to branch to these two address
                                                                         // Since this poitns to a push.E 0, and the next instruction is a BF so it will ALWAYS jump, change the offset of this to 3
                    if (indirectB != null) foreach (Opcode o in indirectB) { o.Address = final_destnation; }//  o.Op = OpType.B; }
                    // we don't care about the target branch type
                    // Change that B to BF but not yet
                     //  codes.Add(inlineBranch.Pc, inlineBranch); // put it back cause we need it for the previous conditional!

                    // This saves some code.  Its either watch it on the branch decompile stage for inline opcodes
                    // OR modify the existing opcodes to fix this issue
                    //      inlineBranches.Add(match[0].Pc, inlineBranch); // make it inline as well for any targets
                    //       inlineBranches.Add(match[2].Pc, inlineBranch); // make it inline as well for any targets

                    // Second, we need to find, and there SHOULD be one out there, of some branch that points to the Push.E 0
                    System.Diagnostics.Debug.WriteLineIf(indirectB == null, "No B match for the scond bit?  What is the compiler thinking?");
                    

                }
            }
            for (int i = 0; i < list.Count; i++)
            {
                var match = MatchPatern(false, o => o.isConditional, o => o.Op == GMCode.B);
                if(match != null)
                {
                  //  Debug.WriteLine("Meh " + i);
                }
            }
                System.Diagnostics.Debug.WriteLineIf(fix_count > 0, "Amount of peephole fixes: " + fix_count);
        }
        Stack<AST> tree;// = new Stack<AST>(); // used to caculate stuff
        class EnvState
        {
            public string env;
            public int stackSize;
            public EnvState(string env, int stack) { this.env = env;  stackSize = stack; }
        }
        Stack<EnvState> enviroment;// = new Stack<AST>();
        Regex WordTest = new Regex(@"[A-Za-z0-9]+", RegexOptions.Compiled);
        CodeLine processOpcode(Opcode code, int pc, ref int start_pc, ref int last_pc)
        {
            last_pc = pc;
            pc = code.Pc;
            if (code.Op != GMCode.BadOp && start_pc == -1) start_pc = pc;
            CodeLine line = new CodeLine();
            line.code = code;
            line.pc = pc;
            line.startPc = -1;
            switch (code.Op)
            {
                case GMCode.BadOp:
                    break;
                // A hack for push enviroment right here.
                // It SEEMS that push enviroment is set up so we can call a function that is in an instance
                // aka self.myobject.instance_destroy()
                // Sence we know the start and the end of the call, lets see if we can simplfiy this with 
                // recording the start of a push env and back.
                // This might not work well with recursive returns or calls though
                case GMCode.Pushenv:
                    {
                        AST env = tree.Pop();
                        string instance = GetInstance(tree, env.ToString());
                        enviroment.Push(new EnvState(instance, tree.Count));
                    }
                    
                  //  codeLine = "Pushing Enviroment : " + GetInstance(tree, enviroment.Peek().ToString());
                    // codeLine = "Push Enviroment " + tree.Pop().ToString() + "goto on error " + checkLabel(op);
                    break;
                case GMCode.Popenv:
                    {
                        var env = enviroment.Pop();
                        Statements statments = new Statements();
                        while (tree.Count != env.stackSize) statments.Add(tree.Pop());
                        line.ast = new Enviroment(env.env, statments);
                    }
                        break;
                case GMCode.Exit:
                    line.ast = new Constant("Exit");
                    System.Diagnostics.Debug.Assert(tree.Count == 0);
                    break;
                case GMCode.Neg: // sure this is a negitive
                case GMCode.Not:
                // This op is CLEARLY a a not.  It was hard verified after I saw a return call from a function, a conv from a var to a bool
                // and THIS statment doing a bool to a double
                case GMCode.Ret:
                    {
                        AST value = tree.Pop();
                        tree.Push(new UntaryOp(code.Op, tree.Pop()));   
                    }
                    break;
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Rem:
                case GMCode.Mod:
                case GMCode.Add:
                case GMCode.Sub:
                case GMCode.Or:
                case GMCode.And:
                case GMCode.Xor:
                case GMCode.Sal:
                case GMCode.Slt:
                case GMCode.Sle:
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Sgt:
                case GMCode.Sne:

                    ProcessMathOp(tree, code.Raw);
                    break;

                case GMCode.Conv:
                    {
                         GM_Type fromType = (GM_Type)(int)((code.Raw >> 16) & 0xF);
                         GM_Type tooType = (GM_Type)(int)((code.Raw >> 20) & 0xF);
                        tree.Push(new Conv(tree.Pop(), fromType, tooType));  
                    }
                    break;
                case GMCode.Push: // most common.  Used for all math operations
                    {
                        GM_Disam.PushOpcode popcode = code as GM_Disam.PushOpcode;
                        switch (popcode.OperandType)
                        {
                            case GM_Type.Double: tree.Push(new Constant(popcode.OperandValueDouble)); break;
                            case GM_Type.Float: tree.Push(new Constant((float)popcode.OperandValueDouble)); break;
                            case GM_Type.Long: tree.Push(new Constant((long)popcode.OperandValue)); break;
                            case GM_Type.Int: tree.Push(new Constant((int)popcode.OperandValue)); break;
                            case GM_Type.Var: ProcessVarPush(tree, popcode); break;
                            case GM_Type.String:
                                {
                                    string value = cr.stringList[(int)popcode.OperandValue].str;
                                    Match m = WordTest.Match(value); // if its a simple word, we don't need to escape it, still might make it easyer to read howerver
                                    if (m.Value == value) tree.Push(new Constant(value));
                                    else tree.Push(new Constant(GM_Disam.EscapeString(value)));
                                }
                                break;
                            case GM_Type.Short: tree.Push(new Constant((short)popcode.OperandValue)); break;
                            default:
                                throw new Exception("Bad type");
                        }
                        
                    }
                    break;
                case GMCode.Pop:
                    line.ast = ProcessAssignPop(tree, code as GM_Disam.PopOpcode);
                    break;
                case GMCode.Popz: // usally on void funtion returns, so just pop the stack and print it
                    {
                        line.ast = tree.Pop();
                    }
                    break;
                case GMCode.Dup:
                    {
                        short extra = (short)(code.Raw & 0xFFFF); // humm extra data? OR COPY THE TOP TWO ON THE STACK MABE
                        if (extra > 0)
                        {
                            AST top = tree.ElementAt(0);
                            AST second = tree.ElementAt(1);
                            tree.Push(second);
                            tree.Push(top);
                        } else tree.Push(tree.Peek());
                        System.Diagnostics.Debug.Assert(extra == 0 || extra == 1);
                        System.Diagnostics.Debug.Assert(extra == 0);



                        //    tree.Push(tree.First()); // will this work?
                        //   Variable v = tree.First() as Variable;
                        //   tree.First()
                        //   if (v == null) throw new ArgumentException("Dup didn't work, meh");
                        //  tree.Push(v.Dup());
                    }
                    break;
                case GMCode.Break:
                    {
                        line.ast = new Constant(String.Format("break {0}", code.Raw & 0xFFFF)); // have yet to run into this
                    }
                    break;
                case GMCode.Call:
                    ProcessCall(tree, code as GM_Disam.CallOpcode);
                    break;
                default:
                    //      System.Diagnostics.Debug.WriteLine("Not implmented at {0} value {1} valuehex {1:X}", r.Position, op, op);
                    throw new NotImplementedException("Opcode not implmented"); // NO MORE FUCKING AROUND HERE
            }
            //  int startPosition = r.Position;
            if (line.ast != null)
            {
                line.startPc = start_pc;
                start_pc = -1;
                line.endPC = pc;
            }
            return line;
        }

        void DoBranch(Stack<AST> stack, List<CodeLine> lines, OffsetOpcode code, int pc, ref int start_pc, ref int last_pc)
        {
            var last = codes.Last();
            CodeLine line = new CodeLine();
            if (start_pc == -1) start_pc = pc;
            line.code = code;
            line.pc = pc;
            line.startPc = start_pc;
            line.endPC = pc;
            lines.Add(line);
            start_pc = -1;
            switch (code.Op)
            {
                case GMCode.Bf:
                    line.ast  = ProcessBranch(stack, GetLabel(code.Address), false);
                    break;
                case GMCode.Bt:
                    line.ast = ProcessBranch(stack, GetLabel(code.Address), true);
                    break;
                case GMCode.B:
                    Debug.Assert(stack.Count == 0);
                    line.ast = new GotoLabel(GetLabel(code.Address));
                    break;
                default:
                    throw new Exception("Can't be called here");
            }
        }
#if false
        void PurgeExtraLines(List<CodeLine> lines)
        {

        }
        // We find if statments that are single statments and try to consoidate them int a single one
        //  Since we have eveything flattened, photoshop term look it up, we have to use regedit to fix
        // the strings and gotos
        // because of my shit coading and the lack of proper AST processing, we can do this, so ERASE THIS
        // if we ever do proper AST trees
        // can probery make this a class
        // Sigh.  I was SOOOOO CLOSE to scraping the CodeLine object and making proper Statment and AST
        // tree objects.  But this works.  Its either that or properly ASTing labels.  Sigh.
        static Dictionary<string, string> invertIfString = new Dictionary<string, string>()
        {
            {"==","!=" },
              {"!=","==" },
            {">=","<" },
            {"<=",">" },
            {">","<=" },
            {"<",">=" },
            { "!", "" }
        };
        void FixSimpleEnviroment(List<CodeLine> lines)
        {
            Regex findPushEnv = new Regex(@"Pushing\s+Enviroment\s*:\s*(\w+)", RegexOptions.Compiled);
            Regex findPopEnv = new Regex(@"Poping\s+Envorment\s*:\s*(\w+)", RegexOptions.Compiled);
            Regex findInstanceDestroy = new Regex(@"(instance_destroy\(\))", RegexOptions.Compiled);
            int index = 0;
            int start = 0;
            Func<Regex, string> FindMatch = (Regex r) =>
            {
                for (int i = index ; i < lines.Count; i++)
                {
                    if (lines[i].Text == null) continue;
                    Match m = r.Match(lines[i].Text);
                    if (m.Success) {
                        index = i;
                        return m.Groups[1].Value;
                    }
                }
                return null;
            };
            while(index < lines.Count)
            {
                string env = FindMatch(findPushEnv);
                if (env == null) break;
                start = index;
                string destroy = FindMatch(findInstanceDestroy);
                if (destroy == null) continue;
                string endenv = FindMatch(findPopEnv);
                Debug.Assert(env == endenv);
                destroy = env + "." + destroy;
                for (int j = start+1; j <= index; j++)
                {
                    CodeLine c = lines[j];
                    c.Text = null;
                    c.Label = null;
                }
                lines[start].Text = destroy;
            }


        }

        void SimplifyIFStatements(List<CodeLine> lines)
        {
            Regex findLabelRegex = new Regex("Label_\\d+", RegexOptions.Compiled);
            Regex findConditionalStatment = new Regex(@"(?<=if).*?(?=then)", RegexOptions.Compiled);
            Regex replaceLabel = new Regex("goto\\s*Label_\\d+", RegexOptions.Compiled);
            Regex InvertLogic = new Regex(@"==|!=|>=|<=|>|<|!", RegexOptions.Compiled);
            int index = 0;
            string fixed_if = "";
            string currentLabel = "";
            CodeLine current = null;
            CodeLine statment = null;
            int start = 0;
            Func<string, string> ProcessIfStatment = (string func) =>
            {
                Match conditional = findConditionalStatment.Match(func);
                return InvertLogic.Replace(conditional.Value, m => invertIfString[m.Value]);
            };
            Func<CodeLine> FindNextStatment = () =>
            {
                for (int i = index + 1; index < lines.Count; i++)
                {
                    CodeLine c = lines[i];
                    if (c.Text != null && c.Label == null)
                    {
                        if (c.Text.IndexOf("if") == 0)
                        { // if we have another if statment going to the same label right AFTER this one, then lets combine them
                            if (c.Text.IndexOf(currentLabel) != -1)
                            {
                                fixed_if += " && " + ProcessIfStatment(c.Text);
                                continue;
                            }
                            return null;
                        }
                        index = i;
                        return c;
                    }
                }
                return null;
            };
            Func<CodeLine> CheckIfNextisLabel = () =>
            {
                for (int i = index + 1; index < lines.Count; i++)
                {
                    CodeLine c = lines[i];
                    if (c.Text == null && c.Label == null) continue;
                    if (c.Text != null) return null;
                    index = i;
                    return c;
                }
                return null;
            };
            int conditional_max_length = 0;
            for (index = 0; index < lines.Count; index++)
            {
                current = lines[index];
                if (current.Text == null || current.Text.IndexOf("if") != 0) continue; // find the if statement
                start = index;
                fixed_if = ProcessIfStatment(current.Text);
                currentLabel = findLabelRegex.Match(current.Text).Value;
                statment = FindNextStatment();
                if (statment == null) continue;
                CodeLine label = CheckIfNextisLabel();
                if (label == null || label.Label != currentLabel) continue;
                // WE FOUND A MATCH LETS FIX THE FUCKER
                if (fixed_if.Length > conditional_max_length) conditional_max_length = fixed_if.Length;
                fixed_if = "if " + fixed_if + " then " + statment.Text;
                for (int j = start; j <= index; j++)
                {
                    CodeLine c = lines[j];
                    c.Text = null;
                    c.Label = null;
                }

                current.Text = fixed_if;
            }
            string newFormat = "{0,-" + (conditional_max_length + 3) + "} {1}";
            for (index = 0; index < lines.Count; index++)
            {
                current = lines[index];
                if (current.Text == null || current.Text.IndexOf("if") != 0 || current.Text.IndexOf("goto") != -1) continue; // find one of the modified statments
                int thenIndex = current.Text.IndexOf("then");
                string firstpart = current.Text.Substring(0, thenIndex).Trim();
                string secondpart = current.Text.Substring(thenIndex).Trim();
                current.Text = String.Format(newFormat, firstpart, secondpart);
            }
        }
#endif
        void ClearEmptyLines(List<CodeLine> lines)
        {
         
        }
        T DetectNextStatment<T>(List<CodeLine> lines,int index) where T: AST
        {
            for(int i=index+1;i<lines.Count;i++)
            {
                while (lines[i].ast == null) continue;
                T ret = lines[i].ast as T;
                if (ret != null) return ret;
            }
            return null;
        }
        void SimplifyIFStatements(List<CodeLine> lines)
        {
            for (int index = 0; index < lines.Count; index++)
            {
                CodeLine first = lines[index];
                Conditional testIf = first.ast as Conditional;
                if (testIf == null) continue;
                CodeLine next = lines[index + 1];
                Conditional testSecond = next.ast as Conditional;
                if (testSecond != null && (testIf.ifTrue as ASTLabel).Equals(first.label)) {
                    testIf = new Conditional(new MathOp(testIf.Condition, GMCode.And, testSecond.Condition), testSecond.ifTrue);
                    ++index;
                    next = lines[index + 1];
                }
                CodeLine labelCheck = lines[index+2];
                if (labelCheck.label == first.label)
                {
                    
                    index += 2;
                   
                    testIf = new Conditional(testIf.Condition, next.ast);
                    testIf = testIf.Invert(false);
                    next.ast = null;
                    labelCheck.label = null;
                } else continue;
                first.ast = new Statements(testIf);
            }
        }
        void restart(ChunkStream r, int codeOffset, int code_size, string scriptName)
        {
            tree = new Stack<AST>(); // used to caculate stuff
            enviroment = new Stack<EnvState>();
            gotos = new Dictionary<int, ASTLabel>();
            this.scriptName = scriptName;
            GM_Disam disam = new GM_Disam(r);
            this.codes = disam.ReadOpCode(codeOffset, code_size, scriptName);
            // find all the labels first
            foreach (var o in codes)
            {
                if (o.Value.Op == GMCode.B || o.Value.Op == GMCode.Bt || o.Value.Op == GMCode.Bf)
                {
                    int offset = GM_Disam.getBranchOffset(o.Value.Raw);
                    ASTLabel label = new ASTLabel(o.Key + offset, o.Key);
                    gotos.Add(o.Key, label);
                }
            }
        }
        void RemoveAllConv()
        {
            // We don't need them like, at all for this process.  Might as well just remove them

        }
        List<CodeLine> processStream2(ChunkStream r, int codeOffset, int code_size,string scriptName)
        {
            restart(r, codeOffset, code_size, scriptName);
            //  PeepholeFix(); // skip this, lets try something diffrent
            // FIRST lets find all the labels
            List<CodeLine> lines = new List<CodeLine>();
            int limit = (int)(codeOffset + code_size);
            int start_pc = -1, last_pc = 0;
            int end_pc = codes.Last().Key;
            for (int pc = 0; pc < end_pc; pc++) {
                if (!codes.ContainsKey(pc)) continue;
                Opcode code = codes[pc];
                // Ok we are going to do this a bit diffrently, we are going to find a valid statment FIRST, then resolve it
                // so lets see if we can decode 
                switch (code.Op)
                {
                    
                    case GMCode.Bf: // branch if false
                    case GMCode.Bt: // branch if true
                    case GMCode.B:
                         DoBranch(tree, lines, code as OffsetOpcode, pc, ref start_pc, ref last_pc);
                        break;
        
                    default:
                        lines.Add(processOpcode(code, pc, ref start_pc, ref last_pc));
                        continue;
                }
            }
            InsertLabels(lines, end_pc);
            lines.RemoveAll(line => line.ast == null);
        //    SimplifyIFStatements(lines);

            return lines;
        }
    }
}