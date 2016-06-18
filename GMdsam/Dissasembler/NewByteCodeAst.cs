using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.Ast;
using System.IO;
using System.Diagnostics;

namespace GameMaker.Dissasembler
{
    public enum NewOpcodeCondtions
    {
        Bad = 0,
        Lt,
        Leq,
        Eq,
        Neq,
        Gte,
        Gt
    };
    public enum NewOpcode
    {
        popv = 5,
        conv = 7,
        mul = 8,
        div = 9,
        rem = 10,
        mod = 11,
        @add = 12,
        sub = 13,
        and = 14,
        or = 15,
        xor = 16,
        neg = 17,
        not = 18,
        shl = 19,
        shr = 20,
        @set = 21,
        // Set seems to be like a cmp for other stuff
        // 1 : <
        // 2 : <=
        // 3 : ==
        // 4 : !=
        // 5 : >=
        // 6 : = >
        pop = 69,
        pushv = 128,
        pushi = 132, // push int? ah like a pushe
        dup = 134,
        //  call = 153,
        ret = 156,
        exit = 157,
        popz = 158,
        b = 182,
        bt = 183,
        bf = 184,
        pushenv = 186,
        popenv = 187,
        push = 192, // generic? -1
        pushl = 193, // local? -7
        pushg = 194, // global? -5 // id is the last bit?
        pushb = 195, // built in? hummmm
        call = 217,
        @break = 255,
    };
    // Going to try to skip the "to instruction" step as I am faimuar enough with it

