#pragma once
#include <cstdint>
#include <array>
#include <vector>
#include <type_traits>
#include <unordered_map>
#include <unordered_set>
#include <memory>
#include <string>
#include <functional>
#include <stack>
#include <string>
#include <sstream>
#include <iostream>
#include <iomanip>
#include <iterator>
#include <mutex>
#include <thread>
#include <atomic>
#include <fstream>
#include <string_view>
#include <variant>
#include <utility>
#include <cassert>
#include <math.h>

#ifndef __GCC
#include <safeint.h>
#endif

namespace util {
	namespace priv {
#ifndef __GCC
		template<typename _Tp>
		bool __raise_and_add(_Tp& __val, int __base, unsigned char __c) {
			if (SafeMultiply(__val, __base, &__val) || SafeAdd(__val, __c, &__val))
				return true;
			return false;
		}
#else
		 template<typename _Tp>
			bool
			 __raise_and_add(_Tp& __val, int __base, unsigned char __c)
			 {
			if (__builtin_mul_overflow(__val, __base, &__val)
				 || __builtin_add_overflow(__val, __c, &__val))
				 return false;
			return true;
			}
#endif
	}
}

struct StreamInterface {
	virtual void to_stream(std::ostream& os) const = 0;
	virtual std::string to_string() const {
		std::stringstream ss;
		to_stream(ss);
		return ss.str();
	}
	virtual ~StreamInterface() {}
};
inline std::ostream& operator<<(std::ostream& os, const StreamInterface& res) {
	res.to_stream(os);
	return os;
}
namespace console {
	class no_stream_exception : public std::exception {
	public:
		no_stream_exception() : std::exception() {}
		const char* what() const noexcept { return "Oject has no to_string method"; }
	};


};

namespace ext {
	// http://horstmann.com/cpp/iostreams.html
	struct set_format {
		const char* _fmt;
		set_format(const char* fmt) : _fmt(fmt) {}
		std::ostream& operator<<(std::ostream& os) const;
	};
	struct offset {
		size_t _offset;
		offset(size_t o) : _offset(o) {}
		void to_stream(std::ostream& os) const {
			os << "[0x" << std::uppercase << std::setw(8) << std::setfill('0') << std::hex << _offset << ']';
			os << std::dec << _offset;
		}
	};

};
inline std::ostream& operator<<(std::ostream& os, const ext::set_format& fmt) { fmt << os;  return os; }
inline std::ostream& operator<<(std::ostream& os, const ext::offset& fmt) { fmt.to_stream(os);  return os; }
	
namespace util {
	// http://stackoverflow.com/questions/257288/is-it-possible-to-write-a-template-to-check-for-a-functions-existence
	namespace priv {
		// http://stackoverflow.com/questions/257288/is-it-possible-to-write-a-template-to-check-for-a-functions-existence

		// https://stackoverflow.com/questions/22758291/how-can-i-detect-if-a-type-can-be-streamed-to-an-stdostream
		template<typename S, typename T>
		class is_streamable {
			template<typename SS, typename TT>
			static auto test(int) -> decltype(std::declval<SS&>() << std::declval<TT>(), std::true_type());
			template<typename, typename>
			static auto test(...)->std::false_type;
		public:
			static const bool value = decltype(test<S, T>(0))::value;
		};
		template<typename T>
		class has_hash_member {
			template<typename TT>
			static auto test(int) -> decltype(std::declval<TT&>().hash(), std::true_type());
			template<typename>
			static auto test(...)->std::false_type;
		public:
			static const bool value = decltype(test<T>(0))::value;
		};
		struct has_no_serializtion {};
		struct has_to_string : has_no_serializtion {};
		struct has_stream_operator : has_to_string {};
		struct has_stream_function : has_stream_operator {};
		struct debug_serializtion : has_stream_function {};

		template<typename T> auto serialize_imp(std::ostream& os, T const& obj, has_no_serializtion) { os << obj; } // throw console::no_stream_exception;
		template<typename T> auto serialize_imp(std::ostream& os, T const& obj, has_to_string)-> decltype(obj.to_string(), void()) { os << obj.to_string(); }
		template<typename T> auto serialize_imp(std::ostream& os, T const& obj, has_stream_operator)-> decltype(s << obj, void()) { obj << obj; }
		template<typename T> auto serialize_imp(std::ostream& os, T const& obj, has_stream_function)-> decltype(obj.to_stream(os), void()) { obj.to_stream(os); }
		template<typename T> auto serialize_imp(std::ostream& os, T const& obj, debug_serializtion)-> decltype(obj.to_debug(os), void()) { obj.to_debug(os); }

		template<typename C, typename E, typename T>
		std::basic_ostream<C, E>&  serialize(std::basic_ostream<C, E>& os, T const& obj) {
			serialize_imp(os, obj, priv::has_stream_function{});
			return os;
		}
		template<typename C, typename E, typename T>
		std::basic_ostream<C, E>& debug_serialize(std::basic_ostream<C, E>& os, T const& obj) {
			serialize_imp(os, obj, priv::debug_serializtion{});
			return os;
		}

		//template<class>
		//struct can_stream : std::false_type {};

		template<class> struct sfinae_true : std::true_type {};
		namespace detail {
			struct false_detection {};
			struct true_detection : false_detection {};
			template<class T>
			static auto test_at(true_detection)->sfinae_true<decltype(std::declval<T>().at(std::declval<size_t>()))>;
			template<class T>
			static auto test_at(false_detection)->std::false_type;
			template<typename T>
			static auto test(true_detection)->sfinae_true<T>;
			template<typename T>
			static auto test(true_detection)->std::false_type;

			template<class T>
			static auto test_stream(true_detection)->sfinae_true<decltype(std::declval<std::ostream>() << std::declval<T>())>;
			template<class T>
			static auto test_stream(false_detection)->std::false_type;

			template<class T>
			static auto test_to_stream(true_detection)->sfinae_true<decltype(std::declval<T>().to_stream(std::declval<std::ostream>()))>;
			template<class T>
			static auto test_to_stream(false_detection)->std::false_type;

			template<class T>
			static auto test_to_debug(true_detection)->sfinae_true<decltype(std::declval<T>().to_debug(std::declval<std::ostream>()))>;
			template<class T>
			static auto test_to_debug(false_detection)->std::false_type;

			template<typename T>
			struct test_it : decltype(detail::test<T>(true_detection{}))   {};
		};

		//template<typename T_ARRAY>
		//struct has_at_func : has_declval<decltype(std::declval<T_ARRAY>().at(std::declval<size_t>()))> {};
		template<typename T_ARRAY>
		struct has_at_func : decltype(detail::test_at<T_ARRAY>(detail::true_detection{})){};

	};
	template<typename T>
	struct can_stream : decltype(priv::detail::test_stream<T>(priv::detail::true_detection{})){};
	template<typename T>
	struct has_to_stream : decltype(priv::detail::test_to_stream<T>(priv::detail::true_detection{})){};
	template<typename T>
	struct has_to_debug : decltype(priv::detail::test_to_debug<T>(priv::detail::true_detection{})){};

	template<typename C, typename E, typename T>
	std::basic_ostream<C, E>&  serialize(std::basic_ostream<C, E>&  os, T const& obj)
	{
#if _DEBUG
		priv::debug_serialize(os, obj);
#else
		priv::serialize(os, obj);
#endif
	}


	template<typename T, typename = std::enable_if<(std::is_arithmetic<T>::value || std::is_pod<T>::value), T>::type>
	constexpr T cast(const uint8_t* ptr) { return *reinterpret_cast<const T*>(ptr); }

	template<typename T, typename A, typename = std::enable_if<((std::is_arithmetic<T>::value || std::is_pod<T>::value) && std::is_pointer<A>::value), T>::type>
	constexpr T cast(A ptr) { return cast(reinterpret_cast<const uint8_t*>(ptr)); }

	template<typename T, typename = std::enable_if<(std::is_arithmetic<T>::value || std::is_pod<T>::value)>::type>
	constexpr typename const std::pointer_traits<T>::pointer cast_ptr(const uint8_t* ptr) { return reinterpret_cast<const T*>(ptr); }

	template<typename T, typename A, typename = std::enable_if<((std::is_arithmetic<T>::value || std::is_pod<T>::value) && std::is_pointer<A>::value)>::type>
	constexpr typename const std::pointer_traits<T>::pointer cast_ptr(A ptr) { return cast(reinterpret_cast<const uint8_t*>(ptr)); }

	template<typename T>
	const T* read_struct(const uint8_t*& ptr) {
		const T* value = reinterpret_cast<const T*>(ptr);
		ptr += sizeof(T);
		return value;
	}
	template<typename T>
	T read_value(const uint8_t** ptr) {
		T value = *reinterpret_cast<const T*>(*ptr);
		*ptr += sizeof(T);
		return value;
	}
	inline size_t simple_hash(const char *str)
	{
		size_t hash = 5381;
		int c;
		while (c = (unsigned char)(*str++)) hash = ((hash << 5) + hash) + c; /* hash * 33 + c */
		return hash;
	}
	inline size_t simple_hash(const char *str, size_t len)
	{
		size_t hash = 5381;
		while (len > 0) {
			int c = *str++;
			hash = ((hash << 5) + hash) + c; /* hash * 33 + c */
			len--;
		}
		return hash;
	}

