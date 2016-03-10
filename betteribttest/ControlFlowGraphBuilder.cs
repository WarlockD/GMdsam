using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace betteribttest
{
    class ControlFlowGraphBuilder
    {
        private List<Instruction> _instructions;
        private List<ControlFlowNode> _nodes = new List<ControlFlowNode>();
        public ControlFlowNode EntryPoint { get; private set; }
        public ControlFlowNode RegularExit { get; private set; }

        private int _nextBlockId;

        public static ControlFlowGraph Build(List<Instruction> instructions)
        {
            ControlFlowGraphBuilder builder = new ControlFlowGraphBuilder(instructions);
            return builder.build();
        }

        public ControlFlowGraph build()
        {
            createNodes();
            createRegularControlFlow();
            return new ControlFlowGraph(_nodes.ToArray());
        }
        private ControlFlowGraphBuilder(List<Instruction> instructions)
        {
            Debug.Assert(instructions != null);
            _instructions = instructions;
            _nextBlockId = 0;
        }
        private ControlFlowEdge createEdge(ControlFlowNode fromNode, Instruction toInstruction)
        {
            ControlFlowNode target = null;

            foreach (ControlFlowNode node in _nodes)
            {
                if (node.Start != null && node.Start.Address == toInstruction.Address)
                {
                    if (target != null)
                    {
                        throw new Exception("Multiple edge targets detected!");
                    }
                    target = node;
                }
            }

            if (target != null)
            {
                return createEdge(fromNode, target);
            }

            throw new Exception("Could not find target node!");
        }

        private ControlFlowEdge createEdge(ControlFlowNode fromNode, ControlFlowNode toNode)
        {
            ControlFlowEdge edge = new ControlFlowEdge(fromNode, toNode);

            foreach (ControlFlowEdge existingEdge in fromNode.Outgoing)
            {
                if (existingEdge.Source == fromNode && existingEdge.Target == toNode) return existingEdge;
            }
            fromNode.Outgoing.Add(edge);
            toNode.Incomming.Add(edge);

            return edge;
        }
        private void createBranchControlFlow(ControlFlowNode node, Instruction jump, Instruction target)
        {
            createEdge(node, target);  // We don't have exceptions in game maker whew
        }
        private void createReturnControlFlow(ControlFlowNode node, Instruction end)
        {
            createEdge(node, RegularExit);
        }

        private void createNodes()
        {
            //
            // Step 2a: Find basic blocks and create nodes for them.
            //

            List<Instruction> instructions = _instructions;

            for (int i = 0, n = instructions.Count; i < n; i++)
            {
                Instruction blockStart = instructions[i];

                //
                // See how big we can make that block...
                //
                for (; i + 1 < n; i++)
                {
                    Instruction instruction = instructions[i];
                    if (instruction.isBranch || (instruction.Next != null && instruction.Next.Label != null)) break;///*|| opCode.canThrow()*/ || _hasIncomingJumps[i + 1]) break;


                    //    Instruction next = instruction.Next;
                }
                _nodes.Add(new ControlFlowNode(_nodes.Count, blockStart, instructions[i]));
            }
            // first node should be the entry point
            EntryPoint = _nodes[0];
            // last node is the exit, hopefuly
            RegularExit = _nodes.Last();
        }
        private void createRegularControlFlow()
        {
            //
            // Step 3: Create edges for the normal control flow (assuming no exceptions thrown).
            //

            List<Instruction> instructions = _instructions;
            int last_pc = instructions.Last().Address;
            createEdge(EntryPoint, instructions[0]);

            foreach (ControlFlowNode node in _nodes)
            {
                Instruction end = node.End;
               
                if (end == null || end.Address >= _instructions.Last().Address) continue;

                //
                // Create normal edges from one instruction to the next.
                //
                if (end.GMCode != GMCode.B)
                {
                    Instruction next = end.Next;
                    if (next != null) createEdge(node, next);

                }

                //
                // Create edges for branch instructions.
                //
                for (Instruction instruction = node.Start; instruction != null && instruction.Address <= end.Address; instruction = instruction.Next)
                {
                    if (!instruction.isBranch) continue;
                    Label l = instruction.Operand as Label;
                    if (l.Address > last_pc)
                        createReturnControlFlow(node, instruction);
                    else
                        createBranchControlFlow(node, instruction, l.InstructionOrigin);
                }


                //
                // Create edges for return and leave instructions.
                //
                Label end_label = end.Operand as Label;
                if (end.GMCode == GMCode.Exit || end.GMCode == GMCode.Ret || (end_label != null && end_label.Address > last_pc)) createReturnControlFlow(node, end);
            }
        }
    }
}