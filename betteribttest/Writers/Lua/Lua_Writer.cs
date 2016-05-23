using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GameMaker.Writers.Lua
{
   
    public class Writer : CodeWriter
    {
        public Writer(BlockToCode output) : base(output)
        {
        }
        void WriteAction(int @event, ILBlock block)
        {
            string eventName = Constants.MakePrittyEventName(@event, 0);
            output.WriteLine("self.{0} = function()", eventName);
            foreach (ILCall c in block.GetSelfAndChildrenRecursive<ILCall>(x => x.Name == "event_inherited")) // have to fix this
            {
                c.Arguments.Add(ILExpression.MakeVariable("self"));
                c.Arguments.Add(ILExpression.MakeConstant(eventName));
            }
            output.Write(block); // auto ident
            output.WriteLine("end");
        }
        void WriteAction(int @event, int subEvent, ILBlock block)
        {
            string eventName = Constants.MakePrittyEventName(@event, subEvent);
            output.WriteLine("self.{0} = function()", eventName);
            foreach (ILCall c in block.GetSelfAndChildrenRecursive<ILCall>(x => x.Name == "event_inherited")) // have to fix this
            {
                c.Arguments.Add(ILExpression.MakeVariable("self"));
                c.Arguments.Add(ILExpression.MakeConstant(eventName));
            }
            output.Write(block); // auto ident
            output.WriteLine("end");
        }
        void WriteAction(int @event, List<ActionInfo> actions)
        {
            output.WriteLine("self.{0} = self.{0} or {{}}", Constants.LookUpEvent(@event));
            foreach (var func in actions) WriteAction(@event, func.SubType, func.Method);
        }
        void CheckOrCreateVar(string varname, string value)
        {
            output.WriteLine("self.{0} = self.{0}  or {1}", varname, value);
        }
        void CheckOrCreateVar(string varname, int value)
        {
            output.WriteLine("self.{0} = self.{0}  or {1}", varname, value);
        }
        void CheckOrCreateVar(string varname, bool value)
        {
            output.WriteLine("self.{0} = self.{0}  or {1}", varname, value ? "true" : "false");
        }
        protected override void WriteObject(File.GObject obj, List<EventInfo> infos)
        {
            // start writing file
            // Headder
            output.WriteLine("local new_{0} = function(self)", obj.Name);
            output.Indent++;
            output.WriteLine("function event_user(v) self.UserEvent[v]() end");
            output.WriteLine();
            output.WriteLine("self.index = {0}", obj.Index);
            output.WriteLine("self.name = \"{0}\"", obj.Name);
            if (obj.Parent >= 0)
            {
                output.WriteLine("self.parent_index = {0}", obj.Parent);
                if (string.IsNullOrWhiteSpace(obj.ParentName)) obj.ParentName = File.Objects[obj.Parent].Name;
                output.WriteLine("self.parent_name = \"{0}\"", obj.ParentName);
            }
            CheckOrCreateVar("sprite_index", obj.SpriteIndex);
            CheckOrCreateVar("visible", obj.Visible);
            CheckOrCreateVar("solid", obj.Solid);
            CheckOrCreateVar("persistent", obj.Persistent);
            CheckOrCreateVar("depth", obj.Depth);
            CheckOrCreateVar("sprite_index", obj.SpriteIndex);
            CheckOrCreateVar("sprite_index", obj.SpriteIndex);

            output.WriteLine();
            output.WriteLine("-- check self for arrays");

            var all_arrays = cache.GetAll(x => !x.isGlobal && x.isArray && !Constants.IsDefined(x.Name)).Select(x => x.Name).Distinct();
            var all_values = cache.GetAll(x => !x.isGlobal && !x.isArray && !Constants.IsDefined(x.Name)).Select(x => x.Name).Distinct();
            foreach (var v in all_arrays)
            {
                output.WriteLine("self.{0} = self.{0} or {{}}", v == "in" ? "_in" : v); // bunch of null correlesing
            }

            output.WriteLine("-- makesure we all have a value");
            foreach (var v in all_values)
                CheckOrCreateVar(v == "in" ? "_in" : v,  0);

            output.WriteLine();
            foreach (var e in infos)
            {
                switch (e.Type)
                {
                    case 0:
                        Debug.Assert(e.Actions.Count == 1);
                        WriteAction(0, e.Actions.Single().Method);
                        break;
                    case 1:
                        Debug.Assert(e.Actions.Count == 1);
                        WriteAction(1, e.Actions.Single().Method);
                        break;
                    case 2:
                        WriteAction(2, e.Actions);
                        break;
                    case 3:
                        foreach (var a in e.Actions) WriteAction(3, a.SubType, a.Method);
                        break;
                    case 4:
                        WriteAction(4, e.Actions);
                        break;
                    case 5:
                        WriteAction(5, e.Actions);
                        break;
                    case 6:
                        foreach (var a in e.Actions) WriteAction(6, a.SubType, a.Method);
                        break;
                    case 7: // we only really care about user events
                        foreach (var a in e.Actions) WriteAction(7, a.SubType, a.Method);
                        break;
                    case 8:
                        foreach (var a in e.Actions)
                        { // special case for 0 since I cannot find it
                            WriteAction(7, a.SubType, a.Method);
                        }
                        break;
                    case 9:
                        WriteAction(9, e.Actions);
                        break;
                    case 10:
                        WriteAction(10, e.Actions);
                        break;
                    case 11:
                        WriteAction(11, e.Actions);
                        break;
                }
                output.WriteLine();
            }
            output.Indent--;
            output.WriteLine("end");
            output.WriteLine();
            if (obj.Parent >= 0)
            {
                output.WriteLine("_parents[\"{0}\"] = \"{1}\"", obj.Name, obj.ParentName);
                output.WriteLine("_parents[{0}] = {1}", obj.Index, obj.Parent);
            }

            output.WriteLine("_objects[\"{0}\"] = new_{0}", obj.Name);
            output.WriteLine("_objects[{1}] = new_{0}", obj.Name, obj.Index); // put it in both to make sure we can look it up by both
        }

        protected override void WriteScript(File.Script script, ILBlock block, int arg_count)
        {
            output.WriteLine("_scripts = _scripts or {}");
            output.WriteLine("-- CodeName: {0} ", script.Name);
            output.Write("function {0}(self", script.Name);
            for (int i = 0; i < arg_count; i++) output.Write(",argument{0}", i);
            output.WriteLine(") ");
            output.Write(block);
            output.WriteLine("end");
            output.WriteLine(); // extra next line
            output.WriteLine("_scripts[{0}] = {1}", script.Index, script.Name);
            output.WriteLine("_scripts[\"{0}\"] = {1}", script.Name, script.Name);
        }
    }
}