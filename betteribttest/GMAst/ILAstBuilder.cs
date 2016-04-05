using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using betteribttest.FlowAnalysis;
using System.Diagnostics;

namespace betteribttest.GMAst
{
    public class ILAstBuilder
    {
        SortedList<int, ControlFlowNode> PushEnviromentStarts; // might not use
        SortedList<int, ControlFlowNode> PopEnviromentEnds;   // might not use, may simplify instructions though
        int[] offsets; // array index = instruction index; value = IL offset
        bool[] hasIncomingJumps; // array index = instruction index
        List<Instruction> instructions;
        List<ControlFlowNode> nodes = new List<ControlFlowNode>();
        ControlFlowNode entryPoint;
        ControlFlowNode regularExit;
        ControlFlowGraph graph;
        ILDecompile dn;
        ILAstBuilder(List<Instruction> instructions, ILDecompile dn)
        {
            this.dn = dn; 
            this.instructions = instructions;
            offsets = instructions.Select(i => i.Address).ToArray();
            hasIncomingJumps = new bool[instructions.Count];

            entryPoint = new ControlFlowNode(0, 0, ControlFlowNodeType.EntryPoint);
            nodes.Add(entryPoint);
            regularExit = new ControlFlowNode(1, -1, ControlFlowNodeType.RegularExit);
            nodes.Add(regularExit);
            Debug.Assert(nodes.Count == 2);
        }
        /// <summary>
        /// Determines the index of the instruction (for use with the hasIncomingJumps array)
        /// </summary>
        int GetInstructionIndex(Instruction inst)
        {
            int index = Array.BinarySearch(offsets, inst.Address);
            Debug.Assert(index >= 0);
            return index;
        }
        int GetInstructionIndex(Label label)
        {
            int index = Array.BinarySearch(offsets, label.Address);
            Debug.Assert(index >= 0);
            return index;
        }
        /// <summary>
        /// Builds the ControlFlowGraph.
        /// </summary>
        public ControlFlowGraph BuildGraph()
        {
            CalculateHasIncomingJumps();
            CreateNodes();
            CreateRegularControlFlow();
            graph = new ControlFlowGraph(nodes.ToArray());
            StackExpresionEvaluation();
            
            return graph;
        }
        void StackExpresionEvaluation()
        {

        }
        #region Step 1: calculate which instructions are the targets of jump instructions.
        void CalculateHasIncomingJumps()
        {
            Instruction last = instructions.Last();
            foreach (Instruction inst in instructions)
            {
                Label l = inst.Operand as Label;
                // we ignore popenv as a jump as its more of a marker on the start of the enviroment
                if (l != null && last.Address >= l.Address)
                {
                    hasIncomingJumps[GetInstructionIndex((Label)inst.Operand)] = true;
                }
            }
        }
        #endregion
   
        #region Step 2: create nodes
        void CreateNodes()
        {
            PushEnviromentStarts = new SortedList<int, ControlFlowNode>();
            PopEnviromentEnds = new SortedList<int, ControlFlowNode>();
            // Step 2a: find basic blocks and create nodes for them
            for (int i = 0; i < instructions.Count; i++)
            {
                Instruction blockStart = instructions[i];
                //  ExceptionHandler blockStartEH = FindInnermostExceptionHandler(blockStart.Offset);
                // try and see how big we can make that block:
                for (; i + 1 < instructions.Count; i++)
                {
                    Instruction inst = instructions[i];
                    if (IsBranch(inst.Code) || inst.Code == GMCode.Pushenv || inst.Code == GMCode.Popenv)
                        break;
                    // HACK.  popenv should be the last statment but the way the code dissasembles
                    // the node builder would of put it as its own statment.
                    // This hack skips the jump check so it gets in there right
                    // not sure how this will work on popenv breaks though
                    //  if (inst.Next != null && inst.Next.Code == GMCode.Popenv) continue; 
                    if (hasIncomingJumps[i + 1])
                        break;
                }
                var node = new ControlFlowNode(nodes.Count, blockStart, instructions[i]);
                nodes.Add(node);
            }
        }
        #endregion

