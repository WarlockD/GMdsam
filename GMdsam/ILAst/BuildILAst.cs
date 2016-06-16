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
        bool CombineCall(IList<ILNode> nodes, ILExpression expr, int pos)
        {
            ILCall call;
            if(expr.Match(GMCode.CallUnresolved, out call))
            {
                List <ILExpression> args = GetArguments(nodes,  pos-1, expr.Extra); // must use extra as the number of args could be diffrent
                if (args != null)
                {
                    expr.Arguments = args;
                    expr.Code = GMCode.Call;           
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
        List<ILExpression> GetArguments(IList<ILNode> nodes,  int index, int count, bool remove = true)
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
            if (remove && count == 0)
            {
                if (scount == 1) nodes.RemoveAt(index);
                else
                {
                    for (int i = index; i >= 0 && scount > 0; i--, scount--) nodes.RemoveAt(i);
                   // nodes.RemoveRange(index - scount + 1, scount);
                }
            }
            return count == 0 ? args : null;
        }
        // Here we convert constants and vars into expression containers, combine simple expresions
        // and create IAssign's and ILCalls if nessasary
        bool CombineExpressions(IList<ILNode> nodes, ILExpression expr, int pos)
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
        void ResolveVariable(ILExpression expr, ILExpression instance = null, ILExpression index = null)
        {
            Debug.Assert(expr != null);
            UnresolvedVar uv = expr.Operand as UnresolvedVar;
            Debug.Assert(uv != null);
            ILVariable v = null;
            if (uv.Extra != 0)
            {
                Debug.Assert(instance == null);
                instance = new ILExpression(GMCode.Constant, new ILValue(uv.Extra)); // create a fake instance value
            }
            instance = instance.MatchSingleArgument();
            if (instance.Code == GMCode.Constant)
            {
                ILValue value = instance.Operand as ILValue;
                v = ILVariable.CreateVariable(uv.Name, (int) value, this.locals);
            }
            else if (instance.Code == GMCode.Var)
            {
                v = ILVariable.CreateVariable(uv.Name, 0, this.locals);
                (instance.Operand as ILVariable).Type = GM_Type.Instance; // make sure its an instance
            }
            else throw new Exception("humm");
            if (uv.Operand >= 0)
            {
                Debug.Assert(index != null);
                index = index.MatchSingleArgument();
                if(index.Code == GMCode.Var)
                {
                    ILVariable ivar = index.Operand as ILVariable; // indexs can only be ints, but they can be negitive?
                    ivar.Type = GM_Type.Int;
                }
            }
            else index = null;
            Debug.Assert(v != null);
            expr.Code = GMCode.Var;
            expr.Operand = v;
            expr.Arguments.Clear();
            expr.Arguments.Add(instance);
            if (index != null) expr.Arguments.Add(index);
        }
        bool MatchVariablePush(IList<ILNode> nodes, ILExpression expr, int pos)
        {
            if (VarBuilderMatch(nodes, ref expr, pos, GMCode.Push))
            {
                Debug.Assert(expr.Code == GMCode.Var);
                nodes[pos] = new ILExpression(GMCode.Push, null, expr);
                return true;
            }
            return false;
        }
      
        bool VarBuilderMatchSimple(IList<ILNode> nodes, ref ILExpression expr, int pos, GMCode code)
        {
            UnresolvedVar uv;
            if (expr.Match(code, out uv) && uv.Extra != 0)
            {

               ResolveVariable(expr);
                return true; 
            }
            return false;
        }
        bool VarBuilderMatchStack(IList<ILNode> nodes, ref ILExpression expr, int pos, GMCode code)
        {
            UnresolvedVar uv;
            ILExpression instance = null;
            if (nodes.ElementAtOrDefault(pos + 1).Match(code, out uv) &&
                uv.Extra == 0 && uv.Operand < 0 &&
                expr.Match(GMCode.Push, out instance))
            {
                expr = nodes[pos + 1] as ILExpression;
                ResolveVariable(expr, instance);
                nodes.RemoveAt(pos);
                return true;
            }
            return false;
        }
        bool VarBuilderMatchArray(IList<ILNode> nodes, ref ILExpression expr, int pos, GMCode code)
        {
            UnresolvedVar uv;
            ILExpression instance = null;
            ILExpression index = null;
            if (nodes.ElementAtOrDefault(pos + 2).Match(code, out uv) &&
                uv.Extra == 0 && uv.Operand >= 0 &&
                nodes.ElementAtOrDefault(pos + 1).Match(GMCode.Push, out index) &&
                expr.Match(GMCode.Push, out instance))
            {
                expr = nodes[pos + 2] as ILExpression;
                ResolveVariable(expr, instance, index);
                nodes.RemoveRange(pos, 2);
                return true;
            }
            return false;
        }
        bool VarBuilderMatch(IList<ILNode> nodes, ref ILExpression expr, int pos, GMCode code)
        {
            return VarBuilderMatchSimple(nodes, ref expr, pos, code) || 
                VarBuilderMatchStack(nodes, ref expr, pos, code) || 
                VarBuilderMatchArray(nodes, ref expr, pos, code);
        }
        bool AssignValueTo(IList<ILNode> nodes, ILExpression expr, int pos)
        {
            IList<ILExpression> args;
            ILExpression assigmentValue;
            if (nodes.ElementAtOrDefault(pos + 1).Match(GMCode.Assign, out args) &&
                args.Count == 1 &&
               expr.Match(GMCode.Push, out assigmentValue))
            {
                nodes.RemoveAt(pos);
                args.Add(assigmentValue);

                return true;
            }
            return false;
        }
        bool SimpleAssignments(IList<ILNode> nodes, ILExpression expr, int pos)
        {
            if (VarBuilderMatch(nodes, ref expr, pos, GMCode.Pop))
            {
                Debug.Assert(expr.Code == GMCode.Var);
                nodes[pos] = new ILExpression(GMCode.Assign, null, expr);
                return true;
            }
            return false;           
        }
        // Changed the design on this, just emulated the dup by replacing dup and inserting the values needed for the two unresolved assigns, it SHOULD all resolve
        // though the pump
        void FixComplexPop(IList<ILNode> nodes, ILExpression var_expr, int start, UnresolvedVar uv) // uv is only used for debugging
        {
            UnresolvedVar popVar = null; // the pop var that we dupped
            for (int i = start; i < nodes.Count; i++) // need to find the matching pop, should be only a push up
            {

                if (nodes[i].Match(GMCode.Pop, out popVar))
                {
                    Debug.Assert(popVar.Name == uv.Name && uv.Operand == popVar.Operand && uv.Extra == popVar.Extra); // only thing we can compare
                    ILExpression expr = nodes[i] as ILExpression;
                    ILExpression assign = new ILExpression(GMCode.Assign, null, new ILExpression(var_expr));
                    assign.WithILRanges(expr);
                    nodes[i] = assign;
                    break;
                }
            }
        }
        // This is like assign add, ++, etc
        bool ComplexPopArray(IList<ILNode> nodes, ILExpression expr, int pos)
        {
            ILLabel l = nodes[0] as ILLabel;
            int dupType = 0;
            ILExpression instance=null;
            ILExpression index=null;
            UnresolvedVar uv = null; // compiler warning
            if (nodes.ElementAtOrDefault(pos + 2).Match(GMCode.Dup, out dupType) && dupType == 1 && // stack var
                expr.Match(GMCode.Push, out instance) &&
                nodes.ElementAtOrDefault(pos + 1).Match(GMCode.Push, out index) &&
                nodes.ElementAtOrDefault(pos + 3).Match(GMCode.Push, out uv))
            {
                expr = nodes[pos+3] as ILExpression;
                Debug.Assert(expr.Operand is UnresolvedVar);
                expr.AddILRange(nodes[2]); // add the dup to the offsets
                nodes.RemoveRange(pos, 3); 
                ResolveVariable(expr, instance, index); // fix it this way so we keep all the ILRanges
                nodes[pos] = new ILExpression(GMCode.Push, null, expr);
                FixComplexPop(nodes, expr, pos + 1, uv);
                return true;
            }
            return false;
        }
        bool ComplexPopStack(IList<ILNode> nodes, ILExpression expr, int pos)
        {
            int dupType = 0;
            ILExpression instance;
            UnresolvedVar uv = null; // compiler warning
            if (nodes.ElementAtOrDefault(pos+1).Match(GMCode.Dup, out dupType) && dupType == 0 && // stack var
                expr.Match(GMCode.Push, out instance) &&
                nodes.ElementAtOrDefault(pos+2).Match(GMCode.Push, out uv))
            {
                expr = nodes[pos + 3] as ILExpression;
                Debug.Assert(expr.Operand is UnresolvedVar);
                nodes.RemoveRange(pos, 2); // dup and instance
                ResolveVariable(expr, instance);
                FixComplexPop(nodes, expr, pos + 1,uv);
                return true;
            }
            return false;
        }
        bool ComplexAssignments(IList<ILNode> nodes, ILExpression expr, int pos)
        {
            return ComplexPopStack(nodes, expr, pos) || ComplexPopArray(nodes, expr, pos);
        }

        // Try to make reduce conditional branches
        bool SimplifyBranches(IList<ILNode> nodes, ILExpression expr, int pos)
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
      
   
    
        // This should ONLY be run at the start
        public void FixAllPushes(IList<ILNode> ast) // on the offchance we have a bunch of pushes, fix them for latter
        {
            foreach (var e in ast.OfType<ILExpression>())
            {
                if (e.Code != GMCode.Push || e.Code != GMCode.Pop) continue; // not an expresson or null
                Debug.Assert(e.Operand == null);
            }
        }

        public bool MultiDimenionArray(IList<ILNode> body, ILExpression expr, int pos)
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
            IList<ILExpression> args;
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
          //      Context.CheckDebugThenSave(badmethod, "bad_after_stuff.txt");
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
                foreach (ILBasicBlock bb in block.GetSelfAndChildrenRecursive<ILBasicBlock>())
                {
                    foreach (ILExpression expr in bb.GetSelfAndChildrenRecursive<ILExpression>())
                    {
                        if (expr.Operand is UnresolvedVar)
                        {
                            if (!badCodes.ContainsKey(expr.Code)) badCodes[expr.Code] = 0;
                            badCodes[expr.Code]++;
                            badblocks.Add(bb);
                        }
                        else if (expr.Code == GMCode.Dup || expr.Code == GMCode.Push || expr.Code == GMCode.Popz || expr.Code == GMCode.CallUnresolved)
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
                    sw.WriteLine("Time : {0}", DateTime.Now);
                    sw.WriteLine("Filename: {0}", dfilename);
                    foreach (var kp in badCodes)
                        sw.WriteLine("Code: {0} Count: {1}", kp.Key, kp.Value);
                    sw.WriteLine();
                    sw.WriteLine(badmethod.ToString());
                }
                error.Message("Saved '{0}'", dfilename);
                error.FatalError("Before graph sanity check failed, look at bad_block_dump.txt");
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
        public static bool RunTestOptimiztion(ILBasicBlock bb, Func<IList<ILNode>, ILExpression, int, bool> optimization)
        {
            for (int i = bb.Body.Count - 1; i >= 0; i--)
            {
                ILExpression expr = bb.Body.ElementAtOrDefault(i) as ILExpression;
                if (expr != null && optimization(bb.Body, expr, i)) return true;
            }
            return false;
        }
        ErrorContext error;
        Dictionary<string, ILVariable> locals = null;

        // This resolves a block.  Only use functions that don't break the block boundry!
        Func<IList<ILNode>, ILExpression, int, bool>[] singleBasicBlockOptimizations = null; 
        bool ResolveBasicBlock(IList<ILNode> nodes, ILBasicBlock bb, int pos)
        {
            if (singleBasicBlockOptimizations == null)
            {
                singleBasicBlockOptimizations = new Func<IList<ILNode>, ILExpression, int, bool>[]
                   {
                    MatchVariablePush,  // checks pushes for instance or indexs for vars
                       SimpleAssignments,
                       AssignValueTo,
                       ComplexAssignments, // basicly self increment, this SHOULDN'T cross block boundrys
                       SimplifyBranches,   // Any resolved pushes are put into a branch argument
                       CombineCall,        // Any resolved pushes are put into a branch argument
                       CombineExpressions
                   };
            }
            bool modified = bb.RunOptimizationAndRestart(singleBasicBlockOptimizations);
            if (modified && Context.Debug) // It should be 100% resolved at this point
            {
                if (bb.RunOptimizationAndRestart(singleBasicBlockOptimizations))
                {
                    bb.DebugSave("badresolve.txt");
                    bool test = bb.RunOptimizationAndRestart(singleBasicBlockOptimizations);



                    Debug.Assert(!test);
                }
            }
            return modified;
        }
        public ILBlock Build(ILBlock method, Dictionary<string, ILVariable> locals, ErrorContext error)
        {
            if (method == null) throw new ArgumentNullException("method");
            if (error == null) throw new ArgumentNullException("error");
            if (locals == null) throw new ArgumentNullException("locals");
            this.error = error;
            this.locals = locals;
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
                    modified |= block.RunOptimization(ResolveBasicBlock); /// This fixes all internal block vars, simple dups, push expressions etc
                    modified |= block.RunOptimization(new SimpleControlFlow(method,error).DetectSwitch);


                

                    //  modified |= RunTestOptimization(block);
                    //  modified |= block.RunOptimization(MultiDimenionArray);
      
                    modified |= block.RunOptimization(Optimize.SimplifyBoolTypes);
                    modified |= block.RunOptimization(Optimize.SimplifyLogicNot);
            //        modified |= block.RunOptimization(ResolveVarAssigmentType);  We need to change this as we are fixing the type system


                    modified |= block.RunOptimization(PushEnviromentFix); // match all with's with expressions
                    // fixes expressions that add strings

                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).SimplifyShortCircuit);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).SimplifyTernaryOperator);

                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).FixOptimizedForLoops);
                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).JoinBasicBlocks);                                     
                    // somewhere, so bug, is leaving an empty block, I think because of switches
                    // It screws up the flatten block check for some reason
                    modified |= block.RunOptimization(new SimpleControlFlow(method, error).RemoveRedundentBlocks);
                    if (Context.HasFatalError) return null;
                } while (modified);
                block.FixParents();
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

           

            Optimize.RemoveRedundantCode(method);
            new GotoRemoval().RemoveGotos(method);

            error.CheckDebugThenSave(method, "final.txt");
            if (AfterLoopsAndConditions(method)) return null; // another sanity check

            return method;

        }
    }
}