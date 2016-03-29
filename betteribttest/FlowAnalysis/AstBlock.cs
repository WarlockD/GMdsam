using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.FlowAnalysis
{
    sealed class AstBlock
    {
        public readonly List<AstBlock> Successors = new List<AstBlock>();
        public readonly List<AstBlock> Predecessors = new List<AstBlock>();
        public readonly ControlFlowNodeType NodeType;
        public readonly List<Instruction> Instructions = new List<Instruction>();
        public List<AstStatement> Block = null;
        /// <summary>
        /// The block index in the control flow graph.
        /// This correspons to the node index in ControlFlowGraph.Nodes, so it can be used to retrieve the original CFG node and look
        /// up additional information (e.g. dominance).
        /// </summary>
        public readonly int BlockIndex;

        internal AstBlock(ControlFlowNode node)
        {
            this.NodeType = node.NodeType;
            this.BlockIndex = node.BlockIndex;
        }

        public override string ToString()
        {
            StringWriter writer = new StringWriter();
            writer.Write("Block #{0} ({1})", BlockIndex, NodeType);
            if(Block!= null)
            {
                foreach(var s in Block) s.DecompileToText(writer);

            } else
            foreach (var inst in Instructions)
            {
                writer.WriteLine();
                inst.WriteTextLine(writer);
            }
            return writer.ToString();
        }
    }
}
