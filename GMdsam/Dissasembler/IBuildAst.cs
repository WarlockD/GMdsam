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
        protected int StartingOffset { get; private set; }
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
        protected bool labelExists(int position)
        {
            ILLabel l;
            return labels.TryGetValue(position, out l);
        }
        protected ILExpression CreateLabeledExpression(GMCode code)
        {
            int absolute = GMCodeUtil.getBranchOffset(CurrentRaw) + CurrentPC;
            ILExpression e = new ILExpression(code, GetLabel(absolute));
            e.Extra = (int)(CurrentRaw & 0xFFFF);
            e.ILRanges.Add(new ILRange(CurrentPC, CurrentPC));
            return e;
        }
        public List<ILNode> Build(File.Code code,  ErrorContext error=null)
        {
            if (code == null) throw new ArgumentNullException("code");
            Stream stream = code.Data;
            if (stream == null) throw new ArgumentNullException("code.Data");
            if (!stream.CanRead) throw new ArgumentException("Must be readable", "code_stream");
            if (!stream.CanSeek) throw new ArgumentException("Must be seekable", "code_stream");
            if (stream.Length == 0) return new List<ILNode>(); // empty stream
            StartingOffset = code.CodePosition;
            Error = error ?? new ErrorContext(code.Name);
            labels = new Dictionary<int, ILLabel>(); // cause they are all the same
            stream.Position = 0;
            r = new BinaryReader(stream);
            CurrentPC = 0;
            List<ILNode> list = new List<ILNode>();
            Dictionary<int, int> pcToExpressoin = new Dictionary<int, int>();
          
            Start(list);
            while (stream.Position < stream.Length)
            {
                CurrentPC = (int)stream.Position / 4;
                CurrentRaw = r.ReadUInt32();
                ILExpression e = CreateExpression(list);
                if (e != null)
                {
                    /*
                    // hack here cause of issues
                    if (e.Code == GMCode.Conv)
                    {
                        var prev = list.Last.Value as ILExpression;
                        Debug.Assert(prev.Code != GMCode.Pop);
                        prev.ILRanges.Add(new ILRange(CurrentPC, CurrentPC));
                        prev.Types = e.Types; // don't add it
                    }
                    else
                    */
                    pcToExpressoin.Add(CurrentPC, list.Count);
                    list.Add(e);
                   
                }
            }
            CurrentPC = (int) stream.Position / 4;
            CurrentRaw = 0;
            if(labelExists(CurrentPC)) // this is in case we do have a jump but we need to exit clean
            {
                pcToExpressoin.Add(CurrentPC, list.Count);
                list.Add(CreateExpression(GMCode.Exit, null)); // make sure we got an exit as the last code
                // we HAVE to have an exit
            } else 
            {
                ILExpression last = list.Last() as ILExpression;
                if(last.Code != GMCode.Ret && last.Code != GMCode.Exit)
                {
                    pcToExpressoin.Add(CurrentPC, list.Count);
                    list.Add(CreateExpression(GMCode.Exit, null)); // make sure we got an exit as the last code
                }
            }
           
            foreach (var l in labels)
            {
                int n;
                if (pcToExpressoin.TryGetValue(l.Key, out n))
                    list[n].UserData = l.Value;
            }
            var rlist = new List<ILNode>();
            for(int i=0; i < list.Count; i++)
            {
                ILExpression e = list[i] as ILExpression;
                if (e != null) {
                    if (e.UserData != null) { rlist.Add(e.UserData as ILLabel); e.UserData = null; }
                    if (e.Code == GMCode.Conv)
                    {
                        ILExpression ne =  list[i+1] as ILExpression;
                        ne.ILRanges.AddRange(e.ILRanges);
                        ne.Types = e.Types;
                        continue; // skip
                    }
                }
                rlist.Add(list[i]);
            }
            Finish(rlist);

            return rlist;
        }
        protected static GM_Type ReadRaw(uint i)
        {
            switch (i & 0xF)
            {
                case 0: return GM_Type.Double;
                case 1: return GM_Type.Float;
                case 2: return GM_Type.Int;
                case 3: return GM_Type.Long;
                case 4: return GM_Type.Bool;
                case 5: return GM_Type.Var;
                case 6: return GM_Type.String;
                case 15: return GM_Type.Short;
                default:
                    throw new Exception("Bad type read");
            }
        }
        protected static GM_Type[] ReadTypes(uint rawCode)
        {
            uint topByte = (rawCode >> 24) & 255;
            uint secondTopByte = (rawCode >> 16) & 255;
            GM_Type[] types = null;
            if ((topByte & 160) == 128) types = new GM_Type[] { ReadRaw(secondTopByte & 15) };
            else if ((topByte & 160) == 0) types = new GM_Type[] { ReadRaw(secondTopByte & 15), ReadRaw((secondTopByte >> 4) & 15) };
            return types;
        }

        protected UnresolvedVar BuildUnresolvedVar(int operand)
        {
            string name = Context.LookupString(operand & 0x1FFFFF);
            int extra = (short) (CurrentRaw);// & 0xFFFF);
            // int loadtype = operand >> 24;
            return new UnresolvedVar() { Name = name, Operand = operand, Extra = extra };
        }

        static protected ILValue ReadConstant(BinaryReader r, GM_Type t)
        {
            ILValue v = null;
            int offset = (int)r.BaseStream.Position;
            switch (t)
            {
                case GM_Type.Double: v= new ILValue(r.ReadDouble()); break;
                case GM_Type.Float: v= new ILValue(r.ReadSingle()); break;
                case GM_Type.Int: v= new ILValue(r.ReadInt32()); break;// function?
                case GM_Type.Long: v= new ILValue(r.ReadInt64()); break;// function?
                case GM_Type.Bool: v= new ILValue(r.ReadInt32() != 0); break;
                case GM_Type.String:
                    {
                        int i = r.ReadInt32();
                        if (i < 0 || i >= File.Strings.Count) v= new ILValue("$BADSTRINGVALUE$");
                        else v= new ILValue(File.Strings[i].String);
                    }
                    break;
                default:
                    throw new Exception("Should not get here");
            }
            v.DataOffset = offset;
            return v;
        }
        protected ILExpression CreateExpression(GMCode code,  GM_Type[] types)
        {
            ILExpression e = new ILExpression(code, null);
            e.Types = types;
            e.Extra = (int)(CurrentRaw & 0xFFFF);
            e.AddILRange(CurrentPC);
            return e;
        }
        protected  ILExpression CreateExpression(GMCode code,  GM_Type[] types, ILLabel operand)
        {
            Debug.Assert(operand != null);
            ILExpression e = new ILExpression(code, operand);
            e.Types = types;
            e.Extra = (int)(CurrentRaw & 0xFFFF);
            e.AddILRange(CurrentPC);
            return e;
        }
        void DebugILValueOffset(ILValue v)
        {
            Debug.Assert(v.DataOffset != null);
            int offset = (int) v.DataOffset;
            BinaryReader r = new BinaryReader(File.DataWinStream);
            r.BaseStream.Position = offset;
          
            if(v.Type != GM_Type.Short)
            {
                ILValue test = ReadConstant(r, v.Type);
                Debug.Assert(test.ToString() == v.ToString());
            } else
            {
                uint raw = r.ReadUInt32();
                short test = (short) (raw & 0xFFFF);
                Debug.Assert(test == v.IntValue);
            }
        }
        protected ILExpression CreatePushExpression(GMCode code, GM_Type[] types)
        {
            ILExpression e = new ILExpression(code, null);
            e.Types = types;
            e.Extra = (int)(CurrentRaw & 0xFFFF);
            Debug.Assert(e.ILRanges.Count == 0);
            e.AddILRange(CurrentPC);
            ILValue v = null;
            switch (types[0])
            {
                case GM_Type.Var:
                        e.Operand = BuildUnresolvedVar(r.ReadInt32());
                    break;
                case GM_Type.Short:
                    {
                        v = new ILValue((short)(CurrentRaw & 0xFFFF));
                        v.DataOffset = (int)(r.BaseStream.Position - 4);
                        e.Arguments.Add(new ILExpression(GMCode.Constant, v));
                    }
                    break;
                default:
                    v = ReadConstant(r, types[0]);
                    e.Arguments.Add(new ILExpression(GMCode.Constant, v));
                    break;
            }
            if (v != null)
            {
                v.DataOffset += StartingOffset ;
                DebugILValueOffset(v);
            }
           
            return e;
        }
        protected abstract ILExpression CreateExpression(List<ILNode> list);

        /// <summary>
        /// Before reading the first opcode
        /// </summary>
        /// <param name="list"></param>
        protected virtual void Start(List<ILNode> list)
        {

        }
        /// <summary>
        /// After label resolving and right before returning
        /// </summary>
        /// <param name="list"></param>
        protected virtual void Finish(List<ILNode> list)
        {

        }
    }
}
