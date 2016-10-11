#pragma once
#include <Windows.h>
#include <vector>
#include <iostream>
#include <fstream>
#include <string>
#include <sstream>
#include <map>
#include <unordered_map>
#include <thread>
#include <atomic>

// order is important, atl
#include <atltypes.h>
#include <atldef.h>
#include <atlbase.h>

#include <atlstr.h>
// wtl
#include <atlapp.h>
#include <atlimage.h>
#include <cassert>

//#ifdef __ATLSTR_H__
//#define _CSTRING_NS	ATL



#include <atlgdi.h>
#include <atluser.h>
#include <atlwin.h>
#include <atlwin.h>        // ATL GUI classes</span>

//#include <atlmisc.h>       // WTL utility classes like CString</span>



#include <atlframe.h>
#include <atlctrls.h>
#include <atldlgs.h>
//#include <atlmisc.h>
#include <atlsplit.h>
#include <atlcrack.h>      // WTL enhanced msg map macros</span>
#include <atlwinx.h>

#include "vt100sim.h"
extern Vt100Sim* sim;
std::vector<uint8_t> s_rom;
#define IDD_ABOUTBOX                    100
#define IDR_MAINFRAME                   128
#define IDS_EDITSTRING                  57666
#define IDS_TITLESTRING                 57667

// Next default values for new objects
// 
// useful functions to deal with bit shuffling encryptions
template <typename T, typename U> constexpr T BIT(T x, U n) { return (x >> n) & T(1); }
struct Glyph {
	std::vector<bool> data;
	size_t width() const { return 10; }
	size_t height() const { return 8; }
	Glyph() :data(8 * 10) {}
	Glyph(char ch, bool double_height = false, bool double_width = false) :data(8 * 10) { load(ch, double_height, double_width); }
	void load(char ch, bool double_height = false, bool double_width = false) {
		for (int j = 0; j < 10; j++) {
			uint8_t line = s_rom[(ch << 4) + j];
			for (int b = 7; b >=0; b--) {// 0..7
										 //uint8_t bit = BIT((line << b), 7);
				data[b + j * 8] = line & 0x1 ? true : false;
				line >>= 1;
			}
		}
	}
	void to_stream(std::ostream& s) const {
		for (int j = 0; j < 10; j++) {
			for (int i = 0; i < 8; i++)
				if (getPixel(i, j)) s << "*"; else s << ' ';
			s << std::endl;
		}
	}
	bool getPixel(int x, int y) const { return data[x + y * 8]; }
};
extern bool vsync_happened;
extern uint8_t ram[];
extern uint8_t touched[];
CAppModule _Module;
typedef CWinTraits<WS_OVERLAPPEDWINDOW, 0> CDxAppWinTraits;

