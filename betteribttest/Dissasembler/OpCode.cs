using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.Dissasembler
{
    public static class InstructionHelper {
        public static void ReLinkList(this IEnumerable<Instruction> list)
        {
            Instruction prev = null;
            foreach(var i in list.OrderBy(x => x.Address))
            {
                i.Previous = prev;
                if (prev == null) prev = i;
                else prev.Next = i;
            }
        }
        public static void DebugSaveList(this IEnumerable<Instruction> list, string filename) {
            using (StreamWriter sr = new StreamWriter(filename))
            {
                foreach(var i in list) sr.WriteLine(i.ToString());
            }
        }
    }
    public class Label : IEquatable<Instruction>, IEquatable<Label>
    {
        int _address;
        public int Address { get { return _address; } }
        public Label(int address) { this._address = address; }
        public override string ToString()
        {
            return "L" + _address;
        }
        public Instruction Origin = null;
        public List<Instruction> Refrences = new List<Instruction>();
        public bool Equals(Instruction other)
        {
            return other.Address == Address;
        }
        public bool Equals(Label other)
        {
            return other.Address == Address;
        }
        public override bool Equals(object obj)
        {
            if (!object.ReferenceEquals(obj, null))
            {
                if (object.ReferenceEquals(obj, this)) return true;
                Label ltest = obj as Label;
                if (ltest != null) return Equals(ltest);
                Instruction itest = obj as Instruction;
                if (itest != null) return Equals(itest);
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Address;
        }
    }
    public class Instruction : IEquatable<Instruction>, IComparable<Instruction>
    {
        public Instruction Next = null;
        public Instruction Previous = null;
        int _address;
        int _extra; // usally the short after a pop/push
        GMCode _code = GMCode.BadOp;
        public GMCode Code {  get { return _code; } set { _code = value; } }
        // extra data.  Could be branch address, could be instance data
        public int Extra { get { return _extra; } }
        public int Address { get { return _address; } } // address MUST be diffrent, it what makes each Instruction truely unique
        public GM_Type[] Types = null;
        public object Operand = null;
        public string OperandText = null; // helper string that prity prints the Operand.
        public string Comment = null; // helper string that prints after the end of the asemble line
        public Label Label = null; // Label to here from a branch
        public bool TryParseOperand<T>(out T value) where T : IConvertible
        {
            if (Operand != null && Types == null) {
                IConvertible test = Operand as IConvertible;
                if (test != null)
                {
                    value = (T)Convert.ChangeType(Operand, typeof(T));
                    return true;
                }
            }
            value = default(T);
            return false;
        }
        public bool TryParseOperand(out short value)
        {
            if (Types != null)
            {
                if (Types[0] == GM_Type.Short)
                {
                    value = (short)_extra; // specal case, general case you use int
                    return true;
                }
            }
            value = default(short);
            return false;
        }
        // Override a very common useage
        public bool TryParseOperand(out int value) {
            do
            {
                if (Operand == null || Types == null) break;
                if (Types[0] == GM_Type.Int || Types[0] == GM_Type.Short || Operand is int)
                {
                    value = (int)Operand;
                    return true;
                }
            } while (false);
            value = default(int);
            return false;
        }
        // we want to detect if the operand IS a string, not that it can be converted
        // to one
        public bool TryParseOperand(out string value)
        {
            if (Operand != null && Types == null)
            {
                string ret = Operand as string;
                if(ret != null)
                {
                    value = ret;
                    return true;
                }
            }
            value = default(string);
            return false;
        }
        
        public Instruction(int address, GMCode code)
        {
            _address = address;
            _code = code;
        }
        public Instruction(int address)
        {
            _address = address;
        }
        public bool Equals(Instruction i)
        {
            return i.Address == Address;
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) return false;
            if (object.ReferenceEquals(obj, this)) return true;
            Instruction test = obj as Instruction;
            return test != null && Equals(test);
        }
        public override int GetHashCode()
        {
            return Address;
        }
        public int Size
        {
            get
            {
                switch (Code)
                {
                    case GMCode.Pop:
                    case GMCode.Call:
                        return 2;
                    case GMCode.Push:
                        return Types[0].GetSize() + 1;
                    default:
                        return 1;
                }
            }
        }
        public bool isUnconditionalBranch { get { return Code == GMCode.B || Code == GMCode.Exit || Code == GMCode.Ret; } }
        public bool isConditionalBranch
        {
            get { return Code == GMCode.Bt || Code == GMCode.Bf; }
        }
        public bool isBranch
        {
            get { return Code == GMCode.Bt || Code == GMCode.Bf || Code == GMCode.B; }
        }
        public int CompareTo(Instruction other)
        {
            return Address.CompareTo(other.Address);
        }
        /// <summary>
        /// Diassembles a raw opcode.  
        /// </summary>
        /// <param name="i"></param>
        /// <param name="raw"></param>
        /// <returns>Returns truee if operand is needed</returns>
        public static bool DissasembleRawCode(Instruction i,  uint raw)
        {
            i.Code = GMCodeUtil.getFromRaw(raw);
            i.Types = null;
            i.Operand = null;
            i.OperandText = null; // we clear eveything just in case
            i._extra = (short)(0xFFFF & raw); // default for almost eveything
            switch (i.Code)
            {
                
                case GMCode.Call:
                    i.Types = new GM_Type[] { (GM_Type)((raw >> 16) & 15) };
                    return true;
                case GMCode.Exit:
                case GMCode.Ret:
                case GMCode.Not:
                case GMCode.Neg:
                case GMCode.Popz:
                    i.Types = new GM_Type[] { (GM_Type)((raw >> 16) & 15) };
                    break;
                case GMCode.Pop:
                    i.Types = new GM_Type[] {  (GM_Type)((raw >> 16) & 15),(GM_Type)((raw >> 20) & 15) };
                    return true;
                case GMCode.Push:
                    i.Types = new GM_Type[] { (GM_Type)((raw >> 16) & 15) };
                    // this should be in the operand, but just in case make sure extra is right
                    if (i.Types[0] == GM_Type.Short)
                    {
                        i.Operand = i._extra; // convert it to int
                        break; // set the operand ourselfs don't need it
                    }
                    else return true; // need to read an operand
                case GMCode.Add:
                case GMCode.Sub:
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                case GMCode.Or:
                case GMCode.And:
                case GMCode.Dup:
                case GMCode.Sal:
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Sgt:
                case GMCode.Sle:
                case GMCode.Sne:
                case GMCode.Conv:
                case GMCode.Rem:
                case GMCode.Slt:
                case GMCode.Xor:
                    i.Types = new GM_Type[] { (GM_Type)((raw >> 16) & 15), (GM_Type)((raw >> 20) & 15) };
                    break;
                case GMCode.Break:
                    //  i._extra = (int)(0x00FFFFFFF & raw); // never seen the need for more than this
                    break;
                case GMCode.B:
                case GMCode.Bf:
                case GMCode.Bt:
                    i._extra = i.Address + GMCodeUtil.getBranchOffset(raw);
                    break;
                case GMCode.Popenv:
                  //  Debug.WriteLine("Popenv: Address: {0}, Extra: {1} {1:X8}  Calc: {2}",i.Address, raw, GMCodeUtil.getBranchOffset(raw));
                    if (0xBCF00000 == raw) // its a popbreak
                        i._extra = 0;
                    else
                        i._extra = i.Address + GMCodeUtil.getBranchOffset(raw);
                    break;
                case GMCode.Pushenv:
                //    Debug.WriteLine("Pushenv: Address: {0}, Extra: {1} {1:X8}  Calc: {2}",i.Address, raw, GMCodeUtil.getBranchOffset(raw));
                    i._extra = i.Address + GMCodeUtil.getBranchOffset(raw);
                    break;
                case GMCode.BadOp:
                    throw new Exception("Bad opcode?");
             
                default:
                    throw new Exception("Unkonwn opcode");
            }
            return false;
        }
        public static Instruction DissasembleFromReader(int address, BinaryReader r)
        {
            Instruction ret = new Instruction(address);
            uint raw = r.ReadUInt32();
            ret.Code = GMCodeUtil.getFromRaw(raw);
            if (DissasembleRawCode(ret, raw))
            {
                switch (ret.Code)
                {
                    case GMCode.Call:
                    case GMCode.Pop:
                        ret.Operand = r.ReadInt32();
                        break;
                    case GMCode.Push:
                        {
                            switch (ret.Types[0])
                            {
                                case GM_Type.Long:
                                    ret.Operand = r.ReadInt64();
                                    break;
                                case GM_Type.Double:
                                    ret.Operand = r.ReadDouble();
                                    break;
                                case GM_Type.Float:
                                    ret.Operand = r.ReadSingle();
                                    break;
                                case GM_Type.Bool:
                                    ret.Operand = r.ReadInt32() != 0 ? true : false; // tested, yess this is silly
                                    break;
                                case GM_Type.String:
                                case GM_Type.Var:
                                case GM_Type.Int:
                                    ret.Operand = r.ReadInt32();
                                    break;
                                case GM_Type.Short:
                                    break; // already read in DissasembleRawCode
                            }
                        }
                        break;
                }
                Debug.Assert(ret.Operand != null);
            }
            return ret;
        }
      
        public static SortedList<int, Instruction> Dissasemble(Stream stream, GMContext context)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (!stream.CanRead) throw new IOException("Cannot read stream");
            if(!stream.CanSeek) throw new IOException("Cannot seak stream");
            stream.Position = 0;
            BinaryReader r = new BinaryReader(stream);
            return Dissasemble(r, stream.Length, context);
        }
        // I had multipul passes on this so trying to combine it all to do one pass
        public static  SortedList<int,Instruction> Dissasemble(BinaryReader r, long length, GMContext context)
        {
            if (r == null) throw new ArgumentNullException("stream");
            if (r.BaseStream.CanRead != true) throw new IOException("Cannot read stream");
            int pc = 0;
            long lastpc = r.BaseStream.Length / 4;
            SortedList<int, Instruction> list = new SortedList<int, Instruction>();
            Dictionary<int,Label> labels = new Dictionary<int, Label>();
            Dictionary<int, Label> pushEnviroment = new Dictionary<int, Label>();
            List<Instruction> branches = new List<Instruction>();
            Func<Instruction, int,Label> GiveInstructionALabel = (Instruction inst, int laddress) =>
             {
                 Label label;
                 if(!labels.TryGetValue(laddress,out label)) labels.Add(laddress, label = new Label(laddress));
                 inst.Operand = label;
                 branches.Add(inst); // add it to the branches to handle latter
                 return label;
             };
          
            Instruction prev = null;
            StringBuilder sb = new StringBuilder(50);
            while(r.BaseStream.Position < length)
            {
                Instruction i = DissasembleFromReader(pc, r);
                pc += i.Size;
                switch (i.Code)
                {
                    case GMCode.Pushenv:
                        {
                            Label endOfEnviroment = GiveInstructionALabel(i, i.Extra +1);// skip the pop
                            i.Operand = endOfEnviroment;
                            pushEnviroment.Add(i.Address, endOfEnviroment);
                            branches.Add(i);
                        }
                        break;
                    case GMCode.Popenv:
                        {
                            Label endOfEnviroment;
                            if (pushEnviroment.TryGetValue(i.Extra - 1, out endOfEnviroment))
                            {
                                i.Operand = GiveInstructionALabel(i, i.Extra - 1);  // not a break, set the label BACK to the push as we are simulating a loop
                            }
                            else {
                                foreach(Instruction ii in list.Values.Reverse())
                                {
                                    if(ii.Code == GMCode.Pushenv)
                                    {
                                        i.Operand = GiveInstructionALabel(i, ii.Address); // we want to make these continues
                                        break;
                                    }
                                }
                            }
                            branches.Add(i);
                            Debug.Assert(i.Operand != null);       
                        }
                        break;
                    case GMCode.Bf:
                    case GMCode.Bt:
                    case GMCode.B:
                        GiveInstructionALabel(i, i.Extra);
                        break;
                    case GMCode.Push:
                        if (i.Types[0] == GM_Type.Var)
                        {
                            // i.Operand = StringList[(int)i.Operand]; Don't want to touch as it might 
                            sb.Clear();
                            sb.Append(context.InstanceToString(i.Extra));
                            sb.Append('.');
                            sb.Append(context.LookupString((int)i.Operand & 0xFFFFF));
                            i.OperandText = sb.ToString();
                        }
                        else if (i.Types[0] == GM_Type.String)
                        {
                            i.Operand = context.LookupString((int)i.Operand & 0xFFFFF);
                            i.OperandText = GMCodeUtil.EscapeString(i.Operand as string);
                        }
                        break;
                    case GMCode.Pop: // technicaly pop is always a var, but eh
                                     //i.Operand = StringList[(int)i.Operand]; Don't do it this wa as we might need to find out of its an array
                        sb.Clear();
                        sb.Append(context.InstanceToString(i.Extra));
                        sb.Append('.');
                        sb.Append(context.LookupString((int)i.Operand & 0xFFFFF));
                        i.OperandText = sb.ToString();
                        break;
                    case GMCode.Call:
                        i.OperandText = context.LookupString((int)i.Operand & 0xFFFFF);
                        i.Operand = i.OperandText;
                        break;

                }
                i.Previous = prev;
                if (prev == null) prev = i;
                else prev.Next = i;
                list.Add(i.Address, i);
            }
            if (list.Count == 0) return list; // return list, its empty

            Instruction last = list.Last().Value;
            if(last.Code != GMCode.Exit || last.Code != GMCode.Ret)
            {
                int address = last.Address + last.Size;
                list.Add(address, new Instruction(address, GMCode.Exit));
            } // must be done for graph and in case we have a branch that goes right outside.  Its implied in any event
            foreach (var i in branches)
            {
                Label l = i.Operand as Label;
                if(l.Origin == null) // Link the label 
                {
                    l.Origin = list[l.Address];
                    list[l.Address].Label = l;
                }
            }
            return list;
        }
        public void WriteTo(ITextOutput output)
        {
            output.Write(ToString());
        }
        void FormatPrefix(StringBuilder line)
        {
            line.AppendFormat("{0,-5}{1,-5} ", Address, this.Label == null ? "" : "L" + this.Label.Address);
            line.Append(this.Code.GetName());
            if (Types != null)
            {
                foreach (var t in Types)
                {
                    string ts = null;
                    switch (t)
                    {
                        case GM_Type.Double: ts = ".d"; break;
                        case GM_Type.Float: ts = ".f"; break;
                        case GM_Type.Int: ts = ".i"; break;
                        case GM_Type.Long: ts = ".l"; break;
                        case GM_Type.Bool: ts = ".b"; break;
                        case GM_Type.Var: ts = ".v"; break;
                        case GM_Type.String: ts = ".s"; break;
                        case GM_Type.Short: ts = ".e"; break;
                    }
                    line.Append(ts);
                }
            }
            // so we should have 28 spaces here
            if (line.Length < 21) line.Append(' ', 21 - line.Length);
        }
        void FormatPostFix(StringBuilder line)
        {
            if (Comment != null)
            {
                if (line.Length < 40) line.Append(' ', 40 - line.Length); // make sure all the comments line up
                line.Append("; ");
                line.Append(Comment);
            }
        }
        public override string ToString()
        {
            StringBuilder line = new StringBuilder();
            FormatPrefix(line);
            if (Operand != null)
            {
                if (OperandText != null) line.Append(OperandText);
                else line.Append(Operand.ToString());
                if (Code == GMCode.Pop && Types[0] == GM_Type.Var && ((int)Operand) >=0) line.Append("[]");
            }
            FormatPostFix(line);
            return line.ToString();
        }
    }
}
