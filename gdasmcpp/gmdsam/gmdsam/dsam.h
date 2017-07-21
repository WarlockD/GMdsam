#pragma once
#include "gm_lib.h"
#include <map>
#include "ast.h"

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
				case Type::Double: return "Double";
				case Type::Float: return "Float";
				case Type::Int: return "Int";
				case Type::Long: return "Long";
				case Type::Bool: return "Bool";
				case Type::Var: return "Var";
				case Type::String: return "String";
				case Type::Short: return "Short";
				case Type::Instance: return "Instance";
				case Type::Sprite: return "Sprite";
				case Type::Sound: return "Sound";
				case Type::Path: return "Path";
				default:
					//case Type::NoType: 
					return "NoType";
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

			size_t byte_size() const;
			template<typename C, typename E>
			void to_stream(std::basic_ostream<C, E>& os) const {
				so << to_string();
			}

		};
		class invalid_type_conversion : public gm_exception {
			GM_Type _from;
			GM_Type _to;
		public:
			invalid_type_conversion(GM_Type from, GM_Type to) :_from(from), _to(to), gm_exception("gm::dsam::invalid_type_conversion(%s -> %s)", to_cstring(from), to_cstring(to)) {}
			const char* what() const { return "gm::dsam::invalid_type_conversion"; }
		};
		class VarRefrence {
			String _name;
			int _extra;
		public:
			VarRefrence(String name, int extra = 0) : _name(name), _extra(extra) {}
			const String& name() const { return _name; }
			int extra() const { return _extra; }
		};



		class Value {

		public:
			Value() : _value((int)0), _type(Type::NoType) {}
			Value(double v) : _value(v), _type(Type::Double) {}
			Value(float v) : _value(v), _type(Type::Float) {}
			Value(int32_t v) : _value(v), _type(Type::Int) {}
			Value(int64_t v) : _value(v), _type(Type::Long) {}
			Value(bool v) : _value(v), _type(Type::Bool) {}
			Value(String s) : _value(s), _type(Type::String) {}
			Value(const Object& o) : _value(o), _type(Type::Instance) {}
			Value(Object&& o) : _value(o), _type(Type::Instance) {}
			Value(const VarRefrence& o) : _value(o), _type(Type::Var) {}
			Value(VarRefrence&& o) : _value(o), _type(Type::Var) {}
			Value(short o) : _value(o), _type(Type::Short) {}
			template<typename T>
			T get() const { return std::get<T>(_value); }
			template<>
			StringView get<StringView>() const {
				return std::get<String>(_value).strv();
			}
			int to_integer() const;
			//template<>
		//	StringView get<StringView>() const { return get<String>(); }
			GM_Type type() const { return _type; }
			Value convert(GM_Type to, gm::DataWinFile& file) const;
			bool operator==(const Value& value) const;
			bool operator!=(const Value& value) const { return !(*this == value); }
			using union_type = std::variant<double, float, int32_t, int64_t, bool, VarRefrence, String, Object, short>;
		public:
			GM_Type _type;
			union_type _value;
		};

	}
}