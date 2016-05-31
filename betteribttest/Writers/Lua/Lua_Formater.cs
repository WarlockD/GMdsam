using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using GameMaker.Ast;

namespace GameMaker.Writers.Lua
{
    public class Formater : CodeFormater
    {
        string EnviromentOverride = null;
        public override void Write(ILSwitch f)
        {
            throw new Exception("Switch here, should not reach");
        }
        public override void Write(ILBasicBlock block)
        {
            throw new Exception("Should not run into basic block here");
        }

        public override void Write(ILElseIfChain chain)
        {
            for (int i = 0; i < chain.Conditions.Count; i++)
            {
                var c = chain.Conditions[i];
                if (i == 0) writer.Write("if ");
                writer.WriteNode(c.Condition);
                writer.Write(" then");
                writer.WriteLine();
                writer.Write(c.TrueBlock); // auto indent
                if (i < chain.Conditions.Count - 1) writer.Write("elseif ");
            }
            if (chain.Else != null && chain.Else.Body.Count > 0)
            {
                writer.WriteLine("else");
                writer.Write(chain.Else); // auto indent
            }
            writer.Write("end"); // Side note: ILBlock puts the writeline here
        }
        class ExpresionInfo
        {
            public readonly string Kind;
            public readonly int Args;
            public readonly int Precedence;
            public ExpresionInfo(string k, int args, int prec)
            {
                this.Kind = k;
                this.Args = args;
                this.Precedence = prec;
            }
        }
        static readonly Dictionary<GMCode, ExpresionInfo> GMCodeToLua = new Dictionary<GMCode, ExpresionInfo>()
        {
            { GMCode.Not,   new ExpresionInfo("not",1,7) }, // lua wierdnes
            { GMCode.Neg,   new ExpresionInfo("-",1,7) },
            { GMCode.Mul,   new ExpresionInfo("*",2,6) },
            { GMCode.Div,   new ExpresionInfo("/" ,2,6)},
            { GMCode.Mod,   new ExpresionInfo("%" ,2,6)},
            { GMCode.Add,   new ExpresionInfo("+",2,5)},
            { GMCode.Sub,   new ExpresionInfo("-",2,5)},
            { GMCode.Concat, new ExpresionInfo(".." ,2,4) } ,// lua wierdnes, watch prec
            // in lua, these have all the same prec
            { GMCode.Sne,   new ExpresionInfo("~=",2,3)  },
            { GMCode.Sge,   new ExpresionInfo(">=" ,2,3) },
            { GMCode.Slt,   new ExpresionInfo("<" ,2,3) },
            { GMCode.Sgt,   new ExpresionInfo(">" ,2,3) },
            { GMCode.Seq,   new ExpresionInfo("==" ,2,3) },
            { GMCode.Sle,   new ExpresionInfo("<=" ,2,3) },
            { GMCode.LogicAnd, new ExpresionInfo("and",2,2) },
            { GMCode.LogicOr, new ExpresionInfo("or",2,1) },
        };
        public override string GMCodeToString(GMCode c)
        {
            return GMCodeToLua[c].Kind;
        }
        // all this does is just check to see if the next tree is equal to the last tree of precidence
        // that is (4- 3) +3, the parms don't matter so don't print them, otherwise we need them
        protected override int Precedence(GMCode code)
        {
            switch (code)
            {
                case GMCode.Not:
                case GMCode.Neg:
                    return 7;
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                    return 6;
                case GMCode.Add:
                case GMCode.Sub:
                    return 5;
                // case GMCode.Pow: // not in gm
                case GMCode.Concat:
                    return 4;
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Slt:
                case GMCode.Sgt:
                case GMCode.Sne:
                case GMCode.Sle:
                    return 3;
                case GMCode.LogicAnd:
                    return 2;
                case GMCode.LogicOr:
                    return 1;
                default:
                    return 8;
            }
        }


        public override void Write(ILExpression expr)
        {
            ExpresionInfo info;
            if (GMCodeToLua.TryGetValue(expr.Code, out info)) {
                if (info.Args == 1)
                {
                    writer.Write(info.Kind);
                    if (expr.Code == GMCode.Not) writer.Write(' '); // add an extra space for not
                    WriteParm(expr, 0);
                } else if (info.Args == 2)
                {
                    WriteParm(expr, 0);
                    writer.Write(' ');
                    writer.Write(info.Kind);
                    writer.Write(' ');
                    WriteParm(expr, 1);
                }
            } else
            {
                //  if (Code.isExpression())
                //      WriteExpressionLua(output);
                // ok, big one here, important to get this right
                switch (expr.Code)
                {
                    case GMCode.Call:
                        writer.Write(expr.Operand as ILCall);
                        break;
                    case GMCode.Constant: // primitive c# type
                        writer.Write(expr.Operand as ILValue);
                        break;
                    case GMCode.Var:  // should be ILVariable
                        writer.Write(expr.Operand as ILVariable);
                        break;
                    case GMCode.Exit:
                        writer.Write("return -- exit");
                        break;
                    case GMCode.Ret:
                        writer.Write("return ");
                        writer.Write(expr.Arguments.Single());
                        break;
                    case GMCode.LoopOrSwitchBreak:
                        writer.Write("break");
                        break;
                    case GMCode.LoopContinue:
                        writer.Write("continue");
                        break;
                    case GMCode.Assign:
                        writer.Write(expr.Operand as ILVariable);
                        writer.Write(" = ");
                        writer.Write(expr.Arguments.Single());
                        break;
                    default:
                        throw new Exception("Not Implmented! ugh");
                }
            }
            if (expr.Comment != null) writer.Write("--[[ " + expr.Comment + "--]]");
        }

