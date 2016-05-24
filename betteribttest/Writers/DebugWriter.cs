using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameMaker.Ast;

namespace GameMaker.Writers
{
    public class DebugFormater : ICodeFormater 
    {
        public ICodeFormater Clone()
        {
            return new DebugFormater();
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
        void WriteField<T>(string field, T n) where T: ILNode
        {
            writer.Write(" ,");
            writer.Write(field);
            writer.Write('=');
            writer.WriteNode(n,false);
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
        public void Write(ILSwitch f)
        {
            writer.WriteLine("ILSwitch Condition={0}", f.Condition);
            writer.Indent++;
            foreach (var c in f.Cases)
            {
                writer.Write("ILSwitch.ILCase Value=");
                writer.WriteNodesComma(c.Values);
                writer.WriteLine();
                writer.Write((ILBlock)c);
            }
            if(f.Default != null && f.Default.Body.Count > 0)
            {
                writer.WriteLine();
                writer.WriteLine("ILSwitch Default", f.Condition);
                writer.Write(f.Default);
            }
            writer.Indent--;
        }
        public  void Write(ILElseIfChain chain)
        {
            
            for(int i=0; i < chain.Conditions.Count; i++)
            {
                var c = chain.Conditions[i];
                if(i == 0) writer.Write("IFElseChain If ");
                writer.Write(c.Condition);
                writer.WriteLine();
                writer.Write(c.TrueBlock); // auto indent
                if (i < chain.Conditions.Count - 1) writer.Write("IFElseChain ElseIf ");
            }
            if(chain.Else != null && chain.Else.Body.Count > 0)
            {
                writer.WriteLine("IFElseChain Else");
                writer.Write(chain.Else); // auto indent
            }
            writer.WriteLine("IFElseChain End ");
        }


        public void Write(ILExpression expr)
        {
            writer.Write("{ Code=");
            writer.Write(expr.Code.ToString());
            //  if (Code.isExpression())
            //      WriteExpressionLua(output);
            // ok, big one here, important to get this right
            if(expr.Operand != null) writer.Write(" , Operand=");
            switch (expr.Code)
            {
                case GMCode.Call:
                    if (expr.Operand is string)
                    {
                        writer.Write(" , FuncName=");
                        writer.Write(expr.Operand as string);
                    }
                    else writer.Write(expr.Operand as ILCall);
                    break;
                case GMCode.Constant: // primitive c# type
                    if (expr.Operand != null) writer.Write(expr.Operand as ILValue);
                    break;
                case GMCode.Var:  // should be ILVariable
                    if (expr.Operand != null) writer.Write(expr.Operand as ILVariable);
                    break;
                case GMCode.B:
                case GMCode.Bf:
                case GMCode.Bt:
                    if (expr.Operand != null) writer.Write(expr.Operand as ILLabel);
                    break;
                default:
                    if (expr.Operand != null)
                        writer.Write(expr.Operand.ToString());
                    break;
                 
            }
            if (expr.Arguments.Count > 0)
            {
                writer.Write(" ,Arguments=");
                foreach (var e in expr.Arguments) writer.Write(e);
            }
            writer.Write(" }");
        }
        public void Write(ILVariable v)
        {
            writer.Write("{ Name=");
            writer.Write(v.Name);
            if (!v.isResolved)
            {
                writer.Write("?");
                if (v.isArray) writer.Write(" ,isArray=true");
            }
            if (v.Instance != null) WriteField("Instance", v.Instance);
            if (v.Index != null) WriteField("Index", v.Index);
            writer.Write(" }");
        }
        public  void Write(ILValue v)
        {
            writer.Write(v.ToString());
        }
        public  void Write(ILLabel label)
        {
            writer.Write(":{0}:", label.Name);
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
