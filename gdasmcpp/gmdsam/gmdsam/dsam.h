#pragma once
#include "gm_lib.h"

namespace gm {
	namespace dsam {
		enum class Type : int
		{
			Double = 0,
			Float,
			Int,
			Long,
			Bool,
			Var,
			String,
			Short = 15, // This is usally short anyway
			Instance,
			Sprite,
			Sound,
			Path,
			NoType = -100,

		};
		const char* to_cstring(Type t);

		class GM_Type
		{
			Type _type;
		public:
			const char* to_string() const {
				switch (_type) {
				case Type::Double: return "Type::Double";
				case Type::Float: return "Type::Float";
				case Type::Int: return "Type::Int";
				case Type::Long: return "Type::Long";
				case Type::Bool: return "Type::Bool";
				case Type::Var: return "Type::Var";
				case Type::String: return "Type::String";
				case Type::Short: return "Type::Short";
				case Type::Instance: return "Type::Instance";
				case Type::Sprite: return "Type::Sprite";
				case Type::Sound: return "Type::Sound";
				case Type::Path: return "Type::Path";
				default:
					//case Type::NoType: 
					return "Type::NoType";
				}
			}

			GM_Type(Type type = Type::NoType) : _type(type) {}
			operator Type() const { return _type; }
			bool isReal() const { return _type == Type::Double || _type == Type::Float; }
			bool isInstance() const { return _type == Type::Instance || _type == Type::Sprite || _type == Type::Sound || _type == Type::Path; }
			bool canBeInstance() const { return _type == Type::Int || _type == Type::Short || isInstance(); }
			bool isInteger() const { return _type == Type::Int || _type == Type::Long || _type == Type::Short; }
			bool isNumber() const { return isReal() || isInteger() || !isInstance(); }

			// This is the top dog, you can't convert this downward without a function or some cast
			bool isBestVar() { return isInstance() || _type == Type::String || _type == Type::Double || _type == Type::Bool; }
			static GM_Type ConvertType(GM_Type t0, GM_Type t1);

		};
		class invalid_type_conversion : public gm_exception {
			GM_Type _from;
			GM_Type _to;
		public:
			invalid_type_conversion(GM_Type from, GM_Type to) :_from(from), _to(to), gm_exception("gm::dsam::invalid_type_conversion(%s -> %s)", to_cstring(from), to_cstring(to)) {}
			const char* what() const { return "gm::dsam::invalid_type_conversion"; }
		};
		namespace undertale_1_00 {

		}
		// from c# source
		namespace undertale_1_01 {
			enum class NewOpcodeCondtions
			{
				Bad = 0,
				Lt,
				Leq,
				Eq,
				Neq,
				Gte,
				Gt
			};
			enum class NewOpcode
			{
				popv = 5,
				conv = 7,
				mul = 8,
				div = 9,
				rem = 10,
				mod = 11,
				add = 12,
				sub = 13,
				and = 14,
				or = 15,
				xor = 16,
				neg = 17,
				not = 18,
				shl = 19,
				shr = 20,
				set = 21,
				// Set seems to be like a cmp for other stuff
				// 1 : <
				// 2 : <=
				// 3 : ==
				// 4 : !=
				// 5 : >=
				// 6 : = >
				pop = 69,
				pushv = 128,
				pushi = 132, // push int? ah like a pushe
				dup = 134,
				//  call = 153,
				ret = 156,
				exit = 157,
				popz = 158,
				b = 182,
				bt = 183,
				bf = 184,
				pushenv = 186,
				popenv = 187,
				push = 192, // generic? -1
				pushl = 193, // local? -7
				pushg = 194, // global? -5 // id is the last bit?
				pushb = 195, // built in? hummmm
				call = 217,
				break_ = 255,
			};
			const char* to_cstring(NewOpcode t);
			class opcode {

