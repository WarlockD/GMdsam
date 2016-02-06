using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web.UI;
using System.Web;
using System.Runtime.InteropServices;

namespace betteribttest
{
    class Disam
    {
        public static string EscapeString(string s)
        {
            using (var writer = new StringWriter())
            {
                using (var provider = System.CodeDom.Compiler.CodeDomProvider.CreateProvider("CSharp"))
                {
                    provider.GenerateCodeFromExpression(new System.CodeDom.CodePrimitiveExpression(s), writer, null);
                    return writer.ToString();
                }
            }
        }
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
        enum GM_Type : int
        {
            Double = 0,
            Float,
            Int,
            Long,
            Bool,
            Var,
            String,
            Instance,
            Short = 15, // This is usally short anyway
            refrenceInt = 0xFF00, // Used when we are a refrence that refers to a string
            refrenceString,// Used when we are a refrence that refers to a string
            NoType
        }
        enum OpType  : byte
        {
            Conv = 0x03,
            Mul = 0x04,
            Div = 0x05,
            Rem = 0x06,
            Mod = 0x07,
            Add = 0x08,
            Sub = 0x09,
            And = 0x0a,
            Or = 0x0b,
            Xor = 0x0c,
            XNor = 0xd,
            Not = 0x0e,
            Sal = 0x0f,
            Slt = 0x11,
            Sle = 0x12,
            Seq = 0x13,
            Sne = 0x14,
            Sge = 0x15,
            Sgt = 0x16,
            Pop = 0x41,
            Dup = 0x82,
            Ret = 0x9d,
            Exit = 0x9e,
            Popz = 0x9f,
            B = 0xb7,
            Bt = 0xb8,
            Bf = 0xb9,
            Pushenv = 0xbb,
            Popenv = 0xbc,
            Push = 0xc0,
            Call = 0xda,
            Break = 0xff
        }
        static Dictionary<OpType, string> opDecode = new Dictionary<OpType, string>()  {
            {  (OpType)0x03, "conv" },
            {  (OpType)0x04, "mul" },
            {  (OpType)0x05, "div" },
            { (OpType) 0x06, "rem" },
            {  (OpType)0x07, "mod" },
            {  (OpType)0x08, "add" },
            {  (OpType)0x09, "sub" },
            {  (OpType)0x0a, "and" },
            {  (OpType)0x0b, "or" },
            { (OpType) 0x0c, "xor" },
             { (OpType) 0x0d, "com" },
            {  (OpType)0x0e, "not" },
            {  (OpType)0x0f, "sal" },
            {  (OpType)0x10, "sar" },
            { (OpType) 0x11, "slt" },
            { (OpType) 0x12, "sle" },
            { (OpType) 0x13, "seq" },
            {  (OpType)0x14, "sne" },
            {  (OpType)0x15, "sge" },
            {  (OpType)0x16, "sgt" },
            {  (OpType)0x41, "pop" }, // multi place
            {  (OpType)0x82, "dup" },
            {  (OpType)0x9d, "ret" },
            {  (OpType)0x9e, "exit" },
            {  (OpType)0x9f, "popz" },
            {  (OpType)0xb7, "b" },
            {  (OpType)0xb8, "bt" },
            {  (OpType)0xb9, "bf" },
            {  (OpType)0xbb, "pushenv" },
            {  (OpType)0xbc, "popenv" },
            {  (OpType)0xc0, "push" },
            {  (OpType)0xda, "call" },
            {  (OpType)0xff, "break" },
        };
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
        class StackValue // habit, I like unions, sue me
        {
            GM_Type type;
            long lvalue;
            double dvalue;
            string svalue;
            bool noValue;
            public StackValue(StackValue c)
            {
                type = c.type;
                lvalue = c.lvalue;
                dvalue = c.doubelValue;
                noValue = c.noValue;

            }
            public StackValue()
            {
                type = GM_Type.NoType;
                lvalue = 0;
                dvalue = 0.0;
                noValue = false;
            }
            public StackValue(int instance, string var_name) {
                noValue = true;
                svalue = var_name;
                lvalue = instance;
                type = GM_Type.Var;
            }
            public StackValue(string v) { type = GM_Type.String; svalue = v; }
            public StackValue(double v) { type = GM_Type.Double; dvalue = v; }
            public StackValue(float v) { type = GM_Type.Float; dvalue = v; }
            public StackValue(int v) { type = GM_Type.Int; lvalue = v; }
            public StackValue(short v) { type = GM_Type.Int; lvalue = v; } // I have yet to see this NOT be an int on the stack
            public StackValue(bool v) { type = GM_Type.Bool; lvalue = v ? 1 : 0; }
            public StackValue(long v) { type = GM_Type.Long; lvalue = v; }
            public StackValue(GM_Type t) { type = t; }
            public StackValue convert(GM_Type from, GM_Type to)
            {
                if (this.type != from) throw new Exception("Not the same type");
                if (from == to) return new StackValue(this);
                StackValue value = new StackValue(to);
                if (this.noValue)
                {
                    // fake convert
                    value.svalue = this.svalue;
                    value.noValue = true;
                    return value;
                }
                if (this.type == GM_Type.Int || this.type == GM_Type.Long || this.type == GM_Type.Short)
                {
                    if (to == GM_Type.Double || to == GM_Type.Float)
                        value.dvalue = (double)this.lvalue;
                    else if (to == GM_Type.String)
                        value.svalue = this.lvalue.ToString();
                    else if (to == GM_Type.Var)
                    {
                        value.svalue = this.lvalue.ToString();
                        value.lvalue = -10; // we don't know the instance?  Fucntion call?
                        value.noValue = true;
                    }else 
                     throw new NotImplementedException(from + " to " + to);
                }
                else if (this.type == GM_Type.Float || this.type == GM_Type.Double)
                {
                    if (to == GM_Type.Int || to == GM_Type.Long || to == GM_Type.Short)
                        value.lvalue = (long)this.dvalue;
                    else if (to == GM_Type.String)
                        value.svalue = this.dvalue.ToString();
                    else if (to == GM_Type.Var)
                    {
                        value.svalue = this.dvalue.ToString();
                        value.lvalue = (long)this.dvalue;
                        value.noValue = true;
                    }else 
                        throw new NotImplementedException(from + " to " + to);
                }
                else if (this.type == GM_Type.String)
                {
                    if (to == GM_Type.Int || to == GM_Type.Long || to == GM_Type.Short)
                        value.lvalue = int.Parse(this.svalue);
                    else if (to == GM_Type.Double || to == GM_Type.Float)
                        value.dvalue = double.Parse(this.svalue);
                    else if (to == GM_Type.Var)
                    {
                        value.svalue = this.svalue;
                        value.lvalue = this.lvalue;
                        value.noValue = true;
                    }else 
                        throw new NotImplementedException(from + " to " + to);
                }
                return value;
            }
            public bool hasValue { get { return !noValue; } }
            public int instanceValue { get { return (int)lvalue; } }
            public bool boolValue { get { return lvalue != 0; } }
            public int intValue { get { return (int)lvalue; } }
            public double doubelValue { get { return dvalue; } }
            public float floatValue { get { return (float)dvalue; } }
            public long longValue { get { return lvalue; } }
            public bool isValid { get { return type != GM_Type.NoType; } }
            public GM_Type Type { get { return type; } }
            public string valueToString()
            {
                if (noValue) return svalue;
                switch (type)
                {
                    case GM_Type.Int:
                    case GM_Type.Long:
                    case GM_Type.Short:
                        return lvalue.ToString();
                    case GM_Type.Float:
                    case GM_Type.Double:
                        return dvalue.ToString();
                    case GM_Type.Instance:
                        return Disam.lookupInstance((int)lvalue);
                    case GM_Type.String:
                        return svalue;
                    case GM_Type.refrenceInt:
                        return "Ref(" + lvalue + ")";
                    case GM_Type.refrenceString:
                        return "Ref(" + svalue + ")";
                    default:
                        return null;
                }
            }
        }
        class Opcode
        {
            public string opcode_text = "";
            public string operand_text = "";
            public string comment_text = "";
            public long pc = 0;
            public uint op = 0;
            public int operand = 0;
            public StackValue value;
            public string soperand = null;
   
