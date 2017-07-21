#pragma once

#include <map>
#include "dsam.h"

namespace gm {
	namespace dsam {
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

			class Opcode {

			public:
				using types_pair = std::pair<GM_Type, GM_Type>;
				template<typename C, typename E>
				struct visit_stream {
					std::basic_ostream<C, E>& os;
					void operator()(uint32_t v) {
						os << '#' << std::dec << v;
					}
					void operator()(const std::string& v) {
						os << "$$ " << v << " $$";
					}
					void operator()(const types_pair& v) {
						v.first.to_stream(os);
						if (v.second != GM_Type()) {
							os << ',';
							v.second.to_stream(os);
						}
					}
					void operator()(const Value& v) {
						os << v;
					}
				};
				Opcode(uint32_t offset, NewOpcode op, uint32_t branch) : _offset(offset), _opcode(op), _operand(branch) {}
				Opcode(uint32_t offset, NewOpcode op, const types_pair&  types) : _offset(offset), _opcode(op), _operand(types) {}
				Opcode(uint32_t offset, NewOpcode op, types_pair&& types) : _offset(offset), _opcode(op), _operand(types) {}
				Opcode(uint32_t offset, NewOpcode op, const std::string&  message) : _offset(offset), _opcode(op), _operand(message) {}
				Opcode(uint32_t offset, NewOpcode op, std::string&& message) : _offset(offset), _opcode(op), _operand(message) {}
				Opcode(uint32_t offset, NewOpcode op, Value&& operand) : _offset(offset), _opcode(op), _operand(operand) {}
				Opcode(uint32_t offset, NewOpcode op, const Value& operand) : _offset(offset), _opcode(op), _operand(operand) {}

				operator NewOpcode() const { return _opcode; }
				NewOpcode op() const { return _opcode; }
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
				uint32_t offset() const { return std::get<uint32_t>(_operand); }
				Value& operand() { return std::get<Value>(_operand); }
				const Value& operand() const { return std::get<Value>(_operand); }
				const types_pair& types() const { return std::get<types_pair>(_operand); }
				const std::string& message() const { return std::get<std::string>(_operand); }

				template<typename C, typename E>
				void to_stream(std::basic_ostream<C, E>& os) const {
					os << std::dec << std::setw(6) << std::fill(' ') << '[' << _offset << "]: ";
					os << to_string(_opcode); << "  ";
					std::visit(_operand, visit_stream(os));
				}

			private:
				uint32_t _offset;
				NewOpcode _opcode;
				std::variant<uint32_t, types_pair, Value, std::string> _operand;
			};
			// Going to try to skip the "to instruction" step as I am faimuar enough with it
			class dsam {
				gm::DataWinFile& file;
				const gm::Undertale_1_01_Code& code;
				std::vector<Opcode> _opcodes;
				union cvt_t {
					uint32_t u[2];
					int32_t i;
					double d;
					float f;
					int64_t l;
				};
			public:
				dsam(gm::DataWinFile& file, const gm::Undertale_1_01_Code& code) : file(file), code(code) {}
				static int GetSecondTopByte(int int_0) { return int_0 >> 16 & 255; }
				static int GetTopByte(int int_0) { return int_0 >> 24 & 255; }
				static int OpCodeWithOffsetBranch(int int_0, int int_1) { int int0 = int_0 << 24 | int_1 >> 2 & 8388607; return int0; }
				static int ThreebytesToTop(int int_0, int int_1, int int_2) { int int0 = int_0 << 24 | int_1 << 16 | int_2 << 8; return int0; }
				static int TwoBytesToTop(int int_0, int int_1) { return int_0 << 24 | int_1 << 16; }
				uint32_t getBranchOffset(uint32_t code) {
					// Ok having a horred problem here.  I thought it was a 24 bit signed value
					// but when I try to detect a 1 in the signed value, it dosn't work.
					// so instead of checking for 0x80 0000 I am checking for 0x40 0000,
					// I can get away with this cause the offsets never go this far, still,
					// bug and unkonwn
					//  if ((op & 0x800000) != 0) op |= 0xFFF00000; else op &= 0x7FFFFF;
					uint32_t offset;
					if ((code & 0x400000) != 0) offset = code | 0xFFF00000; else offset = code & 0x7FFFFF;
					assert((int)offset < 80000); // arbatrary but havn't seen code this big
					return offset;
				}
				String LookupString(uint32_t index) {
					index &= 0x1FFFFF;
					if (index < 0 || index >= file.stringtable().size()) throw gm_exception("Cannot find string index %i", index);
					return file.stringtable()[index];
				}
				VarRefrence BuildUnresolvedVar(int operand, int extra) {
					return VarRefrence(LookupString(operand), (short)extra);
				}

				uint32_t DissasembleOneOpcode(uint32_t pos) {

					cvt_t cvt;
					uint32_t opcode = code[pos++];
					int topByte = GetTopByte(opcode);
					int secondTopByte = GetSecondTopByte(opcode);
					Opcode::types_pair types;
					NewOpcode op = static_cast<NewOpcode>(opcode >> 24);
					if ((topByte & 160) == 128)
						types.first = GM_Type(static_cast<Type>(secondTopByte & 15));
					else if ((topByte & 160) == 0) {
						types.first = GM_Type(static_cast<Type>(secondTopByte & 15));
						types.first = GM_Type(static_cast<Type>((secondTopByte >> 4) & 15));
					}
					if ((topByte & 32) != 0) { // branch
						_opcodes.emplace_back(pos, op, static_cast<uint32_t>(pos + (pos << 9 >> 7)));
					}
					else if ((topByte & 64) != 0) { // we have an operand
						switch (types.first) {
						case Type::Double:
							cvt.u[0] = code[pos++]; cvt.u[0] = code[pos++];
							_opcodes.emplace_back(pos, op, Value(cvt.d));
							break;
						case Type::Float:
							cvt.u[0] = code[pos++];
							_opcodes.emplace_back(pos, op, Value(cvt.f));
							break;
						case Type::Int:
							cvt.u[0] = code[pos++];
							if (op == NewOpcode::call) { // function call
								_opcodes.emplace_back(pos, op, Value(LookupString(cvt.i)));
							}
							else
								_opcodes.emplace_back(pos, op, cvt.i);
							break;
						case Type::Long:
							cvt.u[0] = code[pos++]; cvt.u[0] = code[pos++];
							_opcodes.emplace_back(pos, op, Value(cvt.l));
							break;
						case Type::Bool:
							cvt.u[0] = code[pos++];
							_opcodes.emplace_back(pos, op, cvt.i == 0 ? false : true);
						case Type::Var:
							cvt.u[0] = code[pos++];
							_opcodes.emplace_back(pos, op, Value(BuildUnresolvedVar(cvt.i, opcode & 0xFFFF)));
							break;
						case Type::String:
							cvt.u[0] = code[pos++];
							_opcodes.emplace_back(pos, op, Value(LookupString(cvt.i)));
							break;
						case Type::Short:
							_opcodes.emplace_back(pos, op, Value(static_cast<short>((opcode << 16) >> 16)));
							break;
						default:
							assert(0); // bad var
							break;
						}

					}
					else {
						_opcodes.emplace_back(pos, op, types);
					}
					return pos;
				}
				int Dissasemble()
				{
					_opcodes.clear();
					for (uint32_t i = 0; i < code.size(); i = DissasembleOneOpcode(i)) {
					}
					return 0;
				}

			};
		};

	};


}

