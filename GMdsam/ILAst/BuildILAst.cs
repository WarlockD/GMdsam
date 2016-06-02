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
        List<ILExpression> GetArguments(List<ILNode> nodes,  int index, int count, bool remove = true)
        {
            List<ILExpression> args = new List<ILExpression>();
            int scount = count;

            for (int i= index; i >= 0 && count > 0; i--, count--)
            {
                ILNode n = nodes[i];
                if (n.isNodeResolved())
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
        List<ILRange> ResolveVariable( ILVariable v, ILExpression instance, ILExpression index = null)
        {
            if (v.isResolved || v.isLocal || v.isGenerated) return null;
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
                List<ILRange> ranges = new List<ILRange>();
                ranges.AddRange(instance.ILRanges);
                if (index != null) ranges.AddRange(index.ILRanges);
                return ranges;
            }
            return null;
        }
        List<ILRange> ResolveVariable(List<ILNode> nodes, ref int i, ILVariable v, bool remove = true)
        {
            ILExpression t_instance = null;
            ILExpression t_index = null;
            if (!v.isResolved && !v.isLocal && !v.isGenerated)
            {
                if (v.isArray)
                {
                    t_instance = nodes.ElementAtOrDefault(i - 2) as ILExpression;
                    t_index = nodes.ElementAtOrDefault(i - 1) as ILExpression;
                    if (t_instance.isNodeResolved() && t_index.isNodeResolved())
                    {
                        v.Index = t_index.MatchSingleArgument();
                        v.Instance = t_instance.MatchSingleArgument();
                        v.isResolved = true;
                        i -= 2;
                        if (remove) nodes.RemoveRange(i, 2);
                       
                    }
                }
                else
                {
                    t_instance = nodes.ElementAtOrDefault(i - 1) as ILExpression;
                    if ( t_instance.isNodeResolved())
                    {
                        v.Index = null;
                        v.Instance = t_instance.MatchSingleArgument();
                        v.isResolved = true;
                        i--; // backup
                        if (remove) nodes.RemoveAt(i);
                        
                    }
                }
                if (v.isResolved)
                {
                    List<ILRange> ranges = new List<ILRange>();
                    ranges.AddRange(t_instance.ILRanges);
                    if(t_index != null) ranges.AddRange(t_index.ILRanges);
                    return ranges;
                }
            }
            return null;
        }
        bool MatchVariablePush(List<ILNode> nodes, ILExpression expr, int pos)
        {
            ILExpression ev;
            ILVariable v;
            if (expr.Match(GMCode.Push, out ev) && ev.Code == GMCode.Var && !(v = ev.Operand as ILVariable).isResolved)
            {
                var ranges = ResolveVariable(nodes, ref pos, v);
                if(ranges != null)
                {
                    ev.WithILRangesAndJoin(ranges); // last time we will need to do this
                    return true;
                }
            }
            return false;
        }
        bool SimpleAssignments(List<ILNode> nodes, ILExpression expr, int pos)
        {
            ILVariable v;
            if (expr.Match(GMCode.Pop, out v)) {
                if (!v.isResolved)
                {
                    var ranges = ResolveVariable(nodes, ref pos, v);
                    if (ranges == null) return false; // exit if we can't resolve it
                    expr.WithILRanges(ranges);
                }
                ILExpression e;
                if(nodes.ElementAtOrDefault(pos-1).Match(GMCode.Push, out e) && e.isNodeResolved())
                {
                    expr.Code = GMCode.Assign;
                    expr.Operand = v;
                    expr.WithILRangesAndJoin(e.ILRanges, (nodes.ElementAtOrDefault(pos - 1) as ILExpression).ILRanges);
                    Debug.Assert(expr.Arguments.Count == 0);
                    expr.Arguments.Add(e);
                    nodes.RemoveAt(pos - 1); // remove the push
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
                ILExpression expr_var=null; // compiler warning
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
                    expr_var.WithILRangesAndJoin(ResolveVariable(v, instance, index), expr.ILRanges);// resolve it                   
                }
                else if (dupType == 0 &&
                  nodes.ElementAtOrDefault(pos - 1).Match(GMCode.Push, out instance) && instance.isNodeResolved() &&
                  nodes.ElementAtOrDefault(pos + 1).Match(GMCode.Push, out expr_var) && expr_var.Code == GMCode.Var)
                {
                    v = expr_var.Operand as ILVariable;
                    Debug.Assert(!v.isArray); // its not an array
                    Debug.Assert(!v.isResolved); // Some sanity checks
                    nodes.RemoveRange(pos - 1, 2); // lets remove the dup and the pushes, we are at the push var now
                    expr_var.WithILRangesAndJoin(ResolveVariable(v, instance), expr.ILRanges);// resolve it   
                }
                else return false;
                if (v != null)
                {
                    ILVariable popVar = null;
                    Debug.Assert(v.isResolved);
                    for (int i = pos; i < nodes.Count; i++) // need to find the matching pop
                    {
                        
                        if (nodes[i].Match(GMCode.Pop, out popVar))
                        {
                            ILExpression e = nodes[i] as ILExpression;
                            Debug.Assert(popVar.Name == v.Name); // only thing we can compare
                            e.WithILRangesAndJoin(expr_var.ILRanges); // copy the ranges cause its a dup
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
        // This should ONLY be run at the start
        public void FixAllPushes(List<ILNode> ast) // on the offchance we have a bunch of pushes, fix them for latter
        {
            foreach (var e in ast.OfType<ILExpression>())
            {
                if (e.Code != GMCode.Push) continue; // not an expresson or null
                if (e.Operand == null)
                {
                    ILExpression arg = e.Arguments.Single();
                    if (arg.Code != GMCode.Constant && arg.Code != GMCode.Var) throw new Exception("sanity check");

                }
                else  // check if it needs to be processed
                {
                    Debug.Assert(e.Arguments.Count == 0);
                    if (e.Code == GMCode.Constant || e.Code == GMCode.Var)
                    {
                        ILExpression arg = null;
                        object o = e.Operand;
                        if (o is ILValue) arg = new ILExpression(GMCode.Constant, o as ILValue);
                        else if (o is ILVariable) arg = new ILExpression(GMCode.Var, o as ILVariable);
                        else if (o is ILCall) arg = new ILExpression(GMCode.Call, o);
                        Debug.Assert(arg != null);
                        e.Arguments.Clear(); // make sure
                        e.Arguments.Add(arg);
                        e.Operand = null;
                    }
                }
            }
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
                    var dwriter = new Writers.BlockToCode(error); 
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
        public ILBlock Build(ILBlock method, Context.ErrorContext error)
        {
            if (method == null) throw new ArgumentNullException("method");
            if (error == null) throw new ArgumentNullException("error");
            this.error = error;
            // Not sure I need this pass now 
            FixAllPushes(method.Body); // makes sure all pushes have no operands and are all expressions for latter matches
                

            Optimize.RemoveRedundantCode(method);
            error.CheckDebugThenSave(method, "raw.txt");
            foreach (var block in method.GetSelfAndChildrenRecursive<ILBlock>()) Optimize.SplitToBasicBlocks(block,true);
            error.CheckDebugThenSave(method, "basic_blocks.txt");

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