	template<typename Function, std::size_t... Indices>
	constexpr auto make_array_helper(Function f, std::index_sequence<Indices...>) 
		->std::array<typename std::result_of<Function(std::size_t)>::type, sizeof...(Indices)>
	{
		return { { f(Indices)... } };
	}
	// for making my own array https://stackoverflow.com/questions/19019252/create-n-element-constexpr-array-in-c11
	template<std::size_t N, typename Function>
	constexpr auto make_array(Function f)
		->std::array<typename std::result_of<Function(std::size_t)>::type, N>
	{
		return make_array_helper(f, std::make_index_sequence<N>{});
	}
#if 0
	template<typename TO, typename FROM,  typename = std::enable_if<std::is_floating_point<FROM>::value && std::is_integral<TO>::value>>
	inline static bool try_convert(TO& to, const FORM& from) {
		if (std::isfinite(from) && from >= static_cast<FROM>(std::numeric_limits<TO>::min()) && from <= static_cast<FROM>(std::numeric_limits<TO>::max())) {
			to = static_cast<TO>(from);
			return true;
		}
		return false;
	}
	template<typename TO, typename FROM, typename = std::enable_if<std::is_integral<FROM>::value && std::is_floating_point<TO>::value>>
	inline static bool try_convert(TO& to, const FORM& from) {
		if (from >= static_cast<FROM>(std::numeric_limits<TO>::min()) && from <= static_cast<FROM>(std::numeric_limits<TO>::max())) {
			to = static_cast<TO>(from);
			return true;
		}
		return false;
	}
#endif
	namespace priv {
#if 0
		template<typename T, typename = std::enable_if<std::is_floating_point<T>::value>>
		static size_t hasher(const T  n) {
			int i;
			int ni;
			n = std::frexp(n, &i) * -static_cast<lua_Number>(std::numeric_limits<int>::min());
			if (!try_convert(ni, n)) {  /* is 'n' inf/-inf/NaN? */
				assert(std::isnan(n) || std::isinf(std::fabs(n)));
				return 0;
			}
			else {  /* normal case */
				size_t u = static_cast<size_t>(i) + static_cast<size_t>(ni);  
				return  u <= static_cast<size>(std::numeric_limits<int>::max()) ? u : ~u;    
			}
		}
#endif
	}
}

// if we have a public function called hash() then use that for the hash of the object
template<typename C, typename E, typename U, typename = std::enable_if<!util::priv::is_streamable<std::basic_ostream<C, E>, U>::value>>
static inline std::basic_ostream<C, E>& operator<<(std::basic_ostream<C, E>& os, const U& obj) {
#if _DEBUG
	util::priv::debug_serialize(os, obj);
#else
	util::priv::serialize(os, obj);
#endif
	return os;
}

// fucking windows
#ifdef max
#undef max
#endif
#ifdef min
#undef min
#endif

namespace lua {
	using lua_Number = double;
	using lua_Integer = int64_t;
	/*
	** 'lu_mem' and 'l_mem' are unsigned/signed integers big enough to count
	** the total memory used by Lua (in bytes). Usually, 'size_t' and
	** 'ptrdiff_t' should work, but we use 'long' for 16-bit machines.
	*/
	static constexpr lua_Integer LUA_MAXINTEGER = std::numeric_limits<lua_Integer>::max();
	using lu_mem = size_t;
	using l_mem = ptrdiff_t;
	/* chars used as small naturals (so that 'char' is reserved for characters) */
	using lu_byte = uint8_t; 
	 /* maximum value for size_t */
	static constexpr size_t MAX_SIZET = std::numeric_limits<size_t>::max();
	static constexpr lu_mem MAX_LUMEM = std::numeric_limits<lu_mem>::max();
	static constexpr l_mem MAX_LMEM = std::numeric_limits<l_mem>::max();
	static constexpr int MAX_INT = std::numeric_limits<int>::max();
	/* maximum size visible for Lua (must be representable in a lua_Integer */
	static constexpr size_t MAX_SIZE = (sizeof(size_t) < sizeof(lua_Integer) ? MAX_SIZET : (size_t)(LUA_MAXINTEGER));
	/*
	** conversion of pointer to unsigned integer:
	** this is for hashing only; there is no problem if the integer
	** cannot hold the whole pointer value
	*/
	template<typename T>
	uint32_t point2uint(const T* p) { return ((uint32_t)((size_t)(p)& std::numeric_limits<uint32_t>::max())); }
	constexpr static size_t STRCACHE_N = 53;
	constexpr static size_t STRCACHE_M = 2;
	/* type to ensure maximum alignment */
#if defined(LUAI_USER_ALIGNMENT_T)
	typedef LUAI_USER_ALIGNMENT_T L_Umaxalign;
#else
	typedef union {
		lua_Number n;
		double u;
		void *s;
		lua_Integer i;
		long l;
	} L_Umaxalign;
#endif
	// basic settings
	struct lua_State {};


	typedef int(*lua_CFunction) (lua_State *L);
	enum class LUATYPE : uint8_t {
		NONE = (uint8_t)(-1),
		NIL = 0,
		BOOLEAN = 1,
		LIGHTUSERDATA = 2,
		NUMBER = 3,
		STRING = 4,
		TABLE = 5,
		FUNCTION = 6,
		USERDATA = 7,
		THREAD = 8,
		_NUMTAGS = 9
	};

	//#define check_expr(e,o) (o)
#define check_exp(e,o) (assert((e)),(o))
	/*
	** some useful bit tricks
	*/
	template<typename A, typename B>
	constexpr static int lmod(A s, B size) { return check_exp((size&(size - 1)) == 0, static_cast<int>(s & (size - 1))); }
	template<typename A>
	constexpr static A twoto(A x) { (1 << (x)); }
	template<typename X, typename M>
	static inline void resetbits(X& x, M m) { x &= ~static_cast<X>(m); }
	template<typename X, typename M>
	static inline void setbits(X& x, M m) { x |= static_cast<X>(m); }
	template<typename X, typename M>
	constexpr static inline bool testbits(X x, M m) { return (x & m) != 0; }
	template<typename B>
	constexpr static inline B bitmask(B b) { return(1 << (b)); }
	template<typename B>
	constexpr static inline B bit2mask(B b) { return bitmask(b); }
	template<typename B, typename ... Args>
	constexpr static inline B bit2mask(B b, Args&& ... args) { return bit2mask(b) | bit2mask(std::forward<Args>(args)...); }

	template<typename B1, typename B2>
	constexpr static inline B1 bit2mask(B1 b1, B2 b2) { return bitmask(b1) | bitmask(b2); }
	template<typename X, typename B>
	static inline void l_setbit(X& x, B b) { setbits(x, bitmask(b)); }
	template<typename X, typename B>
	static inline void resetbit(X& x, B b) { resetbit(x, bitmask(b)); }
	template<typename X, typename B>
	constexpr static inline bool testbit(X x, B b) { return testbits(x, bitmask(b)); }


	struct GCObject;
	struct Table;
	struct TString;

	struct LightUserData {
		void* _data;
	public:
		LightUserData(void* data) : _data(data) {}
		void* get() const { return _data; }
	};

	namespace cvt {

		// from wiki https://en.wikipedia.org/wiki/Base64
		struct Base64 {


			constexpr double fun(double x) { return x * x; }
			static constexpr char _base64set[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
			static constexpr char base64set[] =
				"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
				"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
				"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
				"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
			// I wonder how slow this in compiling?
			static constexpr uint8_t index_of_base64_set(size_t x) { 
				for (size_t i = 0; i < sizeof(_base64set); i++)
					if (static_cast<uint8_t>(_base64set[i]) == x) return i;
				return sizeof(_base64set);
			
			}
			

			// so we dont have ot 077 & in the set
			// repeating the base64 characters in the index array avoids the  077 &  operations
			static inline size_t to_base64(const uint8_t* data, char* chars,size_t size) {
				if (size > 0) {
					chars[0] = base64set[data[0] >> 2];
					if (size > 2) chars[1] = base64set[data[0] << 4 | data[1] >> 4];
					chars[3] = base64set[data[2]];

				}
				if (size > 3) chars[3] = base64set[data[2]]; else chars[3] = '=';
				if (size > 2) chars[3] = base64set[data[2]];
				if (size > 1) chars[3] = base64set[data[2]];
				
			}
			static inline std::string encode_base64str(const uint8_t* data, size_t size) {
				std::string out;
				out.reserve((size * 4) / 3);// roughly how big it will be
				while (size > 3) {
					out.push_back(base64set[data[0] >> 2]);
					out.push_back(base64set[data[0] << 4 | data[1] >> 4]);
					out.push_back(base64set[data[1] << 2 | data[2] >> 6]);
					out.push_back(base64set[data[2]]);
					size -= 3;
					data += 3;
				}
				if (size > 0) {
					out.push_back(base64set[data[0] >> 2]);
					out.push_back(size > 1 ? base64set[data[0] << 4 | data[1] >> 4] : '=');
					out.push_back(size > 2 ? base64set[data[1] << 2 | data[2] >> 6] : '=');
					out.push_back('=');
				}
				return out;
			}
			static inline void _decode_base64vec(std::vector<uint8_t>& out, uint8_t(&b)[4]) {
				out.push_back(((b[0] << 2) | (b[1] >> 4)));
				if (b[2] < 64) {
					out.push_back(((b[1] << 4) | (b[2] >> 2)));
					if (b[3] < 64) out.push_back((b[2] << 6) | b[3]);
				}
			}
			static std::vector<uint8_t> decode_base64vec(const char* str, size_t size) {
				static std::array<uint8_t, 256> base64index = util::make_array<256>(&index_of_base64_set);
				assert(size % 4 == 0); // have to be alligned
				std::vector<uint8_t> out;
				out.reserve((size * 3 / 4) + 4);
				uint8_t b[4];
				while (size > 4) {
					for (size_t i = 0; i < 4; i++) {
						if ((b[0] = base64index[str[i]]) == sizeof(_base64set)) 
							throw std::exception(); // bad charater
					}
					_decode_base64vec(out, b);
					size -= 4;
					str += 4;
				}
				return out;
			}

		};
		// used the example https://gcc.gnu.org/ml/gcc-patches/2017-04/msg00364.html for conversion routines
		// it be nice if from_chars comes out for vc or gcc, but its going to be a while for it
		namespace priv {
		//	template<typename T, typename E = void>
		//	struct number_format { static constexpr bool has_format = false; };
			template<typename T, typename = std::enable_if<std::is_floating_point<T>::value>>
			struct number_format {
				using type = T;
				static constexpr const char* fmt =
					std::is_same<signed_type, float>::value ? "%.7g" :
					std::is_same<signed_type, double>::value ? "%.14g" :
					std::is_same<signed_type, long double>::value ? "%.19Lg" : nullptr;
				static constexpr size_t buffer_size = std::numeric_limits<long double>::digits10 + 2; // the dot and 0
				static constexpr bool has_format = fmt != nullptr;
			};
			template<typename T, typename = std::enable_if<std::is_integral<T>::value>>
			struct number_format {
				using type = T;
				static constexpr const char* fmt =
					std::is_same<signed_type, int>::value ? "%d" :
					std::is_same<signed_type, long>::value ? "%ld" :
					std::is_same<signed_type, long long>::value ? "%lld" : nullptr;
				static constexpr size_t buffer_size = std::numeric_limits<T>::digits10 + 2;
				static constexpr bool has_format = fmt != nullptr;
			};
			struct from_chars_result {
				const char* ptr;
				::std::error_code ec;
			};
			struct to_chars_result {
				char* ptr;
				::std::error_code ec;
			};
			template<typename _Tp>
			to_chars_result __to_chars(char* __first, char* __last, _Tp __val, int __base=10) {
				using traits = number_format<_Tp>;
				assert(_base == 10); // only using base 10 for now
				to_chars_result __res;
				//const unsigned __len = __to_chars_len(__val, __base);
				const unsigned __len = static_cast<int>(_last - _first) > 0 ? static_cast<unsigned>(_last - _first) : 0;
				if (traits::buffer_size > __len) { // buffer isn't big enough
					__res.ptr = __last;
					__res.ec = std::make_error_code(std::errc::value_too_large);
					return __res;
				}
				// slower than doing manualy, but I don't have to worry about it
				int __plen = ::snprintf(__first, __len, traits::fmt, from);
				assert(__plen > 0);
				__res.ptr = __first + __plen;
				return __res;
			}

