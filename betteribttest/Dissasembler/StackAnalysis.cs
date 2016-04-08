using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using betteribttest.FlowAnalysis;
using betteribttest.GMAst;

namespace betteribttest.Dissasembler
{

    public static class ILAstBuilderExtensions
    {
        public static void DebugPrintILAst(this IEnumerable<ILNode> nodes, string filename)
        {
            int labelMax = 0;
            foreach(var n in nodes.OfType<ILLabel>()) if (n.ToString().Length > labelMax) labelMax = n.ToString().Length;
            using (StreamWriter sw = new StreamWriter(filename))
            {
                PlainTextWriter ptw = new PlainTextWriter(sw);
                ptw.Header = new string(' ', labelMax+2); // fill up header
                bool inLabel = false;
                foreach (var i in nodes)
                {
                    if (i is ILLabel) {
                        if (inLabel) ptw.WriteLine();
                        ptw.Header = i.ToString();
                        inLabel = true;
                    } else
                    {
                        i.WriteTo(ptw);
                        ptw.WriteLine();
                        inLabel = false;
                        ptw.Header = null;
                    }
                }
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
                case GMCode.B:
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
                    return 2;
                default:
                    throw new Exception("Unkonwn opcode");
            }
            return 0;
        }
        public static int GetPushDelta(this GMCode code)
        {
            switch (code)
            {
                case GMCode.Popenv:
                case GMCode.Exit:
                case GMCode.Conv:
                    break; // we ignore conv
                case GMCode.Call:
                case GMCode.Push:
                    return 1;
                case GMCode.Pop:
                case GMCode.Popz:
                case GMCode.B:
                case GMCode.Bt:
                case GMCode.Bf:
                case GMCode.Ret:
                case GMCode.Pushenv:
                    break;
                case GMCode.Dup:
                    throw new Exception("Need more info for dup");
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
                    return 1;
                    break;
                default:
                    throw new Exception("Unkonwn opcode");

            }
            return 0;
        }
        public static int? GetPopDelta(this Instruction i)
        {
            int count = 0;
            switch (i.Code)
            {
              
                case GMCode.Call:
                    count = i.Extra; // number of args
                    break;
                case GMCode.Push:
                    if (i.Types[0] == GM_Type.Var)
                    {
                        if (i.Extra == 0) count++; // the instance is on the stack
                        if ((int)i.Operand >= 0) count++; // it is an array so need the index
                    }
                    break;
                case GMCode.Pop:
                    count = 1;
                    if (i.Extra == 0) count++; // the instance is on the stack
                    if ((int)i.Operand >= 0) count++; // it is an array so need the index
                    break;
                case GMCode.Dup:
                    if (i.Extra == 0) count = 1;
                    else count = 2; // we need to figure this out
                    break;
                default:
                    count = i.Code.GetPopDelta();
                    break;
            }
            return count;
        }
        public static int GetPushDelta(this Instruction i)
        {
            switch (i.Code)
            {
                case GMCode.Dup:
                    if (i.Extra == 0) return 1;
                    else return 2; // we need to figure this out
                default:
                    return i.Code.GetPushDelta();

            }
        }
        public static List<T> CutRange<T>(this List<T> list, int start, int count)
        {
            List<T> ret = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                ret.Add(list[start + i]);
            }
            list.RemoveRange(start, count);
            return ret;
        }

        public static T[] Union<T>(this T[] a, T b)
        {
            if (a.Length == 0)
                return new[] { b };
            if (Array.IndexOf(a, b) >= 0)
                return a;
            var res = new T[a.Length + 1];
            Array.Copy(a, 0, res, 0, a.Length);
            res[res.Length - 1] = b;
            return res;
        }

