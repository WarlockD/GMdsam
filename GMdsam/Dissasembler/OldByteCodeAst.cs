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
        // This pass accepts index or instance values being 
        protected override ILExpression CreateExpression(LinkedList<ILNode> list)
        {
            ILExpression e = null;
            GMCode nOpCode = (GMCode)(CurrentRaw >> 24);
            GM_Type[] types = ReadTypes(CurrentRaw);
            switch (nOpCode) // the bit switch
            {
                case GMCode.Conv:
                    if (list.Last != null)
                    {
                        var prev = list.Last.Value as ILExpression;
                        Debug.Assert(prev.Code != GMCode.Pop);
                        prev.ILRanges.Add(new ILRange(CurrentPC, CurrentPC));
                        prev.InferredType = types[1];
                        prev.Conv = prev.Conv.Concat(types).ToArray();
                    }
                    break;// ignore all Conv for now
                case GMCode.Popz: e = CreateExpression(GMCode.Popz, types); break;
                case GMCode.Mul: e = CreateExpression(GMCode.Mul, types); break;
                case GMCode.Div: e = CreateExpression(GMCode.Div, types); break;
                case GMCode.Rem: e = CreateExpression(GMCode.Rem, types); break;
                case GMCode.Mod: e = CreateExpression(GMCode.Mod, types); break;
                case GMCode.Add: e = CreateExpression(GMCode.Add, types); break;
                case GMCode.Sub: e = CreateExpression(GMCode.Sub, types); break;
                case GMCode.And: e = CreateExpression(GMCode.And, types); break;
                case GMCode.Or: e = CreateExpression(GMCode.Or, types); break;
                case GMCode.Xor: e = CreateExpression(GMCode.Xor, types); break;
                case GMCode.Neg: e = CreateExpression(GMCode.Neg, types); break;
                case GMCode.Not: e = CreateExpression(GMCode.Not, types); break;
                case GMCode.Sal: e = CreateExpression(GMCode.Sal, types); break;
                //    case GMCode.S: e = CreateExpression(GMCode.Sal, types); break;
                //    case GMCode.S: e = CreateExpression(GMCode.Sal, types); break;
                //  case GMCode.shr:     e = CreateExpression(GMCode.Saa, types); break; // hack, handle shift right
                case GMCode.Slt: e = CreateExpression(GMCode.Slt, types); break;
                case GMCode.Sle: e = CreateExpression(GMCode.Sle, types); break;
                case GMCode.Seq: e = CreateExpression(GMCode.Seq, types); break;
                case GMCode.Sne: e = CreateExpression(GMCode.Sne, types); break;
                case GMCode.Sge: e = CreateExpression(GMCode.Sge, types); break;
                case GMCode.Sgt: e = CreateExpression(GMCode.Sgt, types); break;


                case GMCode.Dup:
                    e = CreateExpression(GMCode.Dup, types);
                    e.Operand = (int)(CurrentRaw & 0xFFFF); // dup type
                    break;
                case GMCode.Call: e = CreateExpression(GMCode.Call, types); e.Operand = File.Strings[r.ReadInt32()]; break;
                case GMCode.Ret: e = CreateExpression(GMCode.Ret, types); break;
                case GMCode.Exit: e = CreateExpression(GMCode.Exit, types); break;
                case GMCode.B: e = CreateLabeledExpression(GMCode.B); break;
                case GMCode.Bt: e = CreateLabeledExpression(GMCode.Bt); break;
                case GMCode.Bf: e = CreateLabeledExpression(GMCode.Bf); break;

                // We have to fix these to a lopp to emulate a while latter
                case GMCode.Pushenv:
                    {
                        //  Debug.WriteLine("Popenv: Address: {0}, Extra: {1} {1:X8}  Calc: {2}",i.Address, raw, GMCodeUtil.getBranchOffset(raw));
                        int sextra = CurrentPC + GMCodeUtil.getBranchOffset(CurrentRaw);
                        e = CreateExpression(GMCode.Pushenv, types, GetLabel(sextra + 1)); // we are one instruction after the pop
                        pushEnviroment.Add(sextra, GetLabel(CurrentPC)); // record the pop position
                    }
                    break;
                case GMCode.Popenv:
                    {
                        // We convert this to a Branch so the loop detecter will find it
                        e = CreateExpression(GMCode.B, types);
                        // e = CreateExpression(GMCode.Popenv, types);
                        if (CurrentRaw == 0xBB70000)// its a break, ugh
                        {
                            var last = list.Last;
                            while (last != null)
                            {
                                ILExpression pushe = last.Value as ILExpression;
                                if (pushe.Code == GMCode.Pushenv)
                                {
                                    e.Operand = GetLabel(pushe.ILRanges.First().From);
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
                case GMCode.Pop:
                    e = CreateExpression(GMCode.Pop, types); e.Operand = BuildVar(r.ReadInt32());
                    // e = CreateExpression(GMCode.Pop, types, ReadOperand(CurrentRaw));
                    break;
                case GMCode.Push:
                    e = CreatePushExpression(GMCode.Push, types);
                    break;
                case GMCode.Break: e = CreateExpression(GMCode.Break, types); break;
                default:
                    throw new Exception("Bad opcode");
            }
            return e;

        }
    }
}