				uint32_t _code;
			public:
				opcode(uint32_t code) : _code(code) {}
				operator uint32_t() const { return _code; }
				NewOpcode op() const { return static_cast<NewOpcode>(_code >> 24); }
				bool IsUnconditionalControlFlow() const { return op() == NewOpcode::b || op() == NewOpcode::exit || op() == NewOpcode::ret; }
				bool IsConditionalControlFlow() const { return op() == NewOpcode::bt || op() == NewOpcode::bf; }
				bool isBranch() const { return op() == NewOpcode::b || op() == NewOpcode::bt || op() == NewOpcode::bf; }
				bool isBinaryExpression() const { 
					return op() == NewOpcode::mul || op() == NewOpcode::div || op() == NewOpcode::mod || 
						op() == NewOpcode::add || op() == NewOpcode::sub || op() == NewOpcode::and || 
						op() == NewOpcode::or  || op() == NewOpcode::xor || op() == NewOpcode::shl || op() == NewOpcode::shr;
				}
				bool isUnataryExpression() const {
					return op() == NewOpcode::neg || op() == NewOpcode::not;
				}
				bool isExpression() const { return isBinaryExpression() || isUnataryExpression(); }
				uint32_t getBranchOffset() const {
					// Ok having a horred problem here.  I thought it was a 24 bit signed value
					// but when I try to detect a 1 in the signed value, it dosn't work.
					// so instead of checking for 0x80 0000 I am checking for 0x40 0000,
					// I can get away with this cause the offsets never go this far, still,
					// bug and unkonwn
					//  if ((op & 0x800000) != 0) op |= 0xFFF00000; else op &= 0x7FFFFF;
					uint32_t offset;
					if ((_code & 0x400000) != 0) offset = _code | 0xFFF00000; else offset = _code & 0x7FFFFF;
					assert((int)offset < 80000); // arbatrary but havn't seen code this big
					return offset;
					
					return
				
				}
			};
			// Going to try to skip the "to instruction" step as I am faimuar enough with it
			class dsam {
				gm::Undertale_1_01_Code code;
				static int GetSecondTopByte(int int_0) { return int_0 >> 16 & 255; }
				static int GetTopByte(int int_0) { return int_0 >> 24 & 255; }
				static int OpCodeWithOffsetBranch(int int_0, int int_1) { int int0 = int_0 << 24 | int_1 >> 2 & 8388607; return int0; }
				static int ThreebytesToTop(int int_0, int int_1, int int_2) { int int0 = int_0 << 24 | int_1 << 16 | int_2 << 8; return int0; }
				static int TwoBytesToTop(int int_0, int int_1) { return int_0 << 24 | int_1 << 16; }
				static int smethod_1(int int_5)
				{
					int num = 0;
					GM_Type int5 = (GM_Type)(int_5 & 15);
					switch (int5)
					{
					case Type::Double:
					{
						num = 8;
						break;
					}
					case Type::Float:
					case Type::Int:
					case Type::Bool:
					case Type::Var:
					case Type::String:
					{
						num = 4;
						break;
					}
					case Type::Long:
					{
						num = 8;
						break;
					}
					default:
					{
						if (int5 == Type::Short)
						{
							break;
						}
						break;
					}
					}
					return num;
				}

				int DissasembleOneOpcode()
				{
					int size = 1;
					int pos = (int)r.BaseStream.Position;
					int opcode = r.ReadInt32();
					int topByte = GetTopByte(opcode);
					int secondTopByte = GetSecondTopByte(opcode);
					int length = 11;
					writer.Write("{0:x8} : ", pos);
					writer.Write("{0:x8}", opcode);
					byte[] operand = null;
					if ((topByte & 64) != 0)
					{
						int extra = smethod_1(secondTopByte);
						operand = r.ReadBytes(extra);
						size += extra;
						foreach(var b in operand)
						{
							writer.Write("{0:x2}", b);
							length += 2;
						}
					}
					while (length < 36)
					{
						writer.Write(" ");
						length++;
					}
					string str = ((NewOpcode)topByte).ToString();
					writer.Write(str);
					length = length + str.Length;
					if ((topByte & 160) == 128)
					{
						writer.Write(((GM_Type)(secondTopByte & 15)).GMTypeToPostfix());
						length = length + 2;
					}
					else if ((topByte & 160) == 0)
					{
						writer.Write(((GM_Type)(secondTopByte & 15)).GMTypeToPostfix());
						writer.Write(((GM_Type)((secondTopByte >> 4) & 15)).GMTypeToPostfix());
						length = length + 4;
					}
					while (length < 46)
					{
						writer.Write(" ");
						length++;
					}
					if ((topByte & 64) != 0)
					{
						GM_Type eVMType = (GM_Type)(secondTopByte & 15);
						switch (eVMType)
						{
						case Type::Double:
						{
							writer.Write("{0}", BitConverter.ToDouble(operand, 0));
							break;
						}
						case Type::Float:
						{
							writer.Write("{0}", BitConverter.ToSingle(operand, 0));
							break;
						}
						case Type::Int:
						{
							int i = BitConverter.ToInt32(operand, 0);
							if (topByte == 217)
							{
								if (i < 0 || i >= File.Strings.Count)
								{
									writer.Write("$unknown_function$");
									break;
								}
								else
								{
									writer.Write("${0}$", File.Strings[i]);
									break;
								}
							}
							else
							{
								writer.Write("{0}", i);
								break;
							}
						}
						case Type::Long:
						{
							writer.Write("{0}", BitConverter.ToInt64(operand, 0));
							break;
						}
						case Type::Bool:
						{
							writer.Write("{0}", BitConverter.ToInt32(operand, 0) != 0);
							break;
						}
						case Type::Var:
						{
							int i = (BitConverter.ToInt32(operand, 0) & 0xFFFFFFF) + 1;
							if (i < 0 || i >= File.Strings.Count)
							{
								writer.Write("$null$");
								break;
							}
							else
							{
								string s = File.Strings[i].String;
								writer.Write("${0}$", s);
								break;
							}
						}
						case Type::String:
						{
							int i = BitConverter.ToInt32(operand, 0);
							if (i < 0 || i >= File.Strings.Count)
							{
								writer.Write("null");
								break;
							}
							else
							{
								writer.Write("\"{0}\"", File.Strings[i]);
								break;
							}
						}
						case Type::Short:
						{
							int i = (opcode << 16) >> 16;
							writer.Write("{0}", i);
						}
						break;
						default:
							writer.Write("T{0} ", eVMType.ToString());
							if (operand != null)
								foreach(var b in operand) writer.Write("{0:x2}", b);
							break;
						}
					}
					else if ((topByte & 32) != 0)
					{
						writer.Write("0x{0:x8}", pos + (opcode << 9 >> 7));
					}
					writer.WriteLine();
					return size;
				}
			}
		};

	};

	StringView to_string(gm::dsam::Type t);
}

