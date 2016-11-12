#include "gm_lib.h"
#include <chrono>
#include <random>
#include <type_traits>

namespace {
	namespace _private {
		template<typename T>
		static T generate_seed() {
			// Seed with a real random value, if available
			std::random_device r; // supposed its a seed?
			std::default_random_engine e(r());
			std::uniform_int_distribution<T> uniform_dist(std::numeric_limits<T>::min(), std::numeric_limits<T>::max());
			return uniform_dist(e);
		}
	};


	static size_t luaS_hash(const char *str, size_t l) {
		static const size_t seed = _private::generate_seed<size_t>();
		constexpr size_t LUAI_HASHLIMIT = 5;
		size_t h = seed ^ static_cast<size_t>(l);
		size_t step = (l >> LUAI_HASHLIMIT) + 1;
		for (; l >= step; l -= step)
			h ^= ((h << 5) + (h >> 2) + static_cast<uint8_t>(str[l - 1]));
		return h;
	}
	/*
	** 'module' operation for hashing (size is always a power of 2)
	*/
	template<typename T>
	inline constexpr T lmod(T s, T size) { return  (s) & ((size)-1); }
};

namespace gm {
	// copied from Lua 5.3.3
	class StringTable {
		struct istring {
			// for compiler errors
			istring(const istring&) = delete;
			istring(istring&&) = delete;
			void mark() { _size |= (0x01 << 24); }
			void  unmark() { _size &= ~(0x01 << 24); }
			bool marked() const { return _size & 0xFF000000 != 0; }
			bool fixed() const { return _size & 0x10000000 != 0; }
			size_t size() const { return _size&0x00FFFFFF; }
			size_t _size;
			char str[1];
		};
		union istring_find {
			struct {
				size_t size;
				const char* str;
			} find;
			istring org;
		};
		struct hasher {
			size_t operator()(const istring* l) const { return luaS_hash(l->str, l->size()); };
		};
		struct equaler {
			bool operator()(const istring* l, const istring* r) const { return l->size() == r->size() && ::memcmp(l->str, r->str, sizeof(char)*l->size()) == 0; };
		};
		std::unordered_set<istring*, hasher, equaler> m_string_table;
		std::unordered_set<const String*> m_istrings;
		size_t m_strings_used=0;

		
		void clear_marks() {
			for (auto a : m_string_table) a->unmark(); // a->size &= ~(0x10 << 24);
		}
	public:
		istring*  intern(const char* str, size_t l) {
			istring_find search = { l , str };
			auto it = m_string_table.find(&search.org);
			if (it != m_string_table.end()) return *it;
			istring* ts = reinterpret_cast<istring*>(new char[sizeof(istring) + l]);
			ts->_size = l & 0x00FFFFFF;
			::memcpy(ts->str, str, sizeof(char)*l);
			ts->str[l] = 0;
			m_string_table.emplace(ts);
			return ts;
		}
		static istring s_empty;
		StringTable() {
			m_string_table.emplace(&s_empty);
		}
		void make_perm(istring* s) {
			s->_size |= (0x10 << 24);
		}
		void collect_garbage() {
			for (auto s : m_istrings) {
				istring* str = const_cast<istring*>(reinterpret_cast<const istring*>(s->c_str() - 4));
				str->mark();
			}
			for (auto it = m_string_table.begin(); it != m_string_table.end();) {
				istring* str = (*it);
				if (str->fixed() || str->marked()) {
					it++;
					str->unmark();
				} else {
					it = m_string_table.erase(it);
					delete str;
				}
			}
		}
		void add_string(const String* str) { 
			if (str->c_str() != StringTable::s_empty.str) 
				m_istrings.emplace(str);
		}
		void del_string(const String* str) { 
			if (str->c_str() != StringTable::s_empty.str)
				m_istrings.erase(str);
		}
		void swap_string(String* l, String* r) {
			if (l->m_str != r->m_str) {
				if(l->m_str == s_empty.str) { m_istrings.emplace(l); m_istrings.erase(r); }
				else if(r->m_str == s_empty.str) { m_istrings.emplace(r); m_istrings.erase(l); }
				std::swap(l->m_str, r->m_str);
			}
		}
		void assign_string(String* l, const char* r) {
			if (l->m_str != r) {
				if (l->m_str == s_empty.str)  m_istrings.emplace(l);
				else if (r == s_empty.str)  m_istrings.erase(l); 
				l->m_str = r;
			}
		}
		static const char* cast(const istring* str) { return str->str; }
	};
	StringTable::istring StringTable::s_empty = { (0x10 << 24), 0 };
	static StringTable s_string_table;

