using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace betteribttest.GMAst
{
    public class SimpleControlFlow
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();

       // DecompilerContext context;
      //  TypeSystem typeSystem;

        public SimpleControlFlow(ILBlock method)
        {
          //  this.context = context;
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


        public bool SimplifyShortCircuit(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            Debug.Assert(body.Contains(head));

            ILExpression condExpr;
            ILLabel trueLabel;
            ILLabel falseLabel;
            if (head.MatchLastAndBr(GMCode.Bt, out trueLabel, out condExpr, out falseLabel))
            {
                for (int pass = 0; pass < 2; pass++)
                {

                    // On the second pass, swap labels and negate expression of the first branch
                    // It is slightly ugly, but much better then copy-pasting this whole block
                    ILLabel nextLabel = (pass == 0) ? trueLabel : falseLabel;
                    ILLabel otherLablel = (pass == 0) ? falseLabel : trueLabel;
                    bool negate = (pass == 1);

                    ILBasicBlock nextBasicBlock = labelToBasicBlock[nextLabel];
                    ILExpression nextCondExpr;
                    ILLabel nextTrueLablel;
                    ILLabel nextFalseLabel;
                    if (body.Contains(nextBasicBlock) &&
                        nextBasicBlock != head &&
                        labelGlobalRefCount[(ILLabel)nextBasicBlock.Body.First()] == 1 &&
                        nextBasicBlock.MatchSingleAndBr(GMCode.Bt, out nextTrueLablel, out nextCondExpr, out nextFalseLabel) &&
                        (otherLablel == nextFalseLabel || otherLablel == nextTrueLablel))
                    {
                        // Create short cicuit branch
                        ILExpression logicExpr;
                        if (otherLablel == nextFalseLabel)
                        {
                            logicExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicAnd, negate ? new ILExpression(GMCode.Not, null, condExpr) : condExpr, nextCondExpr);
                        }
                        else {
                            logicExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicOr, negate ? condExpr : new ILExpression(GMCode.Not, null, condExpr), nextCondExpr);
                        }
                        head.Body.RemoveTail(GMCode.Bt, GMCode.B);
                        head.Body.Add(new ILExpression(GMCode.Bt, nextTrueLablel, logicExpr));
                        head.Body.Add(new ILExpression(GMCode.B, nextFalseLabel));

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