    public class NewByteCodeToAst : BuildAst
    {
        // Generaly the new bytecode is very similar to the old byte code
        // compile wise.  The major diffrences is the introduction to scope spficific pushes (pushl, pushg, etc) and
        // changing conditions to an op of @set, witch isn't a bad idea when you think of it
        // I will probery convert the AST code to use something else so that I can keep the ILAst abstracted from
        // the bytecode
        // This pass accepts index or instance values being 
        Dictionary<int, ILLabel> pushEnviroment = new Dictionary<int, ILLabel>();
        protected override void Start(LinkedList<ILNode> list)
        {
            pushEnviroment = new Dictionary<int, ILLabel>();
        }
        /// <summary>
        /// After label resolving and right before returning
        /// </summary>
        /// <param name="list"></param>
        protected override void Finish(LinkedList<ILNode> list)
        {
            pushEnviroment = null;
        }
        protected override ILExpression CreateExpression(LinkedList<ILNode> list)
        {
            ILExpression e = null;
            NewOpcode nOpCode = (NewOpcode)(CurrentRaw >> 24);
            GM_Type[] types = ReadTypes(CurrentRaw);
            switch (nOpCode) // the bit switch
            {
                case NewOpcode.conv:
                    if (list.Last != null)
                    {
                        var prev = list.Last.Value as ILExpression;
                        Debug.Assert(prev.Code != GMCode.Pop);
                        prev.ILRanges.Add(new ILRange(CurrentPC, CurrentPC));
                        prev.Types = types;
                    }
                    break;// ignore all Conv for now
                case NewOpcode.popz: e = CreateExpression(GMCode.Popz, types); break;
                case NewOpcode.mul: e = CreateExpression(GMCode.Mul, types); break;
                case NewOpcode.div: e = CreateExpression(GMCode.Div, types); break;
                case NewOpcode.rem: e = CreateExpression(GMCode.Rem, types); break;
                case NewOpcode.mod: e = CreateExpression(GMCode.Mod, types); break;
                case NewOpcode.@add: e = CreateExpression(GMCode.Add, types); break;
                case NewOpcode.sub: e = CreateExpression(GMCode.Sub, types); break;
                case NewOpcode.and: e = CreateExpression(GMCode.And, types); break;
                case NewOpcode.or: e = CreateExpression(GMCode.Or, types); break;
                case NewOpcode.xor: e = CreateExpression(GMCode.Xor, types); break;
                case NewOpcode.neg: e = CreateExpression(GMCode.Neg, types); break;
                case NewOpcode.not: e = CreateExpression(GMCode.Not, types); break;
                case NewOpcode.shl: e = CreateExpression(GMCode.Sal, types); break;
                //  case NewOpcode.shr:     e = CreateExpression(GMCode.Saa, types); break; // hack, handle shift right
                case NewOpcode.@set:
                    switch ((CurrentRaw >> 8) & 0xFF)
                    {
                        case 1: e = CreateExpression(GMCode.Slt, types); break;
                        case 2: e = CreateExpression(GMCode.Sle, types); break;
                        case 3: e = CreateExpression(GMCode.Seq, types); break;
                        case 4: e = CreateExpression(GMCode.Sne, types); break;
                        case 5: e = CreateExpression(GMCode.Sge, types); break;
                        case 6: e = CreateExpression(GMCode.Sgt, types); break;
                        default:
                            throw new Exception("Bad condition");
                    }
                    break;

                case NewOpcode.dup:
                    e = CreateExpression(GMCode.Dup, types);
                    e.Operand = (int)(CurrentRaw & 0xFFFF); // dup type
                    break;
                case NewOpcode.call:
                    e = CreateExpression(GMCode.CallUnresolved, types);
                    e.Operand = ILCall.CreateCall(File.Strings[r.ReadInt32()], (int)(CurrentRaw & 0xFFFF));
                    // since we can have var args on alot of functions, extra is used
                    break;
                case NewOpcode.ret: e = CreateExpression(GMCode.Ret, types); break;
                case NewOpcode.exit: e = CreateExpression(GMCode.Exit, types); break;
                case NewOpcode.b: e = CreateLabeledExpression(GMCode.B); break;
                case NewOpcode.bt: e = CreateLabeledExpression(GMCode.Bt); break;
                case NewOpcode.bf: e = CreateLabeledExpression(GMCode.Bf); break;

                // We have to fix these to a lopp to emulate a while latter
                case NewOpcode.pushenv:
                    {
                        //  Debug.WriteLine("Popenv: Address: {0}, Extra: {1} {1:X8}  Calc: {2}",i.Address, raw, GMCodeUtil.getBranchOffset(raw));
                        int sextra = CurrentPC + GMCodeUtil.getBranchOffset(CurrentRaw);
                        e = CreateExpression(GMCode.Pushenv, types, GetLabel(sextra + 1)); // we are one instruction after the pop
                        pushEnviroment.Add(sextra, GetLabel(CurrentPC)); // record the pop position
                    }
                    break;
                case NewOpcode.popenv:
                    {
                        // We convert this to a Branch so the loop detecter will find it
                        e = CreateExpression(GMCode.B, types);
                        // e = CreateExpression(GMCode.Popenv, types);
                        if (CurrentRaw == 0xBBF00000)// its a break, ugh
                        {
                            var last = list.Last;
                            while (last != null)
                            {
                                ILExpression pushe = last.Value as ILExpression;
                                if (pushe.Code == GMCode.Pushenv)
                                {
                                    e.Operand = pushe.Operand;
                                    break;
                                }
                                last = last.Previous;
                            }
                            Debug.Assert(last != null);
                        }
                        else
                        {
                            // some reason its the negitive offset?
                            // int offset = GMCodeUtil.getBranchOffset(CurrentRaw) - currentPC;
                            ILLabel endOfEnviroment;
                            if (pushEnviroment.TryGetValue(CurrentPC, out endOfEnviroment)) // this is the code
                                e.Operand = endOfEnviroment;  // not a break, set the label BACK to the push as we are simulating a loop

                            else throw new Exception("This MUST be a break");
                        }
                    }
                    break;
                case NewOpcode.pop:
                    e = CreateExpression(GMCode.Pop, types);
                    e.Operand = BuildUnresolvedVar(r.ReadInt32()); 
                    break;
                //      push = 192, // generic? -1
                //  pushl = 193, // local? -7
                //  pushg = 194, // global? -5 // id is the last bit?
                //   pushb = 195, // built in? hummmm
                // so it dosn't matter, the id is still put in there the same?
                //      case NewOpcode.pushv: // I don't think you exisit little buddy
                //         e = CreateExpression(GMCode.Push, types, operand);
                //         break;
                case NewOpcode.pushi:
                    Debug.Assert(types[0] == GM_Type.Short);
                    e = CreatePushExpression(GMCode.Push, types);
                    break;// push int? ah like a pushe

                case NewOpcode.push:
                    e = CreatePushExpression(GMCode.Push, types);
                    break; // generic push is fine right?

                case NewOpcode.pushl: // local
                    e = CreatePushExpression(GMCode.Push, types);

                    break;// local? -7
                case NewOpcode.pushg: // Global
                    e = CreatePushExpression(GMCode.Push, types);
                    break;// global? -5 // id is the last bit?
                case NewOpcode.pushb: // builtin .. seeing a patern hummmm
                                      // Built in vars?  always -1?
                    e = CreatePushExpression(GMCode.Push, types);
                    break;
                //      case NewOpcode.call2: e = CreateExpression(GMCode.Sal, types, operand); break;
                case NewOpcode.@break: e = CreateExpression(GMCode.Break, types); break;
                default:
                    throw new Exception("Bad opcode");
            }
            return e;
        }
    }


    class StupidNewDisasembler
    {
        File.NewCode code;
        TextWriter writer;
        BinaryReader r;
        public StupidNewDisasembler(File.NewCode code)
        {
            this.code = code;
        }
        public void DissembleToFile(string filename)
        {
            r = new BinaryReader(code.Data);
            writer = new StringWriter();
            writer.WriteLine("FileName: " + Path.GetFileName(filename));
            r.BaseStream.Position = 0;
            while (r.BaseStream.Position < r.BaseStream.Length) DissasembleOneOpcode();

            using (var file = new StreamWriter(filename))
                file.Write(writer.ToString());

            writer = null;
        }
        public static int GetSecondTopByte(int int_0)
        {
            return int_0 >> 16 & 255;
        }

        public static int GetTopByte(int int_0)
        {
            return int_0 >> 24 & 255;
        }

