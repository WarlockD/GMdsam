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
    class OldByteCodeAst : BuildAst
    {
        public enum OldCode : byte
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
        Dictionary<int, ILLabel> pushEnviroment = new Dictionary<int, ILLabel>();
        protected override void Start(List<ILNode> list)
        {
            pushEnviroment = new Dictionary<int, ILLabel>();
        }
        /// <summary>
        /// After label resolving and right before returning
        /// </summary>
        /// <param name="list"></param>
        protected override void Finish(List<ILNode> list)
        {
            pushEnviroment = null;
        }
        // This pass accepts index or instance values being 
        protected override ILExpression CreateExpression(List<ILNode> list)
        {
            ILExpression e = null;
            OldCode nOpCode = (OldCode) (CurrentRaw >> 24);
            GM_Type[] types = ReadTypes(CurrentRaw);
            switch (nOpCode) // the bit switch
            {
               
                case OldCode.Conv: e = CreateExpression(GMCode.Conv, types); break;
                case OldCode.Popz: e = CreateExpression(GMCode.Popz, types); break;
                case OldCode.Mul: e = CreateExpression(GMCode.Mul, types); break;
                case OldCode.Div: e = CreateExpression(GMCode.Div, types); break;
                case OldCode.Rem: e = CreateExpression(GMCode.Rem, types); break;
                case OldCode.Mod: e = CreateExpression(GMCode.Mod, types); break;
                case OldCode.Add: e = CreateExpression(GMCode.Add, types); break;
                case OldCode.Sub: e = CreateExpression(GMCode.Sub, types); break;
                case OldCode.And: e = CreateExpression(GMCode.And, types); break;
                case OldCode.Or: e = CreateExpression(GMCode.Or, types); break;
                case OldCode.Xor: e = CreateExpression(GMCode.Xor, types); break;
                case OldCode.Neg: e = CreateExpression(GMCode.Neg, types); break;
                case OldCode.Not: e = CreateExpression(GMCode.Not, types); break;
                case OldCode.Sal: e = CreateExpression(GMCode.Sal, types); break;
                //    case GMCode.S: e = CreateExpression(GMCode.Sal, types); break;
                //    case GMCode.S: e = CreateExpression(GMCode.Sal, types); break;
                //  case GMCode.shr:     e = CreateExpression(GMCode.Saa, types); break; // hack, handle shift right
                case OldCode.Slt: e = CreateExpression(GMCode.Slt, types); break;
                case OldCode.Sle: e = CreateExpression(GMCode.Sle, types); break;
                case OldCode.Seq: e = CreateExpression(GMCode.Seq, types); break;
                case OldCode.Sne: e = CreateExpression(GMCode.Sne, types); break;
                case OldCode.Sge: e = CreateExpression(GMCode.Sge, types); break;
                case OldCode.Sgt: e = CreateExpression(GMCode.Sgt, types); break;


                case OldCode.Dup:
                    e = CreateExpression(GMCode.Dup, types);
                    e.Operand = (int)(CurrentRaw & 0xFFFF); // dup type
                    break;
                case OldCode.Call:
                    e = CreateExpression(GMCode.CallUnresolved, types);
                    e.Operand = ILCall.CreateCall(File.Strings[r.ReadInt32()].String, (int) (CurrentRaw & 0xFFFF));

                     break;
                case OldCode.Ret: e = CreateExpression(GMCode.Ret, types); break;
                case OldCode.Exit: e = CreateExpression(GMCode.Exit, types); break;
                case OldCode.B: e = CreateLabeledExpression(GMCode.B); break;
                case OldCode.Bt: e = CreateLabeledExpression(GMCode.Bt); break;
                case OldCode.Bf: e = CreateLabeledExpression(GMCode.Bf); break;

                // We have to fix these to a lopp to emulate a while latter
                case OldCode.Pushenv:
                    {

                        Debug.WriteLine("Popenv: Address: {0}, Extra: {1} {1:X8}  Calc: {2}", CurrentPC, CurrentRaw, GMCodeUtil.getBranchOffset(CurrentRaw));
                        int sextra = CurrentPC + GMCodeUtil.getBranchOffset(CurrentRaw);
                        e = CreateExpression(GMCode.Pushenv, types, GetLabel(sextra + 1)); // we are one instruction after the pop
                        pushEnviroment.Add(sextra, GetLabel(CurrentPC)); // record the pop position
                    }
                    break;
                case OldCode.Popenv:
                    {
                        // We convert this to a Branch so the loop detecter will find it
                        e = CreateExpression(GMCode.Popenv, types);
                        // e = CreateExpression(GMCode.Popenv, types); 
                        if (CurrentRaw == 0xbcf00000)// its a break, ugh, old break code ugh
                        {
                            foreach (var last in list.Reverse<ILNode>().OfType<ILExpression>())
                            {
                                if (last.Code == GMCode.Pushenv)
                                {
                                    e.Operand = last.Operand;
                                    return e;
                                }
                            }
                            Debug.Assert(false);
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
                case OldCode.Pop:
                    e = CreateExpression(GMCode.Pop, types);
                    e.Operand = BuildUnresolvedVar(r.ReadInt32());

                    break;
                case OldCode.Push:
                    e = CreatePushExpression(GMCode.Push, types);
                    break;
                case OldCode.Break: e = CreateExpression(GMCode.Break, types); break;
                default:
                    throw new Exception("Bad opcode");
            }
            return e;

        }
    }
}