#pragma once
#include "global.h"
#include <bitset>
#include <array>
/*

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
*/
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
//class TerminalView : public CWindowImpl<TerminalView, CWindow, CFrameWinTraits > {
	std::bitset <20> bittt;
	typedef std::bitset<8 * 10> vt_glyph;

	struct CharInfo {
		wchar_t ch;
		CharAttributes attrib;
	};
	struct Line {
		LineAttributes attrib = LineAttributes::None;
		std::vector<wchar_t> line;
		bool changed = false;
		void swap(Line& other) {
			std::swap(other.attrib, attrib);
			std::swap(other.line, line);
			other.changed = changed = true;
		}
		void putch(int pos, wchar_t c) {
			while (line.size() < pos + 1) line.push_back(' ');
			line[pos] = c;
		}
		void clear() { line.clear(); }
	};
	int cursor_x = 0;
	int cursor_y = 0;
	int char_height = 10;
	int char_width = 10;

	std::vector<Line> _lines;
	std::vector<vt_glyph> _glyphs;
	CDC _glyph_dc;
	//std::vector<CDC> _glyphs_dc;
	bool screen_rev = false;
	std::vector<CharInfo> _char_buffer;
	CharAttributes _defaultAttrib;
	CharAttributes _blankAttrib;

	COLORREF _forground;
	COLORREF _background;
	bool _80columns;
	int _reverse_field = 0;
	int _basic_attribute = 0;
	int _linedoubler = 0;
	std::vector<uint8_t> _char_rom;
	CImage _screen;
	CDCHandle _screen_dc;
	//CDC _screen_dc;
//	std::vector<vt100_glyph> _glyphs;
	int _topMargin = 0;
	int _bottomMargin = 23;
	//	std::atomic_flag _vrequest = { 0 };
	//	std::atomic_flag _vrupdated = { 0 };