	String::String() : m_str(StringTable::s_empty.str) { }
	String::String(const String& a) : m_str(a.m_str) { s_string_table.add_string(this); }
	String::String(String&& a) : String() { s_string_table.swap_string(this, &a); }
	String& String::operator=(const String& a) { s_string_table.assign_string(this, a.m_str); return *this; }
	String& String::operator=(String&& a) { s_string_table.swap_string(this, &a); return *this; }
	String::~String() { s_string_table.del_string(this); }

	void String::assign(const char* str, size_t size) {
		auto p = s_string_table.intern(str, size);
		s_string_table.assign_string(this, p->str);
	}

	String::String(const char* str, size_t l) : m_str(s_string_table.intern(str, l)->str) { s_string_table.add_string(this); }



	static const std::unordered_map<size_t, std::string> key_to_string = {
		{ 0, "NOKEY" },
		{ 1, "ANYKEY" },
		{ 8, "BACKSPACE" },
		{ 9, "TAB" },
		{ 13, "ENTER" },
		{ 16, "SHIFT" },
		{ 17, "CTRL" },
		{ 18, "ALT" },
		{ 19, "PAUSE" },
		{ 27, "ESCAPE" },
		{ 32, "SPACE" },
		{ 33, "PAGEUP" },
		{ 34, "PAGEDOWN" },
		{ 35, "END" },
		{ 36, "HOME" },
		{ 37, "LEFT" },
		{ 38, "UP" },
		{ 39, "RIGHT" },
		{ 40, "DOWN" },
		{ 45, "INSERT" },
		{ 46, "DELETE" },
		{ 48, "0" },
		{ 49, "1" },
		{ 50, "2" },
		{ 51, "3" },
		{ 52, "4" },
		{ 53, "5" },
		{ 54, "6" },
		{ 55, "7" },
		{ 56, "8" },
		{ 57, "9" },
		{ 65, "A" },
		{ 66, "B" },
		{ 67, "C" },
		{ 68, "D" },
		{ 69, "E" },
		{ 70, "F" },
		{ 71, "G" },
		{ 72, "H" },
		{ 73, "I" },
		{ 74, "J" },
		{ 75, "K" },
		{ 76, "L" },
		{ 77, "M" },
		{ 78, "N" },
		{ 79, "O" },
		{ 80, "P" },
		{ 81, "Q" },
		{ 82, "R" },
		{ 83, "S" },
		{ 84, "T" },
		{ 85, "U" },
		{ 86, "V" },
		{ 87, "W" },
		{ 88, "X" },
		{ 89, "Y" },
		{ 90, "Z" },
		{ 96, "NUM_0" },
		{ 97, "NUM_1" },
		{ 98, "NUM_2" },
		{ 99, "NUM_3" },
		{ 100, "NUM_4" },
		{ 101, "NUM_5" },
		{ 102, "NUM_6" },
		{ 103, "NUM_7" },
		{ 104, "NUM_8" },
		{ 105, "NUM_9" },
		{ 106, "NUM_STAR" },
		{ 107, "NUM_PLUS" },
		{ 109, "NUM_MINUS" },
		{ 110, "NUM_DOT" },
		{ 111, "NUM_DIV" },
		{ 112, "F1" },
		{ 113, "F2" },
		{ 114, "F3" },
		{ 115, "F4" },
		{ 116, "F5" },
		{ 117, "F6" },
		{ 118, "F7" },
		{ 119, "F8" },
		{ 120, "F9" },
		{ 121, "F10" },
		{ 122, "F11" },
		{ 123, "F12" },
		{ 144, "NUM_LOCK" },
		{ 145, "SCROLL_LOCK" },

		{ 186, "SEMICOLON" },
		{ 187, "PLUS" },
		{ 188, "COMMA" },
		{ 189, "MINUS" },
		{ 190, "FULLSTOP" },
		{ 191, "FWSLASH" },
		{ 192, "AT" },

		{ 219, "RIGHTSQBR" },
		{ 220, "BKSLASH" },
		{ 221, "LEFTSQBR" },
		{ 222, "HASH" },
		{ 223, "TILD" },
	};
	struct gm_key {
		size_t _key;
		gm_key(size_t key) : _key(key) {}
	};
	std::ostream& operator<<(std::ostream& os, const gm_key& key) {
		auto it = key_to_string.find(key._key);
		if (it == key_to_string.end()) os << "0x" << std::setw(2) << std::setfill('0') << std::hex << key._key;
		else os << it->second;
		return os;
	}
	void EventType::to_stream(std::ostream& os) const  {
		std::string str;
		switch (event())
		{
		case 0: os << "CreateEvent"; return;
		case 1: os << "DestroyEvent"; return;
		case 2:
			if (sub_event() < 12) {
				os << "ObjAlarm(" << sub_event() << ')';
				return;
			}
		case 3:
			switch (sub_event())
			{
			case 0: os << "StepNormalEvent"; return;
			case 1: os << "StepBeginEvent"; return;
			case 2: os << "StepEndEvent"; return;
			}
			break;
		case 4: os << "CollisionEvent(" << sub_event() << ')'; return;
		case 5: os << "KeyEvent(" << gm_key(sub_event()) << ')'; return;
		case 6:
			switch (sub_event()) {
			case 0: os << "LeftButtonDown"; return;
			case 1:os << "RightButtonDown"; return;
			case 2:os << "MiddleButtonDown"; return;
			case 3:os << "NoButtonPressed"; return;
			case 4:os << "LeftButtonPressed"; return;
			case 5:os << "RightButtonPressed"; return;
			case 6:os << "MiddleButtonPressed"; return;
			case 7:os << "LeftButtonReleased"; return;
			case 8: os << "RightButtonReleased"; return;
			case 9: os << "MiddleButtonReleased"; return;
			case 10: os << "MouseEnter"; return;
			case 11: os << "MouseLeave"; return;
			case 16: os << "Joystick1Left"; return;
			case 17: os << "Joystick1Right"; return;
			case 18: os << "Joystick1Up"; return;
			case 19: os << "Joystick1Down"; return;
			case 21: os << "Joystick1Button1"; return;
			case 22: os << "Joystick1Button2"; return;
			case 23: os << "Joystick1Button3"; return;
			case 24: os << "Joystick1Button4"; return;
			case 25: os << "Joystick1Button5"; return;
			case 26: os << "Joystick1Button6"; return;
			case 27: os << "Joystick1Button7"; return;
			case 28: os << "Joystick1Button8"; return;
			case 31: os << "Joystick2Left"; return;
			case 32: os << "Joystick2Right"; return;
			case 33: os << "Joystick2Up"; return;
			case 34: os << "Joystick2Down"; return;
			case 36: os << "Joystick2Button1"; return;
			case 37: os << "Joystick2Button2"; return;
			case 38: os << "Joystick2Button3"; return;
			case 39: os << "Joystick2Button4"; return;
			case 40: os << "Joystick2Button5"; return;
			case 41: os << "Joystick2Button6"; return;
			case 42: os << "Joystick2Button7"; return;
			case 43: os << "Joystick2Button8"; return;
			case 50: os << "GlobalLeftButtonDown"; return;
			case 51: os << "GlobalRightButtonDown"; return;
			case 52: os << "GlobalMiddleButtonDown"; return;
			case 53: os << "GlobalLeftButtonPressed"; return;
			case 54: os << "GlobalRightButtonPressed"; return;
			case 55: os << "GlobalMiddleButtonPressed"; return;
			case 56: os << "GlobalLeftButtonReleased"; return;
			case 57: os << "GlobalRightButtonReleased"; return;
			case 58: os << "GlobalMiddleButtonReleased"; return;
			case 60: os << "MouseWheelUp"; return;
			case 61: os << "MouseWheelDown"; return;
			}
			break;
		case 7:
			switch (sub_event()) {
			case 0: os << "OutsideEvent"; return;
			case 1: os << "BoundaryEvent"; return;
			case 2: os << "StartGameEvent"; return;
			case 3: os << "EndGameEvent"; return;
			case 4: os << "StartRoomEvent"; return;
			case 5: os << "EndRoomEvent"; return;
			case 6: os << "NoLivesEvent"; return;
			case 7: os << "AnimationEndEvent"; return;
			case 8: os << "EndOfPathEvent"; return;
			case 9: os << "NoHealthEvent"; return;
			case 10: os << "UserEvent0"; return;
			case 11: os << "UserEvent1"; return;
			case 12: os << "UserEvent2"; return;
			case 13: os << "UserEvent3"; return;
			case 14: os << "UserEvent4"; return;
			case 15: os << "UserEvent5"; return;
			case 16: os << "UserEvent6"; return;
			case 17: os << "UserEvent7"; return;
			case 18: os << "UserEvent8"; return;
			case 19: os << "UserEvent9"; return;
			case 20: os << "UserEvent10"; return;
			case 21: os << "UserEvent11"; return;
			case 22: os << "UserEvent12"; return;
			case 23: os << "UserEvent13"; return;
			case 24: os << "UserEvent14"; return;
			case 25: os << "UserEvent15"; return;
			case 30: os << "CloseButtonEvent"; return;
			case 40: os << "OutsideView0Event"; return;
			case 41: os << "OutsideView1Event"; return;
			case 42: os << "OutsideView2Event"; return;
			case 43: os << "OutsideView3Event"; return;
			case 44: os << "OutsideView4Event"; return;
			case 45: os << "OutsideView5Event"; return;
			case 46: os << "OutsideView6Event"; return;
			case 47: os << "OutsideView7Event"; return;
			case 50: os << "BoundaryView0Event"; return;
			case 51: os << "BoundaryView1Event"; return;
			case 52: os << "BoundaryView2Event"; return;
			case 53: os << "BoundaryView3Event"; return;
			case 54: os << "BoundaryView4Event"; return;
			case 55: os << "BoundaryView5Event"; return;
			case 56: os << "BoundaryView6Event"; return;
			case 57: os << "BoundaryView7Event"; return;
			case 58: os << "AnimationUpdateEvent"; return;
			case 60: os << "WebImageLoadedEvent"; return;
			case 61: os << "WebSoundLoadedEvent"; return;
			case 62: os << "WebAsyncEvent"; return;
			case 63: os << "WebUserInteractionEvent"; return;
			case 66: os << "WebIAPEvent"; return;
			case 67: os << "WebCloudEvent"; return;
			case 68: os << "NetworkingEvent"; return;
			case 69: os << "SteamEvent"; return;
			case 70: os << "SocialEvent"; return;
			case 71: os << "PushNotificationEvent"; return;
			case 72: os << "AsyncSaveLoadEvent"; return;
			case 73: os << "AudioRecordingEvent"; return;
			case 74: os << "AudioPlaybackEvent"; return;
			case 75: os << "SystemEvent"; return;
			}
			break;
		case 8:
			switch (sub_event())
			{
			case 64: os << "DrawGUI"; return;
			case 65: os << "DrawResize"; return;
			case 72: os << "DrawEventBegin"; return;
			case 73: os << "DrawEventEnd"; return;
			case 74: os << "DrawGUIBegin"; return;
			case 75: os << "DrawGUIEnd"; return;
			case 76: os << "DrawPre"; return;
			case 77: os << "DrawPost"; return;
			default: os << "DrawEvent"; return;
			}
			break;
		case 9:  os << "KeyPressed(" << gm_key(sub_event()) << ')'; return;
		case 10: os << "KeyReleased(" << gm_key(sub_event()) << ')'; return;
		case 11: os << "Trigger(" << sub_event() << ')'; return;
		}
		os << "Unknown(" << event() << ',' << sub_event() << ')';
	}
}; 