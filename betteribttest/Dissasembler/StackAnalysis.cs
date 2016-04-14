using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace betteribttest.Dissasembler
{

    public static class ILAstBuilderExtensions
    {
        public static void DebugPrintILAst(this IEnumerable<ILNode> nodes, string filename)
        {
            int labelMax = 0;
            foreach (var n in nodes.OfType<ILLabel>()) if (n.ToString().Length > labelMax) labelMax = n.ToString().Length;
            using (StreamWriter sw = new StreamWriter(filename))
            {
                PlainTextWriter ptw = new PlainTextWriter(sw);
                ptw.Header = new string(' ', labelMax + 2); // fill up header
                bool inLabel = false;
                foreach (var i in nodes)
                {
                    if (i is ILLabel)
                    {
                        if (inLabel) ptw.WriteLine();
                        ptw.Header = i.ToString();
                        inLabel = true;
                    }
                    else
                    {
                        i.WriteTo(ptw);
                        ptw.WriteLine();
                        inLabel = false;
                        ptw.Header = null;
                    }
                }
            }

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
        List<string> InstanceList;
        List<string> StringList;
        bool optimize;

        ILExpression OperandToExpresson(object obj, GM_Type type)
        { // throws things if the cast is bad
            switch (type)
            {
                case GM_Type.Bool: return new ILExpression(GMCode.Constant, (bool)obj);
                case GM_Type.Double: return new ILExpression(GMCode.Constant, (double)obj);
                case GM_Type.Float: return new ILExpression(GMCode.Constant, (float)obj);
                case GM_Type.Long: return new ILExpression(GMCode.Constant, (long)obj);
                case GM_Type.Int: return new ILExpression(GMCode.Constant, (int)obj);
                case GM_Type.String: return new ILExpression(GMCode.Constant, (string)obj);
                case GM_Type.Short: return new ILExpression(GMCode.Constant, (int)obj);
                default:
                    throw new Exception("Cannot convert simple type");
            }
        }
        /// <summary>
        /// This just removes pushes and gets the expresion from it
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        bool ExpressionIsSimple(ILExpression expr)
        {
            return expr.Code == GMCode.Call || expr.Code == GMCode.Constant || expr.Code == GMCode.Var ||
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
        ILExpression InstanceToExpression(int instance)
        {
            if(instance < 0)
            {
                string instanceName;
                if (GMCodeUtil.instanceLookup.TryGetValue(instance, out instanceName))
                    return new ILExpression(GMCode.Constant, instanceName);
                
            } else if(InstanceList != null && instance>0 && instance < InstanceList.Count)
            {
                return new ILExpression(GMCode.Constant, InstanceList[instance]);
            }
            // fallback
            return new ILExpression(GMCode.Constant, instance);  
        }
        ILExpression InstanceToExpression(ILExpression instance)
        {
            switch (instance.Code)
            {
                case GMCode.Constant:
                    if (instance.Operand is int)
                    {
                        ILExpression ret = InstanceToExpression((int)instance.Operand);
                        ret.ILRanges = instance.ILRanges;
                        instance = ret;
                    }
                    break;
                case GMCode.Push: // it was a push, pull the arg out and try it
                    return InstanceToExpression(instance.Arguments.Single());
                case GMCode.Var:
                    break; // if its a var like global.var.something = then just pass it though
                case GMCode.Pop:
                    break; // this is filler in to be filled in latter?  yea
                default:
                    throw new Exception("Something went wrong?");
            }
            return instance;// eveything else we just return as we cannot simplify it
        }
        ILExpression BuildVar(int operand, int extra, List<ILNode> nodes)
        {
            ILExpression v = new ILExpression(GMCode.Var, StringList[operand & 0x1FFFFF]);// standard for eveyone
           
            // check if its simple
            if (extra != 0) // its not on the stack, so its not an array and we have the instance so resolve the name, simple
            {
                v.Arguments.Add(InstanceToExpression(extra));
            }
            else // its ON the stack so all we know is if its an array or if is
            {
                ILExpression instance;
                if (operand >= 0) // is array
                {
                    ILExpression index;
                    if (NodeIsSimple(nodes.Last(), out index) && NodeIsSimple(nodes.ElementAt(nodes.Count-2), out instance))
                    {
                        v.Arguments.Add(InstanceToExpression(instance)); // instance first
                        v.Arguments.Add(index); // then index
                        nodes.RemoveLast(2);
                    }
                }
                else
                {
                    if (NodeIsSimple(nodes.Last(), out instance))
                    {
                        v.Arguments.Add(InstanceToExpression(instance)); // instance
                        nodes.RemoveLast();
                    }
                }
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
                if (i.Label != null) nodes.Add(ConvertLabel(i.Label));
                ILExpression expr = null;
                switch (code)
                {
                    case GMCode.Conv:
                        continue; // ignore all Conv for now
                    case GMCode.Call:
                        
                        expr = new ILExpression(GMCode.Call, operand as string); //   TryResolveCall(operand as string, extra, nodes);
                      //  Debug.Assert(extra != 3);
                        expr = TryResolveSimpleExpresions(extra, expr, nodes);
                 //       Debug.Assert("instance_create" != (operand as string));
                        // HACK TODO: Ok, so I screwed up on poping the expresions, because of this there
                        // needs to be a refactor on the optimize code that deals with conditions.  Ugh.
                         if (expr.Arguments[0].Arguments.Count > 1) expr.Arguments[0].Arguments = expr.Arguments[0].Arguments.Reverse().ToList();
                        break;
                    case GMCode.Popz:
                        {
                            ILExpression push;
                            if (nodes.Last().Match(GMCode.Push, out push) && push.Code == GMCode.Call)
                            {
                                nodes[nodes.Count - 1] = push;  // its not a push anymore as it was popped void return
                                continue;
                            }
                            else expr = new ILExpression(code, null);
                        }
                        break;
                    case GMCode.Pop: // var define, so lets define it
                        expr = BuildVar((int)operand, extra,nodes);  // try to figure out the var
                        expr = new ILExpression(GMCode.Assign, null, expr); // change it to an assign
                        {
                            ILExpression push; // see if we can get the value
                            if (NodeIsSimple(nodes.Last(), out push))
                            {
                                nodes.RemoveLast();
                                expr.Arguments.Add(push);
                            }
                        }
                        break;
                    case GMCode.Push:
                        if (i.Types[0] != GM_Type.Var)
                            expr = new ILExpression(GMCode.Push, null, OperandToExpresson(operand, i.Types[0]));// simple constant 
                        else 
                            expr = new ILExpression(GMCode.Push, null, BuildVar((int)operand, extra, nodes));  // try to figure out the var);
                        break;
                    case GMCode.Pushenv: // the asembler converted the positions to labels at the end of the push/pop enviroments
                        {
                            expr = new ILExpression(GMCode.Pushenv, ConvertLabel(i.Operand as Label));
                            ILExpression push;
                            if (NodeIsSimple(nodes.ElementAt(nodes.Count-2), out push))
                            {
                                nodes.RemoveAt(nodes.Count - 2);
                                expr.Arguments.Add(InstanceToExpression(push));
                            } 
                        }
                        break;
                    case GMCode.Popenv:
                        // Since the disasseembler turned the popenv labels to the end of the pushenviroment, lets make them
                        // branches so it makes the with detection easier

                        //expr = new ILExpression(GMCode.Popenv, ConvertLabel(i.Operand as Label));
                        expr = new ILExpression(GMCode.B, ConvertLabel(i.Operand as Label));
                        break;
                    case GMCode.B:
                        expr = new ILExpression(GMCode.B, ConvertLabel(i.Operand as Label));
                        break;
                    case GMCode.Bt:
                    case GMCode.Bf: // we could try converting all Bf to Bt here, but Bt's seem to only be used in special shorts or switch/case, so save that info here
                        {
                            expr = new ILExpression(code, ConvertLabel(i.Operand as Label));
                            ILExpression push;
                            if (NodeIsSimple(nodes.Last(), out push))
                                nodes.RemoveAt(nodes.Count - 1);
                            else // We must have a fake push so the patern matching works
                                push = new ILExpression(GMCode.Pop, null);
                            expr.Arguments.Add(push);
                            //  Debug.Assert(code == GMCode.Bt || expr.Arguments.Count == 1);
                        }
                        break;
                    case GMCode.Dup:
                        expr = new ILExpression(code, extra); // save the extra value for dups incase its dup eveything or just one
                        break;
                    case GMCode.Exit:
                        expr = new ILExpression(code, null);
                        break;
                    default:
                        {
                            expr = new ILExpression(code, null);
                            int popDelta = code.GetPopDelta();
                            expr = TryResolveSimpleExpresions(popDelta, expr, nodes);
                        }
                        break;
                }
                expr.ILRanges.Add(new ILRange(i.Address, i.Address));
                nodes.Add(expr);
            }
            return nodes;
        }
        // try to match a patern that goes with a switch and change it to a switch Expression
        // detect the size of the case going backwards, if there isn't a case there return -1;
        int FindEndOfSwitch(List<ILNode> ast, ILLabel fallOutLabel)
        {
            ILLabel test;
            for (int i = 0; i < ast.Count; i++)
            {
                if (ast[i].Match(GMCode.Bt) && ast[i + 1].Match(GMCode.B, out test) && (test == fallOutLabel)) return i + 1;
            }
            return -1;
        }
        // trying to do this here instead of the Optimize portion
        bool MatchPushConstant(List<ILNode> nodes, int start, out ILExpression expr)
        {
            do
            {
                ILExpression e = nodes.ElementAtOrDefault(start) as ILExpression;
                if (e == null) break;
                if (e.Code == GMCode.Call) expr = e;
                else if (e.Code == GMCode.Push) expr = e.Arguments[0];
                else break;
                return true;
            } while (false);
            expr = default(ILExpression);
            return false;
        }

        bool SimplifyExpression(int start, List<ILNode> nodes)
        {
            do
            {
                ILExpression expr;
                if (!nodes[start].Match(GMCode.Push, out expr) || !expr.Code.isExpression()) break;
                int popDelta = expr.Code.GetPopDelta();
                if (popDelta == 1)
                {
                    if (expr.Arguments.Count > 0) break; // already resolved
                    ILExpression arg1 = null;
                    if (MatchPushConstant(nodes, start - 1, out arg1))
                    {
                        expr.Arguments.Clear();
                        expr.Arguments.Add(arg1);
                        nodes.RemoveAt(start - 1);
                    }
                    else break; // couldn't match
                }
                else if (popDelta == 2)
                {
                    if (expr.Arguments.Count > 0) break; // already resolved

                    ILExpression arg1 = null;
                    ILExpression arg2 = null;
                    if (MatchPushConstant(nodes, start - 1, out arg2) && MatchPushConstant(nodes, start - 2, out arg1))
                    {
                        expr.Arguments.Clear();
                        expr.Arguments.Add(arg1);
                        expr.Arguments.Add(arg2);
                        nodes.RemoveRange(start - 2, 2);
                    }
                    else break; // couldn't match
                }
                return true;
            } while (false);
            return false;
        }
      
        public bool MatchDupPatern(int start, List<ILNode> nodes)
        {
            /* Pattern is
                Push instance
                Push arrayIndex
                Dup 1 // I THINK this copys the entire stack
                %POP%.msg[%POP%] = %POP%.msg[%POP%] + something
                I beleve this can be rolled into the assignAdd code
            */
            do
            {
                int index = start;
                IList<ILExpression> args;
                if (!nodes[index--].Match(GMCode.Assign, out args)) break; // try to match a assign first
                int dupCount;
                if (!nodes.ElementAtOrDefault(index--).Match(GMCode.Dup, out dupCount)) break;
                Debug.Assert(dupCount == 0 || dupCount == 1); // only seen these two
                ILExpression instance = null;
                ILExpression arrayIndex = null;

                if (!nodes.ElementAtOrDefault(index).Match(GMCode.Push, out instance)) break; // we need this push
                if (dupCount == 1)
                {
                    arrayIndex = instance; // first push was index
                    if (!nodes.ElementAtOrDefault(--index).Match(GMCode.Push, out instance)) break; // we need this push for index
                }
                Debug.Assert(dupCount == 1);
                instance = InstanceToExpression(instance); // try to resolve the instance
                // We got all we needed, lets check the assignment
                Debug.Assert(args[0].Code == GMCode.Var); // sanity check
                // Need to make copies so the parrents are all happy
                args[0].Arguments.Add(new ILExpression(instance));
                if (arrayIndex != null) args[0].Arguments.Add(new ILExpression(arrayIndex));
                // now the left hand of the expresson for assgment
                args[1].Arguments[0].Arguments.Add(new ILExpression(instance));
                if (arrayIndex != null) args[1].Arguments[0].Arguments.Add(new ILExpression(arrayIndex));
                // DONE! lets clean up being sure not to remove the assign we just modified
                nodes.RemoveRange(index, start - index);
                return true;
            } while (false);
            return false;
        }
        // match all function calls that ignore the return and remove the popz
        // also, to save on looping we also throw if there are any dup's left
        // this much be run at the end
        public bool FixPopZandCheckDUP(int start, List<ILNode> nodes)
        {
            do
            {
               // if (nodes[start].Match(GMCode.Dup) &&) throw new Exception("We Missed a Dup");
                if (!nodes[start].Match(GMCode.Call) 
                     || !nodes.ElementAtOrDefault(start + 1).Match(GMCode.Popz)) break;
                nodes.RemoveAt(start+1); // remove it
                return true;
            } while (false);
            return false;
        }
        // We try to resolve simple push enviroments here
        public bool SimplfyPushEnviroments(int start, List<ILNode> nodes)
        {
            /* A simple push envorment is
                // pushenv object
                // single statement, var assign or call
                // pop L393
                // L393:
                We don't want to remove the label as it might be used by other things
                This will simplify graph making as the only time the graph will care
                Is when the pop enviroment breaks
                Be sure to run all the optimziers first BEFORE this or put this in a big loop
                till eveything is fixed and optimized
            */
            do
            {
                ILExpression pushEnv = nodes[start] as ILExpression; // make sure its resolved and a push
                if (pushEnv == null || pushEnv.Code != GMCode.Pushenv || pushEnv.Arguments[0].Code == GMCode.Pop) break;
                ILExpression popEnv = nodes.ElementAtOrDefault(start+2) as ILExpression;
                ILBlock block = new ILBlock();
                int index = start+1;
                bool nope = false;
                while ((popEnv = nodes.ElementAtOrDefault(index++) as ILExpression) != null) {
                    Debug.Assert(popEnv.Code != GMCode.Pushenv); // ugh, this will be annoying if I run into it
                    if (popEnv.Code == GMCode.Popenv) break;
                    else if(popEnv.IsBranch()) { nope = true; break; }
                    else block.Body.Add(popEnv);
                }
                if (popEnv == null || nope) break; // There are labels and/or ifstatements in here
                Debug.Assert((popEnv.Operand as ILLabel) == (pushEnv.Operand as ILLabel)); // they should exit the same
                nodes[start] = new ILWithStatement() { Enviroment = pushEnv.Arguments[0], Body = block };
                int count = index - start - 1;
                nodes.RemoveRange(start + 1, count);
                return true; 
                // we will want to remove the extra label statements that arn't used as well
                // but that princess is in another castle

            } while (false);
            return false;
        }
        public void DoPattern<T>(List<T> nodes, Func<int, List<T>, bool> pred) where T : ILNode
        {
            bool modified;
            do
            {
                modified = false;
                for (int i = 0; i < nodes.Count; i++) modified |= pred(i, nodes);
            } while (modified);
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
        public List<ILNode> Build(SortedList<int, Instruction> code, bool optimize, List<string> StringList, List<string> InstanceList = null) //DecompilerContext context)
        {
            if (code.Count == 0) return new List<ILNode>();
            //variables = new Dictionary<string, VariableDefinition>();
            this.InstanceList = InstanceList;
            this.StringList = StringList;
            _method = code;
            this.optimize = optimize;
            List<ILNode> ast = BuildPreAst();
            
         //   DoPattern(ast, FixPopZandCheckDUP);
            DoPattern(ast, MatchDupPatern);
            // DoPattern(ast, SimplfyPushEnviroments);
            ILBlock method = new ILBlock();
            method.Body = ast;
            betteribttest.Dissasembler.Optimize.RemoveRedundantCode(method);
            foreach(var block in method.GetSelfAndChildrenRecursive<ILBlock>())
                Optimize.SplitToBasicBlocks(block);
            method.Body.DebugPrintILAst("basic_blocks.txt");
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                bool modified;
                do
                {
                    modified = false;
                    //    modified |= block.RunOptimization(new SimpleControlFlow(method).SimplifyShortCircuit);
                    modified |= block.RunOptimization(new SimpleControlFlow(method).SwitchDetection);
                    modified |= block.RunOptimization(new SimpleControlFlow(method).FixAndShort);
                    modified |= block.RunOptimization(new SimpleControlFlow(method).FixOrShort);
                    modified |= block.RunOptimization(new SimpleControlFlow(method).MakeSimplePushEnviroments); 
                    modified |= block.RunOptimization(new SimpleControlFlow(method).JoinBasicBlocks);

                    //  modified |= block.RunOptimization(SimplifyLogicNot);
                    //  modified |= block.RunOptimization(MakeAssignmentExpression);
                } while (modified);
            }
          
            method.Body.DebugPrintILAst("before_loop.txt");
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions().FindLoops(block);
            }
            method.Body.DebugPrintILAst("before_conditions.txt");
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions().FindConditions(block);
            }

            FlattenBasicBlocks(method);
 
            Optimize.RemoveRedundantCode(method);
            new GotoRemoval().RemoveGotos(method);
            Optimize.RemoveRedundantCode(method);
            //List<ByteCode> body = StackAnalysis(method);
            method.Body.DebugPrintILAst("bytecode_test.txt");

            // We don't have a fancy


            return ast;

        }
    }
}