std::unordered_map<uint8_t, VT_KEY> windows_to_vt100() {
	std::unordered_map<uint32_t, uint32_t> m;
	// 0x01, 0x02 (both) -> del
	m[VK_DELETE] = 0x03;
	// ??? m[KEY_ENTER] = 0x04;
	// 0x04 -> nul
	m['p'] = 0x05;
	m['o'] = 0x06;
	m['y'] = 0x07;
	m['t'] = 0x08;
	m['w'] = 0x09;
	m['q'] = 0x0a;

	// 0x0b, 0x0c, 0x0d, 0x0e, 0x0f (Mirror of next 5)

	m[VK_LEFT] = 0x10;
	// 0x11 -> nul
	// 0x12 -> nul
	// 0x13 -> nul
	m[']'] = 0x14; m['}'] = 0x94;
	m['['] = 0x15; m['{'] = 0x95;
	m['i'] = 0x16;
	m['u'] = 0x17;
	m['r'] = 0x18;
	m['e'] = 0x19;
	m['1'] = 0x1a; m['!'] = 0x9a; 

	// 0x1b, 0x1c, 0x1d, 0x1e, 0x1f (Mirror of next 5)

	m[VK_LEFT] = 0x20;
	// 0x21 -> nul
	m[VK_DOWN] = 0x22;
	m[VK_PAUSE] = 0x23; // break
	m[VK_F6] = 0x23;  // PF3
	m[VK_F7] = 0xA3;  // Tab?


	m['`'] = 0x24; m['~'] = 0xa4;
	m['-'] = 0x25; m['_'] = 0xa5;
	m['9'] = 0x26; m['('] = 0xa6;
	m['7'] = 0x27; m['&'] = 0xa7;
	m['4'] = 0x28; m['$'] = 0xa8;
	m['3'] = 0x29; m['#'] = 0xa9;
	m[VK_CANCEL] = 0x2a;	// cancel
	m[VK_ESCAPE] = 0x2a; // Escape Key

												// 0x2b, 0x2c, 0x2d, 0x2e, 0x2f (Mirror of next 5)

	m[VK_UP] = 0x30;
	m[VK_F3] = 0x31; // PF3
	m[VK_F1] = 0x32; // PF1
	m[VK_BACK] = 0x33;
	m['='] = 0x34; m['+'] = 0xb4;
	m['0'] = 0x35; m[')'] = 0xb5;
	m['8'] = 0x36; m['*'] = 0xb6;
	m['6'] = 0x37; m['^'] = 0xb7;
	m['5'] = 0x38; m['%'] = 0xb8;
	m['2'] = 0x39; m['@'] = 0xb9;
	m['\t'] = 0x3a;

	// 0x3b, 0x3c, 0x3d, 0x3e, 0x3f (Mirror of next 5)

	// 0x40 -> '7'	(Keypad ^[Ow)
	m[VK_F4] = 0x41;
	m[VK_F2] = 0x42;
	// 0x43 -> '0'
	m['\n'] = 0x44; // Linefeed key:
	m['\\'] = 0x45; m['|'] = 0xc5;
	m['l'] = 0x46;
	m['k'] = 0x47;
	m['g'] = 0x48;
	m['f'] = 0x49;
	m['a'] = 0x4a;

	// 0x4b, 0x4c, 0x4d, 0x4e, 0x2f (Mirror of next 5)

	// 0x50 -> '8'    (Keypad ^[Ox)
	// 0x51 -> ^M	    (Keypad Enter)
	// 0x52 -> '2'
	// 0x53 -> '1'
	// 0x54 -> nul
	m['\''] = 0x55; m['"'] = 0xd5;
	m[';'] = 0x56; m[':'] = 0xd6;
	m['j'] = 0x57;
	m['h'] = 0x58;
	m['d'] = 0x59;
	m['s'] = 0x5a;

	// 0x5b, 0x5c, 0x5d, 0x5e, 0x5f

	// 0x60 -> '.'
	// 0x61 -> ','
	// 0x62 -> '5'
	// 0x63 -> '4'
	// 0x64 -> ^M	    (Return Key)
	m[VK_RETURN] = 0x64;
	m['.'] = 0x65; m['>'] = 0xe5;
	m[','] = 0x66; m['<'] = 0xe6;
	m['n'] = 0x67;
	m['b'] = 0x68;
	m['x'] = 0x69;
	// 0x6a -> NoSCROLL

	// 0x6b, 0x6c, 0x6d, 0x6e, 0x6f (Mirror of next 5)

	// 0x70 -> '9'
	// 0x71 -> '3'
	// 0x72 -> '6'
	// 0x73 -> '-'
	// 0x74 -> nul
	m['/'] = 0x75; m['?'] = 0xf5;
	m['m'] = 0x76;
	m[' '] = 0x77;
	m['v'] = 0x78;
	m['c'] = 0x79;
	m['z'] = 0x7a;

	// setup
	m[VK_F9] = 0x7b;
	m[VK_CONTROL] = 0x7c; // 0x7c   Control Key
	m[VK_SHIFT] = 0x7d; // 0x7d   Shift Key
	m[VK_CAPITAL] = 0x7e;
	
	
	// 0x7e
	// 0x7f

	//for (int i = 0; i < 26; i++) {
	//	m['A' + i] = m['a' + i] | 0x80;
	//}
	std::unordered_map<uint8_t, VT_KEY> ret;
	for (auto& m : m) {
		ret[m.first] = static_cast<VT_KEY>(m.second);
	}
	ret[VK_UP] = VT_KEY::VT_UP;
	ret[VK_DOWN] = VT_KEY::VT_DOWN;
	ret[VK_LEFT] = VT_KEY::VT_LEFT;
	ret[VK_RIGHT] = VT_KEY::VT_RIGHT;

	//m[KEY_LEFT] = 0x20;
	// 0x21 -> nul
	//m[KEY_DOWN] = 0x22;
	return ret;
}
auto vk_map = windows_to_vt100();// public CDoubleBufferWindowImpl<TerminalView, CWindow, CDxAppWinTraits > ,
class TerminalView : public CDoubleBufferWindowImpl<TerminalView, CWindow, CDxAppWinTraits > {
	struct CharInfo {
		uint8_t attrib;
		uint8_t ch;
		CharInfo() : ch(' '), attrib(0xF) {}
		CharInfo(uint8_t ch, uint8_t attrib) : ch(ch), attrib(attrib) {}
		explicit CharInfo(char ch) : ch(ch & 0x7F), attrib(0xF) {}
		char c() const { return ch & 0x7F; }
		bool blink() const { return!(attrib & 0x1); }
		bool uline() const { return !(attrib & 0x2); }
		bool bold() const { return!(attrib & 0x4); }
		bool altchar() const { return !(attrib & 0x8); }
		bool invert() const {return  (ch & 0x80) ? true : false; } // REVERSE
	};
	struct LineAttributes {
		uint8_t attrib;
		uint8_t operator*() const { return attrib; }
		LineAttributes() :attrib(0x3) {}
		explicit LineAttributes(uint8_t attrib) :attrib(attrib) {}
		bool double_width() const { return attrib & 0x3 != 3; } // all except normal: double width
		bool double_height() const { return attrib & 1; } // 0,2 = double height
		bool scroll_region() const { return attrib & 4 != 0; }
	};
	struct Line {
		std::vector<CharInfo> line;
		std::vector<uint8_t> attribs;
		LineAttributes attrib;
		Line() :  line(140) , attribs(140) {}
	};
	std::vector<Line> _line_alloc;
	std::vector<Line*> _lines;
	bool screen_rev = false;
	std::vector<CharInfo> _char_buffer;
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
			char c =ci.c() & 0x7F;
			uint8_t char_line = s_rom[(c << 4) + ch_line];
			COLORREF fg = screen_rev ^ ci.invert() ? RGB(0, 0, 0) : RGB(0, 255, 0);
			COLORREF bg = screen_rev ^ ci.invert() ? RGB(0,255, 0) : RGB(0, 0, 0);
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

