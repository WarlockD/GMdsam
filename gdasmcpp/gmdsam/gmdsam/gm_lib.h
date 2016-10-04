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

namespace gm {
	// structore for event info
	enum class e_event {
		CreateEvent = 0,
		DestroyEvent,
		Alarm,
		Step,
		Collision,
		Key,
		JoystickMouse,
		Other,
		Draw,
		Keypressed,
		Keyreleased,
		Trigger
	};
	class EventType : public StreamInterface {
		size_t _event;
		friend struct std::hash<gm::EventType>;
	public:
		
		EventType(size_t event) : _event(event << 16) {}
		EventType(size_t event, size_t sub_event) : _event(event << 16 | (sub_event & 0xFFFF)) {}
		static EventType from_index(int index) { return EventType(index >> 16, index & 0xFFFF); }
		size_t event() const { return _event >> 16; }
		size_t sub_event() const { return _event & 0xFFFF; }
		size_t raw() const { return _event; }
		bool operator==(const EventType& other) const { return _event == other._event; }
		bool operator!=(const EventType& other) const { return !(*this == other); }
		bool operator<(const EventType& other) const { return _event < other._event; }
		void to_stream(std::ostream& os) const override;
	};
};
// custom specialization of std::hash can be injected in namespace std
namespace std
{
	template<> struct hash<gm::EventType>
	{
		size_t operator()(gm::EventType const& e) const { return e._event; }
	};
}
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
	
	template <typename TT>
	class StaticArray
	{
	public:
		typedef typename TT value_type;
		typedef typename TT* pointer;
		typedef typename TT& reference;
		typedef typename const TT* const_pointer;
		typedef typename const TT& const_reference;
		typedef typename std::ptrdiff_t difference_type;
		typedef typename PointerIterator<const TT> const_iterator;
		typedef typename PointerIterator<TT> iterator;
		StaticArray() : _list(nullptr), _size(0) {  }
		StaticArray(const_pointer list, size_t size) : _list(list), _size(size) {  }
		const_iterator begin() const { return const_iterator(_list); }
		const_iterator end() const { return const_iterator(_list + _size); }
		size_t size() const { return _size; }
		const_reference at(size_t i) const { return _list[i]; }
		const_reference operator[](size_t i) const { return _list[i]; }
		const_pointer data() const { return _list; }
	protected:
		const_pointer _list;
		size_t _size;
	};
};
namespace gm {
	class Offsets : public util::StaticArray<uint32_t> {
	public:
		Offsets() : StaticArray(){  }
		Offsets(const uint8_t* ptr) : StaticArray(reinterpret_cast<const uint32_t*>(ptr + sizeof(uint32_t)), *reinterpret_cast<const uint32_t*>(ptr)) {  }
		Offsets(const uint8_t* ptr, size_t offset) : Offsets(ptr + offset) {}
	};
	template<typename VALUE_T>
	class OffsetList {
		const uint8_t* _data;
		Offsets _list;
	public:
		class OffsetListIT {
			const OffsetList& _vec;
			size_t _pos;
		public:
			typedef typename std::bidirectional_iterator_tag iterator_category;
			typedef typename VALUE_T value_type;
			typedef typename long difference_type;
			typedef typename const VALUE_T* pointer;
			typedef typename const VALUE_T& reference;
			OffsetListIT(const OffsetList& vec, size_t pos) : _vec(vec), _pos(pos) {}
			OffsetListIT(const OffsetList& vec) : _vec(vec), _pos(0) {}
			OffsetListIT& operator++() { _pos++; return *this; }
			OffsetListIT operator++(int) { OffsetListIT copy(*this); _pos++; return copy; }
			OffsetListIT& operator--() { _ pos--; return *this; }
			OffsetListIT operator--(int) { OffsetListIT copy(*this); _pos--; return copy; }
			const VALUE_T* operator*() const { return _vec.at(_pos); }
			pointer operator->() const { return _list; }
			bool operator==(const OffsetListIT& other) const { return   _pos == other._pos; }
			bool operator!=(const OffsetListIT& other) const { return !(*this == other); }
			bool operator<(const OffsetListIT& other) const { return _pos < other._pos; }
			bool operator>(const OffsetListIT& other) const { return _pos > other._pos; }
			bool operator<=(const OffsetListIT& other) const { return _pos <= other._pos; }
			bool operator>=(const OffsetListIT& other) const { return _pos >= other._pos; }
		};
		typedef typename std::bidirectional_iterator_tag iterator_category;
		typedef typename VALUE_T value_type;
		typedef typename long difference_type;
		typedef typename VALUE_T* pointer;
		typedef typename VALUE_T& reference;
		typedef typename OffsetListIT iterator;
		OffsetList(const uint8_t* ptr,size_t offset) : _data(ptr), _list(ptr,offset) {}
		OffsetList(const uint8_t* ptr, const uint8_t* list) : _data(ptr), _list(list) {}
		size_t size() const { return _list.size(); }
		const VALUE_T* at(size_t i) const { return reinterpret_cast<const VALUE_T*>(data + _list.at(i)); }
		iterator begin() const { return OffsetList(this, 0); }
		iterator end() const { return OffsetList(this, size()); }
	};
	

