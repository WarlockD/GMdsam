#pragma once
#include <Windows.h>
#include <vector>
#include <iostream>
#include <fstream>
#include <string>
#include <sstream>

#include <thread>
#include <atomic>

#include <windows.h>
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


class TerminalView : public CWindowImpl<TerminalView, CWindow, CDxAppWinTraits > {
	struct CharInfo {
		uint16_t attrib;
		uint16_t ch;
	};
	struct Line {
		std::vector<CharInfo> line;
		bool double_height;
		bool double_width;
		bool scroll;
		Line() : double_height(false), double_width(false), scroll(true), line(140) {}
	};
	std::vector<Line> _line_alloc;
	std::vector<Line*> _lines;
	CImage _screen;
public:
	TerminalView() : _line_alloc(40), _lines(40) {
		for (size_t i = 0; i < 40; i++) {
			_lines[i] = _line_alloc.data() + i;
		}
	}
	BEGIN_MSG_MAP(MemoryView)
		//MESSAGE_HANDLER(WM_DESTROY, OnDestroy)
		//MSG_WM_CREATE(OnCreate)
		//MSG_WM_DESTROY(OnDestroy)
	//	MSG_WM_CLOSE(OnClose);
	MSG_WM_PAINT(OnPaint)
		MSG_WM_CREATE(OnCreate)
		MSG_WM_DESTROY(OnDestroy)
		MSG_WM_TIMER(OnTimer)
		//	MSG_WM_ERASEBKGND(OnEraseBkgnd)
		//MESSAGE_HANDLER(WM_DESTROY, OnCreate)
		//ON_PAINT
	END_MSG_MAP()
	DECLARE_WND_CLASS("TerminalView")
	void OnTimer(UINT uTimerID)//, TIMERPROC pTimerProc)
	{
		if (1 != uTimerID)
			SetMsgHandled(false);
		else {
			//	TCHAR cArray[1000];
			//	m_edit.SetWindowText(cArray);
			
			update_display();
			RedrawWindow();
		}
	}
	LRESULT OnCreate(LPCREATESTRUCT lpcs) {
		for (size_t i = 0; i < 25; i++) {
			_lines[i] = _line_alloc.data() + i;
		}
		if (!_screen.Create(800, 320, 32))
			throw 0;
		putString(10, 10, "abcdefghijklmnop");
		putString(10, 11, "ABCDEFGHIJKLMNOP");
		SetTimer(1, 1000 / 15);
		//putString(0, 9, "at the end");
		
	}
	void putString(int x, int y, const std::string& str) {
		Line& line = *_lines.at(y);
		auto it = str.begin();
		for (size_t i = x; i < line.line.size() && it != str.end(); i++,it++) {
			line.line[i].ch = *it;
		}
		refresh_screen();
	}
	std::vector<uint32_t> _dma;
	void draw_scanline(size_t line, size_t ch_line, uint8_t lattr, const CharInfo* chars, size_t count) {
		uint32_t* bits = static_cast<uint32_t*>(_screen.GetPixelAddress(0, line));
		bool last_bit = false;
		count = (count * 8) > 800 ? 80 : count; //std::max(count, (count * 8)
		for (size_t i = 0; i < count; i++) {
			uint8_t char_line = s_rom[(chars[i].ch << 4) + ch_line];
			for (int b = 7; b >= 0; b--) {
				//bool bit = glyph.getPixel(b, y);
				bool bit = char_line & 0x80 ? true : false;
				if (!bit && last_bit) { bit = true; last_bit = false; }
				else last_bit = bit;
				*bits++ = bit ? RGB(0, 255, 0) : RGB(0, 0, 0);
				//*line_ptr++ = bit ? RGB(0, 255, 0) : RGB(255, 255, 255);
				char_line <<= 1;
			}
		}
	}
	bool screen_rev = false;
	std::vector<uint8_t> _char_buffer;
	std::vector<uint8_t> _attrb_buffer;
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
					bool inverse = (c & 128);
					bool blink = !(attrs & 0x1);
					bool uline = !(attrs & 0x2);
					bool bold = !(attrs & 0x4);
					bool altchar = !(attrs & 0x8);
					c &= 0x7F;
					if (screen_rev) inverse = ~inverse;
					if (c == 0 || c == 127) c = ' ';
					_char_buffer.push_back(c);

