using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace betteribttest.FlowAnalysis
{

	/// <summary>
	/// Constructs the Control Flow Graph from a Cecil method body.
	/// </summary>
	public sealed class ControlFlowGraphBuilder
    {
        public static ControlFlowGraph Build(MethodBody methodBody)
        {
            return new ControlFlowGraphBuilder(methodBody).Build();
        }

        // This option controls how finally blocks are handled:
        // false means that the endfinally instruction will jump to any of the leave targets (EndFinally edge type).
        // true means that a copy of the whole finally block is created for each leave target. In this case, each endfinally node will be connected with the leave
        //   target using a normal edge.


        MethodBody methodBody;
        int[] offsets; // array index = instruction index; value = IL offset
        bool[] hasIncomingJumps; // array index = instruction index
        List<ControlFlowNode> nodes = new List<ControlFlowNode>();
        ControlFlowNode entryPoint;
        ControlFlowNode regularExit;

        private ControlFlowGraphBuilder(MethodBody methodBody)
        {
            this.methodBody = methodBody;
            offsets = methodBody.Instructions.Select(i => i.Address).ToArray();
            hasIncomingJumps = new bool[methodBody.Instructions.Count];

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
        public ControlFlowGraph Build()
        {
            CalculateHasIncomingJumps();
            CreateNodes();
            CreateRegularControlFlow();
            /*
            CreateExceptionalControlFlow();
            if (copyFinallyBlocks)
                CopyFinallyBlocksIntoLeaveEdges();
            else
                TransformLeaveEdges();
                */
            return new ControlFlowGraph(nodes.ToArray());
        }

        #region Step 1: calculate which instructions are the targets of jump instructions.
        void CalculateHasIncomingJumps()
        {
            Instruction last = methodBody.Instructions.Last();
            foreach (Instruction inst in methodBody.Instructions)
            {
                Label l = inst.Operand as Label;
                // we ignore popenv as a jump as its more of a marker on the start of the enviroment
                if (l!=null && last.Address >= l.Address)
                {
                    hasIncomingJumps[GetInstructionIndex((Label)inst.Operand)] = true;
                }
                else if (inst.Operand is Instruction) // I think this is a better idea might go this way
                {
                    hasIncomingJumps[GetInstructionIndex((Instruction)inst.Operand)] = true;
                }
            }
        }
        #endregion
        SortedList<int, ControlFlowNode> PushEnviromentStarts;
        SortedList<int, ControlFlowNode> PopEnviromentEnds;
        #region Step 2: create nodes
        void CreateNodes()
        {
            PushEnviromentStarts = new SortedList<int, ControlFlowNode>();
            PopEnviromentEnds = new SortedList<int, ControlFlowNode>();
            // Step 2a: find basic blocks and create nodes for them
            for (int i = 0; i < methodBody.Instructions.Count; i++)
            {
                Instruction blockStart = methodBody.Instructions[i];
              //  ExceptionHandler blockStartEH = FindInnermostExceptionHandler(blockStart.Offset);
                // try and see how big we can make that block:
                for (; i + 1 < methodBody.Instructions.Count; i++)
                {
                    Instruction inst = methodBody.Instructions[i];
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
                var node = new ControlFlowNode(nodes.Count, blockStart, methodBody.Instructions[i]);
                nodes.Add(node);
            }
        }
        #endregion

        #region Step 3: create edges for the normal flow of control (assuming no exceptions thrown)
        void CreateRegularControlFlow()
        {
            Instruction last = methodBody.Instructions.Last();
            CreateEdge(entryPoint, methodBody.Instructions[0], JumpType.Normal);
            Action<ControlFlowNode> NextInstructionEdge = (ControlFlowNode node) =>{
                if (node.End.Next == null) CreateEdge(node, regularExit, JumpType.Normal);
                else CreateEdge(node, node.End.Next, JumpType.Normal);
            };
            foreach (ControlFlowNode node in nodes)
            {
                //Debug.Assert(node.BlockIndex != 93);
                if (node.End != null)
                {
                    var code = node.End.Code;
                    Label operandLabel = node.End.Operand as Label;



                    switch (code)
                    {
                        case GMCode.Pushenv:  // jump out of enviroment
                            CreateEdge(node, node.End.Next, JumpType.PushEnviroment);
                            break;
                        case GMCode.Popenv:
                            // jump out of enviroment
                            //    Debug.Assert(operandLabel != null); // figure out breaks
                            if (node.End.Next == null) // bug, meh
                                CreateEdge(node, regularExit, JumpType.PopEnviroment);
                            else
                                CreateEdge(node, node.End.Next, JumpType.PopEnviroment);
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

 

        /// <summary>
        /// Creates a copy of all nodes pointing to 'end' and replaces those references with references to 'newEnd'.
        /// Nodes pointing to the copied node are copied recursively to update those references, too.
        /// This recursion stops at 'start'. The modified version of start is returned.
        /// </summary>
        ControlFlowNode CopyFinallySubGraph(ControlFlowNode start, ControlFlowNode end, ControlFlowNode newEnd)
        {
            return new CopyFinallySubGraphLogic(this, start, end, newEnd).CopyFinallySubGraph();
        }

        class CopyFinallySubGraphLogic
        {
            readonly ControlFlowGraphBuilder builder;
            readonly Dictionary<ControlFlowNode, ControlFlowNode> oldToNew = new Dictionary<ControlFlowNode, ControlFlowNode>();
            readonly ControlFlowNode start;
            readonly ControlFlowNode end;
            readonly ControlFlowNode newEnd;

            public CopyFinallySubGraphLogic(ControlFlowGraphBuilder builder, ControlFlowNode start, ControlFlowNode end, ControlFlowNode newEnd)
            {
                this.builder = builder;
                this.start = start;
                this.end = end;
                this.newEnd = newEnd;
            }

            internal ControlFlowNode CopyFinallySubGraph()
            {
                foreach (ControlFlowNode n in end.Predecessors)
                {
                    CollectNodes(n);
                }
                foreach (var pair in oldToNew)
                    ReconstructEdges(pair.Key, pair.Value);
                return GetNew(start);
            }

            void CollectNodes(ControlFlowNode node)
            {
                if (node == end || node == newEnd)
                    throw new InvalidOperationException("unexpected cycle involving finally construct");
                if (!oldToNew.ContainsKey(node))
                {
                    int newBlockIndex = builder.nodes.Count;
                    ControlFlowNode copy;
                    switch (node.NodeType)
                    {
                        case ControlFlowNodeType.Normal:
                            copy = new ControlFlowNode(newBlockIndex, node.Start, node.End);
                            break;
                    //    case ControlFlowNodeType.FinallyOrFaultHandler:
                    //        copy = new ControlFlowNode(newBlockIndex, node.ExceptionHandler, node.EndFinallyOrFaultNode);
                     //       break;
                        default:
                            // other nodes shouldn't occur when copying finally blocks
                            throw new NotSupportedException(node.NodeType.ToString());
                    }
                    copy.CopyFrom = node;
                    builder.nodes.Add(copy);
                    oldToNew.Add(node, copy);

                    if (node != start)
                    {
                        foreach (ControlFlowNode n in node.Predecessors)
                        {
                            CollectNodes(n);
                        }
                    }
                }
            }

            void ReconstructEdges(ControlFlowNode oldNode, ControlFlowNode newNode)
            {
                foreach (ControlFlowEdge oldEdge in oldNode.Outgoing)
                {
                    builder.CreateEdge(newNode, GetNew(oldEdge.Target), oldEdge.Type);
                }
            }

            ControlFlowNode GetNew(ControlFlowNode oldNode)
            {
                if (oldNode == end)
                    return newEnd;
                ControlFlowNode newNode;
                if (oldToNew.TryGetValue(oldNode, out newNode))
                    return newNode;
                return oldNode;
            }
        }

        #region CreateEdge methods
        void CreateEdge(ControlFlowNode fromNode, Instruction toInstruction, JumpType type)
        {
            CreateEdge(fromNode, nodes.Single(n => n.Start == toInstruction), type);
        }
        void CreateEdge(ControlFlowNode fromNode, Label toLabel, JumpType type)
        {
            Instruction last = methodBody.Instructions.Last();
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
