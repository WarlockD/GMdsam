#pragma once
#include <Windows.h>
#include <vector>
#include <iostream>
#include <fstream>

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
			for (int b = 0; b < 8; b++) {// 0..7
										 //uint8_t bit = BIT((line << b), 7);
				data[b + j * 8] = BIT((line << b), 7) ? true : false;
			}
		}
	}
	bool getPixel(int x, int y) const { return data[x + y * 8]; }
};

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
		Line() : double_height(false), double_width(false), scroll(true), line(83) {}
	};
	std::vector<Line> _line_alloc;
	std::vector<Line*> _lines;
	CImage _screen;
public:
	TerminalView() : _line_alloc(25), _lines(25) {
		for (size_t i = 0; i < 25; i++) {
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
	//	MSG_WM_TIMER(OnTimer)
		//	MSG_WM_ERASEBKGND(OnEraseBkgnd)
		//MESSAGE_HANDLER(WM_DESTROY, OnCreate)
		//ON_PAINT
	END_MSG_MAP()
	DECLARE_WND_CLASS("TerminalView")
	LRESULT OnCreate(LPCREATESTRUCT lpcs) {
		for (size_t i = 0; i < 25; i++) {
			_lines[i] = _line_alloc.data() + i;
		}
		if (!_screen.Create(800, 240, 32))
			throw 0;
		putString(10, 10, "fuck me");
		refresh_screen();
	}
	void putString(int x, int y, const std::string& str) {
		Line& line = *_lines.at(y);
		for (size_t i = x; i < line.line.size() && i < str.size(); i++) {
			line.line[i].ch = str.at(i);
		}
	}
	void refresh_screen() {
		for (size_t l = 0; l < 24; l++) {
			Line& line = *_lines.at(l);
			for (size_t c = 0; c < line.line.size(); c++) {
				auto info = line.line.at(c);
				bool last_bit = false;
				for (size_t scan_line = 0; scan_line < 10; scan_line++) {
					uint32_t* bits = static_cast<uint32_t*>(_screen.GetPixelAddress(c * 8, scan_line + l * 10));
					uint8_t char_line = s_rom[(info.ch << 4) + scan_line];
					for (int b = 0; b < 8; b++) {
						bool bit = BIT((char_line << b), 7) ? true : false;
						if (!bit && last_bit) { bit = true; last_bit = false; }
						else last_bit = bit;
						bits[b] = bit ? RGB(0, 255, 0) : RGB(255, 255, 255);
					}
				}

			}

		}
	}
	void OnDestroy() {
		_screen.Destroy();
	}
	void OnPaint(HDC hdc) {
		CPaintDC dc(*this);
		//CRect rect;
		//GetClientRect(&rect);
		refresh_screen();
		//_screen.Draw(dc, rect);
		_screen.Draw(dc, 0,0);
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
	void OnPaint(HDC hdc) {
		CPaintDC dc(*this);
		TEXTMETRIC font_metric;
		CSize size;
		CPoint point;
		CRect rect;
		CRect client_rect;
		dc.GetTextMetricsA(&font_metric);
		GetClientRect(&client_rect);
		dc.FillSolidRect(&client_rect, RGB(255, 255, 255));
		// black
		//	int my, mx;
		//	getmaxyx(memWin, my, mx);
		//	int bavail = (mx - 7) / 3;
		int bdisp = 16;
		//	while (bdisp * 2 <= bavail) bdisp *= 2;

		_tabs.clear();
		point = CPoint();
		_tabs.push_back(0);
		point.x = font_metric.tmMaxCharWidth * 5;
		point.y = 20;
		dc.SetTextColor(RGB(0, 0, 0));

		for (int b = 0; b < bdisp; b++) {
			rect = CRect(point, CSize(0, 0));
			int count = sprintf_s(_buffer, " %02X", b);
			//dc.GetTextExtent(_buffer, count, &size);
			//	dc.TextOutA()
			dc.DrawTextA(_buffer, count, &rect, DT_SINGLELINE | DT_NOCLIP); // | DT_CALCRECT); // DT_CALCRECT
		//	_tabs.push_back(point.x);
			//dc.GetTextExtent(_buffer, count, &size);
			point.x += font_metric.tmMaxCharWidth * 3;
		}
		uint16_t start = 0x2000;
		int tab = 0;
		for (int y = 1; y < 30; y++) {
			dc.SetTextColor(RGB(0, 0, 0));
			point.y = y * font_metric.tmHeight;
			point.x = 0;
			int count = sprintf_s(_buffer, "%04X:", start);
			dc.TextOutA(point.x, point.y, _buffer);
			point.x += font_metric.tmMaxCharWidth * 5;
			for (int b = 0; b < bdisp; b++) {
				if (!touched[start])
					dc.SetBkColor(RGB(255, 255, 255));
				else
					dc.SetBkColor(RGB(0, 255, 0));
				if (ram[start] == 00)
					dc.SetTextColor(RGB(127, 127, 127));
				else
					dc.SetTextColor(RGB(0, 0, 0));
				count = sprintf_s(_buffer, "%02X", ram[start++]);
				
				dc.TextOutA(point.x, point.y, _buffer);
				point.x += font_metric.tmMaxCharWidth * 3;

			}
		}
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
		m_term.Create(m_hWnd, rcHorz, NULL, WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN);
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
		_backbuffer.Draw(dc, 0, 0);
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

