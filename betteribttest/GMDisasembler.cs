using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace betteribttest
{
    interface GMInfo
    {
        /// <summary>
        /// This Property is the string offsets for looking up a string by the offset in the file.
        /// This is the offset AFTER the Int32 size value
        /// </summary>
        string LookupStringByOffset(int offset);
        /// <summary>
        ///  Look up a string by the index it was read in the data.win file.
        /// </summary>
        /// <param name="index"></param>
        /// <returns>Name of string</returns>
        string LookupStringByIndex(int index);
        /// <summary>
        /// Look up function name by index read in the file
        /// </summary>
        /// <param name="index"></param>
        /// <returns>Name of Funtion</returns>
        string LookupFunctionNameByIndex(int index);
        /// <summary>
        /// Look up object by index.  This usally works with permainstanced objects
        /// </summary>
        /// <param name="index"></param>
        /// <returns>Name of Object</returns>
        string LookupObjectByIndex(int index);
        /// <summary>
        /// Look up an instance number by value, includes the static stuff
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        string LookupInstanceByValue(int value);

    }

    public enum GM_Type : byte
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
        NoType
    }
    public enum GMCode : byte
    {
        BadOp = 0x00, // used for as a noop, mainly for branches hard jump location is after this
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
        Neg = 0xd,
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
    class DismLabel
    {
        public string Name;
        public List<int> CalledFrom;
        public int Target;
        public DismLabel(string name, int target, int pc)
        {
            Name = name;
            CalledFrom = new List<int>(); CalledFrom.Add(pc);
            Target = target;
        }
    }
    public static class GMCodeUtil
    {
        public static int getBranchOffset(uint op)
        {
            if ((op & 0x800000)!=0) op |= 0xFF000000; else op &= 0x00FFFFFF;
            return (int)(op);
        }
        public readonly static Dictionary<GMCode, int> opMathOperationCount = new Dictionary<GMCode, int>()  {
            {  (GMCode)0x04,2},
            {  (GMCode)0x05, 2},
            { (GMCode) 0x06, 2},
            {  (GMCode)0x07, 2 },
            {  (GMCode)0x08, 2},
            {  (GMCode)0x09, 2 },
            {  (GMCode)0x0a, 2 },
            {  (GMCode)0x0b, 2 },
            { (GMCode) 0x0c, 2 },
            { (GMCode) 0x0d,1},
            {  (GMCode)0x0e, 1 },

            {  (GMCode)0x0f, 2},
            {  (GMCode)0x10, 2 },
            { (GMCode) 0x11, 2},
            { (GMCode) 0x12, 2 },
            { (GMCode) 0x13, 2},
            {  (GMCode)0x14, 2},
            {  (GMCode)0x15, 2},
            {  (GMCode)0x16, 2 },
        };
        public static int getOpTreeCount(this GMCode t)
        {
            int count;
            if (opMathOperationCount.TryGetValue(t, out count)) return count;
            return 0;
        }
        public static string getOpTreeString(this GMCode t)
        {
            string  ret;
            if (opMathOperation.TryGetValue(t, out ret)) return ret;
            return null;
        }
        public static GMCode getInvertedOp(this GMCode t)
        {
            switch(t)
            {
                case GMCode.Sne: return GMCode.Seq;
                case GMCode.Seq: return GMCode.Sne;
                case GMCode.Sgt: return GMCode.Sle;
                case GMCode.Sge: return GMCode.Slt;
                case GMCode.Slt: return GMCode.Sge;
                case GMCode.Sle: return GMCode.Sgt;
                default:
                    return GMCode.BadOp;
            }
        }
        public readonly static Dictionary<GMCode, string> opMathOperation = new Dictionary<GMCode, string>()  {
            {  (GMCode)0x03, "conv" },
            {  (GMCode)0x04, "*" },
            {  (GMCode)0x05, "/" },
            { (GMCode) 0x06, "rem" },
            {  (GMCode)0x07, "%" },
            {  (GMCode)0x08, "+" },
            {  (GMCode)0x09, "-" },
            {  (GMCode)0x0a, "&" },
            {  (GMCode)0x0b, "|" },
            { (GMCode) 0x0c, "^" },
            { (GMCode) 0x0d, "~" },
            {  (GMCode)0x0e, "!" },

            {  (GMCode)0x0f, "<<" },
            {  (GMCode)0x10, ">>" },
            { (GMCode) 0x11, "<" },
            { (GMCode) 0x12, "<=" },
            { (GMCode) 0x13, "==" },
            {  (GMCode)0x14, "!=" },
            {  (GMCode)0x15, ">=" },
            {  (GMCode)0x16, ">" },
        };
        public static Dictionary<int, string> instanceLookup = new Dictionary<int, string>()
        {
            {  0 , "stack" },
            {  -1, "self" },
            {  -2, "other" },
            {  -3, "all" },
            {  -4, "noone" },
            {  -5, "global" },
        };
        public static string lookupInstance(short instance)
        {
            string ret;
            if (instanceLookup.TryGetValue(instance, out ret)) return ret;
            else return String.Format("%{0}%", instance);
        }
        public static string lookupInstanceFromRawOpcode(uint opcode)
        {
            return lookupInstance((short)(opcode & 0xFFFF));
        }
        public static bool IsArrayPushPop(uint opcode)
        {
            return (opcode & 0x8000000) == 0;
        }
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
        public static GMCode getFromRaw(uint raw)
        {
            return (GMCode)((raw >> 24) & 0xFF);
        }
        public static string GetName(this GMCode code)
        {
            return code.ToString().ToLowerInvariant().TrimStart('_').Replace('_', '.');
        }
        public static int GetSize(this GM_Type t)
        {
            switch (t)
            {
                case GM_Type.Double:
                case GM_Type.Long:
                    return 2;
                case GM_Type.Short:
                    return 0;
                default:
                    return 1;
            }
        }
        public static int OpCodeSize(uint raw)
        {
            switch ((GMCode)(raw>>24))
            {
                case GMCode.Pop:
                case GMCode.Call:
                    return 2;
                case GMCode.Push:
                    return((GM_Type)((raw >> 16) & 0xF)).GetSize() + 1;
                default:
                    return 1;
            }
        }
        public static string TypeToStringPostfix(int type) { return TypeToStringPostfix((GM_Type)(type & 15)); }
        public static string TypeToStringPostfix(this GM_Type t)
        {
            switch (t)
            {
                case GM_Type.Double: return ".d";
                case GM_Type.Float: return ".f";
                case GM_Type.Int: return ".i";
                case GM_Type.Long: return ".l";
                case GM_Type.Bool: return ".b";
                case GM_Type.Var: return ".v";
                case GM_Type.String: return ".s";
                case GM_Type.Short: return ".e";
            }
            throw new Exception("Not sure how we got here");
        }
        public static bool IsConditionalControlFlow(this GMCode code)
        {
            return code == GMCode.Bt || code == GMCode.Bf;
        }
        public static bool IsUnconditionalControlFlow(this GMCode code)
        {
            return code == GMCode.B;
        }
        public static bool IsConditional(this GMCode code)
        {
            return code == GMCode.Bt || code == GMCode.Bf|| code == GMCode.B;
        }
        public static bool IsConditionalStatment(this GMCode code)
        {
            switch(code)
            {
                case GMCode.Seq:
                case GMCode.Sne:
                case GMCode.Sge:
                case GMCode.Sle:
                case GMCode.Sgt:
                case GMCode.Slt:
                    return true;
                default:
                    return false;

            }
        }
    }
    class GMVariable
    {
        public string Name;
        public GM_Type Type;
        public int Instance;
    }
    class ByteCode
    {

        public DismLabel Label = null;
        public uint Raw = 0;
        public GMCode Code = GMCode.BadOp;
        public int Pc = 0;
        public int Size = 0;
        public object Operand = null;
        public int? PopCount = null;
        public int PushCount = 0;
        public ByteCode Next = null;
        public string Name { get { return "IL_" + this.Pc.ToString("4"); } }
        public bool IsVariableDefinition
        {
            get
            {
                return (this.Code == GMCode.Pop);
            }
        }
        //  public StackSlot[] StackBefore;     // Unique per bytecode; not shared
        //  public VariableSlot[] VariablesBefore; // Unique per bytecode; not shared
        //  public List<ILVariable> StoreTo;         // Store result of instruction to those AST variables
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this.Name);
            sb.Append(':');
            if (Label == null)
                sb.Append('*');
            else
                sb.Append(' ');
            sb.Append(this.Code.GetName());
            sb.Append(this.Code.GetName());

            if (this.Operand != null)
            {
                sb.Append(' ');
                // if (this.Operand.GetType().IsValueType) sb.Append(Operand.ToString());
                //   else if(Operand is DismLabel) sb.Append((Operand as ))
                if (Operand is string) sb.Append(GM_Disam.EscapeString(Operand.ToString()));
                else sb.Append(Operand.ToString());
            }
            return sb.ToString();
        }
    }
    class GM_Disam
    {
        public SortedDictionary<int, DismLabel> Labels = new SortedDictionary<int, DismLabel>();
        DismLabel getNewLabel(string name, int target, int pc)
        {
            DismLabel l;
            if (!Labels.TryGetValue(target, out l))
            {
                l = new DismLabel("Label_" + Labels.Count, target, pc);
                Labels.Add(target, l);
            }
            else l.CalledFrom.Add(pc);
            return l;
        }
        string scriptName;
        static Dictionary<GMCode, string> opDecode = new Dictionary<GMCode, string>()  {
            {  (GMCode)0x03, "conv" },
            {  (GMCode)0x04, "mul" },
            {  (GMCode)0x05, "div" },
            { (GMCode) 0x06, "rem" },
            {  (GMCode)0x07, "mod" },
            {  (GMCode)0x08, "add" },
            {  (GMCode)0x09, "sub" },
            {  (GMCode)0x0a, "and" },
            {  (GMCode)0x0b, "or" },
            { (GMCode) 0x0c, "xor" },
             { (GMCode) 0x0d, "neg" },
            {  (GMCode)0x0e, "not" },
            {  (GMCode)0x0f, "sal" },
            {  (GMCode)0x10, "sar" },
            { (GMCode) 0x11, "slt" },
            { (GMCode) 0x12, "sle" },
            { (GMCode) 0x13, "seq" },
            {  (GMCode)0x14, "sne" },
            {  (GMCode)0x15, "sge" },
            {  (GMCode)0x16, "sgt" },
            {  (GMCode)0x41, "pop" }, // multi place
            {  (GMCode)0x82, "dup" },
            {  (GMCode)0x9d, "ret" },
            {  (GMCode)0x9e, "exit" },
            {  (GMCode)0x9f, "popz" },
            {  (GMCode)0xb7, "b" },
            {  (GMCode)0xb8, "bt" },
            {  (GMCode)0xb9, "bf" },
            {  (GMCode)0xbb, "pushenv" },
            {  (GMCode)0xbc, "popenv" },
            {  (GMCode)0xc0, "push" },
            {  (GMCode)0xda, "call" },
            {  (GMCode)0xff, "break" },
        };
        public static int getBranchOffset(uint op)
        {
            if (((op<<8) & 0x80000000) != 0) op |= 0xFF000000;
            return (int)(op);
        }
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
        public static Dictionary<GM_Type, string> gmTypeLookup = new Dictionary<GM_Type, string>()
        {
            {  GM_Type.Double, "double" },
            {  GM_Type.Float, "float" },
            {  GM_Type.Int, "int32" },
            {  GM_Type.Long, "int64" },
            {  GM_Type.Bool, "bool" },
            {  GM_Type.Var, "var" },
            {  GM_Type.String, "string" },
            {  GM_Type.Instance, "instance" },
            {  GM_Type.Short, "int16" },
        };
        public interface TypeOpcodeInterface
        {
            GM_Type FirstType { get; }
            GM_Type SecondType { get; }
        }
        public interface OffsetOpcodeInterface
        {
            int Offset { get; }
        }
        public class Opcode : IEquatable<GMCode>
        {
            public int Compare(Opcode a, Opcode b)
            {
                return a.Pc.CompareTo(b.Pc);
            }
            public GMCode Op { get;  set; }
            public uint Raw { get; private set; }
            public int Pc { get; private set; }
            public int Size { get; protected set; }
            public int Offset { get; set; } 
            // making this public saves so many issues
            // and putting it in Opcode? golden
            public int Address {  get { return Pc + Offset; } set { Offset = value - Pc; } }
            public Opcode(uint raw, int pc)
            {
                this.Op = (GMCode)((raw >> 24) & 0xFF);
                this.Raw = raw;
                this.Pc = pc;
                this.Size = 1;
            }
            public bool isBranch { get { return Op == GMCode.B || Op == GMCode.Bf || Op == GMCode.Bt; } }
            public bool isConditional {  get { return Op == GMCode.Slt || Op == GMCode.Sle || Op == GMCode.Seq || Op == GMCode.Sne || Op == GMCode.Sge || Op == GMCode.Sgt; } }

            public bool Equals(Opcode other) { return other.Raw == Raw; }
            public bool Equals(GMCode other) { return other == Op; }
           // public override int GetHashCode() { return (int)Raw; }
          
            public virtual string Operand { get { return null; } } // none
            public virtual string OpText { get { return opDecode[Op]; } }
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("{0,-6}: {1} ", Pc,OpText);
                if (Operand != null) sb.Append(Operand);
                return sb.ToString();
                
            }
        }
        public class OffsetOpcode : Opcode
        {
            public static uint MakeBranchRawOpcode(GMCode op, int offset)
            {
                uint raw = (uint)((byte)op << 24);
                raw |= (uint)(offset & 0x00FFFFFF);
                return raw;
            }
            
            public OffsetOpcode(uint raw, int pc) : base(raw, pc)
            {
                uint offset = raw & 0x00FFFFFF;
                if ((offset & 0x00100000) != 0) offset |= 0xFF000000; // make it negitive
                Offset = (int)offset;
            }
            public OffsetOpcode(uint raw, int offset, int pc) : base(raw, pc) { Offset = offset; } // use this if the offset dosn't match the opcode
            public OffsetOpcode(GMCode op, int offset, int pc) : base(MakeBranchRawOpcode(op, offset), pc) { Offset = offset; } // use this if you need to make an opcode
            public override string Operand { get { return Address.ToString(); } }
        }
        public class PopOpcode : Opcode,  TypeOpcodeInterface
        {
            public GM_Type FirstType { get { return (GM_Type)(int)((Raw >> 16) & 0xF); } }
            public GM_Type SecondType { get { return (GM_Type)(int)((Raw >> 20) & 0xF); } }
            public int Instance { get { return (short)(Raw & 0xFFFF); } }
            public PopOpcode(uint raw, BinaryReader r, int pc) : base(raw, pc)
            {
                Offset = r.ReadInt32();
                Size += 1;
            }
            public override string Operand
            {
                get
                {
                    return String.Format("( {0} -> {1} {2})", gmTypeLookup[FirstType], gmTypeLookup[SecondType], base.Operand);
                }
            }
        }
        public class TypeOpcode : Opcode, TypeOpcodeInterface
        {
            public GM_Type FirstType { get { return (GM_Type)(int)((Raw >> 16) & 0xF); } }
            public GM_Type SecondType { get { return (GM_Type)(int)((Raw >> 20) & 0xF); } }
            public TypeOpcode(uint raw, int pc) : base(raw, pc)
            {
            }
            public override string Operand { get { return String.Format("( {0} -> {1} )", gmTypeLookup[FirstType], gmTypeLookup[SecondType]); } }
        }
        public class CallOpcode : Opcode, OffsetOpcodeInterface
        {
            public int ArgumentCount { get { return (short)(Raw & 0xFFFF); } }
            public CallOpcode(uint raw, BinaryReader r, int pc) : base(raw, pc)
            {
                Offset = r.ReadInt32();
                Size += 1;
            }
            public override string Operand { get { return String.Format("{0}({1})", Offset, ArgumentCount); } }
        }
        public class PushOpcode : Opcode
        {
            [StructLayout(LayoutKind.Explicit)]
            struct OperandValueUnion
            {
                [FieldOffset(0)]
                public double dvalue;
                [FieldOffset(0)]
                public long lvalue;
            }
            OperandValueUnion operand;
            public GM_Type OperandType { get; private set; }
            public double OperandValueDouble { get { return operand.dvalue; } }
            public double OperandValue { get { return operand.lvalue; } }
            public int Instance {  get { return (short)(Raw & 0xFFFF); } }
            public PushOpcode(uint raw, BinaryReader r, int pc) : base(raw, pc)
            {
                operand = new OperandValueUnion();
                long startPos = r.BaseStream.Position;
                byte t = (byte)(raw >> 16);
                switch (t)
                {
                    case 0x0: operand.dvalue = r.ReadDouble(); OperandType = GM_Type.Double; break;
                    case 0x1: operand.dvalue = r.ReadSingle(); OperandType = GM_Type.Float; break;
                    case 0x2: operand.lvalue = r.ReadInt32(); OperandType = GM_Type.Int; break;
                    case 0x3: operand.lvalue = r.ReadInt64(); OperandType = GM_Type.Long; break;
                    case 0x5: operand.lvalue = r.ReadInt32(); OperandType = GM_Type.Var; break;
                    case 0x6: operand.lvalue = r.ReadInt32(); OperandType = GM_Type.String; break;
                    case 0xF: operand.lvalue = (short)(raw & 0xFFFF); OperandType = GM_Type.Short; break;
                    default:
                        throw new Exception("Bad type");
                }
                long endPosition = r.BaseStream.Position;
                Size += (int)((endPosition - startPos) / 4);
                System.Diagnostics.Debug.Assert(((endPosition - startPos) % 4) == 0);
            }
            public override string OpText
            {
                get
                {
                    string text = base.OpText;
                    switch (OperandType)
                    {
                        case GM_Type.Double: return text + ".d ";
                        case GM_Type.Float: return text + ".f ";
                        case GM_Type.Int: return text + ".i ";
                        case GM_Type.Long: return text + ".l ";
                        case GM_Type.Var: return text + ".v ";
                        case GM_Type.String: return text + ".s ";
                        case GM_Type.Short: return text + ".e ";
                        default:
                            throw new Exception("Bad type");
                    }
                }
            }
            public override string Operand
            {
                get
                {
                    if (OperandType == GM_Type.Double || OperandType == GM_Type.Float) return operand.dvalue.ToString();
                    else return operand.lvalue.ToString();
                }
            }
        }
        int pc;
        ChunkStream r;
        public GM_Disam(ChunkStream r)
        {
            this.r = r;
            this.pc = 0;
        }
        Opcode ReadOpcode()
        {
            uint raw = r.ReadUInt32();
            GMCode opType = (GMCode)((byte)(raw >> 24));
            switch (opType)
            {
                case GMCode.Dup:
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Rem:
                case GMCode.Mod:
                case GMCode.Add:
                case GMCode.Sub:
                case GMCode.Or:
                case GMCode.And:
                case GMCode.Xor:
                case GMCode.Not:
                case GMCode.Sal:
                case GMCode.Slt:
                case GMCode.Sle:
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Sgt:
                case GMCode.Sne:
                case GMCode.Neg:
                case GMCode.Conv:
                    return new TypeOpcode(raw, pc);
                case GMCode.Popenv:
                case GMCode.Pushenv:
                    return new OffsetOpcode(raw, (short)(raw & 0xFFFF), pc);
                case GMCode.Bf:
                case GMCode.Bt:
                case GMCode.B:
                    return new OffsetOpcode(raw, pc);
                case GMCode.Push:
                    return new PushOpcode(raw, r, pc);
                case GMCode.Pop:
                    return new PopOpcode(raw, r, pc);
                case GMCode.Popz: // usally on void funtion returns, so just pop the stack and print it
                case GMCode.Break:
                case GMCode.Exit:
                case GMCode.Ret:
                    return new Opcode(raw, pc);
                case GMCode.Call:
                    return new CallOpcode(raw, r, pc);
                default:
                 //   System.Diagnostics.Debug.WriteLine("Not implmented at {0} value {1} valuehex {1:X}", r.BaseStream.Position, op.opcode, op.opcode);
                    throw new Exception("Bad opcode"); // We fix this now no more ignoreing this
            }
        }
        public SortedList<int,GM_Disam.Opcode> ReadOpCode(int start, int length, string scriptName)
        {
            // test
            OffsetStream s = new OffsetStream(r.BaseStream, start, length);
            this.scriptName = scriptName;
            SortedList<int, Opcode> codes = new SortedList<int, Opcode>(length * 2); // should be safe
            pc = 0;
            r.Position = start;
            int limit = start + length;
            while (r.Position < limit)
            {
                Opcode o = ReadOpcode();
                codes.Add(pc,o);
                pc += o.Size;
            }
            // We might have a branch that goes to the end of this, so just throwing in Exit just  in case
            codes.Add(pc, new Opcode(unchecked((uint)((byte)GMCode.Exit << 24)), pc));
            return codes;
        }


    }
}