			// https://news.ycombinator.com/item?id=8749154
			template<typename _Tp, typename = std::enable_if<std::is_signed<_Tp>::value>>
			static constexpr bool int_add_safe(const _Tp a, const _Tp b) {
				return b < _Tp{} ? a >= (std::numeric_limits<_Tp>::min() - b) : a <= (std::numeric_limits<_Tp>::max() - b);
			}

			template<typename _Tp>
			bool __from_chars_integral_base_10(const char* __first, const char* __last, _Tp& __val) {
				

			}
			template<typename _Tp, typename = std::enable_if<std::is_integral<_Tp>::value> && std::is_unsigned<_Tp>::value>>
				to_chars_result __from_chars(const char* __first, const char* __last, _Tp& __val, int __base = 10) {
				to_chars_result __res;
				const char* __start = __first
				while (__first != __last) {
					unsigned char __c = *__first;
					if (std::isdigit(__c))
						__c -= '0';
					else
					{
						constexpr char __abc[] = "abcdefghijklmnopqrstuvwxyz";
						unsigned char __lc = std::tolower(__c);
						// Characters 'a'..'z' are consecutive
						if (std::isalpha(__c) && (__lc - 'a') < __b)
							__c = __lc - 'a' + 10;
						else
							break;
					}

					if (__builtin_expect(__valid, 1))
						__valid = __raise_and_add(__val, __base, __c);
					__first++;
				}


			}
#if 0
#define MAXBY10		cast(lua_Unsigned, LUA_MAXINTEGER / 10)
#define MAXLASTD	cast_int(LUA_MAXINTEGER % 10)

			static const char *l_str2int(const char *s, lua_Integer *result) {
				lua_Unsigned a = 0;
				int empty = 1;
				int neg;
				
				neg = isneg(&s);
				if (s[0] == '0' &&
					(s[1] == 'x' || s[1] == 'X')) {  /* hex? */
					s += 2;  /* skip '0x' */
					for (; lisxdigit(cast_uchar(*s)); s++) {
						a = a * 16 + luaO_hexavalue(*s);
						empty = 0;
					}
				}
				else {  /* decimal */
					for (; lisdigit(cast_uchar(*s)); s++) {
						int d = *s - '0';
						if (a >= MAXBY10 && (a > MAXBY10 || d > MAXLASTD + neg))  /* overflow? */
							return NULL;  /* do not accept it (as integer) */
						a = a * 10 + d;
						empty = 0;
					}
				}
				while (lisspace(cast_uchar(*s))) s++;  /* skip trailing spaces */
				if (empty || *s != '\0') return NULL;  /* something wrong in the numeral */
				else {
					*result = l_castU2S((neg) ? 0u - a : a);
					return s;
				}
#endif
			}
			template<typename_Tp, typename = std::enable_if<std::is_floating_point<_Tp>::value>> 
			from_chars_result __from_chars(const char* __first, const char* __last, _Tp& __val, int __base=10) {
				from_chars_result __res;
				assert(_base == 10); // only using base 10 for now
				int dot = lua_getlocaledecpoint();
				lua_Number r = 0.0;  /* result (accumulator) */
				int sigdig = 0;  /* number of significant digits */
				int nosigdig = 0;  /* number of non-significant digits */
				int e = 0;  /* exponent correction */
				int neg;  /* 1 if number is negative */
				int hasdot = 0;  /* true after seen a dot */
				*endptr = cast(char *, s);  /* nothing is valid yet */
				while (lisspace(cast_uchar(*s))) s++;  /* skip initial spaces */
				neg = isneg(&s);  /* check signal */
				if (!(*s == '0' && (*(s + 1) == 'x' || *(s + 1) == 'X')))  /* check '0x' */
					return 0.0;  /* invalid format (no '0x') */
				for (s += 2; ; s++) {  /* skip '0x' and read numeral */
					if (*s == dot) {
						if (hasdot) break;  /* second dot? stop loop */
						else hasdot = 1;
					}
					else if (lisxdigit(cast_uchar(*s))) {
						if (sigdig == 0 && *s == '0')  /* non-significant digit (zero)? */
							nosigdig++;
						else if (++sigdig <= MAXSIGDIG)  /* can read it without overflow? */
							r = (r * cast_num(16.0)) + luaO_hexavalue(*s);
						else e++; /* too many digits; ignore, but still count for exponent */
						if (hasdot) e--;  /* decimal digit? correct exponent */
					}
					else break;  /* neither a dot nor a digit */
				}
				if (nosigdig + sigdig == 0)  /* no digits? */
					return 0.0;  /* invalid format */
				*endptr = cast(char *, s);  /* valid up to here */
				e *= 4;  /* each digit multiplies/divides value by 2^4 */
				if (*s == 'p' || *s == 'P') {  /* exponent part? */
					int exp1 = 0;  /* exponent value */
					int neg1;  /* exponent signal */
					s++;  /* skip 'p' */
					neg1 = isneg(&s);  /* signal */
					if (!lisdigit(cast_uchar(*s)))
						return 0.0;  /* invalid; must have at least one digit */
					while (lisdigit(cast_uchar(*s)))  /* read exponent */
						exp1 = exp1 * 10 + *(s++) - '0';
					if (neg1) exp1 = -exp1;
					e += exp1;
					*endptr = cast(char *, s);  /* valid up to here */
				}
				if (neg) r = -r;
				return l_mathop(ldexp)(r, e);
			}

		}
	
		template<typename FROM, typename TO, typename = std::enable_if<std::is_integral<TO>::value && std::is_floating_point<FROM>::value>>
		static inline bool convert(TO& to, const FROM& from) {
			if (from >= std::numeric_limits<TO>::min() && from < std::numeric_limits<TO>::max()) {
				to = static_cast<TO>(from);
				return true;
			

			}
			return false;
		}
		template<typename FROM, typename TO, typename = std::enable_if<std::is_floating_point<TO>::value && std::is_integral<FROM>::value>
		static inline bool convert(TO& to, const FROM& from) {
			if (from >= std::numeric_limits<TO>::min() && from < std::numeric_limits<TO>::max()) {
				to = static_cast<TO>(from);
				return true;
				std::from_chars
			}
			return false;
		}
		template<typename FROM, typename TO, typename = std::enable_if<std::is_constructible<TO, const char*>::value && std::number_format<FROM>::has_format>>
		static inline bool convert(TO& to, const FROM& from) {
			using traits = number_format<FROM>;
			char buffer[traits::buffer_size];
			int len = ::snprintf(buffer, traits::buffer_size - 1, traits::fmt, from);
			assert(len >= 0 && len <= traits::buffer_size);
			to = TO(buffer);
			return true;
		}
		template<typename FROM, typename TO, typename = std::enable_if<std::number_format<FROM>::has_format> && <std::is_convertible<TO, const char*>::value>>
		static inline bool convert(TO& to, const FROM& from) {
			const char* str = from;
			using traits = number_format<FROM>;
			char buffer[traits::buffer_size];
			int len = ::snprintf(buffer, traits::buffer_size - 1, traits::fmt, from);
			assert(len >= 0 && len <= traits::buffer_size);
			to = TO(buffer);
			return true;
		}

	}
	struct Number {
		using number_type = std::variant<lua_Number, lua_Integer>;
		number_type _value;
	public:
		template<typename T, typename = std::enable_if<std::is_floating_point<T>::value>>
		Number(T v) : _value(static_cast<lua_Number>(v)) {}
		template<typename T, typename = std::enable_if<!std::is_integral<T>::value>>
		Number(T v) : _value(static_cast<lua_Integer>(v)) {}
		template<typename T>
		Number& operator=(T v) { *this = Number(v); return *this; }
		bool is_integer() const { return std::holds_alternative<lua_Integer>(_value); }
		bool is_float() const { return std::holds_alternative<lua_Number>(_value); }
		// see the soure code on line 96 ltable.c 5.3.5 in lua for more information on the floating point hash
		// bit
		size_t hash() const {
			if (is_integer()) return std::get<lua_Integer>(_value);
			else {
#define lua_numbertointeger(n,p) \

			}
		}

	};
	using Value = std::variant<nullptr_t, bool, LightUserData, lua_Integer, lua_Number, lua_CFunction, Table*, TString*>;
	static constexpr size_t LUA_NUMTAGS = static_cast<size_t>(LUATYPE::_NUMTAGS);
	enum class LUATAGS : uint8_t {
		NONE			= static_cast<uint8_t>(LUATYPE::NONE),
		NIL				= static_cast<uint8_t>(LUATYPE::NIL),
		BOOLEAN			= static_cast<uint8_t>(LUATYPE::BOOLEAN),
		LIGHTUSERDATA	= static_cast<uint8_t>(LUATYPE::LIGHTUSERDATA),
		NUMBER			= static_cast<uint8_t>(LUATYPE::NUMBER),
		STRING			= static_cast<uint8_t>(LUATYPE::STRING),
		TABLE			= static_cast<uint8_t>(LUATYPE::TABLE),
		FUNCTION		= static_cast<uint8_t>(LUATYPE::FUNCTION),
		USERDATA		= static_cast<uint8_t>(LUATYPE::USERDATA),
		THREAD			= static_cast<uint8_t>(LUATYPE::THREAD),
		PROTO			= static_cast<uint8_t>(LUATYPE::_NUMTAGS),/* function prototypes */
		DEADKEY			, /* removed keys in tables */
		_TOTALTAGS		,
		/*
		** tags for Tagged Values have the following use of bits:
		** bits 0-3: actual tag (a LUA_T* value)
		** bits 4-5: variant bits
		** bit 6: whether value is collectable
		*/

