using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;

using betteribttest.Dissasembler;
using System.Text.RegularExpressions;
using System.Globalization;

namespace betteribttest
{


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
        NoType,
        ConstantExpression // used when an expression is constant
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
        Assign,
        // filler used when a push is VERY simple, no breaks
        // it makes the graph builder not puke as much
        SimplePushenv, 
    }
    public static class GMCodeUtil
    {
   
        public static int getBranchOffset(uint op)
        {
            if ((op & 0x800000) != 0) op |= 0xFF000000; else op &= 0x00FFFFFF;
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
              { GMCode.LogicAnd, "&&" },
            { GMCode.LogicOr, "||" },
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
            return code == GMCode.B || code == GMCode.Exit || code == GMCode.Ret || code == GMCode.LoopContinue || code == GMCode.LoopOrSwitchBreak;
        }
        public static bool IsConditionalControlFlow(this GMCode code)
        {
            return code == GMCode.Bt || code == GMCode.Bf || code == GMCode.Switch;
        }
        public static bool isBranch(this GMCode code)
        {
            return code == GMCode.Bt || code == GMCode.Bf || code == GMCode.B;
        }
        public static bool IsConditionalStatment(this GMCode code)
        {
            switch (code)
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

    public interface ITextOut
    {
        /// <summary>
        /// Writes a line of text, normaly its from the toString()
        /// </summary>
        /// <param name="wr">TextWriterStream</param>
        /// <returns>Number of lines written, 0 means no new line was written</returns>
        int WriteTextLine(TextWriter wr);
    }
    public sealed class Label : IComparable<Label>, IComparable<Instruction>, IEquatable<Label>, ITextOut
    {
        private List<Instruction> _forwardRefrences;
        private List<Instruction> _backardRefrences;
        private List<Instruction> _refrences;
        public IReadOnlyList<Instruction> ForwardRefrences { get { return _forwardRefrences; } }
        public IReadOnlyList<Instruction> BackwardRefrences { get { return _backardRefrences; } }
        public IReadOnlyList<Instruction> AllRefrencess { get { return _refrences; } }
        public int CallsTo { get { return _refrences.Count; } }
        public bool hasBackwardRefrences { get { return _backardRefrences.Count != 0; } }
        public bool hasForwardRefrences { get { return _backardRefrences.Count != 0; } }
        public int Address { get; internal set; }

        public Instruction InstructionOrigin { get; private set; }
        public Label(Label l)
        {
            Address = l.Address;
            _forwardRefrences = l._forwardRefrences;
            _backardRefrences = l._backardRefrences;
            _refrences = l._refrences;
            InstructionOrigin = l.InstructionOrigin;
        }
        bool _exitJump;
        internal Label(int target,bool exitJump=false)
        {
            Address = target;
            _forwardRefrences = null;
            _backardRefrences = null;
            _refrences = null;
            InstructionOrigin = null;
            _exitJump = exitJump;
        }
        public Label Copy()
        {
            Label l = new Label(this);
            return l;
        }
        const uint breakPopenvOpcode = (uint)(((uint)(GMCode.Popenv)) << 24 | 0x00F00000);
        // this fixes all the instructions, and branches with labels so we can track them all


        public static void ResolveCalls(Instruction.Instructions list)
        {
            HashSet<Label> allLabels = new HashSet<Label>();
            foreach (var i in list)
            {
                if (i.Label != null) allLabels.Add(i.Label);
                Label l = i.Operand as Label;
                if (l != null) allLabels.Add(l);
            } // makes sure there are no label duplicates when we do this
            foreach (var l in allLabels) ResolveCalls(l, list);
        }
        // does not caculated refrences
        public static void SimpleResolveCalls(Instruction.Instructions list,  Dictionary<int, Label> labels)
        {
            foreach(var l in labels.Values)
            {
                l.InstructionOrigin = list.atOffset(l.Address);
                list.atOffset(l.Address).Label = l;
            }
            foreach(var i in list.Where(x=> x.isBranch))
            {
                var l = labels[i.BranchDesitation];
                if (l._refrences == null) l._refrences = new List<Instruction>();
                l._refrences.Add(i);
                i.Operand = l;
            }
        }

        public static void ResolveCalls(Label l, Instruction.Instructions list)
        {
            var fRefs = new List<Instruction>();
            var bRefs = new List<Instruction>();
            var Refs = new List<Instruction>();
            bool behind = true;
            foreach(var i in list) { 
                if (i.Address == l.Address) {
                    i.Label = l;                    // Link the label, even if it wasn't there before
                    l.InstructionOrigin = i;     // Link the instruction its attached too
                    behind = false;                 // tell the system to start to ad to the forward list
                } else {
                    if (i.Code.isBranch())// || i.GMCode == GMCode.Pushenv || (i.GMCode == GMCode.Popenv && i.OpCode != breakPopenvOpcode))// || inst.GMCode == GMCode.Pushenv || inst.GMCode == GMCode.Popenv) // pushenv also has a branch 
                    {
                        int target = GMCodeUtil.getBranchOffset(i.OpCode);
                        target += i.Address;
                        if (target == l.Address)
                        {
                            if (behind) bRefs.Add(i); else fRefs.Add(i);
                            i.Operand = l; // make sure the label is in the operand even if it wasnt before
                            Refs.Add(i);
                        }
                    }
                }
            }
            l._forwardRefrences = fRefs;
            l._backardRefrences = bRefs;
            l._refrences = Refs;
        }
        public int WriteTextLine(TextWriter wr)
        {
            wr.Write(this.ToString());
            return 0;
        }
        public override string ToString()
        {
            if (Address == 0)
                return "LStart";
            else if(_exitJump)
                return "LExit";
            else
                return String.Format("L{0}", Address);
        }
        public override bool Equals(object obj)
        {
            if (Object.ReferenceEquals(obj, null)) return false;
            if (obj.GetType() != this.GetType()) return false;
            if (Object.ReferenceEquals(obj, this)) return true;
            Label l = obj as Label;
            if (l == null) return false;
            return Equals(l);
        }
        public override int GetHashCode()
        {
            return Address;
        }

        public int CompareTo(Label other)
        {
            return Address.CompareTo(other.Address);
        }
        public int CompareTo(Instruction other)
        {
            return Address.CompareTo(other.Address);
        }
        public bool Equals(Label l)
        {
            return l.Address == Address;
        }
    }
    public class MethodBody
    {
        public List<Instruction> Instructions;
        public List<Label> Labels;
        public List<EnviromentHandler> Enviroments;
    }

    public sealed class Instruction : IComparable<Label>, IComparable<Instruction>, IEquatable<Instruction>
    {
        // link the instructions, makes it easier to process
        Instruction _next;
        Instruction _previous;
        public Instruction Next { get { return _next; } }
        public Instruction Previous { get { return _previous; } }

        public IEnumerable<Instruction> RangeFrom(Instruction to)
        {
            Debug.Assert(to != null);
            var at = this;
            while (at != to && at !=null)
            {
                yield return at;
                at = at.Next;
            }
            Debug.Assert(at != null); // return an issue if it was before, maybe we can compare addresses
            yield return to;
        }
        const uint breakPopenvOpcode = (uint)(((uint)(GMCode.Popenv)) << 24 | 0x00F00000);
        byte[] raw_opcode;
        int _operandInt;
        // makes it a bad op code that just has the offset and the operand
        public bool isBranch { get { return this.Code.isBranch(); } }
        public int BranchDesitation
        {
            get
            {
                return GMCodeUtil.getBranchOffset(OpCode) + Address;
              //  Label l = Operand as Label;
               // return l.Address;
            }
        }
        public string Comment { get; set; }
        public int Address { get; private set; }
        public Label Label { get;  set; }
        public uint OpCode { get; private set; }
        public GMCode Code { get; private set; }
        public object Operand { get; set; }
        public int OperandInt { get { return _operandInt; } }
       
        public static Instruction CreateFakeExitNode(Instruction i, bool need_label)
        {
            uint op = ((uint)GMCode.Exit << 24);
            op |= ((uint)GM_Type.Var << 16);

            Instruction ni = new Instruction(i.Address + i.Size, op);
            if (need_label) ni.Label = new Label(ni.Address);
            return ni;
        }
        // this is used for the peep changing, don't need this really outside of it
        internal void ChangeInstruction(GMCode code, Label l)
        {
            Debug.Assert(this.Code.isBranch());
            OpCode = (uint)(((byte)code) << 24);
            OpCode &= (uint)(this.Address-l.Address);
            Debug.Assert(this.Code.isBranch());
        }
        public Instruction(GMCode code, int offset) {
            this.Address = offset;
            this.Code = code;
            this.OpCode = 0;
            this.Operand = null;
            this._operandInt = 0;
            this.Label = null;
            this.Comment = null;
        }
        internal Instruction(int offset, uint opCode) 
        {
            this.Code = GMCodeUtil.getFromRaw(OpCode);
            this.Address = offset;
            this.OpCode = opCode;
            this.Operand = null;
            this._operandInt = 0;
            this.Label = null;
            this.Comment = null;
        }
        public int PopDelta
        {
            get
            {
                int count = Code.getOpTreeCount(); // not a leaf
                if (count == 2) return 2; // pop 2, push 1
                else
                {
                    switch (Code)
                    {
                        case GMCode.Dup:
                            Debug.Assert(this.Instance == 0);
                            return 1; // figure this one out, its more than this
                        case GMCode.Popz:   // the call is now a statlemtn
                        case GMCode.Pushenv:
                        case GMCode.Bf:
                        case GMCode.Bt:
                        case GMCode.B:
                        case GMCode.Conv:
                        case GMCode.Not:
                        case GMCode.Neg: // pop 1 push 1
                            return 1;
                        case GMCode.Pop:
                            if (this.Instance == 0) return 0; // simple local var
                            return OperandInt > 0 ? 2 : 1;
                        case GMCode.Call:
                            return this.Instance; // number of arguments
                        default:
                            return 0;
                            throw new Exception("Not Implmented! ugh");
                    }
                }
            }
        }
        public int PushDelta
        {
            get
            {
                int count = Code.getOpTreeCount(); // not a leaf
                if (count == 2) return 1; // pop 2, push 1
                else
                {
                    switch (Code)
                    {
                        case GMCode.Dup:
                            Debug.Assert(this.Instance == 0);
                            return 1; // figure this one out, its more than this
                        case GMCode.Conv:
                        case GMCode.Not:
                        case GMCode.Neg: // pop 1 push 1 
                        case GMCode.Push:
                        case GMCode.Call:
                            return 1;
                        default:
                            return 0;
                            throw new Exception("Not Implmented! ugh");
                    }
                }
            }
        }
        internal Instruction(int offset, BinaryReader r) 
        {
            this.Operand = null;
            this.Label = null;
            this.Address = offset;
            // raw_opcode = r.ReadBytes(sizeof(int));
            this.OpCode = r.ReadUInt32();
            this.Code =  GMCodeUtil.getFromRaw(OpCode); 
            raw_opcode = BitConverter.GetBytes(this.OpCode);
            if (Size > 1)
            {
               // Array.Resize(ref raw_opcode, size * sizeof(int));
              //  r.Read(raw_opcode, 4, (size - 1) * sizeof(int));
                switch (this.Code)
                {
                    case GMCode.Call:
                    case GMCode.Pop:
                        Operand = _operandInt= r.ReadInt32();
                        break;
                    case GMCode.Push:
                        {
                            GM_Type eVMType = (GM_Type)((OpCode >> 16) & 15);
                            switch (eVMType)
                            {
                                case GM_Type.Long:
                                    Operand = r.ReadInt64();
                                    break;
                                case GM_Type.Double:
                                    Operand = r.ReadDouble();
                                    break;
                                case GM_Type.Float:
                                    Operand = r.ReadSingle();
                                    break;
                                case GM_Type.Bool:
                                    Operand = BitConverter.ToInt32(raw_opcode, 4) != 0 ? true : false;
                                    break;
                                case GM_Type.String: 
                                case GM_Type.Var:
                                     _operandInt = r.ReadInt32() ;
                                     Operand = _operandInt & 0x1FFFFF;
                                    break;
                                case GM_Type.Int:
                                    Operand = r.ReadInt32();
                                    break;
                            }
                        }
                        break;
                }
                System.Diagnostics.Debug.Assert(Operand != null);
            }
        }
        // Screw it, this is hemerging memory anyway
        public List<string> InstanceLookup = null;
        public List<string> StringLookup = null;
        // Common enough for me to put it here
        public short Instance
        {
            get
            {
                return (short)(OpCode & 0xFFFF);
            }
        }
        public int Size
        {
            get
            {
                return GMCodeUtil.OpCodeSize(OpCode);
            }
        }
        public GM_Type FirstType
        {
            get
            {
                return (GM_Type)((OpCode >> 16) & 0x0F);
            }
        }
        public GM_Type SecondType
        {
            get
            {
                return (GM_Type)((OpCode >> 20) & 0x0F);
            }
        }
        void FormatPrefix(StringBuilder line,int opcode,int type) {
            line.AppendFormat("{0,-5}{1,-5} ", Address, this.Label == null ? "" : this.Label.ToString());
            line.Append(this.Code.GetName());
            if ((opcode & 160) == 128)
            {
                line.Append(GMCodeUtil.TypeToStringPostfix(type & 0xF));
            }
            else if ((opcode & 160) == 0)
            {
                line.Append(GMCodeUtil.TypeToStringPostfix(type & 0xF));
                line.Append(GMCodeUtil.TypeToStringPostfix(type >> 4));
            }
            // so we should have 28 spaces here
            if (line.Length < 21) line.Append(' ', 21 - line.Length);
        }
        void CommaList(StringBuilder line, IReadOnlyCollection<Instruction> list)
        {
            bool need_comma = false;
            foreach (var c in list)
            {
                if (need_comma) line.Append(',');
                line.Append(c.Address);
                need_comma = true;
            }
        }
        void FormatPostFix(StringBuilder line)
        {
            if(Comment != null || Label != null)
            {
                if (line.Length < 40) line.Append(' ', 40 - line.Length); // make sure all the comments line up
                line.Append("; ");
                if(Label != null)
                {
                    if(Label.BackwardRefrences!=null &&  Label.BackwardRefrences.Count !=0)
                    {
                        line.Append("BackwardRefs[");
                        CommaList(line, Label.BackwardRefrences);
                        line.Append("] ");
                    }
                    if (Label.ForwardRefrences != null && Label.ForwardRefrences.Count > 0)
                    {
                        line.Append("ForwardRefs[");
                        CommaList(line, Label.ForwardRefrences);
                        line.Append("] ");
                    }
                }
                if (Comment != null) line.Append(Comment);
             }
        }
        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            WriteTextLine(new PlainTextOutput(sw));
            return sw.ToString();
        }
        public int WriteTextLine(ITextOutput wr)
        {
            StringBuilder line = new StringBuilder();
            int opcode = (int)((OpCode >> 24) & 0xFF);
            int type = (int)((OpCode >> 16) & 0xFF);
            string str;

            FormatPrefix(line,opcode,type); // both type prints are caculated in prefix

           // if(GMCode.IsConditional() && Operand is Label)
           if(Operand is Label)
            {
                str = Operand.ToString();
                line.Append(str);
            }
            else if ((opcode & 64) != 0) // we have an operand
            {
                GM_Type eVMType = (GM_Type)(type & 15);
                switch (eVMType)
                {
                    case GM_Type.Double:
                    case GM_Type.Int:
                    case GM_Type.Long:
                    case GM_Type.Bool:
                        line.Append(Operand.ToString());
                        break;
                    case GM_Type.Var:
                        line.Append(GMCodeUtil.lookupInstanceFromRawOpcode(OpCode, InstanceLookup));
                        line.Append('.');
                        if (Operand is int)
                        {
                            int operand = (int)Operand & 0x1FFFFFF;
                            if (StringLookup != null) line.Append(StringLookup[operand]);
                            else line.AppendFormat("${0}$", operand);        
                        } else line.Append(Operand.ToString());
                        if (GMCodeUtil.IsArrayPushPop((uint)_operandInt)) line.Append("[]");
                        break;
                    case GM_Type.String:
                        if (Operand is int)
                            line.AppendFormat("\"{0}\"", ((int)Operand & 0x1FFFFFF));
                        else goto case GM_Type.Bool;

                        break;
                    case GM_Type.Short:
                        line.Append((short)(OpCode << 16 >> 16));
                        break;
                }
            }
            else if ((opcode & 32) != 0)
            { //// ooooooooooh
                if(OpCode == breakPopenvOpcode)
                {
                    line.Append("break enviroment");

                } else if (Code.IsConditionalControlFlow() || Code == GMCode.Pushenv || Code == GMCode.Popenv)
                {
                    int offset = GMCodeUtil.getBranchOffset(OpCode);
                    line.Append(Address-offset);
                } else
                {
                    int new_offset = Address + (int)(OpCode << 8 >> 8); // << 8 >> 6 // changed cause we are doing pc by int
                    line.AppendFormat("0x{0:x8}", new_offset);
                }
            }
            FormatPostFix(line);
            wr.Write(line.ToString());
            return 0;
        }
        public class Instructions : Collection<Instruction>, ITextOut
        {
            class InstructionOffsetCollection : KeyedCollection<int, Instruction>
            {
                protected override int GetKeyForItem(Instruction item)
                {
                    // In this example, the key is the part number.
                    return item.Address;
                }
            }
        
            InstructionOffsetCollection _fromOffset = new InstructionOffsetCollection();
            public Instruction atOffset(int i)
            {
                return _fromOffset[i];
            }
            public Instructions() : base()
            {
            }
            static void Relink(IEnumerable<Instruction> list) // sigh this just works
            {
                Instruction prev = null;
                foreach (var i in list)
                {
                    //  node.Parent = _parent;
                    i._next = null;
                    if (prev != null)
                    {
                        i._previous = prev;
                        prev._next = i;
                    }
                    else i._previous = null;
                    prev = i;
                }
            }
            static List<Instruction> CopyInstructionList(IEnumerable<Instruction> list)
            {
                List<Instruction> ret = new List<Instruction>();
                foreach(var i in list)
                {
                    var clone = i.MemberwiseClone() as Instruction;
                    clone._next = clone._previous = null;
                    ret.Add(clone);
                }
                return ret;
            }
           
            public Instructions(Instruction.Instructions copy) : base(CopyInstructionList(copy.Items))
            {
                Relink(this);
            }
            public Instructions(List<Instruction> list) : base(list)
            {
                Relink(this);
            }
            void CheckSorting(Instruction i)
            {
                if (i._previous != null && i._previous.Address >= i.Address) throw new Exception("Previous addres to big");
                if(i._next != null && i._next.Address <= i.Address) throw new Exception("Next addres to big");
            }
            protected override void ClearItems()
            {
                foreach (var item in this) item._next = item._previous = null;
                _fromOffset.Clear();
                base.ClearItems();
            }
            protected override void RemoveItem(int index)
            {
                var old = this[index]; 
                if (old.Previous != null) old._previous._next = old._next;
                if (old.Next != null) old._next._previous = old._previous;
                _fromOffset.Remove(old);
                base.RemoveItem(index);
            }
            protected override void InsertItem(int index, Instruction item)
            {
                Instruction before = null;
                if (index == this.Count) {// at the end
                    if(this.Count != 0) // first item in the list
                    {
                        before = this[this.Count - 1];
                        before._next = item;
                        item._previous = before;
                        item._next = null;
                        CheckSorting(item);
                    } 
                    base.InsertItem(index, item);
                } else
                {
                    base.InsertItem(index, item);
                    Relink(this); // hard way, try not to insert so much
                }
                _fromOffset.Add(item);
            }
            protected override void SetItem(int index, Instruction item)
            {
                var old = this[index]; // trickery!
                if (object.ReferenceEquals(item, old)) return; // don't do anything 
                item._next = old._next; // just swap them
                item._previous = old._previous; // just swap them
                old._next = old._previous = null;
                _fromOffset.Remove(old);
                _fromOffset.Add(item);
                CheckSorting(item);
                base.SetItem(index, item);
            }

            public int WriteTextLine(TextWriter wr)
            {
                int count = 0;
                foreach(var i in this)
                {
                    i.WriteTextLine(new PlainTextOutput(wr));
                    wr.WriteLine();
                    count++;
                }
                return count;
            }
            public void SaveInstructions(string filename)
            {
                StreamWriter sw = new StreamWriter(filename);
                WriteTextLine(sw);
                sw.Flush();
                sw.Close();
            }
        }
        static void ResolveLabels(Instructions instructions)
        {
            HashSet<Label> labels = new HashSet<Label>();
            foreach(var i in instructions)
            {
                if (i.Label != null) labels.Add(i.Label);
                if (i.Operand is Label) labels.Add(i.Operand as Label);
            }
            if (labels.Count > 0) foreach (var l in labels) Label.ResolveCalls(l, instructions);
        }
        // first pass simple create form a stream
        public static void ReplaceShortCircuitInstructionsAnds(Instructions input)
        {
            if (input.Count == 0) return;
            var node = input.First();
            do
            {
                Instruction i = node;
                Label label = i.Label;
                Label target = i.Operand as Label;
                if (i.Code == GMCode.Push && (i.OpCode & 0xFFFF) == 0)
                {
                    // We MIGHT have a short, check the statment before and after
                    Instruction before = i.Previous;
                    Instruction after = i.Next;
                    if(before != null && before.Code == GMCode.B && before.Operand == after.Label && after.Code == GMCode.Bf) // If it skips the 0 push and goes to the branch
                    { // We have a short, lets unoptimize this thing so the control graph dosn't go meh
                        foreach(var iref in i.Label.AllRefrencess) { // these are branches that don't need a stack, just change the operand
                            if (!iref.isBranch) throw new Exception("Something wrong boy");
                            iref.Operand = after.Operand;
                        }
                        if (!(after.Label.AllRefrencess.Count == 1 && after.Label.AllRefrencess[0] == before.Operand))
                        {
                            foreach (var iref in after.Label.AllRefrencess)  // these are branches HAVE to be Bf or Bt, change the op from after don't need a stack, just change the operand
                            {
                                if (!iref.isBranch) throw new Exception("Something wrong boy");
                                iref.ChangeInstruction(after.Code, after.Operand as Label);
                            }
                        }
                    //    input.Remove(after);
                        input.Remove(i);
                        input.Remove(before);
                        after.Label = null;
                        node = input.First();
                        ResolveLabels(input); // fix this
                        continue;
                    }
                }
                node = node.Next;
            } while (node != null);
        }
        public static void ReplaceShortCircuitInstructionsOrs(Instructions input)
        {
            if (input.Count == 0) return;
            var node = input.First();
            do
            {
                Instruction i = node;
                Label label = i.Label;
                Label target = i.Operand as Label;
                if (i.Code == GMCode.Push && (i.OpCode & 0xFFFF) == 1) // push.e 1
                {
                    // We MIGHT have a short, check the statment before and after
                    Instruction before = i.Previous;
                    Instruction after = i.Next;
                    if (before != null && before.Code == GMCode.B && before.Operand == after.Label && after.Code == GMCode.Bf) // This is still the same for ors
                    { // We have a short, lets unoptimize this thing so the control graph dosn't go meh
                        foreach (var iref in i.Label.AllRefrencess)
                        { // these are branches that don't need a stack, just change the operand
                            if (!iref.isBranch) throw new Exception("Something wrong boy");
                            iref.Operand = after.Operand;
                        }
                        if (!(after.Label.AllRefrencess.Count == 1 && after.Label.AllRefrencess[0] == before.Operand))
                        {
                            foreach (var iref in after.Label.AllRefrencess)  // these are branches HAVE to be Bf or Bt, change the op from after don't need a stack, just change the operand
                            {
                                if (!iref.isBranch) throw new Exception("Something wrong boy");
                                iref.ChangeInstruction(after.Code, after.Operand as Label);
                            }
                        }
                        //    input.Remove(after);
                        input.Remove(i);
                        input.Remove(before);
                        after.Label = null;
                        node = input.First();
                        ResolveLabels(input); // fix this
                        continue;
                    }
                }
                node = node.Next;
            } while (node != null);
        }
        public static Instructions Create(System.IO.BinaryReader r, List<string> StringIndex, List<string> InstanceList=null)
        {
            r.BaseStream.Position = 0;
            long lastpc = r.BaseStream.Length / 4;
            Instructions instructions = new Instructions();
            int pc = 0;
            List<Label> LabelsOutsideOfFuntion = new List<Label>();
            Dictionary<Label, Instruction> pushEnvLookup = new Dictionary<Label, Instruction>();
            Dictionary<int, Label> labels = new Dictionary<int, Label>();
            Func<int, Label> GetLabel = (int i) =>
            {
                Debug.Assert(i > 0);
                Label l;
                if (!labels.TryGetValue(i, out l))
                {
                    l = new Label(i, i > lastpc);
                    labels[i] = l;
                }
                return l;
            };
           
          //  Stack<Instruction> envStack = new Stack<Instruction>();
            while (r.BaseStream.Length > r.BaseStream.Position)
            {
                Instruction inst = new Instruction(pc, r);
                inst.InstanceLookup = InstanceList; // hack I know but I want prity print
                inst.StringLookup = StringIndex; 
                instructions.Add(inst);
                System.Diagnostics.Debug.Assert(inst.Address == pc);
                if (inst.Code.isBranch())// || inst.Code == GMCode.Pushenv || inst.Code == GMCode.Popenv) // pushenv also has a branch 
                { 
                    int target = GMCodeUtil.getBranchOffset(inst.OpCode);
                    target += pc;
                    inst.Operand = GetLabel(target);
                } else if(inst.Code == GMCode.Pushenv)
                {
                    int target = GMCodeUtil.getBranchOffset(inst.OpCode);
                    target += pc+1;
                    Label l = GetLabel(target);
                    inst.Operand = l;
                    pushEnvLookup.Add(l, inst);
                }
                else if (inst.Code == GMCode.Popenv)
                {
                    int target = GMCodeUtil.getBranchOffset(inst.OpCode);
                    if(target == 0) // its a popbreak so we have to find the previous push
                    { // this will SO screw up if we are in a loop or doing a branch ugh
                        inst.Operand = null;
                        foreach (var pushI in instructions.Reverse())
                        {
                            if(pushI.Code == GMCode.Pushenv)
                            {
                                inst.Operand = pushI;
                                break;
                            }
                        }
                        Debug.Assert(inst.Operand != null);
                    } else
                    {
                        target += pc - 1; // points to the instruction
                        Label l = GetLabel(target);
                        inst.Operand = l;
                    }
                }
                pc += inst.Size;
            }

            /*
            LabelsOutsideOfFuntion.Sort();
            foreach (var l in LabelsOutsideOfFuntion)
            {
                Instruction i = new Instruction(l.Address, GMCode.Exit.toUInt(GM_Type.Var));// add filler op 
                i.Label = l; // give it the label
                instructions.AddLast(i);
            }
            */
            // Link all the instructions together, mabye I should just include linkList

            // This takes a LOOONG time, so lets shorten it up, we don't need the back refrences
            //if (labels.Count > 0) foreach (var l in labels) Label.ResolveCalls(l.Value, instructions);
            if (labels.Count > 0) foreach (var l in labels) Label.SimpleResolveCalls(instructions,labels);
            
           
            // now fix string indexes 
            foreach (var i in instructions)
            {
                if(i.Operand != null)
                {
                    switch (i.Code)
                    {
                        case GMCode.Push:
                            switch (i.FirstType)
                            {
                                case GM_Type.String:
                                    i.Operand = GMCodeUtil.EscapeString(StringIndex[(int)i.Operand]);
                                    break;
                                case GM_Type.Var:
                                    i.Operand = StringIndex[(int)i.Operand];
                                    break;
                            }
                            break;
                        case GMCode.Call:
                            i.Operand = StringIndex[(int)i.Operand & 0x1FFFFF];
                            break;
                        case GMCode.Pop:
                            i.Operand = StringIndex[(int)i.Operand & 0x1FFFFF];
                            break;
                    }
                }
            }
            instructions.SaveInstructions("before_pre.txt");
        //    ReplaceShortCircuitInstructionsOrs(instructions);
        //    ReplaceShortCircuitInstructionsAnds(instructions);
            instructions.SaveInstructions("after_pre.txt");
            return instructions;
        }
    
        public override bool Equals(object obj)
        {
            if (Object.ReferenceEquals(obj, this)) return true;
            Instruction l = obj as Instruction;
            if (l == null) return false;
            return Equals(l);
        }
        public override int GetHashCode()
        {
            return Address;
        }
        public int CompareTo(Label other)
        {
            return Address.CompareTo(other.Address);
        }
        public int CompareTo(Instruction other)
        {
            return Address.CompareTo(other.Address);
        }
        public bool Equals(Instruction other)
        {
            if (Object.ReferenceEquals(other, null)) return false;
            if (Object.ReferenceEquals(other, this)) return true;
            return other.Address == Address;
        }

        public  void WriteTo(ITextOutput output)
        {
            WriteTextLine(output);
        }
    }
}