        public static int OpCodeWithOffsetBranch(int int_0, int int_1)
        {
            int int0 = int_0 << 24 | int_1 >> 2 & 8388607;
            return int0;
        }

        public static int ThreebytesToTop(int int_0, int int_1, int int_2)
        {
            int int0 = int_0 << 24 | int_1 << 16 | int_2 << 8;
            return int0;
        }

        public static int TwoBytesToTop(int int_0, int int_1)
        {
            return int_0 << 24 | int_1 << 16;
        }
        private static int smethod_1(int int_5)
        {
            int num = 0;
            GM_Type int5 = (GM_Type)(int_5 & 15);
            switch (int5)
            {
                case GM_Type.Double:
                    {
                        num = 8;
                        break;
                    }
                case GM_Type.Float:
                case GM_Type.Int:
                case GM_Type.Bool:
                case GM_Type.Var:
                case GM_Type.String:
                    {
                        num = 4;
                        break;
                    }
                case GM_Type.Long:
                    {
                        num = 8;
                        break;
                    }
                default:
                    {
                        if (int5 == GM_Type.Short)
                        {
                            break;
                        }
                        break;
                    }
            }
            return num;
        }


        int DissasembleOneOpcode()
        {
            int size = 1;
            int pos = (int)r.BaseStream.Position;
            int opcode = r.ReadInt32();
            int topByte = GetTopByte(opcode);
            int secondTopByte = GetSecondTopByte(opcode);
            int length = 11;
            writer.Write("{0:x8} : ", pos);
            writer.Write("{0:x8}", opcode);
            byte[] operand = null;
            if ((topByte & 64) != 0)
            {
                int extra = smethod_1(secondTopByte);
                operand = r.ReadBytes(extra);
                size += extra;
                foreach (var b in operand)
                {
                    writer.Write("{0:x2}", b);
                    length += 2;
                }
            }
            while (length < 36)
            {
                writer.Write(" ");
                length++;
            }
            string str = ((NewOpcode)topByte).ToString();
            writer.Write(str);
            length = length + str.Length;
            if ((topByte & 160) == 128)
            {
                writer.Write(((GM_Type)(secondTopByte & 15)).GMTypeToPostfix());
                length = length + 2;
            }
            else if ((topByte & 160) == 0)
            {
                writer.Write(((GM_Type)(secondTopByte & 15)).GMTypeToPostfix());
                writer.Write(((GM_Type)((secondTopByte >> 4) & 15)).GMTypeToPostfix());
                length = length + 4;
            }
            while (length < 46)
            {
                writer.Write(" ");
                length++;
            }
            if ((topByte & 64) != 0)
            {
                GM_Type eVMType = (GM_Type)(secondTopByte & 15);
                switch (eVMType)
                {
                    case GM_Type.Double:
                        {
                            writer.Write("{0}", BitConverter.ToDouble(operand, 0));
                            break;
                        }
                    case GM_Type.Float:
                        {
                            writer.Write("{0}", BitConverter.ToSingle(operand, 0));
                            break;
                        }
                    case GM_Type.Int:
                        {
                            int i = BitConverter.ToInt32(operand, 0);
                            if (topByte == 217)
                            {
                                if (i < 0 || i >= File.Strings.Count)
                                {
                                    writer.Write("$unknown_function$");
                                    break;
                                }
                                else
                                {
                                    writer.Write("${0}$", File.Strings[i]);
                                    break;
                                }
                            }
                            else
                            {
                                writer.Write("{0}", i);
                                break;
                            }
                        }
                    case GM_Type.Long:
                        {
                            writer.Write("{0}", BitConverter.ToInt64(operand, 0));
                            break;
                        }
                    case GM_Type.Bool:
                        {
                            writer.Write("{0}", BitConverter.ToInt32(operand, 0) != 0);
                            break;
                        }
                    case GM_Type.Var:
                        {
                            int i = (BitConverter.ToInt32(operand, 0) & 0xFFFFFFF) + 1;
                            if (i < 0 || i >= File.Strings.Count)
                            {
                                writer.Write("$null$");
                                break;
                            }
                            else
                            {
                                string s = File.Strings[i];
                                writer.Write("${0}$", s);
                                break;
                            }
                        }
                    case GM_Type.String:
                        {
                            int i = BitConverter.ToInt32(operand, 0);
                            if (i < 0 || i >= File.Strings.Count)
                            {
                                writer.Write("null");
                                break;
                            }
                            else
                            {
                                writer.Write("\"{0}\"", File.Strings[i]);
                                break;
                            }
                        }
                    case GM_Type.Short:
                        {
                            int i = (opcode << 16) >> 16;
                            writer.Write("{0}", i);
                        }
                        break;
                    default:
                        writer.Write("T{0} ", eVMType.ToString());
                        if (operand != null)
                            foreach (var b in operand) writer.Write("{0:x2}", b);
                        break;
                }
            }
            else if ((topByte & 32) != 0)
            {
                writer.Write("0x{0:x8}", pos + (opcode << 9 >> 7));
            }
            writer.WriteLine();
            return size;
        }
    }

}