	void update_display(bool enable_avo=false) {
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

class MemoryView : public CWindowImpl<MemoryView, CWindow, CDxAppWinTraits > {
	CEdit m_edit;
	CListViewCtrl m_list;
	char _buffer[1000];
	std::vector<int> _tabs;
	TerminalView m_term;
public:
	BEGIN_MSG_MAP(MemoryView)
		MSG_WM_CREATE(OnCreate)
		MSG_WM_DESTROY(OnDestroy)
		//MSG_WM_CHAR(OnChar)
		MSG_WM_CLOSE(OnClose)
		MSG_WM_TIMER(OnTimer)
	END_MSG_MAP()
	DECLARE_WND_CLASS("MemoryViewWindow")
	void OnChar(TCHAR ch, UINT status, UINT status0) {

	}
	void OnClose() {
		ShowWindow(SW_HIDE); // hide it instead of showing it
		UpdateWindow();
	}
	
	void OnTimer(UINT uTimerID)//, TIMERPROC pTimerProc)
	{
		if (1 != uTimerID)
			SetMsgHandled(false);
		else {
			//	TCHAR cArray[1000];
			//	m_edit.SetWindowText(cArray);
			RedrawWindow();
		}
	}

	LRESULT OnCreate(LPCREATESTRUCT lpcs)
	{
		SetTimer(1, 1000);
		SetMsgHandled(false);

		return 0;
	}
	void OnDestroy() {
		//m_edit.DestroyWindow();
		
		KillTimer(1);
		PostQuitMessage(0);
		SetMsgHandled(false);
	}
};

//template <class T>//, class TBase = CWindow, class TWinTraits = CDxAppWinTraits >
class AppWindow : public CFrameWindowImpl<AppWindow>    //CWindowImpl<AppWindow, CWindow, CDxAppWinTraits >
{
	MemoryView _memwnd;
	TerminalView m_term;
	std::set<VT_KEY> _keys;
public:
	BEGIN_MSG_MAP(AppWindow)
		MSG_WM_CREATE(OnCreate)
		MSG_WM_DESTROY(OnDestroy)
		MSG_WM_SIZE(OnSize)
		MSG_WM_TIMER(OnTimer)
		//MSG_WM_KEYUP(OnKeyUp)
		MSG_WM_KEYDOWN(OnKeyDown)
		MSG_WM_KEYUP(OnKeyUp)
		MSG_WM_SIZE(OnSize)
		CHAIN_MSG_MAP(CFrameWindowImpl<AppWindow>)
	END_MSG_MAP()
	DECLARE_FRAME_WND_CLASS(NULL, IDR_MAINFRAME)
	void OnTimer(UINT uTimerID)//, TIMERPROC pTimerProc)
	{
		if (2 != uTimerID)
			SetMsgHandled(false);
		else {
			if (_keys.size() > 0) {
				sim->kbd.keys_press(_keys);
				_keys.clear();
			}
			
			//	TCHAR cArray[1000];
			//	m_edit.SetWindowText(cArray);
			//RedrawWindow();
		}
	}
	
	void OnSize(UINT func, CSize size) {
		m_term.ResizeClient(size.cx, size.cy, true);
	}
	void OnKeyDown(TCHAR ch, UINT status, UINT status0) {
		_keys.emplace(vk_map[ch]);

	//	sim->kbd.key_down((VT_KEY)vk_map[ch]);
		//sim->kbd.key_press((VT_KEY)vk_map[ch]);
	}
	void OnKeyUp(TCHAR ch, UINT status, UINT status0) {
		_keys.erase(vk_map[ch]);
		//sim->kbd.key_up((VT_KEY)vk_map[ch]);
	}
	//kbd
	LRESULT OnCreate(LPCREATESTRUCT lpcs)
	{
		CreateSimpleToolBar();
		// set toolbar style to flat look
	//	CToolBarCtrl tool = m_hWndToolBar;
	//	tool.ModifyStyle(0, TBSTYLE_FLAT);
		RECT rcHorz;
		GetClientRect(&rcHorz);
		m_term.Create(m_hWnd, rcHorz, NULL, WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN);
		
		SetTimer(2, 1000/15);
		_memwnd.Create(m_hWnd, CWindow::rcDefault, _T("MemoryViewWindow"));
		_memwnd.ShowWindow(SW_HIDE);
		_memwnd.UpdateWindow();


		SetMsgHandled(false);
		return 0;
	}
	void OnDestroy() {
		KillTimer(2);
		//_memwnd.DestroyWindow();
		
		PostQuitMessage(0);
		SetMsgHandled(false);
	}

};


void start_windows_system() {
	std::ifstream rom_file;
	rom_file.open("roms/23-018e2-00.e4", std::ios::binary);
	if (!rom_file.good())
		throw 0;
	s_rom.resize(0x800);
	rom_file.read((char*)s_rom.data(), 0x800);
	rom_file.close();

	std::thread([]() {
		CMessageLoop messageLoop;
		AppWindow mainwnd;
		
		_Module.Init(NULL, NULL);
		_Module.AddMessageLoop(&messageLoop);
	//	mainwnd.Create(NULL, CWindow::rcDefault, _T("Main Window"));
		// initialize a rect with size of window to create
		RECT rc = { 0, 0, 640, 480 };
		if (mainwnd.CreateEx(NULL, rc) == NULL)
		{
			ATLTRACE(_T("Main window creation failed!\n"));
			return 0;
		}

		// disable the maximize box
		mainwnd.ModifyStyle(WS_MAXIMIZEBOX, 0);

		// center main window in desktop area
		mainwnd.CenterWindow();
		mainwnd.ShowWindow(SW_SHOW);
		mainwnd.UpdateWindow();

		
		int nRet = messageLoop.Run();

		_Module.RemoveMessageLoop();
		_Module.Term();

	}).detach();

	std::cerr << "thread started" << std::endl;
}

