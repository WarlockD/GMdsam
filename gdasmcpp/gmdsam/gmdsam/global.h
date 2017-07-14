#pragma once
#include <cstdint>
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
	// copyied from flyweight
	namespace impl {


		template <typename T>
		struct default_extractor {
			constexpr default_extractor() noexcept { }
			T const& operator () (T const& argument) const noexcept { return argument; }
		};

		template <typename T>
		using const_ref = std::add_lvalue_reference_t<std::add_const_t<T>>;

		template <class T, typename KeyExtractor = default_extractor<T>, typename Allocator = std::allocator<T>>
		struct container_traits final {
			using allocator_type = typename std::allocator_traits<Allocator>::template rebind_alloc<T>;
			using extractor_type = KeyExtractor;
			using computed_key_type = std::decay_t<decltype(std::declval<const_ref<extractor_type>>()(std::declval<const_ref<T>>()))>;


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
				std::hash<computed_key_type>,
				std::equal_to<computed_key_type>,
				typename std::allocator_traits<Allocator>::template rebind_alloc<
					std::pair<std::add_const_t<key_type>, mapped_unique_type>
				>
			>;
			using container_weak_type = std::unordered_map<
				key_type,
				mapped_weak_type,
				std::hash<computed_key_type>,
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

	class symboltable {
	public:
		using string_type = std::string;
		using value_type = std::add_const_t<string_type>;

		struct isymbol {
			std::atomic<int> mark;
			size_t length;
			size_t hash;
			const char* str;

			//template<typename ... Args>
			//isymbol(Args ... arg) : str(std::forward<Args>(arg)...) {}
	
			isymbol(isymbol&& move) = delete;
			static std::unique_ptr<isymbol> create(const isymbol& istr, bool const_string) {
				isymbol* ptr;
				if (const_string && istr.str[istr.length] == 0) {
					ptr = new isymbol(istr);
					ptr->mark = 2;
				}
				else {
					char* rptr = new char[istr.length + sizeof(isymbol) + 1];
					char* nstr = rptr + sizeof(isymbol);
					std::copy(nstr, nstr + istr.length, istr.str);
					ptr = new(rptr) isymbol(nstr, istr.length);
				}
				return std::unique_ptr<isymbol>(ptr);
			}
			isymbol(const char* str, size_t length) : length(length), hash(util::simple_hash(str, length)), str(str) {}
			bool operator==(const isymbol& r) const {
				return length == r.length && std::memcmp(str, r.str, length) == 0;
			}
		private:
			isymbol(const isymbol& copy, const char* str) : length(copy.length), hash(copy.hash), str(str) {}
			isymbol(const isymbol& copy) = default;
		};
		struct isymbol_hasher {
			size_t operator()(const isymbol& s) const { return s.hash;  }
		};
		struct key_extractor {
			constexpr key_extractor() noexcept { }
			string_type const& operator () (string_type const& argument) const noexcept { return argument; }
			string_type const& operator () (isymbol const& argument) const noexcept { return argument.str; }
		};
		using traits = impl::container_traits<isymbol, impl::default_extractor<isymbol>, std::allocator<isymbol>>;
		//	using key_type = std::reference_wrapper<string_type>;
		using container_type = typename traits::container_unique_type;
		using maped_type = typename traits::mapped_unique_type;
		inline static maped_type& empty_symbol() {
			static maped_type _empty = std::make_unique<isymbol>("", 0);
			return _empty;
		}
		static bool is_empty(const isymbol& sym) { return &sym == empty_symbol().get(); }
		static bool is_empty(const maped_type& sym) { return sym.get() == empty_symbol().get(); }
		maped_type& find(const char* str) noexcept {
			if (str == nullptr || str[0] == 0) return empty_symbol();
			std::lock_guard<std::mutex> lock(_mutex);
			isymbol skey(str,strlen(str));
			const auto& key = _extractor(skey);
			auto iter = _table.find(key);
			if (iter != _table.end()) { return iter->second; }
			return insert(skey);
		}
		void remove(const maped_type& value) noexcept {
			if (!value) return;
			std::lock_guard<std::mutex> lock(_mutex);
			const auto& key = _extractor(*value.get());
			auto iter = _table.find(key);
			if (iter != _table.end() && iter->second.get() == value.get()) {
				_table.erase(iter);
			}
		}
		void collect_marked() {
			std::lock_guard<std::mutex> lock(_mutex);
			for (auto it = _table.begin(); it != _table.end();) {
				isymbol* sym = it->second.get();
				if (sym->mark < 0) continue;
				if (sym->mark.exchange(0) != 0)
					it = _table.erase(it);
				else  it++;
			}
		}
	private:
		maped_type&  insert(const isymbol& value) noexcept {
			maped_type ptr = isymbol::create(value);
			const auto& key = _extractor(ptr->str);
			auto result = _table.emplace(key, std::move(ptr));
			return result.first->second;
		}
		std::mutex _mutex;
		container_type _table;
		key_extractor _extractor;
	};

	class symbol {
	public:
		using string_type = typename symboltable::string_type;
		using value_type = typename symboltable::value_type;
		template<typename ValueType> using is_value_type = std::is_same<std::decay_t<ValueType>, std::decay_t<value_type>>;
		template<typename ValueType> using is_symbol_type = std::is_same<std::decay_t<ValueType>, symbol>;
	private:
		using symbol_tracker = class_tracking<symbol>;

		inline static symboltable& table() {
			static symboltable _table;
			return _table;
		}
		inline static symbol_tracker& symbols() {
			static symbol_tracker _symbols;
			return _symbols;
		}
		symboltable::isymbol* _symbol;

		void _assign(symboltable::isymbol* sym) {
			if (sym != _symbol) {
				if (symboltable::is_empty(*sym))  symbols().erase(this);
				if (symboltable::is_empty(*_symbol))  symbols().emplace(this);
				_symbol = sym;
			}
		}
		// we could just use find, as that returns the empty symbol but this is a slight optimizeing
		// on empty strings
		template<typename U>
		void _assign(U&& str) {
			if (empty()) {
				if (!str.empty()) {
					symbols().emplace(this);
					_symbol = table().find(std::forward<U>(str)).get();
				}
			}
			else {
				if (str.empty()) clear();
				else _symbol = table().find(std::forward<U>(str)).get();
			}
		}
	public:
		bool empty() const { return symboltable::is_empty(*_symbol); }
		void clear() {
			if (!empty()) symbols().erase(this);
			_symbol = symboltable::empty_symbol().get();
		}
		void make_perm() {
			// makes this string uncolectable
			_symbol->mark = -1;
		}
		static void collect_garbage() {
			symbols().for_each([](symbol* o) { if (!o->empty() && o->_symbol->mark == 0) o->_symbol->mark = 1; });
			table().collect_marked();
		}
		symbol() : _symbol(symboltable::empty_symbol().get()) {}
		symbol(const symbol& copy) :_symbol(copy._symbol) { symbols().emplace(this); }
		symbol(symbol&& move) :_symbol(move._symbol) { symbols().emplace(this); move.clear(); }
		symbol& operator=(const symbol& copy) { _assign(copy._symbol); return *this; }
		symbol& operator=(symbol&& move) { _assign(move._symbol); move.clear(); return *this; }
		~symbol() { if (!empty()) clear(); }

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
#if 0
			string_type str(std::forward<Args>(args)...);
			_assign(std::move(str));
		}
#endif

		template <class ValueType> symbol& operator =(ValueType&& value) { *this = symbol(std::forward<ValueType>(value)); return *this; }
		// I get what this is trying to do, if the arg is a symbol, then to ingore this and use the constructor
		// just not sure how ot use  std::conjunction



		size_t size() const { return _symbol->length; }
		operator std::string() const { return std::string(_symbol->str,_symbol->length); }
		std::string str() const { return std::string(_symbol->str, _symbol->length); }
		const char* c_str() const { return _symbol->str; }
		const char* begin() const { return _symbol->str; }
		const char* end() const { return _symbol->str + _symbol->length; }
		bool operator==(const symbol& r) const { return _symbol == r._symbol; }
		bool operator!=(const symbol& r) const { return _symbol != r._symbol; }
		bool operator==(value_type& r) const { return _symbol->str == r; }
		bool operator!=(value_type& r) const { return _symbol->str != r; }
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
		size_t operator()(const util::symbol& sym) const { return std::hash<std::string>()(sym.str()); }
	};
};
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



}

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
