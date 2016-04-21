using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.Dissasembler
{
    class BuildFullAst
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();
        
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
            for(int i=0;i < method.Body.Count-1; i++)
            {
                ILBasicBlock bb = method.Body[i] as ILBasicBlock;
                ILBasicBlock next = method.Body[i+1] as ILBasicBlock;
                ILLabel label = (bb.Body.First() as ILLabel);
                labelToNextBlock[label] = next;
            }
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
            if(value != null && value.Value is int)
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
                case GMCode.Push:
                    tempValue = e.Operand as ILValue;
                    tempVar = e.Operand as ILVariable;
                    if (tempValue != null)stack.Push(tempValue);
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
                        if(Dup1Seen) expr=  NodeToExpresion(stack.Pop()); // have to swap the assignment if a dup 1 was done
                        ProcessVar(tempVar, stack);
                        e.Code = GMCode.Assign;
                        e.Operand = null;
                        e.Arguments.Add(NodeToExpresion(tempVar));
                        if(!Dup1Seen) expr = NodeToExpresion(stack.Pop());
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
                            list.Add(e);
                          //  Debug.Assert(false);
                            return Status.ChangedAdded;
                        }
                        else
                        {
                            ILExpression call = stack.Peek() as ILExpression;
                            if (call != null && call.Code == GMCode.Call)
                            {
                                stack.Pop();
                                list.Add(call);
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
                        if(stack.Count >0)
                        {
                            foreach (var n in stack) list.Add(new ILExpression(GMCode.Push, null, NodeToExpresion(n)));
                        }
                        list.Add(e);
                        return Status.ChangedAdded;
                    } else
                    {
                        list.Add(e);
                        return Status.NoChangeAdded;
                    }
                case GMCode.B:
                case GMCode.Exit:
                    if (stack.Count > 0)
                    {
                        foreach (var n in stack) list.Add(new ILExpression(GMCode.Push, null, NodeToExpresion(n)));
                    }
                    list.Add(e);
                    return Status.ChangedAdded;
                case GMCode.Dup:
                    if ((int)e.Operand == 0)
                    {
                        stack.Push(stack.Peek()); // simple case
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
                    } else
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
        {

            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                List<ILNode> body = block.Body;
                for (int i = 0; i < body.Count; i++)
                {
                    if (i < body.Count) ProcessExpressions(body, (ILBasicBlock)body[i], i);
                }
            }
        }

        void ProcessExpressions(List<ILNode> body, ILBasicBlock head, int pos)
        {
            Stack<ILNode> stack = new Stack<ILNode>();
            List<ILNode> list = new List<ILNode>();
            Dup1Seen = false;
            for (int i = 0; i < head.Body.Count; i++)
            {
                if (stack.Count == 1 &&
                    head.MatchAt(i, GMCode.Dup) &&
                    head.MatchAt(i + 1, GMCode.Push) &&
                    head.MatchAt(i + 2, GMCode.Seq) &&
                    head.MatchAt(i + 3, GMCode.Bt))
                {
                    // we are in a case block, check if its the first block

                    List<ILBasicBlock> caseBlocks = new List<ILBasicBlock>();
                    List<ILLabel> caseLabels = new List<ILLabel>();
                    ILExpression switchExpression = new ILExpression(GMCode.Switch, null);
                    switchExpression.Arguments.Add(NodeToExpresion(stack.Pop())); // pop the switch condition
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


                    break; // we are done with this block
                }
                else
                {
                    Status s = ProcessExpression(list, head, i, stack);

                }
            }
        
        
            head.Body = list;
        }
        public bool ProcessExpressions2(IList<ILNode> body, ILBasicBlock head, int pos)
        {

            ILLabel label = head.Body[0] as ILLabel;
            //      Debug.Assert(label.Name != "Block_0");
            //   Debug.Assert(label.Name != "L16");
            List<ILNode> list = new List<ILNode>(); // New body
            Stack<ILNode> stack = new Stack<ILNode>();
            ILValue tempValue;
            ILVariable tempVar;
            bool Dup1Seen = false;
            bool possableCase = false;
            int oldCount = head.Body.Count;
            for (int i = 0; i < oldCount; i++)
            {
                ILExpression e = head.Body[i] as ILExpression;
                ILNode nexpr = null;
                if (e == null)
                {
                    list.Add(head.Body[i]);
                    continue; // skip  labels or wierd stuff
                }
                switch (e.Code)
                {
                    case GMCode.Push:
                        tempValue = e.Operand as ILValue;
                        if (tempValue != null) { stack.Push(tempValue); break; }
                        tempVar = e.Operand as ILVariable;
                        if (tempVar != null)
                        {
                            ProcessVar(tempVar, stack);
                            stack.Push(tempVar);

                        }
                        else
                        { // block comes back as a push expression
                            Debug.Assert(e.Arguments.Count > 0);
                            stack.Push(e.Arguments[0]);
                        }
                        break;
                    case GMCode.Pop: // convert to an assign
                        tempVar = e.Operand as ILVariable;
                        Debug.Assert(tempVar != null);
                        ProcessVar(tempVar, stack);
                        e.Code = GMCode.Assign;
                        e.Operand = null;
                        e.Arguments.Add(NodeToExpresion(tempVar));
                        e.Arguments.Add(NodeToExpresion(stack.Pop()));
                        nexpr = e;
                        Dup1Seen = false;
                        break;
                    case GMCode.Call:
                        {
                            if (e.Extra == -1)
                            {
                                nexpr = e;
                                break;
                            } // its already resolved
                              //ILExpression call = new ILExpression(GMCode.Call, )
                              //ILCall call = new ILCall() { Name = e.Operand as string };
                              //for (int j = 0; j < e.Extra; j++) call.Arguments.Add(stack.Pop());
                            if (e.Extra > 0)
                                for (int j = 0; j < e.Extra; j++) e.Arguments.Add(NodeToExpresion(stack.Pop()));
                            e.Extra = -1;
                            stack.Push(e);
                        }
                        break;
                    case GMCode.Popz:
                        {
                            if (stack.Count == 0) nexpr = e; // else, ignore for switch
                            else
                            {
                                ILExpression call = stack.Peek() as ILExpression;
                                if (call != null && call.Code == GMCode.Call)
                                {
                                    nexpr = call;
                                    stack.Pop();
                                }
                                else nexpr = e; // else, ignore for switch
                            }
                        }
                        break;
                    case GMCode.Bf:
                    case GMCode.Bt:

                        if (stack.Count > 0 && e.Arguments.Count != 1)
                        {
                            Debug.Assert(stack.Count == 1);
                            e.Arguments[0] = NodeToExpresion(stack.Pop());
                        }
                        nexpr = e;
                        break;
                    case GMCode.B:
                    case GMCode.Exit:
                        if (stack.Count > 0)
                        {
                            // we should only have ONE item left on the stack per block, and thats in wierd tertory shorts
                            Debug.Assert(stack.Count == 1);
                            list.Add(new ILExpression(GMCode.Push, null, NodeToExpresion(stack.Pop())));
                        }

                        nexpr = e;
                        break; // end of block, process at bottom
                    case GMCode.Ret:
                        throw new Exception("first return I have EVER seen");
                    // try to resolve, if not ignore as its handled latter

                    case GMCode.Dup:
                        if (stack.Count == 0) possableCase = true;
                        if (possableCase)
                        {
                            nexpr = e;
                            break; // break this and handle outside
                        }
                        // ok now we get into the meat of the issue, 
                        // be sure we run switch detection BEFORE this function, otherwise, you will have a bad time:P
                        if ((int)e.Operand == 0) stack.Push(stack.Peek()); // simple case
                        else
                        {
                            // this is usally on an expression that uses a var multipual times so the instance and index is copyed
                            // HOWEVER the stack may need to be swaped
                            Dup1Seen = true; // hack for now
                            foreach (var n in stack.ToArray()) stack.Push(n); // copy the entire stack
                        }
                        break;
                    default: // ok we handle an expression
                        if (e.Code.isExpression())
                        {
                            for (int j = 0; j < e.Code.GetPopDelta(); j++)
                                e.Arguments.Add(NodeToExpresion(stack.Pop()));
                            e.Arguments.Reverse(); // till I fix it latter, sigh
                            stack.Push(e); // push expressions back
                        }
                        else nexpr = e; // fall though
                        break;
                }
                if (nexpr != null) list.Add(nexpr);
            }
            // this is going

            // now the trick, if we still have stuff on the stack, put it back on the list

            CheckList(list);
            if (list.Count == 0) return false;
            else
            {
                head.Body = list; // replace the list
                return true;
            }
        }
    }
}
