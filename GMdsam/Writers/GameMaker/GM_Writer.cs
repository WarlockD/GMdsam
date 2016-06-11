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

namespace GameMaker.Writers.GameMaker
{
    public class Writer : CodeWriter
    {
        public Writer(BlockToCode output) : base(output)
        {
        }
        void WriteAction(int @event, ILBlock block)
        {
            string eventName = Constants.MakePrittyEventName(@event, 0);
            output.WriteLine("Event: {0}", eventName);
            output.Write(block); // auto ident
            output.WriteLine();
        }
        void WriteAction(int @event, int subEvent, ILBlock block)
        {
            string eventName = Constants.MakePrittyEventName(@event, subEvent);
            output.WriteLine("Event: {0}",  eventName);
            output.Write(block); // auto ident
            output.WriteLine();
        }
        void WriteAction(int @event, List<ActionInfo> actions)
        {
            output.WriteLine("self.{0} = self.{0} or {{}}", Constants.LookUpEvent(@event));
            foreach (var func in actions) WriteAction(@event, func.SubType, func.Method);
        }
    
       
        
        protected override void WriteObject(ObjectInfo info)
        {
            var obj = info.Object;
            // start writing file
            // Headder
            output.WriteLine("Object:");
            output.Indent++;
            output.WriteLine("builtin.index = {0}", obj.Index);
            output.WriteLine("builtin.name = \"{0}\"", obj.Name);
            if (obj.Parent >= 0)
            {
                output.WriteLine("builtin.parent_index = {0}", obj.Parent);
                if (string.IsNullOrWhiteSpace(obj.ParentName)) obj.ParentName = File.Objects[obj.Parent].Name;
                output.WriteLine("builtin.parent_name = \"{0}\"", obj.ParentName);
            }
            output.Write("builtin.sprite_index = {0}", obj.SpriteIndex);
            if (obj.SpriteIndex > -1) output.WriteLine(" // \"{0}\"", File.Sprites[obj.SpriteIndex].Name); else output.WriteLine();
            output.WriteLine("builtin.visible = {0}", obj.Visible ? "true" : "false");
            output.WriteLine("builtin.solid = {0}", obj.Solid ? "true" : "false");
            output.WriteLine("builtin.persistent = {0}", obj.Persistent ? "true" : "false");
            output.WriteLine("builtin.depth = {0}", obj.Depth);
            output.Indent--;
            output.WriteLine();

            WriteLocals(info);
            output.WriteLine();

            foreach (var e in info.Events)
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
            output.WriteLine();
        }

        protected override void WriteScript(ScriptInfo info)
        {
            var script = info.Script;
            output.WriteLine("// ScriptName: {0}", script.Name);
            output.WriteLine("// CodeName: {0} ", script.Code.Name);
            output.WriteLine("// ArgumentCount: {0}", info.ArgumentCount);
            output.WriteLine();
            WriteLocals(info);
            output.WriteLine();
            output.Write(info.Block);
            output.WriteLine(); // extra next line
        }
    }
}
