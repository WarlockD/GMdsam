using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;

namespace betteribttest
{
 
    public class Label :  IComparable<Label>, IComparable<Instruction>, IEquatable<Label>
    {
        internal SortedSet<Instruction> _callsTo;
        public IReadOnlyCollection<Instruction> CallsTo { get { return _callsTo; } }
        public int Target { get; internal set; }

        public LinkedListNode<Instruction> InstructionTarget { get; set; }
        internal Label(int offset)
        {
            this.Target = offset;
            _callsTo = new SortedSet<Instruction>();
            InstructionTarget = null;
        }
        internal Label(Instruction i)
        {
            this.Target = i.Offset + GMCodeUtil.getBranchOffset(i.OpCode);
            _callsTo = new SortedSet<Instruction>();
            AddCallsTo(i);
            InstructionTarget = null;
        }
        public void AddCallsTo(Instruction i)
        {
            //System.Diagnostics.Debug.Assert(i.GMCode == GMCode.B || i.GMCode == GMCode.Bf || i.GMCode == GMCode.Bt);
            _callsTo.Add(i);
        }
        public override string ToString()
        {
            if (Target == 0)
                return "Start";
            else
                return String.Format("Label_{0}", Target);
        }
        public override bool Equals(object obj)
        {
            if (Object.ReferenceEquals(obj, this)) return true;
            Label l = obj as Label;
            if (l == null) return false;
            return Equals(l);
        }
        public override int GetHashCode()
        {
            return Target;
        }

        public int CompareTo(Label other)
        {
            return Target.CompareTo(other.Target);
        }
        public int CompareTo(Instruction other)
        {
            return Target.CompareTo(other.Offset);
        }
        public bool Equals(Label l)
        {
            return l.Target == Target;
        }
    }
    public class Instruction : AstStatement, IComparable<Label>, IComparable<Instruction>, IEquatable<Instruction>
    {
        const uint breakPopenvOpcode = (uint)(((uint)(GMCode.Popenv)) << 24 | 0x00F00000);
        byte[] raw_opcode;
        int _operandInt;
        protected override bool PrintHeader { get { return true; } }
        // makes it a bad op code that just has the offset and the operand
        public void MakeAst()
        {
            OpCode = 0;
        }
     