            public override string ToString()
            {
                string str = String.Format("{0,-6} {1,-2:X2} {2,-6:X6} : {3,-9} {4,-9}", pc, op >> 24, op & 0x00FFFFFF, opcode_text, operand_text);
                if (!string.IsNullOrWhiteSpace(comment_text)) str += "  ;  " + comment_text;
                return str;
            }
        }
        List<Opcode> codes;
        public Disam(ChunkReader cr)
        {
            this.cr = cr;
        }
        string innerHTML(string code_name)
        {
            StringWriter sw = new StringWriter();
            HtmlTextWriter wr = new HtmlTextWriter(sw);

            foreach (Opcode o in codes)
            {
                wr.AddAttribute(HtmlTextWriterAttribute.Name, "pc_" + o.pc.ToString());
                if (o.opcode_text == "b" || o.opcode_text == "bt" || o.opcode_text == "bf")
                {
                    uint pc = (uint)(o.pc + o.value.longValue);
                    string url = code_name + ".html#" + pc.ToString();
                    wr.AddAttribute(HtmlTextWriterAttribute.Href, url);
                }
                wr.RenderBeginTag(HtmlTextWriterTag.A);
                wr.Write(HttpUtility.HtmlEncode(o.ToString()));
                wr.RenderEndTag();
                //      wr.WriteBreak();
                wr.WriteLine();
            }
            wr.Flush();
            return sw.ToString();

        }
        public void writeHTMLFile(string code_name)
        {
            // Initialize StringWriter instance.
            StreamWriter s = new StreamWriter(code_name + ".html");
            HtmlTextWriter wr = new HtmlTextWriter(s);

            wr.RenderBeginTag(HtmlTextWriterTag.Html);
            wr.RenderBeginTag(HtmlTextWriterTag.Body);
            wr.WriteFullBeginTag("pre"); // see http://2e2ba.blogspot.com/2009/12/dont-use-renderbegintagpre.html
            wr.Write(innerHTML(code_name));
            wr.WriteEndTag("pre");
            wr.RenderEndTag(); // body
            wr.RenderEndTag(); // html
            wr.Flush();
            wr.Close();

        }
        public void DissasembleEveything()
        {
            foreach (GMK_Code c  in cr.codeList)
            {
              //  MemoryStream ms = new MemoryStream(c.code);
             //   processStream(ms, c.FilePosition.Position);
             //   StreamWriter s = new StreamWriter(c.Name + ".txt");
             //   foreach (Opcode o in codes) s.WriteLine(o.ToString());
             //   s.Close();
            }
        }
        public string TestStreamOutput(string code_name)
        {
            foreach (GMK_Code c in cr.codeList)
            {
                if (c.Name.IndexOf(code_name) != -1)
                {
                    ChunkStream ms = cr.getReturnStream();
                    var lines = processStream2(ms, c.startPosition,c.size);

                    StreamWriter s = new StreamWriter(c.Name + "_new.txt");
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
                    
                    writeHTMLFile(c.Name);
                }
            }
            if (!found) throw new Exception(code_name + " not found");
        }