	// class forces a struct/class to be non copyable or creatable
	template<typename C> // ugh cannot do this in an undefined class , typename = std::enable_if<std::is_pod<C>::value>>
	struct CannotCreate {
		typedef typename C raw_type;
		static constexpr size_t raw_size() { return  sizeof(C); }
		static const C* cast(const uint8_t* data) { return reinterpret_cast<const C*>(data); }
		static const C* cast(const uint8_t* data, size_t offset) { return reinterpret_cast<const C*>(data + offset); }
		const uint8_t* ptr_begin() const { return reinterpret_cast<const uint8_t*>(this); }
		const uint8_t* ptr_end() const { return ptr_begin() + sizeof(raw_type); }
		CannotCreate() = delete; // we have nothing
		CannotCreate(CannotCreate const &) = delete;           // undefined
		CannotCreate& operator=(CannotCreate const &) = delete;  // undefined
		CannotCreate(CannotCreate &&) = delete;           // undefined
		CannotCreate& operator=(CannotCreate &&) = delete;  // undefined
	};

	namespace priv {
		template<typename T, typename V = bool>
		struct has_name_offset : std::false_type { };
		template<typename T>
		struct has_name_offset<T, typename std::enable_if<!std::is_same<decltype(std::declval<T>().name_offset), void>::value, bool>::type> : std::true_type
		{
			typedef decltype(std::declval<T>().name_offset) type;
		};
		template<typename T, typename V = bool>
		struct is_resource : std::false_type { };
		template<typename T>
		struct is_resource<T, typename std::enable_if<!std::is_same<decltype(T::ResType), void>::value, bool>::type> : std::true_type
		{
			typedef decltype(T::ResType) type;
		};
		template<typename T, typename = std::enable_if<std::is_arithmetic<T>::value>>
		T cast(const uint8_t* ptr) { return *reinterpret_cast<const T*>(ptr); }

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
	};
	// The entire file is read into
	// carful with any strings read from this as they are only valid
	// as long as FileHelper exists
	// Container for const strings for unorded_map
	class GString  {
		size_t _len;
		const char* _str;
	public:
		struct hash_func {
			size_t operator()(const GString& str) const { return priv::simple_hash(str.c_str(), str.length()); }
		};
		GString(const char* str, size_t len) : _str(str), _len(len) {}
		GString(const char* str) : GString(str, strlen(str)) {}
		std::string operator*() const { return std::string(_str, _len); }
		size_t size() const { return _len; }
		size_t length() const { return _len; }
		const char* begin() const { return _str; }
		const char* end() const { return _str + _len; }
		const char* c_str() const { return _str; }
		bool operator==(const GString& other) const { return _str == other._str; }
		bool operator!=(const GString& other) const { return !(*this == other); }
		bool operator==(const std::string& other) const { return other.compare(0,_len,_str) == 0; }
		bool operator!=(const std::string& other) const { return  other.compare(0, _len, _str) != 0; }
	};
	class FileHelper {
		std::vector<uint8_t> _data;
		size_t _pos;
		std::vector<uint32_t> _offsets; // used for offset lists
	public:
		FileHelper() :  _pos(0) {}
		void load(const std::vector<uint8_t>& data) { _data = data;  _pos = 0;  }
		void load(std::vector<uint8_t>&& data) { _data = std::move(data);  _pos = 0; }
		void load(std::istream& is)  {
			_pos = 0;
			is.seekg(std::ios::end, 0);
			size_t size = is.tellg();
			_data.resize(size);
			is.seekg(0);
			is.read(reinterpret_cast<char*>(_data.data()), size);
		}
		FileHelper(const std::vector<uint8_t>& data) { load(data); }
		FileHelper(std::vector<uint8_t>&& data) { load(data); }
		FileHelper(std::istream& is) { load(is); }
		// save or push the offset stack
		size_t offset() const { return _pos; }
		size_t size() const { return _data.size(); }
		const uint8_t* data() const { return _data.data(); }
		uint8_t* data() { return _data.data(); }
		template<typename T, typename = std::enable_if<std::is_arithmetic<T>::value>::type>
		T read() {
			T value = *reinterpret_cast<T*>(_data.data() + _pos);
			_pos += sizeof(T);
			return value;
		}
		template<typename T, typename = std::enable_if<(std::is_arithmetic<T>::value || std::is_pod<T>::value)>::type>
		uint32_t read(T* a, size_t count) {
			std::memcpy(a, _data.data(), sizeof(T)*count);
			_pos += sizeof(T) * count;
			return count;
		}
		template<typename T, typename = std::enable_if<(std::is_arithmetic<T>::value || std::is_pod<T>::value)>::type>
		uint32_t read(std::vector<T>& a) {
			uint32_t size = read<uint32_t>();		// first is a uint32_t that is the size
			if (size > 0) {
				a.resize(size);
				std::memcpy(a, _data.data(), sizeof(T)*size);
				_pos += sizeof(T) * size;
			}
			return size;
		}
		// we read an offset list that contains 
		template<typename T, typename = std::enable_if<(std::is_arithmetic<T>::value || std::is_pod<T>::value)>::type>
		uint32_t read_list(std::vector<T>& a) {
			uint32_t size = read(_offsets);	// get the offset list
			if (size > 0) {
				a.resize(size); // resize the array, hope we have a default constructor
				for (size_t i = 0; i < size; i++)
					std::memcpy(a.data() + i, _data.data() + _offsets.at(i), sizeof(T));
			}
			return size;
		}
		const char* str(size_t offset) { return reinterpret_cast<char*>(_data.data()) + offset; }
	};
	


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
		const char* c_str() const { return reinterpret_cast<const char*>(_data.data() + _offset); }
		const char* c_str_absolute(size_t offset) const { return reinterpret_cast<const char*>(_data.data() + offset); }
		template<typename T>
		const T* cast() const { return reinterpret_cast<const T*>(_data.data() + _offset); }
		template<typename T>
		const T* cast_absolute(size_t offset) const { return reinterpret_cast<const T*>(_data.data() + offset); }
		template<typename T>
		void read(const T*& data) { data = reinterpret_cast<const T*>(_data.data() + _offset); _offset += sizeof(T); }
		template<typename T>
		void read_absolute(const T*& data, size_t offset) const { data = reinterpret_cast<const T*>(_data.data() + offset); }
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