        //Write(expr.ToString()); // debug is the expressions to string
        static Regex ScriptArgRegex = new Regex(@"argument(\d+)", RegexOptions.Compiled);
        public override void Write(ILVariable v)
        {
            if (!v.isResolved) throw new Exception(v.FullName + " is not resolved");

            if (!v.isLocal && !v.isGenerated && !ScriptArgRegex.IsMatch(v.Name))
            {
                if (v.Instance is ILVariable) {
                    writer.Write(v.Instance as ILVariable);
                } else if (v.Instance is ILValue)
                {
                    ILValue value = v.Instance as ILValue;
                    if (EnviromentOverride != null)
                    {
                        int? instance = value.IntValue;
                        if (instance == null) throw new Exception(" instance is wierd");
                        if (instance == -1) writer.Write(EnviromentOverride);// self!
                        else throw new Exception(v.FullName + " instance is wierder = " + instance);
                    }
                    else if (v.InstanceName != null) writer.Write(v.InstanceName);
                    else throw new Exception("We got to have an instantname for a constant int");
                } else writer.WriteNode(v.Instance);

                //  else throw new Exception(info.ToString() + " instance bad?");
                writer.Write(".");
            }
            if (v.Name == "in")  // reserved word in lua, so we change it here
                v.Name = "_" + v.Name;
            writer.Write(v.Name);

            if (v.isArray)
            {
                writer.Write('[');
                if (v.Index.Code == GMCode.Array2D)
                {
                    writer.WriteNode(v.Index.Arguments[0]);
                    writer.Write("][");
                    writer.WriteNode(v.Index.Arguments[1]);
                }
                else writer.WriteNode(v.Index);
                writer.Write(']');
            }
        }
        public override void Write(ILValue v)
        {
            writer.Write(v.ToString());
        }
        public override void Write(ILLabel label)
        {
            throw new Exception("Should not run into labels here in lua");
        }
        static HashSet<string> ILCallMustSendSelf = new HashSet<string>()
        {
            "script_execute",
            "instance_destroy",
        };
        public override void Write(ILCall v)
        {
            if(v.FullTextOverride != null) // we havea ful name override so just write that
            {
                writer.Write(v.FullTextOverride); // have to assump its a proper function
            } else
            {
                string script_name;
                if (v.FunctionNameOverride != null)
                    script_name = v.FunctionNameOverride;
                else
                    script_name = v.Name;
                //   Debug.Assert(!script_name.Contains("scr_damagestandard"));
                writer.Write(script_name);
                bool need_comma = false;
                writer.Write('('); // self is normaly the first of eveything
                writer.WriteNodesComma(v.Arguments, need_comma);
                writer.Write(')');
            }
            if(v.Comment != null) writer.Write(" --[[ {0} --]]", v.Comment);

        }

        public override void Write(ILCondition condition)
        {
            writer.Write("if ");
            writer.Write(condition.Condition); // want to make sure we are using the debug
            writer.WriteLine(" then");
            writer.Write(condition.TrueBlock);
            if (condition.FalseBlock != null && condition.FalseBlock.Body.Count > 0)
            {
                writer.WriteLine("else");
                writer.Write(condition.FalseBlock);
            }
            writer.Write("end"); // Side note: ILBlock puts the writeline here
            return;
        }
        public override void Write(ILWhileLoop loop)
        {
            writer.Write("while ");
            writer.Write(loop.Condition); // want to make sure we are using the debug
            writer.WriteLine(" do");
            writer.Write(loop.BodyBlock);
            writer.Write("end"); // Side note: ILBlock puts the writeline here
        }
        static int localGen = 0;
        public override void Write(ILWithStatement with)
        {
            // UGH Now I see why you use withs
            // This cycles though EACH object named in this instance, so it really IS a loop
            // UNLESS its given an instance number, like from a var or call.  In that case its only doing that instance
            // the with function in code returns a ipairs table of either all the instances OR just the single instance
            // with must be able to tell the diffrence when a string, int or table is sent
            string localVar = "with_" + localGen++;  
            string env = Context.InstanceToString(with.Enviroment,writer);
            writer.Write("for _, {0} in with({1}) do", localVar, env);
            writer.WriteLine(" -- Enviroment: {0}", env);

            writer.Indent++;
            writer.WriteLine("local self = {0} -- change instance", localVar);
            writer.WriteNodes(with.Body.Body, true, false); // manualy write it
            writer.Indent--;
            writer.Write("end");
            writer.Write(" -- Enviroment: {0}", env);
        }


        public override string LineComment { get { return "--"; } }
        public override string BlockCommentStart { get { return "--[["; } }
        public override string BlockCommentEnd { get { return "--]]"; } }

        public override string NodeEnding
        {
            get
            {
                return null;
            }
        }
        public override string Extension { get { return "lua"; } }
    }
}
