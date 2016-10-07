#pragma once
#include <cstdint>
#include <vector>

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
		no_stream_exception() : std::exception("Oject has no to_string method") {}
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
	

namespace debug {
	namespace priv {
		// http://stackoverflow.com/questions/257288/is-it-possible-to-write-a-template-to-check-for-a-functions-existence
		struct has_no_serializtion {};
		struct has_to_string : has_no_serializtion {};
		struct has_stream_operator : has_no_serializtion {};
		struct has_to_stream : has_stream_operator {};
		struct search_for_serializtion : has_to_stream {};
		struct debug_serializtion : search_for_serializtion {};
		template<class T> auto serialize_imp(std::ostream& os, T const& obj, has_no_serializtion) { os << obj; } // throw console::no_stream_exception;
		template<class T> auto serialize_imp(std::ostream& os, T const& obj, has_to_string)-> decltype(obj.to_string(), void()) { os << obj.to_string(); }
		template<class T> auto serialize_imp(std::ostream& os, T const& obj, has_stream_operator)-> decltype(s << obj, void()) { obj << obj; }
		template<class T> auto serialize_imp(std::ostream& os, T const& obj, has_to_stream)-> decltype(obj.to_stream(os), void()) { obj.to_stream(os); }
		template<class T> auto serialize_imp(std::ostream& os, T const& obj, debug_serializtion)-> decltype(obj.to_debug(os), void()) { obj.to_debug(os); }
		template<class T> auto serialize(std::ostream& os, T const& obj) -> decltype(serialize_imp(os, obj, search_for_serializtion{}), void())
		{
			priv::serialize_imp(os, obj, priv::search_for_serializtion{});
		}
		template<class T> auto debug_serialize(std::ostream& os, T const& obj) -> decltype(serialize_imp(os, obj, debug_serializtion{}), void())
		{
			priv::serialize_imp(os, obj, priv::debug_serializtion{});
		}
	};
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
				priv::debug_serialize(_stream, obj);
			else
				priv::serialize(_stream, obj);
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
	template<typename T>
	inline ostream& operator<<(ostream& os, T&& obj) {os.write(obj); return os;}

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
};

namespace util {
	namespace priv {
		// http://stackoverflow.com/questions/257288/is-it-possible-to-write-a-template-to-check-for-a-functions-existence
		struct has_no_serializtion {};
		struct has_to_string : has_no_serializtion {};
		struct has_stream_operator : has_no_serializtion {};
		struct has_to_stream : has_stream_operator {};
		struct search_for_serializtion : has_to_stream {};
		template<class T> auto serialize_imp(std::ostream& os, T const& obj, has_no_serializtion) { os << '?' << typeid(T).name() << '?'; } // throw console::no_stream_exception;
		template<class T> auto serialize_imp(std::ostream& os, T const& obj, has_to_string)-> decltype(obj.to_string(), void()) { os << obj.to_string(); }
		template<class T> auto serialize_imp(std::ostream& os, T const& obj, has_stream_operator)-> decltype(s << obj, void()) { obj << obj; }
		template<class T> auto serialize_imp(std::ostream& os, T const& obj, has_to_stream)-> decltype(obj.to_stream(os), void()) { obj.to_stream(os); }
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

	template<class T> auto serialize(std::ostream& os, T const& obj) -> decltype(priv::serialize_imp(os, obj, priv::search_for_serializtion{}), void())
	{
		priv::serialize_imp(os, obj, priv::search_for_serializtion{});
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

};

namespace std {
	template<>
	struct hash<util::istring>{
		size_t operator()(const util::istring& str) const { return str.hash(); }
	};
};
