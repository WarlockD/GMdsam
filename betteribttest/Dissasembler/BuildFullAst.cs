using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker.Dissasembler
{
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
            ILVariable v = new ILVariable() { Name = name + "_" + tempVarIndex++, InstanceName = "self", isArray = false, isLocal = true, isResolved = true };
            return v;
        }
        void ProcessVar(ILVariable v, Stack<ILNode> stack)
        {
            if (v.isResolved) return; // nothing to do
            ILValue value = v.Instance as ILValue;
            Debug.Assert(value.Value is int); // means the value is an int
            int instance = (int)value;
            Debug.Assert(instance == 0);  // it should be a stack value at this point
            if (v.isArray) v.Index = stack.Pop();  // get the index first
            v.Instance = stack.Pop(); // get the instance
            value = v.Instance as ILValue;
            if (value != null && value.Value is int)
            {
                v.InstanceName = context.InstanceToString((int)value);
            }
            else v.InstanceName = null;
            v.isResolved = true;
        }
        public ILExpression NodeToExpresion(ILNode n)
        {
            if (n is ILValue) return new ILExpression(GMCode.Constant, n as ILValue);
            else if (n is ILVariable) return new ILExpression(GMCode.Var, n as ILVariable);

            else if (n is ILExpression) return n as ILExpression;
            else if (n is ILCall) return new ILExpression(GMCode.Call, n);
            else throw new Exception("Should not happen here");
        }
        void CheckList(List<ILNode> node)
        {
            for (int i = 0; i < node.Count; i++)
            {
                ILExpression e = node[i] as ILExpression;
                if (e == null) continue;
                switch (e.Code)
                {
                    case GMCode.Pop:
                        Debug.Assert(false);
                        break;
                    case GMCode.Push:
                        Debug.Assert(i == (node.Count - 2) && node.LastOrDefault().Match(GMCode.B));
                        break;

                }
                // all variables MUST be resolved, no exception
                var list = e.GetSelfAndChildrenRecursive<ILVariable>(x => !x.isResolved).ToList();
                Debug.Assert(list.Count == 0);
            }
        }

        enum Status
        {
            DetectedPosableCase,
            NoChangeAdded,
            ChangedAdded,
            AddedToStack,
            DupStack0,
            DupStack1
        }
        bool Dup1Seen = false;
        Status ProcessExpression(List<ILNode> list, ILBasicBlock head, int pos, Stack<ILNode> stack)
        {
            ILExpression e = head.Body[pos] as ILExpression;
            if (e == null)
            {
                list.Add(head.Body[pos]);
                return Status.NoChangeAdded;
            }
            ILValue tempValue;
            ILVariable tempVar;
            switch (e.Code)
            {
                case GMCode.Conv:
                    // expr.InferredType
                    break;
                case GMCode.Push:
                    tempValue = e.Operand as ILValue;
                    tempVar = e.Operand as ILVariable;
                    if (tempValue != null) stack.Push(tempValue);
                    else if (tempVar != null)
                    {
                        ProcessVar(tempVar, stack);
                        stack.Push(tempVar);
                    }
                    else
                    { // block comes back as a push expression
                        Debug.Assert(e.Arguments.Count > 0);
                        stack.Push(e.Arguments[0]);
                    }
                    return Status.AddedToStack;
                case GMCode.Pop: // convert to an assign
                    {
                        tempVar = e.Operand as ILVariable;
                        Debug.Assert(tempVar != null);
                        ILExpression expr = null;
                        if (Dup1Seen) expr = NodeToExpresion(stack.Pop()); // have to swap the assignment if a dup 1 was done
                        ProcessVar(tempVar, stack);
                        e.Code = GMCode.Assign;
                        e.Operand = null;
                        e.Arguments.Add(NodeToExpresion(tempVar));
                        if (!Dup1Seen) expr = NodeToExpresion(stack.Pop());
                        e.Arguments.Add(expr);


                        list.Add(e);
                        return Status.ChangedAdded;
                    }
                case GMCode.Call:
                    {
                        if (e.Extra == -1)
                        {
                            list.Add(e);
                            return Status.NoChangeAdded;
                        } // its already resolved
                          //ILExpression call = new ILExpression(GMCode.Call, )
                          //ILCall call = new ILCall() { Name = e.Operand as string };
                          //for (int j = 0; j < e.Extra; j++) call.Arguments.Add(stack.Pop());
                        if (e.Extra > 0)
                            for (int j = 0; j < e.Extra; j++) e.Arguments.Add(NodeToExpresion(stack.Pop()));
                        e.Extra = -1;
                        stack.Push(e);
                    }
                    return Status.AddedToStack;
                case GMCode.Popz:
                    {
                        if (stack.Count == 0)
                        {
                            throw new Exception("We NEED to have a stack");
                            list.Add(e);
                            //  Debug.Assert(false);
                            return Status.ChangedAdded;
                        }
                        else
                        {
                            ILExpression call = stack.Peek() as ILExpression;
                            if (call != null)
                            {
                                stack.Pop();
                                if (call.Code == GMCode.Call) list.Add(call); // we want to show the void call
                                // otherwise lets just drop the stack

                                
                               // throw new Exception("Ugh wierd optimize stuff");
                                return Status.ChangedAdded;
                            }
                            else throw new Exception("popz on a non call?"); // return e; // else, ignore for switch
                        }
                    }
                case GMCode.Bf:
                case GMCode.Bt:
                case GMCode.Ret:
                    if (stack.Count > 0)
                    {
                        // Debug.Assert(stack.Count == 1);
                        e.Arguments[0] = NodeToExpresion(stack.Pop());
                        if (stack.Count > 0)
                        list.Add(e);
                        return Status.ChangedAdded;
                    }
                    else
                    {
                        list.Add(e);
                        return Status.NoChangeAdded;
                    }
                case GMCode.B:
                case GMCode.Exit:
                    list.Add(e);
                    return Status.ChangedAdded;
                case GMCode.Dup:
                    if ((int)e.Operand == 0)
                    {
                        ILExpression top = stack.Peek() as ILExpression;

                        if (top != null) // hack on calls, ugh
                        { // so, if its a call, we have to change it to a temp variable
                            top = new ILExpression(top); // copy it ugh
                            stack.Push(top); // simple case
                        }
                        else stack.Push(stack.Peek());


                        Dup1Seen = true; // usally this is on an assignment += -= of an array or such
                        return Status.DupStack0;
                    }
                    else
                    {
                        // this is usally on an expression that uses a var multipual times so the instance and index is copyed
                        // HOWEVER the stack may need to be swaped
                        foreach (var n in stack.Reverse().ToArray()) stack.Push(n); // copy the entire stack
                        Dup1Seen = true; // usally this is on an assignment += -= of an array or such
                        return Status.DupStack1;
                    }

                default: // ok we handle an expression
                    if (e.Code.isExpression())
                    {

                        for (int j = 0; j < e.Code.GetPopDelta(); j++)
                        {

                            if (stack.Count > 0)
                                e.Arguments.Add(NodeToExpresion(stack.Pop()));
                            else
                                e.Arguments.Add(new ILExpression(GMCode.Pop, null));
                        }
                        e.Arguments.Reverse(); // till I fix it latter, sigh
                        stack.Push(e); // push expressions back
                        return Status.AddedToStack;
                    }
                    else
                    {
                        list.Add(e);
                        return Status.NoChangeAdded;
                    }
            }
            throw new Exception("Shouldn't get here?");
        }

        // we cannot use RunOptimize as we need to go from
        // 1-count AND have to clear the case detecton on each block
        public void ProcessAllExpressions(ILBlock method)
        { // we want to run this in order
            HashSet<ILBasicBlock> unresolvedBlocks = new HashSet<ILBasicBlock>(labelToBasicBlock.Values);
            HashSet<ILBasicBlock> solvedBlocks = new HashSet<ILBasicBlock>();
            /*
            foreach (ILBasicBlock bb in method.GetSelfAndChildrenRecursive<ILBasicBlock>())
            {
                foreach (ILLabel label in bb.GetChildren().OfType<ILLabel>())
                {
                    labelToBasicBlock[label] = bb;
                }
            }
            */
            {
                ILBasicBlock bb = method.Body[0] as ILBasicBlock;
                labelToStack[bb.Body.First() as ILLabel] = new Stack<ILNode>(); // seed it
                unresolvedBlocks.Remove(bb);
                solvedBlocks.Add(bb);
                StackAnalysis(bb);
            }
            do
            {
                foreach (var bb in unresolvedBlocks)
                    if (StackAnalysis(bb)) solvedBlocks.Add(bb);
                unresolvedBlocks.ExceptWith(solvedBlocks);
            } while (unresolvedBlocks.Count>0); // fill all the stack data up
            
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
            return new ILVariable() { Name = "gen_" + generated_var_count++, Instance = new ILValue(-1), InstanceName = "self", isResolved = true };
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

                head.MatchAt(headLen-3, GMCode.Sle) &&
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
               
                newLoopBlock.Body.Add(new ILExpression(GMCode.Sgt,null));
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
        void LuaConvertSwitchExpression(ILNode condition, List<ILNode> list,  List<ILNode> body, ILBasicBlock head, int pos)
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
        bool StackAnalysis(ILBasicBlock head)
        {
            Stack<ILNode> stack;
            ILLabel label = head.Body.First() as ILLabel;
            if (labelToStack.TryGetValue(label, out stack) && stack !=null) {
                List<ILNode> list = new List<ILNode>();
                for (int i = 0; i < head.Body.Count; i++)
                {
                    Status s = ProcessExpression(list, head, i, stack);
                }
                ILExpression br = list[list.Count - 2] as ILExpression; // might not have a goto
                ILExpression b = list[list.Count - 1] as ILExpression; // this is ALWAYS a goto
                label = br != null ? br.Operand as ILLabel : null;
                if (label != null) labelToStack[label] = new Stack<ILNode>(stack);
                label = b != null ? b.Operand as ILLabel : null;
                if (label != null) labelToStack[label] = new Stack<ILNode>(stack);
                head.Body = list;
                return true;
            }
            return false;

        }
        void ProcessExpressions(List<ILNode> body, ILBasicBlock head, int pos)
        {
            ProcessAllExpressions(null);
            Stack<ILNode> stack = new Stack<ILNode>();
            List<ILNode> list = new List<ILNode>();
            Dup1Seen = false;
            //  TestAndFixWierdLoop(body, head, pos);
            // fuck it, doing full stack block stuff, converting pushses to vars, etc
            // I can't stand having one or two fail cases when I want the whole pie damnit
            // skipping switch building as lua dosn't do tha
            // and you know, I can check for it latter
            ILLabel label = head.Body.First() as ILLabel;
            stack = labelToStack[label];

            ILExpression br = list[list.Count - 2] as ILExpression; // might not have a goto
            ILExpression b = list[list.Count - 1] as ILExpression; // this is ALWAYS a goto

            head.Body = list;
        }
    }
}
