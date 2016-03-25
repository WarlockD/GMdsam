﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace betteribttest
{
    class ControlFlowGraphBuilderOld
    {
        private IList<Instruction> _instructions;
        private List<ControlFlowNodeOld> _nodes = new List<ControlFlowNodeOld>();
        public ControlFlowNodeOld EntryPoint { get; private set; }
        public ControlFlowNodeOld RegularExit { get; private set; }

        private int _nextBlockId;

        public static ControlFlowGraphOld Build(IList<Instruction> instructions)
        {
            ControlFlowGraphBuilderOld builder = new ControlFlowGraphBuilderOld(instructions);
            
            return builder.build();
        }

        public ControlFlowGraphBuilderOld build()
        {
            _nextBlockId = 0;
         
            
            createNodes();
            createRegularControlFlow();
            return new ControlFlowGraphBuilderOld(_nodes.ToArray());
        }
        private ControlFlowGraphBuilderOld(IList<Instruction> instructions)
        {
            Debug.Assert(instructions != null);
            _instructions = instructions;
            _nextBlockId = 0;
            EntryPoint = new ControlFlowNodeOld(_nextBlockId++, -1);
            RegularExit = new ControlFlowNodeOld(_nextBlockId++, -1);
            _nodes.Add(EntryPoint);
            _nodes.Add(RegularExit);
        }
        private ControlFlowEdgeOld createEdge(ControlFlowNodeOld fromNode, Instruction toInstruction)
        {
            ControlFlowNodeOld target = null;

            foreach (ControlFlowNodeOld node in _nodes)
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

        private ControlFlowEdgeOld createEdge(ControlFlowNodeOld fromNode, ControlFlowNodeOld toNode)
        {

            ControlFlowEdgeOld edge = fromNode.Outgoing.Find(o => o.Source == fromNode && o.Target == toNode);
            if (edge != null) return edge;
            foreach (ControlFlowEdgeOld existingEdge in fromNode.Outgoing)
            {
                if (existingEdge.Source == fromNode && existingEdge.Target == toNode) return existingEdge;
            }
            edge = new ControlFlowEdgeOld(fromNode, toNode);
            fromNode.Outgoing.Add(edge);
            toNode.Incomming.Add(edge);

            return edge;
        }
        private void createBranchControlFlow(ControlFlowNodeOld node, Instruction jump, Instruction target)
        {
            createEdge(node, target);  // We don't have exceptions in game maker whew
        }
        private void createReturnControlFlow(ControlFlowNodeOld node, Instruction end)
        {
            createEdge(node, RegularExit);
        }

        private void createNodes()
        {
            //
            // Step 2a: Find basic blocks and create nodes for them.
            //

            IList<Instruction> instructions = _instructions;

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
                _nodes.Add(new ControlFlowNodeOld(_nodes.Count, blockStart, instructions[i]));
            }
        }
        private void createRegularControlFlow()
        {
            //
            // Step 3: Create edges for the normal control flow (assuming no exceptions thrown).
            //

            IList<Instruction> instructions = _instructions;
            int last_pc = instructions.Last().Address;
            createEdge(EntryPoint, instructions[0]);

            foreach (ControlFlowNodeOld node in _nodes)
            {
                Instruction end = node.End;

                if (end == null) continue; //  || end.Address >= last_pc) continue;
                Instruction next = end.Next;
              
                //
                // Create normal edges from one instruction to the next.
                //
                if (end.Code != GMCode.B)
                    if (next != null)
                        createEdge(node, next);
                    else
                        createEdge(node, RegularExit); // no more instructions means we are exiting

                //
                // Create edges for branch instructions.
                //
                for (Instruction instruction = node.Start; instruction != null && instruction.Address <= end.Address; instruction = instruction.Next)
                {
                    if (!instruction.isBranch) continue;
                    Label l = instruction.Operand as Label;
                    if (l.Address > last_pc)
                        createEdge(node, RegularExit);
                    else
                        createBranchControlFlow(node, instruction, l.InstructionOrigin);
                }


                //
                // Create edges for return and leave instructions.
                //
                Label end_label = end.Operand as Label;
                // Documenting the conditions here cause it caused a few node errors
                // first end.GMCode == GMCode.Exit || end.GMCode == GMCode.Ret  is ovious
                // second (end.Next == null && end.GMCode != GMCode.B)  If the next op is null (no more opcodes) but NOT a Branch (like a loop) be sure NOT to link it to the return
                //  (end_label != null && end_label.Address > last_pc) If the operand is a label and goes beyond the function, thats an exit right there
                if (end.Code == GMCode.Exit || end.Code == GMCode.Ret || (end.Next == null && end.Code != GMCode.B) || (end_label != null && end_label.Address > last_pc)) createReturnControlFlow(node, end);
            }
        }
    }
}