using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.FlowAnalysis
{
    /// <summary>
    /// Describes the type of a control flow egde.
    /// </summary>
    public enum JumpType
    {
        /// <summary>
        /// A regular control flow edge.
        /// </summary>
        Normal,
        /// <summary>
        /// Jump to end of program, but not an exit or return
        /// </summary>

        /// <summary>
        /// Jump to end of program, but not an exit or return
        /// </summary>
        JumpToEndOfProgram,
        PushEnviroment,
        PopEnviroment
    }

    /// <summary>
    /// Represents an edge in the control flow graph, pointing from Source to Target.
    /// </summary>
    public sealed class ControlFlowEdge
    {
        public readonly ControlFlowNode Source;
        public readonly ControlFlowNode Target;
        public readonly JumpType Type;

        public ControlFlowEdge(ControlFlowNode source, ControlFlowNode target, JumpType type)
        {
            this.Source = source;
            this.Target = target;
            this.Type = type;
        }
        public ControlFlowEdge(ControlFlowNode source, ControlFlowNode target) : this(source, target, JumpType.Normal) { }
        public override string ToString()
        {
            switch (Type)
            {
                case JumpType.Normal:
                    return "#" + Target.BlockIndex;
                default:
                    return Type.ToString() + ":#" + Target.BlockIndex;
            }
        }
    }
}