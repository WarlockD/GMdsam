using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.Dissasembler
{
    public struct OpCode : IEquatable<OpCode>
    {
        readonly uint op;

        public string Name { get { return Code.GetName(); } }
        public GMCode Code { get { return ((GMCode)(op >> 24)); } }

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
                        return ((GM_Type)((op >> 16) & 0xF)).GetSize() + 1;
                    default:
                        return 1;
                }
            }
        }

        public uint Op
        {
            get { return op; }
        }

        public short ShortValue
        {
            get { return (short)(op & 0xFFFF); }
        }
        public int BranchOffset
        {
            get
            {
                var offset = op & 0xFFFFFF;
                if ((offset & 0x800000) != 0) offset |= 0xFF000000;
                return unchecked ((int)offset);
            }
        }
        internal OpCode(uint o)
        {
            this.op = o;
        }

        public override int GetHashCode()
        {
            return (int)this.op;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is OpCode))
                return false;

            var opcode = (OpCode)obj;
            return opcode.op == this.op;
        }

        public bool Equals(OpCode opcode)
        {
            return opcode.op == this.op;
        }

        public static bool operator ==(OpCode one, OpCode other)
        {
            return one.op == other.op;
        }

        public static bool operator !=(OpCode one, OpCode other)
        {
            return one.op != other.op;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