	enum class ChunkType : uint32_t {
		BAD = 0,
		FORM = 'FORM', // not really a chunk, header with size of file
		GEN8 = 'GEN8',
		TXTR = 'TXTR',
		BGND = 'BGND',
		TPAG = 'TPAG',
		SPRT = 'SPRT',
		ROOM = 'ROOM',
		AUDO = 'AUDO',
		SOND = 'SOND',
		FONT = 'FONT',
		OBJT = 'OBJT',
		PATH = 'PATH',
		CODE = 'CODE',
		VARS = 'VARS',
		FUNC = 'FUNC',
		STRG = 'STRG',
	};
	
	class OffsetInterface : public StreamInterface {
	protected:
		uint32_t _offset;
	public:
		OffsetInterface(uint32_t offset) : _offset(offset) {}
		virtual ~OffsetInterface() {}
		uint32_t offset() const { return _offset; } // unique
		bool operator<(const OffsetInterface& other) const { return offset() < other.offset(); }
		bool operator==(const OffsetInterface& other) const { return offset() == other.offset(); }
		bool operator!=(const OffsetInterface& other) const { return !(*this == other); }
		virtual void to_stream(std::ostream& os) const override {
			os << "[" << std::uppercase << std::setfill('0') << std::setw(6) << std::hex << _offset << ']';
		}
	};
	class IndexInterface : public OffsetInterface {
	protected:
		uint32_t _index;
	public:
		IndexInterface(uint32_t offset, uint32_t index) : OffsetInterface(offset), _index(index) {}
		virtual ~IndexInterface() {}
		uint32_t index() const { return _index; } // unique
		bool operator<(const IndexInterface& other) const { return index() < other.index(); }
		bool operator==(const IndexInterface& other) const { return index() == other.index(); }
		bool operator!=(const IndexInterface& other) const { return !(*this == other); }
		virtual void to_stream(std::ostream& os) const override {
			OffsetInterface::to_stream(os);
			os << '(' << std::left << std::setfill(' ') << std::setw(4) << _index << ')';
		}
	};
	class NameInterface : public IndexInterface {
	protected:
		constexpr static const char * NONAME = "<EMPTY>";
		const char* _name;
	public:
		NameInterface(uint32_t offset, uint32_t index, const char* name) : _name(name), IndexInterface(offset,index) {}
		const char* name() const { return _name; }
		bool valid_name() const { return _name && *_name != '\0'; }
		bool operator<(const NameInterface& other) const { return IndexInterface::operator<(other); }
		bool operator==(const IndexInterface& other) const { return IndexInterface::operator==(other); }
		bool operator!=(const IndexInterface& other) const { return IndexInterface::operator!=(other); }
		virtual ~NameInterface() {}
		virtual void to_stream(std::ostream& os) const override {
			OffsetInterface::to_stream(os);
			os << '(' << std::setfill(' ') << std::setw(4) << _index << ':' << (valid_name() ? _name : NONAME) << ')';
		}
	};
	//http://stackoverflow.com/questions/36936584/how-to-write-constexpr-swap-function-to-change-endianess-of-an-integer
	template<class T>
	constexpr typename std::enable_if<std::is_unsigned<T>::value, T>::type
		byte_swap(T i, T j = 0u, std::size_t n = 0u) {
		return n == sizeof(T) ? j :
			byte_swap<T>(i >> CHAR_BIT, (j << CHAR_BIT) | (i & (T)(unsigned char)(-1)), n + 1);
	}
	template<ChunkType CT = ChunkType::BAD>
	struct chunk_traits {
		static constexpr uint32_t value() { return static_cast<uint32_t>(CT); }
		static constexpr uint32_t swap_value() { return byte_swap(value()); }
		static constexpr const char* name() { return { (char)((uint32_t)CT >> 24), (char)((uint32_t)CT >> 16), (char)((uint32_t)CT >> 8), (char)CT,0 }; }
	};
	template<typename RAW_T, ChunkType CT = ChunkType::BAD>
	struct ResourceTraits {
	public:
		static constexpr ChunkType ResType = CT;
		typedef typename RAW_T RawResourceType;
		static constexpr size_t RawResourceSize = sizeof(RawResourceType);
		static constexpr const char ResTypeName[5] = { (char)((uint32_t)CT >> 24), (char)((uint32_t)CT >> 16), (char)((uint32_t)CT >> 8), (char)CT, 0 };
		static constexpr bool HasNameOffset = priv::has_name_offset<RAW_T>::value;
	protected:
		constexpr size_t resource_size() const { return RawResourceSize; }
		constexpr ChunkType resource_type() const { return ResType; }
		constexpr const char* resource_name() const { return ResTypeName; }
	};
	template<typename RAW_T>
	class RawResource : public  OffsetInterface {
	protected:
		const RAW_T* _raw;
	public:
		RawResource(const uint8_t* data, size_t offset) : OffsetInterface(offset), _raw(reinterpret_cast<const RAW_T*>(data + offset)) {}
		bool valid() const { return _raw != nullptr; }
		const RAW_T& raw() const { return _raw; }
	};

