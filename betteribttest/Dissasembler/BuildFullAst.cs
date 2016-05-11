using GameMaker.FlowAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker.Dissasembler
{
    public static class StackExtensions
    {
        public static ILNode[] Backup(this Stack<ILNode> stack)
        {
            if (stack.Count == 0) return null;
            ILNode[] backup = new ILNode[stack.Count];
            stack.CopyTo(backup, 0);
            return backup;
        }
        public static void Restore(this Stack<ILNode> stack, ILNode[] nodes)
        {
            stack.Clear();
            if(nodes!=null) foreach (var n in nodes.Reverse()) stack.Push(n);
        }
    }
    class BuildFullAst
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();
        // no way around this, need to have stacks for eveything
        Dictionary<ILLabel, Stack<ILNode>> labelToStack = new Dictionary<ILLabel, Stack<ILNode>>();

        GMContext context;
        ILBlock method;
        //  TypeSystem typeSystem;

        public BuildFullAst(ILBlock method, GMContext context)
        {
            this.context = context;
            this.method = method;
            //  this.typeSystem = context.CurrentMethod.Module.TypeSystem;

            foreach (ILLabel target in method.GetSelfAndChildrenRecursive<ILExpression>(e => e.IsBranch()).SelectMany(e => e.GetBranchTargets()))
            {
                labelGlobalRefCount[target] = labelGlobalRefCount.GetOrDefault(target) + 1;
                labelToStack[target] = null; // make sure we got a default atleast
            }
            foreach (ILBasicBlock bb in method.GetSelfAndChildrenRecursive<ILBasicBlock>())
            {
                foreach (ILLabel label in bb.GetChildren().OfType<ILLabel>())
                {
                    labelToBasicBlock[label] = bb;
                }
            }
        }
        Dictionary<ILLabel, ILBasicBlock> labelToNextBlock;

        void BuildNextBlockData()
        {
            labelToNextBlock = new Dictionary<ILLabel, ILBasicBlock>();
            for (int i = 0; i < method.Body.Count - 1; i++)
            {
                ILBasicBlock bb = method.Body[i] as ILBasicBlock;
                ILBasicBlock next = method.Body[i + 1] as ILBasicBlock;
                ILLabel label = (bb.Body.First() as ILLabel);
                labelToNextBlock[label] = next;
            }
        }
        int tempVarIndex = 0;
        ILVariable tempVariable(string name)
        {
            ILVariable v = new ILVariable() { Name = name + "_" + tempVarIndex++, InstanceName = "self", isArray = false, isLocal = true, isResolved = true, isGenerated = true };
            return v;
        }
        bool ProcessVar(ILVariable v, Stack<ILNode> stack)
        {

            if (v.isResolved) return true; // nothing to do
            ILValue value = v.Instance as ILValue;
            Debug.Assert(value.Value is int); // means the value is an int
            int instance = (int) value;
            Debug.Assert(instance == 0);  // it should be a stack value at this point
            if (stack.Count < 1 || (v.isArray && stack.Count < 2)) return false;
            if (v.isArray) v.Index = stack.Pop();  // get the index first
            v.Instance = stack.Pop(); // get the instance
            value = v.Instance as ILValue;
            if (value != null && value.Value is int)
            {
                v.InstanceName = context.InstanceToString((int) value);
            }
            else v.InstanceName = null;
            v.isResolved = true;
            return true;
        }
        public ILExpression NodeToExpresion(ILNode n)
        {
            if (n is ILValue) return new ILExpression(GMCode.Constant, n as ILValue);
            else if (n is ILVariable) return new ILExpression(GMCode.Var, n as ILVariable);

            else if (n is ILExpression) return new ILExpression(n as ILExpression);
            else if (n is ILCall) return new ILExpression(GMCode.Call, n);
            else throw new Exception("Should not happen here");
        }

        enum Status
        {
            Resolved,
            NonExpression,
            EmptyStack,
            StackHasData,
            BlockNeedsStack
        }
        bool DupSeen = false;
        List<ILNode> ProcessBlock(ILBasicBlock head, Stack<ILNode> stack)
        {
            List<ILNode> list = new List<ILNode>();
            for (int i = 0; i < head.Body.Count; i++)
            {
                ProcessExpression(list, head, i, stack);
            }




            return list;
        }
        class BasicBlockInfo
        {
            public ILBasicBlock bb;
            public Stack<ILNode> EndStack;
            public bool DupSeen; // the stack may of been a result of a dup
        }
        void DebugBasicBlockInfo(ITextOutput output, IEnumerable<BasicBlockInfo> e)
        {
            foreach (var binfo in e)
            {
                output.WriteLine("===Block");
                output.Indent();
                foreach(var n in binfo.bb.Body)
                {
                    ILExpression expr = n as ILExpression;
                    if (expr != null) expr.DebugWriteTo(output);
                    else n.WriteTo(output);
                    output.WriteLine();


                }
                output.Unindent();
                if (binfo.EndStack.Count == 0)
                    output.WriteLine("===EmptyStack");
                else
                {
                    output.WriteLine("===Stack");
                    output.Indent();
                    var a = binfo.EndStack.ToArray();
                    for (int i = 0; i < a.Length; i++)
                    {
                        output.Write("{0}: ");

                        a[i].WriteToLua(output);
                        output.WriteLine();
                    }
                    output.Unindent();
                }
                output.WriteLine();
            }
        }

        void PreprocessBlocks(List<BasicBlockInfo> needStackData, List<BasicBlockInfo> hasStackData)
        {
            foreach (var bb in method.GetSelfAndChildrenRecursive<ILBasicBlock>())
            {
                List<ILNode> list = new List<ILNode>();
                Stack<ILNode> stack = new Stack<ILNode>();
                bool exprAdded = true;
                int pos = 0;
                bool dupInline = false;
                //Debug.Assert(bb.EntryLabel().Name != "L180");
                while (pos < bb.Body.Count && (exprAdded = ProcessExpression(list, bb, pos++, stack))) dupInline |= DupSeen;
                if (exprAdded)
                {
                    if (stack.Count > 0)
                    {
                        BasicBlockInfo binfo = new BasicBlockInfo() { bb = bb, DupSeen = dupInline, EndStack = stack };
                        hasStackData.Add(binfo);
                    }
                    bb.Body = list; // block didn't have any drops so do it 
                }
                else
                {   // we need a stack to do this block

                    BasicBlockInfo binfo = new BasicBlockInfo() { bb = bb, DupSeen = dupInline, EndStack = new Stack<ILNode>() };
                    needStackData.Add(binfo);
                }
            }
        }
        void DebugBlocks(List<BasicBlockInfo> needStackData, List<BasicBlockInfo> hasStackData, string filename)
        {
            using (var stream = context.MakeDebugStream(filename)) 
            {
                var output = new PlainTextOutput(stream);
                output.WriteLine("DebugName: " + context.DebugName);
                output.WriteLine("Needs Stack Data: {0}", needStackData.Count);
                DebugBasicBlockInfo(output, needStackData);
                output.WriteLine("Has Stack Data: {0}", hasStackData.Count);
                DebugBasicBlockInfo(output, hasStackData);
            }
             method.DebugSave(context.MakeDebugFileName("block_" + filename));
        }
        void ProcessBranchPushes(List<BasicBlockInfo> needStackData, List<BasicBlockInfo> hasStackData)
        {
            HashSet<ILLabel> ignoreNeeds = new HashSet<ILLabel>();
            List<BasicBlockInfo> toRemove = new List<BasicBlockInfo>();
            foreach (var bi in hasStackData)
            {
                ILBasicBlock bb = bi.bb;
                if (bb.Body.Count == 2 && bi.EndStack.Count == 1) // We only have a label, a goto and a push
                {
                    ignoreNeeds.Add(bb.GotoLabel());
                    bb.Body.Insert(1, new ILExpression(GMCode.Push, null, NodeToExpresion(bi.EndStack.Pop())));
                    toRemove.Add(bi); // process needstack and remove
                } 
            }
            hasStackData.RemoveOrThrow(toRemove);
            needStackData.RemoveAll(x => ignoreNeeds.Contains(x.bb.EntryLabel()));
        }

        void ResolveBlockData(List<BasicBlockInfo> needStackData, List<BasicBlockInfo> hasStackData)
        {
          
            LoopsAndConditions landc = new LoopsAndConditions(context);
            ControlFlowGraph graph = landc.BuildGraph(method.Body, method.EntryGoto.Operand as ILLabel);
            graph.ComputeDominance();
            graph.ComputeDominanceFrontier();
            var export = graph.ExportGraph();
            export.Save("test.dot");
            foreach (var bi in hasStackData)
            {
                HashSet<ControlFlowNode> scope = new HashSet<ControlFlowNode>(graph.Nodes.Skip(2));
                ControlFlowNode head = landc.LabelToNode(bi.bb.EntryLabel());
               // scope.Remove(head)
                var domnodes = LoopsAndConditions.FindDominatedNodes(scope, head);
                // taking a chance here.  Again, as I stated before, I now know why you convert all the pushes to temp gemerated variables
                // its becuase of optimization passes, if eveything is in one block, its ok, but once you break that block boundry, you don't know
                // where the hell it will be used.  So you have to track that fucker around the world.  Like we are doing now
                // only seems to happen in loops so thats good
                domnodes.Remove(head); // ... fuck it lets hack this bitch, fix it with asserts
                foreach (var sd in needStackData.ToList())
                {
                    var sn = domnodes.SingleOrDefault(x => x.UserData == sd.bb);
                    if (sn == null) continue;
                   
                    var inner = LoopsAndConditions.FindDominatedNodes(scope, sn);
                    Debug.Assert(!LoopsAndConditions.HasSingleEdgeEnteringBlock(sn));  // if it has more than one..ugh
                    // OK here comes the magic
                    List<ILNode> list = new List<ILNode>();
                    Stack<ILNode> stack = new Stack<ILNode>(bi.EndStack);
                    for(int i=0;i< sd.bb.Body.Count; i++)
                    {
                        
                        Debug.Assert(ProcessExpression(list, sd.bb, i, stack));
                    }
                    if (stack.Count>0) {
                      //  var viaBackEdges = head.Predecessors.Where(p => p.Dominates(sn));
                        //Debug.Assert(stack.Count == 0);
                        // ignore stacks now, mabye later put it back on the has stack data
                    }
                    sd.bb.Body = list;
                    needStackData.Remove(sd); // remove it
                }
            }
            hasStackData.Clear();
        }
        public void ProcessAllExpressions()
        {
            List<BasicBlockInfo> needStackData = new List<BasicBlockInfo>();
            List<BasicBlockInfo> hasStackData = new List<BasicBlockInfo>();
            PreprocessBlocks(needStackData, hasStackData);
            if (needStackData.Count == 0 && hasStackData.Count == 0) return; // no extra processing required
            if (context.Debug) DebugBlocks(needStackData, hasStackData, "pre_ast_block_info.txt");
            ProcessBranchPushes(needStackData, hasStackData);// First we resolve any nodes that will be filedered by the teetery/short code
            if (context.Debug) DebugBlocks(needStackData, hasStackData, "post_ast_block_info.txt");
            ResolveBlockData(needStackData, hasStackData);// First we resolve any nodes that will be filedered by the teetery/short code

            if (needStackData.Count == 0 && hasStackData.Count == 0) return; // no extra processing required
            if (context.Debug) DebugBlocks(needStackData, hasStackData, "error_ast_block_info.txt");
            Debug.Assert(false);
        }
        /// <summary>
        /// Process an expression in an ILBasicBlock
        /// </summary>
        /// <param name="list"></param>
        /// <param name="head"></param>
        /// <param name="pos"></param>
        /// <param name="stack"></param>
        /// <returns>true if added to list, false if bad stack and not added to list</returns>
        bool ProcessExpression(List<ILNode> list, ILBasicBlock head, int pos, Stack<ILNode> stack)
        {
            ILExpression e = head.Body[pos] as ILExpression;
            if (e == null)
            {
                list.Add(head.Body[pos]);
                return true; // label or other stuff
            }
            ILValue tempValue;
            ILVariable tempVar;
            switch (e.Code)
            {
                case GMCode.Push:
                    tempValue = e.Operand as ILValue;
                    tempVar = e.Operand as ILVariable;
                    if (tempValue != null) stack.Push(tempValue);
                    else if (tempVar != null)
                    {
                        if (!ProcessVar(tempVar, stack))
                            return false;// hard fault
                        stack.Push(tempVar);
                    }
                    else
                    { // block comes back as a push expression
                        Debug.Assert(e.Arguments.Count > 0);
                        stack.Push(e.Arguments[0]);
                    }
                    break;
                case GMCode.Pop: // convert to an assign
                    {
                        tempVar = e.Operand as ILVariable;
                        Debug.Assert(tempVar != null);
                        ILExpression expr = null;
                        if (stack.Count == 0)
                            return false; // hard fault, mabye we should throw here
                        if (DupSeen) expr = NodeToExpresion(stack.Pop()); // have to swap the assignment if a dup 1 was done
                        if (!ProcessVar(tempVar, stack))
                            return false;// hard fault
                        ILAssign assign = new ILAssign() { Variable = tempVar };
                     //   e.Code = GMCode.Assign;
                      //  e.Operand = null;
                      //  e.Arguments.Add(NodeToExpresion(tempVar));
                        if (!DupSeen) expr = NodeToExpresion(stack.Pop());
                        assign.Expression = expr;
                        //e.Arguments.Add(expr);


                        //list.Add(e);

                        list.Add(assign);

                    }
                    break;
                case GMCode.Call:
                    {
                        if (e.Extra == -1) // hack done in the dissasembler.  if there is a popz right after the call, its just a call with no return
                        {
                            list.Add(e);
                        }
                        else// its already resolved
                        {
                            if (e.Extra > 0)
                                for (int j = 0; j < e.Extra; j++) e.Arguments.Add(NodeToExpresion(stack.Pop()));
                            e.Extra = -1;
                            stack.Push(e);
                        }
                    }
                    break;
                case GMCode.Popz:
                    {
                        if (stack.Count == 0) return false;// hard fault
                        else
                        {
                            ILExpression call = stack.Peek() as ILExpression;
                            if (call != null)
                            {
                                stack.Pop();
                                if (call.Code == GMCode.Call) list.Add(call); // we want to show the void call
                                                                              // otherwise lets just drop the stack


                                // throw new Exception("Ugh wierd optimize stuff");
                            }
                            else
                            {
                                stack.Pop(); // juuust popit
                             //   throw new Exception("popz on a non call?"); // return e; // else, ignore for switch
                            }
                        }
                    }
                    break;
                case GMCode.Bf:
                case GMCode.Bt:
                case GMCode.Ret:
                    {
                        if (stack.Count == 0)
                            return false;
                        ILExpression expr = NodeToExpresion(stack.Pop());
                        expr.Arguments.Clear(); // clear out the pop for now
                        if (expr.Code.IsConditionalCode())
                        {
                            e.Arguments.Add(expr);
                            // simple case
                        }
                        else if (expr.Code.isExpression())
                        { // something wierd
                            if ((expr.Arguments[0].Match(GMCode.Var, out tempVar) || expr.Arguments[1].Match(GMCode.Var, out tempVar)) &&
                                tempVar.isGenerated)
                            {
                                ILExpression ge = new ILExpression(GMCode.Assign, null, NodeToExpresion(tempVar), expr);
                                list.Add(ge); // add it
                                e.Arguments[0] = new ILExpression(GMCode.Sne, null, NodeToExpresion(tempVar), NodeToExpresion(new ILValue((short) 0)));
                            }

                        }
                        else if (expr.Code == GMCode.Call)
                        {
                            e.Arguments.Add(expr); // just goes here for calls

                        }
                        else throw new Exception("WAHH");
                       // e.Arguments[0] = new ILExpression(GMCode.Sne, null, expr, NodeToExpresion(new ILValue((short) 0)));
                    }
                    list.Add(e);
                    break;
                case GMCode.B:
                case GMCode.Exit:
                    list.Add(e);
                    break;
                case GMCode.Dup:
                    if ((int) e.Operand == 0)
                    {
                        ILExpression top = stack.Peek() as ILExpression;

                        if (top != null) // hack on calls, ugh
                        { // so, if its a call, we have to change it to a temp variable
                            if (top.Code == GMCode.Call)
                            {
                                var v = this.NewGeneratedVar();
                                ILExpression ge = new ILExpression(GMCode.Assign, null, NodeToExpresion(v), NodeToExpresion(stack.Pop()));
                                // generate an assign
                                list.Add(ge); // add it
                                stack.Push(v);
                                stack.Push(v); // push it twice for the dup
                            }
                            else
                            {
                                top = new ILExpression(top); // copy it ugh
                                stack.Push(top); // simple case
                            }
                        }
                        else stack.Push(stack.Peek());


                        DupSeen = true; // usally this is on an assignment += -= of an array or such
                                        //   return Status.DupStack0;
                    }
                    else
                    {
                        // this is usally on an expression that uses a var multipual times so the instance and index is copyed
                        // HOWEVER the stack may need to be swaped
                        foreach (var n in stack.Reverse().ToArray()) stack.Push(n); // copy the entire stack
                        DupSeen = true; // usally this is on an assignment += -= of an array or such
                                        //  return Status.DupStack1;
                    }
                    break;
                default: // ok we handle an expression
                    if (e.Code.isExpression())
                    {
                        int popDelta = e.Code.GetPopDelta();
                        if (stack.Count < popDelta)
                            return false; // bad stack
                        for (int j = 0; j < e.Code.GetPopDelta(); j++)
                        {
                            e.Arguments.Add(NodeToExpresion(stack.Pop()));
                        }
                        e.Arguments.Reverse(); // till I fix it latter, sigh

                            stack.Push(e); // push expressions back
                    }
                    else
                    {
                        list.Add(e);
                        Debug.WriteLine("Wierd Expression added?");
                    }
                    break;
            }
            return true;
        }


        void CreateSwitchExpresion(ILNode condition, List<ILNode> list, List<ILNode> body, ILBasicBlock head, int pos)
        {
            // we are in a case block, check if its the first block

            List<ILBasicBlock> caseBlocks = new List<ILBasicBlock>();
            List<ILLabel> caseLabels = new List<ILLabel>();
            ILExpression switchExpression = new ILExpression(GMCode.Switch, null);
            switchExpression.Arguments.Add(NodeToExpresion(condition)); // pop the switch condition
            ILBasicBlock current = head;
            ILLabel nextCase;
            ILLabel caseTrue;
            ILExpression fakeArgument;
            while (current.MatchLastAndBr(GMCode.Bt, out caseTrue, out fakeArgument, out nextCase))
            {
                ILNode operand;
                if (!current.MatchAt(current.Body.Count - 4, GMCode.Push, out operand)) throw new Exception("fix");
                if (!(operand is ILValue)) throw new Exception("Can only handle constants right now");
                switchExpression.Arguments.Add(new ILExpression(GMCode.Case, caseTrue, NodeToExpresion(operand)));
                caseLabels.Add(caseTrue);
                body.Remove(current);
                current = labelToBasicBlock[nextCase];
            }
            body.Insert(pos, head);

            var lastBlock = current;


            ILLabel fallLabel;
            if (!lastBlock.MatchSingle(GMCode.B, out fallLabel)) throw new Exception("fix");
            current = labelToBasicBlock[fallLabel];
            if (!current.MatchAt(1, GMCode.Popz))
            { // has a default case
              // Side note, the LoopAndConditions figures out if we have a default case by checking
              // if the ending branch is linked to all the other case branches
              // so we don't need to do anything here
              // All this code is JUST for finding that popz that pops the condition out of the switch
              // if you don't care about it, you could just search though all the expresions and remove any and all
              // popz's  I am beggining to think this is the right way to do it as I don't see any other uses
              // of popz's
              //  Debug.Assert(false);
                BuildNextBlockData(); // build block chain, we need this for default and mabye case lookups
                                      // ok, this is why we DON'T want to remove redundent code as there is this empty
                                      // useless goto RIGHt after this that has the link to the finale case
                ILLabel nextBlockLabel = lastBlock.Body.First() as ILLabel;
                var uselessBlock = this.labelToNextBlock[nextBlockLabel];
                ILLabel newFallLabel;
                if (!uselessBlock.MatchSingle(GMCode.B, out newFallLabel)) throw new Exception("fix");
                current = labelToBasicBlock[newFallLabel];

                if (!current.MatchAt(1, GMCode.Popz)) throw new Exception("I have no idea where the popz is for this switch");
            }
            current.Body.RemoveAt(1);
            ILLabel lastBlockLabel = lastBlock.Body.First() as ILLabel;
            if (this.labelGlobalRefCount[lastBlockLabel] == 1) body.Remove(lastBlockLabel);
            switchExpression.Operand = caseLabels.ToArray();

            list.Add(switchExpression);
            list.Add(new ILExpression(GMCode.B, fallLabel));
        }
        int generated_var_count = 0;
        int loop_junk_block = 0;
        ILVariable NewGeneratedVar()
        {
            return new ILVariable() { Name = "gen_" + generated_var_count++, Instance = new ILValue(-1), InstanceName = "self", isResolved = true, isGenerated = true };
        }
        ILLabel NewJunkLoop()
        {
            return new ILLabel() { Name = "gen_block_" + loop_junk_block++ };
        }
        void TestAndFixWierdLoop(List<ILNode> body, ILBasicBlock head, int pos)
        {
            object uvalue1;
            ILValue value2;
            ILLabel endLoop;
            ILLabel startLoop;
            ILExpression filler;
            //Debug.Assert((head.Body[0] as ILLabel).Name != "L36");
            // Wierd one here.  I ran accross this a few times and I think this is generated code
            // for events.  Basicly, it pushes a constant on the stack and uses a repeat loop
            // however since I am not doing ANY kind of real stack/temporary analysis.  I would have
            // to rewrite and add a bunch of code to get that to work and right now its only a few functions
            // Its easyer to build a while loop out of it and let the decompiler handle it rather than 
            // build a more robust stack anilizer
            // ugh have to make a new block for it too, meh
            int headLen = head.Body.Count;
            if (head.MatchAt(headLen - 6, GMCode.Push, out uvalue1) &&
                head.MatchAt(headLen - 5, GMCode.Dup) &&
                head.MatchAt(headLen - 4, GMCode.Push, out value2) &&

                head.MatchAt(headLen - 3, GMCode.Sle) &&
                head.MatchLastAndBr(GMCode.Bt, out endLoop, out filler, out startLoop))
            {
                // ok, lets rewrite the head so it makes sence
                ILLabel newLoopStart = NewJunkLoop();
                ILVariable genVar = NewGeneratedVar();
                List<ILNode> newHead = new List<ILNode>();
                for (int ii = 0; ii <= headLen - 7; ii++) newHead.Add(head.Body[ii]); // add the front of it including the sub part
                newHead.Add(new ILExpression(GMCode.Push, uvalue1));
                newHead.Add(new ILExpression(GMCode.Pop, genVar));
                newHead.Add(new ILExpression(GMCode.B, newLoopStart));
                ILBasicBlock newLoopBlock = new ILBasicBlock();
                newLoopBlock.Body.Add(newLoopStart);
                newLoopBlock.Body.Add(new ILExpression(GMCode.Push, genVar));
                newLoopBlock.Body.Add(new ILExpression(GMCode.Push, new ILValue(0)));

                newLoopBlock.Body.Add(new ILExpression(GMCode.Sgt, null));
                newLoopBlock.Body.Add(new ILExpression(GMCode.Bf, endLoop, new ILExpression(GMCode.Pop, null)));
                newLoopBlock.Body.Add(new ILExpression(GMCode.B, startLoop));
                head.Body = newHead;
                body.Add(newLoopBlock);
                // Now the hard part, we have to find the end bit
                for (int j = pos; j < body.Count; j++)
                {
                    ILBasicBlock bj = body[j] as ILBasicBlock;
                    ILLabel testEnd, testStart;
                    ILValue subPart;
                    int len = bj.Body.Count;
                    //    Debug.Assert((bj.Body[0] as ILLabel).Name != "L114");
                    if (bj.MatchLastAndBr(GMCode.Bt, out testStart, out filler, out testEnd) &&
                        testEnd == endLoop && testStart == startLoop &&
                        bj.MatchAt(len - 3, GMCode.Dup) &&
                        bj.MatchAt(len - 4, GMCode.Sub) &&
                        bj.MatchAt(len - 5, GMCode.Push, out subPart)
                        )
                    {
                        List<ILNode> list2 = new List<ILNode>();
                        for (int ii = 0; ii <= len - 6; ii++) list2.Add(bj.Body[ii]); // add the front of it including the sub part
                        list2.Add(new ILExpression(GMCode.Push, genVar));
                        list2.Add(bj.Body[len - 5]); // add the sub part
                        list2.Add(bj.Body[len - 4]); // add the sub part
                        list2.Add(new ILExpression(GMCode.Pop, genVar)); // assign it
                        list2.Add(new ILExpression(GMCode.B, newLoopStart)); // branch back to the start
                        bj.Body = list2; // replace
                        break; // all done, let it continue
                    }
                }
                //  Debug.Assert(false); // coudln't find the end block
            }
        }
        // lua dosn't have switch expressions, so we are changing this to one big if statement
        // it would proberly look beter if we made some kind of "ifelse" code, but right now
        // this is good enough
        void LuaConvertSwitchExpression(ILNode condition, List<ILNode> list, List<ILNode> body, ILBasicBlock head, int pos)
        {
            // we are in a case block, check if its the first block
            ILBasicBlock current = head;
            ILLabel nextCase;
            ILLabel caseTrue;
            ILExpression fakeArgument;
            while (current.MatchLastAndBr(GMCode.Bt, out caseTrue, out fakeArgument, out nextCase))
            {
                int len = current.Body.Count;
                ILNode operand;
                if (!current.MatchAt(len - 4, GMCode.Push, out operand)) throw new Exception("fix");
                if (!(operand is ILValue)) throw new Exception("Can only handle constants right now");
                // ok, lets replace the dup with the expression, change to a bf, then swap the labels
                // First do the labels as the indexes will screw up
                ILExpression convert = current.Body.Last() as ILExpression;
                convert.Operand = caseTrue;
                convert = current.Body[len - 2] as ILExpression;
                convert.Operand = nextCase;
                convert.Code = GMCode.Bf;
                convert = current.Body[len - 5] as ILExpression;
                if (current == head)
                    current.Body.RemoveAt(len - 5); // if its the current head, we don't need to change the dup
                else
                {
                    convert.Code = GMCode.Push;
                    convert.Operand = condition;
                }
                current = labelToBasicBlock[nextCase];
            }
            // eveything else should be cleared out by all the removals
        }
    }
}