		/*
		** LUA_TFUNCTION variants:
		** 0 - Lua function
		** 1 - light C function
		** 2 - regular C function (closure)
		*/
		/* Variant tags for functions */
		LCL				= (static_cast<uint8_t>(LUATYPE::FUNCTION) | (0 << 4)),  /* Lua closure */
		LCF				= (static_cast<uint8_t>(LUATYPE::FUNCTION) | (1 << 4)),  /* light C function */
		CCL				= (static_cast<uint8_t>(LUATYPE::FUNCTION) | (2 << 4)),  /* C closure */
		/* Variant tags for strings */
		SHRSTR			= (static_cast<uint8_t>(LUATYPE::STRING) | (0 << 4)),  /* short strings */
		LNGSTR			= (static_cast<uint8_t>(LUATYPE::STRING) | (1 << 4)),  /* long strings */
		/* Variant tags for numbers */
		NUMFLT			= (static_cast<uint8_t>(LUATYPE::NUMBER) | (0 << 4)),  /* float numbers */
		NUMINT			= (static_cast<uint8_t>(LUATYPE::NUMBER) | (1 << 4)),  /* integer numbers */
	};
	static constexpr size_t LUA_TOTALTAGS = static_cast<size_t>(LUATAGS::_TOTALTAGS);
	using LUATAGS_INTTYPE = std::underlying_type_t<LUATAGS>;
	/* Bit mark for collectable types */
	static constexpr LUATAGS_INTTYPE BIT_ISCOLLECTABLE = (1 << 6);
	/* mark a tag as collectable */
	static inline LUATAGS ctb(LUATAGS t) { return static_cast<LUATAGS>(static_cast<LUATAGS_INTTYPE>(t) | BIT_ISCOLLECTABLE); }
	struct stringtable {
		std::vector<TString *> hash; /* number of elements */
		int nuse;  
		size_t size() const { return hash.size(); }
	} ;
	typedef void(*lua_Alloc)();

	struct global_State {
		lua_Alloc frealloc;  /* function to reallocate memory */
		void *ud;         /* auxiliary data to 'frealloc' */
		l_mem totalbytes;  /* number of bytes currently allocated - GCdebt */
		l_mem GCdebt;  /* bytes allocated not yet compensated by the collector */
		lu_mem GCmemtrav;  /* memory traversed by the GC */
		lu_mem GCestimate;  /* an estimate of the non-garbage memory in use */
		stringtable strt;  /* hash table for strings */
		TValue l_registry;
		unsigned int seed;  /* randomized seed for hashes */
		lu_byte currentwhite;
		lu_byte gcstate;  /* state of garbage collector */
		lu_byte gckind;  /* kind of GC running */
		lu_byte gcrunning;  /* true if GC is running */
		GCObject *allgc;  /* list of all collectable objects */
		GCObject **sweepgc;  /* current position of sweep in list */
		GCObject *finobj;  /* list of collectable objects with finalizers */
		GCObject *gray;  /* list of gray objects */
		GCObject *grayagain;  /* list of objects to be traversed atomically */
		GCObject *weak;  /* list of tables with weak values */
		GCObject *ephemeron;  /* list of ephemeron tables (weak keys) */
		GCObject *allweak;  /* list of all-weak tables */
		GCObject *tobefnz;  /* list of userdata to be GC */
		GCObject *fixedgc;  /* list of objects not to be collected */
		struct lua_State *twups;  /* list of threads with open upvalues */
		unsigned int gcfinnum;  /* number of finalizers to call in each GC step */
		int gcpause;  /* size of pause between successive GCs */
		int gcstepmul;  /* GC 'granularity' */
		lua_CFunction panic;  /* to be called in unprotected errors */
		struct lua_State *mainthread;
		const lua_Number *version;  /* pointer to version number */
		TString *memerrmsg;  /* memory-error message */
	//	TString *tmname[TM_N];  /* array with tag-method names */
		Table *mt[LUA_NUMTAGS];  /* metatables for basic types */
		TString *strcache[STRCACHE_N][STRCACHE_M];  /* cache for strings in API */
		std::mutex mutex; // global mutex
		static global_State& G() {
			static global_State _G;
			return _G;
		}
	};
	using G = global_State;
	//number of all possible tags (including LUA_TNONE but excluding DEADKEY)
	
	// basic types 





	template<typename T>
	static inline checktype(const Value& v) { return std::holds_alternative<T>(v); }
#define checktag(o,t)		(rttype(o) == (t))
#define checktype(o,t)		(ttnov(o) == (t))
#define ttisnumber(o)		checktype((o), LUA_TNUMBER)
#define ttisfloat(o)		checktag((o), LUA_TNUMFLT)
#define ttisinteger(o)		checktag((o), LUA_TNUMINT)
#define ttisnil(o)		checktag((o), LUA_TNIL)
#define ttisboolean(o)		checktag((o), LUA_TBOOLEAN)
#define ttislightuserdata(o)	checktag((o), LUA_TLIGHTUSERDATA)
#define ttisstring(o)		checktype((o), LUA_TSTRING)
#define ttisshrstring(o)	checktag((o), ctb(LUA_TSHRSTR))
#define ttislngstring(o)	checktag((o), ctb(LUA_TLNGSTR))
#define ttistable(o)		checktag((o), ctb(LUA_TTABLE))
	struct lua_TValue  {
		Value value_;
		lua_TValue() : value_{ nullptr } {}
		template<typename T>
		lua_TValue(T&& v) : value_{ std::forward<T>(v) } {}
		bool is_float() const { return std::holds_alternative<lua_Number>(value_); }
		bool is_integer() const { return std::holds_alternative<lua_Integer>(value_); }
		bool is_number() const { return is_float() || is_integer(); }
		bool is_bool() const { return std::holds_alternative<bool>(value_); }
		bool is_table() const { return std::holds_alternative<Table*>(value_); }
		bool is_string() const { return std::holds_alternative<TString*>(value_); }
		bool iscollectable() const { return is_table() || is_string(); }  
	};
	using TValue = lua_TValue;

	struct GCObject {
		GCObject* next;
		uint8_t marked;
		/* Layout for bit use in 'marked' field: */
		static constexpr uint8_t WHITE0BIT = 0;  /* object is white (type 0) */
		static constexpr uint8_t WHITE1BIT = 1;  /* object is white (type 0) */
		static constexpr uint8_t BLACKBIT = 2;  /* object is white (type 0) */
		static constexpr uint8_t FINALIZEDBIT = 3;  /* object is white (type 0) */
		static constexpr uint8_t WHITEBITS = 3;  /* object is white (type 0) */
												 /* bit 7 is currently used by tests (luaL_checkmemory) */
		static constexpr uint8_t WHITEBITS = bit2mask(WHITE0BIT, WHITE1BIT);
		static constexpr uint8_t maskcolors = (~(bitmask(BLACKBIT) | WHITEBITS));
		constexpr static uint8_t otherwhite(uint8_t currentwhite) { return currentwhite ^ WHITEBITS; }
		constexpr static bool isdeadm(uint8_t ow,uint8_t marked) { return (!(((marked) ^ WHITEBITS) & (ow))); }
		constexpr static uint8_t luaC_white(uint8_t currentwhite) {
			return currentwhite & WHITEBITS;
		}
		bool iswhite() const { return testbits(marked, WHITEBITS); }
		bool isblack() const { return testbits(marked, BLACKBIT); }
		/* neither white nor black */  
		bool isgray() const { return !testbits(marked, WHITEBITS | bitmask(BLACKBIT)); }
		bool tofinalize() const { return testbits(marked, FINALIZEDBIT); }
		
