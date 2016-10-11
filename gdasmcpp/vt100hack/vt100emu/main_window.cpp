#pragma once
#include "global.h"

class TerminalView : public CDoubleBufferWindowImpl<TerminalView, CWindow, CDxAppWinTraits > {
	bool screen_rev = false;
	CImage _screen;
	std::atomic_flag _vrequest = { 0 };
	std::atomic_flag _vrupdated = { 0 };
public:
	TerminalView() : _line_alloc(40), _lines(40) {
		for (size_t i = 0; i < 40; i++) {
			_lines[i] = _line_alloc.data() + i;
		}
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
			RedrawWindow();
		}
	}
	LRESULT OnCreate(LPCREATESTRUCT lpcs) {
		for (size_t i = 0; i < 25; i++) {
			_lines[i] = _line_alloc.data() + i;
		}
		if (!_screen.Create(800, 320, 32)) throw 0;
		SetTimer(1, 1000 / 15);
		sim->m_vsync = [this]() {
			_vrequest.clear();
			update_display(); // update display in seperate thread?
							  //while (_vrupdated.test_and_set()); // block the rendering thread till we update
		};
	}
	void mame_scanline(size_t line, size_t ch_line, uint8_t display_type, const CharInfo* chars, size_t count) {
		bool double_width = (display_type == 2) ? true : false;

		switch (display_type)
		{
		case 0: // bottom half, double height
			ch_line = (ch_line >> 1) + 5; break;
		case 1: // top half, double height
			ch_line = (ch_line >> 1); break;
		case 2: // double width
		case 3: // normal
			ch_line = ch_line;  break;
		default: ch_line = 0; break;
		}
		// modify line since that is how it is stored in rom
		if (ch_line == 0) ch_line = 15; else ch_line--;
		COLORREF fg = RGB(0, 255, 0);
		COLORREF bg = RGB(0, 0, 0);
		bool prevbit = false, bit = false;
		uint32_t* bits = static_cast<uint32_t*>(_screen.GetPixelAddress(0, line));
		for (size_t i = 0; i < count; i++) {
			CharInfo ci = chars[i];
			uint8_t rom_bits = s_rom[ci.c() * 16 + ch_line];
			bool invert = screen_rev ^ ci.invert();
			for (int b = 0; b < 8; b++)
			{
				prevbit = bit;
				bit = BIT((rom_bits << b), 7);
				*bits++ = (bit | prevbit) ^ invert ? fg : bg;
				if (double_width)*bits++ = bit ^ invert ? fg : bg;
			}
			prevbit = bit;
			// char interleave is filled with last bit
			*bits++ = (bit | prevbit) ^ invert ? fg : bg;
			*bits++ = bit ^ invert ? fg : bg;
			if (double_width) {
				*bits++ = bit ^ invert ? fg : bg;
				*bits++ = bit ^ invert ? fg : bg;
			}
		}
	}
	void draw_scanline(size_t line, size_t ch_line, uint8_t lattr, const CharInfo* chars, size_t count) {
		uint32_t* bits = static_cast<uint32_t*>(_screen.GetPixelAddress(0, line));
		bool last_bit = false;
		count = (count * 8) > 800 ? 80 : count; //std::max(count, (count * 8)
		for (size_t i = 0; i < count; i++) {
			CharInfo ci = chars[i];
			char c = ci.c() & 0x7F;
			uint8_t char_line = s_rom[(c << 4) + ch_line];
			COLORREF fg = screen_rev ^ ci.invert() ? RGB(0, 0, 0) : RGB(0, 255, 0);
			COLORREF bg = screen_rev ^ ci.invert() ? RGB(0, 255, 0) : RGB(0, 0, 0);
			for (int b = 7; b >= 0; b--) {
				//bool bit = glyph.getPixel(b, y);
				bool bit = char_line & 0x80 ? true : false;
				if (!bit && last_bit) { bit = true; last_bit = false; }
				else last_bit = bit;
				*bits++ = bit ? fg : bg;
				//*line_ptr++ = bit ? RGB(0, 255, 0) : RGB(255, 255, 255);
				char_line <<= 1;
			}
			// add a dot
			*bits++ = last_bit ? fg : bg;
		}
	}

	void update_display(bool enable_avo = false) {
		if (!vsync_happened) return;
		size_t start = 0x2000;
		size_t next = start;
		int lineno = 0;
		const uint8_t* ptr = ram + start;
		bool double_height = false;
		bool double_width = false;
		bool scrolling_region = false;
		uint8_t line_attributes;
		uint8_t lattr = 0xF;
		int y = -1;
		int inscroll = 0;
		for (uint8_t i = 1; i < 27; i++) {
			const char* p = (const char*)ram + start;
			const char* maxp = p + 133;
			//if (*p != 0x7f) y++;
			y++;
			int x = 0;
			_char_buffer.clear();
			while (*p != 0x7f && p != maxp) {
				unsigned char c = *p;
				int attrs = enable_avo ? p[0x1000] : 0xF;
				p++;
				if (y > 0) {
					_char_buffer.emplace_back(c, attrs);
					bool inverse = (c & 128);
					bool blink = !(attrs & 0x1);
					bool uline = !(attrs & 0x2);
					bool bold = !(attrs & 0x4);
					bool altchar = !(attrs & 0x8);
					c &= 0x7F;
					if (screen_rev) inverse = ~inverse;
					if (c == 0 || c == 127) c = ' ';
				}
			}
			if (!_char_buffer.empty()) {
				for (int cy = 0; cy < 10; cy++) {
					mame_scanline(y * 10 + cy, cy, lattr, _char_buffer.data(), _char_buffer.size());
					//	draw_scanline(y*10+cy, cy, lattr, _char_buffer.data(), _char_buffer.size());
				}

			}
			if (p == maxp) {
				//wprintw(msgWin,"Overflow line %d\n",i); wrefresh(msgWin);
				break;
			}
			// at terminator
			p++;
			unsigned char a1 = *(p++);
			unsigned char a2 = *(p++);
			//printf("Next: %02x %02x\n",a1,a2);fflush(stdout);
			uint16_t next = (((a1 & 0x10) != 0) ? 0x2000 : 0x4000) | ((a1 & 0x0f) << 8) | a2;
			lattr = ((a1 >> 5) & 0x3);
			inscroll = ((a1 >> 7) & 0x1);
			if (start == next) break;
			start = next;
		};
	}
	void OnDestroy() {
		KillTimer(1);
		sim->m_vsync = nullptr;
		_screen.Destroy();
	}
	void DoPaint(CDCHandle dc)
	{
		CRect rect;
		GetClientRect(&rect);
		_screen.Draw(dc, rect);// rect);
	}
};