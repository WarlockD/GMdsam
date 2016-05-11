using betteribttest.FlowAnalysis;
using GameMaker.FlowAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GameMaker.Dissasembler
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
        // checks to see if the node can be used in an expression, or if it needs latter processing
        bool isNodeResolved(ILExpression e)
        {
            if (e.Code == GMCode.Push) e = e.MatchSingleArgument(); // go into
            switch (e.Code)
            {
                case GMCode.Constant: return true; // always
                case GMCode.Var:
                    if ((e.Operand as ILVariable).isResolved) return true;
                    break;
                case GMCode.Call:
                    if ((e.Operand is ILCall)) return true;
                    break;
                default:
                    if (e.Code.isExpression() && e.Arguments.Count != 0) return true;
                    break;

            }
            return false;
        }
        bool isNodeResolved(ILNode node)
        {
            ILExpression e = node as ILExpression;
            if (e == null) return false;
            else return isNodeResolved(e);
        }
    


        bool CombineCall(List<ILNode> nodes, ILExpression expr, int pos)
        {
            if (expr.Code == GMCode.Call && expr.Operand is string)
            {
                List <ILExpression> args = GetArguments(nodes,  pos-1, expr.Extra);
                if (args != null)
                {
                    ILCall call = new ILCall() { Name = expr.Operand as string, Type = expr.InferredType, Arguments = args.Select(x => (ILNode) x).ToList() };
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
                if (n.FixPushExpression() && isNodeResolved(n))
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
                if (popDelta == 1 && nodes.ElementAtOrDefault(pos - 1).Match(GMCode.Push, out left) && isNodeResolved(left))
                {
                    expr.Arguments.Add(left);
                    nodes.RemoveAt(pos - 1);
                    return true;
                } else if (
                    popDelta == 2 &&
                     (nodes.ElementAtOrDefault(pos - 1).Match(GMCode.Push, out left) && isNodeResolved(left)) &&
                     (nodes.ElementAtOrDefault(pos - 2).Match(GMCode.Push, out right) && isNodeResolved(right)) )
                {
                    expr.Arguments.Add(left);
                    expr.Arguments.Add(right);
                    nodes.RemoveRange(pos - 2,2);
                    return true;
                }
            }
            return false;
        }
        bool ResolveVariable( ILVariable v, ILNode instance, ILNode index = null)
        {
            if (v.isResolved || v.isLocal || v.isGenerated) return false;
            if (v.isArray)
            {
                Debug.Assert(index != null);
                if (isNodeResolved(instance) && isNodeResolved(index))                {
                    v.Index = index;
                    v.Instance = instance;
                    v.isResolved = true;
                }
            }
            else
            {
                Debug.Assert(index == null);
                if (isNodeResolved(instance))
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
                            v.InstanceName = t.ToString();
                        }
                        break;
                    case GMCode.Constant:
                        {
                            ILValue value = e.Operand as ILValue;
                            Debug.Assert(v != null && value.Value is int); // should be there and resolved
                            v.Instance = value;
                            v.InstanceName = context.InstanceToString((int) value);
                        }
                        break;
                    default:
                        v.InstanceName = "?(" + v.Instance.ToString() + ")";
                        // any other case should fail here
                        Debug.Assert(false);
                        break;
                }
                Debug.Assert(!v.InstanceName.Contains("Constant"));
            }
            return false;
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
                        isNodeResolved(t_instance) && isNodeResolved(t_index))
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
                    if (t_instance.FixPushExpression() && isNodeResolved(t_instance))
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
                                v.InstanceName = t.ToString();
                            }
                            break;
                        case GMCode.Constant:
                            {
                                ILValue value = e.Operand as ILValue;
                                Debug.Assert(v != null && value.Value is int); // should be there and resolved
                                v.Instance = value;
                                v.InstanceName = context.InstanceToString((int) value);
                            }
                            break;
                        default:
                            v.InstanceName = "?(" + v.Instance.ToString() + ")";
                            // any other case should fail here
                            Debug.Assert(false);
                            break;
                    }
                    Debug.Assert(!v.InstanceName.Contains("Constant"));
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
                        if (n.FixPushExpression() && isNodeResolved(n))
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
        // Create simple assigments that have constants
        bool SimpleAssigments(List<ILNode> nodes, ILExpression expr, int pos)
        {
            bool modified = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                ILVariable v;
                if (nodes[i].Match(GMCode.Pop, out v))
                {
                    ResolveVariable(nodes, ref i, v);
                    if (!v.isResolved) break;
                    ILNode value = nodes.ElementAtOrDefault(i - 1);
                    if (value.FixPushExpression() && isNodeResolved(value))
                    {
                        ILAssign a = new ILAssign() { Variable = v, Expression = value.MatchSingleArgument() };
                        i--; // backup
                        nodes.RemoveAt(i);
                        nodes[i] = a;
                        modified |= true;
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
                if(nodes.ElementAtOrDefault(pos-1).Match(GMCode.Push, out e) && isNodeResolved(e))
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
            bool modified = false;

            ILExpression instance;
            ILExpression index;
            ILExpression ev;
            int dupType = 0;



            


            if (expr.Match(GMCode.Push, out instance) && isNodeResolved(instance) &&
                nodes.ElementAtOrDefault(pos + 1).Match(GMCode.Push, out index) && isNodeResolved(index) &&
                nodes.ElementAtOrDefault(pos + 2).Match(GMCode.Dup, out dupType) &&
                dupType == 1 &&
                nodes.ElementAtOrDefault(pos + 3).Match(GMCode.Push, out ev) &&
                ev.Code == GMCode.Var)  // resonably sure we have a complex assignment
            {
                ILVariable v = ev.Operand as ILVariable;
                nodes.RemoveAt(pos + 2); // lets remove the dup
                Debug.Assert(v.isResolved == false);
               ResolveVariable(v, instance, index);
                pos = pos + 2;
                // we now have a valid var, lets find where the pop is
                ILVariable popVar = null;
                for (int i=pos;i < nodes.Count; i++)
                {
                    if(nodes[i].Match(GMCode.Pop, out popVar))
                    {
                        Debug.Assert(popVar.Name == v.Name); // only thing we can compare
                        (nodes[i] as ILExpression).Operand = v;// replace it with fixed
                        break; 
                    }
                }
                Debug.Assert(popVar != null);
                modified = true;
            }

            return modified;
        }

        // Try to make reduce conditional branches
        bool SimplifyBranches(List<ILNode> nodes, ILExpression expr, int pos)
        {
            if ((expr.Code == GMCode.Bt || expr.Code == GMCode.Bf || expr.Code == GMCode.Ret) && expr.Arguments.Count == 0)
            {
                ILNode condition = nodes.ElementAtOrDefault(pos - 1);
                if (isNodeResolved(condition))
                {
                    expr.Arguments.Add(condition.MatchSingleArgument());
                    nodes.RemoveAt(pos - 1); // remove the push
                    return true;
                }
            }
            return false;
        }

      
            // This pass accepts index or instance values being 
            List < ILNode > BuildPreAst() { 
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
                        expr = new ILExpression(code, null, new ILExpression(GMCode.Pop, null));
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
      
        GMContext context;
        /// <summary>
        /// Combines all the if statements to an elseif chain.
        /// </summary>
        /// <param name="block">The Block to combine</param>
        /// <param name="minCombine">The minimum amount of if statments to combine</param>
        public static void CombineIfStatements(ILBlock method, int minCombine)
        {
            int start = -1;
            ILElseIfChain chain = null;
            string v = null;
            Func<ILCondition,string> GetVarInCondition = (ILCondition c)=>{
                if(c.Condition.Arguments.Count == 1)
                {
                    if (c.Condition.Arguments[0].Code == GMCode.Call)
                        return c.Condition.Operand as string; // combine all function calls
                }
                if(c.Condition.Arguments.Count == 2)
                {
                    if (c.Condition.Arguments[0].Code == GMCode.Var && c.Condition.Arguments[1].Code == GMCode.Constant)
                        return c.Condition.Arguments[0].ToString();
                    else if (c.Condition.Arguments[0].Code == GMCode.Constant && c.Condition.Arguments[1].Code == GMCode.Var)
                        return c.Condition.Arguments[1].ToString();
                }
                return null;
            };
            Func<ILBlock, bool> InsertChain = (ILBlock block) =>
             {
                 if (chain.Conditions.Count < minCombine) return false;
                 ILCondition last = chain.Conditions.Last();
                 // if (chain.Conditions.Take(chain.Conditions.Count - 1).Any(x => x.FalseBlock != null && x != last)) break;
                 if (chain.Conditions.Any(x => x.FalseBlock != null && x != last)) return false;
                 if (last.FalseBlock != null) // we have an else here
                 {
                     chain.Else = last.FalseBlock;
                     last.FalseBlock = null;
                 }
             //    Debug.Assert("self.gx" != v);
                 //    foreach (var a in chain.Conditions) block.Body.Remove(a);
                 block.Body.RemoveRange(start + 1, chain.Conditions.Count - 1);
                 block.Body[start] = chain;
                 return true;
             };
            foreach (var block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {

                for (int i = 0; i < block.Body.Count; i++)
                {
                    ILCondition c = block.Body[i] as ILCondition;
                    if (c != null)
                    {
                        if (v == null)
                        {
                            v = GetVarInCondition(c);
                            if (v == null) continue;
                            start = i;
                            chain = new ILElseIfChain();
                            chain.Conditions.Add(c);
                        }
                        else
                        {
                            string cv = GetVarInCondition(c);
                            if (cv != v) {
                                if (InsertChain(block))
                                    i = start;
                                chain = null;
                                v = null;   
                            }
                            else chain.Conditions.Add(c);
                        }
                    }
                    if(chain != null) {
                        if (InsertChain(block))
                            i = start;
                        chain = null;
                        v = null;
                    }
                }
            }
           
        }
        // Fix for compiler generated loops
        public void FixAllPushes(List<ILNode> ast) // on the offchance we have a bunch of pushes, fix them for latter
        {
            foreach (var n in ast) n.FixPushExpression();
        }
        public void SanityCheck(ILBlock method)
        {
            
        }
        public ILBlock Build(SortedList<int, Instruction> code, bool optimize, GMContext context)  //  List<string> StringList, List<string> InstanceList = null) //DecompilerContext context)
        {
            bool modified;
            if (code.Count == 0) return new ILBlock();
                this.context = context;


            _method = code;
            this.optimize = optimize;
            List<ILNode> ast = BuildPreAst();
            ILBlock method = new ILBlock();
            method.Body = ast;
            method.DebugSave(context.MakeDebugFileName("before_clean.txt"));
            FixAllPushes(ast); // makes sure all pushes have no operands and are all expressions for latter matches
            do
            {
                modified = false;
                
              //  modified |= CombineExpressions(ast);
           //     modified |= ResolveVarsWithExpressions(ast);
              //  modified |= CombineCall(ast);
             //   modified |= SimpleAssigments(ast);
               
                modified |= FixPushEnviroment(ast);
             //   modified |= SimplifyBranches(ast);
             //   if (context.doLua) modified |= DetectSwitchLua(ast); better once basicblocks are created
            } while (modified);
          

            // sainity check, all dups must of been handled
            var list = method.GetSelfAndChildrenRecursive<ILExpression>(x => x.Code == GMCode.Dup).ToList();

            // RealStackAnalysis rsa = new RealStackAnalysis(context);
            //  var body = rsa.Build(method);
            // Beleve it or not, this stack system works
            // However, I am only dealing with a few set paterns
            // so lets match them only

            method.DebugSave(context.MakeDebugFileName("raw_body.txt"));

            if (context.Debug) method.DebugSave(context.MakeDebugFileName("raw_body.txt"));
            GameMaker.Dissasembler.Optimize.RemoveRedundantCode(method);
            foreach(var block in method.GetSelfAndChildrenRecursive<ILBlock>()) Optimize.SplitToBasicBlocks(block);
            if(context.Debug) method.DebugSave(context.MakeDebugFileName("basic_blocks.txt"));

            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
             
                do
                {
                    modified = false;
                    if (context.doLua) modified |= block.RunOptimization(new SimpleControlFlow(method, context).DetectSwitchLua);

                    modified |= block.RunOptimization(MatchVariablePush); // checks pushes for instance or indexs for vars
                    modified |= block.RunOptimization(SimpleAssignments);
                    modified |= block.RunOptimization(ComplexAssignments); // basicly self increment, this SHOULDN'T cross block boundrys
                    modified |= block.RunOptimization(SimplifyBranches); // Any resolved pushes are put into a branch argument
                    modified |= block.RunOptimization(CombineCall); // Any resolved pushes are put into a branch argument
                    modified |= block.RunOptimization(CombineExpressions); // Any resolved pushes are put into a branch argument
                    

                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).SimplifyShortCircuit);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).SimplifyTernaryOperator);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, context).JoinBasicBlocks);


                    // basicly self increment
                    //  modified |= ComplexAssignments(ast);
                  
                    modified |= block.RunOptimization(Optimize.SimplifyBoolTypes);
                    modified |= block.RunOptimization(Optimize.SimplifyLogicNot);
                    modified |= block.RunOptimization(Optimize.ReplaceWithAssignStatements);
                    // This SHOULD work as the root expression would check the rest of it and
                    // the byte code should of made sure they are all add statements anyway
                    //  if(context.doLua)  modified |= block.RunOptimization(Optimize.FixLuaStringAddExpression);
                    if (context.doLua) modified |= block.RunOptimization(Optimize.FixLuaStringAdd); // by block


                } while (modified);
            }
            if (context.Debug) method.DebugSave(context.MakeDebugFileName("before_loop.txt"));
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions(context).FindLoops(block);
            }
            if (context.Debug) method.DebugSave(context.MakeDebugFileName("before_conditions.txt"));
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions(context).FindConditions(block);
            }

            FlattenBasicBlocks(method);
            if (context.Debug) method.DebugSave(context.MakeDebugFileName("before_gotos.txt"));
            Optimize.RemoveRedundantCode(method);
            new GotoRemoval(context).RemoveGotos(method);
            Optimize.RemoveRedundantCode(method);

            new GotoRemoval(context).RemoveGotos(method);

            GotoRemoval.RemoveRedundantCode(context,method);
            new GotoRemoval(context).RemoveGotos(method);

            if (context.doLua) CombineIfStatements(method, 3);
            if (context.Debug) method.DebugSave(context.MakeDebugFileName("final.cpp"));


            // cleanup functions
            Optimize.ReplaceExpressionsWithCalls(method);


            return method;

        }
    }
}