		bool isgray() const { return !testbits(marked, WHITEBITS | bitmask(BLACKBIT)); }
		bool isdead() const { return isdeadm(otherwhite(G::G().currentwhite), marked); }
		void changewhite() { marked ^= WHITEBITS;  }
		void gray2black() { l_setbit(marked, BLACKBIT); }
		/*
		** 'makewhite' erases all color bits then sets only the current white
		** bit
		*/
		bool valiswhite()  const { return iswhite();}
		void makewhite() { marked = marked & maskcolors | G::G().currentwhite; }
		void white2gray() { resetbits(marked, WHITEBITS); }
		void black2gray() { resetbits(marked, BLACKBIT); }


		void fix(lua_State *L) {
			global_State& g = G::G();
			assert(g.allgc == this);  /* object must be 1st in 'allgc' list! */
			white2gray();  /* they will be gray forever */
			g.allgc = this->next;  /* remove object from 'allgc' list */
			this->next = g.fixedgc;  /* link it to 'fixedgc' list */
			g.fixedgc = this;
		}


		static void* operator new(size_t size) {
			if (size < sizeof(L_Umaxalign)) size = sizeof(L_Umaxalign);
			return ::operator new(size);
		}
		template<typename T, typename = std::enable_if<std::is_base_of<GCObject, std::decay_t<T>>::value>>
		static T* create(size_t size = sizeof(T)) {
			if (size < sizeof(L_Umaxalign)) size = sizeof(L_Umaxalign);
			char* ptr = new char[size];
			if (sizeof(T) < sizeof(L_Umaxalign)) size = sizeof(L_Umaxalign);
			return new T();
		}
	private:
		GCObject() : next(nullptr), marked(0) {}
	protected:
		template<typename T>
		constexpr static size_t get_size(T = T{}) { return sizeof(T) < sizeof(L_Umaxalign) ? sizeof(L_Umaxalign) : sizeof(T); }
	};
	/*
	** Tables
	*/

	union TKey {
		struct {
			//TValuefields;
			// dont understand this?
			TValue tv;
			int next;  /* for chaining (offset for next node) */
		} nk;
		TValue tvk;
	};


	/* copy a value into a key without messing up field 'next' */
#define setnodekey(L,key,obj) \
	{ TKey *k_=(key); const TValue *io_=(obj); \
	  k_->nk.value_ = io_->value_; k_->nk.tt_ = io_->tt_; \
	  (void)L; checkliveness(L,io_); }


	struct Node {
		TValue i_val;
		TKey i_key;
	} ;

	/*
	** 'module' operation for hashing (size is always a power of 2)
	*/

	struct Table : public GCObject {
		lu_byte flags;  /* 1<<p means tagmethod(p) is not present */
		lu_byte lsizenode;  /* log2 of size of 'node' array */
		unsigned int sizearray;  /* size of 'array' array */
		std::vector<TValue> array;  /* array part */
		Node *node;
		Node *lastfree;  /* any free position is before this position */
		Table *metatable;
		GCObject *gclist;
		size_t sizenode() const { return twoto(lsizenode); }
	};
	struct TString : public GCObject {
		lu_byte extra;  /* reserved words for short strings; "has hash" for longs */
		lu_byte shrlen;  /* length for short strings */
		unsigned int hash;
		union {
			size_t lnglen;  /* length for long strings */
			TString *hnext;  /* linked list for hash table */
		} u;
		char *getstr() {
			return reinterpret_cast<char*>(this) + get_size<TString>();
		}
	};
	struct TString;









	/*
	** Header for string value; string bytes follow the end of this structure
	** (aligned according to 'UTString'; see next).
	*/




		/* get the actual string (array of bytes) from a Lua value */
#define svalue(o)       getstr(tsvalue(o))

		/* get string length from 'TString *s' */
#define tsslen(s)	((s)->tt == LUA_TSHRSTR ? (s)->shrlen : (s)->u.lnglen)

		/* get string length from 'TValue *o' */
#define vslen(o)	tsslen(tsvalue(o))
	/*
	** Ensures that address after this type is always fully aligned.
	*/
	union UTString {
		L_Umaxalign dummy;  /* ensures maximum alignment for strings */
		TString tsv;
	} ;

	struct lua_TValue : public lua_TTag<int> {
		Value value_; 
		lua_TValue() : value_{ nullptr }, tt_(LUATAGS::NIL) {}
		/* Macros to access values */
auto& ivalue() { return	check_exp(isinteger(), value_.i); }
auto& fltvalue() { return	check_exp(isfloat(), value_.n); }
auto& nvalue() { return	check_exp(isnumber(), (isinteger() ? static_cast<lua_Number>(ivalue(o)) : fltvalue(o))); }
auto& gcvalue() { return	check_exp(iscollectable(o), value_.gc); }
auto& pvalue() { return	check_exp(islightuserdata(), value_.p); }

auto& fvalue() { return	check_exp(islcf(), value_.f); }

auto& tsvalue() { return	check_exp(isstring(), gco2ts(value_.gc)); }
auto& uvalue() { return	check_exp(isfulluserdata(), gco2u(value_.gc)); }
auto& clvalue() { return	check_exp(isclosure(), gco2cl(value_.gc)); }
auto& clLvalue() { return	check_exp(isLclosure(), gco2lcl(value_.gc)); }
auto& clCvalue() { return	check_exp(isCclosure(), gco2ccl(value_.gc)); }
auto& hvalue() { return	check_exp(istable(), gco2t(value_.gc)); }
auto& bvalue() { return	check_exp(isboolean(), value_.b); }
auto& thvalue() { return	check_exp(isthread(), gco2th(value_.gc)); }
		/* a dead value may get the 'gc' field, but cannot access its contents */
auto& deadvalue() { return	check_exp(isdeadkey(), cast(void *, value_.gc)); }

auto& l_isfalse() { return	(isnil() || (isboolean() && bvalue(o) == 0)); }



		LUATAGS_INTTYPE novariant() const { }
	};
	using TValue = lua_TValue;
	// ah the magic of templates
	// should change these though
	/* macro defining a nil value */
};

namespace util {
	// copyied from flyweight
	namespace impl {


		template <typename T>
		struct default_extractor {
			constexpr default_extractor() noexcept { }
			T const& operator () (T const& argument) const noexcept { return argument; }
		};

		template <typename T>
		using const_ref = std::add_lvalue_reference_t<std::add_const_t<T>>;

		template<typename T>
		struct hash_function_call {
			size_t operator()(const T& obj) const { return obj.hash(); }
			size_t operator()(const T* obj) const { return obj->hash(); }
		};


		template <class T, typename KeyExtractor = default_extractor<T>, typename Allocator = std::allocator<T>>
		struct container_traits final {
			using allocator_type = typename std::allocator_traits<Allocator>::template rebind_alloc<T>;
			using extractor_type = KeyExtractor;
			using computed_key_type = std::decay_t<decltype(std::declval<const_ref<extractor_type>>()(std::declval<const_ref<T>>()))>;

			template<typename C>
			using hash_function = std::conditional_t<priv::has_hash_member<C>::value, hash_function_call, std::hash<C>>;

			using is_associative = std::integral_constant<bool,(!std::is_same<std::decay_t<T>,std::decay_t<computed_key_type>>::value)>;

			using mapped_weak_type = std::weak_ptr<std::add_const_t<T>>;
			using mapped_unique_type = std::unique_ptr<T>;
			using key_type = std::conditional_t<
				((sizeof(computed_key_type) <= sizeof(std::reference_wrapper<computed_key_type>)) || !is_associative::value),
				computed_key_type,std::reference_wrapper<std::add_const_t<computed_key_type>>
			>;

			using container_unique_type = std::unordered_map<
				key_type,
				mapped_unique_type,
				hash_function<computed_key_type>,
				std::equal_to<computed_key_type>,
				typename std::allocator_traits<Allocator>::template rebind_alloc<
					std::pair<std::add_const_t<key_type>, mapped_unique_type>
				>
			>;
			using container_weak_type = std::unordered_map<
				key_type,
				mapped_weak_type,
				hash_function<computed_key_type>,
				std::equal_to<computed_key_type>,
				typename std::allocator_traits<Allocator>::template rebind_alloc<
					std::pair<std::add_const_t<key_type>, mapped_weak_type>
				>
			>;
		};
	}; /* namespace impl */
	   // self tracking object
	template<typename T>
	class class_tracking {
	protected:
		using value_type = T;
		using type = class_tracking<T>;
		using pointer = std::add_pointer_t<T>;
		std::mutex mutex;
		std::unordered_set<pointer> objects;
	public:
		void emplace(pointer ptr) { std::lock_guard<std::mutex> lock(mutex); objects.emplace(ptr); }
		void erase(pointer ptr) { std::lock_guard<std::mutex> lock(mutex); objects.erase(ptr); }
		template<typename F>
		void for_each(F func) {
			std::lock_guard<std::mutex> lock(mutex);
			for (auto o : objects) func(o);
		}
	};
	// I am SOOO liking C++17
	// using std::string_view!
	class symboltable {
	public:

		using string_view = std::string_view;
		class isymbol {
			constexpr isymbol(const isymbol& copy) : _str(copy.str), _hash(copy._hash), mark(0) {}
			constexpr isymbol(isymbol&& move) : _str(move.str), _hash(move._hash), mark(0) {}
			isymbol& operator=(const isymbol& copy) = default;
			isymbol& operator=(isymbol&& move) = default;
			static std::hash<std::string_view> _hasher;
			std::atomic<int> mark;
			size_t _hash; // hash is cached, but is it that important?
			string_view _str;
			friend symboltable;
		public:
			friend class symbol;
			constexpr isymbol(const char* str, size_t length) : _str(str, length), _hash(_hasher(_str)), mark(0) {}
			constexpr isymbol(const char* str, size_t length, size_t hash) : _str(str, length), _hash(hash), mark(0) {}
			constexpr isymbol(const char* str) : _str(str), _hash(_hasher(_str)), mark(0) {}
			constexpr isymbol(const string_view& str) : _str(str), _hash(_hasher(_str)), mark(0) {}
			constexpr size_t size() const { return _str.size(); }
			constexpr size_t hash() const { return _hash; }
			void mark_use() { mark = 1; }
			constexpr const string_view& str() const { return _str; }
			static std::unique_ptr<isymbol> create(const isymbol& istr, bool const_string) {
				isymbol* ptr;
				if (const_string) {
					ptr = new isymbol(istr);
					ptr->mark = 2;
				}
				else {
					char* rptr = new char[istr.size() + sizeof(isymbol) + 1];
					char* nstr = rptr + sizeof(isymbol);
					std::copy(nstr, nstr + istr.size(), istr.str);
					ptr = new(rptr) isymbol(nstr, istr.size(),istr.hash());
				}
				return std::unique_ptr<isymbol>(ptr);
			}

