using GameMaker.Ast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker
{
    public static class Constants
    {
        // these two are helper functions for writing commma ordered junk
        // They will write nothing if there is nothing, and will always make sure there is a starter space, and ending space and a space right
        // after the comma of each element.  It also reutnrs true if it was empty but not sure I iwll keep that
        public static bool WriteCommaDelimited<T>(this TextWriter writer, IEnumerable<T> list, Func<T,bool> action) 
        {
            var arr = list.ToArray();
            if (arr.Length == 0) return true;
            int i = 0;
            for (; i < (arr.Length - 1); i++)
            {
                if (action(arr[i]))
                    writer.WriteLine(", ");
                else
                    writer.Write(", ");
            }
            if (action(arr[i]))
                writer.WriteLine();
            return false;
        }
        public static bool isEndingEqual(this StringBuilder sb, char t)
        {
            for(int i = sb.Length - 1; i >= 0; i--)
            {
                char c = sb[i];
                if (c == t) return true;
                else if (char.IsWhiteSpace(c)) continue;
                else break;
            }
            return false;
        }
        public static bool isEndingEqual(this string sb, char t)
        {
            for (int i = sb.Length - 1; i >= 0; i--)
            {
                char c = sb[i];
                if (c == t) return true;
                else if (char.IsWhiteSpace(c)) continue;
                else break;
            }
            return false;
        }
        public static bool isEndingEqual(this string sb, char t1, char t2)
        {
            for (int i = sb.Length - 1; i >= 0; i--)
            {
                char c = sb[i];
                if (c == t1 || c == t2) return true;
                else if (char.IsWhiteSpace(c)) continue;
                else break;
            }
            return false;
        }
        public static bool AppendCommaDelimited<T>(this StringBuilder sb, IEnumerable<T> list, Func<T,bool> action)
        {
            var arr = list.ToArray();
            if (arr.Length == 0) return true;
            sb.Append(' ');
            int i = 0;
            for (; i < arr.Length - 1; i++)
            {
                if (action(arr[i]))
                    sb.AppendLine(",");
                else
                    sb.Append(", ");
            }
            if (action(arr[i]))
                sb.AppendLine();
            else
                sb.Append(' ');
            return false;
        }
        public static string JISONEscapeString(string s)
        {
            StringBuilder sb = new StringBuilder(50);
            sb.Append('"');
            foreach (var c in s) if (c == '\'') sb.Append(c); else  sb.Append(JISONEscapeString(c));
            sb.Append('"');
            return sb.ToString();
        }
        public static string JISONEscapeString(char v)
        {
            switch (v)
            {
                case '\a': return "\\a";
                case '\n': return "\\n";
                case '\r': return "\\r";
                case '\t': return "\\t";
                case '\v': return "\\v";
                case '\\': return "\\\\";
                case '\"': return "\\\"";
                case '\'': return "\\\'";
                //  case '[': return "\\[";
                //   case ']': return "\\]";
                default:
                    if (char.IsControl(v)) return string.Format("\\{0}", (byte)v);
                    else return v.ToString();
            }
        }
        static Dictionary<int, string> identCache = new Dictionary<int, string>();
        // better than making a new string each ident level
        public static void Ident(this StringBuilder sb, int ident)
        {
            if (ident < 0) throw new ArgumentException("ident cannot be less than 0", "ident");
            if (ident > 20) throw new ArgumentException("ident cannot be more than 20", "ident");
            string sident;
            if (!identCache.TryGetValue(ident, out sident))
                identCache.Add(ident, sident = new string('\t', ident));
            sb.Append(sident);
        }
        public static void AppendLineAndIdent(this StringBuilder sb, int ident)
        {
            if (ident < 0) throw new ArgumentException("ident cannot be less than 0", "ident");
            if (ident > 20) throw new ArgumentException("ident cannot be more than 20", "ident");
            string sident;
            if (!identCache.TryGetValue(ident, out sident))
                identCache.Add(ident, sident = new string('\t', ident));
            sb.AppendLine();
            sb.Append(sident);
        }
        public static string DebugHex(this int i)
        {
            return string.Format("{0}(0x{0:X8})", i);
        }
        //static Dictionary<string, int> allConstants = new Dictionary<string, int>();
        //   static Dictionary<string, int> intConstants;
        public static string GMTypeToPostfix(this GM_Type t)
        {
            switch (t)
            {
                case GM_Type.Double: return ".d";
                case GM_Type.Float: return ".f";
                case GM_Type.Int: return ".i";
                case GM_Type.Long: return ".l";
                case GM_Type.Bool: return ".b";
                case GM_Type.Var: return ".v";
                case GM_Type.String: return ".s";
                case GM_Type.Short: return ".e";
                default:
                    return ".0";
            }
        }
        public static bool Contains(this List<Ast.ILRange> range, int value)
        {
            return range.Any(x => x.Contains(value));
        }
     
        class EventInfo
        {
            public string Name;
            public Dictionary<int, string> SubEvents = null;
            public bool needsSubEvent = false;
        }
        public class PropertyInfo
        {
            public readonly string Name;
            public readonly bool CanRead;
            public readonly bool CanWrite;
            public readonly bool Global;
            public readonly GM_Type Type;
            internal PropertyInfo(string name, bool canRead, bool canWrite, bool global)
            {
                this.Name = name;
                this.CanRead = canRead;
                this.CanWrite = canWrite;
                this.Global = global;
                this.Type = GM_Type.Int;
            }
            internal PropertyInfo(string name, bool canRead, bool canWrite, bool global, GM_Type type)
            {
                this.Name = name;
                this.CanRead = canRead;
                this.CanWrite = canWrite;
                this.Global = global;
                this.Type = type;
            }
        }
        public class GlobalFunctionInfo
        {
            public readonly string Name;
            public readonly int ArgumentCount;
            public readonly GM_Type Type;
            internal GlobalFunctionInfo(string name, int args)
            {
                this.Name = name;
                this.ArgumentCount = args;
                this.Type = GM_Type.NoType;
            }
            internal GlobalFunctionInfo(string name, int args, GM_Type type)
            {
                this.Name = name;
                this.ArgumentCount = args;
                this.Type = type;
            }
        }
        static Dictionary<string, GlobalFunctionInfo> globalFunctions;
        static Dictionary<string, PropertyInfo> allProperties;
        static HashSet<string> defined;
        public static IReadOnlyDictionary<string, GlobalFunctionInfo> GlobalFunctions { get { return globalFunctions; } }
        static public IReadOnlyDictionary<string, PropertyInfo> Properties { get { return allProperties; } }
        public static bool IsDefined(string name) { return defined.Contains(name); }

        // Trys to guess what the final type will be because of size

        public static bool FixAndCheckVarType(ILVariable v)
        {
            if (v.Type != GM_Type.NoType) return true;
            if (Constants.IsDefined(v.Name))
            {
                v.Type = Constants.Properties[v.Name].Type;
                return true;
            }
            return false;
        }

        static void AddProperty(string name, bool canRead, bool canWrite, bool global) {
            defined.Add(name);
            allProperties.Add(name, new PropertyInfo(name, canRead, canWrite, global));
        }
        static void AddProperty(string name, bool canRead, bool canWrite, bool global,GM_Type type)
        {
            defined.Add(name);
            allProperties.Add(name, new PropertyInfo(name, canRead, canWrite, global,type));
        }
        static void AddFunction(string name, int args)
        {
            defined.Add(name);
            globalFunctions.Add(name, new GlobalFunctionInfo(name, args));
        }

        static void AddFunction(string name, int args, GM_Type type)
        {
            defined.Add(name);
            globalFunctions.Add(name, new GlobalFunctionInfo(name, args, type));
        }

        static EventInfo[] eventInfo;
        public static bool EventHasNoNamedSub(int @event, int subEvent)
        {
            return eventInfo[@event].SubEvents == null;
        }
        public static string LookUpEvent(int @event)
        {
            return eventInfo[@event].Name;
        }
        public static string LookUpEvent(int @event, int subEvent)
        {
            string ret;
            var info = eventInfo[@event];
            if (info.SubEvents == null) ret =  info.Name;
            else
            {
                if (!info.SubEvents.TryGetValue(subEvent, out ret) && (@event == 8))
                {
                    if (@event == 8) ret = info.Name;
                    else throw new Exception("Bad sub event");
                }
            }
            return  ret;
        }
        // to make a var string for accessing a event
        public static string MakePrittyEventName(int @event, int subEvent)
        {
            var info = eventInfo[@event];
            StringBuilder sb = new StringBuilder();
            if(info.needsSubEvent)
            {
                sb.Append(info.Name);
                sb.Append('[');
                if(info.Name == "ev_keyboard" || info.Name == "ev_keyrelease" || info.Name == "ev_keypress")
                    sb.Append(Context.KeyToString(subEvent));
                 else
                    sb.Append(subEvent);
                sb.Append(']');
            } else
            {
                string ret;
                if (info.SubEvents == null || !info.SubEvents.TryGetValue(subEvent, out ret))
                    ret = info.Name;
                sb.Append(ret);                
            }
            return sb.ToString();
        }
        static void SetUpEventConstants()
        {
            eventInfo = new EventInfo[12];
            eventInfo[0] = new EventInfo() { Name = "ev_create"  };
            eventInfo[1] = new EventInfo() { Name = "ev_destroy" };
            eventInfo[2] = new EventInfo() { Name = "ev_alarm"  , needsSubEvent = true};
            eventInfo[3] = new EventInfo() { Name = "ev_step" , SubEvents  = new Dictionary<int, string>() {
                { 0, "ev_step_normal" },
                { 1, "ev_step_begin" },
                { 2, "ev_step_end" },
            }};
            eventInfo[4] = new EventInfo() { Name = "ev_collision", needsSubEvent = true };
            eventInfo[5] = new EventInfo() { Name = "ev_keyboard", needsSubEvent = true };
            eventInfo[6] = new EventInfo()
            {
                Name = "ev_mouse",
                SubEvents = new Dictionary<int, string>() {
                { 0, "ev_left_button" },
                { 1, "ev_right_button" },
                { 2, "ev_middle_button" },
                { 3, "ev_no_button" },
                { 4, "ev_left_press" },
                { 5, "ev_right_press" },
                { 6, "ev_middle_press" },
                { 7, "ev_left_release" },
                { 8, "ev_right_release" },
                { 9, "ev_middle_release" },
                { 10, "ev_mouse_enter" },
                { 11, "ev_mouse_leave" },
                { 12, "ev_global_press" },
                { 13, "ev_global_release" },
                { 16, "ev_joystick1_left" },
                { 17, "ev_joystick1_right" },
                { 18, "ev_joystick1_up" },
                { 19, "ev_joystick1_down" },
                { 21, "ev_joystick1_button1" },
                { 22, "ev_joystick1_button2" },
                { 23, "ev_joystick1_button3" },
                { 24, "ev_joystick1_button4" },
                { 25, "ev_joystick1_button5" },
                { 26, "ev_joystick1_button6" },
                { 27, "ev_joystick1_button7" },
                { 28, "ev_joystick1_button8" },
                { 31, "ev_joystick2_left" },
                { 32, "ev_joystick2_right" },
                { 33, "ev_joystick2_up" },
                { 34, "ev_joystick2_down" },
                { 36, "ev_joystick2_button1" },
                { 37, "ev_joystick2_button2" },
                { 38, "ev_joystick2_button3" },
                { 39, "ev_joystick2_button4" },
                { 40, "ev_joystick2_button5" },
                { 41, "ev_joystick2_button6" },
                { 42, "ev_joystick2_button7" },
                { 43, "ev_joystick2_button8" },
                { 50, "ev_global_left_button" },
                { 51, "ev_global_right_button" },
                { 52, "ev_global_middle_button" },
                { 53, "ev_global_left_press" },
                { 54, "ev_global_right_press" },
                { 55, "ev_global_middle_press" },
                { 56, "ev_global_left_release" },
                { 57, "ev_global_right_release" },
                { 58, "ev_global_middle_release" },
                { 60, "ev_mouse_wheel_up" },
                { 61, "ev_mouse_wheel_down" },
            }
            };
            eventInfo[7] = new EventInfo()
            {
                Name = "ev_other",
                SubEvents = new Dictionary<int, string>()
                {
                     { 0, "ev_outside" },
                    { 1, "ev_boundary" },
                    { 2, "ev_game_start" },
                    { 3, "ev_game_end" },
                    { 4, "ev_room_start" },
                    { 5, "ev_room_end" },
                    { 6, "ev_no_more_lives" },
                    { 7, "ev_animation_end" },
                    { 8, "ev_end_of_path" },
                    { 9, "ev_no_more_health" },
                    { 10, "ev_user0" },
                    { 11, "ev_user1" },
                    { 12, "ev_user2" },
                    { 13, "ev_user3" },
                    { 14, "ev_user4" },
                    { 15, "ev_user5" },
                    { 16, "ev_user6" },
                    { 17, "ev_user7" },
                    { 18, "ev_user8" },
                    { 19, "ev_user9" },
                    { 20, "ev_user10" },
                    { 21, "ev_user11" },
                    { 22, "ev_user12" },
                    { 23, "ev_user13" },
                    { 24, "ev_user14" },
                    { 25, "ev_user15" },
                    { 30, "ev_close_button" },
                }
            };
            eventInfo[8] = new EventInfo()
            {
                Name = "ev_draw",
                SubEvents = new Dictionary<int, string>()
                {
                    { 64, "ev_gui" },
                    { 72, "ev_draw_begin" },
                    { 73, "ev_draw_end" },
                    { 74, "ev_gui_begin" },
                    { 75, "ev_gui_end" },
                    { 76, "ev_draw_pre" },
                    { 77, "ev_draw_post" },
                }
            };
            eventInfo[9] = new EventInfo() { Name = "ev_keypress", needsSubEvent = true };
            eventInfo[10] = new EventInfo() { Name = "ev_keyrelease", needsSubEvent = true };
            eventInfo[11] = new EventInfo()  { Name = "ev_trigger", needsSubEvent = true };
        }

        // functions that don't screw with the current object
        public static GM_Type GetFunctionType(string name, ErrorContext error=null)
        {
            if (error == null) error = ErrorContext.Out;
            GM_Type t = GM_Type.NoType;
             GlobalFunctionInfo info;
            if (globalFunctions.TryGetValue(name, out info))
            {
                t = info.Type;
                if (t == GM_Type.NoType)
                {
                   // error.Error("Don't know the type of builtin function '{0}', defaulting to int", name);
                 //   t = GM_Type.Int;
                }
                return t;
            }
            // Not found, ok lets try a script
            File.Script script;
            if (File.TryLookup(name, out script))
            {
                t = script.ReturnType;
                if (t == GM_Type.NoType)
                {
                 //   error.Error("Don't know the type of user function '{0}', defaulting to int", name);
                 //   t = GM_Type.Int;
                }
                AddFunction(name, script.ArgumentCount, t);
                return t;
            }
            return GM_Type.NoType;
        }
        static void SetupGlobalFunctions()
        {
            globalFunctions = new Dictionary<string, GlobalFunctionInfo>();
           AddFunction("d3d_start", 0);
           AddFunction("d3d_end", 0);
           AddFunction("d3d_set_perspective", 1);
           AddFunction("d3d_set_hidden", 1);
           AddFunction("d3d_set_depth", 1);
           AddFunction("d3d_set_lighting", 1);
           AddFunction("d3d_set_shading", 1);
           AddFunction("d3d_set_fog", 4);
           AddFunction("d3d_set_culling", 1);
           AddFunction("d3d_set_zwriteenable", 1);
           AddFunction("d3d_primitive_begin", 1);
           AddFunction("d3d_primitive_begin_texture", 2);
           AddFunction("d3d_primitive_end", 0);
           AddFunction("d3d_vertex", 3);
           AddFunction("d3d_vertex_color", 5);
           AddFunction("d3d_vertex_colour", 5);
           AddFunction("d3d_vertex_texture", 5);
           AddFunction("d3d_vertex_texture_color", 7);
           AddFunction("d3d_vertex_texture_colour", 7);
           AddFunction("d3d_vertex_normal", 6);
           AddFunction("d3d_vertex_normal_color", 8);
           AddFunction("d3d_vertex_normal_colour", 8);
           AddFunction("d3d_vertex_normal_texture", 8);
           AddFunction("d3d_vertex_normal_texture_color", 10);
           AddFunction("d3d_vertex_normal_texture_colour", 10);
           AddFunction("d3d_draw_block", 9);
           AddFunction("d3d_draw_cylinder", 11);
           AddFunction("d3d_draw_cone", 11);
           AddFunction("d3d_draw_ellipsoid", 10);
           AddFunction("d3d_draw_wall", 9);
           AddFunction("d3d_draw_floor", 9);
           AddFunction("d3d_set_projection", 9);
           AddFunction("d3d_set_projection_ext", 13);
           AddFunction("d3d_set_projection_ortho", 5);
           AddFunction("d3d_set_projection_perspective", 5);
           AddFunction("d3d_transform_set_identity", 0);
           AddFunction("d3d_transform_set_translation", 3);
           AddFunction("d3d_transform_set_scaling", 3);
           AddFunction("d3d_transform_set_rotation_x", 1);
           AddFunction("d3d_transform_set_rotation_y", 1);
           AddFunction("d3d_transform_set_rotation_z", 1);
           AddFunction("d3d_transform_set_rotation_axis", 4);
           AddFunction("d3d_transform_add_translation", 3);
           AddFunction("d3d_transform_add_scaling", 3);
           AddFunction("d3d_transform_add_rotation_x", 1);
           AddFunction("d3d_transform_add_rotation_y", 1);
           AddFunction("d3d_transform_add_rotation_z", 1);
           AddFunction("d3d_transform_add_rotation_axis", 4);
           AddFunction("d3d_transform_stack_clear", 0);
           AddFunction("d3d_transform_stack_empty", 0);
           AddFunction("d3d_transform_stack_push", 0);
           AddFunction("d3d_transform_stack_pop", 0);
           AddFunction("d3d_transform_stack_top", 0);
           AddFunction("d3d_transform_stack_discard", 0);
           AddFunction("d3d_transform_vertex", 3);
           AddFunction("matrix_get", 1);
           AddFunction("matrix_set", 2);
           AddFunction("matrix_build", 9);
           AddFunction("matrix_multiply", 2);
           AddFunction("d3d_light_define_ambient", 1);
           AddFunction("d3d_light_define_direction", 5);
           AddFunction("d3d_light_define_point", 6);
           AddFunction("d3d_light_enable", 2);
           AddFunction("d3d_model_create", 0);
           AddFunction("d3d_model_destroy", 1);
           AddFunction("d3d_model_clear", 1);
           AddFunction("d3d_model_load", 2);
           AddFunction("d3d_model_save", 2);
           AddFunction("d3d_model_draw", 5);
           AddFunction("d3d_model_primitive_begin", 2);
           AddFunction("d3d_model_primitive_end", 1);
           AddFunction("d3d_model_vertex", 4);
           AddFunction("d3d_model_vertex_color", 6);
           AddFunction("d3d_model_vertex_colour", 6);
           AddFunction("d3d_model_vertex_texture", 6);
           AddFunction("d3d_model_vertex_texture_color", 8);
           AddFunction("d3d_model_vertex_texture_colour", 8);
           AddFunction("d3d_model_vertex_normal", 7);
           AddFunction("d3d_model_vertex_normal_color", 9);
           AddFunction("d3d_model_vertex_normal_colour", 9);
           AddFunction("d3d_model_vertex_normal_texture", 9);
           AddFunction("d3d_model_vertex_normal_texture_color", 11);
           AddFunction("d3d_model_vertex_normal_texture_colour", 11);
           AddFunction("d3d_model_block", 9);
           AddFunction("d3d_model_cylinder", 11);
           AddFunction("d3d_model_cone", 11);
           AddFunction("d3d_model_ellipsoid", 10);
           AddFunction("d3d_model_wall", 9);
           AddFunction("d3d_model_floor", 9);
           AddFunction("action_path_old", 3);
           AddFunction("action_set_sprite", 2);
           AddFunction("action_draw_font", 1);
           AddFunction("action_draw_font_old", 6);
           AddFunction("action_fill_color", 1);
           AddFunction("action_fill_colour", 1);
           AddFunction("action_line_color", 1);
           AddFunction("action_line_colour", 1);
           AddFunction("action_highscore", 0);
           AddFunction("action_set_relative", 1);
           AddFunction("action_move", 2);
           AddFunction("action_set_motion", 2);
           AddFunction("action_set_hspeed", 1);
           AddFunction("action_set_vspeed", 1);
           AddFunction("action_set_gravity", 2);
           AddFunction("action_set_friction", 1);
           AddFunction("action_move_point", 3);
           AddFunction("action_move_to", 2);
           AddFunction("action_move_start", 0);
           AddFunction("action_move_random", 2);
           AddFunction("action_snap", 2);
           AddFunction("action_wrap", 1);
           AddFunction("action_reverse_xdir", 0);
           AddFunction("action_reverse_ydir", 0);
           AddFunction("action_move_contact", 3);
           AddFunction("action_bounce", 2);
           AddFunction("action_path", 4);
           AddFunction("action_path_end", 0);
           AddFunction("action_path_position", 1);
           AddFunction("action_path_speed", 1);
           AddFunction("action_linear_step", 4);
           AddFunction("action_potential_step", 4);
           AddFunction("action_kill_object", 0);
           AddFunction("action_create_object", 3);
           AddFunction("action_create_object_motion", 5);
           AddFunction("action_create_object_random", 6);
           AddFunction("action_change_object", 2);
           AddFunction("action_kill_position", 2);
           AddFunction("action_sprite_set", 3);
           AddFunction("action_sprite_transform", 4);
           AddFunction("action_sprite_color", 2);
           AddFunction("action_sprite_colour", 2);
           AddFunction("action_sound", 2);
           AddFunction("action_end_sound", 1);
           AddFunction("action_if_sound", 1);
           AddFunction("action_another_room", 1);
           AddFunction("action_current_room", 0);
           AddFunction("action_previous_room", 0);
           AddFunction("action_next_room", 0);
           AddFunction("action_if_previous_room", 0);
           AddFunction("action_if_next_room", 0);
           AddFunction("action_set_alarm", 2);
           AddFunction("action_sleep", 2);
           AddFunction("action_set_timeline", 2);
           AddFunction("action_timeline_set", 4);
           AddFunction("action_timeline_start", 0);
           AddFunction("action_timeline_stop", 0);
           AddFunction("action_timeline_pause", 0);
           AddFunction("action_set_timeline_position", 1);
           AddFunction("action_set_timeline_speed", 1);
           AddFunction("action_message", 1);
           AddFunction("action_show_info", 0);
           AddFunction("action_show_video", 3);
           AddFunction("action_end_game", 0);
           AddFunction("action_restart_game", 0);
           AddFunction("action_save_game", 1);
           AddFunction("action_load_game", 1);
           AddFunction("action_replace_sprite", 3);
           AddFunction("action_replace_sound", 2);
           AddFunction("action_replace_background", 2);
           AddFunction("action_if_empty", 3);
           AddFunction("action_if_collision", 3);
           AddFunction("action_if", 1);
           AddFunction("action_if_number", 3);
           AddFunction("action_if_object", 3);
           AddFunction("action_if_question", 1);
           AddFunction("action_if_dice", 1);
           AddFunction("action_if_mouse", 1);
           AddFunction("action_if_aligned", 2);
           AddFunction("action_execute_script", 6);
           AddFunction("action_inherited", 0);
           AddFunction("action_if_variable", 3);
           AddFunction("action_draw_variable", 3);
           AddFunction("action_set_score", 1);
           AddFunction("action_if_score", 2);
           AddFunction("action_draw_score", 3);
           AddFunction("action_highscore_show", 11);
           AddFunction("action_highscore_clear", 0);
           AddFunction("action_set_life", 1);
           AddFunction("action_if_life", 2);
           AddFunction("action_draw_life", 3);
           AddFunction("action_draw_life_images", 3);
           AddFunction("action_set_health", 1);
           AddFunction("action_if_health", 2);
           AddFunction("action_draw_health", 6);
           AddFunction("action_set_caption", 6);
           AddFunction("action_partsyst_create", 1);
           AddFunction("action_partsyst_destroy", 0);
           AddFunction("action_partsyst_clear", 0);
           AddFunction("action_parttype_create_old", 6);
           AddFunction("action_parttype_create", 6);
           AddFunction("action_parttype_color", 6);
           AddFunction("action_parttype_colour", 6);
           AddFunction("action_parttype_life", 3);
           AddFunction("action_parttype_speed", 6);
           AddFunction("action_parttype_gravity", 3);
           AddFunction("action_parttype_secondary", 5);
           AddFunction("action_partemit_create", 6);
           AddFunction("action_partemit_destroy", 1);
           AddFunction("action_partemit_burst", 3);
           AddFunction("action_partemit_stream", 3);
           AddFunction("action_cd_play", 2);
           AddFunction("action_cd_stop", 0);
           AddFunction("action_cd_pause", 0);
           AddFunction("action_cd_resume", 0);
           AddFunction("action_cd_present", 0);
           AddFunction("action_cd_playing", 0);
           AddFunction("action_set_cursor", 2);
           AddFunction("action_webpage", 1);
           AddFunction("action_splash_web", 1);
           AddFunction("action_draw_sprite", 4);
           AddFunction("action_draw_background", 4);
           AddFunction("action_draw_text", 3);
           AddFunction("action_draw_text_transformed", 6);
           AddFunction("action_draw_rectangle", 5);
           AddFunction("action_draw_gradient_hor", 6);
           AddFunction("action_draw_gradient_vert", 6);
           AddFunction("action_draw_ellipse", 5);
           AddFunction("action_draw_ellipse_gradient", 6);
           AddFunction("action_draw_line", 4);
           AddFunction("action_draw_arrow", 5);
           AddFunction("action_color", 1);
           AddFunction("action_colour", 1);
           AddFunction("action_font", 2);
           AddFunction("action_fullscreen", 1);
           AddFunction("action_snapshot", 1);
           AddFunction("action_effect", 6);
           AddFunction("ds_set_precision", 1);
           AddFunction("ds_exists", 2);
           AddFunction("ds_stack_create", 0);
           AddFunction("ds_stack_destroy", 1);
           AddFunction("ds_stack_clear", 1);
           AddFunction("ds_stack_copy", 2);
           AddFunction("ds_stack_size", 1);
           AddFunction("ds_stack_empty", 1);
           AddFunction("ds_stack_push", -1);
           AddFunction("ds_stack_pop", 1);
           AddFunction("ds_stack_top", 1);
           AddFunction("ds_stack_write", 1);
           AddFunction("ds_stack_read", -1);
           AddFunction("ds_queue_create", 0);
           AddFunction("ds_queue_destroy", 1);
           AddFunction("ds_queue_clear", 1);
           AddFunction("ds_queue_copy", 2);
           AddFunction("ds_queue_size", 1);
           AddFunction("ds_queue_empty", 1);
           AddFunction("ds_queue_enqueue", -1);
           AddFunction("ds_queue_dequeue", 1);
           AddFunction("ds_queue_head", 1);
           AddFunction("ds_queue_tail", 1);
           AddFunction("ds_queue_write", 1);
           AddFunction("ds_queue_read", -1);
           AddFunction("ds_list_create", 0);
           AddFunction("ds_list_destroy", 1);
           AddFunction("ds_list_clear", 1);
           AddFunction("ds_list_copy", 2);
           AddFunction("ds_list_size", 1);
           AddFunction("ds_list_empty", 1);
           AddFunction("ds_list_add", -1);
           AddFunction("ds_list_insert", 3);
           AddFunction("ds_list_replace", 3);
           AddFunction("ds_list_delete", 2);
           AddFunction("ds_list_find_index", 2);
           AddFunction("ds_list_find_value", 2);
           AddFunction("ds_list_mark_as_list", 2);
           AddFunction("ds_list_mark_as_map", 2);
           AddFunction("ds_list_sort", 2);
           AddFunction("ds_list_shuffle", 1);
           AddFunction("ds_list_write", 1);
           AddFunction("ds_list_read", -1);
           AddFunction("ds_list_set", 3);
           AddFunction("ds_list_set_pre", 3);
           AddFunction("ds_list_set_post", 3);
           AddFunction("ds_map_create", 0);
           AddFunction("ds_map_destroy", 1);
           AddFunction("ds_map_clear", 1);
           AddFunction("ds_map_copy", 2);
           AddFunction("ds_map_size", 1);
           AddFunction("ds_map_empty", 1);
           AddFunction("ds_map_add", 3);
           AddFunction("ds_map_add_list", 3);
           AddFunction("ds_map_add_map", 3);
           AddFunction("ds_map_replace", 3);
           AddFunction("ds_map_replace_list", 3);
           AddFunction("ds_map_replace_map", 3);
           AddFunction("ds_map_delete", 2);
           AddFunction("ds_map_exists", 2);
           AddFunction("ds_map_find_value", 2);
           AddFunction("ds_map_find_previous", 2);
           AddFunction("ds_map_find_next", 2);
           AddFunction("ds_map_find_first", 1);
           AddFunction("ds_map_find_last", 1);
           AddFunction("ds_map_write", 1);
           AddFunction("ds_map_read", -1);
           AddFunction("ds_map_secure_save", 2);
           AddFunction("ds_map_secure_load", 1);
           AddFunction("ds_map_set", 3);
           AddFunction("ds_map_set_pre", 3);
           AddFunction("ds_map_set_post", 3);
           AddFunction("ds_priority_create", 0);
           AddFunction("ds_priority_destroy", 1);
           AddFunction("ds_priority_clear", 1);
           AddFunction("ds_priority_copy", 1);
           AddFunction("ds_priority_size", 1);
           AddFunction("ds_priority_empty", 1);
           AddFunction("ds_priority_add", 3);
           AddFunction("ds_priority_change_priority", 3);
           AddFunction("ds_priority_find_priority", 2);
           AddFunction("ds_priority_delete_value", 2);
           AddFunction("ds_priority_delete_min", 1);
           AddFunction("ds_priority_find_min", 1);
           AddFunction("ds_priority_delete_max", 1);
           AddFunction("ds_priority_find_max", 1);
           AddFunction("ds_priority_write", 1);
           AddFunction("ds_priority_read", -1);
           AddFunction("ds_grid_create", 2);
           AddFunction("ds_grid_destroy", 1);
           AddFunction("ds_grid_copy", 2);
           AddFunction("ds_grid_resize", 3);
           AddFunction("ds_grid_width", 1);
           AddFunction("ds_grid_height", 1);
           AddFunction("ds_grid_clear", 2);
           AddFunction("ds_grid_set", 4);
           AddFunction("ds_grid_add", 4);
           AddFunction("ds_grid_multiply", 4);
           AddFunction("ds_grid_set_region", 6);
           AddFunction("ds_grid_add_region", 6);
           AddFunction("ds_grid_multiply_region", 6);
           AddFunction("ds_grid_set_disk", 5);
           AddFunction("ds_grid_add_disk", 5);
           AddFunction("ds_grid_multiply_disk", 5);
           AddFunction("ds_grid_set_grid_region", 8);
           AddFunction("ds_grid_add_grid_region", 8);
           AddFunction("ds_grid_multiply_grid_region", 8);
           AddFunction("ds_grid_get", 3);
           AddFunction("ds_grid_get_sum", 5);
           AddFunction("ds_grid_get_max", 5);
           AddFunction("ds_grid_get_min", 5);
           AddFunction("ds_grid_get_mean", 5);
           AddFunction("ds_grid_get_disk_sum", 4);
           AddFunction("ds_grid_get_disk_max", 4);
           AddFunction("ds_grid_get_disk_min", 4);
           AddFunction("ds_grid_get_disk_mean", 4);
           AddFunction("ds_grid_value_exists", 6);
           AddFunction("ds_grid_value_x", 6);
           AddFunction("ds_grid_value_y", 6);
           AddFunction("ds_grid_value_disk_exists", 5);
           AddFunction("ds_grid_value_disk_x", 5);
           AddFunction("ds_grid_value_disk_y", 5);
           AddFunction("ds_grid_shuffle", 1);
           AddFunction("ds_grid_write", 1);
           AddFunction("ds_grid_read", -1);
           AddFunction("ds_grid_sort", 3);
           AddFunction("ds_grid_set_pre", 4);
           AddFunction("ds_grid_set_post", 4);
           AddFunction("mplay_init_ipx", 0);
           AddFunction("mplay_init_tcpip", 1);
           AddFunction("mplay_init_modem", 2);
           AddFunction("mplay_init_serial", 5);
           AddFunction("mplay_connect_status", 0);
           AddFunction("mplay_end", 0);
           AddFunction("mplay_session_mode", 1);
           AddFunction("mplay_session_create", 3);
           AddFunction("mplay_session_find", 0);
           AddFunction("mplay_session_name", 1);
           AddFunction("mplay_session_join", 2);
           AddFunction("mplay_session_status", 0);
           AddFunction("mplay_session_end", 0);
           AddFunction("mplay_player_find", 0);
           AddFunction("mplay_player_name", 1);
           AddFunction("mplay_player_id", 1);
           AddFunction("mplay_data_write", 2);
           AddFunction("mplay_data_read", 1);
           AddFunction("mplay_data_mode", 1);
           AddFunction("mplay_message_send", 3);
           AddFunction("mplay_message_send_guaranteed", 3);
           AddFunction("mplay_message_receive", 1);
           AddFunction("mplay_message_id", 0);
           AddFunction("mplay_message_value", 0);
           AddFunction("mplay_message_player", 0);
           AddFunction("mplay_message_name", 0);
           AddFunction("mplay_message_count", 1);
           AddFunction("mplay_message_clear", 1);
           AddFunction("mplay_ipaddress", 0);
           AddFunction("file_bin_open", 2);
           AddFunction("file_bin_rewrite", 1);
           AddFunction("file_bin_close", 1);
           AddFunction("file_bin_position", 1);
           AddFunction("file_bin_size", 1);
           AddFunction("file_bin_seek", 2);
           AddFunction("file_bin_read_byte", 1);
           AddFunction("file_bin_write_byte", 2);
           AddFunction("file_text_open_from_string", 1, GM_Type.Int);
           AddFunction("file_text_open_read", 1, GM_Type.Int);
           AddFunction("file_text_open_write", 1, GM_Type.Int);
           AddFunction("file_text_open_append", 1, GM_Type.Int);
           AddFunction("file_text_close", 1);
           AddFunction("file_text_read_string", 1, GM_Type.String);
           AddFunction("file_text_read_real", 1, GM_Type.Float);
           AddFunction("file_text_readln", 1);
           AddFunction("file_text_eof", 1);
           AddFunction("file_text_eoln", 1);
           AddFunction("file_text_write_string", 2);
           AddFunction("file_text_write_real", 2);
           AddFunction("file_text_writeln", 1);
           AddFunction("file_open_read", 1);
           AddFunction("file_open_write", 1);
           AddFunction("file_open_append", 1);
           AddFunction("file_close", 0);
           AddFunction("file_read_string", 0, GM_Type.String);
           AddFunction("file_read_real", 0, GM_Type.Float);
           AddFunction("file_readln", 0);
           AddFunction("file_eof", 0);
           AddFunction("file_write_string", 1);
           AddFunction("file_write_real", 1);
           AddFunction("file_writeln", 0);
           AddFunction("file_exists", 1, GM_Type.Bool);
           AddFunction("file_delete", 1);
           AddFunction("file_rename", 2);
           AddFunction("file_copy", 2);
           AddFunction("directory_exists", 1);
           AddFunction("directory_create", 1);
           AddFunction("directory_destroy", 1);
           AddFunction("file_find_first", 2);
           AddFunction("file_find_next", 0);
           AddFunction("file_find_close", 0);
           AddFunction("file_attributes", 2);
           AddFunction("filename_name", 1);
           AddFunction("filename_path", 1);
           AddFunction("filename_dir", 1);
           AddFunction("filename_drive", 1);
           AddFunction("filename_ext", 1);
           AddFunction("filename_change_ext", 2);
           AddFunction("export_include_file", 1);
           AddFunction("export_include_file_location", 2);
           AddFunction("discard_include_file", 1);
           AddFunction("execute_program", 3);
           AddFunction("execute_shell", 2);
           AddFunction("parameter_count", 0);
           AddFunction("parameter_string", 1);
           AddFunction("environment_get_variable", 1);
           AddFunction("registry_write_string", 2);
           AddFunction("registry_write_real", 2);
           AddFunction("registry_read_string", 1);
           AddFunction("registry_read_real", 1);
           AddFunction("registry_exists", 1);
           AddFunction("registry_write_string_ext", 3);
           AddFunction("registry_write_real_ext", 3);
           AddFunction("registry_read_string_ext", 2);
           AddFunction("registry_read_real_ext", 2);
           AddFunction("registry_exists_ext", 2);
           AddFunction("registry_set_root", 1);
           AddFunction("ini_open_from_string", 1, GM_Type.Int);
           AddFunction("ini_open", 1,GM_Type.Int);
           AddFunction("ini_close", 0);
           AddFunction("ini_read_string", 3, GM_Type.String);
           AddFunction("ini_read_real", 3, GM_Type.Float);
           AddFunction("ini_write_string", 3);
           AddFunction("ini_write_real", 3);
           AddFunction("ini_key_exists", 2);
           AddFunction("ini_section_exists", 1);
           AddFunction("ini_key_delete", 2);
           AddFunction("ini_section_delete", 1);
           AddFunction("http_post_string", 2);
           AddFunction("http_get", 1);
           AddFunction("http_get_file", 2);
           AddFunction("http_request", 4);
           AddFunction("json_encode", 1);
           AddFunction("json_decode", 1);
           AddFunction("zip_unzip", 2);
           AddFunction("move_random", 2);
           AddFunction("place_free", 2);
           AddFunction("place_empty", 2);
           AddFunction("place_meeting", 3);
           AddFunction("place_snapped", 2);
           AddFunction("move_snap", 2);
           AddFunction("move_towards_point", 3);
           AddFunction("move_contact", 1);
           AddFunction("move_contact_solid", 2);
           AddFunction("move_contact_all", 2);
           AddFunction("move_outside_solid", 2);
           AddFunction("move_outside_all", 2);
           AddFunction("move_bounce", 1);
           AddFunction("move_bounce_solid", 1);
           AddFunction("move_bounce_all", 1);
           AddFunction("move_wrap", 3);
           AddFunction("motion_set", 2);
           AddFunction("motion_add", 2);
           AddFunction("distance_to_point", 2, GM_Type.Float);
           AddFunction("distance_to_object", 1, GM_Type.Float);
           AddFunction("path_start", 4);
           AddFunction("path_end", 0);
           AddFunction("mp_linear_step", 4);
           AddFunction("mp_linear_path", 5);
           AddFunction("mp_linear_step_object", 4);
           AddFunction("mp_linear_path_object", 5);
           AddFunction("mp_potential_settings", 4);
           AddFunction("mp_potential_step", 4);
           AddFunction("mp_potential_path", 6);
           AddFunction("mp_potential_step_object", 4);
           AddFunction("mp_potential_path_object", 6);
           AddFunction("mp_grid_create", 6);
           AddFunction("mp_grid_destroy", 1);
           AddFunction("mp_grid_clear_all", 1);
           AddFunction("mp_grid_clear_cell", 3);
           AddFunction("mp_grid_clear_rectangle", 5);
           AddFunction("mp_grid_add_cell", 3);
           AddFunction("mp_grid_get_cell", 3);
           AddFunction("mp_grid_add_rectangle", 5);
           AddFunction("mp_grid_add_instances", 3);
           AddFunction("mp_grid_path", 7);
           AddFunction("mp_grid_draw", 1);
           AddFunction("mp_grid_to_ds_grid", 2);
           AddFunction("collision_point", 5, GM_Type.Instance);
           AddFunction("collision_rectangle", 7,GM_Type.Instance);
           AddFunction("collision_circle", 6);
           AddFunction("collision_ellipse", 7);
           AddFunction("collision_line", 7, GM_Type.Instance);
           AddFunction("point_in_rectangle", 6);
           AddFunction("point_in_triangle", 8);
           AddFunction("point_in_circle", 5);
           AddFunction("rectangle_in_rectangle", 8);
           AddFunction("rectangle_in_triangle", 10);
           AddFunction("rectangle_in_circle", 7);
           AddFunction("instance_find", 2, GM_Type.Instance);
           AddFunction("instance_exists", 1, GM_Type.Bool);
           AddFunction("instance_number", 1, GM_Type.Int);
           AddFunction("instance_position", 3, GM_Type.Instance);
           AddFunction("instance_nearest", 3);
           AddFunction("instance_furthest", 3);
           AddFunction("instance_place", 3);
           AddFunction("instance_create", 3, GM_Type.Instance);
           AddFunction("instance_copy", 1);
           AddFunction("instance_change", 2);
           AddFunction("instance_destroy", 0);
           AddFunction("instance_sprite", 1, GM_Type.Sprite);
           AddFunction("position_empty", 2);
           AddFunction("position_meeting", 3);
           AddFunction("position_destroy", 2);
           AddFunction("position_change", 4);
           AddFunction("instance_deactivate_all", 1);
           AddFunction("instance_deactivate_object", 1);
           AddFunction("instance_deactivate_region", 6);
           AddFunction("instance_activate_all", 0);
           AddFunction("instance_activate_object", 1);
           AddFunction("instance_activate_region", 5);
           AddFunction("room_goto", 1);
           AddFunction("room_goto_previous", 0);
           AddFunction("room_goto_next", 0);
           AddFunction("room_previous", 1);
           AddFunction("room_next", 1);
           AddFunction("room_restart", 0);
           AddFunction("game_end", 0);
           AddFunction("game_restart", 0);
           AddFunction("game_load", 1);
           AddFunction("game_save", 1);
           AddFunction("game_save_buffer", 1);
           AddFunction("game_load_buffer", 1);
           AddFunction("transition_define", 2);
           AddFunction("transition_exists", 1);
           AddFunction("sleep", 1);
           AddFunction("display_get_width", 0);
           AddFunction("display_get_height", 0);
           AddFunction("display_get_colordepth", 0);
           AddFunction("display_get_frequency", 0);
           AddFunction("display_get_orientation", 0);
           AddFunction("display_get_gui_width", 0);
           AddFunction("display_get_gui_height", 0);
           AddFunction("display_set_size", 2);
           AddFunction("display_set_colordepth", 1);
           AddFunction("display_set_frequency", 1);
           AddFunction("display_set_all", 4);
           AddFunction("display_test_all", 4);
           AddFunction("display_reset", 0);
           AddFunction("display_mouse_get_x", 0);
           AddFunction("display_mouse_get_y", 0);
           AddFunction("display_mouse_set", 2);
           AddFunction("draw_enable_drawevent", 1);
           AddFunction("device_mouse_x_to_gui", 1);
           AddFunction("device_mouse_y_to_gui", 1);
           AddFunction("window_set_visible", 1);
           AddFunction("window_get_visible", 0);
           AddFunction("window_set_fullscreen", 1);
           AddFunction("window_get_fullscreen", 0);
           AddFunction("window_set_showborder", 1);
           AddFunction("window_get_showborder", 0);
           AddFunction("window_set_showicons", 1);
           AddFunction("window_get_showicons", 0);
           AddFunction("window_set_stayontop", 1);
           AddFunction("window_get_stayontop", 0);
           AddFunction("window_set_sizeable", 1);
           AddFunction("window_get_sizeable", 0);
           AddFunction("window_set_caption", 1);
           AddFunction("window_get_caption", 0);
           AddFunction("window_set_cursor", 1);
           AddFunction("window_get_cursor", 0);
           AddFunction("window_set_color", 1);
           AddFunction("window_get_color", 0);
           AddFunction("window_set_colour", 1);
           AddFunction("window_get_colour", 0);
           AddFunction("window_set_min_width", 1);
           AddFunction("window_set_max_width", 1);
           AddFunction("window_set_min_height", 1);
           AddFunction("window_set_max_height", 1);
           AddFunction("window_set_position", 2);
           AddFunction("window_set_size", 2);
           AddFunction("window_set_rectangle", 4);
           AddFunction("window_center", 0);
           AddFunction("window_default", 0);
           AddFunction("window_get_x", 0, GM_Type.Int);
           AddFunction("window_get_y", 0, GM_Type.Int);
           AddFunction("window_get_width", 0, GM_Type.Int);
           AddFunction("window_get_height", 0, GM_Type.Int);
           AddFunction("window_set_region_size", 3);
           AddFunction("window_get_region_width", 0);
           AddFunction("window_get_region_height", 0);
           AddFunction("window_set_region_scale", 2);
           AddFunction("window_get_region_scale", 0);
           AddFunction("window_mouse_get_x", 0);
           AddFunction("window_mouse_get_y", 0);
           AddFunction("window_mouse_set", 2);
           AddFunction("window_view_mouse_get_x", 1);
           AddFunction("window_view_mouse_get_y", 1);
           AddFunction("window_view_mouse_set", 3);
           AddFunction("window_views_mouse_get_x", 0);
           AddFunction("window_views_mouse_get_y", 0);
           AddFunction("window_views_mouse_set", 2);
           AddFunction("window_get_visible_rects", 4);
           AddFunction("set_synchronization", 1);
           AddFunction("set_automatic_draw", 1);
           AddFunction("screen_redraw", 0);
           AddFunction("screen_refresh", 0);
           AddFunction("screen_wait_vsync", 0);
           AddFunction("screen_save", 1);
           AddFunction("screen_save_part", 5);
           AddFunction("draw_getpixel", 2);
           AddFunction("draw_getpixel_ext", 2);
           AddFunction("draw_set_color", 1);
           AddFunction("draw_set_colour", 1);
           AddFunction("draw_set_alpha", 1);
           AddFunction("draw_get_color", 0);
           AddFunction("draw_get_colour", 0);
           AddFunction("draw_get_alpha", 0);
           AddFunction("merge_color", 3, GM_Type.Int);
           AddFunction("make_color", 3, GM_Type.Int);
           AddFunction("make_color_rgb", 3, GM_Type.Int);
           AddFunction("make_color_hsv", 3, GM_Type.Int);
           AddFunction("color_get_red", 1, GM_Type.Int);
           AddFunction("color_get_green", 1, GM_Type.Int);
           AddFunction("color_get_blue", 1, GM_Type.Int);
           AddFunction("color_get_hue", 1, GM_Type.Int);
           AddFunction("color_get_saturation", 1, GM_Type.Int);
           AddFunction("color_get_value", 1, GM_Type.Int);
           AddFunction("merge_colour", 3, GM_Type.Int);
           AddFunction("make_colour", 3, GM_Type.Int);
           AddFunction("make_colour_rgb", 3, GM_Type.Int);
           AddFunction("make_colour_hsv", 3, GM_Type.Int);
           AddFunction("colour_get_red", 1, GM_Type.Int);
           AddFunction("colour_get_green", 1, GM_Type.Int);
           AddFunction("colour_get_blue", 1, GM_Type.Int);
           AddFunction("colour_get_hue", 1, GM_Type.Int);
           AddFunction("colour_get_saturation", 1, GM_Type.Int);
           AddFunction("colour_get_value", 1, GM_Type.Int);
           AddFunction("draw_set_blend_mode", 1);
           AddFunction("draw_set_blend_mode_ext", 2);
           AddFunction("draw_set_color_write_enable", 4);
           AddFunction("draw_set_colour_write_enable", 4);
           AddFunction("draw_set_alpha_test", 1);
           AddFunction("draw_set_alpha_test_ref_value", 1);
           AddFunction("draw_get_alpha_test", 0);
           AddFunction("draw_get_alpha_test_ref_value", 0);
           AddFunction("draw_clear", 1);
           AddFunction("draw_clear_alpha", 2);
           AddFunction("draw_point", 2);
           AddFunction("draw_line", 4);
           AddFunction("draw_line_width", 5);
           AddFunction("draw_rectangle", 5);
           AddFunction("draw_roundrect", 5);
           AddFunction("draw_roundrect_ext", 7);
           AddFunction("draw_triangle", 7);
           AddFunction("draw_circle", 4);
           AddFunction("draw_ellipse", 5);
           AddFunction("draw_arrow", 5);
           AddFunction("draw_button", 5);
           AddFunction("draw_healthbar", 11);
           AddFunction("draw_path", 4);
           AddFunction("draw_point_color", 3);
           AddFunction("draw_line_color", 6);
           AddFunction("draw_line_width_color", 7);
           AddFunction("draw_rectangle_color", 9);
           AddFunction("draw_roundrect_color", 7);
           AddFunction("draw_roundrect_color_ext", 9);
           AddFunction("draw_triangle_color", 10);
           AddFunction("draw_circle_color", 6);
           AddFunction("draw_ellipse_color", 7);
           AddFunction("draw_point_colour", 3);
           AddFunction("draw_line_colour", 6);
           AddFunction("draw_line_width_colour", 7);
           AddFunction("draw_rectangle_colour", 9);
           AddFunction("draw_roundrect_colour", 7);
           AddFunction("draw_roundrect_colour_ext", 9);
           AddFunction("draw_triangle_colour", 10);
           AddFunction("draw_circle_colour", 6);
           AddFunction("draw_ellipse_colour", 7);
           AddFunction("draw_set_circle_precision", 1);
           AddFunction("draw_primitive_begin", 1);
           AddFunction("draw_primitive_begin_texture", 2);
           AddFunction("draw_primitive_end", 0);
           AddFunction("draw_vertex", 2);
           AddFunction("draw_vertex_color", 4);
           AddFunction("draw_vertex_colour", 4);
           AddFunction("draw_vertex_texture", 4);
           AddFunction("draw_vertex_texture_color", 6);
           AddFunction("draw_vertex_texture_colour", 6);
           AddFunction("sprite_get_uvs", 2);
           AddFunction("background_get_uvs", 1);
           AddFunction("font_get_uvs", 1);
           AddFunction("sprite_get_texture", 2);
           AddFunction("background_get_texture", 1);
           AddFunction("font_get_texture", 1);
           AddFunction("texture_exists", 1, GM_Type.Int);
           AddFunction("texture_set_interpolation", 1);
           AddFunction("texture_set_interpolation_ext", 2);
           AddFunction("texture_set_blending", 1);
           AddFunction("texture_set_repeat", 1);
           AddFunction("texture_set_repeat_ext", 2);
           AddFunction("texture_get_width", 1);
           AddFunction("texture_get_height", 1);
           AddFunction("texture_preload", 1);
           AddFunction("texture_set_priority", 2);
           AddFunction("draw_enable_swf_aa", 1);
           AddFunction("draw_set_swf_aa_level", 1);
           AddFunction("draw_get_swf_aa_level", 0);
           AddFunction("draw_set_font", 1);
           AddFunction("draw_set_halign", 1);
           AddFunction("draw_set_valign", 1);
           AddFunction("string_width", 1, GM_Type.Int);
           AddFunction("string_height", 1, GM_Type.Int);
           AddFunction("string_width_ext", 3, GM_Type.Int);
           AddFunction("string_height_ext", 3, GM_Type.Int);
           AddFunction("draw_text", 3);
           AddFunction("draw_text_ext", 5);
           AddFunction("draw_text_transformed", 6);
           AddFunction("draw_text_ext_transformed", 8);
           AddFunction("draw_text_color", 8);
           AddFunction("draw_text_transformed_color", 11);
           AddFunction("draw_text_ext_color", 10);
           AddFunction("draw_text_ext_transformed_color", 13);
           AddFunction("draw_text_colour", 8);
           AddFunction("draw_text_transformed_colour", 11);
           AddFunction("draw_text_ext_colour", 10);
           AddFunction("draw_text_ext_transformed_colour", 13);
           AddFunction("shader_enable_corner_id", 1);
           AddFunction("draw_self", 0);
           AddFunction("draw_sprite", 4);
           AddFunction("draw_sprite_pos", 11);
           AddFunction("draw_sprite_ext", 9);
           AddFunction("draw_sprite_stretched", 6);
           AddFunction("draw_sprite_stretched_ext", 8);
           AddFunction("draw_sprite_part", 8);
           AddFunction("draw_sprite_part_ext", 12);
           AddFunction("draw_sprite_general", 16);
           AddFunction("draw_sprite_tiled", 4);
           AddFunction("draw_sprite_tiled_ext", 8);
           AddFunction("draw_background", 3);
           AddFunction("draw_background_ext", 8);
           AddFunction("draw_background_stretched", 5);
           AddFunction("draw_background_stretched_ext", 7);
           AddFunction("draw_background_part", 7);
           AddFunction("draw_background_part_ext", 11);
           AddFunction("draw_background_general", 15);
           AddFunction("draw_background_tiled", 3);
           AddFunction("draw_background_tiled_ext", 7);
           AddFunction("tile_get_x", 1);
           AddFunction("tile_get_y", 1);
           AddFunction("tile_get_left", 1);
           AddFunction("tile_get_top", 1);
           AddFunction("tile_get_width", 1);
           AddFunction("tile_get_height", 1);
           AddFunction("tile_get_depth", 1);
           AddFunction("tile_get_visible", 1);
           AddFunction("tile_get_xscale", 1);
           AddFunction("tile_get_yscale", 1);
           AddFunction("tile_get_blend", 1);
           AddFunction("tile_get_alpha", 1);
           AddFunction("tile_get_background", 1);
           AddFunction("tile_set_visible", 2);
           AddFunction("tile_set_background", 2);
           AddFunction("tile_set_region", 5);
           AddFunction("tile_set_position", 3);
           AddFunction("tile_set_depth", 2);
           AddFunction("tile_set_scale", 3);
           AddFunction("tile_set_blend", 2);
           AddFunction("tile_set_alpha", 2);
           AddFunction("tile_add", 8);
           AddFunction("tile_get_count", 0);
           AddFunction("tile_get_id", 1);
           AddFunction("tile_get_ids", 0);
           AddFunction("tile_get_ids_at_depth", 1);
           AddFunction("tile_find", 3);
           AddFunction("tile_exists", 1);
           AddFunction("tile_delete", 1);
           AddFunction("tile_delete_at", 3);
           AddFunction("tile_layer_hide", 1);
           AddFunction("tile_layer_show", 1);
           AddFunction("tile_layer_delete", 1);
           AddFunction("tile_layer_shift", 3);
           AddFunction("tile_layer_find", 3);
           AddFunction("tile_layer_delete_at", 3);
           AddFunction("tile_layer_depth", 2);
           AddFunction("surface_create", 2);
           AddFunction("surface_create_ext", 3);
           AddFunction("surface_resize", 3);
           AddFunction("surface_free", 1);
           AddFunction("surface_exists", 1);
           AddFunction("surface_get_width", 1);
           AddFunction("surface_get_height", 1);
           AddFunction("surface_get_texture", 1);
           AddFunction("surface_set_target", 1);
           AddFunction("surface_set_target_ext", 2);
           AddFunction("surface_reset_target", 0);
           AddFunction("draw_surface", 3);
           AddFunction("draw_surface_ext", 8);
           AddFunction("draw_surface_stretched", 5);
           AddFunction("draw_surface_stretched_ext", 7);
           AddFunction("draw_surface_part", 7);
           AddFunction("draw_surface_part_ext", 11);
           AddFunction("draw_surface_general", 15);
           AddFunction("draw_surface_tiled", 3);
           AddFunction("draw_surface_tiled_ext", 7);
           AddFunction("surface_save", 2);
           AddFunction("surface_save_part", 6);
           AddFunction("surface_getpixel", 3);
           AddFunction("surface_getpixel_ext", 3);
           AddFunction("surface_copy", 4);
           AddFunction("surface_copy_part", 8);
           AddFunction("application_surface_draw_enable", 1);
           AddFunction("application_get_position", 0);
           AddFunction("application_surface_enable", 1);
           AddFunction("application_surface_is_enabled", 0);
           AddFunction("splash_show_video", 2);
           AddFunction("splash_show_text", 2);
           AddFunction("splash_show_image", 2);
           AddFunction("splash_show_web", 2);
           AddFunction("splash_set_caption", 1);
           AddFunction("splash_set_fullscreen", 1);
           AddFunction("splash_set_border", 1);
           AddFunction("splash_set_size", 2);
           AddFunction("splash_set_adapt", 1);
           AddFunction("splash_set_top", 1);
           AddFunction("splash_set_color", 1);
           AddFunction("splash_set_main", 1);
           AddFunction("splash_set_scale", 1);
           AddFunction("splash_set_cursor", 1);
           AddFunction("splash_set_interrupt", 1);
           AddFunction("splash_set_stop_key", 1);
           AddFunction("splash_set_stop_mouse", 1);
           AddFunction("splash_set_close_button", 1);
           AddFunction("splash_set_position", 1);
           AddFunction("show_message", 1);
           AddFunction("show_question", 1);
           AddFunction("show_message_async", 1);
           AddFunction("show_question_async", 1);
           AddFunction("show_error", 2);
           AddFunction("show_info", 0);
           AddFunction("load_info", 1);
           AddFunction("highscore_show", 1);
           AddFunction("highscore_set_background", 1);
           AddFunction("highscore_set_border", 1);
           AddFunction("highscore_set_font", 3);
           AddFunction("highscore_set_strings", 3);
           AddFunction("highscore_set_colors", 3);
           AddFunction("highscore_show_ext", 7);
           AddFunction("highscore_clear", 0);
           AddFunction("highscore_add", 2);
           AddFunction("highscore_add_current", 0);
           AddFunction("highscore_value", 1);
           AddFunction("highscore_name", 1);
           AddFunction("draw_highscore", 4);
           AddFunction("show_message_ext", 4);
           AddFunction("message_background", 1);
           AddFunction("message_button", 1);
           AddFunction("message_alpha", 1);
           AddFunction("message_text_font", 4);
           AddFunction("message_button_font", 4);
           AddFunction("message_input_font", 4);
           AddFunction("message_mouse_color", 1);
           AddFunction("message_input_color", 1);
           AddFunction("message_position", 2);
           AddFunction("message_size", 2);
           AddFunction("message_caption", 2);
           AddFunction("clickable_add", 6);
           AddFunction("clickable_add_ext", 8);
           AddFunction("clickable_change", 4);
           AddFunction("clickable_change_ext", 6);
           AddFunction("clickable_delete", 1);
           AddFunction("clickable_exists", 1);
           AddFunction("clickable_set_style", 2);
           AddFunction("show_menu", 2);
           AddFunction("show_menu_pos", 4);
           AddFunction("get_integer", 2);
           AddFunction("get_string", 2);
           AddFunction("get_integer_async", 2);
           AddFunction("get_string_async", 2);
           AddFunction("get_login_async", 2);
           AddFunction("get_color", 1);
           AddFunction("get_open_filename", 2);
           AddFunction("get_save_filename", 2);
           AddFunction("get_open_filename_ext", 4);
           AddFunction("get_save_filename_ext", 4);
           AddFunction("get_directory", 1);
           AddFunction("get_directory_alt", 2);
           AddFunction("keyboard_get_numlock", 0);
           AddFunction("keyboard_set_numlock", 1);
           AddFunction("keyboard_key_press", 1);
           AddFunction("keyboard_key_release", 1);
           AddFunction("keyboard_set_map", 2);
           AddFunction("keyboard_get_map", 1);
           AddFunction("keyboard_unset_map", 0);
           AddFunction("keyboard_check", 1, GM_Type.Int);
           AddFunction("keyboard_check_pressed", 1, GM_Type.Bool);
           AddFunction("keyboard_check_released", 1, GM_Type.Bool);
           AddFunction("keyboard_check_direct", 1, GM_Type.Int);
           AddFunction("mouse_check_button", 1);
           AddFunction("mouse_check_button_pressed", 1);
           AddFunction("mouse_check_button_released", 1);
           AddFunction("mouse_wheel_up", 0);
           AddFunction("mouse_wheel_down", 0);
           AddFunction("joystick_exists", 1);
           AddFunction("joystick_direction", 1, GM_Type.Int);
           AddFunction("joystick_name", 1);
           AddFunction("joystick_axes", 1);
           AddFunction("joystick_buttons", 1);
           AddFunction("joystick_has_pov", 1);
           AddFunction("joystick_check_button", 2, GM_Type.Bool);
           AddFunction("joystick_xpos", 1, GM_Type.Float);
           AddFunction("joystick_ypos", 1,GM_Type.Float);
           AddFunction("joystick_zpos", 1, GM_Type.Float);
           AddFunction("joystick_rpos", 1, GM_Type.Float);
           AddFunction("joystick_upos", 1, GM_Type.Float);
           AddFunction("joystick_vpos", 1, GM_Type.Float);
           AddFunction("joystick_pov", 1, GM_Type.Int);
           AddFunction("keyboard_clear", 1);
           AddFunction("mouse_clear", 1);
           AddFunction("io_clear", 0);
           AddFunction("io_handle", 0);
           AddFunction("device_mouse_dbclick_enable", 1);
           AddFunction("keyboard_wait", 0);
           AddFunction("mouse_wait", 0);
           AddFunction("is_real", 1);
           AddFunction("is_string", 1);
           AddFunction("is_array", 1);
           AddFunction("is_undefined", 1);
           AddFunction("is_int32", 1);
           AddFunction("is_int64", 1);
           AddFunction("is_ptr", 1);
           AddFunction("is_vec3", 1);
           AddFunction("is_vec4", 1);
           AddFunction("is_matrix", 1);
           AddFunction("array_length_1d", 1);
           AddFunction("array_length_2d", 2);
           AddFunction("array_height_2d", 1);
           AddFunction("array_set", 3);
           AddFunction("array_set_pre", 3);
           AddFunction("array_set_post", 3);
           AddFunction("array_get", 2);
           AddFunction("array_set_2D", 4);
           AddFunction("array_set_2D_pre", 4);
           AddFunction("array_set_2D_post", 4);
           AddFunction("array_get_2D", 3);
           AddFunction("random", 1, GM_Type.Float);
           AddFunction("random_range", 2);
           AddFunction("irandom", 1);
           AddFunction("irandom_range", 2);
           AddFunction("random_set_seed", 1);
           AddFunction("random_get_seed", 0);
           AddFunction("randomize", 0);
           AddFunction("abs", 1, GM_Type.Int);
           AddFunction("round", 1, GM_Type.Double);
           AddFunction("floor", 1, GM_Type.Double);
           AddFunction("ceil", 1, GM_Type.Double);
           AddFunction("sign", 1);
           AddFunction("frac", 1, GM_Type.Double);
           AddFunction("sqrt", 1, GM_Type.Double);
           AddFunction("sqr", 1, GM_Type.Double);
           AddFunction("exp", 1, GM_Type.Double);
           AddFunction("ln", 1, GM_Type.Double);
           AddFunction("log2", 1, GM_Type.Double);
           AddFunction("log10", 1, GM_Type.Double);
           AddFunction("sin", 1, GM_Type.Double);
           AddFunction("cos", 1, GM_Type.Double);
           AddFunction("tan", 1, GM_Type.Double);
           AddFunction("arcsin", 1, GM_Type.Double);
           AddFunction("arccos", 1, GM_Type.Double);
           AddFunction("arctan", 1, GM_Type.Double);
           AddFunction("arctan2", 2, GM_Type.Double);
           AddFunction("dsin", 1, GM_Type.Double);
           AddFunction("dcos", 1, GM_Type.Double);
           AddFunction("dtan", 1, GM_Type.Double);
           AddFunction("darcsin", 1, GM_Type.Double);
           AddFunction("darccos", 1, GM_Type.Double);
           AddFunction("darctan", 1, GM_Type.Double);
           AddFunction("darctan2", 2, GM_Type.Double);
           AddFunction("degtorad", 1, GM_Type.Double);
           AddFunction("radtodeg", 1, GM_Type.Double);
           AddFunction("power", 2, GM_Type.Double);
           AddFunction("logn", 2, GM_Type.Double);
           AddFunction("min", -1, GM_Type.Double);
           AddFunction("max", -1, GM_Type.Double);
           AddFunction("min3", 3, GM_Type.Double);
           AddFunction("max3", 3, GM_Type.Double);
           AddFunction("mean", -1, GM_Type.Double);
           AddFunction("median", -1, GM_Type.Double);
           AddFunction("choose", -1, GM_Type.Double);
           AddFunction("clamp", 3, GM_Type.Double);
           AddFunction("lerp", 3, GM_Type.Double);
           AddFunction("dot_product", 4);
           AddFunction("dot_product_3d", 6);
           AddFunction("dot_product_normalised", 4);
           AddFunction("dot_product_3d_normalised", 6);
           AddFunction("math_set_epsilon", 1);
           AddFunction("math_get_epsilon", 0);
           AddFunction("angle_difference", 2);
           AddFunction("real", 1, GM_Type.Double);
           AddFunction("string", 1, GM_Type.String);
           AddFunction("int64", 1);
           AddFunction("ptr", 1);
           AddFunction("string_format", 3);
           AddFunction("chr", 1);
           AddFunction("ansi_char", 1);
           AddFunction("ord", 1, GM_Type.Int);
           AddFunction("string_length", 1,GM_Type.Int);
           AddFunction("string_byte_length", 1, GM_Type.Int);
           AddFunction("string_pos", 2);
           AddFunction("string_copy", 3);
           AddFunction("string_char_at", 2, GM_Type.String);
           AddFunction("string_ord_at", 2);
           AddFunction("string_byte_at", 2);
           AddFunction("string_set_byte_at", 3);
           AddFunction("string_delete", 3);
           AddFunction("string_insert", 3);
           AddFunction("string_lower", 1, GM_Type.String);
           AddFunction("string_upper", 1, GM_Type.String);
           AddFunction("string_repeat", 2);
           AddFunction("string_letters", 1);
           AddFunction("string_digits", 1);
           AddFunction("string_lettersdigits", 1);
           AddFunction("string_replace", 3);
           AddFunction("string_replace_all", 3);
           AddFunction("string_count", 2);
           AddFunction("point_distance", 4, GM_Type.Float);
           AddFunction("point_distance_3d", 6, GM_Type.Float);
           AddFunction("point_direction", 4, GM_Type.Float);
           AddFunction("lengthdir_x", 2, GM_Type.Float);
           AddFunction("lengthdir_y", 2, GM_Type.Float);
           AddFunction("event_inherited", 0);
           AddFunction("event_perform", 2);
           AddFunction("event_user", 1);
           AddFunction("event_perform_object", 3);
           AddFunction("external_define", -1);
           AddFunction("external_call", -1);
           AddFunction("external_free", 1);
           AddFunction("external_define0", 3);
           AddFunction("external_call0", 1);
           AddFunction("external_define1", 4);
           AddFunction("external_call1", 2);
           AddFunction("external_define2", 5);
           AddFunction("external_call2", 3);
           AddFunction("external_define3", 6);
           AddFunction("external_call3", 4);
           AddFunction("external_define4", 7);
           AddFunction("external_call4", 5);
           AddFunction("external_define5", 3);
           AddFunction("external_call5", 6);
           AddFunction("external_define6", 3);
           AddFunction("external_call6", 7);
           AddFunction("external_define7", 3);
           AddFunction("external_call7", 8);
           AddFunction("external_define8", 3);
           AddFunction("external_call8", 9);
           AddFunction("window_handle", 0);
           AddFunction("window_device", 0);
           AddFunction("show_debug_message", 1);
           AddFunction("show_debug_overlay", 1);
           AddFunction("set_program_priority", 1);
           AddFunction("set_application_title", 1);
           AddFunction("variable_global_exists", 1);
           AddFunction("variable_global_get", 1);
           AddFunction("variable_global_array_get", 2);
           AddFunction("variable_global_array2_get", 3);
           AddFunction("variable_global_set", 2);
           AddFunction("variable_global_array_set", 3);
           AddFunction("variable_global_array2_set", 4);
           AddFunction("variable_local_exists", 1);
           AddFunction("variable_local_get", 1);
           AddFunction("variable_local_array_get", 2);
           AddFunction("variable_local_array2_get", 3);
           AddFunction("variable_local_set", 2);
           AddFunction("variable_local_array_set", 3);
           AddFunction("variable_local_array2_set", 4);
           AddFunction("clipboard_has_text", 0);
           AddFunction("clipboard_set_text", 1);
           AddFunction("clipboard_get_text", 0);
           AddFunction("date_current_datetime", 0);
           AddFunction("date_current_date", 0);
           AddFunction("date_current_time", 0);
           AddFunction("date_create_datetime", 6);
           AddFunction("date_create_date", 3);
           AddFunction("date_create_time", 3);
           AddFunction("date_valid_datetime", 6);
           AddFunction("date_valid_date", 3);
           AddFunction("date_valid_time", 3);
           AddFunction("date_inc_year", 2);
           AddFunction("date_inc_month", 2);
           AddFunction("date_inc_week", 2);
           AddFunction("date_inc_day", 2);
           AddFunction("date_inc_hour", 2);
           AddFunction("date_inc_minute", 2);
           AddFunction("date_inc_second", 2);
           AddFunction("date_get_year", 1);
           AddFunction("date_get_month", 1);
           AddFunction("date_get_week", 1);
           AddFunction("date_get_day", 1);
           AddFunction("date_get_hour", 1);
           AddFunction("date_get_minute", 1);
           AddFunction("date_get_second", 1);
           AddFunction("date_get_weekday", 1);
           AddFunction("date_get_day_of_year", 1);
           AddFunction("date_get_hour_of_year", 1);
           AddFunction("date_get_minute_of_year", 1);
           AddFunction("date_get_second_of_year", 1);
           AddFunction("date_year_span", 2);
           AddFunction("date_month_span", 2);
           AddFunction("date_week_span", 2);
           AddFunction("date_day_span", 2);
           AddFunction("date_hour_span", 2);
           AddFunction("date_minute_span", 2);
           AddFunction("date_second_span", 2);
           AddFunction("date_compare_datetime", 2);
           AddFunction("date_compare_date", 2);
           AddFunction("date_compare_time", 2);
           AddFunction("date_date_of", 1);
           AddFunction("date_time_of", 1);
           AddFunction("date_datetime_string", 1);
           AddFunction("date_date_string", 1);
           AddFunction("date_time_string", 1);
           AddFunction("date_days_in_month", 1);
           AddFunction("date_days_in_year", 1);
           AddFunction("date_leap_year", 1);
           AddFunction("date_is_today", 1);
           AddFunction("date_set_timezone", 1);
           AddFunction("date_get_timezone", 0);
           AddFunction("part_type_create", 0);
           AddFunction("part_type_destroy", 1);
           AddFunction("part_type_exists", 1);
           AddFunction("part_type_clear", 1);
           AddFunction("part_type_shape", 2);
           AddFunction("part_type_sprite", 5);
           AddFunction("part_type_size", 5);
           AddFunction("part_type_scale", 3);
           AddFunction("part_type_life", 3);
           AddFunction("part_type_step", 3);
           AddFunction("part_type_death", 3);
           AddFunction("part_type_speed", 5);
           AddFunction("part_type_direction", 5);
           AddFunction("part_type_orientation", 6);
           AddFunction("part_type_gravity", 3);
           AddFunction("part_type_color_mix", 3);
           AddFunction("part_type_color_rgb", 7);
           AddFunction("part_type_color_hsv", 7);
           AddFunction("part_type_color1", 2);
           AddFunction("part_type_color2", 3);
           AddFunction("part_type_color3", 4);
           AddFunction("part_type_color", 4);
           AddFunction("part_type_colour_mix", 3);
           AddFunction("part_type_colour_rgb", 7);
           AddFunction("part_type_colour_hsv", 7);
           AddFunction("part_type_colour1", 2);
           AddFunction("part_type_colour2", 3);
           AddFunction("part_type_colour3", 4);
           AddFunction("part_type_colour", 4);
           AddFunction("part_type_alpha1", 2);
           AddFunction("part_type_alpha2", 3);
           AddFunction("part_type_alpha3", 4);
           AddFunction("part_type_alpha", 4);
           AddFunction("part_type_blend", 2);
           AddFunction("part_system_create", 0);
           AddFunction("part_system_destroy", 1);
           AddFunction("part_system_exists", 1);
           AddFunction("part_system_clear", 1);
           AddFunction("part_system_draw_order", 2);
           AddFunction("part_system_depth", 2);
           AddFunction("part_system_position", 3);
           AddFunction("part_system_automatic_update", 2);
           AddFunction("part_system_automatic_draw", 2);
           AddFunction("part_system_update", 1);
           AddFunction("part_system_drawit", 1);
           AddFunction("part_particles_create", 5);
           AddFunction("part_particles_create_color", 6);
           AddFunction("part_particles_create_colour", 6);
           AddFunction("part_particles_clear", 1);
           AddFunction("part_particles_count", 1);
           AddFunction("part_emitter_create", 1);
           AddFunction("part_emitter_destroy", 2);
           AddFunction("part_emitter_destroy_all", 1);
           AddFunction("part_emitter_exists", 2);
           AddFunction("part_emitter_clear", 2);
           AddFunction("part_emitter_region", 8);
           AddFunction("part_emitter_burst", 4);
           AddFunction("part_emitter_stream", 4);
           AddFunction("part_attractor_create", 1);
           AddFunction("part_attractor_destroy", 2);
           AddFunction("part_attractor_destroy_all", 1);
           AddFunction("part_attractor_exists", 2);
           AddFunction("part_attractor_clear", 2);
           AddFunction("part_attractor_position", 4);
           AddFunction("part_attractor_force", 6);
           AddFunction("part_destroyer_create", 1);
           AddFunction("part_destroyer_destroy", 2);
           AddFunction("part_destroyer_destroy_all", 1);
           AddFunction("part_destroyer_exists", 2);
           AddFunction("part_destroyer_clear", 2);
           AddFunction("part_destroyer_region", 7);
           AddFunction("part_deflector_create", 1);
           AddFunction("part_deflector_destroy", 2);
           AddFunction("part_deflector_destroy_all", 1);
           AddFunction("part_deflector_exists", 2);
           AddFunction("part_deflector_clear", 2);
           AddFunction("part_deflector_region", 6);
           AddFunction("part_deflector_kind", 3);
           AddFunction("part_deflector_friction", 3);
           AddFunction("part_changer_create", 1);
           AddFunction("part_changer_destroy", 2);
           AddFunction("part_changer_destroy_all", 1);
           AddFunction("part_changer_exists", 2);
           AddFunction("part_changer_clear", 2);
           AddFunction("part_changer_region", 7);
           AddFunction("part_changer_kind", 3);
           AddFunction("part_changer_types", 4);
           AddFunction("effect_create_below", 5);
           AddFunction("effect_create_above", 5);
           AddFunction("effect_clear", 0);
           AddFunction("sprite_name", 1);
           AddFunction("sprite_exists", 1,GM_Type.Bool);
           AddFunction("sprite_get_name", 1, GM_Type.String);
           AddFunction("sprite_get_number", 1, GM_Type.Int);
           AddFunction("sprite_get_width", 1, GM_Type.Int);
           AddFunction("sprite_get_height", 1, GM_Type.Int);
           AddFunction("sprite_get_transparent", 1, GM_Type.Bool);
           AddFunction("sprite_get_smooth", 1, GM_Type.Bool);
           AddFunction("sprite_get_preload", 1, GM_Type.Bool);
           AddFunction("sprite_get_xoffset", 1, GM_Type.Int);
           AddFunction("sprite_get_yoffset", 1, GM_Type.Int);
           AddFunction("sprite_get_bbox_mode", 1, GM_Type.Int);
           AddFunction("sprite_get_bbox_left", 1, GM_Type.Int);
           AddFunction("sprite_get_bbox_right", 1, GM_Type.Int);
           AddFunction("sprite_get_bbox_top", 1, GM_Type.Int);
           AddFunction("sprite_get_bbox_bottom", 1, GM_Type.Int);
           AddFunction("sprite_get_precise", 1, GM_Type.Bool);
           AddFunction("sprite_collision_mask", 9);
           AddFunction("sprite_get_tpe", 2);
           AddFunction("sprite_set_offset", 3);
           AddFunction("sprite_set_bbox_mode", 2);
           AddFunction("sprite_set_bbox", 5);
           AddFunction("sprite_set_precise", 2);
           AddFunction("sprite_set_alpha_from_sprite", 2);
           AddFunction("sprite_create_from_screen", 8);
           AddFunction("sprite_add_from_screen", 7);
           AddFunction("sprite_create_from_surface", 9);
           AddFunction("sprite_add_from_surface", 8);
           AddFunction("sprite_add", 6);
           AddFunction("sprite_replace", 7);
           AddFunction("sprite_add_alpha", 6);
           AddFunction("sprite_replace_alpha", 7);
           AddFunction("sprite_delete", 1);
           AddFunction("sprite_duplicate", 1);
           AddFunction("sprite_assign", 2);
           AddFunction("sprite_merge", 2);
           AddFunction("sprite_save", 3);
           AddFunction("sprite_save_strip", 2);
           AddFunction("sprite_set_cache_size", 2);
           AddFunction("sprite_set_cache_size_ext", 3);
           AddFunction("font_set_cache_size", 2);
           AddFunction("background_name", 1);
           AddFunction("background_exists", 1);
           AddFunction("background_get_name", 1);
           AddFunction("background_get_width", 1);
           AddFunction("background_get_height", 1);
           AddFunction("background_get_transparent", 1);
           AddFunction("background_get_smooth", 1);
           AddFunction("background_get_preload", 1);
           AddFunction("background_set_alpha_from_background", 2);
           AddFunction("background_create_from_screen", 6);
           AddFunction("background_create_from_surface", 7);
           AddFunction("background_create_color", 4);
           AddFunction("background_create_colour", 4);
           AddFunction("background_create_gradient", 5);
           AddFunction("background_add", 3);
           AddFunction("background_replace", 4);
           AddFunction("background_add_alpha", 2);
           AddFunction("background_replace_alpha", 3);
           AddFunction("background_delete", 1);
           AddFunction("background_duplicate", 1);
           AddFunction("background_assign", 2);
           AddFunction("background_save", 2);
           AddFunction("sound_name", 1);
           AddFunction("sound_exists", 1);
           AddFunction("sound_get_name", 1);
           AddFunction("sound_get_kind", 1);
           AddFunction("sound_get_preload", 1);
           AddFunction("sound_discard", 1);
           AddFunction("sound_restore", 1);
           AddFunction("audio_listener_position", 3);
           AddFunction("audio_listener_velocity", 3);
           AddFunction("audio_listener_orientation", 6);
           AddFunction("audio_emitter_position", 4);
           AddFunction("audio_emitter_create", 0);
           AddFunction("audio_emitter_exists", 1);
           AddFunction("audio_emitter_free", 1);
           AddFunction("audio_emitter_pitch", 2);
           AddFunction("audio_emitter_velocity", 4);
           AddFunction("audio_emitter_falloff", 4);
           AddFunction("audio_emitter_gain", 2);
           AddFunction("audio_play_sound", 3, GM_Type.Sound);
           AddFunction("audio_play_sound_on", 4, GM_Type.Sound);
           AddFunction("audio_play_sound_at", 9, GM_Type.Sound);
           AddFunction("audio_stop_sound", 1);
           AddFunction("audio_resume_sound", 1);
           AddFunction("audio_pause_sound", 1);
           AddFunction("audio_channel_num", 1);
           AddFunction("audio_sound_length", 1);
           AddFunction("audio_get_type", 1);
           AddFunction("audio_falloff_set_model", 1);
           AddFunction("audio_sound_get_listener_mask", 1);
           AddFunction("audio_emitter_get_listener_mask", 1);
           AddFunction("audio_get_listener_mask", 0);
           AddFunction("audio_sound_set_listener_mask", 2);
           AddFunction("audio_emitter_set_listener_mask", 2);
           AddFunction("audio_set_listener_mask", 1);
           AddFunction("audio_get_listener_count", 0);
           AddFunction("audio_get_listener_info", 1);
           AddFunction("audio_play_music", 2);
           AddFunction("audio_stop_music", 0);
           AddFunction("audio_master_gain", 1);
           AddFunction("audio_music_gain", 2);
           AddFunction("audio_sound_gain", 3);
           AddFunction("audio_sound_pitch", 2);
           AddFunction("audio_stop_all", 0);
           AddFunction("audio_resume_all", 0);
           AddFunction("audio_pause_all", 0);
           AddFunction("audio_is_playing", 1);
           AddFunction("audio_is_paused", 1);
           AddFunction("audio_pause_music", 0);
           AddFunction("audio_resume_music", 0);
           AddFunction("audio_music_is_playing", 0);
           AddFunction("audio_exists", 1);
           AddFunction("audio_system", 0);
           AddFunction("audio_emitter_get_gain", 1);
           AddFunction("audio_emitter_get_pitch", 1);
           AddFunction("audio_emitter_get_x", 1);
           AddFunction("audio_emitter_get_y", 1);
           AddFunction("audio_emitter_get_z", 1);
           AddFunction("audio_emitter_get_vx", 1);
           AddFunction("audio_emitter_get_vy", 1);
           AddFunction("audio_emitter_get_vz", 1);
           AddFunction("audio_listener_set_position", 4);
           AddFunction("audio_listener_set_velocity", 4);
           AddFunction("audio_listener_set_orientation", 7);
           AddFunction("audio_listener_get_data", 1);
           AddFunction("audio_set_master_gain", 2);
           AddFunction("audio_get_master_gain", 1);
           AddFunction("audio_sound_get_gain", 1);
           AddFunction("audio_sound_get_pitch", 1);
           AddFunction("audio_get_name", 1);
           AddFunction("audio_sound_set_track_position", 2);
           AddFunction("audio_sound_get_track_position", 1, GM_Type.Float);
           AddFunction("audio_group_load", 1);
           AddFunction("audio_group_unload", 1);
           AddFunction("audio_group_is_loaded", 1);
           AddFunction("audio_group_load_progress", 1);
           AddFunction("audio_group_name", 1);
           AddFunction("audio_group_stop_all", 1);
           AddFunction("audio_group_set_gain", 3);
           AddFunction("audio_create_buffer_sound", 6);
           AddFunction("audio_free_buffer_sound", 1);
           AddFunction("audio_create_play_queue", 3);
           AddFunction("audio_free_play_queue", 1);
           AddFunction("audio_queue_sound", 4);
           AddFunction("audio_create_stream", 1);
           AddFunction("audio_destroy_stream", 1);
           AddFunction("audio_start_recording", 1);
           AddFunction("audio_stop_recording", 1);
           AddFunction("audio_get_recorder_count", 0);
           AddFunction("audio_get_recorder_info", 1);
           AddFunction("audio_create_sync_group", 1);
           AddFunction("audio_destroy_sync_group", 1);
           AddFunction("audio_play_in_sync_group", 2);
           AddFunction("audio_start_sync_group", 1);
           AddFunction("audio_stop_sync_group", 1);
           AddFunction("audio_pause_sync_group", 1);
           AddFunction("audio_resume_sync_group", 1);
           AddFunction("audio_sync_group_get_track_pos", 1);
           AddFunction("audio_sync_group_debug", 1);
           AddFunction("audio_sync_group_is_playing", 1);
           AddFunction("audio_debug", 1);
           AddFunction("sound_add", 3);
           AddFunction("sound_replace", 4);
           AddFunction("sound_delete", 1);
           AddFunction("font_name", 1);
           AddFunction("font_exists", 1);
           AddFunction("font_get_name", 1);
           AddFunction("font_get_fontname", 1);
           AddFunction("font_get_size", 1);
           AddFunction("font_get_bold", 1);
           AddFunction("font_get_italic", 1);
           AddFunction("font_get_first", 1);
           AddFunction("font_get_last", 1);
           AddFunction("font_add", 6);
           AddFunction("font_replace", 7);
           AddFunction("font_add_sprite", 4);
           AddFunction("font_add_sprite_ext", 4);
           AddFunction("font_replace_sprite", 5);
           AddFunction("font_replace_sprite_ext", 5);
           AddFunction("font_delete", 1);
           AddFunction("script_name", 1);
           AddFunction("script_exists", 1);
           AddFunction("script_get_name", 1);
           AddFunction("script_get_text", 1);
           AddFunction("script_execute", -1);
           AddFunction("path_name", 1);
           AddFunction("path_exists", 1);
           AddFunction("path_get_name", 1);
           AddFunction("path_get_length", 1);
           AddFunction("path_get_kind", 1);
           AddFunction("path_get_closed", 1);
           AddFunction("path_get_precision", 1);
           AddFunction("path_get_number", 1);
           AddFunction("path_get_point_x", 2);
           AddFunction("path_get_point_y", 2);
           AddFunction("path_get_point_speed", 2);
           AddFunction("path_get_x", 2);
           AddFunction("path_get_y", 2);
           AddFunction("path_get_speed", 2);
           AddFunction("path_set_kind", 2);
           AddFunction("path_set_closed", 2);
           AddFunction("path_set_precision", 2);
           AddFunction("path_add", 0);
           AddFunction("path_duplicate", 1);
           AddFunction("path_assign", 2);
           AddFunction("path_append", 2);
           AddFunction("path_delete", 1);
           AddFunction("path_add_point", 4);
           AddFunction("path_insert_point", 5);
           AddFunction("path_change_point", 5);
           AddFunction("path_delete_point", 2);
           AddFunction("path_clear_points", 1);
           AddFunction("path_reverse", 1);
           AddFunction("path_mirror", 1);
           AddFunction("path_flip", 1);
           AddFunction("path_rotate", 2);
           AddFunction("path_rescale", 3);
           AddFunction("path_shift", 3);
           AddFunction("timeline_name", 1);
           AddFunction("timeline_exists", 1);
           AddFunction("timeline_get_name", 1);
           AddFunction("timeline_add", 0);
           AddFunction("timeline_delete", 1);
           AddFunction("timeline_clear", 1);
           AddFunction("timeline_moment_clear", 2);
           AddFunction("timeline_moment_add", 3);
           AddFunction("timeline_moment_add_script", 3);
           AddFunction("timeline_size", 1);
           AddFunction("timeline_max_moment", 1);
           AddFunction("object_name", 1);
           AddFunction("object_exists", 1);
           AddFunction("object_get_name", 1);
           AddFunction("object_get_sprite", 1);
           AddFunction("object_get_solid", 1);
           AddFunction("object_get_visible", 1);
           AddFunction("object_get_depth", 1);
           AddFunction("object_get_persistent", 1);
           AddFunction("object_get_mask", 1);
           AddFunction("object_get_parent", 1);
           AddFunction("object_get_physics", 1);
           AddFunction("object_is_ancestor", 2);
           AddFunction("object_set_sprite", 2);
           AddFunction("object_set_solid", 2);
           AddFunction("object_set_visible", 2);
           AddFunction("object_set_depth", 2);
           AddFunction("object_set_persistent", 2);
           AddFunction("object_set_mask", 2);
           AddFunction("object_set_parent", 2);
           AddFunction("object_add", 0);
           AddFunction("object_delete", 1);
           AddFunction("object_event_clear", 3);
           AddFunction("object_event_add", 4);
           AddFunction("room_name", 1);
           AddFunction("room_exists", 1);
           AddFunction("room_get_name", 1);
           AddFunction("room_set_width", 2);
           AddFunction("room_set_height", 2);
           AddFunction("room_set_caption", 2);
           AddFunction("room_set_persistent", 2);
           AddFunction("room_set_code", 2);
           AddFunction("room_set_background_color", 3);
           AddFunction("room_set_background_colour", 3);
           AddFunction("room_set_background", 12);
           AddFunction("room_set_view", 16);
           AddFunction("room_set_view_enabled", 2);
           AddFunction("room_add", 0);
           AddFunction("room_duplicate", 1);
           AddFunction("room_assign", 2);
           AddFunction("room_instance_add", 4);
           AddFunction("room_instance_clear", 1);
           AddFunction("room_tile_add", 9);
           AddFunction("room_tile_add_ext", 12);
           AddFunction("room_tile_clear", 1);
           AddFunction("asset_get_index", 1);
           AddFunction("asset_get_type", 1);
           AddFunction("sound_play", 1);
           AddFunction("sound_loop", 1);
           AddFunction("sound_stop", 1);
           AddFunction("sound_stop_all", 0);
           AddFunction("sound_isplaying", 1);
           AddFunction("sound_volume", 2);
           AddFunction("sound_fade", 3);
           AddFunction("sound_pan", 2);
           AddFunction("sound_background_tempo", 1);
           AddFunction("sound_global_volume", 1);
           AddFunction("sound_set_search_directory", 1);
           AddFunction("sound_effect_set", 2);
           AddFunction("sound_effect_chorus", 8);
           AddFunction("sound_effect_compressor", 7);
           AddFunction("sound_effect_echo", 6);
           AddFunction("sound_effect_flanger", 8);
           AddFunction("sound_effect_gargle", 3);
           AddFunction("sound_effect_equalizer", 4);
           AddFunction("sound_effect_reverb", 5);
           AddFunction("sound_3d_set_sound_position", 4);
           AddFunction("sound_3d_set_sound_velocity", 4);
           AddFunction("sound_3d_set_sound_distance", 3);
           AddFunction("sound_3d_set_sound_cone", 7);
           AddFunction("cd_init", 0);
           AddFunction("cd_present", 0);
           AddFunction("cd_number", 0);
           AddFunction("cd_playing", 0);
           AddFunction("cd_paused", 0);
           AddFunction("cd_track", 0);
           AddFunction("cd_length", 0);
           AddFunction("cd_track_length", 1);
           AddFunction("cd_position", 0);
           AddFunction("cd_track_position", 0);
           AddFunction("cd_play", 2);
           AddFunction("cd_stop", 0);
           AddFunction("cd_pause", 0);
           AddFunction("cd_resume", 0);
           AddFunction("cd_set_position", 1);
           AddFunction("cd_set_track_position", 1);
           AddFunction("cd_open_door", 0);
           AddFunction("cd_close_door", 0);
           AddFunction("MCI_command", 1);
           AddFunction("YoYo_AddVirtualKey", 5);
           AddFunction("YoYo_DeleteVirtualKey", 1);
           AddFunction("YoYo_ShowVirtualKey", 1);
           AddFunction("YoYo_HideVirtualKey", 1);
           AddFunction("virtual_key_add", 5);
           AddFunction("virtual_key_delete", 1);
           AddFunction("virtual_key_show", 1);
           AddFunction("virtual_key_hide", 1);
           AddFunction("YoYo_LoginAchievements", 0);
           AddFunction("YoYo_LogoutAchievements", 0);
           AddFunction("YoYo_PostAchievement", 2);
           AddFunction("YoYo_PostScore", 2);
           AddFunction("YoYo_AchievementsAvailable", 0);
           AddFunction("achievement_login", 0);
           AddFunction("achievement_logout", 0);
           AddFunction("achievement_post", 2);
           AddFunction("achievement_increment", 2);
           AddFunction("achievement_post_score", 2);
           AddFunction("achievement_available", 0);
           AddFunction("achievement_show_achievements", 0);
           AddFunction("achievement_show_leaderboards", 0);
           AddFunction("achievement_load_friends", 0);
           AddFunction("achievement_load_leaderboard", 4);
           AddFunction("achievement_send_challenge", 5);
           AddFunction("achievement_load_progress", 0);
           AddFunction("achievement_reset", 0);
           AddFunction("achievement_login_status", 0);
           AddFunction("achievement_get_pic", 1);
           AddFunction("achievement_get_info", 1);
           AddFunction("achievement_get_challenges", 0);
           AddFunction("achievement_show_challenge_notifications", 3);
           AddFunction("achievement_show", 2);
           AddFunction("achievement_event", 1);
           AddFunction("cloud_file_save", 2);
           AddFunction("cloud_string_save", 2);
           AddFunction("cloud_synchronise", 0);
           AddFunction("YoYo_GetDomain", 0);
           AddFunction("YoYo_OpenURL", 1);
           AddFunction("YoYo_OpenURL_ext", 2);
           AddFunction("YoYo_OpenURL_full", 3);
           AddFunction("url_get_domain", 0);
           AddFunction("url_open", 1);
           AddFunction("url_open_ext", 2);
           AddFunction("url_open_full", 3);
           AddFunction("YoYo_EnableAds", 5);
           AddFunction("YoYo_DisableAds", 0);
           AddFunction("ads_setup", 2);
           AddFunction("ads_engagement_launch", 0);
           AddFunction("ads_engagement_available", 0);
           AddFunction("ads_engagement_active", 0);
           AddFunction("ads_get_display_height", 1);
           AddFunction("ads_get_display_width", 1);
           AddFunction("ads_move", 3);
           AddFunction("ads_interstitial_available", 0);
           AddFunction("ads_interstitial_display", 0);
           AddFunction("YoYo_LeaveRating", 4);
           AddFunction("ads_enable", 3);
           AddFunction("ads_disable", 1);
           AddFunction("ads_event", 1);
           AddFunction("ads_event_preload", 1);
           AddFunction("shop_leave_rating", 4);
           AddFunction("pocketchange_display_reward", 0);
           AddFunction("pocketchange_display_shop", 0);
           AddFunction("analytics_event", 1);
           AddFunction("analytics_event_ext", -1);
           AddFunction("ads_set_reward_callback", 1);
           AddFunction("playhaven_add_notification_badge", 5);
           AddFunction("playhaven_hide_notification_badge", 0);
           AddFunction("playhaven_update_notification_badge", 0);
           AddFunction("playhaven_position_notification_badge", 4);
           AddFunction("YoYo_EnableAlphaBlend", 1);
           AddFunction("draw_enable_alphablend", 1);
           AddFunction("draw_texture_flush", 0);
           AddFunction("draw_flush", 0);
           AddFunction("YoYo_GetTimer", 0);
           AddFunction("YoYo_GetPlatform", 0);
           AddFunction("YoYo_GetDevice", 0);
           AddFunction("YoYo_GetConfig", 0);
           AddFunction("YoYo_GetTiltX", 0);
           AddFunction("YoYo_GetTiltY", 0);
           AddFunction("YoYo_GetTiltZ", 0);
           AddFunction("YoYo_IsKeypadOpen", 0);
           AddFunction("get_timer", 0);
           AddFunction("os_get_config", 0);
           AddFunction("os_get_info", 0);
           AddFunction("os_get_language", 0);
           AddFunction("os_get_region", 0);
           AddFunction("display_get_dpi_x", 0);
           AddFunction("display_get_dpi_y", 0);
           AddFunction("display_set_gui_size", 0);
           AddFunction("display_set_gui_maximise", 0);
           AddFunction("device_get_tilt_x", 0);
           AddFunction("device_get_tilt_y", 0);
           AddFunction("device_get_tilt_z", 0);
           AddFunction("device_is_keypad_open", 0);
           AddFunction("code_is_compiled", 0);
           AddFunction("YoYo_SelectPicture", 0);
           AddFunction("YoYo_GetPictureSprite", 0);
           AddFunction("device_ios_get_imagename", 0);
           AddFunction("device_ios_get_image", 0);
           AddFunction("YoYo_OF_StartDashboard", 0);
           AddFunction("YoYo_OF_AddAchievement", 2);
           AddFunction("YoYo_OF_AddLeaderboard", 3);
           AddFunction("YoYo_OF_SendChallenge", 3);
           AddFunction("YoYo_OF_SendInvite", 1);
           AddFunction("YoYo_OF_SendSocial", 3);
           AddFunction("YoYo_OF_SetURL", 1);
           AddFunction("YoYo_OF_AcceptChallenge", 0);
           AddFunction("YoYo_OF_IsOnline", 0);
           AddFunction("YoYo_OF_SendChallengeResult", 2);
           AddFunction("openfeint_start", 0);
           AddFunction("achievement_map_achievement", 2);
           AddFunction("achievement_map_leaderboard", 3);
           AddFunction("openfeint_send_challenge", 3);
           AddFunction("openfeint_send_invite", 1);
           AddFunction("openfeint_send_social", 3);
           AddFunction("openfeint_set_url", 1);
           AddFunction("openfeint_accept_challenge", 0);
           AddFunction("achievement_is_online", 0);
           AddFunction("openfeint_send_result", 2);
           AddFunction("YoYo_MouseCheckButton", 2);
           AddFunction("YoYo_MouseCheckButtonPressed", 2);
           AddFunction("YoYo_MouseCheckButtonReleased", 2);
           AddFunction("YoYo_MouseX", 1);
           AddFunction("YoYo_MouseY", 1);
           AddFunction("YoYo_MouseXRaw", 1);
           AddFunction("YoYo_MouseYRaw", 1);
           AddFunction("device_mouse_check_button", 2);
           AddFunction("device_mouse_check_button_pressed", 2);
           AddFunction("device_mouse_check_button_released", 2);
           AddFunction("device_mouse_x", 1);
           AddFunction("device_mouse_y", 1);
           AddFunction("device_mouse_raw_x", 1);
           AddFunction("device_mouse_raw_y", 1);
           AddFunction("iap_activate", 1);
           AddFunction("iap_status", 0);
           AddFunction("iap_acquire", 2);
           AddFunction("iap_consume", 1);
           AddFunction("iap_is_purchased", 1);
           AddFunction("iap_enumerate_products", 1);
           AddFunction("iap_restore_all", 0);
           AddFunction("iap_product_details", 2);
           AddFunction("iap_purchase_details", 2);
           AddFunction("iap_store_status", 0);
           AddFunction("iap_product_status", 1);
           AddFunction("iap_is_downloaded", 1);
           AddFunction("iap_event_queue", 0);
           AddFunction("iap_files_purchased", 0);
           AddFunction("iap_product_files", 2);
           AddFunction("facebook_init", 0);
           AddFunction("facebook_login", 2);
           AddFunction("facebook_status", 0);
           AddFunction("facebook_graph_request", 4);
           AddFunction("facebook_dialog", 3);
           AddFunction("facebook_logout", 0);
           AddFunction("facebook_launch_offerwall", 1);
           AddFunction("facebook_post_message", 7);
           AddFunction("facebook_send_invite", 5);
           AddFunction("facebook_user_id", 0);
           AddFunction("facebook_accesstoken", 0);
           AddFunction("facebook_check_permission", 1);
           AddFunction("facebook_request_read_permissions", 1);
           AddFunction("facebook_request_publish_permissions", 1);
           AddFunction("gamepad_is_supported", 0);
           AddFunction("gamepad_get_device_count", 0);
           AddFunction("gamepad_is_connected", 1);
           AddFunction("gamepad_get_description", 1);
           AddFunction("gamepad_get_button_threshold", 1);
           AddFunction("gamepad_set_button_threshold", 2);
           AddFunction("gamepad_get_axis_deadzone", 1);
           AddFunction("gamepad_set_axis_deadzone", 2);
           AddFunction("gamepad_button_count", 1);
           AddFunction("gamepad_button_check", 2);
           AddFunction("gamepad_button_check_pressed", 2);
           AddFunction("gamepad_button_check_released", 2);
           AddFunction("gamepad_button_value", 2);
           AddFunction("gamepad_axis_count", 1);
           AddFunction("gamepad_axis_value", 2);
           AddFunction("gamepad_set_vibration", 3);
           AddFunction("gamepad_set_colour", 2);
           AddFunction("gamepad_set_color", 2);
           AddFunction("YoYo_OSPauseEvent", 0);
           AddFunction("os_is_paused", 0);
           AddFunction("window_has_focus", 0);
           AddFunction("base64_encode", 1);
           AddFunction("base64_decode", 1);
           AddFunction("md5_string_unicode", 1);
           AddFunction("md5_string_utf8", 1);
           AddFunction("md5_file", 1);
           AddFunction("os_is_network_connected", 0);
           AddFunction("sha1_string_unicode", 1);
           AddFunction("sha1_string_utf8", 1);
           AddFunction("sha1_file", 1);
           AddFunction("os_powersave_enable", 1);
           AddFunction("os_lock_orientation", 1);
           AddFunction("physics_world_create", 1);
           AddFunction("physics_world_gravity", 2);
           AddFunction("physics_world_update_speed", 1);
           AddFunction("physics_world_update_iterations", 1);
           AddFunction("physics_world_draw_debug", 1);
           AddFunction("physics_pause_enable", 1);
           AddFunction("physics_fixture_create", 0);
           AddFunction("physics_fixture_set_kinematic", 1);
           AddFunction("physics_fixture_set_awake", 2);
           AddFunction("physics_fixture_set_density", 2);
           AddFunction("physics_fixture_set_restitution", 2);
           AddFunction("physics_fixture_set_friction", 2);
           AddFunction("physics_fixture_set_collision_group", 2);
           AddFunction("physics_fixture_set_sensor", 2);
           AddFunction("physics_fixture_set_linear_damping", 2);
           AddFunction("physics_fixture_set_angular_damping", 2);
           AddFunction("physics_fixture_set_circle_shape", 2);
           AddFunction("physics_fixture_set_box_shape", 3);
           AddFunction("physics_fixture_set_edge_shape", 5);
           AddFunction("physics_fixture_set_polygon_shape", 1);
           AddFunction("physics_fixture_set_chain_shape", 2);
           AddFunction("physics_fixture_add_point", 3);
           AddFunction("physics_fixture_bind", 2);
           AddFunction("physics_fixture_bind_ext", 4);
           AddFunction("physics_fixture_delete", 1);
           AddFunction("physics_apply_force", 4);
           AddFunction("physics_apply_impulse", 4);
           AddFunction("physics_apply_angular_impulse", 1);
           AddFunction("physics_apply_local_force", 4);
           AddFunction("physics_apply_local_impulse", 4);
           AddFunction("physics_apply_torque", 1);
           AddFunction("physics_mass_properties", 4);
           AddFunction("physics_draw_debug", 0);
           AddFunction("physics_test_overlap", 4);
           AddFunction("physics_remove_fixture", 2);
           AddFunction("physics_get_friction", 1);
           AddFunction("physics_get_density", 1);
           AddFunction("physics_get_restitution", 1);
           AddFunction("physics_set_friction", 2);
           AddFunction("physics_set_density", 2);
           AddFunction("physics_set_restitution", 2);
           AddFunction("physics_joint_enable_motor", 2);
           AddFunction("physics_joint_get_value", 2);
           AddFunction("physics_joint_set_value", 3);
           AddFunction("physics_joint_distance_create", 7);
           AddFunction("physics_joint_rope_create", 8);
           AddFunction("physics_joint_revolute_create", 11);
           AddFunction("physics_joint_prismatic_create", 13);
           AddFunction("physics_joint_pulley_create", 12);
           AddFunction("physics_joint_wheel_create", 12);
           AddFunction("physics_joint_weld_create", 8);
           AddFunction("physics_joint_friction_create", 7);
           AddFunction("physics_joint_gear_create", 5);
           AddFunction("physics_joint_delete", 1);
           AddFunction("physics_particle_create", 8);
           AddFunction("physics_particle_delete", 1);
           AddFunction("physics_particle_delete_region_circle", 3);
           AddFunction("physics_particle_delete_region_box", 4);
           AddFunction("physics_particle_delete_region_poly", 1);
           AddFunction("physics_particle_set_flags", 2);
           AddFunction("physics_particle_set_category_flags", 2);
           AddFunction("physics_particle_draw", 4);
           AddFunction("physics_particle_draw_ext", 9);
           AddFunction("physics_particle_count", 0);
           AddFunction("physics_particle_get_data", 2);
           AddFunction("physics_particle_get_data_particle", 3);
           AddFunction("physics_particle_group_begin", 12);
           AddFunction("physics_particle_group_circle", 1);
           AddFunction("physics_particle_group_box", 2);
           AddFunction("physics_particle_group_polygon", 0);
           AddFunction("physics_particle_group_add_point", 2);
           AddFunction("physics_particle_group_end", 0);
           AddFunction("physics_particle_group_join", 2);
           AddFunction("physics_particle_group_delete", 1);
           AddFunction("physics_particle_group_count", 1);
           AddFunction("physics_particle_group_get_data", 3);
           AddFunction("physics_particle_group_get_mass", 1);
           AddFunction("physics_particle_group_get_inertia", 1);
           AddFunction("physics_particle_group_get_centre_x", 1);
           AddFunction("physics_particle_group_get_centre_y", 1);
           AddFunction("physics_particle_group_get_vel_x", 1);
           AddFunction("physics_particle_group_get_vel_y", 1);
           AddFunction("physics_particle_group_get_ang_vel", 1);
           AddFunction("physics_particle_group_get_x", 1);
           AddFunction("physics_particle_group_get_y", 1);
           AddFunction("physics_particle_group_get_angle", 1);
           AddFunction("physics_particle_set_group_flags", 2);
           AddFunction("physics_particle_get_group_flags", 1);
           AddFunction("physics_particle_get_max_count", 0);
           AddFunction("physics_particle_get_radius", 0);
           AddFunction("physics_particle_get_density", 0);
           AddFunction("physics_particle_get_damping", 0);
           AddFunction("physics_particle_get_gravity_scale", 0);
           AddFunction("physics_particle_set_max_count", 1);
           AddFunction("physics_particle_set_radius", 1);
           AddFunction("physics_particle_set_density", 1);
           AddFunction("physics_particle_set_damping", 1);
           AddFunction("physics_particle_set_gravity_scale", 1);
           AddFunction("win8_livetile_tile_notification", 4);
           AddFunction("win8_livetile_badge_notification", 1);
           AddFunction("win8_livetile_tile_clear", 0);
           AddFunction("win8_livetile_badge_clear", 0);
           AddFunction("win8_livetile_queue_enable", 1);
           AddFunction("win8_secondarytile_pin", 8);
           AddFunction("win8_secondarytile_badge_notification", 2);
           AddFunction("win8_secondarytile_delete", 1);
           AddFunction("win8_livetile_notification_begin", 1);
           AddFunction("win8_livetile_notification_secondary_begin", 2);
           AddFunction("win8_livetile_notification_expiry", 1);
           AddFunction("win8_livetile_notification_tag", 1);
           AddFunction("win8_livetile_notification_text_add", 1);
           AddFunction("win8_livetile_notification_image_add", 1);
           AddFunction("win8_livetile_notification_end", 0);
           AddFunction("win8_appbar_enable", 1);
           AddFunction("win8_appbar_add_element", 6);
           AddFunction("win8_appbar_remove_element", 1);
           AddFunction("win8_settingscharm_add_entry", 2);
           AddFunction("win8_settingscharm_add_html_entry", 3);
           AddFunction("win8_settingscharm_add_xaml_entry", 5);
           AddFunction("win8_settingscharm_set_xaml_property", 4);
           AddFunction("win8_settingscharm_get_xaml_property", 3);
           AddFunction("win8_settingscharm_remove_entry", 1);
           AddFunction("win8_share_image", 4);
           AddFunction("win8_share_screenshot", 3);
           AddFunction("win8_share_file", 4);
           AddFunction("win8_share_url", 4);
           AddFunction("win8_share_text", 4);
           AddFunction("win8_search_enable", 1);
           AddFunction("win8_search_disable", 0);
           AddFunction("win8_search_add_suggestions", 1);
           AddFunction("win8_device_touchscreen_available", 0);
           AddFunction("win8_license_initialize_sandbox", 1);
           AddFunction("win8_license_trial_version", 0);
           AddFunction("winphone_license_trial_version", 0);
           AddFunction("winphone_tile_title", 1);
           AddFunction("winphone_tile_count", 1);
           AddFunction("winphone_tile_back_title", 1);
           AddFunction("winphone_tile_back_content", 1);
           AddFunction("winphone_tile_back_content_wide", 1);
           AddFunction("winphone_tile_front_image", 1);
           AddFunction("winphone_tile_front_image_small", 1);
           AddFunction("winphone_tile_front_image_wide", 1);
           AddFunction("winphone_tile_back_image", 1);
           AddFunction("winphone_tile_back_image_wide", 1);
           AddFunction("winphone_tile_background_color", 1);
           AddFunction("winphone_tile_background_colour", 1);
           AddFunction("winphone_tile_icon_image", 1);
           AddFunction("winphone_tile_small_icon_image", 1);
           AddFunction("winphone_tile_wide_content", 2);
           AddFunction("winphone_tile_cycle_images", -1);
           AddFunction("winphone_tile_small_background_image", 1);
           AddFunction("immersion_play_effect", 1);
           AddFunction("immersion_stop", 0);
           AddFunction("gml_release_mode", 1);
           AddFunction("gml_pragma", 1);
           AddFunction("buffer_create", 3);
           AddFunction("buffer_delete", 1);
           AddFunction("buffer_write", 3);
           AddFunction("buffer_read", 2);
           AddFunction("buffer_poke", 4);
           AddFunction("buffer_peek", 3);
           AddFunction("buffer_seek", 3);
           AddFunction("buffer_save", 2);
           AddFunction("buffer_save_ext", 4);
           AddFunction("buffer_load", 1);
           AddFunction("buffer_load_ext", 3);
           AddFunction("buffer_load_partial", 5);
           AddFunction("buffer_copy", 5);
           AddFunction("buffer_fill", 5);
           AddFunction("buffer_get_size", 1);
           AddFunction("buffer_tell", 1);
           AddFunction("buffer_resize", 2);
           AddFunction("buffer_md5", 3);
           AddFunction("buffer_sha1", 3);
           AddFunction("buffer_base64_encode", 3);
           AddFunction("buffer_base64_decode", 1);
           AddFunction("buffer_base64_decode_ext", 3);
           AddFunction("buffer_sizeof", 1);
           AddFunction("buffer_get_address", 1);
           AddFunction("buffer_save_async", 4);
           AddFunction("buffer_load_async", 4);
           AddFunction("buffer_async_group_begin", 1);
           AddFunction("buffer_async_group_end", 0);
           AddFunction("buffer_async_group_option", 2);
           AddFunction("buffer_get_surface", 5);
           AddFunction("buffer_set_surface", 5);
           AddFunction("buffer_set_network_safe", 2);
           AddFunction("buffer_create_from_vertex_buffer", 3);
           AddFunction("buffer_create_from_vertex_buffer_ext", 5);
           AddFunction("buffer_copy_from_vertex_buffer", 5);
           AddFunction("network_create_socket", 1);
           AddFunction("network_create_socket_ext", 2);
           AddFunction("network_create_server", 3);
           AddFunction("network_create_server_raw", 3);
           AddFunction("network_connect", 3);
           AddFunction("network_connect_raw", 3);
           AddFunction("network_send_packet", 3);
           AddFunction("network_send_raw", 3);
           AddFunction("network_send_broadcast", 4);
           AddFunction("network_send_udp", 5);
           AddFunction("network_send_udp_raw", 5);
           AddFunction("network_resolve", 1);
           AddFunction("network_receive_packet", 3);
           AddFunction("network_destroy", 1);
           AddFunction("network_set_timeout", 3);
           AddFunction("network_get_address", 1);
           AddFunction("network_set_config", 2);
           AddFunction("steam_activate_overlay", 1);
           AddFunction("steam_is_overlay_enabled", 0);
           AddFunction("steam_is_overlay_activated", 0);
           AddFunction("steam_get_persona_name", 0);
           AddFunction("steam_initialised", 0);
           AddFunction("steam_is_cloud_enabled_for_app", 0);
           AddFunction("steam_is_cloud_enabled_for_account", 0);
           AddFunction("steam_file_persisted", 1);
           AddFunction("steam_get_quota_total", 0);
           AddFunction("steam_get_quota_free", 0);
           AddFunction("steam_file_write", 3);
           AddFunction("steam_file_write_file", 2);
           AddFunction("steam_file_read", 1);
           AddFunction("steam_file_delete", 1);
           AddFunction("steam_file_exists", 1, GM_Type.Bool);
           AddFunction("steam_file_size", 1);
           AddFunction("steam_file_share", 1);
           AddFunction("steam_publish_workshop_file", 4);
           AddFunction("steam_is_screenshot_requested", 0);
           AddFunction("steam_send_screenshot", 3);
           AddFunction("steam_is_user_logged_on", 0);
           AddFunction("steam_get_user_steam_id", 0);
           AddFunction("steam_user_owns_dlc", 1);
           AddFunction("steam_user_installed_dlc", 1);
           AddFunction("steam_set_achievement", 1);
           AddFunction("steam_get_achievement", 1);
           AddFunction("steam_clear_achievement", 1);
           AddFunction("steam_set_stat_int", 2);
           AddFunction("steam_set_stat_float", 2);
           AddFunction("steam_set_stat_avg_rate", 3);
           AddFunction("steam_get_stat_int", 1);
           AddFunction("steam_get_stat_float", 1);
           AddFunction("steam_get_stat_avg_rate", 1);
           AddFunction("steam_reset_all_stats", 0);
           AddFunction("steam_reset_all_stats_achievements", 0);
           AddFunction("steam_stats_ready", 0);
           AddFunction("steam_create_leaderboard", 3);
           AddFunction("steam_upload_score", 2);
           AddFunction("steam_upload_score_buffer", 3);
           AddFunction("steam_download_scores_around_user", 3);
           AddFunction("steam_download_scores", 3);
           AddFunction("steam_download_friends_scores", 1);
           AddFunction("steam_current_game_language", 0);
           AddFunction("steam_available_languages", 0);
           AddFunction("steam_activate_overlay_browser", 1);
           AddFunction("steam_activate_overlay_user", 2);
           AddFunction("steam_activate_overlay_store", 1);
           AddFunction("steam_get_user_persona_name", 1);
           AddFunction("steam_get_app_id", 0);
           AddFunction("steam_get_user_account_id", 0);
           AddFunction("steam_ugc_download", 2);
           AddFunction("steam_ugc_create_item", 2);
           AddFunction("steam_ugc_start_item_update", 2);
           AddFunction("steam_ugc_set_item_title", 2);
           AddFunction("steam_ugc_set_item_description", 2);
           AddFunction("steam_ugc_set_item_visibility", 2);
           AddFunction("steam_ugc_set_item_tags", 2);
           AddFunction("steam_ugc_set_item_content", 2);
           AddFunction("steam_ugc_set_item_preview", 2);
           AddFunction("steam_ugc_submit_item_update", 2);
           AddFunction("steam_ugc_get_item_update_progress", 2);
           AddFunction("steam_ugc_subscribe_item", 1);
           AddFunction("steam_ugc_unsubscribe_item", 1);
           AddFunction("steam_ugc_num_subscribed_items", 0);
           AddFunction("steam_ugc_get_subscribed_items", 1);
           AddFunction("steam_ugc_get_item_install_info", 2);
           AddFunction("steam_ugc_get_item_update_info", 2);
           AddFunction("steam_ugc_request_item_details", 2);
           AddFunction("steam_ugc_create_query_user", 4);
           AddFunction("steam_ugc_create_query_user_ex", 7);
           AddFunction("steam_ugc_create_query_all", 3);
           AddFunction("steam_ugc_create_query_all_ex", 5);
           AddFunction("steam_ugc_query_set_cloud_filename_filter", 2);
           AddFunction("steam_ugc_query_set_match_any_tag", 2);
           AddFunction("steam_ugc_query_set_search_text", 2);
           AddFunction("steam_ugc_query_set_ranked_by_trend_days", 2);
           AddFunction("steam_ugc_query_add_required_tag", 2);
           AddFunction("steam_ugc_query_add_excluded_tag", 2);
           AddFunction("steam_ugc_query_set_return_long_description", 2);
           AddFunction("steam_ugc_query_set_return_total_only", 2);
           AddFunction("steam_ugc_query_set_allow_cached_response", 2);
           AddFunction("steam_ugc_send_query", 1);
           AddFunction("shader_set", 1);
           AddFunction("shader_reset", 0);
           AddFunction("shader_is_compiled", 1);
           AddFunction("shader_get_sampler_index", 2);
           AddFunction("shader_get_uniform", 2);
           AddFunction("shader_set_uniform_i", -1);
           AddFunction("shader_set_uniform_i_array", 2);
           AddFunction("shader_set_uniform_i1", 1);
           AddFunction("shader_set_uniform_f", -1);
           AddFunction("shader_set_uniform_f_array", 2);
           AddFunction("shader_set_uniform_matrix", 1);
           AddFunction("shader_set_uniform_matrix_array", 2);
           AddFunction("texture_set_stage", 2);
           AddFunction("texture_get_texel_width", 1);
           AddFunction("texture_get_texel_height", 1);
           AddFunction("shaders_are_supported", 0);
           AddFunction("vertex_format_begin", 0);
           AddFunction("vertex_format_end", 0);
           AddFunction("vertex_format_delete", 1);
           AddFunction("vertex_format_add_position", 0);
           AddFunction("vertex_format_add_position_3d", 0);
           AddFunction("vertex_format_add_colour", 0);
           AddFunction("vertex_format_add_normal", 0);
           AddFunction("vertex_format_add_textcoord", 0);
           AddFunction("vertex_format_add_custom", 2);
           AddFunction("vertex_create_buffer", 0);
           AddFunction("vertex_create_buffer_ext", 1);
           AddFunction("vertex_delete_buffer", 1);
           AddFunction("vertex_begin", 2);
           AddFunction("vertex_end", 1);
           AddFunction("vertex_position", 3);
           AddFunction("vertex_position_3d", 4);
           AddFunction("vertex_colour", 3);
           AddFunction("vertex_argb", 2);
           AddFunction("vertex_texcoord", 3);
           AddFunction("vertex_normal", 4);
           AddFunction("vertex_float1", 2);
           AddFunction("vertex_float2", 3);
           AddFunction("vertex_float3", 4);
           AddFunction("vertex_float4", 5);
           AddFunction("vertex_ubyte4", 5);
           AddFunction("vertex_submit", 3);
           AddFunction("vertex_freeze", 1);
           AddFunction("vertex_get_number", 1);
           AddFunction("vertex_get_buffer_size", 1);
           AddFunction("vertex_create_buffer_from_buffer", 2);
           AddFunction("vertex_create_buffer_from_buffer_ext", 4);
           AddFunction("push_local_notification", 4);
           AddFunction("push_get_first_local_notification", 1);
           AddFunction("push_get_next_local_notification", 1);
           AddFunction("push_cancel_local_notification", 1);
           AddFunction("skeleton_animation_set", 1);
           AddFunction("skeleton_animation_get", 0);
           AddFunction("skeleton_animation_mix", 3);
           AddFunction("skeleton_animation_set_ext", 2);
           AddFunction("skeleton_animation_get_ext", 1);
           AddFunction("skeleton_animation_get_duration", 1);
           AddFunction("skeleton_animation_clear", 1);
           AddFunction("skeleton_skin_set", 1);
           AddFunction("skeleton_skin_get", 0);
           AddFunction("skeleton_attachment_set", 2);
           AddFunction("skeleton_attachment_get", 1);
           AddFunction("skeleton_attachment_create", 8);
           AddFunction("skeleton_collision_draw_set", 1);
           AddFunction("skeleton_bone_data_get", 2);
           AddFunction("skeleton_bone_data_set", 2);
           AddFunction("skeleton_bone_state_get", 2);
           AddFunction("skeleton_bone_state_set", 2);
           AddFunction("draw_skeleton", 11);
           AddFunction("draw_skeleton_time", 11);
           AddFunction("draw_skeleton_collision", 9);
           AddFunction("skeleton_animation_list", 2);
           AddFunction("skeleton_skin_list", 2);
           AddFunction("skeleton_slot_data", 2);
           AddFunction("yyg_player_run", 4);
           AddFunction("yyg_player_restarted", 0);
           AddFunction("yyg_player_launch_args", 0);
           AddFunction("extension_stubfunc_real", -1);
           AddFunction("extension_stubfunc_string", -1);
           AddFunction("ps4_share_screenshot_enable", 1);
           AddFunction("ps4_share_video_enable", 1);
           AddFunction("xboxone_get_user_count", 0);
           AddFunction("xboxone_get_user", 1);
           AddFunction("xboxone_get_activating_user", 0);
           AddFunction("xboxone_user_is_active", 1);
           AddFunction("xboxone_user_is_guest", 1);
           AddFunction("xboxone_user_is_signed_in", 1);
           AddFunction("xboxone_user_is_remote", 1);
           AddFunction("xboxone_gamedisplayname_for_user", 1);
           AddFunction("xboxone_appdisplayname_for_user", 1);
           AddFunction("xboxone_agegroup_for_user", 1);
           AddFunction("xboxone_gamerscore_for_user", 1);
           AddFunction("xboxone_reputation_for_user", 1);
           AddFunction("xboxone_user_for_pad", 1);
           AddFunction("xboxone_pad_count_for_user", 1);
           AddFunction("xboxone_sponsor_for_user", 1);
           AddFunction("xboxone_pad_for_user", 2);
           AddFunction("xboxone_show_account_picker", 2);
           AddFunction("xboxone_sprite_add_from_gamerpicture", 4);
           AddFunction("xboxone_show_profile_card_for_user", 2);
           AddFunction("xboxone_set_savedata_user", 1);
           AddFunction("xboxone_get_savedata_user", 0);
           AddFunction("xboxone_get_file_error", 0);
           AddFunction("xboxone_was_terminated", 0);
           AddFunction("xboxone_is_suspending", 0);
           AddFunction("xboxone_is_constrained", 0);
           AddFunction("xboxone_suspend", 0);
           AddFunction("xboxone_show_help", 1);
           AddFunction("xboxone_license_trial_version", 0);
           AddFunction("xboxone_license_trial_user", 0);
           AddFunction("xboxone_license_trial_time_remaining", 0);
           AddFunction("xboxone_check_privilege", 3);
           AddFunction("xboxone_user_id_for_user", 1);
           AddFunction("xboxone_fire_event", -1);
           AddFunction("xboxone_get_stats_for_user", -1);
           AddFunction("xboxone_stats_setup", 3);
           AddFunction("xboxone_set_rich_presence", 3);
           AddFunction("xboxone_matchmaking_create", 5);
           AddFunction("xboxone_matchmaking_find", 4);
           AddFunction("xboxone_matchmaking_start", 1);
           AddFunction("xboxone_matchmaking_stop", 1);
           AddFunction("xboxone_matchmaking_session_get_users", 1);
           AddFunction("xboxone_matchmaking_session_leave", 1);
           AddFunction("xboxone_matchmaking_send_invites", 3);
           AddFunction("xboxone_matchmaking_set_joinable_session", 2);
           AddFunction("xboxone_matchmaking_join_invite", 3);
           AddFunction("xboxone_chat_add_user_to_channel", 2);
           AddFunction("xboxone_chat_remove_user_from_channel", 2);
           AddFunction("xboxone_chat_set_muted", 2);
           AddFunction("browser_input_capture", 1);
            // extra stuff

            AddFunction("caster_load", 1, GM_Type.Sound);
            AddFunction("scr_marker", 2, GM_Type.Instance);
            AddFunction("caster_loop", 2, GM_Type.Sound);

        }
       
      
        static void SetUpProperties()
        {
            allProperties = new Dictionary<string, PropertyInfo>();

            AddProperty("argument_relative", true, false, true);
            AddProperty("argument_count", true, false, true);
            AddProperty("argument", true, true, true);
            AddProperty("argument0", true, true, true);
            AddProperty("argument1", true, true, true);
            AddProperty("argument2", true, true, true);
            AddProperty("argument3", true, true, true);
            AddProperty("argument4", true, true, true);
            AddProperty("argument5", true, true, true);
            AddProperty("argument6", true, true, true);
            AddProperty("argument7", true, true, true);
            AddProperty("argument8", true, true, true);
            AddProperty("argument9", true, true, true);
            AddProperty("argument10", true, true, true);
            AddProperty("argument11", true, true, true);
            AddProperty("argument12", true, true, true);
            AddProperty("argument13", true, true, true);
            AddProperty("argument14", true, true, true);
            AddProperty("argument15", true, true, true);
            AddProperty("debug_mode", true, false, true);
            AddProperty("pointer_invalid", true, false, true);
            AddProperty("pointer_null", true, false, true);
            AddProperty("undefined", true, false, true);
            AddProperty("room", true, true, true);
            AddProperty("room_first", true, false, true);
            AddProperty("room_last", true, false, true);
            AddProperty("transition_kind", true, true, true);
            AddProperty("transition_steps", true, true, true);
            AddProperty("score", true, true, true);
            AddProperty("lives", true, true, true);
            AddProperty("health", true, true, true);
            AddProperty("game_id", true, false, true);
            AddProperty("game_display_name", true, false, true);
            AddProperty("game_project_name", true, false, true);
            AddProperty("game_save_id", true, false, true);
            AddProperty("working_directory", true, false, true);
            AddProperty("temp_directory", true, false, true);
            AddProperty("program_directory", true, false, true);
            AddProperty("instance_count", true, false, true);
            AddProperty("instance_id", true, false, true);
            AddProperty("room_width", true, true, true);
            AddProperty("room_height", true, true, true);
            AddProperty("room_caption", true, true, true);
            AddProperty("room_speed", true, true, true);
            AddProperty("room_persistent", true, true, true);
            AddProperty("background_color", true, true, true);
            AddProperty("background_showcolor", true, true, true);
            AddProperty("background_colour", true, true, true);
            AddProperty("background_showcolour", true, true, true);
            AddProperty("background_visible", true, true, true);
            AddProperty("background_foreground", true, true, true);
            AddProperty("background_index", true, true, true);
            AddProperty("background_x", true, true, true);
            AddProperty("background_y", true, true, true);
            AddProperty("background_width", true, false, true);
            AddProperty("background_height", true, false, true);
            AddProperty("background_htiled", true, true, true);
            AddProperty("background_vtiled", true, true, true);
            AddProperty("background_xscale", true, true, true);
            AddProperty("background_yscale", true, true, true);
            AddProperty("background_hspeed", true, true, true);
            AddProperty("background_vspeed", true, true, true);
            AddProperty("background_blend", true, true, true);
            AddProperty("background_alpha", true, true, true);
            AddProperty("view_enabled", true, true, true);
            AddProperty("view_current", true, false, true);
            AddProperty("view_visible", true, true, true);
            AddProperty("view_xview", true, true, true);
            AddProperty("view_yview", true, true, true);
            AddProperty("view_wview", true, true, true);
            AddProperty("view_hview", true, true, true);
            AddProperty("view_xport", true, true, true);
            AddProperty("view_yport", true, true, true);
            AddProperty("view_wport", true, true, true);
            AddProperty("view_hport", true, true, true);
            AddProperty("view_angle", true, true, true);
            AddProperty("view_hborder", true, true, true);
            AddProperty("view_vborder", true, true, true);
            AddProperty("view_hspeed", true, true, true);
            AddProperty("view_vspeed", true, true, true);
            AddProperty("view_object", true, true, true);
            AddProperty("view_surface_id", true, true, true);
            AddProperty("mouse_x", true, false, true);
            AddProperty("mouse_y", true, false, true);
            AddProperty("mouse_button", true, true, true);
            AddProperty("mouse_lastbutton", true, true, true);
            AddProperty("keyboard_key", true, true, true);
            AddProperty("keyboard_lastkey", true, true, true);
            AddProperty("keyboard_lastchar", true, true, true);
            AddProperty("keyboard_string", true, true, true);
            AddProperty("show_score", true, true, true);
            AddProperty("show_lives", true, true, true);
            AddProperty("show_health", true, true, true);
            AddProperty("caption_score", true, true, true);
            AddProperty("caption_lives", true, true, true);
            AddProperty("caption_health", true, true, true);
            AddProperty("fps", true, false, true);
            AddProperty("fps_real", true, false, true);
            AddProperty("current_time", true, false, true);
            AddProperty("current_year", true, false, true);
            AddProperty("current_month", true, false, true);
            AddProperty("current_day", true, false, true);
            AddProperty("current_weekday", true, false, true);
            AddProperty("current_hour", true, false, true);
            AddProperty("current_minute", true, false, true);
            AddProperty("current_second", true, false, true);
            AddProperty("event_type", true, false, true);
            AddProperty("event_number", true, false, true);
            AddProperty("event_object", true, false, true);
            AddProperty("event_action", true, false, true);
            AddProperty("secure_mode", true, false, true);
            AddProperty("error_occurred", true, true, true);
            AddProperty("error_last", true, true, true);
            AddProperty("gamemaker_registered", true, false, true);
            AddProperty("gamemaker_pro", true, false, true);
            AddProperty("application_surface", true, false, true);
            AddProperty("os_type", true, false, true);
            AddProperty("os_device", true, false, true);
            AddProperty("os_browser", true, false, true);
            AddProperty("os_version", true, false, true);
            AddProperty("browser_width", true, false, true);
            AddProperty("browser_height", true, false, true);
            AddProperty("async_load", true, false, true);
            AddProperty("display_aa", true, false, true);
            AddProperty("iap_data", true, false, true);
            AddProperty("cursor_sprite", true, true, true);
            AddProperty("delta_time", true, true, true);
            AddProperty("webgl_enabled", true, false, true);
            AddProperty("x", true, true, true);
            AddProperty("y", true, true, true);
            AddProperty("xprevious", true, true, true);
            AddProperty("yprevious", true, true, true);
            AddProperty("xstart", true, true, true);
            AddProperty("ystart", true, true, true);
            AddProperty("hspeed", true, true, true);
            AddProperty("vspeed", true, true, true);
            AddProperty("direction", true, true, true);
            AddProperty("speed", true, true, true);
            AddProperty("friction", true, true, true);
            AddProperty("gravity", true, true, true);
            AddProperty("gravity_direction", true, true, true);
            AddProperty("object_index", true, false, true, GM_Type.Instance);
            AddProperty("id", true, false, true);
            AddProperty("alarm", true, true, true);
            AddProperty("solid", true, true, true);
            AddProperty("visible", true, true, true);
            AddProperty("persistent", true, true, true);
            AddProperty("depth", true, true, true);
            AddProperty("bbox_left", true, false, true);
            AddProperty("bbox_right", true, false, true);
            AddProperty("bbox_top", true, false, true);
            AddProperty("bbox_bottom", true, false, true);
            AddProperty("sprite_index", true, true, true, GM_Type.Sprite);
            AddProperty("image_index", true, true, true);
            AddProperty("image_single", true, true, true);
            AddProperty("image_number", true, false, true);
            AddProperty("sprite_width", true, false, true);
            AddProperty("sprite_height", true, false, true);
            AddProperty("sprite_xoffset", true, false, true);
            AddProperty("sprite_yoffset", true, false, true);
            AddProperty("image_xscale", true, true, true);
            AddProperty("image_yscale", true, true, true);
            AddProperty("image_angle", true, true, true);
            AddProperty("image_alpha", true, true, true);
            AddProperty("image_blend", true, true, true);
            AddProperty("image_speed", true, true, true);
            AddProperty("mask_index", true, true, true);
            AddProperty("path_index", true, false, true);
            AddProperty("path_position", true, true, true);
            AddProperty("path_positionprevious", true, true, true);
            AddProperty("path_speed", true, true, true);
            AddProperty("path_scale", true, true, true);
            AddProperty("path_orientation", true, true, true);
            AddProperty("path_endaction", true, true, true);
            AddProperty("timeline_index", true, true, true);
            AddProperty("timeline_position", true, true, true);
            AddProperty("timeline_speed", true, true, true);
            AddProperty("timeline_running", true, true, true);
            AddProperty("timeline_loop", true, true, true);
            AddProperty("phy_rotation", true, true, true);
            AddProperty("phy_position_x", true, true, true);
            AddProperty("phy_position_y", true, true, true);
            AddProperty("phy_angular_velocity", true, true, true);
            AddProperty("phy_linear_velocity_x", true, true, true);
            AddProperty("phy_linear_velocity_y", true, true, true);
            AddProperty("phy_speed_x", true, true, true);
            AddProperty("phy_speed_y", true, true, true);
            AddProperty("phy_speed", true, false, true);
            AddProperty("phy_angular_damping", true, true, true);
            AddProperty("phy_linear_damping", true, true, true);
            AddProperty("phy_bullet", true, true, true);
            AddProperty("phy_fixed_rotation", true, true, true);
            AddProperty("phy_active", true, true, true);
            AddProperty("phy_mass", true, false, true);
            AddProperty("phy_inertia", true, false, true);
            AddProperty("phy_com_x", true, false, true);
            AddProperty("phy_com_y", true, false, true);
            AddProperty("phy_dynamic", true, false, true);
            AddProperty("phy_kinematic", true, false, true);
            AddProperty("phy_sleeping", true, false, true);
            AddProperty("phy_position_xprevious", true, true, true);
            AddProperty("phy_position_yprevious", true, true, true);
            AddProperty("phy_collision_points", true, false, true);
            AddProperty("phy_collision_x", true, false, true);
            AddProperty("phy_collision_y", true, false, true);
            AddProperty("phy_col_normal_x", true, false, true);
            AddProperty("phy_col_normal_y", true, false, true);
           
        }
        static Constants()
        {
            defined = new HashSet<string>();
            SetUpEventConstants();
            SetupGlobalFunctions();
            SetUpProperties();
        }
       
    }
}
