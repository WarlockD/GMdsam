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

        Double = 0,
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
        public static bool isReal(this GM_Type t) { return t == GM_Type.Double || t == GM_Type.Float; }
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
            if (t1 == GM_Type.String && t0.isInstance()) throw new Exception("bad type");
            if (t1.isBestVar()) return t1;
            if (t0.isBestVar()) return t0;
            switch (t0)
            {
                case GM_Type.Var:
                    return t1; // Vars are general variables so we don't want that
                case GM_Type.String:
                    Debug.Assert(t1.isInstance()); // check in case
                    return t0;
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
        BadOp, // used for as a noop, mainly for branches hard jump location is after this
        Conv,
        Mul,
        Div,
        Rem,
        Mod,
        Add,
        Sub,
        And,
        Or,
        Xor,
        Neg,
        Not,
        Sal,
        Slt,
        Sle,
        Seq,
        Sne,
        Sge,
        Sgt,
        Pop,
        Dup,
        Ret,
        Exit,
        Popz,
        B,
        Bt,
        Bf,
        Pushenv,
        Popenv,
        Push,
        Call,
        Break,
        // special for IExpression
        Var,
        LogicAnd,
        LogicOr,
        LoopContinue,
        LoopOrSwitchBreak,
        Switch,
        Case,
        Constant,
        //     Assign,
        DefaultCase,
        Concat, // -- filler for lua or string math
        Array2D,
        Assign,
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
            // so instead of checking for 0x80 0000 I am checking for 0x40 0000,
            // I can get away with this cause the offsets never go this far, still,
            // bug and unkonwn
            //  if ((op & 0x800000) != 0) op |= 0xFFF00000; else op &= 0x7FFFFF;
            if ((op & 0x400000) != 0) op |= 0xFFF00000; else op &= 0x7FFFFF;
            int nop = (int) op;
            Debug.Assert(nop < 80000); // arbatrary but havn't seen code this big
            return (int) (op);
        }

        public static Dictionary<int, string> instanceLookup = new Dictionary<int, string>()
        {
            {  0 , "stack" },
            {  -1, "self" },
            {  -2, "other" },
            {  -3, "all" },
            {  -4, "noone" },
            {  -5, "global" },
        };

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
                    //  case '\'': literal.Append(@"\'"); break;
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
                        else if (Char.GetUnicodeCategory(c) == UnicodeCategory.Control)
                        {
                            literal.Append(@"\u");
                            literal.Append(((int) c).ToString("x4"));
                        }
                        break;
                }
            }
            literal.Append("\"");
            return literal.ToString();
        }
#endif
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
    }
}