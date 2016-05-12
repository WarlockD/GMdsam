using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GameMaker.Dissasembler
{
    public class ControlFlowLabelMap
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();
        public ControlFlowLabelMap(ILBlock method)
        {
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
        public ILBasicBlock LabelToBasicBlock(ILLabel l) { return labelToBasicBlock[l]; }
        public int LabelCount(ILLabel l) { return labelGlobalRefCount[l];  }
    }
    public class SimpleControlFlow
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();
        GMContext context;
        //  TypeSystem typeSystem;

        public SimpleControlFlow(ILBlock method,GMContext context)
        {
            this.context = context;
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
        // Detect a switch block, combine them all, and either build a switch block or 
        // just a bunch of if statements
        // the trick is to get rid of the popv at the end of all these case statements
        // might just have to be removed with the "remove redudent code" system

        bool MatchSwitchCase(ILBasicBlock head, out ILLabel trueLabel, out ILLabel falseLabel, out ILExpression condition)
        {
            if (head.MatchLastAndBr(GMCode.Bt, out trueLabel, out falseLabel) &&
                head.MatchLastAt(3,GMCode.Seq) &&
                head.MatchLastAt(4, GMCode.Push, out condition) &&
                head.MatchLastAt(5, GMCode.Dup)) return true;
            trueLabel = default(ILLabel);
            falseLabel = default(ILLabel);
            condition = default(ILExpression);
            return false;
        }
        ILExpression PreSetUpCaseBlock(ILBasicBlock block, ILExpression condition)
        {
            ILExpression seq = block.Body[block.Body.Count - 3] as ILExpression;
            int dup_push = block.Body.Count - 5;
            block.Body.RemoveRange(dup_push, 3); // remove the push and dup and seq
            ILExpression bt = block.Body[block.Body.Count - 2] as ILExpression;
            bt.Arguments.Add(seq); // add the equals
            seq.Arguments.Add(condition); // add the condition to the equals
            // block is fixed, return the condition as all we need is the left side to compare it to
            return seq;
        }
        public bool DetectSwitchLua(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            bool modified = false;
            List<ILExpression> cases = new List<ILExpression>();
            ILExpression condition;
            ILLabel trueLabel;
            ILLabel falseLabel;
            ILLabel fallThough;
        //    Debug.Assert(head.EntryLabel().Name != "Block_473");
            if (MatchSwitchCase(head, out trueLabel, out fallThough, out condition)) { // we ignore this first match, but remember the position
                cases.Add(PreSetUpCaseBlock(head, condition));
                int lastBlock = pos;
                ILLabel prev = head.EntryLabel();
                for(int i=pos-1; i >=0;i--)
                {
                    ILBasicBlock bb = body[i] as ILBasicBlock;
                    if (MatchSwitchCase(bb, out trueLabel, out falseLabel, out condition))
                    {
                        head = bb;
                        cases.Add(PreSetUpCaseBlock(head, condition));
                        Debug.Assert(falseLabel == prev);
                        prev = head.EntryLabel();
                    }
                    else break;
                }
                // we have all the cases
                // head is at the "head" of the cases
                ILExpression left;
                if (head.Body[head.Body.Count - 3].Match(GMCode.Push, out left))
                {
                    head.Body.RemoveAt(head.Body.Count - 3);
                    foreach (var e in cases) e.Arguments.Insert(0,new ILExpression(left)); // add the expression to all the branches
                } else throw new Exception("switch failure");
                modified |= true;
            }
            return modified;
        }

       
   
        bool MatchGeneratedLoopHeader(ILBasicBlock head, out ILLabel trueLabel, out ILLabel falseLabel, out ILExpression start)
        {
            Debug.Assert(head != null);
            // The patern is
            // Push StartingCount
            // Dup it
            // Push 0
            // Sle 
            // Bt skip
            // :label
            // ...
            // push 1
            // sub from Dup
            // dup it
            // bt dup to label
            // popz
            // in another way
            // if constant > 0 do loop
            // exit loop when constant == 0
            // It feels like this is a loop
            // mabey since the code dosn't need the i in a for loop, that the compiler removes
            // the need for the variable humm
            int dupType = 0;
            ILExpression wierd;
 
            if(head.MatchLastAndBr(GMCode.Bt, out trueLabel, out falseLabel) &&
                head.MatchLastAt(3, GMCode.Sle) &&
                head.MatchLastAt(4, GMCode.Push, out wierd) && // its wierd cause why do we have this match in the first place
                head.MatchLastAt(5, GMCode.Dup, out dupType) &&
                dupType == 0 &&
                head.MatchLastAt(6, GMCode.Push, out start) &&
                wierd.Code == GMCode.Constant) // assume zero
            {
                return true;
            }
            start = default(ILExpression);
            falseLabel = default(ILLabel);
            trueLabel = default(ILLabel);
            return false;
        }
        bool MatchGeneratedLoop(ILBasicBlock head, out ILLabel trueLabel, out ILLabel falseLabel, out ILExpression dec)
        {
            Debug.Assert(head != null);
            // It feels like this is a loop
            // mabey since the code dosn't need the i in a for loop, that the compiler removes
            // the need for the variable humm
            int dupType = 0;
            if (head.MatchLastAndBr(GMCode.Bt,  out trueLabel, out falseLabel) &&
                head.MatchLastAt(3, GMCode.Dup, out dupType) &&
                dupType == 0 &&
                head.MatchLastAt(4, GMCode.Sub) &&
                head.MatchLastAt(5, GMCode.Push, out dec))
            {
                return true;
            }
            dec = default(ILExpression);
            falseLabel = default(ILLabel);
            trueLabel = default(ILLabel);
            return false;
        }
        // These are for loops where the insides don't use the counting variable
        // so the compiler optimized it to just a temp push
        public bool FixOptimizedForLoops(List<ILNode> body, ILBasicBlock head, int pos)
        {
            ILLabel trueLabel; // loop exit
            ILLabel falseLabel; // loop start

            ILExpression start;
            bool modified = false;
            if (MatchGeneratedLoopHeader(head, out trueLabel, out falseLabel, out start))
            {
                modified = true;
                ILVariable tempVar = ILVariable.GenerateTemp("floop");
                ILLabel newBlockLabel = ILLabel.Generate("FLOOP");

                head.Body.RemoveTail(GMCode.Push, GMCode.Dup, GMCode.Push, GMCode.Sle, GMCode.Bt, GMCode.B);
                head.Body.Add(new ILAssign() { Variable = tempVar, Expression = start }); // add the starting value
                head.Body.Add(new ILExpression(GMCode.B, newBlockLabel)); // link it to the new while loop header
   
                ILBasicBlock newLoopHeadder = new ILBasicBlock();
                newLoopHeadder.Body.Add(newBlockLabel);
                newLoopHeadder.Body.Add(new ILExpression(GMCode.Bt, falseLabel, new ILExpression(GMCode.Seq, null, tempVar.ToExpresion(), new ILValue(0).ToExpresion())));
                newLoopHeadder.Body.Add(new ILExpression(GMCode.B, trueLabel));

                for (int i = 0; i < body.Count; i++)
                {
                    ILLabel trueLabelN;
                    ILLabel falseLabelN;
                    ILExpression dec; // not sure if there might be multipul blocks like this, but just loop though it all to be safe

                    ILBasicBlock endBlock = body[i] as ILBasicBlock;
                    if (MatchGeneratedLoop(endBlock, out trueLabelN, out falseLabelN, out dec) &&
                        trueLabelN == falseLabel && falseLabelN == trueLabel // make sure the exit and loop start are the same
                        )
                    { 
                        endBlock.Body.RemoveTail(GMCode.Push, GMCode.Sub, GMCode.Dup, GMCode.Bt, GMCode.B);
                        endBlock.Body.Add(new ILAssign() { Variable = tempVar, Expression = new ILExpression(GMCode.Sub, null, tempVar.ToExpresion(), dec) });
                        endBlock.Body.Add(new ILExpression(GMCode.B, newBlockLabel));
                    }
                }
                if (modified)
                {
                    body.Add(newLoopHeadder);
                    ILBasicBlock exitBlock = labelToBasicBlock[trueLabel];
                    // popz here that pops the temp value
                    Debug.Assert(exitBlock.MatchAt(1, GMCode.Popz));
                    exitBlock.Body.RemoveAt(1); // just remove it
                }
            }
            return modified;
        }


        public bool MatchLastBtOrBf(ILBasicBlock head, out bool isBt, out ILLabel trueLabel,out ILExpression condition, out ILLabel falseLabel) {
            if (head.MatchLastAndBr(GMCode.Bt, out trueLabel, out condition, out falseLabel) ||
               head.MatchLastAndBr(GMCode.Bf, out falseLabel, out condition, out trueLabel)) {
                isBt = (head.Body[head.Body.Count - 2] as ILExpression).Code == GMCode.Bt;
                return true;
            }
            isBt = default(bool);
            trueLabel = default(ILLabel);
            falseLabel = default(ILLabel);
            condition = default(ILExpression);
            return false;
        }
        // This is before the expression is processed, so ILValue's and constants havn't been assigned
        public bool SimplifyTernaryOperator(List<ILNode> body, ILBasicBlock head, int pos)
        {
            Debug.Assert(body.Contains(head));
            //    Debug.Assert((head.Body[0] as ILLabel).Name != "Block_54");
            //     Debug.Assert((head.Body[0] as ILLabel).Name != "L1257");
            bool isBt;
            ILExpression condExpr;
            ILLabel trueLabel;
            ILLabel falseLabel;

            ILExpression trueExpr;
            ILLabel trueFall;

            ILExpression falseExpr;
            ILLabel falseFall;

            List<ILExpression> finalFall;
            ILLabel finalFalseFall;
            ILLabel finalTrueFall;
            if(MatchLastBtOrBf(head,out isBt, out trueLabel, out condExpr, out falseLabel) &&
              //  labelGlobalRefCount[trueLabel] == 1 &&
             //   labelGlobalRefCount[falseLabel] == 1 &&
                labelToBasicBlock[trueLabel].MatchSingleAndBr(GMCode.Push, out trueExpr, out trueFall) &&
                labelToBasicBlock[falseLabel].MatchSingleAndBr(GMCode.Push, out falseExpr, out falseFall) &&
                trueFall == falseFall &&
                body.Contains(labelToBasicBlock[trueLabel]) &&
                labelToBasicBlock[trueFall].MatchLastAndBr(GMCode.Bf, out finalFalseFall, out finalFall, out finalTrueFall) &&
                finalFall.Count == 0
               // finalFall.Code == GMCode.Pop
               ) // (finalFall == null || finalFall.Code == GMCode.Pop)
            {
                Debug.Assert(finalFall.Count ==0);
           //     Debug.Assert(!isBt); // hopefully I don't have to worry about negating
              //  labelToBasicBlock[trueLabel].Body.RemoveAt(1);
                labelToBasicBlock[falseLabel].Body.RemoveAt(1); // remove the pushes
                ILValue falseLocVar = falseExpr.Code == GMCode.Constant ? falseExpr.Operand as ILValue : null;


                ILValue trueLocVar = trueExpr.Code == GMCode.Constant ? trueExpr.Operand as ILValue : null;
                Debug.Assert(falseLocVar != null || trueLocVar != null);
                ILExpression newExpr=null;
                // a ? true : b    is equivalent to  a || b
                // a ? b : true    is equivalent to  !a || b
                // a ? b : false   is equivalent to  a && b
                // a ? false : b   is equivalent to  !a && b
               if (trueLocVar != null && trueLocVar.Type == GM_Type.Short && (trueLocVar == 0 || trueLocVar == 1))
                {
                    // It can be expressed as logical expression
                    if (trueLocVar != 0)
                    {
                        newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicOr, condExpr, falseExpr);
                    }
                    else {
                        newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicAnd, new ILExpression(GMCode.Not, null, condExpr), falseExpr);
                    }
                }
                else if(falseLocVar != null && falseLocVar.Type == GM_Type.Short && (falseLocVar == 0 || falseLocVar == 1))
                    {
                    // It can be expressed as logical expression
                    if (falseLocVar != 0)
                    {
                        newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicOr, new ILExpression(GMCode.Not, null, condExpr), trueExpr);
                    }
                    else {
                        newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicAnd, condExpr, trueExpr);
                    }
                }
                Debug.Assert(newExpr != null);
                // head.Body.RemoveTail(ILCode.Brtrue, ILCode.Br);
                head.Body.RemoveRange(head.Body.Count - 2, 2);
                head.Body.Add(new ILExpression(GMCode.Bf, finalFalseFall, newExpr));
                head.Body.Add(new ILExpression(GMCode.B, finalTrueFall));

                // Remove the inlined branch from scope
               // body.RemoveOrThrow(nextBasicBlock);
                // Remove the old basic blocks
                body.RemoveOrThrow(labelToBasicBlock[trueLabel]);
                body.RemoveOrThrow(labelToBasicBlock[falseLabel]);
                body.RemoveOrThrow(labelToBasicBlock[trueFall]);
                return true;
            }
            return false;
        }
        public bool SimplifyShortCircuit(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            Debug.Assert(body.Contains(head));
           // Debug.Assert((head.Body[0] as ILLabel).Name != "Block_54");
            ILExpression condExpr;
            ILLabel trueLabel;
            ILLabel falseLabel;
            // Ok, since we have not changed out all the Bf's to Bt like in ILSpy, we have to do them seperately
            // as I am getting bugs in my wahhoo about it
            if ((head.MatchLastAndBr(GMCode.Bf, out falseLabel, out condExpr, out trueLabel) ||
                head.MatchLastAndBr(GMCode.Bt, out trueLabel, out condExpr, out falseLabel)) 
                 // its a terrtery so ignore it?
                ) // I saw this too
            {
                GMCode code = (head.Body[head.Body.Count - 2] as ILExpression).Code;
                for (int pass = 0; pass < 2; pass++)
                {

                    // On the second pass, swap labels and negate expression of the first branch
                    // It is slightly ugly, but much better then copy-pasting this whole block
                    ILLabel nextLabel = (pass == 0) ? trueLabel : falseLabel;
                    ILLabel otherLablel = (pass == 0) ? falseLabel : trueLabel;
                    bool negate = (pass == 1);
                    negate = GMCode.Bt == code ? !negate : negate;
                    ILBasicBlock nextBasicBlock = labelToBasicBlock[nextLabel];
                    ILExpression nextCondExpr;
                    ILLabel nextTrueLablel;
                    ILLabel nextFalseLabel; 
                    if (body.Contains(nextBasicBlock) &&
                        nextBasicBlock != head &&
                        labelGlobalRefCount[(ILLabel)nextBasicBlock.Body.First()] == 1 &&
                        (nextBasicBlock.MatchSingleAndBr(GMCode.Bf, out nextFalseLabel, out nextCondExpr, out nextTrueLablel) ||
                        nextBasicBlock.MatchSingleAndBr(GMCode.Bt, out nextTrueLablel, out nextCondExpr, out nextFalseLabel) )&&
                        nextCondExpr.Code != GMCode.Pop && // ugh
                        (otherLablel == nextFalseLabel || otherLablel == nextTrueLablel))
                    {
                        //     Debug.Assert(nextCondExpr.Arguments.Count != 2);
                        // Create short cicuit branch
                        ILExpression logicExpr;
                        if (otherLablel == nextFalseLabel)
                        {
                            logicExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicAnd, negate ? new ILExpression(GMCode.Not, null, condExpr) : condExpr, nextCondExpr);
                           
                        }
                        else {
                            logicExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicOr, negate ? condExpr : new ILExpression(GMCode.Not, null, condExpr), nextCondExpr);

                        }
                     //   head.Body.RemoveTail(GMCode.Bf, GMCode.B);
                        head.Body.RemoveRange(head.Body.Count - 2, 2);
                        head.Body.Add(new ILExpression(GMCode.Bf, nextFalseLabel, logicExpr));
                        head.Body.Add(new ILExpression(GMCode.B, nextTrueLablel));

                        // Remove the inlined branch from scope
                        body.RemoveOrThrow(nextBasicBlock);

                        return true;
                    }
                    
                }
                
            }
         
            return false;
        }



        ILExpression MakeLeftAssociativeShortCircuit(GMCode code, ILExpression left, ILExpression right)
        {
            // Assuming that the inputs are already left associative
            if (right.Match(code))
            {
                // Find the leftmost logical expression
                ILExpression current = right;
                while (current.Arguments[0].Match(code))
                    current = current.Arguments[0];
                current.Arguments[0] = new ILExpression(code, null, left, current.Arguments[0]) { InferredType = GM_Type.Bool };
                return right;
            }
            else {
                return new ILExpression(code, null, left, right) { InferredType = GM_Type.Bool };
            }
        }
        // somewhere, so bug, is leaving an empty block, I think because of switches
        public bool RemoveRedundentBlocks(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            if(head.Body.Count == 2 && body.Contains(head) && !labelGlobalRefCount.ContainsKey(head.EntryLabel()) )
            {
                body.RemoveOrThrow(head);
                return true;
            }
            return false;
        }
        public bool JoinBasicBlocks(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            ILLabel nextLabel;
            ILBasicBlock nextBB;
            if (!head.Body.ElementAtOrDefault(head.Body.Count - 2).IsConditionalControlFlow() &&
                head.Body.Last().Match(GMCode.B, out nextLabel) &&
                labelGlobalRefCount[nextLabel] == 1 &&
                labelToBasicBlock.TryGetValue(nextLabel, out nextBB) &&
                body.Contains(nextBB) &&
                nextBB.Body.First() == nextLabel 
               )
            {
                head.Body.RemoveTail(GMCode.B);
                nextBB.Body.RemoveAt(0);  // Remove label
                foreach (var a in nextBB.Body) head.Body.Add(a); // head.Body.AddRange(nextBB.Body);

                body.RemoveOrThrow(nextBB);
                return true;
            }
            return false;
        }
    }
}
