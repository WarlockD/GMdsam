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

    enum GM_Type : byte
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
    enum OpType : byte
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
    class GM_Disam
    {
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
             { (OpType) 0x0d, "neg" },
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
        static int getBranchOffset(uint op)
        {
            op &= 0x00FFFFFF;
            if ((op & 0x800000) != 0) op |= 0xFF000000;
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
        public class Opcode : IEquatable<OpType>
        {
            public int Compare(Opcode a, Opcode b)
            {
                return a.Pc.CompareTo(b.Pc);
            }
            public OpType Op { get;  set; }
            public uint Raw { get; private set; }
            public int Pc { get; private set; }
            public int Size { get; protected set; }
            public int Offset { get; set; } 
            // making this public saves so many issues
            // and putting it in Opcode? golden
            public int Address {  get { return Pc + Offset; } set { Offset = value - Pc; } }
            public Opcode(uint raw, int pc)
            {
                this.Op = (OpType)((raw >> 24) & 0xFF);
                this.Raw = raw;
                this.Pc = pc;
                this.Size = 1;
            }
            public bool isBranch { get { return Op == OpType.B || Op == OpType.Bf || Op == OpType.Bt; } }
            public bool isConditional {  get { return Op == OpType.Slt || Op == OpType.Sle || Op == OpType.Seq || Op == OpType.Sne || Op == OpType.Sge || Op == OpType.Sgt; } }

            public bool Equals(Opcode other) { return other.Raw == Raw; }
            public bool Equals(OpType other) { return other == Op; }
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
            public static uint MakeBranchRawOpcode(OpType op, int offset)
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
            public OffsetOpcode(OpType op, int offset, int pc) : base(MakeBranchRawOpcode(op, offset), pc) { Offset = offset; } // use this if you need to make an opcode
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
            OpType opType = (OpType)((byte)(raw >> 24));
            switch (opType)
            {
                case OpType.Dup:
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
                case OpType.Neg:
                case OpType.Conv:
                    return new TypeOpcode(raw, pc);
                case OpType.Popenv:
                case OpType.Pushenv:
                    return new OffsetOpcode(raw, (short)(raw & 0xFFFF), pc);
                case OpType.Bf:
                case OpType.Bt:
                case OpType.B:
                    return new OffsetOpcode(raw, pc);
                case OpType.Push:
                    return new PushOpcode(raw, r, pc);
                case OpType.Pop:
                    return new PopOpcode(raw, r, pc);
                case OpType.Popz: // usally on void funtion returns, so just pop the stack and print it
                case OpType.Break:
                case OpType.Exit:
                    return new Opcode(raw, pc);
                case OpType.Call:
                    return new CallOpcode(raw, r, pc);
                default:
                 //   System.Diagnostics.Debug.WriteLine("Not implmented at {0} value {1} valuehex {1:X}", r.BaseStream.Position, op.opcode, op.opcode);
                    throw new Exception("Bad opcode"); // We fix this now no more ignoreing this
            }
        }
        public SortedList<int,GM_Disam.Opcode> ReadOpCode(int start, int length)
        {
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
            codes.Add(pc, new Opcode(unchecked((uint)((byte)OpType.Exit << 24)), pc));
            return codes;
        }


    }
}