        public static Dictionary<int, string> typeLookup = new Dictionary<int, string>()
        {
            {  0x00, "double" },
            {  0x01, "float" },
            {  0x02, "int" },
            {  0x03, "long" },
            {  0x04, "bool" },
            {  0x05, "var" },
            {  0x06, "string" },
            {  0x07, "instance" },
            {  0x0f, "short" },
        };



        public string PrintInts(List<int> data)
        {
            string v = "";
            int col = 0;
            foreach (int a in data)
            {
                v += String.Format("{0,6} ", a);
                if (++col > 16)
                {
                    v += "\n";
                    col = 0;
                }

            }
            return v;
        }
        class AST
        {
            public GM_Type Type { get; private set; }
            public AST( GM_Type type) {  this.Type = type; }
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
            public Variable(bool value) : base(GM_Type.Bool) { ivalue = value ? 1:0; Value = value.ToString(); }
            public Variable(string value) : base(GM_Type.String) { Value = svalue = value;  }
            public Variable(string value,GM_Type type) : base(type) { Value = svalue = value; }
            public Variable(int value, string instance) : base(GM_Type.Instance) {
                ivalue = value;
                Value = instance;
            }
            public override string ToString()
            {
                if (this.Type == GM_Type.String)
                    return EscapeString(Value);
                else
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
                            return "(String)(" + EscapeString(next.ToString()) + ")";
                        else
                            return "(String)(" + EscapeString(next.ToString()) + ")";
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
                if(opMathOperation.TryGetValue(Op,out sop))
                {
                    return Left.ToString() + " " + sop + " " + Right.ToString();
                }
                throw new ArgumentException("Cannot find math operation");
            }
        }
        class Assign : AST
        {
            public string Variable { get; private set; }
            public AST Value { get; private set; }
            public Assign(string variable, GM_Type type,  AST value) : base(type) { this.Variable = variable; this.Value = value; }
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