	template<typename RAW_T, ChunkType CT>
	class Resource :  public std::conditional<ResourceTraits<RAW_T, CT>::HasNameOffset, NameInterface, IndexInterface>::type , public ResourceTraits<RAW_T, CT>{
	protected:
		typedef typename std::conditional<ResourceTraits<RAW_T, CT>::HasNameOffset, NameInterface, IndexInterface>::type base_type;
		const RAW_T* _raw;
		struct _index_test {};
		struct _name_test : _index_test {};
		template<typename T = RAW_T, typename = std::enable_if<!HasNameOffset>::type>
		Resource(int index, const uint8_t* data, size_t offset, _name_test) : _raw(reinterpret_cast<const RAW_T*>(data + offset)), base_type(offset, index) { }
		template<typename T = RAW_T, typename = std::enable_if<HasNameOffset>::type>
		Resource(int index, const uint8_t* data, size_t offset, _index_test) : _raw(reinterpret_cast<const RAW_T*>(data + offset)), base_type(offset, index, reinterpret_cast<const char*>(data + reinterpret_cast<const RAW_T*>(data + offset)->name_offset)) { }
	public:
		Resource() : _name(), _index(-1), _raw(nullptr) {}
		bool valid() const { return _raw != nullptr; }
		const RAW_T* raw() const { return _raw; }
		Resource(int index, const uint8_t* data, size_t offset) : Resource(index, data, offset, _name_test{}) {}
	};
	namespace raw_type {
		// these are all the internal raw types in game maker that I could derive
		// the exception will be func/code as the two versions I know about have diffrent structures
		
#pragma pack(push, 1)

