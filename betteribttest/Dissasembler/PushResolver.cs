using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GameMaker.Dissasembler
{
    public class PushResolver
    {
        Dictionary<ILLabel, int> labelGlobalRefCount = new Dictionary<ILLabel, int>();
        Dictionary<ILLabel, ILBasicBlock> labelToBasicBlock = new Dictionary<ILLabel, ILBasicBlock>();

        GMContext context;
        //  TypeSystem typeSystem;

        public PushResolver(ILBlock method, GMContext context)
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
        void ResolveAllBranchPushes(ILBlock block)
        {

        }
    }
}
