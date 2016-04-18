using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace betteribttest.Dissasembler
{
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
        // Soo, since I cannot be 100% sure where the start of a instance might be
        // ( could be an expresion, complex var, etc)
        // Its put somewhere in a block 
        public bool PushEnviromentFix(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            ILExpression expr;
            ILLabel next;
            ILLabel pushLabel;
            ILLabel pushLabelNext;
            if (head.MatchLastAndBr(GMCode.Push, out expr, out next)&&
            //     labelGlobalRefCount[next] == 1 &&  // don't check this
                body.Contains(labelToBasicBlock[next]) &&
                labelToBasicBlock[next].MatchSingleAndBr(GMCode.Pushenv, out pushLabel, out pushLabelNext) 
                )
            {
                ILBasicBlock pushBlock = labelToBasicBlock[next];
                head.Body.RemoveAt(head.Body.Count - 2);// hackery, but sure, block should be removed in a flatten
                if(expr.Code == GMCode.Constant)
                {
                    ILValue value = expr.Operand as ILValue;
                    if(value.Value is int)
                    {
                        value.ValueText = context.InstanceToString((int)value.Value);
                    }
                }
                (pushBlock.Body[pushBlock.Body.Count - 2] as ILExpression).Arguments.Add(expr);
                return true;
            }
            return false;
        }
        // you know, this would be SOO much simpler with blocks..  Humm.. change after test
        public bool SwitchDetection(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            ILExpression switchCondition;
            ILExpression caseCondition;
            ILLabel caseLabel;
            ILLabel nextLabel;
            //Debug.Assert((head.Body[0] as ILLabel).Name != "Block_0");
            if(head.MatchCaseBlockStart(out switchCondition,out caseCondition, out caseLabel, out nextLabel) ){
                // We match the start of the case block
                List<ILBasicBlock> toRemove = new List<ILBasicBlock>();
                List<ILLabel> caseLabels = new List<ILLabel>();
                List<ILExpression> caseExpressions = new List<ILExpression>();
                caseExpressions.Add(switchCondition); // wish I had another place to put this
                ILBasicBlock current = head;
                do
                {
                    caseLabels.Add(caseLabel);
                    caseExpressions.Add(new ILExpression(GMCode.Case, caseLabel, caseCondition)); // add the first one
                    toRemove.Add(current);
                    current = labelToBasicBlock[nextLabel];
                } while (current.MatchCaseBlock(out caseCondition, out caseLabel, out nextLabel));
                // we should be stopped on the block that jumps to the end of the case
                ILLabel fallLabel;
                if (!current.MatchSingle(GMCode.B, out fallLabel)) throw new Exception("Bad case fail case?");
                // This is SOO much easyer in basicblocks, I understand why you break them up now
                ILBasicBlock fallBlock = labelToBasicBlock[fallLabel];
                if(!fallBlock.MatchAt(1,GMCode.Popz)) throw new Exception("Bad block  fail case?");
                fallBlock.Body.RemoveAt(1); // remove the popZ for the switch condition
                
                head.Body.RemoveTail(GMCode.Push, GMCode.Dup, GMCode.Push,GMCode.Push, GMCode.Bt,GMCode.B); // better to just rewrite it?
                head.Body.Add(new ILExpression(GMCode.Switch, caseLabels.ToArray(), caseExpressions));
                head.Body.Add(new ILExpression(GMCode.B, fallLabel));
                foreach (var b in toRemove) body.Remove(b);
                body.Add(head); // put the head back on
                return true;
            }

            return false;
        }

        public bool SimplifyTernaryOperator(List<ILNode> body, ILBasicBlock head, int pos)
        {
            Debug.Assert(body.Contains(head));
        //    Debug.Assert((head.Body[0] as ILLabel).Name != "Block_54");
       //     Debug.Assert((head.Body[0] as ILLabel).Name != "L1257");
            
            ILExpression condExpr;
            ILLabel trueLabel;
            ILLabel falseLabel;
           
            ILExpression trueExpr;
            ILLabel trueFall;
            
            ILExpression falseExpr;
            ILLabel falseFall;

            ILExpression finalFall;
            ILLabel finalFalseFall;
            ILLabel finalTrueFall;

            if ((head.MatchLastAndBr(GMCode.Bt, out trueLabel, out condExpr, out falseLabel) ||
                head.MatchLastAndBr(GMCode.Bf, out falseLabel, out condExpr, out trueLabel)) &&
                labelGlobalRefCount[trueLabel] == 1 &&
                labelGlobalRefCount[falseLabel] == 1 &&
                labelToBasicBlock[trueLabel].MatchSingleAndBr(GMCode.Push, out trueExpr, out trueFall) &&
                labelToBasicBlock[falseLabel].MatchSingleAndBr(GMCode.Push, out falseExpr, out falseFall) &&
                trueFall == falseFall &&
                body.Contains(labelToBasicBlock[trueLabel]) &&
                labelToBasicBlock[trueFall].MatchLastAndBr(GMCode.Bf, out finalFalseFall, out finalFall, out finalTrueFall) &&
                finalFall.Code == GMCode.Pop
               ) // (finalFall == null || finalFall.Code == GMCode.Pop)
            {
                Debug.Assert(finalFall.Arguments.Count != 2);
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
                        newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicAnd, condExpr, falseExpr);
                    }
                    else {
                        newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicOr, new ILExpression(GMCode.Not, null, condExpr), falseExpr);
                    }
                }
                else if(falseLocVar != null && falseLocVar.Type == GM_Type.Short && (falseLocVar == 0 || falseLocVar == 1))
                    {
                    // It can be expressed as logical expression
                    if (falseLocVar != 0)
                    {
                        newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicAnd, new ILExpression(GMCode.Not, null, condExpr), trueExpr);
                    }
                    else {
                        newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicOr, condExpr, trueExpr);
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
                head.MatchLastAndBr(GMCode.Bt, out trueLabel, out condExpr, out falseLabel)) &&
                condExpr.Code != GMCode.Pop // its a terrtery so ignore it?
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
