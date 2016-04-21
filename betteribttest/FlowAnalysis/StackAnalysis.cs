using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace betteribttest.Dissasembler
{

    public static class ILAstBuilderExtensions
    {
      
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
        bool optimize;

        ILValue OperandToIValue(object obj, GM_Type type)
        { // throws things if the cast is bad
            switch (type)
            {
                case GM_Type.Bool: 
                case GM_Type.Double:
                case GM_Type.Float: 
                case GM_Type.Long: 
                case GM_Type.Int: 
                case GM_Type.String: 
                case GM_Type.Short:
                    return new ILValue(obj, type);
                default:
                    throw new Exception("Cannot convert simple type");
            }
        }
        bool dupPatternhack = false;
        /// <summary>
        /// This just removes pushes and gets the expresion from it
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        bool ExpressionIsSimple(ILExpression expr)
        {
            return !dupPatternhack && expr.Code == GMCode.Call || expr.Code == GMCode.Constant || expr.Code == GMCode.Var ||
                                (expr.Code.isExpression() && expr.Arguments.Count > 0);
        }
        bool NodeIsSimple(ILNode node, out ILExpression expr)
        {
            expr = node as ILExpression;
            if (expr != null)
            {
                if (expr.Code == GMCode.Push) expr = expr.Arguments[0];
                return ExpressionIsSimple(expr);
            }
            return false;
        }
   
        // hack for right now
        // Just wierd, I had this issue before, where the arguments become swapped when you use a dup 0
        // is the dup 0 mean just to dup the var stack and not the expression stack?  I am suspecting yes
        // this is JUST a hack till I find a better way to fix it
        ILExpression DupVarFixHack(List<ILNode> nodes)
        {
            ILExpression e = nodes.Last() as ILExpression;
            if(e != null && e.Code == GMCode.Dup && (int)e.Operand == 0) // simple dup
            {
                e = nodes.LastOrDefault(x => x is ILExpression && (x as ILExpression).Code == GMCode.Push) as ILExpression;
            }
            return e;
        }
        ILVariable BuildVar(int operand, int extra)
        {
            // int loadtype = operand >> 24;
            ILVariable v = new ILVariable() { Name = context.LookupString(operand & 0x1FFFFF), Instance = new ILValue(extra)  };// standard for eveyone

            if (extra != 0) {
                v.isResolved = true; // very simple var
                v.InstanceName = context.InstanceToString(extra);
            } else {
                v.isResolved = false;
                v.isArray = (extra == 0 && operand >= 0);
                v.InstanceName = "stack"; // filler
            }
            return v;
        }
        // This tries to do a VERY simple resolve of a var.
        // for instance, if its an array, and the index is a simple constant, remove it from nodes and asemble a proper ILVarable

        ILExpression TryResolveSimpleExpresions(int popCount, ILExpression v, List<ILNode> nodes)
        {
            int nodeIndex = nodes.Count - popCount;
            while (nodeIndex < nodes.Count)
            {
                ILExpression arg = null;
                if (NodeIsSimple(nodes.ElementAt(nodeIndex++), out arg))
                    v.Arguments.Add(arg);
                else break;
            }
            if (v.Arguments.Count == popCount)
                nodes.RemoveRange(nodes.Count - popCount, popCount);
            else v.Arguments.Clear();
            return new ILExpression(GMCode.Push, null, v);
        }
        ILExpression TryResolveCall(string funcName, int length, List<ILNode> nodes)
        {
            ILExpression call = new ILExpression(GMCode.Call, funcName);
            return TryResolveSimpleExpresions(length, call, nodes);
        }
        void HackDebug(Instruction inst , IList<Instruction> list)
        {
            int index = list.IndexOf(inst);
            for(int i= index -5; i < index +5; i++)
            {
                string line = list[i].ToString();
                if (i == index) line+="**";
                Debug.WriteLine(line);
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
          //      Debug.Assert(nodes.Count != 236);
                GMCode code = i.Code;
                object operand = i.Operand;
                int extra = i.Extra;
                if (i.Label != null) nodes.Add(ConvertLabel(i.Label));
                ILExpression expr = null;
                switch (code)
                {
                    case GMCode.Conv:
                        continue; // ignore all Conv for now
                    case GMCode.Call:
                        // Since we have to resolve calls seperately and need 
                        expr = new ILExpression(GMCode.Call, operand as string);
                        expr.Extra = extra; // need to know how many arguments we have
                        break;
                    case GMCode.Popz:
                        expr = new ILExpression(code, null);
                        break;
                    case GMCode.Pop: // var define, so lets define it
                        expr = new ILExpression(GMCode.Pop, BuildVar((int)operand, extra));
                        expr.Extra = extra;
                        break;
                    case GMCode.Push:
                        if (i.Types[0] != GM_Type.Var)
                            expr = new ILExpression(GMCode.Push, OperandToIValue(operand, i.Types[0]));// simple constant 
                        else
                            expr = new ILExpression(GMCode.Push, BuildVar((int)operand, extra));  // try to figure out the var);
                        expr.Extra = extra;
                        break;
                    case GMCode.Pushenv: // the asembler converted the positions to labels at the end of the push/pop enviroments
                        expr = new ILExpression(GMCode.Pushenv, ConvertLabel(i.Operand as Label));
                        break;
                    case GMCode.Popenv:
                        expr = new ILExpression(GMCode.B, ConvertLabel(i.Operand as Label));
                        break;
                    case GMCode.B:
                        expr = new ILExpression(GMCode.B, ConvertLabel(i.Operand as Label));
                        break;
                    case GMCode.Ret:
                        expr = new ILExpression(code, null, new ILExpression(GMCode.Pop, null));
                        break;
                    case GMCode.Bt:
                    case GMCode.Bf: 
                         expr = new ILExpression(code, ConvertLabel(i.Operand as Label), new ILExpression(GMCode.Pop, null));
                        break;
                    case GMCode.Dup:
                        expr = new ILExpression(code, extra); // save the extra value for dups incase its dup eveything or just one
                                                              //      HackDebug(i, _method.Values);
                        break;
                    case GMCode.Exit:
                        expr = new ILExpression(code, null);
                        break;
                    default:
                        expr = new ILExpression(code, null);
                        break;
                }
                expr.ILRanges.Add(new ILRange(i.Address, i.Address));
                nodes.Add(expr);
            }
            return nodes;
        }
  
    
        void FlattenBasicBlocks(ILNode node)
        {
            ILBlock block = node as ILBlock;
            if (block != null)
            {
                List<ILNode> flatBody = new List<ILNode>();
                foreach (ILNode child in block.GetChildren())
                {
                    FlattenBasicBlocks(child);
                    ILBasicBlock childAsBB = child as ILBasicBlock;
                    if (childAsBB != null)
                    {
                        if (!(childAsBB.Body.FirstOrDefault() is ILLabel))
                            throw new Exception("Basic block has to start with a label. \n" + childAsBB.ToString());
                        if (childAsBB.Body.LastOrDefault() is ILExpression && !childAsBB.Body.LastOrDefault().IsUnconditionalControlFlow())
                            throw new Exception("Basci block has to end with unconditional control flow. \n" + childAsBB.ToString());
                        flatBody.AddRange(childAsBB.GetChildren());
                    }
                    else {
                        flatBody.Add(child);
                    }
                }
                block.EntryGoto = null;
                block.Body = flatBody;
            }
            else if (node is ILExpression)
            {
                // Optimization - no need to check expressions
            }
            else if (node != null)
            {
                // Recursively find all ILBlocks
                foreach (ILNode child in node.GetChildren())
                {
                    FlattenBasicBlocks(child);
                }
            }
        }
      

     
        public void RunOptimizations(ILBlock method, params Func<List<ILNode>, ILBasicBlock, int, bool>[] optimizations)
        {
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                bool modified;
                do
                {
                    modified = false;
                    foreach (var fun in optimizations) modified |= block.RunOptimization(fun);
                } while (modified);
            }
        }
        void DebugBasicBlocks(ILBlock method)
        {
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                bool modified;
                do
                {
                    modified = false;
                //    modified |= block.RunOptimization(new SimpleControlFlow(method, context).SwitchDetection);
                //    modified |= block.RunOptimization(Optimize.ProcessExpressions);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).PushEnviromentFix);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).JoinBasicBlocks);
                } while (modified);
            }
           
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                bool modified;
                do
                {
                    modified = false;
                 //   modified |= block.RunOptimization(new SimpleControlFlow(method, context).SwitchDetection);
                 //   modified |= block.RunOptimization(Optimize.ProcessExpressions);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).PushEnviromentFix);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).SimplifyShortCircuit);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).SimplifyTernaryOperator);


                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).JoinBasicBlocks);
                    modified |= block.RunOptimization(Optimize.SimplifyLogicNot);
                } while (modified);
            }
            
        }
        GMContext context;
        public ILBlock Build(SortedList<int, Instruction> code, bool optimize, GMContext context)  //  List<string> StringList, List<string> InstanceList = null) //DecompilerContext context)
        {
            if (code.Count == 0) return new ILBlock();
                this.context = context;


            _method = code;
            this.optimize = optimize;
            List<ILNode> ast = BuildPreAst();

            ILBlock method = new ILBlock();
            method.Body = ast;
            if (context.Debug) method.DebugSave("raw_body.txt");
            betteribttest.Dissasembler.Optimize.RemoveRedundantCode(method);
            foreach(var block in method.GetSelfAndChildrenRecursive<ILBlock>())
                Optimize.SplitToBasicBlocks(block);
            if(context.Debug) method.DebugSave("basic_blocks.txt");
  
            new BuildFullAst(method, context).ProcessAllExpressions(method);
            if(context.Debug) method.DebugSave("basic_blocks2.txt");
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                bool modified;
                do
                {
                    modified = false;
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).PushEnviromentFix);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).SimplifyShortCircuit);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).SimplifyTernaryOperator);


                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).JoinBasicBlocks);
                    modified |= block.RunOptimization(Optimize.SimplifyLogicNot);
                } while (modified);
            }
            if (context.Debug) method.DebugSave("before_loop.txt");
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions().FindLoops(block);
            }
            if (context.Debug) method.DebugSave("before_conditions.txt");
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions().FindConditions(block);
            }

            FlattenBasicBlocks(method);
            if (context.Debug) method.DebugSave("before_gotos.txt");
            Optimize.RemoveRedundantCode(method);
            new GotoRemoval().RemoveGotos(method);
            Optimize.RemoveRedundantCode(method);

            new GotoRemoval().RemoveGotos(method);

            GotoRemoval.RemoveRedundantCode(method);
            new GotoRemoval().RemoveGotos(method);

            if (context.Debug) method.DebugSave("final.cpp");
            return method;

        }
    }
}