					//_lines.at(y)->line[x++].ch = c;
				}
			//	if (lattr != 3) waddch(vidWin, ' ');
			//	if (inverse) wattroff(vidWin, A_REVERSE);
			//	if (uline) wattroff(vidWin, A_UNDERLINE);
			//	if (bold) wattroff(vidWin, A_BOLD);
			//	if (blink) wattroff(vidWin, A_BLINK);
			}
			if (!_char_buffer.empty()) {

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
		}
		refresh_screen();
	}
	void refresh_screen() {
		size_t scan_line = 0;
		auto it = _lines.begin();
		HDC screen_dc = _screen.GetDC();
		uint32_t* bits = static_cast<uint32_t*>(_screen.GetBits());
		Glyph glyph;
		for (size_t lineno = 0; lineno < 24; lineno++) {
			Line& line = *_lines.at(lineno);
			for (size_t y = 0; y < 10; y++, scan_line++) {
				draw_scanline(scan_line, y, line.line.data(), line.line.size());

			}
		}
		_screen.ReleaseDC();
	}
	void OnDestroy() {
		KillTimer(1);
		_screen.Destroy();
	}
	void OnPaint(HDC hdc) {
		CPaintDC dc(*this);
		CRect rect;
		GetClientRect(&rect);
		rect.top = 20;
		rect.left = 20;
		_screen.BitBlt(dc, 20, 20);// rect);
		//_screen.BitBlt
		//dc.TextOutA(20, 20, "FUCK FUCK FUCK");
		//_screen.Draw(dc, rect);
		//_screen.Draw(dc, 0,0);
	}
};
class MemoryView : public CWindowImpl<MemoryView, CWindow, CDxAppWinTraits > {
	CEdit m_edit;
	CListViewCtrl m_list;
	char _buffer[1000];
	std::vector<int> _tabs;
	TerminalView m_term;
//	CHorSplitterWindow m_hzSplit;
public:
	BEGIN_MSG_MAP(MemoryView)
		//MESSAGE_HANDLER(WM_DESTROY, OnDestroy)
		MSG_WM_CREATE(OnCreate)
		MSG_WM_DESTROY(OnDestroy)
		MSG_WM_CLOSE(OnClose);
		//MSG_WM_PAINT(OnPaint)
		MSG_WM_TIMER(OnTimer)
	//	MSG_WM_ERASEBKGND(OnEraseBkgnd)
		//MESSAGE_HANDLER(WM_DESTROY, OnCreate)
		//ON_PAINT
	END_MSG_MAP()
	DECLARE_WND_CLASS("MemoryViewWindow")

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
		/*
	
	//	int my, mx;
	//	getmaxyx(memWin, my, mx);
	//	int bavail = (mx - 7) / 3;
		int bdisp = 16;
	//	while (bdisp * 2 <= bavail) bdisp *= 2;
		uint16_t start = 0x2000;

		wattrset(memWin, A_NORMAL);
		for (int b = 0; b<bdisp; b++) {
			mvwprintw(memWin, 0, 7 + 3 * b, "%02x", b);
		}
		wattrset(memWin, COLOR_PAIR(1));

		for (int y = 1; y < my - 1; y++) {
			wattrset(memWin, COLOR_PAIR(1));
			mvwprintw(memWin, y, 1, "%04x:", start);
			for (int b = 0; b<bdisp; b++) {
				if (!touched[start])
					wattron(memWin, A_STANDOUT);
				if (ram[start] != 00) {
					wattron(memWin, COLOR_PAIR(2));
					wprintw(memWin, " %02x", ram[start++]);
					wattron(memWin, COLOR_PAIR(1));
				}
				else {
					wprintw(memWin, " %02x", ram[start++]);
				}
				wattroff(memWin, A_STANDOUT);
			}
		}
		*/
	}

	LRESULT OnCreate(LPCREATESTRUCT lpcs)
	{
		
		RECT rcHorz;
		GetClientRect(&rcHorz);
		m_term.Create(m_hWnd, rcHorz, NULL, WS_CHILD | WS_VISIBLE); // | WS_CLIPSIBLINGS | WS_CLIPCHILDREN);
	//	m_edit.Create(m_hWnd, rcHorz, NULL, WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN);
		SetTimer(1, 1000);
		SetMsgHandled(false);
		// Edit control //
		//m_edit.Create(m_hWnd, rcDefault, NULL, ES_MULTILINE | ES_AUTOVSCROLL | WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN, WS_EX_CLIENTEDGE);
	
	//	CFont font;
		//font.CreateFontA()
		// set the edit control to the default font
	//	m_edit.SetFont(AtlGetStockFont(DEFAULT_GUI_FONT), TRUE);

		// AtlLoadString supports strings > 255 characters
	//	TCHAR cArray[1000];
	//	AtlLoadString(IDS_EDITSTRING, cArray, 1001);
	//	m_edit.SetWindowText(cArray);

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
public:
	BEGIN_MSG_MAP(CDxWindowImpl)
		//MESSAGE_HANDLER(WM_DESTROY, OnDestroy)
		MSG_WM_CREATE(OnCreate)
		MSG_WM_DESTROY(OnDestroy)
		MSG_WM_PAINT(OnPaint)
		MSG_WM_TIMER(OnTimer)
		MSG_WM_ERASEBKGND(OnEraseBkgnd)
		CHAIN_MSG_MAP(CFrameWindowImpl<AppWindow>)
		//MESSAGE_HANDLER(WM_DESTROY, OnCreate)
		//ON_PAINT
	END_MSG_MAP()
	DECLARE_FRAME_WND_CLASS(NULL, IDR_MAINFRAME)

	//DECLARE_WND_CLASS("Main Window")
	void OnTimer(UINT uTimerID)//, TIMERPROC pTimerProc)
	{
		if (1 != uTimerID)
			SetMsgHandled(false);
		else
			RedrawWindow();
	}

	void OnPaint(HDC hdc) {
		CPaintDC dc(*this);
	//	_backbuffer.Draw(dc, 0, 0);
		CRect rect;
		GetClientRect(&rect);
		_backbuffer.StretchBlt(dc, rect);
		/*

		CDC dcImage;
		if (dcImage.CreateCompatibleDC(dc.m_hDC))
		{
		_backbuffer.Draw(hdc, 0, 0);
		CSize size;
		if (_backbuffer.GetSize(size))
		{
		HBITMAP hBmpOld = dcImage.SelectBitmap(_backbuffer);
		dc.BitBlt(0, 0, size.cx, size.cy, dcImage, 0, 0, SRCCOPY);
		dcImage.SelectBitmap(hBmpOld);
		}
		}
		*/
	}
	void draw_screen() {

	}
	void draw_line(int lineno, const char* chars, const char* attrib) {
		// scan line is 800 wide, charters are 8 * 10
		uint32_t* scan_line = static_cast<uint32_t*>(_backbuffer.GetBits()) + lineno * 10 * 800;

	}
	void create_backbuffer() {
		if (!_backbuffer.Create(800, 240, 32))
			throw 0;
		int off_x = 0;
		int off_y = 0;
		//COLORREF* bits = (COLORREF*)_backbuffer.GetBits();
		Glyph glyph;
		for (int code = 0; code < 0x80; code += 0x10, off_y += 10) {
			for (int x = 0, c = 0; c < 0xF; c++, x += 8) {
				glyph.load(code + c);
				for (int yy = 0; yy < 10; yy++) {
					uint32_t* bits = static_cast<uint32_t*>(_backbuffer.GetPixelAddress(x, off_y + yy));
					for (int xx = 0; xx < 8; xx++) {
						COLORREF color = glyph.getPixel(xx, yy) ? RGB(255, 255, 255) : RGB(0, 0, 0);
						*bits = color;
						bits++;// += _backbuffer.GetPitch();
						//	_backbuffer.SetPixel(xx + x, off_y + yy, color);
					}
				}
					
			}
		}
	}
	LRESULT OnCreate(LPCREATESTRUCT lpcs)
	{
		CreateSimpleToolBar();
		// set toolbar style to flat look
	//	CToolBarCtrl tool = m_hWndToolBar;
	//	tool.ModifyStyle(0, TBSTYLE_FLAT);
		create_backbuffer();
		_memwnd.Create(m_hWnd, CWindow::rcDefault, _T("MemoryViewWindow"));
		SetTimer(1, 1000);
		_memwnd.ShowWindow(SW_SHOW);
		_memwnd.UpdateWindow();
	//	


		SetMsgHandled(false);
		return 0;
	}
	void OnDestroy() {
		KillTimer(1);
		//_memwnd.DestroyWindow();
		
		PostQuitMessage(0);
		SetMsgHandled(false);
	}
	LRESULT OnEraseBkgnd(HDC hdc)
	{
		CDCHandle  dc(hdc);
		CRect      rc;
		SYSTEMTIME st;
		CString    sTime;

		// Get our window's client area.
		GetClientRect(rc);

		// Build the string to show in the window.
		GetLocalTime(&st);
		sTime.Format(_T("The time is %d:%02d:%02d"),
			st.wHour, st.wMinute, st.wSecond);

		// Set up the DC and draw the text.
		dc.SaveDC();

		dc.SetBkColor(RGB(255, 153, 0));
		dc.SetTextColor(RGB(0, 0, 0));
		dc.ExtTextOut(0, 0, ETO_OPAQUE, rc, sTime,
			sTime.GetLength(), NULL);

		// Restore the DC.
		dc.RestoreDC(-1);
		return 1;    // We erased the background (ExtTextOut did it)
	}
	CImage _backbuffer;
	MemoryView _memwnd;
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