        StackValue getValue(List<Opcode> opcodes, int pos, bool remove)
        {
            Opcode opcode = opcodes[pos];;
            if (opcode.value == null) return null;
            if (remove) opcodes.RemoveAt(pos);
            return opcode.value;
        }

        StackValue getValue(List<Opcode> opcodes)
        {
            return getValue(opcodes, opcodes.Count - 1, true);
        }
        List<StackValue> getValues(List<Opcode> opcodes, int count) // get the last whatever values, return null and don't mess up the stack if not
        {
            List<StackValue> values = new List<StackValue>();
            int removeAt = opcodes.Count - 1;
            for (int i = 0, pos = opcodes.Count - 1; i < count; i++, removeAt--)
            {
                StackValue value= getValue(opcodes, removeAt, false);
                if (value == null) return null;
                values.Add(value);
            }
             while (count > 0) { opcodes.RemoveAt(opcodes.Count - 1); count--; }
          //  opcodes.RemoveRange(removeAt + 1, count);
            return values;
        }
        string FormatAssign(string sinstance, string var_name, int index, StackValue value)
        {
            return String.Format("{0}.{1}[{2}] = {3}", sinstance, var_name, index, value.valueToString());
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
        string FormatAssign(string sinstance, string var_name, string sindex, StackValue value)
        {
            return String.Format("{0}.{1}[{2}] = {3}", sinstance, var_name, sindex, value.valueToString());
        }
        string FormatAssign(string sinstance, string var_name, StackValue value)
        {
            return String.Format("{0}.{1} = {2}", sinstance, var_name, value.valueToString());
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
        public string decodePushOrPop(int operand)
        {
            string name = decodeCallName(operand);
            int load_type = ((operand >> 24) & 0xFF);
            string sload_type = null;
            switch (load_type)
            {
                case 0:
                    sload_type = "array";
                    break;
                case 0xA0:
                    sload_type = "assign";
                    break;
                case 0x80:
                    sload_type = "unknown";
                    break;
                default:
                    throw new NotImplementedException("Unknown load type");
            }
            return "( LoadType: " + sload_type + " Name: " + name + ")";
        }
        // The new Fangled method
        void ChangeOperation(List<Opcode> codes, string operation) {


        }
        void ProcessVarPush(Stack<AST> tree, uint op, int operand) {
            int instance = (short)(op & 0xFFFF);
            string sinstance = lookupInstance(instance);
            int load_type = (operand >> 24) & 0xFF;
            string var_name = decodeCallName(operand & 0x00FFFFFF);
            if (load_type == 0xA0) // simple varable, usally an int 
            {
                if (sinstance == "stack" )
                {
                    AST value = tree.Pop();
                    Variable valueVar = value as Variable;
                    if (valueVar != null)
                        sinstance = lookupInstance((int)valueVar.IValue);
                    else
                        sinstance = value.ToString();
                    tree.Push(new Variable(sinstance + "." + var_name, GM_Type.Var));
                    return;
                } else if(sinstance == "self")
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
                // ret = sinstance + "." + var_name + " (Load: 0x80)";
                //  opcode.value = new StackValue(instance, ret);
            }
            throw new NotImplementedException("Push.V convert not implmented"); // we are going to handle it all, no exceptions this time around
        }
        string ProcessAssignPush(Stack<AST> tree, uint op,  int operand)
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
        void ProcessCall(Stack<AST> tree, uint op, int operand)
        {
            //  byte return_type = (byte)((op >> 16) & 0xFF); // always i
            // string return_type_string = typeLookup[return_type];
            int arg_count = (ushort)(op & 0xFFFF);
            string func_name = decodeCallName(operand);

            List<AST> args = new List<AST>();
            for (int i = 0; i < arg_count; i++) args.Add(tree.Pop());
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
            s += "then goto " + gotoLabel;
            return s;
        }
        class CodeLine
        {
            public string Text=null;
            public string Label = null;
            public uint op = 0;
            public int operand = 0;
            public int pc = 0;
            public long startPc = 0;
            public long endPC = 0;
   
            public string DecompileLine()
            {
                if (this.Text == null && this.Label == null) return null;
                string s = String.Format("{0,-10} {1}", this.Label == null ? "" : this.Label+":", this.Text == null ? "" : this.Text);
                return s;
            }
            public override string ToString()
            {
                return Label != null ? Label + " : " + Text : Text;
            }
        }
        static int getBranchOffset(uint op)
        {
            op &= 0x00FFFFFF;
            if ((op & 0x800000) != 0) op |= 0xFF000000;
            return (int)op;
        }
        List<CodeLine> processStream2(ChunkStream r, int codeOffset, int code_size)
        {
            r.PushSeek((int)codeOffset);
            int limit = (int)(codeOffset + code_size);
            Stack<AST> tree = new Stack<AST>(); // used to caculate stuff
            List<CodeLine> lines = new List<CodeLine>();
            Dictionary<int, string> gotos = new Dictionary<int, string>();
            int len = (int)r.Length / 4;
            int startPos = r.Position;
            string codeLine = null;
            int pc = 0;
            int last_pc = 0;
            int start_pc = pc;
            Func<uint, string> checkLabel = (value) =>
              {
                  value &= 0x00FFFFFF;
                  if ((value & 0x800000) != 0) value |= 0xFF000000;
                  int offset = pc + (int)value;
                  string label;
                  if (gotos.TryGetValue(offset, out label)) return label;
                  return gotos[offset] = ("Label_" + gotos.Count);
              };
            uint op = 0;
            //     int shit_header = r.ReadInt32();
            while (r.Position != limit)
            {
                CodeLine line = new CodeLine();
                last_pc = pc;
                line.pc = pc = (r.Position - codeOffset) / 4;
                line.op = op = r.ReadUInt32();
                OpType code = (OpType)((byte)(op >> 24));
                switch (code)
                {
                    case OpType.Pushenv:

                    case OpType.Popenv:

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
                        ProcessMathOp(tree, op);
                        break;
                    case OpType.Bf: // branch if false
                        codeLine = ProcessBranch(tree, checkLabel(op), false);
                        break;
                    case OpType.Bt: // branch if false
                        codeLine = ProcessBranch(tree, checkLabel(op), true);
                        break;
                    case OpType.B: // branch if false
                        codeLine = "goto " + checkLabel(op);
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
                            Variable v = tree.First() as Variable;
                            if (v == null) throw new ArgumentException("Dup didn't work, meh");
                            tree.Push(v.Dup());
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
                if (codeLine != null)
                {
                    int endPos = last_pc == start_pc ? start_pc : last_pc - 1;
                    line.startPc = pc;
                    line.endPC = endPos;
                    line.Text = codeLine;
                    codeLine = null;
                    start_pc = pc;
                }
                lines.Add(line);
            }
            r.PopPosition();
            foreach (CodeLine line in lines)
            {
                string label;
                if (gotos.TryGetValue(line.pc, out label)) line.Label = label;
            }
            return lines;
        }
    }
}
