#include "dsam.h"


namespace gm {
	namespace dsam {
		const char* to_cstring(Type t) {
			switch (t) {
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
		namespace undertale_1_01 {
			const char* to_cstring(NewOpcode t) {
				switch (t) {
					case NewOpcode::popv: return "popv";
					case NewOpcode::conv: return "conv";
					case NewOpcode::mul: return "mul";
					case NewOpcode::div: return "div";
					case NewOpcode::rem: return "rem";
					case NewOpcode::mod: return "mod";
					case NewOpcode::add: return "add";
					case NewOpcode::sub: return "sub";
					case NewOpcode::and: return "and";
					case NewOpcode::or: return "or";
					case NewOpcode::xor: return "xor";
					case NewOpcode::neg: return "neg";
					case NewOpcode::not: return "not";
					case NewOpcode::shl: return "shl";
					case NewOpcode::shr: return "shr";
					case NewOpcode::set: return "set";
							// Set seems to be like a cmp for other stuff
							// 1 : <
							// 2 : <=
							// 3 : ==
							// 4 : !=
							// 5 : >=
							// 6 : = >
					case NewOpcode::pop: return "pop";
					case NewOpcode::pushv: return "pushv";
					case NewOpcode::pushi: return "pushi"; // push int? ah like a pushe
					case NewOpcode::dup: return "dup";
							//  call = 153,
					case NewOpcode::ret: return "ret";
					case NewOpcode::exit: return "exit";
					case NewOpcode::popz: return "popz";
					case NewOpcode::b: return "b";
					case NewOpcode::bt: return "bt";
					case NewOpcode::bf: return "bf";
					case NewOpcode::pushenv: return "pushenv";
					case NewOpcode::popenv: return "popenv";
					case NewOpcode::push: return "push"; // generic? -1
					case NewOpcode::pushl: return "pushl"; // local? -7
					case NewOpcode::pushg: return "pushg"; // global? -5 // id is the last bit?
					case NewOpcode::pushb: return "pushb"; // built in? hummmm
					case NewOpcode::call: return "call";
					case NewOpcode::break_: return "break";
					default:
					return "unkonwn op";

				}
			}
		}

		GM_Type GM_Type::ConvertType(GM_Type t0, GM_Type t1)
		{
			if (t0 == t1) return t0;
			if (t1 == Type::Bool) return t1; // bool ALWAYS overrides
			if (t1 == Type::String && t0.isInstance()) throw invalid_type_conversion(t0, t1);
			if (t1.isBestVar()) return t1;
			if (t0.isBestVar()) return t0;
			switch (t0)
			{
			case Type::Var:
				return t1; // Vars are general variables so we don't want that
			case Type::String:
				assert(t1.isInstance()); // check in case
				return t0;
			case Type::Bool:
				return t0; // bool ALWAYS overrides eveything
			case Type::Short:
			case Type::NoType:
				return t1; // whatever t1 is its better
			case Type::Double:
				assert(!t1.isInstance());
				if (t1.isNumber()) return t0; // we can convert
				else if (t1.isInstance() || t1 == Type::String) return t1; // instance is MUCH better than double, more important
				return t0;
			case Type::Sound:
			case Type::Instance:
			case Type::Sprite:
				if (t1 == Type::Int || t1 == Type::Short) return t0; // we can be an instance
				throw invalid_type_conversion(t0, t1);
			case Type::Int:
				if (t1 == Type::Long || t1 == Type::Float || t1 == Type::Double || t1 == Type::Instance || t1 == Type::Sprite) return t1;
				else return t0;
			case Type::Long:
				if (t1 == Type::Double) return t1;
				else return t0;
			case Type::Float:
				if (t1 == Type::Double) return t1;
				else return t0;
			default:
				// should not be able to get here
				throw invalid_type_conversion(t0, t1);
			}
		}

	};

}
