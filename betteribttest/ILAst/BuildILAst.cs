using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GameMaker.Ast
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
            ILVariable v = new ILVariable() { Name = Context.LookupString(operand & 0x1FFFFF), Instance = new ILValue(extra)  };// standard for eveyone

            if (extra != 0) {
                v.isResolved = true; // very simple var
                v.InstanceName = Context.InstanceToString(extra);
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
        
    


        bool CombineCall(List<ILNode> nodes, ILExpression expr, int pos)
        {
            if (expr.Code == GMCode.Call && expr.Operand is string)
            {
                string fun_name = expr.Operand as string;
        
                List <ILExpression> args = GetArguments(nodes,  pos-1, expr.Extra);
                if (args != null)
                {
                    
                    ILCall call = new ILCall() { Name = fun_name, Type = expr.InferredType, Arguments = args };
                   
                    expr.Arguments.Clear();
                    expr.Operand = call; // fix it
                    pos -= expr.Extra;
                    if (nodes.ElementAtOrDefault(pos + 1).Match(GMCode.Popz))
                    {
                        nodes[pos] = expr; //calls itself
                        nodes.RemoveAt(pos + 1);// remove it
                    }
                    else
                    {
                        nodes[pos] = new ILExpression(GMCode.Push, null, expr); // wrap it in a push
                    }
                    return true;
                }
            }
            return false;
        }
        ILExpression CleanPush(ILExpression e)
        {
            if (e.Code == GMCode.Push) return e.Arguments.Single();
            else return e;
        }
        List<ILExpression> GetArguments(List<ILNode> nodes,  int index, int count, bool remove = true)
        {
            List<ILExpression> args = new List<ILExpression>();
            int scount = count;

            for (int i= index; i >= 0 && count > 0; i--, count--)
            {
                ILNode n = nodes[i];
                if (n.FixPushExpression() && n.isNodeResolved())
                    args.Add(n.MatchSingleArgument());
                else break;
            }
            if(remove && count == 0)
            {
                nodes.RemoveRange(index- scount+1, scount);
            }
            return count == 0 ? args : null;
        }
        // Here we convert constants and vars into expression containers, combine simple expresions
        // and create IAssign's and ILCalls if nessasary
        bool CombineExpressions(List<ILNode> nodes, ILExpression expr, int pos)
        {
            if (expr.Code.isExpression() && expr.Arguments.Count == 0)
            {
                int popDelta = expr.Code.GetPopDelta();
                ILExpression left;
                ILExpression right;
                if (popDelta == 1 && nodes.ElementAtOrDefault(pos - 1).Match(GMCode.Push, out left) && left.isNodeResolved())
                {
                    expr.Arguments.Add(left);
                    nodes[pos] = new ILExpression(GMCode.Push, null, expr); // change it to a push
                    nodes.RemoveAt(pos - 1);
                    return true;
                } else if (
                    popDelta == 2 &&
                     (nodes.ElementAtOrDefault(pos - 1).Match(GMCode.Push, out left) && left.isNodeResolved()) &&
                     (nodes.ElementAtOrDefault(pos - 2).Match(GMCode.Push, out right) && right.isNodeResolved()) )
                {
                    expr.Arguments.Add(right);
                    expr.Arguments.Add(left);
                    nodes[pos] = new ILExpression(GMCode.Push, null, expr); // change it to a push
                    nodes.RemoveRange(pos - 2,2);
                    return true;
                }
            }
            return false;
        }
        void ResolveVariable( ILVariable v, ILNode instance, ILExpression index = null)
        {
            if (v.isResolved || v.isLocal || v.isGenerated) return;
            if (v.isArray)
            {
                Debug.Assert(index != null);
                if (instance.isNodeResolved() && index.isNodeResolved())                {
                    v.Index = index;
                    v.Instance = instance;
                    v.isResolved = true;
                }
            }
            else
            {
                Debug.Assert(index == null);
                if (instance.isNodeResolved())
                {
                    v.Index = null;
                    v.Instance = instance;
                    v.isResolved = true;
                }
            }
            if (v.isResolved)
            {
                ILExpression e = v.Instance as ILExpression; // should be an expresson we need to simplify
                switch (e.Code)
                {
                    case GMCode.Var:
                        {
                            ILVariable t = e.Operand as ILVariable;
                            Debug.Assert(t != null && t.isResolved); // should be there and resolved
                            v.Instance = t;
                       //     v.InstanceName = t.ToString();
                        }
                        break;
                    case GMCode.Constant:
                        {
                            ILValue value = e.Operand as ILValue;
                            Debug.Assert(v != null && value.Value is int); // should be there and resolved
                            v.Instance = value;
                        //    v.InstanceName = Context.InstanceToString((int) value);
                        }
                        break;
                    default:
                        v.InstanceName = "?(" + v.Instance.ToString() + ")";
                        // any other case should fail here
                        Debug.Assert(false);
                        break;
                }
            //    Debug.Assert(!v.InstanceName.Contains("Constant"));
            }
        }
        bool ResolveVariable(List<ILNode> nodes, ref int i, ILVariable v, bool remove = true)
        {
            if (!v.isResolved && !v.isLocal && !v.isGenerated)
            {
                if (v.isArray)
                {
                    ILNode t_instance = nodes.ElementAtOrDefault(i - 2);
                    ILNode t_index = nodes.ElementAtOrDefault(i - 1);
                    if (t_instance.FixPushExpression() && t_index.FixPushExpression() &&
                        t_instance.isNodeResolved() && t_index.isNodeResolved())
                    {
                        v.Index = t_index.MatchSingleArgument();
                        v.Instance = t_instance.MatchSingleArgument();
                        i -= 2;
                        if (remove) nodes.RemoveRange(i, 2);
                        v.isResolved = true;
                    }
                }
                else
                {
                    ILNode t_instance = nodes.ElementAtOrDefault(i - 1);
                    if (t_instance.FixPushExpression() && t_instance.isNodeResolved())
                    {
                        v.Index = null;
                        v.Instance = t_instance.MatchSingleArgument();
                        i--; // backup
                        if (remove) nodes.RemoveAt(i);
                        v.isResolved = true;
                    }
                }
                if (v.isResolved)
                {
                    ILExpression e = v.Instance as ILExpression; // should be an expresson we need to simplify
                    switch (e.Code)
                    {
                        case GMCode.Var:
                            {
                                ILVariable t = e.Operand as ILVariable;
                                Debug.Assert(t != null && t.isResolved); // should be there and resolved
                                v.Instance = t;
                            }
                            break;
                        case GMCode.Constant:
                            {
                                ILValue value = e.Operand as ILValue;
                                Debug.Assert(v != null && value.Value is int); // should be there and resolved
                                v.Instance = value;
                                v.InstanceName = Context.InstanceToString((int) value);
                            }
                            break;
                        default:
                            v.InstanceName = "?(" + v.Instance.ToString() + ")";
                            // any other case should fail here
                            Debug.Assert(false);
                            break;
                    }
                    return true;
                }
            }
            return false;
        }
        // FixPushEnviroment 

        public bool FixPushEnviroment(IList<ILNode> nodes)
        {
            bool modified = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Match(GMCode.Pushenv))
                {
                    ILExpression e = nodes[i] as ILExpression;
                    if (e.Arguments.Count > 0) continue; // skip
                    for (int j = i - 1; j >= 0; j--)
                    {
                        ILNode n = nodes[j];
                        if (n is ILLabel) continue; // skip labels
                        if (n.isExpressionResolved())
                        {
                            e.Arguments.Add(n.MatchSingleArgument());
                            nodes.RemoveAt(j);
                            i--;
                            modified = true;
                        }
                        break;
                    }
                }
            }
            return modified;
        }

        bool MatchVariablePush(List<ILNode> nodes, ILExpression expr, int pos)
        {
            ILExpression ev;
            ILVariable v;
            if (expr.Match(GMCode.Push, out ev) && ev.Code == GMCode.Var && !(v = ev.Operand as ILVariable).isResolved)
            {
                if (ResolveVariable(nodes, ref pos, v))
                    return true;
            }
            return false;
        }
        bool SimpleAssignments(List<ILNode> nodes, ILExpression expr, int pos)
        {
            ILVariable v;
            if (expr.Match(GMCode.Pop, out v)) {
                if (!v.isResolved)
                {
                    ResolveVariable(nodes, ref pos, v); // resolve like a push,
                    if (!v.isResolved) return false; // exit if we can't resolve it
                }
                ILExpression e;
                if(nodes.ElementAtOrDefault(pos-1).Match(GMCode.Push, out e) && e.isNodeResolved())
                {
                    
                    ILAssign assign = new ILAssign() { Variable = v, Expression = e.MatchSingleArgument() };
                    nodes[pos] = assign;
                    nodes.RemoveAt(pos - 1);
                    return true;
                }
            }
            return false;

        }
        bool ComplexAssignments(List<ILNode> nodes, ILExpression expr, int pos)
        {

            int dupType = 0;

            if (expr.Match(GMCode.Dup, out dupType))
            {
                ILExpression instance;
                ILExpression index;
                ILExpression expr_var;
                ILVariable v = null;
                if (dupType == 1 &&
                 nodes.ElementAtOrDefault(pos - 1).Match(GMCode.Push, out index) && index.isNodeResolved() &&
                 nodes.ElementAtOrDefault(pos - 2).Match(GMCode.Push, out instance) && instance.isNodeResolved() &&
                 nodes.ElementAtOrDefault(pos + 1).Match(GMCode.Push, out expr_var) && expr_var.Code == GMCode.Var)
                // this is usally an assign
                {
                    v = expr_var.Operand as ILVariable;
                    Debug.Assert(v.isArray);
                    Debug.Assert(!v.isResolved); // Some sanity checks
                    nodes.RemoveRange(pos - 2, 3); // lets remove the dup and the pushes, we are at the push var now
                    ResolveVariable(v, instance, index);// resolve it                   
                }
                else if (dupType == 0 &&
                  nodes.ElementAtOrDefault(pos - 1).Match(GMCode.Push, out instance) && instance.isNodeResolved() &&
                  nodes.ElementAtOrDefault(pos + 1).Match(GMCode.Push, out expr_var) && expr_var.Code == GMCode.Var)
                {
                    v = expr_var.Operand as ILVariable;
                    Debug.Assert(!v.isArray); // its not an array
                    Debug.Assert(!v.isResolved); // Some sanity checks
                    nodes.RemoveRange(pos - 1, 2); // lets remove the dup and the pushes, we are at the push var now
                    ResolveVariable(v, instance);// resolve it
                }
                if (v != null)
                {
                    ILVariable popVar = null;
                    Debug.Assert(v.isResolved);
                    for (int i = pos; i < nodes.Count; i++) // need to find the matching pop
                    {
                        if (nodes[i].Match(GMCode.Pop, out popVar))
                        {
                            Debug.Assert(popVar.Name == v.Name); // only thing we can compare
                            (nodes[i] as ILExpression).Operand = v;// replace it with fixed
                            break;
                        }
                    }
                    Debug.Assert(popVar != null);
                    return true;
                }
            }
            return false;
        }

        // Try to make reduce conditional branches
        bool SimplifyBranches(List<ILNode> nodes, ILExpression expr, int pos)
        {
            if ((expr.Code == GMCode.Bt || expr.Code == GMCode.Bf || expr.Code == GMCode.Ret) && expr.Arguments.Count == 0)
            {
                ILExpression condition = nodes.ElementAtOrDefault(pos - 1) as ILExpression;
                if (condition.isNodeResolved())
                {
                    expr.Arguments.Add(condition.MatchSingleArgument());
                    nodes.RemoveAt(pos - 1); // remove the push
                    return true;
                }
            }
            return false;
        }

      
            // This pass accepts index or instance values being 
            List < ILNode > BuildPreAst(List<Instruction> list) { 
         // Just convert instructions to ast streight
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
            ILExpression prev = null;
            foreach (var i in list)
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
                     //   Debug.Assert(prev.Code != GMCode.Call);
                        Debug.Assert(prev.Code != GMCode.Pop);
                        //   prev
                        if (prev != null && prev.Code == GMCode.Push || prev.Code == GMCode.Call || prev.Code == GMCode.Pop || prev.Code == GMCode.Dup)
                        {
                            prev.InferredType = i.Types[1];
                            prev.Conv = prev.Conv.Concat(i.Types).ToArray();
                        }
                        continue; // ignore all Conv for now
                    case GMCode.Call:
                        // Since we have to resolve calls seperately and need 
                        expr = new ILExpression(GMCode.Call, operand as string);
                        expr.Extra = extra; // need to know how many arguments we have
                        expr.ExpectedType = i.Types[0];
                        expr.Conv = i.Types;
                        break;
                    case GMCode.Popz:
                        expr = new ILExpression(code, null);
                        break;
                    case GMCode.Pop: // var define, so lets define it
                        expr = new ILExpression(GMCode.Pop,  BuildVar((int)operand, extra));
                        expr.Extra = extra;
                        expr.ExpectedType = i.Types[0];
                        expr.Conv = i.Types;
                        break;
                    case GMCode.Push:
                        if (i.Types[0] != GM_Type.Var)
                            expr = new ILExpression(GMCode.Push,  OperandToIValue(operand, i.Types[0]));// simple constant 
                        else
                            expr = new ILExpression(GMCode.Push, BuildVar((int)operand, extra));  // try to figure out the var);
                        expr.Extra = extra;
                        expr.ExpectedType = i.Types[0];
                        expr.Conv = i.Types;
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
                        expr = new ILExpression(code, null);
                        expr.Conv = i.Types;
                        break;
                    case GMCode.Bt:
                    case GMCode.Bf:
                        expr = new ILExpression(code, ConvertLabel(i.Operand as Label));
                        break;
                    case GMCode.Dup:
                        expr = new ILExpression(code, extra); // save the extra value for dups incase its dup eveything or just one
                                                              //      HackDebug(i, _method.Values);
                        expr.Conv = i.Types;
                        break;
                    case GMCode.Exit:
                        expr = new ILExpression(code, null);
                        break;
                    default:
                        expr = new ILExpression(code, null);
                        expr.Conv = i.Types;
                        break;
                }
                expr.ILRanges.Add(new ILRange(i.Address, i.Address));
                nodes.Add(expr);
                prev = expr;
            }
            return nodes;
        }
  
    
        void FlattenBasicBlocks(ILNode node)
        {
            ILBlock block = node as ILBlock;
            if (block != null)
            {
               
                List <ILNode> flatBody = new List<ILNode>();
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
      
        // IfElse fixes two things.  
        // 1. A long list of if then statements that have no else
        // 2. An inbeded if statment that goes to else, then continues as such
        static void CombineIFThenList(ILBlock block)
        {
            ILElseIfChain ifelse = null;
            for (int i=0; i < block.Body.Count; i++)
            {
                ILCondition c = block.Body[i] as ILCondition;
                int start = i;
                while (c != null && (c.FalseBlock == null || c.FalseBlock.Body.Count > 0))
                {
                    if (ifelse == null) ifelse = new ILElseIfChain();
                    ifelse.Conditions.Add(c);
                    c = block.Body.ElementAtOrDefault(++i) as ILCondition;
                }
                if (ifelse != null)
                {
                    block.Body[start] = ifelse;
                    block.Body.RemoveRange(start + 1, ifelse.Conditions.Count - 1);
                    ifelse = null; // out of scope
                }
            }
        }
        static void CombineIfThenElse(ILBlock block)
        {
            List<ILCondition> chain = null;
            for (int i = 0; i < block.Body.Count; i++)
            {
                ILCondition c = block.Body[i] as ILCondition;
                while (c != null && c.FalseBlock != null && c.FalseBlock.Body.Count == 1)
                {
                    ILCondition next = c.FalseBlock.Body[0] as ILCondition;
                    if (next == null) break;
                    chain = chain ?? new List<ILCondition>();
                    chain.Add(c);
                    c.FalseBlock = null;
                    c = next;
                }
                if (chain != null)
                {
                    ILElseIfChain ifelse = new ILElseIfChain() { Conditions = chain, Else = c.FalseBlock };
                    block.Body[i] = ifelse;
                    // ifelse.
                }
                chain = null; // out of scope
            }
        }
        /// <summary>
        /// Combines all the if statements to an elseif chain if we hit a case or something
        /// </summary>
        /// <param name="block">The Block to combine</param>
        /// <param name="minCombine">The minimum amount of if statments to combine</param>
        public static void CombineIfStatements(ILBlock method)
        {
            foreach (var block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                CombineIfThenElse(block); // usally made by switch statements
                // Don't use below found a bug where code might just want a bunch checking.  but above
                // is still valid
               // CombineIFThenList(block); // just a list of ifs
            }
            
        }
        // Fix for compiler generated loops
        public void FixAllPushes(List<ILNode> ast) // on the offchance we have a bunch of pushes, fix them for latter
        {
            foreach (var n in ast) n.FixPushExpression();
        }

        public bool MultiDimenionArray(List<ILNode> body, ILExpression expr, int pos)
        {
            // A break is when gamemaker has multi dimional array access.
            // Basicly, and I think this is a STUIPID hack as I supsect, internaly, GameMaker just makes
            // arrays as hash lookups.  It takes the y and * it by 32000 then adds the x, then wraps both 
            // expressions as a break  We will change it from V[x,y] to V[y][x] to be compatable with 
            // most stuff
            ILExpression yAccess;
            ILExpression multiConstant;
            ILExpression xAccess;
            if (expr.Code == GMCode.Add && expr.Arguments.Count == 0 &&
                body[pos - 1].Match(GMCode.Break) &&  
                body[pos - 2].Match(GMCode.Push, out yAccess) &&  // we are going backwards so this should be the y
                body[pos - 3].Match(GMCode.Mul) &&
                body[pos - 4].Match(GMCode.Push, out multiConstant) &&
                body[pos - 5].Match( GMCode.Break) &&
                body[pos - 6].Match(GMCode.Push, out xAccess))
            {
                if(xAccess.isNodeResolved() && yAccess.isNodeResolved())
                {
                    ILExpression newExpr = new ILExpression(GMCode.Array2D, null, yAccess, xAccess);
                    body.RemoveRange(pos - 6, 6);
                    body[pos - 6] = new ILExpression(GMCode.Push, null, newExpr);
                    //       Debug.Assert(false);
                    return true;
                }
            }
            return false;
        }
        // Soo, since I cannot be 100% sure where the start of a instance might be
        // ( could be an expresion, complex var, etc)
        // Its put somewhere in a block 
        public bool PushEnviromentFix(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            ILExpression expr;
            ILLabel pushLabelNext;
            ILLabel pushExprLabel;
            ILLabel pushenvLabel;
            List<ILExpression> args;
            if (head.MatchSingleAndBr(GMCode.Pushenv, out pushenvLabel, out args, out pushLabelNext) && args.Count == 0 &&
                (body.ElementAtOrDefault(pos - 1) as ILBasicBlock).MatchLastAndBr(GMCode.Push, out expr, out pushExprLabel))
            {
                ILBasicBlock pushBlock = body.ElementAtOrDefault(pos - 1) as ILBasicBlock;
                pushBlock.Body.RemoveAt(pushBlock.Body.Count - 2);
                args.Add(expr);
                return true;
            }
            return false;
        }
        bool AfterLoopsAndConditions(ILBlock method)
        {
            HashSet<ILBlock> badblocks = new HashSet<ILBlock>();
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                foreach(var child in block.GetChildren().OfType<ILExpression>().Where(x=> x.Code == GMCode.B || x.Code == GMCode.Bt || x.Code == GMCode.Bf))
                {
                    badblocks.Add(block);
                }
            }
            if (badblocks.Contains(method)) badblocks.Remove(method);
            if(badblocks.Count > 0) // we shouldn't have any branches anymore
            {
                ILBlock badmethod = new ILBlock();
                badmethod.Body = badblocks.Select(b => (ILNode) b).ToList();
                error.Error("After Loop And Conditions failed, look at bad_after_stuff.txt");
                error.CheckDebugThenSave(badmethod, "bad_after_stuff.txt");
                return true;
            }
            if(Context.Debug) Debug.Assert(badblocks.Count == 0);
            return false;
         
        }
        bool BeforeConditionsDebugSainityCheck(ILBlock method)
        {
            HashSet<ILBasicBlock> badblocks = new HashSet<ILBasicBlock>();
            Dictionary<GMCode, int> badCodes = new Dictionary<GMCode, int>();
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                foreach (ILBasicBlock bb in block.Body)
                {
                    for (int i = bb.Body.Count - 1; i >= 0; i--)
                    {
                        ILExpression expr = bb.Body.ElementAtOrDefault(i) as ILExpression;
                        if (expr != null)
                        {
                            if (expr.Code == GMCode.Dup || expr.Code == GMCode.Push || expr.Code == GMCode.Popz)
                            {
                                if (!badCodes.ContainsKey(expr.Code)) badCodes[expr.Code] = 0;
                                badCodes[expr.Code]++;
                                badblocks.Add(bb);
                            }
                            else if ((expr.Code == GMCode.Bt || expr.Code == GMCode.Bf) && expr.Arguments.Count == 0)
                            {
                                if (!badCodes.ContainsKey(expr.Code)) badCodes[expr.Code] = 0;
                                badCodes[expr.Code]++;
                                badblocks.Add(bb);
                            }
                        }
                    }
                }
            }

            if (badblocks.Count > 0)
            {
                ControlFlowLabelMap map = new ControlFlowLabelMap(method, error);

                error.DebugSave(method, "bad_block_dump.txt");
                //  HashSet<ILBasicBlock> callies = new HashSet<ILBasicBlock>();
                foreach (var bb in badblocks.ToList())
                {
                    badblocks.UnionWith(bb.GetChildren()
                        .OfType<ILExpression>()
                        .Where(x => x.Operand is ILLabel)
                        .Select(x => x.Operand as ILLabel)
                        .Select(x => map.LabelToBasicBlock(x)));

                    var p = map.LabelToParrents(bb.EntryLabel());
                    p.Add(bb.GotoLabel());

                    //  Debug.Assert(p.Count == 1);
                    badblocks.UnionWith(p.Select(x => map.LabelToBasicBlock(x)));
                    //  badblocks.Add(map.LabelToBasicBlock(p[0]));
                }
                ILBlock badmethod = new ILBlock();
                badmethod.Body = badblocks.OrderBy(b => b.GotoLabelName()).Select(b => (ILNode) b).ToList();
                
                string dfilename = error.MakeDebugFileName("bad_blocks.txt");
               
                using (StreamWriter sw = new StreamWriter(dfilename))
                {
                    sw.WriteLine("Filename: {0}", dfilename);
                    foreach (var kp in badCodes)
                        sw.WriteLine("Code: {0} Count: {1}", kp.Key, kp.Value);
                    sw.WriteLine();
                    var dwriter = new Writers.BlockToCode(new Writers.DebugFormater()); 
                    dwriter.Write(badmethod);
                    sw.WriteLine(dwriter.ToString());
                }
                error.Error("Before graph sanity check failed, look at bad_block_dump.txt");
                return true;
            }
            return false;
        }
        void FixIfStatements(ILBlock method)
        {
            bool modified;
            do
            {
                // We have to do this here as we want clean if statments
                // ILSpy does this later when it converts the ILAst to normal Ast, but since were skipping that
                // step, this "should" make if statements a bit cleaner
                modified = false;
                foreach (ILCondition ilCond in method.GetSelfAndChildrenRecursive<ILCondition>())
                {
                    if (ilCond.TrueBlock.Body.Count == 0 && (ilCond.FalseBlock != null && ilCond.FalseBlock.Body.Count != 0))
                    { // If we have an empty true block but stuff in the falls block, negate the condition and swap blocks
                        var swap = ilCond.TrueBlock;
                        ilCond.TrueBlock = ilCond.FalseBlock;
                        ilCond.FalseBlock = swap;
                        ilCond.Condition = ilCond.Condition.NegateCondition();
                        modified |= true;
                    }
                }
            } while (modified);
        }
        Context.ErrorContext error;
        public ILBlock Build(List<Instruction> list, Context.ErrorContext error)  //  List<string> StringList, List<string> InstanceList = null) //DecompilerContext context)
        {
            if (list.Count == 0) return new ILBlock();
            this.error = error;
            List<ILNode> ast = BuildPreAst(list);
            ILBlock method = new ILBlock();
            method.Body = ast;
            FixAllPushes(ast); // makes sure all pushes have no operands and are all expressions for latter matches
                
            Optimize.RemoveRedundantCode(method);
            error.CheckDebugThenSave(method, "raw.txt");
            foreach (var block in method.GetSelfAndChildrenRecursive<ILBlock>()) Optimize.SplitToBasicBlocks(block,true);
            if (Context.Debug)
            {
                error.SaveGraph(method, "basic_blocks.dot");
                error.CheckDebugThenSave(method, "basic_blocks.txt");
            }
            bool modified = false;
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
             
                do
                {
                    modified = false;
                  modified |= block.RunOptimization(new SimpleControlFlow(method,error).DetectSwitch);

                    modified |= block.RunOptimization(MatchVariablePush); // checks pushes for instance or indexs for vars
                    modified |= block.RunOptimization(SimpleAssignments);
                    modified |= block.RunOptimization(ComplexAssignments); // basicly self increment, this SHOULDN'T cross block boundrys
                    modified |= block.RunOptimization(SimplifyBranches); // Any resolved pushes are put into a branch argument
                    modified |= block.RunOptimization(CombineCall); // Any resolved pushes are put into a branch argument
                    modified |= block.RunOptimization(CombineExpressions); // Any resolved pushes are put into a branch argument
                    modified |= block.RunOptimization(PushEnviromentFix); // match all with's with expressions
                    modified |= block.RunOptimization(MultiDimenionArray);
                    modified |= block.RunOptimization(Optimize.SimplifyBoolTypes);
                    modified |= block.RunOptimization(Optimize.SimplifyLogicNot);

                    // fixes expressions that add strings
                    if (Context.outputType == OutputType.LoveLua) modified |= block.RunOptimization(Optimize.FixLuaStringAdd); // by block


                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).SimplifyShortCircuit);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).SimplifyTernaryOperator);

                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).FixOptimizedForLoops);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).JoinBasicBlocks);                                     
                    // somewhere, so bug, is leaving an empty block, I think because of switches
                    // It screws up the flatten block check for some reason
                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).RemoveRedundentBlocks);
                    if (Context.HasFatalError) return null;
                } while (modified);
            }
            error.CheckDebugThenSave(method, "before_loops.txt");
            if (BeforeConditionsDebugSainityCheck(method)) return null ;// sainity check, evething must be ready for this
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions(error).FindLoops(block);
            }
            if (Context.HasFatalError) return null;
            error.CheckDebugThenSave(method, "before_conditions.txt");

            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions(error).FindConditions(block);
            }
            if (Context.HasFatalError) return null;

            error.CheckDebugThenSave(method, "before_flatten.txt");
            FlattenBasicBlocks(method);
            error.CheckDebugThenSave(method, "before_gotos.txt");

            Optimize.RemoveRedundantCode(method);
            new GotoRemoval().RemoveGotos(method);

            error.CheckDebugThenSave(method, "before_if.txt");

            // This is cleaned up in ILSpy latter when its converted to another ast structure, but I clean it up here
            // cause I don't convert it and mabye not converting all bt's to bf's dosn't
            FixIfStatements(method);

            // final fix to clean up switch statements and remove extra ends
            if (Context.outputType == OutputType.LoveLua) CombineIfStatements(method);
           

            Optimize.RemoveRedundantCode(method);
            new GotoRemoval().RemoveGotos(method);

            error.CheckDebugThenSave(method, "final.txt");
            if (AfterLoopsAndConditions(method)) return null; // another sanity check

            return method;

        }
    }
}