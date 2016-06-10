using GameMaker.Ast;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GameMaker.File;

namespace GameMaker.Dissasembler
{

    public abstract class BuildAst
    {
        protected ErrorContext Error { get; private set; }
        protected BinaryReader r { get; private set; }
        Dictionary<int, ILLabel> labels = null;
        protected int CurrentPC { get; private set; }
        protected uint CurrentRaw { get; private set; }
        protected ILLabel GetLabel(int position)
        {
            ILLabel l;
            if (!labels.TryGetValue(position, out l)) labels[position] = l = new ILLabel(position);
            return l;
        }
        protected ILExpression CreateLabeledExpression(GMCode code)
        {
            int absolute = GMCodeUtil.getBranchOffset(CurrentRaw) + CurrentPC;
            ILExpression e = new ILExpression(code, GetLabel(absolute));
            e.Conv = null;
            e.Extra = (int)(CurrentRaw & 0xFFFF);
            e.ILRanges.Add(new ILRange(CurrentPC, CurrentPC));
            return e;
        }
        public List<ILNode> Build(File.Code code, ErrorContext error=null)
        {
            if (code == null) throw new ArgumentNullException("code");
            Stream stream = code.Data;
            if (stream == null) throw new ArgumentNullException("code.Data");
            if (!stream.CanRead) throw new ArgumentException("Must be readable", "code_stream");
            if (!stream.CanSeek) throw new ArgumentException("Must be seekable", "code_stream");
            if (stream.Length == 0) return new List<ILNode>(); // empty stream
            Error = error ?? new ErrorContext(code.Name);
            labels = new Dictionary<int, ILLabel>(); // cause they are all the same
            stream.Position = 0;
            r = new BinaryReader(stream);
            CurrentPC = 0;
            LinkedList<ILNode> list = new LinkedList<ILNode>();
            Dictionary<int, LinkedListNode<ILNode>> pcToExpressoin = new Dictionary<int, LinkedListNode<ILNode>>();
            ILExpression e = null;
            Start(list);
            while (stream.Position < stream.Length)
            {
                CurrentPC = (int)stream.Position / 4;
                CurrentRaw = r.ReadUInt32();
                e = CreateExpression(list);
                if (e != null)
                {
                    list.AddLast(e);
                    pcToExpressoin.Add(CurrentPC, list.Last); // in case it points to a conv
                }
            }
            bool needExit = e.Code != GMCode.Ret || e.Code != GMCode.Exit;
        
            int lastpc = (int)r.BaseStream.Position / 4;
            ILLabel endLabel = GetLabel(lastpc);
            foreach (var l in labels)
            {
                LinkedListNode<ILNode> n;
                if (pcToExpressoin.TryGetValue(l.Key, out n))
                {
                    list.AddBefore(n, l.Value);
                } else if(l.Key >= lastpc) list.AddLast(l.Value);
            }
            if (needExit) list.AddLast(new ILExpression(GMCode.Exit, null)); // make sure we got an exit as the last code
            Finish(list);
            r = null; // for GC
            return list.ToList();
        }
        protected static GM_Type[] ReadTypes(uint rawCode)
        {
            uint topByte = (rawCode >> 24) & 255;
            uint secondTopByte = (rawCode >> 16) & 255;
            GM_Type[] types = null;
            if ((topByte & 160) == 128) types = new GM_Type[] { (GM_Type)(secondTopByte & 15) };
            else if ((topByte & 160) == 0) types = new GM_Type[] { (GM_Type)(secondTopByte & 15), (GM_Type)((secondTopByte >> 4) & 15) };
            return types;
        }
        protected ILVariable BuildVar(int operand)
        {
            int extra = (short)(CurrentRaw & 0xFFFF);
            // int loadtype = operand >> 24;
            if (extra != 0) // never see simple vars that are arrays
                return new ILVariable(Context.LookupString(operand & 0x1FFFFF), extra);// simple var
            else
                return new ILVariable(Context.LookupString(operand & 0x1FFFFF), extra,  operand >= 0);// standard for eveyone
        }
        protected ILValue ReadConstant(GM_Type t)
        {
            switch (t)
            {
                case GM_Type.Double: return new ILValue(r.ReadDouble());
                case GM_Type.Float: return new ILValue(r.ReadSingle());
                case GM_Type.Int: return new ILValue(r.ReadInt32()); // function?
                case GM_Type.Long: return new ILValue(r.ReadInt64()); // function?
                case GM_Type.Bool: return new ILValue(r.ReadInt32() != 0);
                case GM_Type.String:
                    {
                        int i = r.ReadInt32();
                        if (i < 0 || i >= File.Strings.Count) return new ILValue("$BADSTRINGVALUE$");
                        else return new ILValue(File.Strings[i]);
                    }
                default:
                    throw new Exception("Should not get here");
            }
        }
        protected ILExpression CreateExpression(GMCode code,  GM_Type[] types)
        {
            ILExpression e = new ILExpression(code, null);
            e.Conv = types;
            e.Extra = (int)(CurrentRaw & 0xFFFF);
            e.AddILRange(CurrentPC);
            return e;
        }
        protected  ILExpression CreateExpression(GMCode code,  GM_Type[] types, ILLabel operand)
        {
            Debug.Assert(operand != null);
            ILExpression e = new ILExpression(code, operand);
            e.Conv = types;
            e.Extra = (int)(CurrentRaw & 0xFFFF);
            e.AddILRange(CurrentPC);
            return e;
        }
        protected ILExpression CreatePushExpression(GMCode code, GM_Type[] types)
        {
            ILExpression e = new ILExpression(code, null);
            e.Conv = types;
            e.Extra = (int)(CurrentRaw & 0xFFFF);
            e.AddILRange(CurrentPC);
            switch (types[0])
            {
                case GM_Type.Var:
                    e.Arguments.Add(new ILExpression(GMCode.Var, BuildVar(r.ReadInt32())));
                    break;
                case GM_Type.Short:
                    e.Arguments.Add(new ILExpression(GMCode.Constant, new ILValue((short)(CurrentRaw & 0xFFFF))));
                    break;
                default:
                    e.Arguments.Add(new ILExpression(GMCode.Constant, ReadConstant(types[0])));
                    break;
            }
            return e;
        }
        protected abstract ILExpression CreateExpression(LinkedList<ILNode> list);

        /// <summary>
        /// Before reading the first opcode
        /// </summary>
        /// <param name="list"></param>
        protected virtual void Start(LinkedList<ILNode> list)
        {

        }
        /// <summary>
        /// After label resolving and right before returning
        /// </summary>
        /// <param name="list"></param>
        protected virtual void Finish(LinkedList<ILNode> list)
        {

        }
    }
}
