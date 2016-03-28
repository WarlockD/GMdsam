using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betteribttest.FlowAnalysis
{
    class AstGraphBuilder
    {

        readonly ControlFlowGraph cfg;

        AstBlock[] blocks; // array index = block index
        Stack<Ast>[] stacks;
        Decompile decompile;
        StatementBlock finalBlock = null;
        public static StatementBlock BuildAst(ControlFlowGraph cfg, Decompile decompile)
        {
            AstGraphBuilder builder = new AstGraphBuilder(cfg, decompile);
            builder.Build();
            return builder.finalBlock;
        }
        AstGraphBuilder(ControlFlowGraph cfg, Decompile decompile)
        {
            this.cfg = cfg;
            this.decompile = decompile;
            this.blocks = new AstBlock[cfg.Nodes.Count];
            stacks = new Stack<Ast>[cfg.Nodes.Count];
        }
        void CreateGraphStructure()
        {
            for (int i = 0; i < blocks.Length; i++) blocks[i] = new AstBlock(cfg.Nodes[i]);
            for (int i = 0; i < blocks.Length; i++)
            {
                foreach (ControlFlowNode node in cfg.Nodes[i].Successors)
                {
                    blocks[i].Successors.Add(blocks[node.BlockIndex]);
                    blocks[node.BlockIndex].Predecessors.Add(blocks[i]);
                }
            }
        }
        void Build()
        {
            CreateGraphStructure();
            cfg.ResetVisited();
            decompile.SetUpDecompiler();
            cfg.Nodes[0].TraversePreOrder(x => x.Successors, (ControlFlowNode node) =>
              {
                  int blockIndex = node.BlockIndex;
                  Stack<Ast> stack = stacks[blockIndex];
                  if (stack == null) stacks[blockIndex] = stack = new Stack<Ast>();
                  if (node.Start != null) node.Block = decompile.ConvertManyStatements(node.Start, node.End, stack);
                  foreach (ControlFlowEdge edge in node.Outgoing)
                  {
                      var edgeBlock = blocks[edge.Target.BlockIndex];
                      stacks[blockIndex] = new Stack<Ast>(stack);
                  }

              });
            finalBlock = new StatementBlock();
            List<AstStatement> all = new List<AstStatement>();
            foreach(var node in cfg.Nodes)
            {
                if(node != cfg.EntryPoint && node != cfg.RegularExit) all.AddRange(node.Block);
            }
            decompile.ExtraLabels(all);
            finalBlock = new StatementBlock(all);
        }
    }
}