			static const isymbol* empty_isymbol() { 
				static isymbol _empty("");
				return &_empty;
			}
			bool is_cstring() const {
				return _str.data() != reinterpret_cast<const char*>(this) + sizeof(isymbol);
			}
			bool is_perm() const {
				return is_cstring() || mark < 0;
			}
			constexpr bool operator==(const isymbol& r) const { return (_str.data() == r._str.data()) || (_str == r._str); }
		};
		
		// container meh
		struct isymbol_hasher {
			size_t operator()(const isymbol& s) const { return s.hash();  }
		};
#if 0
		using traits = impl::container_traits<isymbol, impl::default_extractor<isymbol>, std::allocator<isymbol>>;
		//	using key_type = std::reference_wrapper<string_type>;
		using container_type = typename traits::container_unique_type;
		using maped_type = typename traits::mapped_unique_type;
#else
		using container_key = std::reference_wrapper<std::add_const_t<isymbol>>;
		//using container_key = const isymbol&;
		using container_value = std::unique_ptr<isymbol>;
		// cause I gave up on std::string, on how fucking crazy the memory allocation is, can't use above
		using container_type = std::unordered_map<container_key, container_value, isymbol_hasher, isymbol_hasher>;
#endif
		symboltable() { }
		static bool is_empty(const isymbol* sym) { return sym == isymbol::empty_isymbol(); }
		const isymbol* find(const char* str,bool const_string=false) noexcept {
			if (str == nullptr || str[0] == 0) return isymbol::empty_isymbol();
			std::lock_guard<std::mutex> lock(_mutex);
			isymbol skey(str,strlen(str));
			container_key key(skey);
			auto iter = _table.find(key);
			if (iter != _table.end()) { 
				if (const_string && !iter->second->is_cstring()) 
					// we need to replace this with the new string, so erase it
					_table.erase(iter);
				else 
					return iter->second.get(); 
			}
			return insert(skey, const_string);
		}
		void remove(const isymbol* value) noexcept {
			if (!value) return;
			std::lock_guard<std::mutex> lock(_mutex);
			container_key key(*value);
			auto iter = _table.find(key);
			if (iter != _table.end() && iter->second.get() == value) {
				_table.erase(iter);
			}
		}
		void collect_marked() {
			std::lock_guard<std::mutex> lock(_mutex);
			for (auto it = _table.begin(); it != _table.end();) {
				isymbol* sym = const_cast<isymbol*>(it->second.get());
				if (sym->is_perm() || sym->mark.exchange(0) == 0) 
					it++; // ignore const strings
				else 
					it = _table.erase(it);
			}
		}
		template<typename FUNC>
		const isymbol* make_perm(const isymbol* value, FUNC func) {
			std::lock_guard<std::mutex> lock(_mutex);
			(void)func; // function not used right now
			if (value->is_perm()) return value;
			const_cast<isymbol*>(value)->mark = -1;
			//func()
			return value;
		}
		std::mutex& mutex() { return _mutex;  } // get the table mutex
	private:
		const isymbol* insert(const isymbol& value, bool const_string=false) noexcept {
			auto ptr = isymbol::create(value, const_string);
			container_key key(*ptr.get());
			auto result = _table.emplace(key, std::move(ptr));
			return result.first->second.get();
		}
		std::mutex _mutex;
		container_type _table;
		friend class symbol;
	};

	class symbol {
	public:
		using string_view = symboltable::string_view;
		using iterator = string_view::iterator;
		using const_iterator = string_view::const_iterator;
		using reverse_iterator = string_view::reverse_iterator;
		using const_reverse_iterator = string_view::const_reverse_iterator;
		template<typename ValueType> using is_value_type = std::is_same<std::decay_t<ValueType>, std::decay_t<value_type>>;
		template<typename ValueType> using is_symbol_type = std::is_same<std::decay_t<ValueType>, symbol>;
	private:
		const symboltable::isymbol* _symbol;
		using symbol_tracker = class_tracking<symbol>;

		inline static symboltable& table() {
			static symboltable _table;
			return _table;
		}
		inline static symbol_tracker& symbols() {
			static symbol_tracker _symbols;
			return _symbols;
		}
		void _assign(const symboltable::isymbol* sym) {
			if (sym != _symbol) {
				if (symboltable::is_empty(sym))  symbols().erase(this);
				if (symboltable::is_empty(_symbol))  symbols().emplace(this);
				_symbol = sym;
			}
		}
	public:
		bool empty() const { return symboltable::is_empty(_symbol); }
		void clear() {
			if (!empty()) symbols().erase(this);
			_symbol = symboltable::isymbol::empty_isymbol();
		}
		void make_perm() { 
			// be so much better if we used a **_symbol... ah something to think about latter
			std::lock_guard<std::mutex> lock(table().mutex());
			table().make_perm(_symbol, [](const symboltable::isymbol* osym, const symboltable::isymbol* nsym) {
				symbols().for_each([osym, nsym](symbol* s) {
					if (s->_symbol == osym) s->_symbol = nsym;
				});
			});
		}
		static void collect_garbage() {
			symbols().for_each([](symbol* o) { if (!o->empty() && o->_symbol->mark == 0) o->_symbol->mark = 1; });
			table().collect_marked();
		}
		symbol() : _symbol(symboltable::isymbol::empty_isymbol())) {}
		symbol(const symbol& copy) :_symbol(copy._symbol) { symbols().emplace(this); }
		symbol(symbol&& move) :_symbol(move._symbol) { symbols().emplace(this); move.clear(); }
		symbol& operator=(const symbol& copy) { _assign(copy._symbol); return *this; }
		symbol& operator=(symbol&& move) { _assign(move._symbol); move.clear(); return *this; }
		~symbol() { if (!empty()) clear(); }
		friend class symboltable;
		// saved for notes
#if 0
		template <typename ValueType, typename = std::enable_if_t<is_value_type<ValueType>::value> || std::is_constructible<value_type, ValueType>::value>
			explicit symbol(ValueType&& value) {
			_assign(std::forward<ValueType>(value));
		}
		template <typename... > struct typelist;

		template <typename... Args,
			typename = std::enable_if_t<
			!std::is_same<typelist<symbol>,
			typelist<std::decay_t<Args>...>>::value
			>>
	//	template <typename... Args, typename = std::enable_if<std::is_constructible<value_type, Args&&...>::value && !std::is_same<std::decay<Args>,symbol>::value>>
			symbol(Args&&... args) {
			_assign(value_type(std::forward<Args>(args)...));
		}
#endif
		// ok this is a hack to make string literals work

		const char x[] = "Hello, World!";
		template<size_t N>
		explicit symbol(const char (&clit)[N]) : _symbol(table().find(std::forward<T>(clit), true)) { assert(clit[N] == 0); }
		explicit symbol(const char* str) : _symbol(table().find(str, false)) {  }
		explicit symbol(const std::string& str) : _symbol(table().find(str.c_str(), false)) {  }

		symbol& operator=(const char* str) { _assign(table().find(str, false)); return *this; }
		void swap(symbol& right) {
			if (_symbol != right._symbol) {
				auto tmp = _symbol;
				_assign(right._symbol);
				right._assign(tmp);
			}
		}
		const string_view& strv() const { return _symbol->str(); }
		size_t size() const { return _symbol->size(); }
		operator std::string() const { return std::string(strv()); }
		operator const string_view&() const { return strv(); }
		
		const char* c_str() const { return _symbol->str().data(); }
		auto begin() const { return  strv().begin(); }
		auto end() const { return strv().end(); }
		auto rbegin() const { return  strv().rbegin(); }
		auto rend() const { return strv().rend(); }
		size_t hash() const { return _symbol->hash(); }
		bool operator==(const symbol& r) const { return _symbol == r._symbol; }
		bool operator==(const string_view& r) const { return _symbol->str().data() == r.data() || _symbol->str() == r; }
		bool operator==(const char* r) const { return *this == string_view(r); } // is this needed?
		bool operator==(const std::string& r) const { return _symbol->str() == r;  }
		template<typename T> bool operator!=(const T& r) const { return !(*this == r); } // hack to save code
	};
};

template<typename C, typename E>
static inline std::basic_ostream<C, E>& operator<<(std::basic_ostream<C, E>& os, const util::symbol& sym) {
	os << sym.str();
	return os;
};

namespace std {
	template<>
	struct hash<util::symbol> {
		size_t operator()(const util::symbol& sym) const { return sym.hash(); }
	};
};


namespace debug {
	
	void enable_windows10_vt100_support();
	class ostream {
		std::ostream _stream;
		bool _debug;
		std::string _name;
	public:
		// debug is true if we want to use the debug sieralizer
		ostream(const std::string& name, bool debug = false);
		ostream(bool debug = false) : ostream("", debug) {}
		virtual ~ostream();
		template<class T>
		void write(const T& obj) // -> decltype(write_check(obj, has_to_debug{}), void())
		{
			if (_debug)
				util::priv::debug_serialize(_stream, obj);
			else
				util::priv::serialize(_stream, obj);
		}
		std::ostream& stream() { return _stream; }
		const std::string& name() const { return _name; }
		const std::ostream& stream() const { return _stream; }

