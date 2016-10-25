#pragma once
#include <vector>
#include <queue>
#include <cassert>
#include <array>
#include <mutex>
#include <condition_variable>


struct Term
{
	enum  Attribute {
		 NORMAL = 0,
	 BOLD = 1,
	 UNDER = 2,
	 BLINK = 4,
	 REVERSE = 8,
	 SINGLE_W_H = 16,
	 DOUBLE_W = 32,
	 DOUBLE_T = 64,
	 DOUBLE_B = 128
	};
	virtual int getRowCount() = 0;
	virtual int getColumnCount() = 0;
	virtual int getCharWidth() = 0;
	virtual int getCharHeight() = 0;
	virtual void setFont(int paramInt) = 0;
	virtual void setCursor(int paramInt1, int paramInt2) = 0;
	virtual void setLEDs(int paramInt) = 0;
	virtual void clear() = 0;
	virtual void draw_cursor() = 0;
	virtual void redraw(int paramInt1, int paramInt2, int paramInt3, int paramInt4) = 0;
	virtual void clear_area(int paramInt1, int paramInt2, int paramInt3, int paramInt4) = 0;
	virtual void scroll_window(int paramInt1, int paramInt2, int paramInt3, uint32_t paramColor, bool paramBoolean) = 0;
	virtual void drawBytes(const uint8_t*  paramArrayOfByte, int paramInt1, int paramInt2, int paramInt3, int paramInt4) = 0;
	virtual void drawChars(const wchar_t* paramArrayOfChar, int paramInt1, int paramInt2, int paramInt3, int paramInt4) = 0;
	virtual void drawString(const std::string& paramString, int paramInt1, int paramInt2) = 0;
	virtual void beep() = 0;

	virtual bool sendOutByte(int paramInt) = 0;
	virtual void sendKeySeq(int key) = 0;
	template<size_t N>
	void sendKeySeq(int keys[N] keys) {
		for (size_t i = 0; i < N; i++) sendKeySeq(keys[i]);
	}
	template <class ...Args>
	void sendKeySeq(Args&& ...args) {
		sendKeySeq(Args...);
	}
	virtual void setFGround(uint32_t paramColor) = 0;
	virtual void setBGround(uint32_t paramColor) = 0;
	virtual uint32_t getFGround()=0;
	virtual uint32_t getBGround() = 0;
};

class vt_terminal_parser {
protected:
	enum class State {
		ground,
		escape,
		csi,
		dcs,
		ignore
	};
	State _state;
	std::vector<wchar_t> _escape_text;
	std::array<int, 16> _parms;
	int _parm_position = 0;
	int csi_private_parm = 0;
	virtual void clear_parser() {
		_parm_position = 0;
		_parms.fill(0);
		_escape_text.clear();
		csi_private_parm = 0;
	}
	// is a control charater
	bool is_execute(wchar_t c) const {
		if ((c >= 0x00 && c <= 0x17) || c == 0x19 || (c >= 0x1C && c <= 0x1F)) return true; // execute, happens in any state
		return false;
	}
	int parse(wchar_t c) {
		if (is_execute(c)) return c;
		if (_state == State::ignore) {
			if (!is_execute(c) && c != 0x7f && !(c >= 0x40 && c <= 0x7e)) _state = State::ground;
			else return;
		}
		switch (c) {
		case 0x7f: // ignore
			return -1;
		case 0x1B:
			clear_parser();
			_state = State::escape;
			_escape_text.push_back(c);
			return -1;
		}
		switch (_state) {
		case State::ground: return c; break;
		case State::escape:
			_escape_text.push_back(c);
			if (c == '[')
				_state = State::csi;
			else if (c >= ' ' && c <= '/')
				_parms[_parm_position++] = c;
			else if (c >= '0' && c <= '~') {
				esc_dispatch(c);
				_state = State::ground;
			}
			else
				_state = State::ignore; // bad
			return -1;
		case State::csi:
			if (c >= '>' && c <= '?')
				csi_private_parm = c;
			else if (c >= '0' && c < '9')
				_parms[_parm_position] = _parms[_parm_position] * 10 + (c - '0');
			else if (c == ';')
				_parm_position++;
			else if (c >= '@' && c <= '`') {
				csi_dispatch(c);
				_state = State::ground;
			}
			else
				_state = State::ignore; // bad
			return -1;

		case State::ignore:
			//	if (!(c >= 0x40 && c <= 0x7e))
			_state = State::ground;
			return c; // figure out error correction
		}
		return c; 
	}
	virtual void esc_dispatch(int ch) = 0;
	virtual void csi_dispatch(int ch) = 0;
};