public:
	TerminalView() : _lines(24) {
	}

	void load_glyphs(const std::vector<uint8_t>& char_rom) {
		_char_rom = char_rom;
		_glyphs.clear();
		bool prevbit, bit;
		int ch_line;

		for (size_t ch = 0; ch < 0x80; ch++) {
			vt_glyph glyph;
			for (size_t y = 0; y < 10; y++) {
				ch_line = y;
				if (ch_line == 0) ch_line = 15; else ch_line--;
				uint8_t rom_bits = _char_rom[ch * 16 + ch_line];
				for (int b = 0; b < 8; b++)
					glyph.set(b + y * 8, BIT((rom_bits << b), 7) ? true : false);
			}
			_glyphs.emplace_back(glyph);
		}
	}
	void putstr(int x, int y, const std::string& str) {
		for (auto c : str) putch(x++, y, c, _defaultAttrib);
	}
	void scroll(int i) {
		CSize size(_screen.GetWidth(), _screen.GetHeight());
		int stride = _screen.GetPitch();
		size_t line_size = std::abs(stride);
		size_t total_size = line_size *(size.cy - std::abs(i));
		uint8_t* begin = reinterpret_cast<uint8_t*>(_screen.GetBits());
		if (stride < 0) begin -= line_size * (size.cy - 1); // negitive stride means bitmap is bottom top
		uint8_t* end = begin + total_size;
		
		if (i > 0) { // scroll down
			std::copy_n(begin + total_size, total_size, begin);
			std::memcpy(begin + total_size, begin, total_size);
			size_t black_pixels = ((size.cy * line_size)-total_size) / sizeof(COLORREF);
			std::fill_n(reinterpret_cast<COLORREF*>(begin), black_pixels, _background); 
		}
		else {
			std::memcpy(begin, begin + total_size, total_size);
			size_t black_pixels = ((size.cy * line_size) - total_size) / sizeof(COLORREF);
			std::fill_n(reinterpret_cast<COLORREF*>(end), black_pixels, _background);
		//	dline = dline + line_size *(size.cy - std::abs(i));
		//	sline = dline + line_size *(size.cy)
			
		}
		this->RedrawWindow();
		/*
	
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
		//	update_display();
		*/
	}
	BEGIN_MSG_MAP(TerminalView)
		MSG_WM_CREATE(OnCreate)
		MSG_WM_DESTROY(OnDestroy)
		MSG_WM_TIMER(OnTimer)
	//	MSG_WM_PAINT(OnPaint)
		CHAIN_MSG_MAP(CDoubleBufferImpl<TerminalView>)
	END_MSG_MAP()
	DECLARE_WND_CLASS("TerminalView")
	void OnTimer(UINT uTimerID)//, TIMERPROC pTimerProc)
	{

		if (1 != uTimerID)
			SetMsgHandled(false);
		else {
			//	update_display();
			RedrawWindow();
		}
	}
	void draw_cursor() {
		CDCHandle handle = _screen.GetDC();
		handle.FillSolidRect(cursor_x, cursor_y - char_height, char_width, char_height, _forground);
		_screen.ReleaseDC();
	}
	void clear_area(CDCHandle& handle, int x1, int y1, int x2, int y2) {
		for (int i = y1; i < y2; i += char_height) {
			for (int j = x1; j < x2; j += char_width) {
				handle.FillSolidRect(j, i, char_width, char_height, _background);
			}
		}
	}
	void clear_area(int x1, int y1, int x2, int y2) {
		CDCHandle handle = _screen.GetDC();
		clear_area(handle,x1, y1, x2, y2);
		_screen.ReleaseDC();
	}
	void scroll_window(CDCHandle& handle , int top, int bot, int dy, COLORREF back, boolean smooth)
	{
		/*
	
		int width = _screen.GetWidth();
		if (smooth)
		{
			CPen back_pen;
			back_pen.CreatePen(back);
			auto backup = handle.SelectPen(back_pen);
			try
			{
				if (dy > 0) {
					for (int yt = top; yt < top + dy; yt++)
					{
						handle.MoveTo(0, yt);
						handle.LineTo(width, yt);

						this.graphics.drawLine(0, yt, w, yt);
						this.graphics.copyArea(0, yt, w, bot - top, 0, 1);
						this.term_area.repaint(0, yt, w, yt + bot - top);
						Thread.sleep(10L);
					}
				}
				else {
					for (int yt = top; yt > top + dy; yt--)
					{
						this.graphics.drawLine(0, yt + bot - top, w, yt + bot - top);
						this.graphics.copyArea(0, yt, w, bot - top, 0, -1);
						this.term_area.repaint(0, yt, w, yt + bot - top);
						Thread.sleep(10L);
					}
				}
			}
			catch (Exception e) {}
			this.graphics.setColor(tcolor);
		}
		else
		{
			this.graphics.copyArea(0, top, w, bot - top, 0, dy);
			if (dy > 0) {
				clear_area(0, top, w, top + this.char_height);
			}
			else {
				clear_area(0, bot - this.char_height, w, bot);
			}
			redraw(0, top - this.char_height, w, bot + this.char_height);
		}
		*/
	}
	void putch(int x, int y, wchar_t code, CharAttributes info) {
		//auto& glyph = _glyphs.at(c & 0x7f);
		bool prevbit = false, bit = false;
		UINT16 x_preset = (x << 3) + x; // x_preset = x * 9 (= 132 column mode)
		if (_80columns == 80) x_preset += x; //        x_preset = x * 10 (80 column mode)
		UINT16 y_preset;
		UINT16 CHARPOS_y_preset = (y << 3) + (y * 2); // CHARPOS_y_preset = y * 10;
		UINT16 DOUBLE_x_preset = (x_preset << 1); // 18 for 132 column mode, else 20 (x_preset * 2)

		UINT8 line = 0;
		int  j = 0;
		int fg_intensity;
		int back_intensity, back_default_intensity;

		int invert = info.reverse() ? 1 : 0; // REVERSE
		int bold = info.bold() ? 0 : 1; // BIT 4
		int blink = info.blink() ? 0 : 1; // BIT 5
		int underline = info.underline() ? 0 : 1; // BIT 6
		bool blank = info.blank() ? true : false; // BIT 7
		invert = invert ^ _reverse_field ^ _basic_attribute;
		fg_intensity = bold + 2;   // FOREGROUND (FG):  normal (2) or bright (3)

		back_intensity = 0; // DO NOT SHUFFLE CODE AROUND !!
	//	if ((blink != 0) && (m_blink_flip_flop != 0))
	//		fg_intensity -= 1; // normal => dim    bright => normal (when bold)

							   // INVERSION: background gets foreground intensity (reduced by 1).
							   // _RELIES ON_ on_ previous evaluation of the BLINK signal (fg_intensity).
		if (invert != 0)
		{
			back_intensity = fg_intensity - 1; // BG: normal => dim;  dim => OFF;   bright => normal

			if (back_intensity != 0)           //  FG: avoid 'black on black'
				fg_intensity = 0;
			else
				fg_intensity = fg_intensity + 1; // FG: dim => normal; normal => bright
		}

		// BG: DEFAULT for entire character (underline overrides this for 1 line) -
		back_default_intensity = back_intensity;

		bool double_width = info.double_width() ? true : false; // all except normal: double width
		bool double_height = info.double_height() ? false : true;  // 0,2 = double height

		int smooth_offset = 0;
		//	if (scroll_region != 0)
			//	smooth_offset = m_last_scroll; // valid after VBI

		int i = 0;
		int extra_scan_line = 0;
		for (int scan_line = 0; scan_line < (_linedoubler ? 20 : 10); scan_line++)
		{
			y_preset = CHARPOS_y_preset + scan_line;

			// 'i' points to char-rom (plus scroll offset; if active) -
			// IF INTERLACED: odd lines = even lines
			i = (_linedoubler ? (scan_line >> 1) : scan_line) + smooth_offset;

			if (i > 9) // handle everything in one loop (in case of smooth scroll):
			{
				extra_scan_line += 1;

				// Fetch appropriate character bitmap (one scan line) -
				// IF INTERLACED: no odd lines
				i = smooth_offset - (_linedoubler ? (extra_scan_line >> 1) : extra_scan_line);

				if (CHARPOS_y_preset >= extra_scan_line) // If result not negative...
					y_preset = CHARPOS_y_preset - extra_scan_line; // correct Y pos.
				else
				{
					y_preset = (_linedoubler ? 480 : 240) - extra_scan_line;
					i = 0; // blank line. Might not work with TCS or other charsets (FIXME)
				}
			}
			if (info.double_height()) {
				j = (i >> 1);
				if (info.attrib & CharAttributes::Attribute::DOUBLE_T) j += 5;
			}
			else j = i;

			// modify line since that is how it is stored in rom
			if (j == 0) j = 15; else j = j - 1;

			line = _char_rom[(code << 4) + j]; // code * 16

												// UNDERLINED CHARACTERS (CASE 5 - different in 1 line):
			back_intensity = back_default_intensity; // 0, 1, 2
			if (underline != 0)
			{
				if (i == 8)
				{
					if (invert == 0)
						line = 0xff; // CASE 5 A)
					else
					{
						line = 0x00; // CASE 5 B)
						back_intensity = 0; // OVERRIDE: BLACK BACKGROUND
					}
				}
			}

			for (int b = 0; b < 8; b++) // 0..7
			{
				if (blank)
				{
					bit = _reverse_field ^ _basic_attribute;
				}
				else
				{
					bit = BIT((line << b), 7);

					if (bit > 0)
						bit = fg_intensity;
					else
						bit = back_intensity;
				}

				// Double, 'double_height + double_width', then normal.
				if (double_width)
				{
					_screen.SetPixel(DOUBLE_x_preset + (b << 1) + 1, y_preset, bit ? _forground : _background);
					_screen.SetPixel(DOUBLE_x_preset + (b << 1), y_preset, bit ? _forground : _background);

					if (double_height)
					{
						_screen.SetPixel(DOUBLE_x_preset + (b << 1) + 1, y_preset + 1, bit ? _forground : _background);
						_screen.SetPixel(DOUBLE_x_preset + (b << 1), y_preset + 1, bit ? _forground : _background);
					}
				}
				else
				{
					_screen.SetPixel(x_preset + b, y_preset, bit ? _forground : _background);
				}
			} // for (8 bit)

			  // char interleave (X) is filled with last bit
			if (double_width)
			{
				// double chars: 18 or 20 bits
				_screen.SetPixel(DOUBLE_x_preset + 16, y_preset, bit ? _forground : _background);
				_screen.SetPixel(DOUBLE_x_preset + 17, y_preset, bit ? _forground : _background);
				if (_80columns)
				{
					_screen.SetPixel(DOUBLE_x_preset + 18, y_preset, bit ? _forground : _background);
					_screen.SetPixel(DOUBLE_x_preset + 19, y_preset, bit ? _forground : _background);
				}
			}
			else
			{   // normal chars: 9 or 10 bits
				_screen.SetPixel(x_preset + 8, y_preset, bit ? _forground : _background);
				if (_80columns) _screen.SetPixel(x_preset + 9, y_preset, bit ? _forground : _background);
			}
		} // for (scan_line)

	}
	/*

		for (size_t y = 0; y < 10; y++) {
			COLORREF* line = reinterpret_cast<COLORREF*>(_screen.GetPixelAddress(col * 10, row * 10 + y));
			line++; // we skip the first col as thats filled in by the previous bit
			prevbit = false;
			for (int x = 0; b < 8; b++)
			{
				bit = BIT((rom_bits << b), 7) ? true : false;
				glyph.set(b + y * 10, (bit || prevbit));
				prevbit = bit;
			}
			glyph.set(8 + y * 10, (bit || prevbit));
			glyph.set(9 + y * 10, bit);
		}
		_glyphs.emplace_back(glyph);
		for (size_t i = 0; i < 10; i++) {
			COLORREF* line = reinterpret_cast<COLORREF*>(_screen.GetPixelAddress(x * 10, y * 10+i));
			for (int b = 0; b < 10; b++)
			{
				line[b] = glyph.test(i * 10 + b) ? _forground :  _background;
			}
		}
		*/

	LRESULT OnCreate(LPCREATESTRUCT lpcs) {
		_forground = RGB(0x00, 0xFF, 0x00);
		_background = RGB(0x00, 0x00, 0x00);
		std::ifstream rom_file;
		std::vector<uint8_t> char_rom;
		rom_file.open("roms/23-018e2-00.e4", std::ios::binary);
		if (!rom_file.good())
			throw 0;
		char_rom.resize(0x800);
		rom_file.read((char*)char_rom.data(), 0x800);
		rom_file.close();
		load_glyphs(char_rom);
		if (!_screen.Create(800, 320, 32)) throw 0;
		_screen_dc = _screen.GetDC();
		_glyph_dc.CreateCompatibleDC(_screen_dc);
		





		
		//	sim->m_vsync = [this]() {
			//	_vrequest.clear();
		//		update_display(); // update display in seperate thread?
								  //while (_vrupdated.test_and_set()); // block the rendering thread till we update


	
		putstr(0, 0, "Fuck me in the ASSS");
		putstr(0, 2, "abcdefghijklmnopqrstuvwxyz");
	//	update_display();
		//_lines[1].line = _lines[0].line;
		//_lines[3].line = _lines[0].line;
		//_lines[0].attrib += LineAttributes::DoubleWidth + LineAttributes::DoubleHeightTopHalf;
		//_lines[1].attrib += LineAttributes::DoubleWidth + LineAttributes::DoubleHeightBottomHalf;
		//SetTimer(1, 1000 / 15);
	}

	
	void draw_scanline(size_t line, size_t ch_line,  const Line& cline) {
		/*

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
		*/
	}
	void update_display_old() {
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
		_glyph_dc.SelectBitmap(NULL);
		_glyph_dc.DeleteDC();
	//	_screen_dc.SelectBitmap(NULL);
		//sim->m_vsync = nullptr;
		_screen.ReleaseDC();
		_screen.Destroy();
	}

	void DoPaint(CDCHandle dc)
	{
		//CPaintDC dc(*this);
		CRect rect;
		GetClientRect(&rect);
		_screen.Draw(dc, rect);// rect);
	}
};