		template<class T>
		ostream& operator<<(T&& obj)  { write(obj); return *this; }
	

		ostream& operator<<(std::ostream& (__cdecl *func)(std::ostream&)) { _stream << func; return *this; }
		ostream& operator<<(std::ios& (__cdecl *func)(std::ios&)){ _stream << func; return *this;}
		ostream& operator<<(std::ios_base& (__cdecl *func)(std::ios_base&)) { _stream << func; return *this;}
		ostream& operator<<(ostream& (__cdecl *func)(ostream&)) { _stream << func; return *this;}
//		ostream& operator<<(ostream& (__cdecl *func)(ostream&)) { return func(*this); }
	};
	// wrapper for debug_stream, handles most of not all cases.  I could rewrite it but this template does the job
#if 0
	template<typename T>
	inline ostream& operator<<(ostream& os, T&& obj) {os.write(obj); return os;}
#endif
	//inline ostream& operator<<(ostream& os, std::ostream& (__cdecl *func)(std::ostream&)) { os.write(func); return os; }
	//inline ostream& operator<<(ostream& os, std::ios& (__cdecl *func)(std::ios&)) { os.write(func); return os; }
	//inline ostream& operator<<(ostream& os, std::ios_base& (__cdecl *func)(std::ios_base&)){ os.write(func); return os; }
	



	template<typename T>
	struct debug_ptr {
		const T* _ptr;
		size_t _size;
		size_t _offset;
		bool hex = false;
		debug_ptr(const uint8_t* ptr, size_t offset, size_t size) : _ptr(reinterpret_cast<const T*>(ptr + offset)), _offset(offset), _size(size) {}
		debug_ptr(const uint8_t* ptr, size_t size) : _ptr(reinterpret_cast<const T*>(ptr)), _offset(0), _size(size) {}
		template<typename TT>
		debug_ptr(const TT* ptr, size_t size) : _ptr(ptr), _offset(0), _size(size) {}

		std::string to_stream_ptr(size_t index) const {
			std::stringstream ss;
			ss << index << ':';
			if (_offset > 0) {
				size_t offset = _offset + index * sizeof(T);
				ext::offset(offset).to_stream(ss);
			}
			return ss.str();
		}
		
		void to_debug(std::ostream& os) const {
			for (size_t s = 0; s < _size; s++) {
				os << std::left << std::setfill(' ') << std::setw(20) << to_stream_ptr(s);
				os << " ";
				if (hex)
					os << "0x" << std::uppercase << std::setw(8) << std::setfill('0') << std::hex << _ptr[s];
				else
					os << _ptr[s];
				os << std::endl;
			}
		}
		std::string to_string() const {
			std::stringstream ss;
			to_debug(ss);
			return ss.str();
		}
	//	debug_stream& ostream operator<< (debug_stream& os) { to_debug(os); return os; }
	};
	extern ostream cerr;
	//extern std::ostream& cerr = std::cerr;
};



namespace util {
	

	// Great taken from here
	// http://zotu.blogspot.com/2010/01/creating-random-access-iterator.html
	template<typename TT>
	class PointerIterator {
	public:
		typedef std::random_access_iterator_tag iterator_category;
		typedef TT value_type;
		typedef TT* pointer;
		typedef TT& reference;
		typedef std::ptrdiff_t difference_type;
		PointerIterator(pointer vec) : _list(vec) {}
		PointerIterator() : _list(nullptr) {}
		template<typename T2> PointerIterator(const PointerIterator<T2>& r) : _list(&(*r)) {}
		template<typename T2> PointerIterator& operator=(const PointerIterator<T2>& r) { _list = &(*r); return *this; }
		PointerIterator& operator++() { ++_list; return *this; }
		PointerIterator& operator--() { --_list; return *this; }
		PointerIterator operator++(int) { return PointerIterator(_list++); }
		PointerIterator operator--(int) { return PointerIterator(_list--); }
		PointerIterator& operator+=(const difference_type& n) { _list += n; return *this; }
		PointerIterator& operator-=(const difference_type& n) { _list -= n; return *this; }
		PointerIterator operator+(const difference_type& n) const { return PointerIterator(pointer(_list + n)); }
		PointerIterator operator-(const difference_type& n) const { return PointerIterator(pointer(_list - n)); }
		reference operator*() const { return *_list; }
		pointer operator->() const { return _list; }
		reference operator[](const difference_type& n) const { return _list[n]; }
		template<typename T> friend bool operator==(const PointerIterator<T>& r1, const PointerIterator<T>& r2);
		template<typename T> friend bool operator!=(const PointerIterator<T>& r1, const PointerIterator<T>& r2);
		template<typename T> friend bool operator<(const PointerIterator<T>& r1, const PointerIterator<T>& r2);
		template<typename T> friend bool operator>(const PointerIterator<T>& r1, const PointerIterator<T>& r2);
		template<typename T> friend bool operator<=(const PointerIterator<T>& r1, const PointerIterator<T>& r2);
		template<typename T> friend bool operator>=(const PointerIterator<T>& r1, const PointerIterator<T>& r2);
		template<typename T> friend typename PointerIterator<T>::difference_type operator+(const PointerIterator<T>& r1, const PointerIterator<T>& r2);
		template<typename T> friend typename PointerIterator<T>::difference_type operator-(const PointerIterator<T>& r1, const PointerIterator<T>& r2);
	private:
		pointer _list;
	};
	template<typename T> bool operator==(const PointerIterator<T>& r1, const PointerIterator<T>& r2) { return (r1._list == r2._list); }
	template<typename T> bool operator!=(const PointerIterator<T>& r1, const PointerIterator<T>& r2) { return (r1._list != r2._list); }
	template<typename T> bool operator<(const PointerIterator<T>& r1, const PointerIterator<T>& r2) { return (r1._list < r2._list); }
	template<typename T> bool operator>(const PointerIterator<T>& r1, const PointerIterator<T>& r2) { return (r1._list > r2._list); }
	template<typename T> bool operator>=(const PointerIterator<T>& r1, const PointerIterator<T>& r2) { return (r1._list >= r2._list); }
	template<typename T> bool operator<=(const PointerIterator<T>& r1, const PointerIterator<T>& r2) { return (r1._list <= r2._list); }
	template<typename T>
	typename PointerIterator<T>::difference_type operator+(const PointerIterator<T>& r1, const PointerIterator<T>& r2) { return PointerIterator<T>(r1._list + r2._list); }
	template<typename T>
	typename PointerIterator<T>::difference_type operator-(const PointerIterator<T>& r1, const PointerIterator<T>& r2) { return PointerIterator<T>(r1._list - r2._list); }

	template <typename TT, typename = std::enable_if<(std::is_pod<TT>::value || std::is_arithmetic<TT>::value)>::type>
	class PointerArray
	{
		//static_assert((std::is_pod<TT>::value || std::is_arithmetic<TT>::value), "Pointer Array can only handle simple types");
	public:
		using difference_type = typename std::ptrdiff_t;
		using value_type = typename TT;
		using const_value_type = typename std::conditional<std::is_const<value_type>::value, value_type, const value_type>::type;
		using pointer = typename value_type*;
		using reference = typename value_type&;
		using const_pointer = typename std::conditional<std::is_const<value_type>::value, pointer, const pointer>::type;
		using const_reference = typename std::conditional<std::is_const<value_type>::value, reference, const reference>::type;

		using const_iterator = typename PointerIterator<const value_type>;
		using iterator = typename PointerIterator<value_type>;

		PointerArray() : _list(nullptr), _size(0) {  }
		PointerArray(pointer list, size_t size) : _list(list), _size(size) {  }
		const_iterator begin() const { return const_iterator(_list); }
		const_iterator end() const { return const_iterator(_list+_size); }
		//const_iterator begin() const { return const_iterator(_list); }
		//const_iterator end() const { return const_iterator(_list + _size); }
		size_t size() const { return _size; }
		const_reference at(size_t i) const { return _list[i]; }
		const_reference operator[](size_t i) const { return _list[i]; }
		const_pointer data() const { return _list; }
		template <typename T = TT, typename = std::enable_if<std::is_const<T>::value>::type>
		reference at(size_t i) { return _list[i]; }
		template <typename T = TT, typename = std::enable_if<std::is_const<T>::value>::type>
		reference operator[](size_t i) { return _list[i]; }
		template <typename T = TT, typename = std::enable_if<std::is_const<T>::value>::type>
		pointer data() { return _list; }
		void to_stream(std::ostream& os) const {
			os << "[ ";
			for (size_t s = 0; s < _size; s++) {
				if (s != 0) os << " ,";
				os << "0x" << std::uppercase << std::setw(8) << std::setfill('0') << std::hex << at(s);
			}
			os << " ]";
		}
		void to_debug(std::ostream& os) const {
			debug::debug_ptr<const_value_type> debug(_list, _size);
			debug.to_debug(os);
		}
	protected:
		pointer _list;
		size_t _size;
	};

	template<typename T_VALUE, typename T_ARRAY, typename = std::enable_if<priv::has_at_func<T_ARRAY>::value>>
	class GenericIterator {
	public:
		using iterator_category = typename std::random_access_iterator_tag;
		using difference_type = typename std::ptrdiff_t;
		using value_type = typename std::remove_const<T_VALUE>::type;
		using pointer = typename value_type*;
		using reference = typename value_type&;
		using const_pointer = typename const pointer;
		using const_reference = typename  const reference;