class char_glyph {
	std::vector<bool> _data;
	size_t _width, _height;
public:
	char_glyph(size_t width, size_t height) : _width(width), _height(height), _data(width*height) {}
	size_t width() const { return _width; }
	size_t height() const { return _height; }
	const std::vector<bool>& vector() const { return _data; }
	std::vector<bool>& vector()  { return _data; }
	bool pixel(size_t x, size_t y) const { return _data.at(x + y * _height); }
	void pixel(size_t x, size_t y, bool value) { _data.at(x + y * _height)=value; }

	template<typename T>
	void blit_to(T* image, size_t image_stride, T bg, T fg) const{
		auto it = _data.begin();
		for (size_t y = 0; y < _height; y++) {
			auto iline = image + image_stride * y;
			for (size_t x = 0; x < _width; x++,it++) {
				*iline = *it ? fg : bg;
			}
		}
	}
};

template<typename T, typename = std::enable_if<std::is_trivially_copyable<T>::value>>
class char_queue {
	std::queue<T> _data;
	std::mutex _mutex;
	std::condition_variable _cv;
public:
	// not sure if we need it copyable or movable but just in case
	char_queue(char_queue&& r) {
		std::lock_guard guard(r._mutex);
		_data = std::move(r._data);
	}
	char_queue& operator= (char_queue&& r)
	{
		if (&l == this) return *this;
		std::unique_lock lk1(_mutex, std::defer_lock);
		std::unique_lock lk2(r._mutex, std::defer_lock);
		std::lock(lk1, lk2);
		_queue = std::move(r._queue);
		return *this;
	}
	char_queue(const char_queue& r)
	{
		std::lock_guard guard(r._mutex);
		_data = r._data;
	}
	char_queue& operator= (const char_queue& r)
	{
		if (&l == this) return *this;
		std::unique_lock lk1(_mutex, std::defer_lock);
		std::unique_lock lk2(r._mutex, std::defer_lock);
		std::lock(lk1, lk2);
		_queue = r._queue;
		return *this;
	}
	bool pop_try(T& data) {
		if (!_mutex.try_lock()) return false;
		_cv.tr
		bool ret = _data.empty();
		if (!ret) {
			data = _data.front();
			_data.pop();
		}
		_mutex.unlock();
		return ret;
	}
	uint8_t pop_wait() {
		std::unique_lock<std::mutex> lk(_mutex); // mutex scope lock
		_cv.wait(lk, [this]() { return !_data.empty(); });
		uint8_t ret = _data.front();
		_data.pop();
		return ret;
	}
	template <class ...Args>
	void emplace(Args&& ...args) {
		std::unique_lock<std::mutex> lk(_mutex);
		_data.emplace(std::forward<Args>(args...));
		lk.unlock();
		_cv.notify_one(); // notify one waiting thread
	}
	void push(const T& data) {
		std::unique_lock<std::mutex> lk(_mutex);
		_data.push(data);
		lk.unlock();
		_cv.notify_one(); // notify one waiting thread
	}
	template<typename U>
	void push(U&& data) {
		std::unique_lock<std::mutex> lk(_mutex);
		_data.push(std::forward<T>(data));
		lk.unlock();
		_cv.notify_one(); // notify one waiting thread
	}
};
class vt100_parser : public vt_terminal_parser {
protected:
	enum  CharAttributes : uint16_t
	{
		None = 0x00,
		Blink = 0x01,
		Underline = 0x02,
		Bold = 0x04,
		Inverted = 0x08,
		AltChar = 0x10
	};
	enum  LineAttributes : uint16_t
	{
		None = 0x00,
		DoubleHeightTopHalf = 0x01,
		DoubleHeightBottomHalf = 0x02,
		DoubleWidth = 0x04,
		ScrollRegion = 0x08
	};
	struct CharAttribute {
		uint32_t _attrib = 0;
	public:
		uint8_t forground() const { return (_attrib >> 20) & 0xf; }
		uint8_t background() const { return (_attrib >> 16) & 0xf; }
		CharAttributes attributes() const { return static_cast<CharAttributes>(_attrib & 0xFFFF); }
		void forground(uint8_t color) { _attrib = (_attrib & 0x0FFFFF) | ((color & 0xF) << 20); }
		void background(uint8_t color) { _attrib = (_attrib & 0xF0FFFF) | ((color & 0xF) << 16); }
		bool operator==(const CharAttribute& r) const { return _attrib == r._attrib; }
		bool operator!=(const CharAttribute& r) const { return _attrib != r._attrib; }
		void addAttrib(CharAttributes a) { _attrib = (_attrib & 0xFFFF0000) & static_cast<uint32_t>((attributes() + a)); }
		void removeAttrib(CharAttributes a) { _attrib = (_attrib & 0xFFFF0000) & static_cast<uint32_t>((attributes() - a)); }
		bool hasAttrib(CharAttributes a) const { return static_cast<uint32_t>(a) & _attrib != 0; }
	};
	struct CharInfo {
		wchar_t ch;
		CharAttribute attrib;
	};
	struct Line {
		LineAttributes attrib;
		std::vector<CharInfo> line;
		int first = -1;
		int last = -1;
		void swap(Line& other) {
			std::swap(other.attrib, attrib);
			std::swap(other.line, line);
			std::swap(other.first, first);
			std::swap(other.last, last);
		}
	};
	std::vector<Line> _lines;
	bool screen_rev = false;
	std::vector<CharInfo> _char_buffer;
	CharAttribute _defaultAttrib;
	CharAttribute _blankAttrib;
	int _topMargin = 0;
	int _bottomMargin = 23;
	
public:
	virtual void esc_dispatch(int ch) override {

	}
	virtual void csi_dispatch(int ch) override {

	}
	void putch(int x, int y, wchar_t c) {
		auto& line = _lines[y];
		if (line.line.size() < x) {
			if (line.first > x) line.first = x;
			if (line.last < x) line.last = x;
			while (x + 1 > line.line.size()) line.line.push_back({ ' ', _blankAttrib });
		}
		CharInfo& info = line.line.at(x);
		if (c != info.ch || _defaultAttrib != info.attrib) {
			info.ch = c;
			info.attrib = _defaultAttrib;
			if (line.first > x) line.first = x;
			if (line.last < x) line.last = x;
		}
	}
	void putstr(int x, int y, const std::string& str) {
		for (auto c : str) putch(x++, y, c);
	}
	void scroll(int i) {
		if (i > 0) { // scroll down
			while (i-- > 0) {
				for (size_t l = _topMargin; l < _bottomMargin; l++) {
					_lines[l].swap(_lines[l + 1]);
				}
				_lines[_bottomMargin].line.clear();
			}
		}
		else {
			while (i++ < 0) {
				for (size_t l = _bottomMargin; l > _topMargin; l--) {
					std::swap(_lines[l], _lines[l - 1]);
				}
			}
			_lines[_topMargin].line.clear();
		}
		for (auto& l : _lines) { l.first = 0; l.last = l.line.size()-1; }
	}

};


