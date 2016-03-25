using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.FlowAnalysis
{
    class OpCodeInfo
    {
        public static bool IsUnconditionalBranch(GMCode opcode)
        {
            switch (opcode.FlowControl)
            {
                case FlowControl.Branch:
                case FlowControl.Throw:
                case FlowControl.Return:
                    return true;
                case FlowControl.Next:
                case FlowControl.Call:
                case FlowControl.Cond_Branch:
                    return false;
                default:
                    throw new NotSupportedException(opcode.FlowControl.ToString());
            }
        }
    }
}