		// All strings ARE null terminated within a data.win file.  However when you read offsets the point to 
		// the string itself and NOT to this structure.  This structure is only in the STNG chunk
		struct String : CannotCreate<String> {
			uint32_t length;
			const char u_str[1];
		};
		struct SpriteFrame : CannotCreate<SpriteFrame> {
			short x;
			short y;
			unsigned short width;
			unsigned short height;
			short offset_x;
			short offset_y;
			unsigned short crop_width;
			unsigned short crop_height;
			unsigned short original_width;
			unsigned short original_height;
			short texture_index;
			bool valid() const { return texture_index != -1; }
		};
		struct Background : CannotCreate<Background> {
			uint32_t name_offset;
			uint32_t trasparent;
			uint32_t smooth;
			uint32_t preload;
			uint32_t frame_offset;
		};
		struct RoomView : CannotCreate<RoomView> {
			int visible;
			int x;
			int y;
			int width;
			int height;
			int port_x;
			int port_y;
			int port_width;
			int port_height;
			int border_x;
			int border_y;
			int speed_x;
			int speed_y;
			int view_index;
		};
		struct RoomBackground : CannotCreate<RoomBackground> {
			int visible;// bool
			int foreground;// bool
			int background_index;// bool
			int x;
			int y;
			int tiled_x;
			int tiled_y;
			int speed_x;
			int speed_y;
			int strech; // bool
		};
		struct RoomObject : CannotCreate<RoomObject> {
			int x;
			int y;
			int object_index;
			int id;
			int code_offset;
			float scale_x;
			float scale_y;
			int color;
			float rotation;
		};
		struct RoomTile : CannotCreate<RoomTile> {
			int x;
			int y;
			int background_index;
			int offset_x;
			int offset_y;
			int width;
			int height;
			int depth;
			int id;
			float scale_x;
			float scale_y;
			int blend; // color value
		};
		struct Room : CannotCreate<Room> {
			int name_offset;
			int caption_offset;
			int width;
			int height;
			int speed;
			int persistent;
			int color;
			int show_color;
			int code_offset;
			int flags;
			int background_offset;
			int view_offset;
			int object_offset;
			int tiles_offset;
		};
		struct Sound : CannotCreate<Sound> {
			int name_offset;
			int audio_type;
			int extension_offset;
			int filename_offset;
			int effects;
			float volume;
			float pan;
			int other;
			int sound_index;
		};
		struct AudioData : CannotCreate<AudioData> {
			const int size;
			const uint8_t data[1];
		};
		struct Font : CannotCreate<Font> {
			int name_offset;
			int description_offset;
			int size;
			int bold;
			int italic;
			int flags; // (antiAlias | CharSet | firstchar)
			int lastChar;
			uint32_t frame_offset;
			float scale_width;
			float scale_height;
		};
		struct ObjectPhysicsVert : CannotCreate<ObjectPhysicsVert> {
			float x;
			float y;
		};
		struct ObjectAction : CannotCreate<ObjectAction> {
			int lib_id;
			int id;
			int kind;
			int use_relative;
			int is_question;
			int use_apply_to;
			int exe_type;
			uint32_t name_offset;
			uint32_t code_offset;
			int argument_count;
			int who;
			int is_relative;
			int is_not;
			int is_compiled; // should be zero?
		};
		struct Object : CannotCreate<Object> {
			int name_offset;
			int sprite_index;
			int visible;
			int solid;
			int depth;
			int persistent;
			int parent_index;
			int mask;
			int physics_enabled;
			int physics_sensor;
			int physics_shape;
			float physics_density;
			float physics_restitution;
			int physics_group;
			float physics_linear_damping;
			float physics_angular_damping;
			int physics_vert_count;
			float physics_angular_friction;
			int physics_awake;
			int physics_kinematic;
		};
		struct Sprite : CannotCreate<Sprite> {
			int name_offset;
			int width;
			int height;
			int left;
			int right;
			int bottom;
			int top;
			int trasparent;
			int smooth;
			int preload;
			int mode;
			int colcheck;
			int original_x;
			int original_y;
			//uint32_t frame_count;
			//uint32_t frame_offsets[1];
		};
		struct OldCode : CannotCreate<OldCode> {
			int name_offset;
			int list_size;
		};
#pragma pack(pop)
	};


