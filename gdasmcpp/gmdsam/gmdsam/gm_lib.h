#pragma once
#include "global.h"
#include <variant>
#include <cassert>

#define OLD_XML_WRITER
#ifndef OLD_XML_WRITER
#include "ticpp\ticpp.h"
#endif

#define USE_SYMBOL
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


#ifdef USE_SYMBOL
	using Symbol = util::symbol;
	using StringView = std::string_view;
#else
	class String {
	public:
		String::String() : m_str(s_empty_string) { }
		String(const String& a);
		String(String&& a);
		String& operator=(const String& a);
		String& operator=(String&& a);
		~String();

		void assign(const char* str, size_t size);
		void assign(const std::string& str) { assign(str.c_str(), str.size()); }
		String(const char* str, size_t size);
		String(const std::string& str) : String(str.c_str(), str.size()) {}
		String(const char* str) : String(str, ::strlen(str)) {}
		bool empty() const { return m_str == s_empty_string; }
		operator const char*() const { return m_str; }

		String& operator=(const char* str) { assign(str, ::strlen(str)); return *this; }
		String& operator=(const std::string& str) { assign(str); return *this; }

		const char* c_str() const { return m_str; }
		size_t size() const { return *reinterpret_cast<const size_t*>(m_str - 4) & 0x00FFFFFF; }
		const char* begin() const { return m_str; }
		const char* end() const { return m_str + size(); }
		char at(size_t i) const { return m_str[i]; }
		char operator[](size_t i) const { return m_str[i]; }
		friend inline bool operator==(const String& l, const String& r) { return l.m_str == r.m_str; }
		friend inline bool operator!=(const String& l, const String& r) { return l.m_str != r.m_str; }
		friend inline std::ostream& operator<<(std::ostream& os, const String& s) { os << s.c_str(); return os; }
	private:
		friend class StringTable;
		const char* m_str;
		static const char* s_empty_string;
	};
#endif

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

	class GMException : public std::exception {
		std::string _msg;
	public:
		GMException(const std::string& msg) : _msg(msg) {}
		GMException(std::string&& msg) : _msg(std::move(msg)) {}
		virtual char const* what() const override { return _msg.c_str(); }
	};

	class SimpleXmlWriter {
	public:
		using string_type = std::string_view;
		struct XmlElement;
		using XmlElementPtr = std::unique_ptr<XmlElement>;
		using value_type = std::variant<string_type, int, float, XmlElementPtr>;
		using container_type = std::unordered_map<string_type, value_type>;
		struct to_stream_helper {
			std::ostream& os;
			util::StreamIdent& ident;
			to_stream_helper(std::ostream& os, util::StreamIdent& ident) : os(os), ident(ident) {}
			void output_attributes(const XmlElement& v) const {
				if (!v.attributes.empty()) {
					for (const auto& a : v.attributes) {
						const auto& elm = a.second;
						os << ' ';
						os << a.first << "=\"";
						std::visit(*this, elm);
						os << "\"";
					}
				}
			}
			bool operator()(const std::string_view& v) const { os << v; return false; }
			bool operator()(const float v) const { 
				os << std::defaultfloat << v; // we atleast want one decimal
				if (v == (int)v) os << ".0";
				return false; 
			}
			bool operator()(const int v) const { os << std::setfill(' ') << std::setw(18) << std::dec <<  v; return false; }

			bool operator()(const XmlElement& v) const {
				os << '<' << v.name;
				output_attributes(v);

				if (v.elements.empty()) { os << "/>"; return false; }
				os << '>';
				// we assum the front tag is printed, we don't care bout the name
				// special case with one tag, purely for formatting
				if (v.elements.size() == 1 && !std::holds_alternative<XmlElementPtr>(v.elements.front())) {
					std::visit(*this, v.elements.front());
					// end tag is anotmaticly printed here as well as endln
				} else {
					ident++;
					os  << std::endl;
					os << ident;
					bool endline = false;
					for (const auto& e : v.elements) {
						std::visit(*this, e);
						os << std::endl;
						os << ident;
					}
					os << std::endl;

					ident--;
				}
				os << ident;
				os  << "</" << v.name << '>';
				return false;
			}
			bool operator()(const XmlElementPtr& v) const { return operator()(*v.get()); }
		};
		struct XmlElement {
			std::vector<value_type> elements;
			std::unordered_map<string_type, std::variant<string_type, int, float>> attributes;
			string_type name;
			XmlElement(const std::string_view& name) : name(name) {}
			XmlElement&  AddTag(float v) { elements.emplace_back(v); return *this; }
			XmlElement&  AddTag(int v) { elements.emplace_back(v); return *this; }
			XmlElement&  AddTag(const string_type& v) { elements.emplace_back(v); return *this;}
			XmlElement& AddElement(const string_type& v) {
				XmlElement* e = new XmlElement(v);
				elements.emplace_back(std::move(std::unique_ptr<XmlElement>( e)));
				return *e;
			}
			XmlElement& AddElement(const string_type& v, float value) {
				return AddElement(v).AddTag(value);
			}
			XmlElement& AddElement(const string_type& v, int value) {
				return AddElement(v).AddTag(value);
			}
			XmlElement& AddElement(const string_type& v, const string_type& value) {
				return AddElement(v).AddTag(value);
			}
			void AddAttribute(const string_type& key, int value) { attributes.emplace(key, value); }
			void AddAttribute(const string_type& key, float value) { attributes.emplace(key, value); }
			void AddAttribute(const string_type& key, const string_type& value) { attributes.emplace(key, value); }

		};
		XmlElement _root;
	public:
		SimpleXmlWriter() : _root("xml") { _root.AddAttribute("version", 1.0f); }
		XmlElement& AddElement(const string_type& s) { return _root.AddElement(s); }
		void save(const std::string& filename) {
			std::ofstream file(filename);
			if (file.good()) {
				to_stream(file);
			}
			file.close();
		}
		void to_stream(std::ostream& os) const {
			util::StreamIdent ident;
			to_stream_helper _helper(os, ident);

			os << "<?" << _root.name;
			_helper.output_attributes(_root);
			os << "?>";// << std::endl;
			// should we check to make sure each is an element?
			_helper(_root);
			os << std::endl;
		}
	};
};

