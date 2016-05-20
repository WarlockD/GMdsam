using GameMaker.Dissasembler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GameMaker.Writers.JavaScript
{
    public class ObjectWriter : IObjectWriter
    {
        BlockToCode output;
        class ActionInfo
        {
            public ILBlock Method;
            public string Name;
            public int SubType;
            public int Type;

        }
        class EventInfo
        {
            public int Type;
            public List<ActionInfo> Actions = new List<ActionInfo>();
        }
        void WriteAction(string funName, ILBlock block)
        {
            output.WriteLine("self.{0} = function()", funName);
            foreach (ILCall c in block.GetSelfAndChildrenRecursive<ILCall>(x => x.Name == "event_inherited")) // have to fix this
            {
                c.Arguments.Add(ILExpression.MakeVariable("self"));
                c.Arguments.Add(ILExpression.MakeConstant(funName));
            }
            output.WriteMethod(funName, block); // auto ident
            output.WriteLine("end");
        }
        void InsertIntoTable(string table, int index, ILBlock func)
        {
            output.WriteLine("self.{0}[{1}] = function() {", table, index);
            output.WriteMethod(table + "_" + index, func);
            output.WriteLine("}");
        }
        void InsertIntoTable(string table, string index, ILBlock func)
        {
            output.WriteLine("self.{0}[\"{1}\"]  = function() {", table, index);
            output.WriteMethod(table + "_" + index, func);
            output.WriteLine("}");
        }
        void InsertIntoTable(string table, List<ActionInfo> actions)
        {
            output.WriteLine("self.{0} = self.{0} || {{}}", table);
            foreach (var func in actions) InsertIntoTable(table, func.SubType, func.Method);
        }
        public void WriteObject(File.GObject obj, BlockToCode output)
        {
            this.output = output;
            //   ILVariable.WriteSelfOnTextOut = false;
            LuaVarCheckCashe cache = new LuaVarCheckCashe();
            List<EventInfo> infos = new List<EventInfo>();

            for (int i = 0; i < obj.Events.Length; i++)
            {
                if (obj.Events[i] == null) continue;
                EventInfo einfo = new EventInfo();
                infos.Add(einfo);
                einfo.Type = i;
                foreach (var e in obj.Events[i])
                {
                    foreach (var a in e.Actions)
                    {
                        File.Code codeData = File.Codes[a.CodeOffset];
                        Context.DebugName = obj.Name + "_" + Context.EventToString(i, e.SubType); // in case of debug
                        if (Context.Debug)
                        {
                            Debug.WriteLine("Name: " + codeData.Name + " Event: " + Context.EventToString(i, e.SubType));
                        }
                        ILBlock method = Context.DecompileBlock( codeData.Data);
                        if (method == null) continue;
                        ActionInfo info = new ActionInfo() { Method = method, Name = Context.EventToString(i, e.SubType), SubType = e.SubType, Type = i };
                        cache.AddVars(method);
                        einfo.Actions.Add(info);
                    }
                }
            }
            // start writing file
            // Headder
            output.WriteLine("var new_{0} = function(self) {", obj.Name);
            output.Indent++;
            output.WriteLine("function event_user(v) { self.UserEvent[v]()  };");
            output.WriteLine();
            output.WriteLine("self.index = {0};", obj.Index);
            output.WriteLine("self.name = \"{0}\";", obj.Name);
            if (obj.Parent >= 0)
            {
                output.WriteLine("self.parent_index = {0}", obj.Parent);
                if (string.IsNullOrWhiteSpace(obj.ParentName)) obj.ParentName = File.Objects[obj.Parent].Name;
                output.WriteLine("self.parent_name = \"{0}\"", obj.ParentName);
            }
            output.WriteLine("self.sprite_index = {0};", obj.SpriteIndex);
            output.WriteLine("self.visible = {0};", obj.Visible ? "true" : "false");
            output.WriteLine("self.solid = {0};", obj.Solid ? "true" : "false");
            output.WriteLine("self.persistent = {0};", obj.Persistent ? "true" : "false");
            output.WriteLine("self.depth = {0};", obj.Depth);
            output.WriteLine();
            output.WriteLine("// check self for arrays");

            var all_arrays = cache.GetAll(x => !x.isGlobal && x.isArray).Select(x => x.Name).Distinct();
            var all_values = cache.GetAll(x => !x.isGlobal && !x.isArray).Select(x => x.Name).Distinct();
            foreach (var v in all_arrays)
            {
                output.WriteLine("self.{0} = self.{0} or {{}};", v == "in" ? "_in" : v); // bunch of null correlesing
            }

            output.WriteLine("//makesure we all have a value");
            foreach (var v in all_values)
                output.WriteLine("self.{0} = 0", v == "in" ? "_in" : v); // hack 
            output.WriteLine();
            foreach (var e in infos)
            {
                switch (e.Type)
                {
                    case 0:
                        Debug.Assert(e.Actions.Count == 1);
                        WriteAction("CreateEvent", e.Actions.Single().Method);
                        break;
                    case 1:
                        Debug.Assert(e.Actions.Count == 1);
                        WriteAction("DestroyEvent", e.Actions.Single().Method);
                        break;
                    case 2:
                        InsertIntoTable("AlarmEvent", e.Actions);
                        break;
                    case 3:
                        foreach (var a in e.Actions)
                        {
                            switch (a.SubType)
                            {
                                case 0: WriteAction("StepNormalEvent", a.Method); break;
                                case 1: WriteAction("StepBeginEvent", a.Method); break;
                                case 2: WriteAction("StepEndEvent", a.Method); break;
                            }
                        }
                        break;
                    case 4:
                        InsertIntoTable("CollisionEvent", e.Actions);
                        break;
                    case 5:
                        InsertIntoTable("Keyboard", e.Actions);
                        break;
                    case 6: // joystick and mouse stuff here, not used much in undertale
                        output.WriteLine("self.{0} = self.{0} or {{}};", "ControlerEvents");
                        foreach (var a in e.Actions)
                            InsertIntoTable("ControlerEvents", a.Name, a.Method);
                        break;
                    case 7: // we only really care about user events
                        output.WriteLine("self.{0} = self.{0} or {{}};", "UserEvent");
                        foreach (var a in e.Actions)
                        {
                            string @event = a.Name;
                            if (a.SubType > 9 && a.SubType < 26)
                            {
                                InsertIntoTable("UserEvent", a.SubType - 10, a.Method);
                            }
                            else
                            {
                                WriteAction(a.Name, a.Method);
                            }
                        }
                        break;
                    case 8:
                        if (e.Actions.Count == 1)
                        {
                            WriteAction("DrawEvent", e.Actions.Single().Method);
                        }
                        else
                        {
                            // special case, alot of diffrent draw events are here but undertale mainly just uses
                            // one, so we will figure out if we need a table or not
                            InsertIntoTable("DrawEvents", e.Actions);
                        }
                        break;
                    case 9:
                        InsertIntoTable("KeyPressed", e.Actions);
                        break;
                    case 10:
                        InsertIntoTable("KeyReleased", e.Actions);
                        break;
                    case 11:
                        InsertIntoTable("Trigger", e.Actions);
                        break;
                }
                output.WriteLine();
            }
            output.Indent--;
            output.WriteLine("end");
            output.WriteLine();
            if (obj.Parent >= 0)
            {
                output.WriteLine("_parents[\"{0}\"] = \"{1}\";", obj.Name, obj.ParentName);
                output.WriteLine("_parents[{0}] = {1};", obj.Index, obj.Parent);
            }

            output.WriteLine("_objects[\"{0}\"] = new_{0};", obj.Name);
            output.WriteLine("_objects[{1}] = new_{0};", obj.Name, obj.Index); // put it in both to make sure we can look it up by both
            output.Flush();
            output.Dispose();
        }

    }
}

