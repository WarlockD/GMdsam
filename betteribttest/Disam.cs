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
            Variable,
            String,
            Instance,
            Error = 15, // This is usally short anyway
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
                type = GM_Type.Variable;
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
                if (this.type == GM_Type.Int || this.type == GM_Type.Long || this.type == GM_Type.Error)
                {
                    if (to == GM_Type.Double || to == GM_Type.Float)
                        value.dvalue = (double)this.lvalue;
                    else if (to == GM_Type.String)
                        value.svalue = this.lvalue.ToString();
                    else if (to == GM_Type.Variable)
                    {
                        value.svalue = this.lvalue.ToString();
                        value.lvalue = -10; // we don't know the instance?  Fucntion call?
                        value.noValue = true;
                    }else 
                     throw new NotImplementedException(from + " to " + to);
                }
                else if (this.type == GM_Type.Float || this.type == GM_Type.Double)
                {
                    if (to == GM_Type.Int || to == GM_Type.Long || to == GM_Type.Error)
                        value.lvalue = (long)this.dvalue;
                    else if (to == GM_Type.String)
                        value.svalue = this.dvalue.ToString();
                    else if (to == GM_Type.Variable)
                    {
                        value.svalue = this.dvalue.ToString();
                        value.lvalue = (long)this.dvalue;
                        value.noValue = true;
                    }else 
                        throw new NotImplementedException(from + " to " + to);
                }
                else if (this.type == GM_Type.String)
                {
                    if (to == GM_Type.Int || to == GM_Type.Long || to == GM_Type.Error)
                        value.lvalue = int.Parse(this.svalue);
                    else if (to == GM_Type.Double || to == GM_Type.Float)
                        value.dvalue = double.Parse(this.svalue);
                    else if (to == GM_Type.Variable)
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
                    case GM_Type.Error:
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
                MemoryStream ms = new MemoryStream(c.code);
                processStream(ms, c.FilePosition.Position);
                StreamWriter s = new StreamWriter(c.Name + ".txt");
                foreach (Opcode o in codes) s.WriteLine(o.ToString());
                s.Close();
            }
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
        class OpcodeDecode
        {
            public uint op;
            public OpcodeDecode(uint op) { this.op = op; }
        }
        class Opcode_Push : OpcodeDecode
        {
            public int instance;
            public int operand;
            public int load_type;
            public GM_Type push_type;

            public Opcode_Push(uint op, int operand) : base(op)
            {
                this.operand = operand & 0x00FFFFFF;
                this.load_type = operand >> 24;
                this.instance = (int)(op & 0xFFFF);
                this.push_type = (GM_Type)((op >> 16) & 0xFF);
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
        string ProcessAssign(string var_name, int load_type, GM_Type convertFrom, GM_Type convertTo, int instance, List<Opcode> opcodes)
        {
            Queue<StackValue> valueStack = new Queue<StackValue>(); // its a queue as we have to reverse the order
            // Lets do the simplest conversion first  int -> ref ( integer being assigned to a value of an object aka global.bob = 4;
            string sinstance = lookupInstance(instance);
            if (load_type == 0xA0 && (sinstance == "global" || sinstance == "self") && convertFrom != GM_Type.Variable && convertTo == GM_Type.Variable)
            {
                StackValue value = getValue(opcodes);
                if (value == null) return null;
                return FormatAssign(sinstance, var_name, value);
            }
            if (load_type == 0xA0 && (sinstance == "global" || sinstance == "self") && convertFrom == GM_Type.Variable && convertTo == GM_Type.Variable) // usally a function return, assigning to an object
            {
                Opcode lastOp = opcodes.Last();
                if (lastOp.opcode_text == "call") // last was a call
                {
                    opcodes.RemoveAt(opcodes.Count - 1);
                    return FormatAssign(sinstance, var_name, lastOp.operand_text);
                } else
                {
                    StackValue value = getValue(opcodes);
                    if (value == null) return null;
                    return FormatAssign(sinstance, var_name, value);
                }
            }
            // load_type 0 is an array and instance is on the stack
            if (load_type == 0 && (sinstance == "stack"))
            {
                List<StackValue> values = getValues(opcodes, 3);
                if (values == null) return null;
                // remember, values is reversed
                string sindex = values[0].valueToString();
                sinstance = lookupInstance(values[1].intValue);
                StackValue value = values[2];
                return FormatAssign(sinstance, var_name, sindex, value);
            }
            return null;
            // zero seems to be an array assign
        }
        string ProcessVariable(string var_name, int load_type, int instance, Opcode opcode, List<Opcode> opcodes)
        {
            string sinstance = lookupInstance(instance);
            string ret = null;
            if (load_type == 0xA0) // simple varable, usally an int 
            {
                if (sinstance == "stack")
                {
                    StackValue value = getValue(opcodes);
                    if (value == null) return null;
                    if (value.hasValue)
                        sinstance = lookupInstance(value.intValue);
                    else
                        sinstance = value.valueToString();
                    sinstance = lookupInstance(value.intValue);
                    ret = sinstance + "." + var_name;
                } else
                {
                    ret = sinstance + "." + var_name;
                    StackValue value = new StackValue(instance, ret);
                    opcode.value = value;
                }
            }
            if (load_type == 0 && sinstance == "stack") // instance is on the stack and its an array
            {
                List<StackValue> values = getValues(opcodes, 2);
                if (values == null) return null; // can't read the stack for some reason
                string sindex = values[0].valueToString();
                if (values[1].hasValue)
                    sinstance = lookupInstance(values[1].intValue);
                else
                    sinstance = values[1].valueToString();

                ret = sinstance + "." + var_name + "[" + sindex + "]";
                opcode.value = new StackValue(instance, ret);
            }
            if (load_type == 0x80) // not sure what this is
            {
                ret = sinstance + "." + var_name + " (Load: 0x80)";
                opcode.value = new StackValue(instance, ret);
            }

            if (ret == null)
                throw new NotImplementedException("Push.V convert not implmented");
            return ret;
        }
        public string decodeCallName(int operand)
        {
            int string_ref = (int)(operand & 0x0FFFFFFF); // this COULD be 24 bits?
            if (string_ref < cr.stringList.Count) return cr.stringList[string_ref].str;
            else return "NOT FOUND: " + string_ref.ToString();
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


        public void processStream2(Stream f, long codeOffset)
        {
            f.Position = 0;
            BinaryReader r = new BinaryReader(f);
            codes = new List<Opcode>();
            Stack<Opcode> exprs = new Stack<Opcode>(); // used to caculate stuff
            int len = (int)f.Length / 4;
            long startPos = f.Position;

            while (f.Position != f.Length)
            {
                Opcode info = new Opcode();
                long pc = info.pc = (f.Position / 4);
                uint op = info.op = r.ReadUInt32();
                OpType code = (OpType)((byte)(op >> 24));
                if (!opDecode.TryGetValue(code, out info.opcode_text))
                {
                    info.opcode_text = String.Format("E{0:X2}", (byte)code);
                    codes.Add(info);
                    continue; //skip it
                }
                if ((byte)code <= 0x16)
                {
                    string operation = opMathOperation[code];
                    int topType = (int)((op >> 16) & 0xF);
                    int secondType = (int)((op >> 20) & 0xF);
                    List<StackValue> values = getValues(codes, 2);
                    if (values == null) throw new Exception("Stack missmatch");
                    info.opcode_text = String.Format("{0} {1} {2}", values[0].valueToString(), operation, values[1].valueToString());
                    info.value = new StackValue(-6, info.opcode_text);
                } else switch (code)
                    {
                        case OpType.Pop:
                            {
                                int topType = (int)((op >> 20) & 0xF);
                                int secondType = (int)((op >> 16) & 0xF);
                                int instance = (short)(op & 0xFFFF);
                                string sinstance = lookupInstance(instance);
                                int func = r.ReadInt32();
                                int object_var_type = func >> 24 & 0xFF; // I think this might only be 4 bits
                                string varname = decodeCallName(func);
                                info.opcode_text = ProcessAssign(varname, object_var_type, (GM_Type)topType, (GM_Type)secondType, instance, codes);
                                if (info.opcode_text == null) throw new Exception("Fix me");
                                info.value = null; // we are a setter, not a value
                            }
                            break;
                        case OpType.Dup:
                        case OpType.Ret:
                        case OpType.Exit:
                        case OpType.Popz:

                            info.opcode_text = String.Format("{0}", typeLookup[(int)((op >> 16) & 0xFF)]);
                            break;
                        case OpType.B:
                        case OpType.Bt:
                        case OpType.Bf:
                        case OpType.Pushenv:
                        case OpType.Popenv:
                        case OpType.Call:
                        case OpType.Break:
                        default:
                            throw new NotImplementedException("Ugh");
                    }

            }
        }
        public void processStream(Stream f,long codeOffset)
        {
            f.Position = 0;
            BinaryReader r = new BinaryReader(f);
            int len = (int)f.Length / 4;
            codes = new List<Opcode>(len);
            string scode;
            string soperand;
            string scomment;
            long startPos = f.Position;
            while (f.Position != f.Length)
            {
                Opcode info = new Opcode();
                long startOpPos = f.Position;
                uint op  = r.ReadUInt32();
                OpType opcode = (OpType)((byte)(op >> 24));
                soperand = "";
                scomment = "";
                // scode = opDecode[opcode];
                if (!opDecode.TryGetValue(opcode, out scode)) scode = String.Format("E{0:X2}", (byte)opcode);
               
               else  if ((byte)opcode <= 0x16)
                {
                    int topType = (int)((op >> 16) & 0xF);
                    int secondType = (int)((op >> 20) & 0xF);
                    if(scode == "com") // still not sure what this does
                    {
                        string mathop = opMathOperation[opcode];
                        StackValue value = getValue(codes);
                        if (value != null) // not sure about the stack here
                        {
                            soperand = String.Format("{0}({1})", mathop, value.valueToString());
                            info.value = new StackValue(-5, soperand);
                        }
                        else
                        {
                            soperand = String.Format("Types: {0}, {1}", typeLookup[topType], typeLookup[secondType]);
                            info.value = new StackValue(0, "Broken " + scode + " at " + startOpPos);
                        }
                    }
                    else if (scode == "conv") // going to try to get rid of extra pushes in the list
                    {
                        Opcode last = codes.Last();
                        OpType last_opcode = (OpType)((byte)(last.op >> 24));
                        if (last.value != null)
                        {
                            // ignore this, convert the push
                            last.value = last.value.convert((GM_Type)topType, (GM_Type)secondType);
                            continue;
                        }
                        else throw new Exception("Bad stack?");
                    }
                    else
                    {
                        string mathop = opMathOperation[opcode];
                        List<StackValue> values = getValues(codes, 2);
                        if(values != null) // not sure about the stack here
                        {
                            soperand = String.Format("{0} {1} {2}", values[0].valueToString(), mathop, values[1].valueToString());
                            info.value = new StackValue(-5, soperand);
                        } else
                        {
                            soperand = String.Format("Types: {0}, {1}", typeLookup[topType] , typeLookup[secondType]);
                            info.value = new StackValue(0,"Broken " + scode + " at " + startOpPos);
                        }
                        
                        //   Opcode left = codes[codes.Count - 2];
                        //   Opcode right = codes[codes.Count - 2];
                        //  soperand = String.Format("{0} {1} {2}", typeLookup[topType], mathop, typeLookup[secondType]);

                    }
                        
                }
                else switch (scode)
                    {
                        case "dup": // we just copy the last opcode on the stack, prey this dosn't fuck up
                            {
                                Opcode last = codes.Last();
                                info.value = last.value.convert(last.value.Type, (GM_Type)(int)((op >> 16) & 0xFF));
                            }
                            break;
                        case "ret":
                        case "exit":
                        case "popz":
                            soperand = String.Format("{0}", typeLookup[(int)((op >> 16) & 0xFF)]);
                            break;
                        case "b":
                        case "bt":
                        case "bf":
                        case "pushenv":
                            op &= 0x00FFFFFF;
                            if ((op & 0x800000) != 0) op |= 0xFF000000;
                            info.value = new StackValue((int)op);
                            soperand = String.Format("{0}", (int)op + (startOpPos/4)+1);
                            break;
                        case "popenv":
                            soperand = String.Format("{0} --> {1} ; (unknown, to pushenv?", (op >> 16) & 0xFF, (short)(op & 0xFFFF));
                            break;
                        case "pop":
                            {
                                int topType = (int)((op >> 20) & 0xF);
                                int secondType = (int)((op >> 16) & 0xF);
                                int instance = (short)(op & 0xFFFF);
                                string sinstance = lookupInstance(instance);
                                int func = r.ReadInt32();
                                int object_var_type = func >> 24 & 0xFF; // I think this might only be 4 bits
                                string varname = decodeCallName(func);
                                soperand = ProcessAssign(varname, object_var_type, (GM_Type)topType, (GM_Type)secondType, instance, codes);
                                if (soperand != null) scode = "assign";
                                else soperand = String.Format("{0} -> {1} ({2} {3}", typeLookup[topType], typeLookup[secondType], lookupInstance(instance), decodePushOrPop(func));       
                            }
                            break;
                        case "push":
                            byte t = (byte)(op >> 16);
                            // string ts = typeLookup[t];

                            switch (t)
                            {
                                case 0x0:
                                    {
                                        double operand = r.ReadDouble();
                                        scode += ".d";
                                        info.value = new StackValue(operand);
                                        soperand = String.Format("{0}", operand);
                                    }
                                    break;
                                case 0x1:
                                    {
                                        float operand = r.ReadSingle();
                                        scode += ".f";
                                        info.value = new StackValue(operand);
                                        soperand = String.Format("{0}", operand);
                                    }
                                    break;
                                case 0x2:
                                    {
                                        int operand = r.ReadInt32();
                                        scode += ".i";
                                        info.value = new StackValue(operand);
                                        soperand = String.Format("{0}", operand);
                                    }
                                    break;
                                case 0x3:
                                    {
                                        long operand = r.ReadInt64();
                                        scode += ".l";
                                        info.value = new StackValue(operand);
                                        soperand = String.Format("{0}", operand);
                                    }
                                    break;
                                case 0x5: // need to function this
                                    {
                                        int instance = (short)(op & 0xFFFF);
                                        string sinstance = lookupInstance(instance);
                                        int func = r.ReadInt32();
                                        int load_type = (func >> 24) & 0xFF;
                                        string varname = decodeCallName(func);
                                        scode += ".v";
                                        soperand = ProcessVariable(varname, load_type, instance, info, codes);   
                                        info.operand = func;
                                    }
                                    break;
                                case 0x6: // string hum
                                    {
                                        int operand = r.ReadInt32();
                                        scode += ".s";
                                        info.value = new StackValue(cr.stringList[(int)operand].escapedString);
                                        info.soperand = cr.stringList[(int)operand].escapedString;
                                        soperand = String.Format("{0}", info.soperand);
                                    }
                                    break;
                                case 0xF:
                                    {
                                        short operand = (short)(op & 0xFFFF);
                                        scode += ".e";
                                        info.value = new StackValue(operand);
                                        soperand = String.Format("{0}", operand);
                                    }
                                    break;
                                default:
                                    throw new Exception("Bad type");
                            }
                            break;
                        case "call":
                            {
                                //  byte return_type = (byte)((op >> 16) & 0xFF); // always i
                                // string return_type_string = typeLookup[return_type];
                                int args = (ushort)(op & 0xFFFF);
                                int fuc = r.ReadInt32();
                                soperand = decodeCallName(fuc);
                                List<StackValue> values = getValues(codes, args);
                                if (values == null) soperand += "(? " + args + ")";
                                else // now the magic happens
                                {
                                    soperand += "(";
                                    for (int i=0;i<args;i++)
                                    {
                                        soperand += values[i].valueToString();
                                        if (i != (args - 1)) soperand += ",";
                                    }
                                    soperand += ")";
                                }
                                info.value = new StackValue(-10, soperand);

                            }
                            break;
                        case "break":
                            {
                                int brkint = (ushort)(op & 0xFFFF);
                                soperand = String.Format("{0}", brkint);
                            }
                            break;

                    }
                info.pc = (startOpPos - startPos)/4;
                info.op = op;
                info.opcode_text = scode;
                info.operand_text = soperand;
                info.comment_text = scomment;
                this.codes.Add(info);
            }
            r = null;
            f.Close();
            f = null;
          //  System.Diagnostics.Debug.Write(line);
        }
    }
}
