using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GameMaker.Ast
{
    public class ControlFlowLabelMap
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();
        Dictionary<ILLabel, List<ILLabel>> labelToBranch = new Dictionary<ILLabel, List<ILLabel>>();
        Context.ErrorContext error;
        public ControlFlowLabelMap(ILBlock method, Context.ErrorContext error)
        {
            this.error = error;
            foreach (ILBasicBlock bb in method.GetSelfAndChildrenRecursive<ILBasicBlock>())
            {
                ILLabel entry = bb.Body[0] as ILLabel;
                //     ILExpression br = bb.Body.ElementAtOrDefault(bb.Body.Count - 2) as ILExpression;
                //     ILExpression b = bb.Body.ElementAtOrDefault(bb.Body.Count - 1) as ILExpression;
                //     if (br != null && (br.Code == GMCode.Bt || br.Code == GMCode.Bt)) labelToBranch[br.Operand as ILLabel].Add(entry);
                //   if (b != null && b.Code == GMCode.B) labelToBranch[b.Operand as ILLabel].Add(entry);
                // skip 1 cause we have the label entry
                labelToBasicBlock[entry] = bb;
                for (int i = 1; i < bb.Body.Count; i++)
                {
                    ILNode n = bb.Body[i];
                    ILLabel target = null;
                    ILExpression e = n as ILExpression;
                    if (e != null) target = e.Operand as ILLabel;
                    if (target != null)
                    {
                        labelGlobalRefCount[target] = labelGlobalRefCount.GetOrDefault(target) + 1;
                        labelToBranch.GetOrDefault(target).Add(entry);
                        continue;
                    }
                    ILFakeSwitch fswitch = n as ILFakeSwitch;
                    if (fswitch != null)
                    {
                        foreach (var fl in fswitch.GetLabels())
                            labelGlobalRefCount[fl] = labelGlobalRefCount.GetOrDefault(fl) + 1;
                    }
                }
            }
        }
        public void ControlFlowLabelMapOld(ILBlock method)
        {
            // This single constructor is eating ALOT o time, renaming it and trying to considate it into one loop
            foreach (ILLabel target in method.GetSelfAndChildrenRecursive<ILExpression>(e => e.IsBranch()).SelectMany(e => e.GetBranchTargets()))
            {
                labelGlobalRefCount[target] = labelGlobalRefCount.GetOrDefault(target) + 1;
                labelToBranch[target] = new List<ILLabel>();
            }
            foreach (ILBasicBlock bb in method.GetSelfAndChildrenRecursive<ILBasicBlock>())
            {
                ILLabel entry = bb.EntryLabel();
                ILExpression br = bb.Body.ElementAtOrDefault(bb.Body.Count - 2) as ILExpression;
                ILExpression b = bb.Body.ElementAtOrDefault(bb.Body.Count - 1) as ILExpression;
                if (br != null && (br.Code == GMCode.Bt || br.Code == GMCode.Bt)) labelToBranch[br.Operand as ILLabel].Add(entry);
                if (b != null && b.Code == GMCode.B) labelToBranch[b.Operand as ILLabel].Add(entry);
                //

                foreach (ILLabel label in bb.GetChildren().OfType<ILLabel>())
                {
                    labelToBasicBlock[label] = bb;
                }
                foreach (ILFakeSwitch fswitch in bb.GetChildren().OfType<ILFakeSwitch>())
                {
                    foreach (var target in fswitch.GetLabels())
                        labelGlobalRefCount[target] = labelGlobalRefCount.GetOrDefault(target) + 1;
                }
            }
           
        }
        public List<ILLabel> LabelToParrents(ILLabel l) { return labelToBranch[l]; }
        public ILBasicBlock LabelToBasicBlock(ILLabel l) { return labelToBasicBlock[l]; }
        public int LabelCount(ILLabel l) { return labelGlobalRefCount[l];  }
    }
    public class SimpleControlFlow
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();
        //  TypeSystem typeSystem;
        Context.ErrorContext error;
        public SimpleControlFlow(ILBlock method, Context.ErrorContext error)
        {
            this.error = error;
            //  this.typeSystem = Context.CurrentMethod.Module.TypeSystem;
           foreach (ILLabel target in method.GetSelfAndChildrenRecursive<ILExpression>(e => e.IsBranch()).SelectMany(e => e.GetBranchTargets()))
            {
                labelGlobalRefCount[target] = labelGlobalRefCount.GetOrDefault(target) + 1;
            }
            foreach (ILFakeSwitch fswitch in method.GetSelfAndChildrenRecursive<ILFakeSwitch>())
            {
                foreach(var target in fswitch.GetLabels())
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
        struct AgendaParrent
        {
            public ILLabel Parent;
            public ILBasicBlock Block;
        }
        ILBasicBlock FindEndOfSwitch(ILBasicBlock start, out ILLabel Parent)
        {
            Stack<AgendaParrent> agenda = new Stack<AgendaParrent>();
            agenda.Push(new AgendaParrent() { Block = start, Parent = null });
            while (agenda.Count > 0)
            {
                AgendaParrent bb = agenda.Pop();
                if (bb.Block.MatchAt(1, GMCode.Popz))
                {
                    Parent = bb.Parent;
                    return bb.Block;
                }
                foreach (ILLabel target in bb.Block.GetSelfAndChildrenRecursive<ILExpression>(e => e.IsBranch()).SelectMany(e => e.GetBranchTargets()))
                    agenda.Push(new AgendaParrent() { Block = labelToBasicBlock[target], Parent = bb.Block.EntryLabel() });
            }
            Parent = default(ILLabel);
            return null;
        }
        ILBasicBlock FindEndOfSwitch(ILBasicBlock start)
        {
            ILLabel callTo;
            return FindEndOfSwitch(start, out callTo);
        }
        public bool DetectSwitch_GenerateSwitch(List<ILNode> body, ILBasicBlock head, int pos)
        {

            bool modified = false;
            ILExpression condition;
            ILLabel trueLabel;
            ILLabel falseLabel;
            ILLabel fallThough;
            //    Debug.Assert(head.EntryLabel().Name != "Block_473");
            if (MatchSwitchCase(head, out trueLabel, out fallThough, out condition))
            {
                ILFakeSwitch fake = new ILFakeSwitch() { Default = head.GotoLabel() };
                fake.Cases.Add(new ILFakeSwitch.ILCase() { Goto = trueLabel, Value = condition });
                List<ILNode> caseBlocks = new List<ILNode>();
                ILLabel prev = head.EntryLabel();
                ILBasicBlock startOfCases = head;
                caseBlocks.Add(startOfCases);

                for (int i = pos - 1; i >= 0; i--)
                {
                    ILBasicBlock bb = body[i] as ILBasicBlock;
                    if (MatchSwitchCase(bb, out trueLabel, out falseLabel, out condition))
                    {
                        caseBlocks.Add(bb);
                        fake.Cases.Add(new ILFakeSwitch.ILCase() { Goto = trueLabel, Value = condition });
                        Debug.Assert(falseLabel == prev);
                        prev = bb.EntryLabel();
                        startOfCases = bb;
                    }
                    else break;
                } // we have all the cases


                if (!startOfCases.Body[startOfCases.Body.Count - 4].Match(GMCode.Push, out fake.Condition)) { 

                    error.Info("switch failure " + startOfCases.ToString());
                    
                    // Something not worky?
                    throw new Exception("switch failure");

                }
                // It seems GM makes a default case that just jumps to the end of the switch but I think I can 
                // rely on it always being there
                ILBasicBlock default_case = body[pos + 1] as ILBasicBlock;
                Debug.Assert(default_case.EntryLabel() == head.GotoLabel());
                ILBasicBlock end_of_switch = FindEndOfSwitch(default_case, out fallThough);
                if(end_of_switch != null)
                {
                    if(fallThough == null) // we have no default case
                    {
                        fallThough = default_case.EntryLabel();
                        end_of_switch.Body.RemoveAt(1); // yeaa!
                    } else
                    {
                        ILBasicBlock test1 = FindEndOfSwitch(startOfCases);
                        ILBasicBlock test2 = FindEndOfSwitch(head);
                        // Make sure we all reach the same place, I should test ALL the cases to make sure this happens
                        // but just a few should be good enough
                        if (end_of_switch == test1 && end_of_switch == test2) {
                            end_of_switch.Body.RemoveAt(1); // yeaa!
                        } else
                        {
                            error.Error("Cannot find end of switch", end_of_switch);  // bad, cant find end
                            throw new Exception("end_of_switch");
                        }
                    }
                } else
                {
                    error.Error("Cannot find end of switch", end_of_switch);  // bad, cant find end
                    throw new Exception("end_of_switch");
                }
                // Now we have to  clean up
                foreach (var b in caseBlocks.Where(x => x != startOfCases)) body.RemoveOrThrow(b);
                // then modify head with the new switch structure
                startOfCases.Body.RemoveTail(GMCode.Push, GMCode.Dup, GMCode.Push, GMCode.Seq, GMCode.Bt, GMCode.B);
                startOfCases.Body.Add(fake);
                startOfCases.Body.Add(new ILExpression(GMCode.B, fake.Default)); //  end_of_switch.EntryLabel()));
                modified = true;
            }
            return modified;
        }
        public bool DetectSwitch(List<ILNode> body, ILBasicBlock head, int pos)
        {
            // We can either convert the switch into a switch body or into a sequrence 
            // of branches.  Since Lua dosn't have a switch, it makes more sence to convert
            // it to if statements as we can optimize it afterwards
            if(Context.outputType == OutputType.LoveLua)
                return DetectSwitch_GenerateBranches(body, head, pos);
            else
                return DetectSwitch_GenerateSwitch(body, head, pos);
        }
        public bool DetectSwitch_GenerateBranches(List<ILNode> body, ILBasicBlock head, int pos)
        {
            bool modified = false;
            ILExpression condition;
            ILLabel trueLabel;
            ILLabel falseLabel;
            ILLabel fallThough;
        //    Debug.Assert(head.EntryLabel().Name != "Block_473");
            if (MatchSwitchCase(head, out trueLabel, out fallThough, out condition)) { // we ignore this first match, but remember the position
                List<ILExpression> cases = new List<ILExpression>();
                List<ILNode> caseBlocks = new List<ILNode>();
                ILLabel prev = head.EntryLabel();
                ILBasicBlock startOfCases = head;
                cases.Add(PreSetUpCaseBlock(startOfCases, condition));
                caseBlocks.Add(startOfCases);
    
                for (int i=pos-1; i >=0;i--)
                {
                    ILBasicBlock bb = body[i] as ILBasicBlock;
                    if (MatchSwitchCase(bb, out trueLabel, out falseLabel, out condition))
                    {
                        caseBlocks.Add(bb);
                        cases.Add(PreSetUpCaseBlock(bb, condition));
                      
                        Debug.Assert(falseLabel == prev);
                        prev = bb.EntryLabel();
                        startOfCases = bb;
                    }
                    else break;
                }
                // we have all the cases
                // head is at the "head" of the cases
                ILExpression left;
                if (startOfCases.Body[startOfCases.Body.Count - 3].Match(GMCode.Push, out left))
                {
                    startOfCases.Body.RemoveAt(startOfCases.Body.Count - 3);
                    foreach (var e in cases) e.Arguments.Insert(0,new ILExpression(left)); // add the expression to all the branches
                } else throw new Exception("switch failure");
                // It seems GM makes a default case that just jumps to the end of the switch but I cannot
                // rely on it always being there
                ILBasicBlock default_case = body[pos + 1] as ILBasicBlock;
                Debug.Assert(default_case.EntryLabel() == head.GotoLabel());
                ILBasicBlock end_of_switch = labelToBasicBlock[default_case.GotoLabel()];
                if ((end_of_switch.Body[1] as ILExpression).Code == GMCode.Popz)
                {
                    end_of_switch.Body.RemoveAt(1); // yeaa!
                }
                else // booo
                { // We have a default case so now we have to find where the popz ends, 
                    // this could be bad if we had a wierd generated for loop, but we are just doing stupid search
                    ILBasicBlock test1 = FindEndOfSwitch(end_of_switch);
                    // we take a sample from one of the cases to make sure we do end up at the same place
                    ILBasicBlock test2 = FindEndOfSwitch(head);
                    if (test1 == test2)
                    { // two matches are good enough for me
                        test1.Body.RemoveAt(1); // yeaa!
                    }
                    else
                    {
                        error.Error("Cannot find end of switch", end_of_switch); // booo
                    }
                }
                // tricky part, finding that damn popz

                // Ok, we have all the case blocks, they are all fixed, and its like a big chain of ifs now.
                // But for anything OTHER than lua that has switch statments, we want to mark this for latter

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
                head.Body.Add(new ILExpression(GMCode.B,  tempVar,  start )); // add the starting value
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
                        endBlock.Body.Add(new ILExpression(GMCode.Assign, tempVar, new ILExpression(GMCode.Sub, null, tempVar.ToExpresion(), dec))); // add the starting value
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
       
        public ILExpression ResolveTernaryExpression(ILExpression condExpr, ILExpression trueExpr, ILExpression falseExpr)
        {
            int? falseLocVar = falseExpr.Operand is ILValue ? (falseExpr.Operand as ILValue).IntValue : null;
            int? trueLocVar = trueExpr.Operand is ILValue ? (trueExpr.Operand as ILValue).IntValue : null;

            Debug.Assert(falseLocVar != null || trueLocVar != null);
            ILExpression newExpr = null;
            // a ? true : b    is equivalent to  a || b
            // a ? b : true    is equivalent to  !a || b
            // a ? b : false   is equivalent to  a && b
            // a ? false : b   is equivalent to  !a && b
            if (trueLocVar != null && (trueLocVar == 0 || trueLocVar == 1))
            {
                // It can be expressed as logical expression
                if (trueLocVar != 0)
                {
                    newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicOr, condExpr, falseExpr);

                }
                else
                {

                    newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicAnd, new ILExpression(GMCode.Not, null, condExpr), falseExpr);

                }
            }
            else if (falseLocVar != null && (falseLocVar == 0 || falseLocVar == 1))
            {
                // It can be expressed as logical expression
                if (falseLocVar != 0)
                {
                    newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicOr, new ILExpression(GMCode.Not, null, condExpr), trueExpr);
                }
                else
                {
                    newExpr = MakeLeftAssociativeShortCircuit(GMCode.LogicAnd, condExpr, trueExpr);
                }
            }
            Debug.Assert(newExpr != null);
            return newExpr;
        }
        // This is before the expression is processed, so ILValue's and constants havn't been assigned

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

            List<ILExpression> finalFall;
            ILLabel finalFalseFall;
            ILLabel finalTrueFall;
            if(head.MatchLastAndBr(GMCode.Bt, out trueLabel, out condExpr, out falseLabel) &&
               labelGlobalRefCount[trueLabel] == 1 &&
               labelGlobalRefCount[falseLabel] == 1 &&
                labelToBasicBlock[trueLabel].MatchSingleAndBr(GMCode.Push, out trueExpr, out trueFall) &&
                labelToBasicBlock[falseLabel].MatchSingleAndBr(GMCode.Push, out falseExpr, out falseFall) &&
                trueFall == falseFall &&
                body.Contains(labelToBasicBlock[trueFall]) 
               // finalFall.Code == GMCode.Pop
               ) // (finalFall == null || finalFall.Code == GMCode.Pop)
            {
                ILBasicBlock trueBlock = labelToBasicBlock[trueLabel];
                ILBasicBlock falseBlock = labelToBasicBlock[falseLabel];
                ILBasicBlock fallBlock = labelToBasicBlock[trueFall];
                ILExpression newExpr = ResolveTernaryExpression(condExpr, trueExpr, falseExpr);

                head.Body.RemoveTail(GMCode.Bt, GMCode.B);
                body.RemoveOrThrow(trueBlock);
                body.RemoveOrThrow(falseBlock);
                // figure out if its a wierd short or not
                if (fallBlock.MatchSingleAndBr(GMCode.Bt, out finalTrueFall, out finalFall, out finalFalseFall) &&
                finalFall.Count == 0)
                {
                    head.Body.Add(new ILExpression(GMCode.Bt, finalTrueFall, newExpr));
                    if (labelGlobalRefCount[trueFall] == 2) body.RemoveOrThrow(fallBlock);
                } else if(fallBlock.Body.Count == 2) // wierd break,
                {
                    finalFalseFall = fallBlock.GotoLabel();
                    head.Body.Add(new ILExpression(GMCode.Push, null, newExpr)); // we want to push it for next pass
                    if (labelGlobalRefCount[trueFall] == 2) body.RemoveOrThrow(fallBlock);
                }
                else if (fallBlock.MatchAt(1,GMCode.Pop)) { // generated? wierd instance?
                    finalFalseFall = fallBlock.EntryLabel();
                    error.Info("Wierd Generated Pop here", newExpr);
                    head.Body.Add(new ILExpression(GMCode.Push, null, newExpr));
                    // It should be combined in JoinBasicBlocks function
                    // so don't remove failblock
                }
                Debug.Assert(finalFalseFall != null);
              
                head.Body.Add(new ILExpression(GMCode.B, finalFalseFall));         
                return true;
            }
            return false;
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
                        labelGlobalRefCount[(ILLabel) nextBasicBlock.Body.First()] == 1 &&
                        nextBasicBlock.MatchSingleAndBr(GMCode.Bt, out nextTrueLablel, out nextCondExpr, out nextFalseLabel) &&
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
                        else
                        {
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
        // somewhere, so bug, is leaving an empty block, I think because of switches
        public bool RemoveRedundentBlocks(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            if(!labelGlobalRefCount.ContainsKey(head.EntryLabel())  && body.Contains(head))
            {
                if(head.Body.Count != 2)
                {
                    // we have an empty block that has data in it? throw it as an error.
                    // Might just be extra code like after an exit that was never used or a programer error but lets record it anyway
                    error.Warning("BasicBlock with data removed, not linked to anything so should be safe", head);
                }
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
