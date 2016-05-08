using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker.FlowAnalysis
{
    class OpCodeInfo
    {
        public static bool IsUnconditionalBranch(GMCode opcode)
        {
            switch (opcode)
            {
                case GMCode.Exit:
                case GMCode.Ret:
                case GMCode.B:
                    return true;
                case GMCode.Bt:
                case GMCode.Bf:
                    return false;
                default:
                    return false;
            }
        }
        public static bool IsConditionalBranch(GMCode opcode)
        {
            switch (opcode)
            {
                case GMCode.Popenv:
                case GMCode.Pushenv:
                case GMCode.Exit:
                case GMCode.Ret:
                case GMCode.B:
                    return false;
                case GMCode.Bt:
                case GMCode.Bf:
                    return true;
                default:
                    return false;
            }
        }
    }
}
