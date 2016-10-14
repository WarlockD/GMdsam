#pragma once
#include "global.h"

enum class CharAttributes : uint16_t
{
	None = 0x00,
	Blink = 0x01,
	Underline = 0x02,
	Bold = 0x04,
	Inverted = 0x08,
	AltChar = 0x10
};
MAKE_CLASSENUM_OPERATIONS(CharAttributes)
enum class LineAttributes : uint16_t
{
	None = 0x00,
	DoubleHeightTopHalf = 0x01,
	DoubleHeightBottomHalf = 0x02,
	DoubleWidth = 0x04,
	ScrollRegion = 0x08
};
MAKE_CLASSENUM_OPERATIONS(LineAttributes)


class TerminalView : public CDoubleBufferWindowImpl<TerminalView, CWindow, CFrameWinTraits > {
	struct CharAttribute {
		uint32_t _attrib=0;
	public:
		uint8_t forground() const { return (_attrib >> 20) & 0xf; }
		uint8_t background() const { return (_attrib >> 16) & 0xf; }
		CharAttributes attributes() const { return static_cast<CharAttributes>(_attrib & 0xFFFF); }
		void forground(uint8_t color)   { _attrib = (_attrib & 0x0FFFFF) | ((color &0xF)  << 20); }
		void background(uint8_t color)  { _attrib = (_attrib & 0xF0FFFF) | ((color & 0xF) << 16); }
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
		bool changed = true;
		void swap(Line& other) {
			std::swap(other.attrib, attrib);
			std::swap(other.line, line);
			std::swap(other.changed, changed);
		}
	};
	std::vector<Line> _lines;
	bool screen_rev = false;
	std::vector<CharInfo> _char_buffer;
	CharAttribute _defaultAttrib;
	CharAttribute _blankAttrib;
	std::vector<uint8_t> _char_rom;
	CImage _screen;
	int _topMargin=0;
	int _bottomMargin=23;
//	std::atomic_flag _vrequest = { 0 };
//	std::atomic_flag _vrupdated = { 0 };
public:
	TerminalView() :  _lines(24) {
	}
	void putch(int x, int y, wchar_t c) {
		auto& line = _lines[y];
		bool changed = line.line.size() < x;
		while (x+1 > line.line.size()) line.line.push_back({ ' ', _blankAttrib });
		CharInfo& info = line.line.at(x);
		if (c != info.ch || _defaultAttrib != info.attrib) {
			info.ch = c;
			info.attrib = _defaultAttrib;
			changed = true;
		}
		line.changed = changed;
	}
	void putstr(int x, int y, const std::string& str) {
		for(auto c : str) putch(x++, y, c);
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
		for (auto& l : _lines) l.changed = true;
		update_display();
	}
	BEGIN_MSG_MAP(TerminalView)
		MSG_WM_CREATE(OnCreate)
		MSG_WM_DESTROY(OnDestroy)
		MSG_WM_TIMER(OnTimer)
		CHAIN_MSG_MAP(CDoubleBufferImpl<TerminalView>)
	END_MSG_MAP()
	DECLARE_WND_CLASS("TerminalView")
	void OnTimer(UINT uTimerID)//, TIMERPROC pTimerProc)
	{

		if (1 != uTimerID)
			SetMsgHandled(false);
		else {
			update_display();
			RedrawWindow();
		}
	}
	LRESULT OnCreate(LPCREATESTRUCT lpcs) {
		std::ifstream rom_file;
		rom_file.open("roms/23-018e2-00.e4", std::ios::binary);
		if (!rom_file.good())
			throw 0;
		_char_rom.resize(0x800);
		rom_file.read((char*)_char_rom.data(), 0x800);
		rom_file.close();
		if (!_screen.Create(800, 320, 32)) throw 0;
		

		
		//	sim->m_vsync = [this]() {
			//	_vrequest.clear();
		//		update_display(); // update display in seperate thread?
								  //while (_vrupdated.test_and_set()); // block the rendering thread till we update


		
		putstr(0, 0, "Fuck me in the ASSS");
		update_display();
		_lines[1].line = _lines[0].line;
		_lines[3].line = _lines[0].line;
		_lines[0].attrib += LineAttributes::DoubleWidth + LineAttributes::DoubleHeightTopHalf;
		_lines[1].attrib += LineAttributes::DoubleWidth + LineAttributes::DoubleHeightBottomHalf;
		SetTimer(1, 1000 / 15);
	}

	
	void draw_scanline(size_t line, size_t ch_line,  const Line& cline) {
		uint32_t* bits = static_cast<uint32_t*>(_screen.GetPixelAddress(0, line));
		// clear line
		for(size_t i=0; i < _screen.GetWidth();i++) bits[i] = screen_rev ? RGB(0, 255, 0) : RGB(0, 0, 0);
		if (cline.line.size() == 0) return; // skip if blank
		if(cline.attrib % LineAttributes::DoubleHeightTopHalf)
			ch_line = (ch_line >> 1); 
		else if(cline.attrib % LineAttributes::DoubleHeightBottomHalf)
			ch_line = (ch_line >> 1) + 5;
		// modify line since that is how it is stored in rom
		if (ch_line == 0) ch_line = 15; else ch_line--;
		COLORREF fg = RGB(0, 255, 0);
		COLORREF bg = RGB(0, 0, 0);
		bool prevbit = false, bit = false;
		
		size_t x = 0;
		for (auto& ci : cline.line) {
			uint8_t rom_bits = _char_rom[ci.ch * 16 + ch_line];
			bool invert = screen_rev ^ ci.attrib.hasAttrib(CharAttributes::Inverted);
			for (int b = 0; b < 8; b++)
			{
				prevbit = bit;
				bit = BIT((rom_bits << b), 7);
				*bits++ = (bit | prevbit) ^ invert ? fg : bg;
				if (cline.attrib % LineAttributes::DoubleWidth) *bits++ = bit ^ invert ? fg : bg;
			}
			prevbit = bit;
			// char interleave is filled with last bit
			*bits++ = (bit | prevbit) ^ invert ? fg : bg;
			*bits++ = bit ^ invert ? fg : bg;
			if (cline.attrib % LineAttributes::DoubleWidth) {
				*bits++ = bit ^ invert ? fg : bg;
				*bits++ = bit ^ invert ? fg : bg;
			}
		}
	}
	void update_display() {
		size_t scan_line = 0;
		for (auto& l : _lines) {
			//if (l.changed) {
				// figure out smooth scrolling here
			for (size_t i = 0; i < 10; i++) {
				draw_scanline(scan_line + i, i, l);
			}
			//	l.changed = false;

			scan_line += 10;
		}
	}
	void OnDestroy() {
		KillTimer(1);
		//sim->m_vsync = nullptr;
		_screen.Destroy();
	}
	void DoPaint(CDCHandle dc)
	{
		CRect rect;
		GetClientRect(&rect);
		_screen.Draw(dc, rect);// rect);
	}
};
