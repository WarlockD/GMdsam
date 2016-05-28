using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using GameMaker.Ast;

namespace GameMaker.Writers.Lua
{
    public class Mutater : INodeMutater
    {
        public INodeMutater Clone()
        {
            return new Mutater();
        }
        BlockToCode output;
        public void SetStream(BlockToCode output)
        {
            if(output == null)
            {
                this.output = null;
            }
            else
            {
                this.output = output;
            }
          
        }
        static void spriteArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg))
                arg.ValueText = "\"" + File.Sprites[(int) arg.Value].Name + "\"";

        }
        static  void ordArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg))
            {
                char c = (char) (int) arg.Value;
                if (char.IsControl(c))
                    arg.ValueText = "\'\\x" + ((int) arg.Value).ToString("X2") + "\'";
                else
                    arg.ValueText = "\'" + c + "\'";
            }
        }
        static void soundArgument(ILNode expr)
        {

            ILValue arg;
            if (expr.MatchIntConstant(out arg) && (int) arg.Value != -1)
            {
                int instance = (int) arg.Value;
                arg.ValueText = "\"" + File.Sounds[instance].Name + "\"";
            }
        }
        static void instanceArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg) && (int) arg.Value != -1)
                arg.ValueText = "\"" + File.Objects[(int) arg.Value].Name + "\"";
        }
        static void fontArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg) && (int) arg.Value != -1)
                arg.ValueText = "\"" + File.Fonts[(int) arg.Value].Name + "\"";

        }
        // This just makes color look easyer to read
        static void colorArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg))
            {
                int color = (int) arg.Value;
                byte red = (byte) (color & 0xFF);
                byte green = (byte) (color >> 8 & 0xFF);
                byte blue = (byte) (color >> 16 & 0xFF);
                arg.ValueText = "{ " + red + ", " + green + ", " + blue + " }";
            }
        }
        static void scriptArgument(ILNode expr)
        {
            ILValue arg;
            if (expr.MatchIntConstant(out arg) && (int) arg.Value != -1)
                arg.ValueText = "\"" + File.Scripts[(int) arg.Value].Name + "\"";
        }
        static Dictionary<string, Action<ILCall, BlockToCode>> FunctionFix = new Dictionary<string, Action<ILCall, BlockToCode>>();
        static Dictionary<string, Action<ILCall, BlockToCode>> undertale_script_fix = new Dictionary<string, Action<ILCall, BlockToCode>>();
        static void AddStandardLuaConversions()
        {
            FunctionFix.Add("floor", (x,o) => x.Name = "math.floor");
            FunctionFix.Add("random", (x, o) => {
                // Have to change the way because of the way lua does it
                // GM, when you do "random(3)" is a random FLOAT between 0 and 3, not int
             //   StringBuilder sb = new StringBuilder();
             //   sb.Append("(math.random() + math.random(0,");
            //    sb.Append(o.NodeToString(x.Arguments.Single()));
           //     sb.Append("))");
            //    x.FullTextOverride = sb.ToString();
                });
            FunctionFix.Add("randomize", (x, o) => {
                x.Name = "math.randomseed";
                ILValue v = new ILValue("os.time()");
                v.ValueText = "os.time()";
                x.Arguments.Add(new ILExpression(GMCode.Constant, v));
           });
            FunctionFix.Add("string", (x, o) => x.Name = "tostring");
            FunctionFix.Add("real", (x, o) => x.Name = "tonumber");
        }
        static void AddUndertaleFixes() // remember self is at 0
        {
            undertale_script_fix.Add("SCR_TEXTSETUP", (x, o) =>
            {
                fontArgument(x.Arguments[1]);
                colorArgument(x.Arguments[2]);
            });
        }
        static Mutater()
        {
            AddStandardLuaConversions();
            AddUndertaleFixes();

       
            FunctionFix.Add("instance_create", (x, o) => instanceArgument(x.Arguments[2]));
            FunctionFix.Add("instance_exists", (x, o) => instanceArgument(x.Arguments[0]));
            FunctionFix.Add("script_execute", (x, o) => 
            {
                // we rename it to the function and add self to it
                ILValue v = x.Arguments[0].Operand as ILValue;
                x.Name = File.Scripts[(int)v].Name;
                Action<ILCall, BlockToCode> func;
                x.Arguments[0] = ILExpression.MakeVariable("self");
                if (undertale_script_fix.TryGetValue(x.Name, out func)) func(x, o); 
            });
            FunctionFix.Add("instance_destroy", (x, o) =>
            {
                if (x.Arguments.Count == 0)
                    x.Arguments.Add(ILExpression.MakeVariable("self"));
            });
            FunctionFix.Add("collision_line", (x, o) => instanceArgument(x.Arguments[3]));
            FunctionFix.Add("draw_sprite", (x, o) => spriteArgument(x.Arguments[0]));
            FunctionFix.Add("draw_sprite_ext", (x, o) => spriteArgument(x.Arguments[0]));
            FunctionFix.Add("snd_stop", (x, o) =>
            {
                Debug.Assert(x.Arguments.Count > 0);
                instanceArgument(x.Arguments[0]);
            });
            FunctionFix.Add("snd_play", (x, o) =>
            {
                Debug.Assert(x.Arguments.Count > 0);
                instanceArgument(x.Arguments[0]);
            });
            FunctionFix.Add("draw_set_font", (x, o) =>
            {
                Debug.Assert(x.Arguments.Count == 1);
                fontArgument(x.Arguments[0]);
            });
            FunctionFix.Add("draw_set_color", (x, o) =>
            {
                Debug.Assert(x.Arguments.Count == 1);
                colorArgument(x.Arguments[0]);

            });
            FunctionFix.Add("merge_color", (x, o) =>
            {
                Debug.Assert(x.Arguments.Count == 3);
                colorArgument(x.Arguments[0]);
                colorArgument(x.Arguments[1]);
            });
        }
        public ILCall MutateCall(ILCall call)
        {
           
            if(call.Name.Contains("gml_Script_")) // need to see why this happends
                call.Name = call.Name.Replace("gml_Script_", "");
            Action<ILCall,BlockToCode> func;
            if (FunctionFix.TryGetValue(call.Name, out func))
                func(call,output);
            else if (Constants.IsDefined(call.Name)) return call; // do nothing cause its defined, wee
            else
            {
                if (call.Name.IndexOf("scr_") == 0) // its a user script default
                {
                    // insert self into the first argument
                    call.Arguments.Insert(0, ILExpression.MakeVariable("self"));
                }
            }
            return call;
        }

        public ILVariable MutateVar(ILVariable v)
        {
            return v;
        }

    }

}