        public uint OpCode { get; private set; }
        public GMCode GMCode { get { return GMCodeUtil.getFromRaw(OpCode); } }
        public object Operand { get; set; }
        public int OperandInt { get { return _operandInt; } }
        internal Instruction(int offset, uint opCode) :base(offset)
        {
            this.OpCode = opCode;
            this.Operand = null;
            this._operandInt = 0;
        }
        internal Instruction(int offset, BinaryReader r) : base(offset)
        {
            this.Operand = null;
           // raw_opcode = r.ReadBytes(sizeof(int));
            this.OpCode = r.ReadUInt32();
            raw_opcode = BitConverter.GetBytes(this.OpCode);
            if (Size > 1)
            {
               // Array.Resize(ref raw_opcode, size * sizeof(int));
              //  r.Read(raw_opcode, 4, (size - 1) * sizeof(int));
                switch (this.GMCode)
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
        public int GetOperandPatch()
        {
            if (Operand != null && Operand is int) return (int)Operand & 0x1FFFFFF;
            throw new Exception("Needed that operand");
        }
        public override void DecompileToText(TextWriter wr)
        {
            int opcode = (int)((OpCode >> 24) & 0xFF);
            int type = (int)((OpCode >> 16) & 0xFF);
            string str;
            int spacing = 0;
          //  int line_start = FormatHeadder(indent, sb);
            str = ((GMCode)opcode).GetName();
            spacing += str.Length;
            wr.Write(str);

            if ((opcode & 160) == 128)
            {
                wr.Write(GMCodeUtil.TypeToStringPostfix(type & 0xF));
                spacing += 2;
            }
            else if ((opcode & 160) == 0)
            {
                wr.Write(GMCodeUtil.TypeToStringPostfix(type & 0xF));
                wr.Write(GMCodeUtil.TypeToStringPostfix(type >> 4));
                spacing += 4;
            }
            wr.Write(new string(' ', 20- spacing));

           // if(GMCode.IsConditional() && Operand is Label)
           if(Operand is Label)
            {
                str = Operand.ToString();
                wr.Write(str);
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
                        wr.Write(Operand.ToString());
                        break;
                    case GM_Type.Var:
                        wr.Write(GMCodeUtil.lookupInstanceFromRawOpcode(OpCode));
                        wr.Write('.');
                        if (Operand is int) wr.Write("${0}$", ((int)Operand & 0x1FFFFFF));
                        else wr.Write(Operand.ToString());
                        if (GMCodeUtil.IsArrayPushPop(OpCode)) wr.Write("[]");
                        break;
                    case GM_Type.String:
                        if (Operand is int)
                            wr.Write("\"{0}\"", ((int)Operand & 0x1FFFFFF));
                        else goto case GM_Type.Bool;

                        break;
                    case GM_Type.Short:
                        wr.Write((short)(OpCode << 16 >> 16));
                        break;
                }
            }
            else if ((opcode & 32) != 0)
            { //// ooooooooooh
                if(OpCode == breakPopenvOpcode)
                {
                    wr.Write("break enviroment");

                } else if (GMCode.IsConditional() || GMCode == GMCode.Pushenv || GMCode == GMCode.Popenv)
                {
                    int offset = GMCodeUtil.getBranchOffset(OpCode);
                    wr.Write("{0}", Offset-offset);
                } else
                {
                    int new_offset = Offset + (int)(OpCode << 8 >> 8); // << 8 >> 6 // changed cause we are doing pc by int
                    wr.Write("0x{0:x8}", new_offset);
                }
            }
        }
        public class Instructions : LinkedList<Instruction>
        {
            public SortedDictionary<int, Label> labels = new SortedDictionary<int, Label>();
            public SortedList<int, LinkedListNode<Instruction>> list = new SortedList<int, LinkedListNode<Instruction>>();
            public LinkedList<Instruction> linkedList = new LinkedList<Instruction>();
            public Instructions() : base() { }
            public LinkedListNode<Instruction> MatchFrom(LinkedListNode<Instruction> start, Predicate<Instruction> match)
            {
                while (start != null) if (match(start.Value)) return start;
                return null;
            }
            public IEnumerable<Instruction> MatchMany(Predicate<Instruction> match)
            {
                var start = First;
                while (start != null) if (match(start.Value)) yield return start.Value;
            }
            public Instruction MatchOne(Predicate<Instruction> match)
            {
                var start = MatchFrom(First, match);
                return start != null ? start.Value : null;
            }

            public IEnumerable<LinkedListNode<Instruction>> MatchMany(params Predicate<Instruction>[] toMatch)
            {
                var start = First;
                List<Instruction> ret = new List<Instruction>();
                while (start != null)
                {
                    var match_node = start;
                    var testNode = start;
                    ret.Clear();
                    foreach (var match in toMatch)
                    {
                        if (testNode == null) yield break; // we are at the end of the list
                        if (!match(testNode.Value)) break; 
                        ret.Add(testNode.Value);
                        testNode = testNode.Next;
                    }
                    start = testNode.Next; // get the next node in case we delete the previous nodes
                    if (ret.Count == toMatch.Length) yield return match_node;
                }
            }
            public int RemoveAll(Predicate<Instruction> match)
            {
                if (list == null)
                {
                    throw new ArgumentNullException("list");
                }
                if (match == null)
                {
                    throw new ArgumentNullException("match");
                }
                var count = 0;
                var node = First;
                
                while (node != null)
                {
                    var next = node.Next;
                    if (match(node.Value))
                    {
                        Remove(node);
                        count++;
                    }
                    node = next;
                }
                return count;
            }
   
        }
        // first pass simple create form a stream
     
        public static Instructions Create(System.IO.BinaryReader r, List<string> StringIndex)
        {
            r.BaseStream.Position = 0;
            Instructions instructions = new Instructions();
            int pc = 0;
            while (r.BaseStream.Length > r.BaseStream.Position)
            {
                Instruction inst = new Instruction(pc, r);
                instructions.AddLast(inst);
                System.Diagnostics.Debug.Assert(inst.Offset == pc);
                if (inst.GMCode.IsConditional() || inst.GMCode == GMCode.Pushenv || (inst.GMCode == GMCode.Popenv && inst.OpCode != breakPopenvOpcode))// || inst.GMCode == GMCode.Pushenv || inst.GMCode == GMCode.Popenv) // pushenv also has a branch 
                { 
                    int target = GMCodeUtil.getBranchOffset(inst.OpCode);
                    target += pc;
                    Label l;
                    if (!instructions.labels.TryGetValue(target, out l))
                    {
                        l = new Label(inst);
                        instructions.labels.Add(target, l);
                    }
                    else l.AddCallsTo(inst);
                    inst.Operand = l;
                }
                pc += inst.Size;
            }
            if(instructions.labels.Count > 0)
            {
                // Check if we have a label that goes to the end of the arguments
                var last_label = instructions.labels.Last();
                if (instructions.Last.Value.Offset < last_label.Key) instructions.AddLast(new Instruction(last_label.Key, ((int)GMCode.BadOp) << 24)); // add filler op
                var start = instructions.First;
                while(start != null)
                {
                    Instruction l = start.Value;
                    Label label;
                    if (instructions.labels.TryGetValue(l.Offset, out label))
                    {
                        l.Label = label;
                        label.InstructionTarget = start;
                        instructions.labels.Remove(l.Offset);
                    }
                    start = start.Next;
                }
                Debug.Assert(instructions.labels.Count == 0);
            }
           
            // now fix string indexes 
            foreach(var i in instructions)
            {
                if(i.Operand != null)
                {
                    switch (i.GMCode)
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
            return Offset;
        }
        public int CompareTo(Label other)
        {
            return Offset.CompareTo(other.Target);
        }
        public int CompareTo(Instruction other)
        {
            return Offset.CompareTo(other.Offset);
        }
        public bool Equals(Instruction other)
        {
            if (Object.ReferenceEquals(other, this)) return true;
            return other.Offset == Offset;
        }
 
    }
}