using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace GameMaker.Writers
{
    public class DebugFormater : ICodeFormater 
    {
        public ICodeFormater Clone()
        {
            return new DebugFormater();
        }
        public static BlockToCode Create(string filename)
        {
            return new BlockToCode(new DebugFormater(), filename);
        }
        BlockToCode writer = null;

        public string LineComment
        {
            get
            {
                return "//";
            }
        }

        public string NodeEnding
        {
            get
            {
                return null;
            }
        }

        public void SetStream(BlockToCode writer)
        {
            this.writer = writer;
        }
        public void Write(ILBasicBlock block)
        {
            ILLabel start = block.Body.First() as ILLabel;
            ILExpression end = block.Body.Last() as ILExpression;
            writer.WriteLine("BasicBlock Entry={0}", start.ToString());
            for (int i = 1; i < block.Body.Count - 1; i++)
                writer.WriteNode(block.Body[i], true);
            if (end != null)
                writer.WriteLine("BasicBlock Exit={0}", end.Code == GMCode.B ? end.Operand.ToString() : end.ToString());
            else
                writer.WriteNode(block.Body.Last()); 
            // The if and loop replace the last goto, so watch for that on basic blocks
        }
        public void Write(ILFakeSwitch f)
        {
            writer.WriteLine("ILFakeSwitch Condition={0}", f.Condition);
            writer.Indent++;
            foreach(var c in f.Cases)
            {
                writer.Write("ILFakeSwitch.ILCase Value=");
                writer.Write(c.Value);
                writer.Write(" Goto=");
                writer.Write(c.Goto);
                writer.WriteLine();
            }
            writer.Indent--;
        }
        public  void Write(ILElseIfChain chain)
        {
            
            for(int i=0; i < chain.Conditions.Count; i++)
            {
                var c = chain.Conditions[i];
                if(i == 0) writer.Write("IFElseChain If ");
                writer.WriteNode(c.Condition);
                writer.WriteLine();
                writer.WriteNode(c.TrueBlock); // auto indent
                if (i < chain.Conditions.Count - 1) writer.Write("IFElseChain ElseIf ");
            }
            if(chain.Else != null && chain.Else.Body.Count > 0)
            {
                writer.WriteLine("IFElseChain Else");
                writer.WriteNode(chain.Else); // auto indent
            }
            writer.WriteLine("IFElseChain End ");
        }

        public void Write(ILExpression expr)
        {
            writer.Write(expr.ToString()); // debug is the expressions to string
        }
        public void Write(ILVariable v)
        {
            writer.Write(v.ToString());
        }
        public  void Write(ILValue v)
        {
            writer.Write(v.ToString());
        }
        public  void Write(ILLabel label)
        {
            writer.Write("ILabel {0} ", label.ToString());
        }
        public  void Write(ILCall v)
        {
            writer.Write("ILCall?");
        }
        public  void Write(ILAssign assign)
        {
            writer.Write("ILAssign ");
            writer.Write(assign.Variable);
            writer.Write(" = ");
            writer.Write(assign.Expression); // want to make sure we are using the debug
        }
        public  void Write(ILCondition condition)
        {
            writer.Write("ILCondition If ");
            writer.Write(condition.Condition); // want to make sure we are using the debug
            writer.WriteLine("then");
            writer.Write(condition.TrueBlock);
            if (condition.FalseBlock != null && condition.FalseBlock.Body.Count > 0)
            {
                writer.WriteLine("else");
                writer.Write(condition.FalseBlock);
            }
            writer.WriteLine("ILCondition end");
        }
        public  void Write(ILWhileLoop loop)
        {
            writer.Write("ILWhileLoop If ");
            writer.Write(loop.Condition); // want to make sure we are using the debug
            writer.WriteLine(" do");
            writer.Write(loop.BodyBlock);
            writer.WriteLine("ILWhileLoop end");
        }
        public  void Write(ILWithStatement with)
        {
            writer.Write("ILWithStatement with ");
            writer.Write(with.Enviroment); // want to make sure we are using the debug
            writer.WriteLine(" do");
            writer.Write(with.Body);
            writer.WriteLine("ILWithStatement end");
        }
        public string Extension { get { return "_d.txt"; } }

    }
}