        #region Step 3: create edges for the normal flow of control (assuming no exceptions thrown)
        ControlFlowNode FindParrentPushEnv(ControlFlowNode node)
        {
            foreach(var n in node.Predecessors)
            {
                if (n.End.Code == GMCode.Pushenv) return n;
                else return FindParrentPushEnv(n);
            }
            throw new Exception("Pop without a push?");
        }
        void CreatePopEdge(ControlFlowNode node)
        {
            if (node.End.Next == null) // bug, meh
                CreateEdge(node, regularExit, JumpType.PopEnviroment);
            else
            {
                CreateEdge(node, node.End.Next, JumpType.PopEnviroment);
            }
        }
        void CreateRegularControlFlow()
        {
            Instruction last = instructions.Last();
            SortedList<int, ControlFlowNode> pushEnvToOffset = new SortedList<int, ControlFlowNode>();
            CreateEdge(entryPoint, instructions[0], JumpType.Normal);
            Action<ControlFlowNode> NextInstructionEdge = (ControlFlowNode node) =>
            {
                if (node.End.Next == null) CreateEdge(node, regularExit, JumpType.Normal);
                else CreateEdge(node, node.End.Next, JumpType.Normal);
            };
            foreach (ControlFlowNode node in nodes)
            {
                //Debug.Assert(node.BlockIndex != 93);
                if (node.End != null)
                {
                    var code = node.End.Code;
                    var inst = node.End;
                    Label operandLabel = node.End.Operand as Label;



                    switch (code)
                    {
                        case GMCode.Pushenv:  // jump out of enviroment
                            CreateEdge(node, node.End.Next, JumpType.PushEnviroment);
                            pushEnvToOffset.Add(inst.BranchDesitation, node);
                            break;
                        case GMCode.Popenv:
                            // jump out of enviroment, check if its linked
                            {
                                ControlFlowNode pushNode;
                                if ((inst.OpCode & 0xFFFF) == 0) {
                                    // its a leave instruction  
                                    // BUG: If the push statement was not defined before this, it screw up
                                    // not sure how to fix it and I have yet to see this happen yet
                                    pushNode = pushEnvToOffset.Last().Value;
                                    if (pushNode.PopEnvNodes != null) pushNode.PopEnvNodes = new List<ControlFlowNode>();
                                    pushNode.PopEnvNodes.Add(node); // we save it to fix it latter till we find the normal pop out
                                } else
                                {
                                    if (!pushEnvToOffset.TryGetValue(inst.BranchDesitation - 1, out pushNode)) throw new Exception("No push?");
                                    if (pushNode.PopEnvNodes != null) foreach (var n in pushNode.PopEnvNodes) CreatePopEdge(n);
                                    CreatePopEdge(node);
                                    //nodes.Single(n => n.Start != null && n.Start.Address == toLabel.Address)
                                }
                            }         
                            break;
                        case GMCode.Bt:
                        case GMCode.Bf:
                            NextInstructionEdge(node);
                            goto case GMCode.B;
                        case GMCode.B:
                            CreateEdge(node, operandLabel, JumpType.Normal);
                            break;
                        case GMCode.Exit:
                        case GMCode.Ret:
                            CreateEdge(node, regularExit, JumpType.Normal);
                            break;
                        default:
                            NextInstructionEdge(node);
                            break;
                    }

                }
            }
        }
        #endregion

        #region CreateEdge methods
        void CreateEdge(ControlFlowNode fromNode, Instruction toInstruction, JumpType type)
        {
            CreateEdge(fromNode, nodes.Single(n => n.Start == toInstruction), type);
        }
        void CreateEdge(ControlFlowNode fromNode, Label toLabel, JumpType type)
        {
            Instruction last = instructions.Last();
            if (toLabel.Address > last.Address)
                CreateEdge(fromNode, regularExit, type);
            else
                CreateEdge(fromNode, nodes.Single(n => n.Start != null && n.Start.Address == toLabel.Address), type);
        }
        void CreateEdge(ControlFlowNode fromNode, ControlFlowNode toNode, JumpType type)
        {
            ControlFlowEdge edge = new ControlFlowEdge(fromNode, toNode, type);
            fromNode.Outgoing.Add(edge);
            toNode.Incoming.Add(edge);
        }
        #endregion

        #region OpCode info

        static bool IsBranch(GMCode opcode)
        {
            switch (opcode)
            {
                case GMCode.B:
                case GMCode.Bf:
                case GMCode.Bt:
                case GMCode.Exit:
                case GMCode.Ret:
                    return true;
                default:
                    return false;
            }
        }
        #endregion
    }
}