// custom specialization of std::hash can be injected in namespace std
namespace std
{
	template<> struct hash<gm::EventType>
	{
		size_t operator()(const gm::EventType & e) const { return e._event; }
	};
#ifndef USE_SYMBOL
	template<> struct hash<gm::String>
	{
		size_t operator()(const gm::String & s) const { return std::hash<const void*>()(s.c_str()); }
	};
#endif
}
namespace gm {
	class DataWinFile;
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
	};
	class Offsets : public util::PointerArray<const uint32_t> {
	public:
		Offsets() : PointerArray() {  }
		Offsets(const uint8_t* ptr) : PointerArray(reinterpret_cast<const uint32_t*>(ptr + sizeof(uint32_t)), *reinterpret_cast<const uint32_t*>(ptr)) {  }
		Offsets(const uint8_t* ptr, size_t offset) : Offsets(ptr + offset) {}
		void to_debug(std::ostream& os) const {
			PointerArray::to_debug(os);
		}
	};
	template<typename VALUE_T>
	class OffsetList {
		const uint8_t* _data;
		Offsets _list;
	public:
		typedef typename std::bidirectional_iterator_tag iterator_category;
		typedef typename VALUE_T value_type;
		typedef typename long difference_type;
		typedef typename VALUE_T* pointer;
		typedef typename VALUE_T& reference;
		typedef typename util::GenericIterator<VALUE_T, OffsetList> iterator;
		OffsetList(const uint8_t* ptr, size_t offset) : _data(ptr), _list(ptr, offset) {}
		OffsetList(const uint8_t* ptr, const uint8_t* list) : _data(ptr), _list(list) {}
		size_t size() const { return _list.size(); }
		const VALUE_T* at(size_t i) const { return reinterpret_cast<const VALUE_T*>(_data + _list.at(i)); }
		iterator begin() const { return iterator(*this, 0); }
		iterator end() const { return iterator(*this, size()); }
		void to_debug(std::ostream& os) const {
			for (size_t i = 0; i < size(); i++) {
				size_t offset = _list.at(i);
				os.stream() << std::setw(5) << std::left << i << ": ";
				os << :ext::offset(_list.at(i)) << " = ";
				os.write(at(i));
				os << std::end;
			}
		}
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

	class FileHelperException : public GMException {
	public:
		FileHelperException(const std::string& msg) : GMException("FileHelper: " + msg) {}
	};

	class FileHelper {
		std::vector<uint8_t> _data;
		size_t _pos;
		std::vector<uint32_t> _offsets; // used for offset lists
	public:
		FileHelper() : _pos(0) {}
		void load(const std::vector<uint8_t>& data) { _data = data;  _pos = 0; }
		void load(std::vector<uint8_t>&& data) { _data = std::move(data);  _pos = 0; }
		void load(std::istream& is) {
			is.seekg(0, std::ios::end);
			size_t size = size_t(is.tellg());
			is.seekg(std::ios::beg, 0);
			_data.resize(size);
			is.read(reinterpret_cast<char*>(_data.data()), size);
		}
		void load(const std::string& filename) {
			//std::ifstream fs(filename, std::ifstream::beg | std::ifstream::binary);
			std::ifstream fs(filename, std::ifstream::ate | std::ifstream::binary);
			if (fs.bad()) throw FileHelperException("Could not open file '" + filename + "'");
			load(fs);
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
		template<typename T, typename = std::enable_if<std::is_convertible<T, uint32_t>::value>::type>
		IndexInterface(uint32_t offset, T index) : OffsetInterface(offset), _index(static_cast<uint32_t>(index)) {}
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
		StringView _name;
	public:
		NameInterface(uint32_t offset, uint32_t index, StringView name) : _name(name), IndexInterface(offset, index) {}
		StringView name() const { return _name; }
		bool valid_name() const { return !_name.empty(); }
		bool operator<(const NameInterface& other) const { return IndexInterface::operator<(other); }
		bool operator==(const IndexInterface& other) const { return IndexInterface::operator==(other); }
		bool operator!=(const IndexInterface& other) const { return IndexInterface::operator!=(other); }
		virtual ~NameInterface() {}
		virtual void to_stream(std::ostream& os) const override {
			OffsetInterface::to_stream(os);
			os << '(' << std::setfill(' ') << std::setw(4) << _index << ':';
			if (valid_name())
				os << _name;
			else
				os << NONAME;
			os << ')';
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
	class Resource : public std::conditional<ResourceTraits<RAW_T, CT>::HasNameOffset, NameInterface, IndexInterface>::type, public ResourceTraits<RAW_T, CT> {
	protected:
		typedef typename std::conditional<ResourceTraits<RAW_T, CT>::HasNameOffset, NameInterface, IndexInterface>::type base_type;
		const RAW_T* _raw;
		struct _index_test {};
		struct _name_test : _index_test {};
		template<typename T = RAW_T, typename = std::enable_if<!HasNameOffset>::type>
		Resource(uint32_t index, const uint8_t* data, uint32_t offset, _name_test) : _raw(reinterpret_cast<const RAW_T*>(data + offset)), base_type(offset, index) { }
		template<typename T = RAW_T, typename = std::enable_if<HasNameOffset>::type>
		Resource(uint32_t index, const uint8_t* data, uint32_t offset, _index_test) : _raw(reinterpret_cast<const RAW_T*>(data + offset)), base_type(offset, index, reinterpret_cast<const char*>(data + reinterpret_cast<const RAW_T*>(data + offset)->name_offset)) { }
	public:
		Resource() : _name(), _index(-1), _raw(nullptr) {}
		bool valid() const { return _raw != nullptr; }
		const RAW_T* raw() const { return _raw; }
		template<typename T, typename = std::enable_if<std::is_convertible<T, uint32_t>::value>::type> // index handles the conversion
		Resource(T index, const uint8_t* data, uint32_t offset) : Resource(static_cast<uint32_t>(index), data, offset, _name_test{}) {}
	};


	class XMLResourceExportInterface {
	public:
		virtual ~XMLResourceExportInterface() {}
		template<typename T>
		static inline std::string xml_export_filename(ChunkType CT, T&& name) {
			std::string filename(std::forward<T>(name));
			switch (CT) {
			case ChunkType::FONT: filename += ".font"; break;
			case ChunkType::SPRT: filename += ".sprite"; break;
			default:
				break;
			}
			filename += ".gmx";
			return filename;
		}
		virtual void xml_export(std::ostream& os) const {};

		virtual bool xml_export(const std::string& path) const = 0;
		template<typename T>
		bool xml_export(T&& s) const {
			const std::string tmp(std::forward<T>(s));
			return xml_export(tmp);
		}
	};
	namespace raw_type {
		// these are all the internal raw types in game maker that I could derive
		// the exception will be func/code as the two versions I know about have diffrent structures

#pragma pack(push, 1)

		// All strings ARE null terminated within a data.win file.  However when you read offsets the point to 
		// the string itself and NOT to this structure.  This structure is only in the STNG chunk
		struct String : CannotCreate<raw_type::String> {
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
	class String : public Resource<raw_type::String, ChunkType::STRG> {
	public:
		String(int index, const uint8_t* data, uint32_t offset) : Resource(index, data, offset) {}
		struct StringHasher {
			std::hash<StringView> _hasher;
			size_t operator()(const String& s) const { return _hasher(s); }
		};
		StringView strv() const { return StringView(_raw->u_str, _raw->length); }
		size_t size() const noexcept { return _raw->length; }
		size_t length() const noexcept { return _raw->length; }
		operator StringView() const noexcept { return strv(); } // main conversion function
		bool operator==(const String& r) const noexcept { return _raw == r._raw; }
		bool operator==(const StringView& r) const noexcept { return strv() == r; }
		bool operator==(const std::string& r) const noexcept { return strv() == r; }
		template<typename T> bool operator!=(const T& r) const noexcept { return !(*this == r); }
		const char* begin() const { return _raw->u_str; }
		const char* end() const { return _raw->u_str + _raw->length; }
	};
	// we have to cut the namespace here to use std::hash
};
template<typename C, typename E>
static inline std::basic_ostream<C, E>& operator<<(std::basic_ostream<C, E>& os, const gm::String& s) noexcept {
	os << s.strv();
	return os;
}
namespace std {
	template<>
	struct hash<gm::String> {
		std::hash<gm::StringView> _hasher;
		size_t operator()(const gm::String& r) const { return _hasher(r); }
	};
}

namespace gm {

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
		Background(int index, const uint8_t* data, uint32_t offset) :
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
		Room(int index, const uint8_t* data, uint32_t offset) : Resource(index, data, offset) {}
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
		Sound(int index, const uint8_t* data, uint32_t offset) : Resource(index, data, offset) {}

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
		Font(int index, const uint8_t* data, uint32_t offset) : Resource(index, data, offset)
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
		Action(const EventType& type, const uint8_t* data, uint32_t offset)
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
	class GString : public Resource<raw_type::String, ChunkType::STRG> {
	public:
		GString(int index, const uint8_t* data, uint32_t offset)
			: Resource(index, data, offset) {}

	};

	class Object : public Resource<raw_type::Object, ChunkType::OBJT> {
		const raw_type::ObjectPhysicsVert* _physics_verts;
		std::unordered_map<EventType, Action> _events;
	public:
		Object(int index, const uint8_t* data, uint32_t offset)
			: Resource(index, data, offset)
			, _physics_verts(_raw->physics_vert_count > 0 ? raw_type::ObjectPhysicsVert::cast(_raw->ptr_end()) : nullptr)
		{
			auto ptr = _raw->ptr_end();
			if (_raw->physics_vert_count > 0) ptr += _raw->physics_vert_count * sizeof(raw_type::ObjectPhysicsVert);
			Offsets root(ptr);
			//debug::debug_ptr<uint32_t> d(data,RawResourceSize+offset, 24);
			if (root.size() != 12) throw; // should always = 12?
			// ugh so the first 12 
			ptr += sizeof(uint32_t) * 12;
			debug::cerr << "Root:" << std::endl << root << std::endl;
			debug::cerr << std::endl;
			// might be a way to template this to caculate the offsets but right now this works
			for (uint32_t i = 0; i < root.size(); i++) {
				//int sub_event = util::cast<int>(data + e);
				Offsets list(data + root.at(i));
				//ptr += sizeof(uint32_t);
				if (list.size() == 0)  continue;  
				debug::cerr << "list:" << std::endl;
				debug::cerr << list;
				debug::cerr << std::endl;
				for (uint32_t e : list) {
					int sub_event = util::cast<int>(data + e);
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

	class Sprite : public Resource<raw_type::Sprite, ChunkType::SPRT>, public XMLResourceExportInterface {
		const uint8_t* _masks;
		// kind of a hack.  First number is an int of the size, after that
		// its just an array of masks, not sure why there would be more than
		// one though
	public:
		Sprite(int index, const uint8_t* data, uint32_t offset) :
			Resource(index, data, offset) {
			size_t frames = util::cast<uint32_t>(_raw->ptr_end());
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
		OffsetList<raw_type::SpriteFrame> frames() const { return OffsetList<raw_type::SpriteFrame>(_raw->ptr_begin()-_offset, _raw->ptr_end()); }
		size_t mask_count() const { return *reinterpret_cast<const int*>(_masks); }
		size_t mask_stride() const { return (width() + 7) / 8; }
		BitMask mask_at(size_t index) const {
			return BitMask(width(), height(), _masks + sizeof(int) + (mask_stride()*height() * index));
		}
		virtual void xml_export(std::ostream& os) const {
			SimpleXmlWriter xml;
			auto& sprite = xml.AddElement("sprite");
			sprite.AddElement("type", _raw->mode);
			sprite.AddElement("xorg", _raw->original_x);
			sprite.AddElement("yorigin", _raw->original_y);
			sprite.AddElement("colkind", _raw->colcheck);
			sprite.AddElement("sepmasks", 0);
			sprite.AddElement("bbox_left", _raw->left);
			sprite.AddElement("bbox_right", _raw->right);
			sprite.AddElement("bbox_top", _raw->top);
			sprite.AddElement("bbox_bottom", _raw->bottom);
			sprite.AddElement("HTile", 0);
			sprite.AddElement("VTile", 0);
			auto& texturegroups = sprite.AddElement("TextureGroups");
			texturegroups.AddElement("TextureGroup0", frames().at(0)->texture_index);
			sprite.AddElement("For3D", 0);
			sprite.AddElement("width", _raw->width);
			sprite.AddElement("height", _raw->height);
			auto& e_frames = sprite.AddElement("frames");
			std::string n;
			auto& ff = frames();
			for (size_t i = 0; i < ff.size(); i++) {
				const auto & f = ff.at(i);
				auto& e_frame = e_frames.AddElement("frame");
				e_frame.AddAttribute("index", (int)i);
				n = name();
				n += "_" + std::to_string(i) + ".png";
				e_frame.AddTag(n);
			}
			xml.to_stream(os);
		}
		virtual bool xml_export(const std::string& path) const {
			SimpleXmlWriter xml;
			std::string filename = path + '\\' + XMLResourceExportInterface::xml_export_filename(ChunkType::SPRT, name());
			std::ofstream file(filename);
			if (!file.good()) {
				debug::cerr << "Could not open filename for xml write '" << filename << '"' << std::endl;
				return false;
			}
			xml_export(file);
			file.close();
			return true;
		}
	};
	
	class DataWinFileException : public GMException {
	public:
		DataWinFileException(const std::string& msg) : GMException("Data Win File: " + msg) {}
	};
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
		//std::vector<std::string_view> _stringtable;
		std::vector<String> _stringtable;
		//std::reference_wrapper<Chunk> test
		using chunk_const_iterator = std::unordered_map<uint32_t, const Chunk*>::const_iterator;
		void fill_stringtable(); // used to get the string table with all the strings from datawin
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
			fill_stringtable();
		}
		template<ChunkType C>
		const Chunk* get_chunk() const {
			auto it = _chunks.find(chunk_traits<C>::swap_value());
			if (it == _chunks.end()) throw DataWinFileException("Chunk type not found"); // not found
			return &(*it->second);
		}
	public:
		DataWinFile() {}
		void load(std::vector<uint8_t>&& data) { _data = std::move(data); load_chunks(); }
		void load(const std::vector<uint8_t>& data) { _data = data; load_chunks(); }
		void load(std::istream& is) { _data.load(is); load_chunks(); }
		void load(const std::string& filename) { _data.load(filename); load_chunks(); }
		bool has_data() const { return !_chunks.empty(); }

		size_t size() const { return _full_size; }
		template<class C, class = std::enable_if<priv::is_resource<C>::value>>
		size_t resource_count() const {
			auto it = _chunks.find(chunk_traits<C::ResType>::swap_value());
			if (it == _chunks.end())  throw; // not found
			else return it->second->count;
		}
		template<class C, class = std::enable_if<priv::is_resource<C>::value>>
		C resource_at(uint32_t index) const {
			auto it = get_chunk<C::ResType>();
			return C(index, _data.data(), it->offsets[index]);
		}
	};
};
	

