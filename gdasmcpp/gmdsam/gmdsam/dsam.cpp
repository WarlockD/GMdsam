#include "dsam.h"
#include "new_code.h"

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
				case NewOpcode:: or : return "or";
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

		class varent_visit {
		public:
			GM_Type to;
			gm::DataWinFile& file;
			const Value& old_value;
			template<typename T>
			typename std::enable_if<std::is_integral<T>::value, Value>::type
				 _convert(T value) const {
				switch (to) {
				case Type::Bool:
					return Value(value == T{} ? false : true);
				case Type::Double:
					return Value(static_cast<double>(value));
				case Type::Float:
					return Value(static_cast<float>(value));
				case Type::Int:
					return Value(static_cast<int32_t>(value));
				case Type::Long:
					return Value(static_cast<int64_t>(value));
				case Type::Var:
					return Value(old_value);
				case Type::String:
					return Value(); // std::to_string(value));
				case Type::Instance:
				{
					size_t instance = static_cast<size_t>(value);
					size_t obj_count = file.resource_count<gm::Object>();
					if (instance < obj_count) {
						auto object = file.resource_at<gm::Object>(instance); // random sprite
						return Value(object);
					}
					return Value(); // no type, invalid, should throw
				}
				case Type::Short:
					return Value(static_cast<short>(value));
				}
			}
			template<typename T>
			typename std::enable_if<std::is_floating_point<T>::value, Value >::type
				_convert(T value) const {
				switch (to) {
				case Type::Bool:
					return Value(value == T{} ? false : true);
				case Type::Double:
					return Value(static_cast<double>(value));
				case Type::Float:
					return Value(static_cast<float>(value));
				case Type::Int:
					return Value(static_cast<int32_t>(value));
				case Type::Long:
					return Value(static_cast<int64_t>(value));
				case Type::Var:
					return Value(old_value);
				case Type::String:
					return Value(); // std::to_string(value));
				case Type::Instance:
					assert(0); // float to an instance? errrrr
					return Value(); // no type, invalid
				case Type::Short:
					return Value(static_cast<short>(value));
				}
			}
			Value _convert(Value* value) const {
				assert(0);
				return Value();
			}
			template<typename T>
			T string_cast(std::stringstream& ss) const {
				T v;
				ss >> v;
				return v;
			}
			Value _convert(const Symbol& value) const {
				std::stringstream ss(value.c_str());
				// slloow
				switch (to) {
				case Type::Bool:
					return Value(string_cast<bool>(ss));
				case Type::Double:
					return Value(string_cast<double>(ss));
				case Type::Float:
					return Value(string_cast<float>(ss));
				case Type::Int:
					return Value(string_cast<int32_t>(ss));
				case Type::Long:
					return Value(string_cast<int64_t>(ss));
				case Type::Var:
					return old_value;
				case Type::String:
					return old_value;
				case Type::Instance: // doing this is a looong ass search
				{
					size_t obj_count = file.resource_count<gm::Object>();
					for (auto& o : file.resource_container<gm::Object>()) {
						if (o.name() == value.c_str()) {
							return Value(o);
						}
					}
					return Value(); // invalid should though
				}
				case Type::Short:
					return Value(string_cast<short>(ss));
				}

				assert(0);
				return Value();
			}
		public:
			varent_visit(GM_Type to, gm::DataWinFile& file, const Value& Value) : to(to), file(file), old_value(Value) {}
			template<typename T>
			typename std::enable_if<std::is_arithmetic<T>::value, Value>::type
				operator()(T v) const { return _convert(v); }
		//	using union_type = std::variant<double, float, int32_t, int64_t, bool, Value*, Symbol, Object, short>;
			Value operator()(Value* v) const { return _convert(v); }
			Value operator()(Symbol v) const { return _convert(v); }
			Value operator()(Object v) const { 
				assert(0); // invalid conversion
				return Value();
			}
			Value operator()(VarRefrence v) const {
				assert(0); // invalid conversion
				return Value();
			}
			
		
		};
		int Value::to_integer() const {
			switch (_type) {
			case Type::Bool:
				return static_cast<int>(std::get<bool>(_value));
			case Type::Double:
				return static_cast<int>(std::get<double>(_value));
			case Type::Float:
				return static_cast<int>(std::get<float>(_value));
			case Type::Int:
				return static_cast<int>(std::get<int32_t>(_value));
			case Type::Long:
				return static_cast<int>(std::get<int64_t>(_value));
			case Type::Var:
				return 0;
			default:
				return 0;
			} // throw?
		}

		Value Value::convert(GM_Type to, gm::DataWinFile& file) const {
			if (to == _type) return *this;
			varent_visit visitor(to, file, *this);
			return std::visit(visitor, _value);
		}
		bool Value::operator==(const Value& value) const {
			if (this->type().isNumber() && value.type().isNumber()) {
				return to_integer() == value.to_integer();
			}
			return false;
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

		size_t GM_Type::byte_size() const {
			switch (_type) {
			case Type::Short:
				return 0; // inside the instruction
			case Type::Long:
			case Type::Double:
				return 8;
			default:
				return 4; // rest are ints
			}
		}
	}
}