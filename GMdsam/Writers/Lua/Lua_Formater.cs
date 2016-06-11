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
    public class Formater : BlockToCode
    {
        string EnviromentOverride = null;
        public override void Write(ILSwitch f)
        {
            throw new Exception("Switch here, should not reach");
        }
        public Formater(IMessages error, bool catch_infos = false) : base(error, catch_infos) { }
        public Formater(IMessages error, string filename, bool catch_infos = false) : base(error, filename, catch_infos) { }
        public Formater(string filename, bool catch_infos = false) : base(filename, catch_infos) { }
        public Formater(IMessages error, TextWriter writer, bool catch_infos = false) : base(error, writer, catch_infos) { }
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

            switch (expr.Code)
            {
                case GMCode.Not: WritePreExpresion("not ", expr); break;
                case GMCode.Neg: WritePreExpresion("-", expr); break;
                case GMCode.Mul: WriteTreeExpression("*", expr); break;
                case GMCode.Div: WriteTreeExpression("/", expr); break;
                case GMCode.Mod: WriteTreeExpression("%", expr); break;
                case GMCode.Add: WriteTreeExpression("+", expr); break;
                case GMCode.Sub: WriteTreeExpression("-", expr); break;
                case GMCode.Concat: WriteTreeExpression("+", expr); break;
                // in lua, these have all the same prec
                case GMCode.Sne: WriteTreeExpression("~=", expr); break;
                case GMCode.Sge: WriteTreeExpression(">=", expr); break;
                case GMCode.Slt: WriteTreeExpression("<", expr); break;
                case GMCode.Sgt: WriteTreeExpression(">", expr); break;
                case GMCode.Seq: WriteTreeExpression("==", expr); break;
                case GMCode.Sle: WriteTreeExpression("<=", expr); break;
                case GMCode.LogicAnd: WriteTreeExpression(" and ", expr); break;
                case GMCode.LogicOr: WriteTreeExpression(" or ", expr); break;
                case GMCode.Call:
                    Write(expr.Operand as ILCall);
                    break;
                case GMCode.Constant: // primitive c# type
                    Write(expr.Operand as ILValue);
                    break;
                case GMCode.Var:  // should be ILVariable
                    Write(expr.Operand as ILVariable);
                    break;
                case GMCode.Exit:
                    Write("return -- exit");
                    break;
                case GMCode.Ret:
                    Write("return ");
                    Write(expr.Arguments.Single());
                    break;
                case GMCode.LoopOrSwitchBreak:
                    Write("break");
                    break;
                case GMCode.LoopContinue:
                    Write("continue");
                    break;
                case GMCode.Assign:
                    Write(expr.Operand as ILVariable);
                    Write(" = ");
                    Write(expr.Arguments.Single());
                    break;
                default:
                    throw new Exception("Not Implmented! ugh");
            }
        }
    

        //Write(expr.ToString()); // debug is the expressions to string
        static Regex ScriptArgRegex = new Regex(@"argument(\d+)", RegexOptions.Compiled);
        public override void Write(ILVariable v)
        {
            if (!v.isResolved) throw new Exception(v.FullName + " is not resolved");

            if (!v.isLocal && !v.isGenerated && !ScriptArgRegex.IsMatch(v.Name))
            {
                if (v.Instance is ILVariable) {
                    Write(v.Instance as ILVariable);
                } else if (v.Instance is ILValue)
                {
                    ILValue value = v.Instance as ILValue;
                    if (EnviromentOverride != null)
                    {
                        int? instance = value.IntValue;
                        if (instance == null) throw new Exception(" instance is wierd");
                        if (instance == -1) Write(EnviromentOverride);// self!
                        else throw new Exception(v.FullName + " instance is wierder = " + instance);
                    }
                    else if (v.InstanceName != null) Write(v.InstanceName);
                    else throw new Exception("We got to have an instantname for a constant int");
                } else Write(v.Instance);

                //  else throw new Exception(info.ToString() + " instance bad?");
                Write(".");
            }
            if (v.Name == "in")  // reserved word in lua, so we change it here
                v.Name = "_" + v.Name;
            Write(v.Name);

            if (v.isArray)
            {
                Write('[');
                if (v.Index.Code == GMCode.Array2D)
                {
                    Write(v.Index.Arguments[0]);
                    Write("][");
                    Write(v.Index.Arguments[1]);
                }
                else Write(v.Index);
                Write(']');
            }
        }
        public override void Write(ILValue v)
        {
            Write(v.ToString());
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
                Write(v.FullTextOverride); // have to assump its a proper function
            } else
            {
                string script_name;
                if (v.FunctionNameOverride != null)
                    script_name = v.FunctionNameOverride;
                else
                    script_name = v.Name;
                Write(script_name);
                Write('('); // self is normaly the first of eveything
                WriteNodesComma(v.Arguments);
                Write(')');
            }
            if(v.Comment != null) Write(" --[[ {0} --]]", v.Comment);

        }

        public override void Write(ILCondition condition)
        {
            Write("if ");
            Write(condition.Condition); // want to make sure we are using the debug
            WriteLine(" then");
            Write(condition.TrueBlock);
            if (condition.FalseBlock != null && condition.FalseBlock.Body.Count > 0)
            {
                WriteLine("else");
                Write(condition.FalseBlock);
            }
            Write("end"); // Side note: ILBlock puts the writeline here
            return;
        }
        public override void Write(ILWhileLoop loop)
        {
            Write("while ");
            Write(loop.Condition); // want to make sure we are using the debug
            WriteLine(" do");
            Write(loop.BodyBlock);
            Write("end"); // Side note: ILBlock puts the writeline here
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
            string env = Context.InstanceToString(with.Enviroment,this);
            Write("for _, {0} in with({1}) do", localVar, env);
            WriteLine(" -- Enviroment: {0}", env);

            Indent++;
            WriteLine("local self = {0} -- change instance", localVar);
            Write(with.Body, false); // manualy write it
            Indent--;
            Write("end");
            Write(" -- Enviroment: {0}", env);
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