	class BitMask {
		int _width;
		int _height;
		const uint8_t* _raw;
	public:
		BitMask() : _width(0), _height(0), _raw(nullptr) {}
		BitMask(int width, int height, const uint8_t* data) : _width(width), _height(height), _raw(data) {}
		int stride() const { return (_width + 7) / 8; }
		int width() const { return _width; }
		int height() const { return _height; }
		const uint8_t* raw() { return _raw; }
		const uint8_t* scaneline(int line) const { return _raw + (stride()*line); }
		// mainly here for a helper function, tells if a bit is set
		// its really here to describe how bitmaks are made
		bool isSet(int x, int y) const {
			uint8_t pixel = _raw[y * _width + x / 8];
			uint8_t bit = (7 - (x & 7) & 31);
			return ((pixel >> bit) & 1) != 0;
		}
	};


	class Texture {
		const uint8_t* _data;
		size_t _len;
	public:
		Texture() : _data(nullptr), _len(0) {}
		Texture(const uint8_t* data, size_t len) :_data(data), _len(len) {}
		const uint8_t* data() const { return _data; }
		size_t len() const { return _len; }
	};
	class Background : public Resource<raw_type::Background, ChunkType::BGND> {
		friend class UndertaleFile;
	protected:
		const raw_type::SpriteFrame* _frame;
	public:
		Background(int index, const uint8_t* data, size_t offset) :
			Resource(index, data, offset)
		{
			_frame = dynamic_cast<const raw_type::SpriteFrame*>(_frame + _raw->frame_offset);
		}
		bool trasparent() const { return _raw->trasparent != 0;  }
		bool smooth() const { return _raw->smooth != 0; }
		bool preload() const { return _raw->preload != 0; }
		const raw_type::SpriteFrame& frame() const { return *_frame; }
	};
	class Room : public Resource<raw_type::Room, ChunkType::ROOM> {
	protected:
		const char* _caption;
	public:
		Room(int index, const uint8_t* data, size_t offset) : Resource(index, data, offset) {}
		const char* caption() const { return _caption; }
		int width() const { return _raw->width; }
		int height() const { return _raw->height; }
		int speed() const { return _raw->speed; }
		bool persistent() const { return _raw->persistent != 0; }
		int color() const { return _raw->color; }
		bool show_color() const { return _raw->show_color != 0; }
		int code_offset() const { return _raw->code_offset; }
		bool enable_views() const { return (_raw->flags & 1) != 0; }
		bool view_clear_screen() const { return (_raw->flags & 2) != 0; }
		bool clear_display_buffer() const { return (_raw->flags & 14) != 0; }
		OffsetList<raw_type::RoomView> views() const { return OffsetList<raw_type::RoomView>(_raw->ptr_begin() - _offset, _raw->view_offset); }
		OffsetList<raw_type::RoomBackground> backgrounds() const { return OffsetList<raw_type::RoomBackground>(_raw->ptr_begin() - _offset, _raw->background_offset); }
		OffsetList<raw_type::RoomObject> objects() const { return OffsetList<raw_type::RoomObject>(_raw->ptr_begin() - _offset, _raw->object_offset); }
		OffsetList<raw_type::RoomTile> tiles() const { return OffsetList<raw_type::RoomTile>(_raw->ptr_begin() - _offset, _raw->tiles_offset); }
	};
	class Sound : public Resource<raw_type::Sound, ChunkType::SOND> {
	private:
		const raw_type::AudioData* _data;
		const char*  _extension;
		const char*   _filename;
	public:
		Sound(int index, const uint8_t* data, size_t offset) : Resource(index, data, offset) {}