		GenericIterator(const T_ARRAY& vec, size_t pos) : _list(vec), _pos(pos) {}
		GenericIterator(const T_ARRAY& vec) : _list(vec), _pos(0) {}
		GenericIterator& operator++() { ++_pos; return *this; }
		GenericIterator& operator--() { --_pos; return *this; }
		GenericIterator operator++(int) { return GenericIterator(_list, _pos++); }
		GenericIterator operator--(int) { return GenericIterator(_list, _pos--); }
		reference operator*() const { return _vec.at(_pos); }
		pointer operator->() const { return &_list.at(_pos); }
		reference operator[](const difference_type& n) const { return _list.at(n); }
		template<typename T, typename A> friend bool operator==(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2);
		template<typename T, typename A> friend bool operator!=(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2);
		template<typename T, typename A> friend bool operator<(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2);
		template<typename T, typename A> friend bool operator>(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2);
		template<typename T, typename A> friend bool operator<=(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2);
		template<typename T, typename A>  friend bool operator>=(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2);
		template<typename T, typename A> friend typename GenericIterator<T, A>::difference_type operator+(const GenericIterator<T, T_ARRAY>& r1, const GenericIterator<T, A>& r2);
		template<typename T, typename A> friend typename GenericIterator<T, A>::difference_type operator-(const GenericIterator<T, T_ARRAY>& r1, const GenericIterator<T, A>& r2);
	protected:
		const T_ARRAY& _list;
		size_t _pos;
	};
	template<typename T, typename A> bool operator==(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2) { return (r1._pos == r2._pos); }
	template<typename T, typename A> bool operator!=(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2) { return (r1._pos != r2._pos); }
	template<typename T, typename A> bool operator<(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2) { return (r1._pos < r2._pos); }
	template<typename T, typename A> bool operator>(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2) { return (r1._pos > r2._pos); }
	template<typename T, typename A> bool operator>=(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2) { return (r1._pos >= r2._pos); }
	template<typename T, typename A> bool operator<=(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2) { return (r1._pos <= r2._pos); }
	template<typename T, typename A>
	typename GenericIterator<T, A>::difference_type operator+(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2) { return GenericIterator<T, A>(_list, r1._pos + r2._pos); }
	template<typename T, typename A>
	typename GenericIterator<T, A>::difference_type operator-(const GenericIterator<T, A>& r1, const GenericIterator<T, A>& r2) { return GenericIterator<T, A>(_list, r1._pos - r2._pos); }


	// Container for strings simple pointer strings.  Used for comparing and matching
	// carful though
	class Buffer {
		const std::vector<uint8_t>& _data;
		size_t _offset;
		size_t _len;
	public:
		typedef std::vector<uint8_t>::const_iterator const_iterator;
		Buffer(const std::vector<uint8_t>& data) : _data(data), _offset(0), _len(data.size()) {}
		Buffer(const std::vector<uint8_t>& data, size_t offset, size_t len) : _data(data), _offset(offset), _len(len) {}
		uint8_t operator[](size_t pos) const { return _data[pos + _offset]; }
		const uint8_t* data() const { return _data.data() + _offset; }
		const uint8_t* data_absolute(size_t offset) const { return _data.data() + offset; }
		const char* c_str() const { return reinterpret_cast<const char*>(_data.data() + _offset); }
		const char* c_str_absolute(size_t offset) const { return reinterpret_cast<const char*>(_data.data() + offset); }
		template<typename T, typename = std::enable_if<std::is_pod<T>::value>::type>
		const T* cast() const { return reinterpret_cast<const T*>(_data.data() + _offset); }
		template<typename T, typename = std::enable_if<std::is_pod<T>::value>::type>
		const T* cast_absolute(size_t offset) const { return reinterpret_cast<const T*>(_data.data() + offset); }
		template<typename T, typename = std::enable_if<(std::is_pod<T>::value || std::is_arithmetic<T>::value)>::type>
		size_t	read_absolute(const T*& data, size_t offset) const { data = reinterpret_cast<const T*>(_data.data() + offset); return offset + sizeof(T); }
		template<typename T, typename std::enable_if<std::is_pod<T>::value>::type>
		void read(const T*& data) { _offset = read_absolute(data, _offset); }
		template<typename T, typename A>
		typename std::enable_if<(std::is_pod<T>::value || std::is_arithmetic<T>::value), size_t>::type
			read_absolute(std::vector<T, A>& data, size_t offset) const {
			uint32_t size = *cast_absolute<uint32_t>(offset);
			offset += sizeof(uint32_t);
			if (size > 0) {
				data.resize(size);
				std::memcpy(data.data(), _data.data() + offset, sizeof(T)*size);
				offset += sizeof(T)*size;
			}
			return offset;
		}
		template<typename T, typename std::enable_if<std::is_pod<T>::value>::type>
		void read(std::vector<T>& data) { _offset = read_absolute(data, _offset); }
		std::vector<uint32_t> read_offset_list(size_t offset) const {
			std::vector<uint32_t> list;
			read_absolute(list, offset);
			return list;
		}
		size_t size() const { return _len; }
		size_t offset() const { return _offset; }
		const std::vector<uint8_t>& vector() const { return _data; }
		const_iterator begin() const { return _data.begin() + _offset; }
		const_iterator end() const { return begin() + _len; }
		bool operator==(const Buffer& other) const { return &_data == &other._data && _len == other._len && _offset == other._offset; }
		bool operator!=(const Buffer& other) const { return !(*this == other); }
	};
	inline Buffer operator+(const Buffer& buffer, int amount) {
		return Buffer(buffer.vector(), buffer.offset() + amount, buffer.size());
	}
	inline Buffer operator-(const Buffer& buffer, int amount) {
		return Buffer(buffer.vector(), buffer.offset() - amount, buffer.size());
	}

	// const char container, used mainly for map and stuff.  Be sure _str is always valid when this object is in use
	// it dosn't HAVE to be zero terminated
	// istring is the interface while cstring and string are the implmentation
	struct istring {
		// interface
		virtual const char* c_str() const = 0;
		virtual size_t length() const = 0;
		// helpers
		size_t hash() const { util::simple_hash(c_str(), length()); }
		size_t size() const { return length(); }
		std::string operator*() const { return std::string(c_str(), length()); }
		const char* begin() const { return c_str(); }
		const char* end() const { return c_str() + length(); }
		virtual ~istring() {  }
	};
	inline bool operator==(const istring& l, const istring& r) { return l.c_str() == r.c_str() || (l.length() == r.length() && std::memcmp(l.c_str(), r.c_str(), r.length()) == 0); }
	inline bool operator!=(const istring& l, const istring& r) { return !(l == r); }
	inline bool operator==(const istring& l, const std::string& r) { return r.compare(0, l.length(), l.c_str()) == 0; }
	inline bool operator!=(const istring& l, const std::string& r) { return  r.compare(0, l.length(), l.c_str()) != 0; }
	inline std::ostream& operator<<(std::ostream& os, const std::string& s) { os.write(s.c_str(), s.length()); return os; }
	// light weight class used mainly for using a string library
	class cstring : public istring {
	public:
		cstring(const char* str, size_t len) : _c_str(str), _len(len) {}
		cstring(const char* str) : cstring(str, strlen(str)) {}
		explicit cstring(const istring& str) : _c_str(str.c_str()), _len(str.length()) {}
		cstring& operator=(const char* str) { _c_str = str; _len = std::strlen(str); return *this; }
		cstring& operator=(const istring& str) { _c_str = str.c_str(); _len = str.length(); return *this; }
		const char* c_str() const override { return _c_str; }
		size_t length() const override { _len; }
	protected:
		size_t _len;
		const char* _c_str;
	};
	// use this if we need to make a copy of the string
	class sstring : public istring {
	public:
		sstring(const char* str, size_t len) : _str(str, len) {}
		explicit sstring(const char* str) : _str(str) {}
		explicit sstring(std::string str) : _str(str) {}
		explicit sstring(const istring& str) : _str(str.c_str(), str.length()) {}
		sstring& operator=(std::string str) { _str = str; return *this; }
		sstring& operator=(const istring& str) { _str.assign(str.c_str(), str.length()); return *this; }
		const char* c_str() const override { return _str.c_str(); }
		size_t length() const override { return _str.length(); }
		const std::string& str() const { return _str; }
		std::string& str() { return _str; }
		void str(std::string str) { _str = str; }
	protected:
		std::string _str;
	};

	class StreamIdent {
	public:
		StreamIdent() : _ident(0) {}
		StreamIdent(size_t ident) : _ident(ident) {}
		template<typename C, typename E>
		void to_stream(std::basic_ostream<C, E>& os) const {
			for (size_t i = 0; i < _ident; i++) os << "\t";
		}
		StreamIdent& operator++() { _ident++; return *this; }
		StreamIdent operator++(int) { auto tmp(*this); operator++(); return tmp; }
		StreamIdent& operator--() { _ident--; return *this; }
		StreamIdent operator--(int) { auto tmp(*this); operator--(); return tmp; }
		StreamIdent& operator+=(size_t v) { _ident += v; return *this; }
		StreamIdent& operator-=(size_t v) { _ident -= v; return *this; }
		StreamIdent& operator=(size_t v) { _ident = v; return *this; }
	private:
		size_t _ident;
	};

};

template<typename C, typename E, typename U, typename = std::enable_if<!util::priv::is_streamable<std::basic_ostream<C, E>, U>::value>>
static inline std::basic_ostream<C, E>& operator<<(std::basic_ostream<C, E>& os, const U& obj) {
#if _DEBUG
	util::priv::debug_serialize(os, obj);
#else
	util::priv::serialize(os, obj);
#endif
	return os;
}

#if 0
template<typename C, typename E>
static inline std::basic_ostream<C, E>& operator<<(std::basic_ostream<C, E>& os, const util::StreamIdent& ident) {
	ident.to_stream<C,E>(os);
	return os;
};

#endif

namespace std {
	template<>
	struct hash<util::istring>{
		size_t operator()(const util::istring& str) const { return str.hash(); }
	};
};
