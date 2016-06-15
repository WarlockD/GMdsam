using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;

using GameMaker.Dissasembler;
using System.Text.RegularExpressions;
using System.Globalization;

namespace GameMaker
{


    public enum GM_Type : int
    {
       
        Double=0, 
        Float,
        Int,
        Long,
        Bool,
        Var,
        String,
        Short = 15, // This is usally short anyway
        Instance,
        Sprite,
        Sound, 
        Path,
        NoType = -100,

    }
    public static class GM_TypeExtensions
    {
        public static bool isReal(this GM_Type t) { return t == GM_Type.Double || t == GM_Type.Float;  }
        public static bool isInstance(this GM_Type t) { return t == GM_Type.Instance || t == GM_Type.Sprite || t == GM_Type.Sound || t == GM_Type.Path; }
        public static bool canBeInstance(this GM_Type t) { return t == GM_Type.Int || t == GM_Type.Short || t.isInstance(); }
        public static bool isInteger(this GM_Type t) { return t == GM_Type.Int || t == GM_Type.Long || t == GM_Type.Short; }
        public static bool isNumber(this GM_Type t) { return t.isReal() || t.isInteger() || !t.isInstance(); }

        // This is the top dog, you can't convert this downward without a function or some cast
        public static bool isBestVar(this GM_Type t) { return t.isInstance() || t == GM_Type.String || t == GM_Type.Double || t == GM_Type.Bool; }
        public static GM_Type ConvertType(this GM_Type t0, GM_Type t1)
        {
            if (t0 == t1) return t0;
            if (t1 == GM_Type.Bool) return t1; // bool ALWAYS overrides
            if (t1.isBestVar()) return t1;
            if (t0.isBestVar()) return t0;
            switch (t0)
            {
                case GM_Type.Var:
                    return t1; // Vars are general variables so we don't want that
                case GM_Type.String:
                case GM_Type.Bool:
                    return t0; // bool ALWAYS overrides eveything
                case GM_Type.Short:
                case GM_Type.NoType:
                    return t1; // whatever t1 is its better
                case GM_Type.Double:
                    Debug.Assert(!t1.isInstance());
                    if (t1.isNumber()) return t0; // we can convert
                    else if (t1.isInstance() || t1 == GM_Type.String) return t1; // instance is MUCH better than double, more important
                    return t0;
                case GM_Type.Sound:
                case GM_Type.Instance:
                case GM_Type.Sprite:
                    if (t1 == GM_Type.Int || t1 == GM_Type.Short) return t0; // we can be an instance
                    throw new Exception("Cannot convert this type to this");
                case GM_Type.Int:
                    if (t1 == GM_Type.Long || t1 == GM_Type.Float || t1 == GM_Type.Double || t1 == GM_Type.Instance || t1 == GM_Type.Sprite) return t1;
                    else return t0;
                case GM_Type.Long:
                    if (t1 == GM_Type.Double) return t1;
                    else return t0;
                case GM_Type.Float:
                    if (t1 == GM_Type.Double) return t1;
                    else return t0;
                default:
                    throw new Exception("Canot get here");
            }
        }

    }
    public enum GMCode : int
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
        Break = 0xff,
       // special for IExpression
       Var = 0xf3,
        LogicAnd = 0xf4,
        LogicOr = 0xf5,
        LoopContinue = 0xf6,
        LoopOrSwitchBreak = 0xf7,
        Switch = 0xf9,
        Case = 0xfa,
        Constant = 0xfb, 
   //     Assign,
        DefaultCase,
        Concat, // -- filler for lua or string math
        Array2D,
        Assign,
        VarUnresolved,
        CallUnresolved
       // AssignAdd,
    //    AssignSub,
     //   AssignMul,
      //  AssignDiv
            // there are more but meh
    }
    public static class GMCodeUtil
    {
   
        public static int getBranchOffset(uint op)
        {
            // Ok having a horred problem here.  I thought it was a 24 bit signed value
            // but when I try to detect a 1 in the signed value, it dosn't work.
            // so instead of checking for 0x80 0000 I am checking for 0x8 0000,
            // I can get away with this cause the offsets never go this far, still,
            // bug and unkonwn
            //  if ((op & 0x800000) != 0) op |= 0xFFF00000; else op &= 0x7FFFFF;
            if ((op & 0x400000) != 0) op |= 0xFFF00000; else op &= 0x7FFFFF;
            int nop = (int)op;
            Debug.Assert(nop < 80000); // arbatrary but havn't seen code this big
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
            { GMCode.LogicAnd, 2 },
            { GMCode.LogicOr, 2 },
            { GMCode.Concat, 2 },
        };
        public static uint toUInt(this GMCode t, int operand)
        {
            byte b = (byte)t;
            return (uint)(b << 24) | (uint)(0x1FFFFFF | operand);
        }
        public static uint toUInt(this GMCode t)
        {
            byte b = (byte)t;
            return (uint)(b << 24);
        }
        public static uint toUInt(this GMCode t, GM_Type type)
        {
            byte b = (byte)t;
            byte bt = (byte)type;
            return (uint)(b << 24) | (uint)(bt << 16);
        }
        public static int getOpTreeCount(this GMCode t)
        {
            int count;
            if (opMathOperationCount.TryGetValue(t, out count)) return count;
            return 0;
        }
        public static string getOpTreeString(this GMCode t)
        {
            string ret;
            if (opMathOperation.TryGetValue(t, out ret)) return ret;
            return null;
        }
        public static GMCode getInvertedOp(this GMCode t)
        {
            switch (t)
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
            { GMCode.Neg, "-"  },
            {  GMCode.Not, "!" },

            {  (GMCode)0x0f, "<<" },
            {  (GMCode)0x10, ">>" },
            { (GMCode) 0x11, "<" },
            { (GMCode) 0x12, "<=" },
            { (GMCode) 0x13, "==" },
          //  {  (GMCode)0x14, "!=" },
          {  (GMCode)0x14, "~=" },
            {  (GMCode)0x15, ">=" },
            {  (GMCode)0x16, ">" },
              { GMCode.LogicAnd, "and" },
            { GMCode.LogicOr, "or" },
            { GMCode.Concat, ".." },
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
        public static string lookupInstance(int instance, List<string> InstanceLookup = null)
        {
            string ret;
            if (instanceLookup.TryGetValue(instance, out ret)) return ret;
            else if (InstanceLookup != null && instance < InstanceLookup.Count) return InstanceLookup[instance];
            else return String.Format("%{0}%", instance);
        }
        public static string lookupInstanceFromRawOpcode(uint opcode, List<string> InstanceLookup = null)
        {
            return lookupInstance((short)(opcode & 0xFFFF), InstanceLookup);
        }
        public static bool IsArrayPushPop(uint operand)
        {
            return (operand & 0x8000000) == 0;
        }
        // can't use this as on larger strings and indents it does stuff like "" + ""
        // I could regex it out of the exit string, but after doing some cpu anilitics this is 
        // a VERY slow function and it needs to be much faster
        // Rewrote it as a manual parser, but needs to be fixed latter
#if false
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
#else
        public static string EscapeString(string input)
        {
            StringBuilder literal = new StringBuilder(input.Length + 2);
            literal.Append("\"");
            foreach (var c in input)
            {
                switch (c)
                {
                    case '\'': literal.Append(@"\'"); break;
                    case '\"': literal.Append("\\\""); break;
                    case '\\': literal.Append(@"\\"); break;
                    case '\0': literal.Append(@"\0"); break;
                    case '\a': literal.Append(@"\a"); break;
                    case '\b': literal.Append(@"\b"); break;
                    case '\f': literal.Append(@"\f"); break;
                    case '\n': literal.Append(@"\n"); break;
                    case '\r': literal.Append(@"\r"); break;
                    case '\t': literal.Append(@"\t"); break;
                    case '\v': literal.Append(@"\v"); break;
                    default:
                        // ASCII printable character
                        if (c >= 0x20 && c <= 0x7e) literal.Append(c);
                         // As UTF16 escaped character
                        else if (Char.GetUnicodeCategory(c) == UnicodeCategory.Control){
                            literal.Append(@"\u");
                            literal.Append(((int)c).ToString("x4"));
                        }
                        break;
                }
            }
            literal.Append("\"");
            return literal.ToString();
        }
#endif
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
            switch ((GMCode)(raw >> 24))
            {
                case GMCode.Pop:
                case GMCode.Call:
                    return 2;
                case GMCode.Push:
                    return ((GM_Type)((raw >> 16) & 0xF)).GetSize() + 1;
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
        public static bool IsUnconditionalControlFlow(this GMCode code)
        {
            return code == GMCode.B || code == GMCode.Exit || code == GMCode.Ret || code == GMCode.LoopContinue || code == GMCode.LoopOrSwitchBreak || code == GMCode.Popenv;
        }
        public static bool IsConditionalControlFlow(this GMCode code)
        {
            return code == GMCode.Bt || code == GMCode.Bf || code == GMCode.Switch || code == GMCode.Pushenv;
        }
        public static bool isBranch(this GMCode code)
        {
            return code == GMCode.Bt || code == GMCode.Bf || code == GMCode.B;
        }
        public static bool IsConditionalCode(this GMCode code)
        {
            switch (code)
            {
                case GMCode.Seq:
                case GMCode.Sne:
                case GMCode.Sge:
                case GMCode.Sle:
                case GMCode.Sgt:
                case GMCode.Slt:
                case GMCode.Not:
                case GMCode.LogicAnd:
                case GMCode.LogicOr:
                    return true;
                default:
                    return false;

            }
        }
        public static bool isExpression(this GMCode i)
        {
            switch (i)
            {
                case GMCode.Concat:
                case GMCode.LogicAnd:
                case GMCode.LogicOr:
                case GMCode.Neg:
                case GMCode.Not:
                case GMCode.Add:
                case GMCode.Sub:
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                case GMCode.And:
                case GMCode.Or:
                case GMCode.Xor:
                case GMCode.Sal:
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Sgt:
                case GMCode.Sle:
                case GMCode.Slt:
                case GMCode.Sne:
                    return true;
                default:
                    return false;
            }
        }
        public static int GetPopDelta(this GMCode i)
        {
            switch (i)
            {
                case GMCode.Popenv:
                case GMCode.Exit:
                case GMCode.Conv:
                    break; // we ignore conv
                case GMCode.Call:
                case GMCode.Push:
                case GMCode.Pop:
                case GMCode.Dup:
                    throw new Exception("Need more info for pop");
                case GMCode.Popz:
                case GMCode.Ret:
             
                case GMCode.Bt:
                case GMCode.Bf:
                case GMCode.Neg:
                case GMCode.Not:
                case GMCode.Pushenv:
                    return 1;
                case GMCode.Add:
                case GMCode.Sub:
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                case GMCode.And:
                case GMCode.Or:
                case GMCode.Xor:
                case GMCode.Sal:
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Sgt:
                case GMCode.Sle:
                case GMCode.Slt:
                case GMCode.Sne:
                    return 2;
                case GMCode.Var:
                case GMCode.Constant:
                case GMCode.B:
                    return 0;
                default:
                    throw new Exception("Unkonwn opcode");
            }
            return 0;
        }
        public static int GetPushDelta(this GMCode code)
        {
            switch (code)
            {
                case GMCode.Popenv:
                case GMCode.Exit:
                case GMCode.Conv:
                    break; // we ignore conv
                case GMCode.Call:
                case GMCode.Push:
                    return 1;
                case GMCode.Pop:
                case GMCode.Popz:
                case GMCode.B:
                case GMCode.Bt:
                case GMCode.Bf:
                case GMCode.Ret:
                case GMCode.Pushenv:
                    break;
                case GMCode.Dup:
                    throw new Exception("Need more info for dup");
                case GMCode.Neg:
                case GMCode.Not:
                case GMCode.Add:
                case GMCode.Sub:
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                case GMCode.And:
                case GMCode.Or:
                case GMCode.Xor:
                case GMCode.Sal:
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Sgt:
                case GMCode.Sle:
                case GMCode.Slt:
                case GMCode.Sne:
                    return 1;
                default:
                    throw new Exception("Unkonwn opcode");

            }
            return 0;
        }
   
    }
}