        public static T[] Union<T>(this T[] a, T[] b)
        {
            if (a == b)
                return a;
            if (a.Length == 0)
                return b;
            if (b.Length == 0)
                return a;
            if (a.Length == 1)
            {
                if (b.Length == 1)
                    return a[0].Equals(b[0]) ? a : new[] { a[0], b[0] };
                return b.Union(a[0]);
            }
            if (b.Length == 1)
                return a.Union(b[0]);
            return Enumerable.Union(a, b).ToArray();
        }
    }
    public class ILAstBuilder
    {
        /// <summary> Immutable </summary>
		
     
        SortedList<int, Instruction> _method;
     
        Dictionary<string, ILVariable> variables = new Dictionary<string, ILVariable>();
        ILVariable AddSimpleVariable(string name, int instance)
        {
            ILVariable v;
            if (!variables.TryGetValue(name, out v)) {
                v = new ILVariable() { Name = name, Instance = new ILExpression(GMCode.Push, new ILValue(instance)), InstanceName = GMCodeUtil.lookupInstance(instance, InstanceList) };
                variables.Add(v.Name, v); // Names are generaly unique in gm so this should work
            } else
            {
                // make sure the one we do have equals the one we are putting in
                
            }
            return v;
        }
        ILVariable AddComplexVariable(string name, ILExpression instance=null, ILExpression index=null)
        {
            ILVariable v;
            if (!variables.TryGetValue(name, out v))
            {
                v = new ILVariable() { Name = name, Instance = instance, Index = index  };
                if(instance.Code == GMCode.Push)
                {
                    int test;
                    if((instance.Operand as ILValue).TryParse(out test)) v.InstanceName = GMCodeUtil.lookupInstance(test, InstanceList);
                }
                variables.Add(v.Name, v); // Names are generaly unique in gm so this should work
            }
            return v;
        }
        List<string> InstanceList;
        List<string> StringList;
        bool optimize;
 
        ILValue OperandToIValue(object obj, GM_Type type)
        { // throws things if the cast is bad
            switch(type)
            {
                case GM_Type.Bool: return new ILValue((bool)obj);
                case GM_Type.Double: return new ILValue((double)obj);
                case GM_Type.Float: return new ILValue((float)obj);
                case GM_Type.Long: return new ILValue((long)obj);
                case GM_Type.Int: return new ILValue((int)obj);
                case GM_Type.String: return new ILValue((string)obj);
                case GM_Type.Short: return new ILValue((short)((int)obj));
                default:
                    throw new Exception("Cannot convert simple type");
            }
        }
        ILExpression BuildPreVar(GMCode code, int operand, int extra)
        {
            // check if its simple
            if (extra != 0) // its not on the stack, so its not an array and we have the instance
            {
                return  new ILExpression(code, AddSimpleVariable(StringList[operand & 0x1FFFFF], extra));
            }
            else // its ON the stack so all we know is if its an array or if is
            {
                return new ILExpression(code, new ILUnkonwnVariable() { Name = StringList[operand & 0x1FFFFF], Operand = operand, isArray = operand >= 0 });
            }
        }
        bool FoundConstant(List<ILNode> nodes, ref int i, out ILExpression value)
        {
            for(; i>=0;i--)
            {
                ILExpression test = nodes[i] as ILExpression;
                    if (test != null)
                    {
                        if (test.Operand is ILValue || test.Operand is ILVariable)
                        {
                            value = test;
                            return true;
                        }
                    }
                    else break; // Stop as no constant was there
            }
            value = default(ILExpression);
            return false;
        }
        // This tries to do a VERY simple resolve of a var.
        // for instance, if its an array, and the index is a simple constant, remove it from nodes and asemble a proper ILVarable
        void TrySimpleResolveVarPush(ILExpression v, List<ILNode> nodes) {
            if (v.Operand is ILValue) return; // its a constant
            ILUnkonwnVariable unkonwn = v.Operand as ILUnkonwnVariable;
            if (unkonwn == null) return; // already resolved
            ILExpression instance = null;
            ILExpression index = null;
            if (unkonwn.isArray) // if its an array, we need to check 
            {
                ILExpression[] constants;
                if (!nodes.MatchLastConstants(2, out constants)) return;
                index = constants[0];
                instance = constants[1];
            } else
            {
                if (!nodes.MatchLastConstant(out instance)) return;
            }
            if (index != null)
            {
                nodes.Remove(index);
                if (index.Code == GMCode.Push) index.Code = GMCode.Var;
            }
            nodes.Remove(instance);
            if (instance.Code == GMCode.Push) instance.Code = GMCode.Var;
            ILVariable newVar = new ILVariable() { Name = unkonwn.Name, Instance = instance, Index = index };
            int instanceInt;
            if ((instance.Operand as ILValue).TryParse(out instanceInt)) newVar.InstanceName = GMCodeUtil.lookupInstance(instanceInt, InstanceList);
            v.Operand = newVar;
        }
        // Try to resolve expresions and turn them into constant expressions
        void TryResolveSimpleExpresions(ref ILExpression v, List<ILNode> nodes)
        {
            int popCount = v.Code.GetPopDelta();
            ILExpression[] args;
            if (nodes.MatchLastConstants(popCount, out args)){
                for(int i=0;i< args.Length; i++)
                {
                    ILExpression a = args[i];
                    nodes.Remove(a); // remove them from nodes;
                    ILValue value = a.Operand as ILValue;
                    if (value != null && value.Type == GM_Type.ConstantExpression)
                    { // We want to unpack the expression
                        args[i] = value.Value as ILExpression;
                    }
                    if (args[i].Code == GMCode.Push) args[i].Code = GMCode.Var; // change pushes to vars in expressions
                }
                v.Arguments = args.ToList();
                v = new ILExpression(GMCode.Push, new ILValue(v)); // make it a constant expression
            }
        }
        ILExpression TryResolveCall(string funcName, int length, List<ILNode> nodes)
        {
            ILExpression call = new ILExpression(GMCode.Call, new ILValue(funcName));
            ILExpression[] args;
            if (nodes.MatchLastConstants(length, out args)){
                foreach (var a in args)
                {
                    nodes.Remove(a); // remove them from nodes;
                    if (a.Code == GMCode.Push) a.Code = GMCode.Var; // convert it to a var as its not being pushed anymore
                }
                call.Arguments = args.ToList();
                return call;
            } else
            {
                while (length-- > 0) call.Arguments.Add(new ILExpression(GMCode.Pop, null));
                return call;// fail, couldn't find constant arguments;
            }
        }
            List<ILNode> BuildPreAst()
        { // Just convert instructions to ast streight
            List<ILNode> nodes = new List<ILNode>();
            Dictionary<int, ILLabel> labels = new Dictionary<int, ILLabel>();
            Func<Label, ILLabel> ConvertLabel = (Label l) =>
             {
                 ILLabel lookup;
                 if (labels.TryGetValue(l.Address, out lookup)) return lookup;
                 lookup = new ILLabel() { Name = l.ToString(), UserData = l };
                 labels.Add(l.Address, lookup);
                 return lookup;
             };
            foreach (var i in _method.Values)
            {
                GMCode code = i.Code;
                object operand = i.Operand;
                int extra = i.Extra;
                 if(i.Label != null) nodes.Add(ConvertLabel(i.Label));
                ILExpression expr=null;
                switch (code)
                {
                    case GMCode.Conv:
                        continue; // ignore all Conv for now
                    case GMCode.Call:
                        expr = TryResolveCall(operand as string, extra, nodes);
                        break;
                    case GMCode.Pop: // var define, so lets define it
                        expr = BuildPreVar(GMCode.Pop, (int)operand, extra);
                        TrySimpleResolveVarPush(expr, nodes);
                        {
                            ILExpression constantAssign;
                            if (nodes.MatchLastConstant(out constantAssign))
                            { // constant assginment like self.i = 0;
                                nodes.Remove(constantAssign);
                                expr.Arguments.Add(constantAssign);
                                if(constantAssign.Code == GMCode.Push) constantAssign.Code = GMCode.Var; // change it to a var as its not being pushed anymore
                            } 
                        }
                        break;
                    case GMCode.Push:
                        if (i.Types[0] != GM_Type.Var) expr = new ILExpression(GMCode.Push, OperandToIValue(operand, i.Types[0]));// simple constant 
                        else expr = BuildPreVar(GMCode.Push, (int)operand, extra);
                        TrySimpleResolveVarPush(expr, nodes);
                        break;
                    case GMCode.Pushenv: // the asembler converted the positions to labels at the end of the push/pop enviroments
                            ILExpression constant;
                            if(nodes.MatchLastConstant(out constant))
                                expr = new ILExpression(GMCode.Pushenv, new ILValue(constant));
                             else
                                expr = new ILExpression(GMCode.Pushenv, new ILExpression(GMCode.Pop, null));
                            break;
                    case GMCode.Popenv:
                        expr = new ILExpression(GMCode.Popenv, ConvertLabel(i.Operand as Label));
                        break;
                    case GMCode.B:
                    case GMCode.Bt:
                    case GMCode.Bf: // we could try converting all Bf to Bt here, but Bt's seem to only be used in special shorts or switch/case, so save that info here
                        expr = new ILExpression(code, ConvertLabel(operand as Label));
                        break;
                    case GMCode.Dup:
                        expr = new ILExpression(code, new ILValue(extra)); // save the extra value for dups incase its dup eveything or just one
                        break;
                    case GMCode.Exit:
                    case GMCode.Popz:
                        expr = new ILExpression(code, null);
                        break;
                    default:
                        expr = new ILExpression(code, null);
                        if(code.GetPopDelta() >0) TryResolveSimpleExpresions(ref expr, nodes);
                        break;
                }
                nodes.Add(expr);
            }
            return nodes;
        }

        public List<ILNode> Build(SortedList<int, Instruction> method, bool optimize, List<string> StringList, List<string> InstanceList = null) //DecompilerContext context)
        {
            if (method.Count == 0) return new List<ILNode>();
            //variables = new Dictionary<string, VariableDefinition>();
            this.InstanceList = InstanceList;
            this.StringList = StringList;
            _method = method;
            this.optimize = optimize;
            List<ILNode> ast = BuildPreAst();
            //List<ByteCode> body = StackAnalysis(method);
            ast.DebugPrintILAst("bytecode_test.txt");
     
            // We don't have a fancy


            return ast;

        }
    }
}