		int audio_type() const { return _raw->audio_type; }
		const char*   extension() const { return _extension; }
		const char*   filename() const { return _filename; }
		int effects() const { return _raw->effects; }
		float volume() const { return _raw->volume; }
		float pan() const { return _raw->pan; }
		int other() const { return _raw->other; }
		const raw_type::AudioData* data() const { return _data; }
	};
	class Font : public Resource<raw_type::Font, ChunkType::FONT> {
	public:
		struct Kerning {
			short other;
			short amount;
		};
		struct Glyph : CannotCreate<Glyph> {
			short ch;
			short x;
			short y;
			short width;
			short height;
			short shift;
			short offset;
			unsigned short kerning_count;
			Kerning kernings[1];
			//uint32_t count;
			//	uint32_t offsets[1];
		};
	private:
		const raw_type::SpriteFrame* _frame;
		const char* _description;
		OffsetList<Glyph> _glyphs;
	public:
		Font(int index, const uint8_t* data, size_t offset) : Resource(index, data, offset)
			,_description(reinterpret_cast<const char*>(data + _raw->description_offset))
			, _frame(reinterpret_cast<const raw_type::SpriteFrame*>(data + _raw->frame_offset))
			, _glyphs(data, offset + sizeof(RawResourceType)){}
		int size() const { return _raw->size; }
		bool bold() const { return _raw->bold != 0; }
		bool italic() const { return _raw->italic != 0; }
		bool antiAlias() const { return ((_raw->flags >> 24) & 0xFF) != 0; }
		int charSet() const { return (_raw->flags >> 16) & 0xFF; }
		uint16_t firstChar() const { return (_raw->flags) & 0xFFFF; }
		uint16_t lastChar() const { return _raw->lastChar; }
		const raw_type::SpriteFrame& frame() const { return *_frame; }
		float scaleWidth() const { return _raw->scale_width; }
		float scaleHeight() const { return _raw->scale_height; }
	//	OffsetList<Glyph> glyphs() const { return OffsetList<Glyph>(_raw->ptr_begin()-_offset, _raw_glyphs; }
	};

	class Action : public Resource<raw_type::ObjectAction, ChunkType::BAD> {
		const uint32_t* _code;
	public:
		Action(const EventType& type, const uint8_t* data, size_t offset)
			: Resource(type.raw(),data, offset)
			, _code(_raw->code_offset > 0 ? reinterpret_cast<const uint32_t*>(data + _raw->code_offset):nullptr) {}
		const uint32_t* code() const { return _code; }
		int lib_id() const { return _raw->lib_id; }
		int id() const { return _raw->id; }
		int kind() const { return _raw->kind; }
		int use_relative() const { return _raw->use_relative; }
		int is_question() const { return _raw->is_question; }
		int use_apply_to() const { return _raw->use_apply_to; }
		int exe_type() const { return _raw->exe_type; }
		int argument_count() const { return _raw->argument_count; }
		int who() const { return _raw->who; }
		int is_relative() const { return _raw->is_relative; }
		int is_not() const { return _raw->is_not; }
		int is_compiled() const { return _raw->is_compiled; }
		virtual void to_stream(std::ostream& os) const override {
			OffsetInterface::to_stream(os);
			os << "{ event : " << EventType::from_index(_index) << ", id : " << id() << " }";
		}
	};

	class Object : public Resource<raw_type::Object, ChunkType::OBJT> {
		const raw_type::ObjectPhysicsVert* _physics_verts;
		std::unordered_map<EventType, Action> _events;
	public:
		Object(int index, const uint8_t* data, size_t offset)
			: Resource(index, data, offset)
			, _physics_verts(_raw->physics_vert_count > 0 ? raw_type::ObjectPhysicsVert::cast(_raw->ptr_end()) : nullptr)
		{
			auto ptr = _raw->ptr_end();
			if (_raw->physics_vert_count > 0) ptr += _raw->physics_vert_count * sizeof(raw_type::ObjectPhysicsVert);
			Offsets root(ptr);
			if (root.size() != 12) throw; // should always = 12?
			// might be a way to template this to caculate the offsets but right now this works
			for (uint32_t i = 0; i < root.size(); i++) {
				Offsets list(data,root.at(i));
				if (list.size() == 0)  continue;  // skip
				for (uint32_t e : list) {
					int sub_event = priv::cast<int>(data + e);
					Offsets events(data, e+sizeof(uint32_t));
					if (events.size() == 0) continue; // skip
					for (uint32_t a : events)  {
						EventType evt(i, sub_event);
						_events.emplace(evt, Action(evt,data, a));
					}
				//	std::cerr << "offset: " << list->at(e) << std::endl;
				//	events.emplace_back(data, list->at(e));
				//	std::cerr << "name: " << events.back().actions().at(0)->raw()->name_offset << std::endl;
				}
			}
		}
		const std::unordered_map<EventType, Action>& events() const { return _events; }
		int sprite_index() const { return _raw->sprite_index; }
		bool visible() const { return _raw->visible != 0; }
		bool solid() const { return _raw->solid != 0; }
		int depth() const { return _raw->depth; }
		bool persistent() const { return _raw->persistent != 0; }
		int parent_index() const { return _raw->parent_index; }
		bool mask() const { return _raw->mask != 0; }
		bool physics_enabled() const { return _raw->physics_enabled != 0; }
	};

