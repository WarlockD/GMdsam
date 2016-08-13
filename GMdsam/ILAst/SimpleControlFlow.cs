using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GameMaker.Ast
{
    static class ListExtensionsLastIndex
    {
        // same as indexof, except backwards
        public static ILLabel OperandLabelAt(this ILBasicBlock bb, int i)
        {
            ILExpression e = bb.Body.ElementAtOrDefault(i) as ILExpression;
            if (e != null && (e.Code == GMCode.B || e.Code == GMCode.Bt || e.Code == GMCode.Bf)) return e.Operand as ILLabel;
            return null;
        }
        public static ILLabel OperandLabelLastAt(this ILBasicBlock bb, int i)
        {
            ILExpression e = bb.Body.ElementAtLastOrDefault(i) as ILExpression;
            if (e != null && (e.Code == GMCode.B || e.Code == GMCode.Bt || e.Code == GMCode.Bf)) return e.Operand as ILLabel;
            return null;
        }
        public static ILNode ElementAt(this ILBasicBlock bb, int i)
        {
            return bb.Body.ElementAt(i);
        }
        public static ILNode ElementAtOrThrow(this ILBasicBlock bb, int i)
        {
            ILNode n = bb.Body.ElementAtOrDefault(i);
            if (n == null) throw new Exception("Null Element");
            return n;
        }
        public static ILNode Last(this ILBasicBlock bb)
        {
            return bb.Body.Last();
        }
        public static ILNode First(this ILBasicBlock bb)
        {
            return bb.Body.First();
        }
        public static void RemoveAll<T>(this IList<T> list, IEnumerable<T> toremove)
        {
            foreach(var a in toremove) list.Remove(a);
        }
        public static void RemoveAll<T>(this IList<ILNode> list, IEnumerable<T> toremove) where T : ILNode
        {
            foreach (var a in toremove) list.Remove(a as ILNode);
        }
        public static ILNode ElementAtOrDefault(this ILBasicBlock bb, int i)
        {
            return bb.Body.ElementAtOrDefault(i);
        }
        public static T ElementAtOrThrow<T>(this ILBasicBlock bb, int i) where T : ILNode
        {
            T t = bb.ElementAtOrThrow(i) as T;
            if (t == null) throw new Exception("Null Element");
            return t;
        }
        public static T ElementAtOrDefault<T>(this ILBasicBlock bb, int i) where T : ILNode
        {
            return bb.Body.ElementAtOrDefault(i) as T;
        }
        public static T ElementAtLast<T>(this IList<T> list, int i)
        {
            return list.ElementAt(list.Count - 1 - i);
        }
        public static T ElementAtLastOrDefault<T>(this IList<T> list, int i)
        {
            return list.ElementAtOrDefault(list.Count - 1 - i);
        }
        public static void RemoveAtLast<T>(this IList<T> list, int i)
        {
            list.RemoveAt(list.Count - 1 - i);
        }
        public static ILNode ElementAtLast(this ILBasicBlock bb, int i)
        {
            return bb.Body.ElementAtLast(i);
        }
        public static ILNode ElementAtLastOrDefault(this ILBasicBlock bb, int i)
        {
            return bb.Body.ElementAtLastOrDefault(i);
        }
        public static void RemoveAt(this ILBasicBlock bb, int i)
        {
            bb.Body.RemoveAt(i);
        }
        public static void RemoveAtLast(this ILBasicBlock bb, int i)
        {
            bb.Body.RemoveAtLast(i);
        }
        
    }
    public class ControlFlowLabelMap
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();
        Dictionary<ILLabel, HashSet<ILLabel>> labelToBranch = new Dictionary<ILLabel, HashSet<ILLabel>>();
        ErrorContext error;
        public ControlFlowLabelMap(ILBlock method, ErrorContext error)
        {
            method.FixParents();
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
                    ILExpression e = n as ILExpression;
                    if (e == null) continue;
                    foreach(var target in e.GetBranchTargets())
                    {
                        labelGlobalRefCount[target] = labelGlobalRefCount.GetOrDefault(target) + 1;
                        HashSet<ILLabel> labels;
                        if (!labelToBranch.TryGetValue(target, out labels)) labelToBranch.Add(target, labels = new HashSet<ILLabel>());
                        labels.Add(entry);
                    }
                }
            }
        }
        public HashSet<ILLabel> LabelToParrents(ILLabel l) { return labelToBranch[l]; }
        public ILBasicBlock LabelToBasicBlock(ILLabel l) { return labelToBasicBlock[l]; }
        public int LabelCount(ILLabel l) { return labelGlobalRefCount[l];  }
    }
    public class SimpleControlFlow
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();
        //  TypeSystem typeSystem;
        ErrorContext error;
        public SimpleControlFlow(ILBlock method, ErrorContext error)
        {
            this.error = error;
            //  this.typeSystem = Context.CurrentMethod.Module.TypeSystem;
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
        void HackSaveNodes(IList<ILNode> body, string filename)
        {
            ILNode parrent = body[0].Parent;
            ILBlock test = new ILBlock();
            test.Body = body;
            error.DebugSave(test, filename, false);
            test.Body = null;
            foreach (var n in body) n.Parent = parrent;
        }
        public bool MatchRepeatStructure(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            ILExpression rcount;
            ILExpression pushZero;
            int dupMode = 0;
            ILLabel fallthough;
            ILLabel repeatBlock;
            if (head.MatchLastAt(6, GMCode.Push, out rcount) && // header for a repeat, sets it up
                head.MatchLastAt(5, GMCode.Dup, out dupMode) &&
                head.MatchLastAt(4, GMCode.Push, out pushZero) &&
                pushZero.Code == GMCode.Constant && (pushZero.Operand as ILValue).IntValue == 0 &&
                head.MatchLastAt(3, GMCode.Sle) &&
                head.MatchLastAndBr(GMCode.Bt, out fallthough, out repeatBlock))
            {

                // We have to seeperate the head from other bits of the block
                // humm, mabye have to put this in the general build routine like we did with push V:(
                head.Body.RemoveTail(GMCode.Push, GMCode.Dup, GMCode.Push, GMCode.Sle, GMCode.Bt, GMCode.B);

                ILBasicBlock header_block;
                ILLabel header_label;
                if (head.Body.Count == 1)
                {// The head only has the label, so its safe to use this as the header
                    header_block = head;
                    header_label = head.EntryLabel();
                }
                else
                {
                    header_label = ILLabel.Generate("R");
                    // We have to seperate the head.
                    header_block = new ILBasicBlock();
                    head.Body.Add(new ILExpression(GMCode.B, header_label));
                    header_block.Body.Add(header_label);
                    body.Insert(pos+1,header_block); // insert before the block so it looks in order
                }

                header_block.Body.Add(new ILExpression(GMCode.Repeat, repeatBlock, rcount));
                header_block.Body.Add(new ILExpression(GMCode.B, fallthough)); 

                 // NOW we got to find the block that matches
                 
                ILExpression subOneConstant;
                ILLabel footerContinue, footerfallThough;
                /*
                 
                
                while (!(start.MatchLastAt(5, GMCode.Push, out subOneConstant) &&
                     subOneConstant.Code == GMCode.Constant && (subOneConstant.Operand as ILValue).IntValue == 1 &&
                     start.MatchLastAt(4, GMCode.Sub) &&
                     start.MatchLastAt(3, GMCode.Dup, out dupMode) && dupMode == 0 &&
                    start.MatchLastAndBr(GMCode.Bt, out footerContinue, out footerfallThough)))
                {
                    ILLabel next = start.GotoLabel();
                    start = labelToBasicBlock[next];
                }
                */ // ok, on more complicated stuf like an internal loop, this fucks up, so we are going to do a hack
                   // The popz fallthough comes RIGHT after the decrment for the repeate loop, so we are going to move up one from that
                   // then check it
                ILBasicBlock popzBlock = labelToBasicBlock[fallthough]; // 
                Debug.Assert((popzBlock.Body[1] as ILExpression).Code == GMCode.Popz);
                popzBlock.Body.RemoveAt(1); // remove the popz
                ILBasicBlock footer = body[body.IndexOf(popzBlock) - 1] as ILBasicBlock;

                if(footer.MatchLastAt(5, GMCode.Push, out subOneConstant) &&
                     subOneConstant.Code == GMCode.Constant && (subOneConstant.Operand as ILValue).IntValue == 1 &&
                     footer.MatchLastAt(4, GMCode.Sub) &&
                     footer.MatchLastAt(3, GMCode.Dup, out dupMode) && dupMode == 0 &&
                    footer.MatchLastAndBr(GMCode.Bt, out footerContinue, out footerfallThough)){
                    Debug.Assert(footerfallThough == fallthough && repeatBlock == footerContinue); // sanity check
                    footer.Body.RemoveTail(GMCode.Push, GMCode.Sub, GMCode.Dup, GMCode.Bt, GMCode.B);
                    footer.Body.Add(new ILExpression(GMCode.B, header_block.EntryLabel()));


                } else throw new Exception("Fuck me");


                // Found!  Some sanity checks thogh
                
                /* MAJOR BUG UNFINSHED WORK ALERT!
                * Ok, so this isn't used in undertale, but at some point, somone might want to do a break or continue
                * Inside of a repeat statment.  I have NO clue why though, use a while?
                * Anyway, if thats the case then you MUST change the target label of evetyhing going to start, to fallthough, otherwise
                * the goto cleaner will start screaming at you and do alot of weird stuff
                * fyi, like the with statments, I am converting this thing into a while loop so I don't have to have
                * custom loop graph code for these things
                * So, for now? convert start to loop back to head, head jumps to fallthough, and we remove the popz from the fall though
                */
             //   ILLabel.Generate("Block_", nextLabelIndex++);
              

          

                return true;
            }
            return false;
        }


        // Detect a switch block, combine them all, and either build a switch block or 
        // just a bunch of if statements
        // the trick is to get rid of the popv at the end of all these case statements
        // might just have to be removed with the "remove redudent code" system

        bool MatchSwitchCase(ILBasicBlock head, out ILLabel trueLabel, out ILLabel falseLabel, out ILExpression condition)
        {
            if (head != null &&
                head.MatchLastAndBr(GMCode.Bt, out trueLabel, out falseLabel) &&
                head.MatchLastAt(3,GMCode.Seq) &&
                head.MatchLastAt(4, GMCode.Push, out condition) &&
                head.MatchLastAt(5, GMCode.Dup)) return true;
            trueLabel = default(ILLabel);
            falseLabel = default(ILLabel);
            condition = default(ILExpression);
            return false;
        }
        bool MatchSwitchCaseAndBuildExpression(ILBasicBlock head, out ILLabel trueLabel, out ILLabel falseLabel, out ILExpression expr)
        {
            ILExpression condition;
            if (MatchSwitchCase(head, out trueLabel, out falseLabel, out condition))
            {
                var ranges = head.JoinILRangesFromTail(5);
                expr = new ILExpression(GMCode.Bt, trueLabel, new ILExpression(GMCode.Seq, null, condition));
                expr.WithILRanges(ranges);
                return true;
            }
            trueLabel = default(ILLabel);
            falseLabel = default(ILLabel);
            expr = default(ILExpression);
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
        class BuildSwitchTree
        {

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
        int _switchStaticCount = 0;
        public bool DetectSwitchAndConvertToBranches(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            ILExpression condition;
            ILLabel trueLabel;
            ILLabel fallThough;
            // REMEMBER:  The searching goes backwards, so we find the LAST case here, so here is the problem
            // Evey time we run this, we have to search for the first block of the case statement backwards and if
            // the push var is not resolved, we have to drop out... evey single time till the push IS resolved
            // so be sure to run this at the bottom of the decision chain.  Its fine if the push is a simple var
            // but I can bet cash this runs 2-3 times if the push is some kind of expression like 5 + (3 % switch_var))
            // we could change the way the optimizing loop works by going from the start and building a que of things 
            // to remove, delete or change hummm...  Take longer but it would make building this functions MUCH simpler
            if (MatchSwitchCase(head, out trueLabel, out fallThough, out condition))  // && head.MatchLastAt(6, GMCode.Push,out switch_expr))                 
            {
                List<ILBasicBlock> caseBlocks = GetAllCaseBlocks(body, head, pos, out condition, out fallThough);

                foreach (var bb in caseBlocks) // replace the dup statements
                {
                    Debug.Assert(bb.MatchLastAt(5, GMCode.Dup));
                    bb.Body[bb.Body.Count - 5] = new ILExpression(GMCode.Push, null, condition);
                    ILExpression expr = bb.Body[bb.Body.Count - 3] as ILExpression;
                 //   expr.Code = GMCode.Case; // conver the equals to a case
                }
                // search the blocks for ending popz's
                HashSet<ILBasicBlock> blocks_done = new HashSet<ILBasicBlock>();
                Stack<ILBasicBlock> agenda = new Stack<ILBasicBlock>(caseBlocks);
                while (agenda.Count > 0)
                {
                    ILBasicBlock bb = agenda.Pop();
                    if (blocks_done.Contains(bb)) continue; // already did it
                    
                    
                    ILExpression popz = bb.Body.OfType<ILExpression>().Where(e => e.Code == GMCode.Popz).SingleOrDefault();
                    if (popz != null)
                    {
                        bb.Body.Remove(popz); // remove it
                        blocks_done.Add(bb);
                    } else
                    {
                        ILLabel exit = bb.OperandLabelLastAt(0);
                        if (exit != null && !blocks_done.Contains(labelToBasicBlock[exit])) agenda.Push(labelToBasicBlock[exit]);
                        exit = bb.OperandLabelLastAt(1); // check if we have a bt or something
                        if (exit != null && !blocks_done.Contains(labelToBasicBlock[exit])) agenda.Push(labelToBasicBlock[exit]);
                    }
                }


                return true;
            }


            return false;
        }
        public List<ILBasicBlock> GetAllCaseBlocks(IList<ILNode> body, ILBasicBlock head, int pos, out ILExpression condition, out ILLabel fallout)
        {
            ILExpression fswitch = new ILExpression(GMCode.Switch, null);
            List<ILBasicBlock> caseBlocks = new List<ILBasicBlock>();
            int swtichStart = pos;
            ILLabel trueLabel;
            ILLabel falseLabel;
            while (MatchSwitchCase(head, out trueLabel, out falseLabel, out condition))
            {
                caseBlocks.Add(head);
                head = body.ElementAtOrDefault(++swtichStart) as ILBasicBlock;
            }
            ILBasicBlock switchHead = caseBlocks.First();
            if (!switchHead.ElementAtLastOrDefault(5).isNodeResolved()) { fallout = null; condition = null; return null; }
         //   caseBlocks.Reverse(); // reverse the order so its correct
            Debug.Assert(switchHead.MatchLastAt(6, GMCode.Push, out condition));// return false;
            switchHead.RemoveAt(switchHead.Body.Count - 6); // ugh, might have to change the matchLastAt
            fallout = caseBlocks.Last().OperandLabelLastAt(0);
            return caseBlocks;
        }
        public bool DetectSwitch_GenerateSwitch(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            ILExpression condition;
            ILLabel trueLabel;
            ILLabel fallThough;
            // REMEMBER:  The searching goes backwards, so we find the FIRST case here, so here is the problem
            // Evey time we run this, we have to search for the first block of the case statement backwards and if
            // the push var is not resolved, we have to drop out... evey single time till the push IS resolved
            // so be sure to run this at the bottom of the decision chain.  Its fine if the push is a simple var
            // but I can bet cash this runs 2-3 times if the push is some kind of expression like 5 + (3 % switch_var))
            // we could change the way the optimizing loop works by going from the start and building a que of things 
            // to remove, delete or change hummm...  Take longer but it would make building this functions MUCH simpler
            if (MatchSwitchCase(head, out trueLabel, out fallThough, out condition) &&
                !MatchSwitchCase(body.ElementAtOrDefault(pos-1) as ILBasicBlock, out trueLabel, out fallThough, out condition)
                )  // && head.MatchLastAt(6, GMCode.Push,out switch_expr))                 
            {
                List<ILBasicBlock> caseBlocks = GetAllCaseBlocks(body, head, pos, out condition, out fallThough);
                if (caseBlocks == null) return false;
                ILExpression fswitch = new ILExpression(GMCode.Switch, null);
                FakeSwitch args = new FakeSwitch();
                args.SwitchExpression = condition;
                args.CaseExpressions = caseBlocks.Select(bb => 
                    new KeyValuePair<ILExpression,ILLabel>((bb.ElementAtLast(3) as ILExpression).Arguments[0],(bb.ElementAtLast(1) as ILExpression).Operand as ILLabel) 
                    ).ToList();

                fswitch.Operand = args;
                // search the blocks for ending popz's and remove the popz
                HashSet<ILBasicBlock> blocks_done = new HashSet<ILBasicBlock>();
                Stack<ILBasicBlock> agenda = new Stack<ILBasicBlock>(caseBlocks);
                while (agenda.Count > 0)
                {
                    ILBasicBlock bb = agenda.Pop();
                    if (blocks_done.Contains(bb)) continue; // already did it

                    ILExpression popz = bb.Body.OfType<ILExpression>().Where(e => e.Code == GMCode.Popz).SingleOrDefault();
                    if (popz != null)
                    {
                        bb.Body.Remove(popz); // remove it
                        
                    }
                    else
                    {
                        ILLabel exit = bb.OperandLabelLastAt(0);
                        if (exit != null && !blocks_done.Contains(labelToBasicBlock[exit])) agenda.Push(labelToBasicBlock[exit]);
                        exit = bb.OperandLabelLastAt(1); // check if we have a bt or something
                        if (exit != null && !blocks_done.Contains(labelToBasicBlock[exit])) agenda.Push(labelToBasicBlock[exit]);
                    }
                    blocks_done.Add(bb);
                }
                ILBasicBlock startOfAllCases = caseBlocks.First();
                caseBlocks.Remove(startOfAllCases);
                startOfAllCases.Body.RemoveTail(GMCode.Dup, GMCode.Push, GMCode.Seq, GMCode.Bt, GMCode.B);
                startOfAllCases.Body.Add(fswitch);
                startOfAllCases.Body.Add(new ILExpression(GMCode.B, fallThough)); //  end_of_switch.EntryLabel()));
                body.RemoveAll(caseBlocks);

                return true;
            }


            return false;
        }
        
        public bool DetectSwitch(IList<ILNode> body, ILBasicBlock head, int pos)
        {
            // We can either convert the switch into a switch body or into a sequrence 
            // of branches.  Since Lua dosn't have a switch, it makes more sence to convert
            // it to if statements as we can optimize it afterwards
            if(Context.outputType == OutputType.LoveLua)
                return DetectSwitch_GenerateBranches(body, head, pos);
            else
                return DetectSwitch_GenerateSwitch(body, head, pos);
        }
        public bool DetectSwitch_GenerateBranches(IList<ILNode> body, ILBasicBlock head, int pos)
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

        public bool SimplifyTernaryOperator(IList<ILNode> body, ILBasicBlock head, int pos)
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
                IList<ILExpression> finalFall;
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
                }else if(fallBlock.MatchAt(1,GMCode.Assign,  out finalFall)) // This is an assignment case and unsure what its for
                {
                    finalFall.Add(newExpr);
                    finalFalseFall = fallBlock.EntryLabel();
                }
                if (finalFalseFall == null && fallBlock.MatchLastAt(1, GMCode.Ret))
                    head.Body.Add(new ILExpression(GMCode.Ret, null));
                else
                {
                    Debug.Assert(finalFalseFall != null);
                    head.Body.Add(new ILExpression(GMCode.B, finalFalseFall));
                }       
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
                current.Arguments[0] = new ILExpression(code, GM_Type.Bool, left, current.Arguments[0]);
                return right;
            }
            else {
                return new ILExpression(code, GM_Type.Bool, left, right);
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