	class Sprite : public Resource<raw_type::Sprite, ChunkType::SPRT> {
		const uint8_t* _masks;
		// kind of a hack.  First number is an int of the size, after that
		// its just an array of masks, not sure why there would be more than
		// one though
	public:
		Sprite(int index, const uint8_t* data, size_t offset) :
			Resource(index, data, offset) {
			size_t frames = priv::cast<uint32_t>(_raw->ptr_end());
			_masks = data +  sizeof(uint32_t) + sizeof(uint32_t) * frames;
		}
		int width() const { return _raw->width; }
		int height() const { return _raw->height; }
		int left() const { return _raw->left; }
		int right() const { return _raw->right; }
		int bottom() const { return _raw->bottom; }
		int top() const { return _raw->top; }
		bool trasparent() const { return _raw->trasparent != 0; }
		bool smooth() const { return _raw->smooth != 0; }
		bool preload() const { return _raw->width != 0; }
		int mode() const { return _raw->mode; }
		int colcheck() const { return _raw->colcheck; }
		int origin_x() const { return _raw->original_x; }
		int origin_y() const { return _raw->original_y; }
		OffsetList<raw_type::SpriteFrame>& frames() const { return OffsetList<raw_type::SpriteFrame>(_raw->ptr_begin()-_offset, _raw->ptr_end()); }
		size_t mask_count() const { return *reinterpret_cast<const int*>(_masks); }
		size_t mask_stride() const { return (width() + 7) / 8; }
		BitMask mask_at(size_t index) const {
			return BitMask(width(), height(), _masks + sizeof(int) + (mask_stride()*height() * index));
		}
	};
	
	inline bool operator==(const GString& gstr, const std::string& str)  { return str == gstr.c_str(); }
	inline bool operator!=(const GString& gstr, const std::string& str) { return str != gstr.c_str(); }
	inline bool operator==(const std::string& str, const GString& gstr) { return str == gstr.c_str(); }
	inline bool operator!=(const std::string& str, const GString& gstr) { return str != gstr.c_str(); }

	class DataWinFile {
		struct Chunk : CannotCreate<Chunk> {
			union {
				char name[4];
				uint32_t iname;
			};
			uint32_t size;
			uint32_t count;
			uint32_t offsets[1];  // used for fast lookups
		};
		FileHelper _data;
		size_t _full_size;
		std::unordered_map<uint32_t, const Chunk*> _chunks;
		//std::reference_wrapper<Chunk> test
		using chunk_const_iterator = std::unordered_map<uint32_t, const Chunk*>::const_iterator;
		void load_chunks() {
			size_t pos = 0;
			_full_size = 0;
			_chunks.clear();
			while (pos < _data.size()) {
				const Chunk* chunk = Chunk::cast(_data.data(), pos);
				pos += sizeof(uint32_t) * 2; // skip over name and size
											 // have to check swaped value as iname is in little edan?
				if (chunk->iname == chunk_traits<ChunkType::FORM>::swap_value()) {
					_full_size = chunk->size; // form has the size of the file
				}
				else {
					if (!_full_size) { // bad file, should start with FORM
						_chunks.clear();
						return;
					}
					_chunks[chunk->iname] = chunk;
					pos += chunk->size;
				}
			}
		}

	public:
		DataWinFile() {}
		void load(std::vector<uint8_t>&& data) { _data = std::move(data); load_chunks(); }
		void load(const std::vector<uint8_t>& data) { _data = data; load_chunks(); }
		void load(std::istream& is) { _data.load(is); load_chunks(); }
		bool has_data() const { return !_chunks.empty(); }

		size_t size() const { return _full_size; }
		template<class C, class = std::enable_if<priv::is_resource<C>::value>>
		size_t resource_count() const {
			auto it = _chunks.find(chunk_traits<C::ResType>::swap_value());
			if (it == _chunks.end())  throw; // not found
			else return it->second->count;
		}
		template<class C, class = std::enable_if<priv::is_resource<C>::value>>
		C resource_at(size_t index) const {
			auto it = _chunks.find(chunk_traits<C::ResType>::swap_value());
			if (it == _chunks.end()) throw; // not found
			const Chunk* chunk = &(*it->second);
			return C(index, _data.data(), it->second->offsets[index]);
		}
	};
};
	

