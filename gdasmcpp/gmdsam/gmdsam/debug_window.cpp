#include <Windows.h>
#include <iostream>
#include <fstream>
#include "global.h"
#include <thread>
#include <atomic>

namespace ext {
	std::ostream& set_format::operator<<(std::ostream& os) const {
		int i = 0;
		while (_fmt[i] != 0)
		{
			if (_fmt[i] != '%') { os << _fmt[i]; i++; }
			else
			{
				i++;
				if (_fmt[i] == '%') { os << _fmt[i]; i++; }
				else
				{
					bool ok = TRUE;
					int istart = i;
					bool more = TRUE;
					int width = 0;
					int precision = 6;
					long flags = 0;
					char fill = ' ';
					bool alternate = FALSE;
					while (more)
					{
						switch (_fmt[i])
						{
						case '+':
							flags |= std::ios::showpos;
							break;
						case '-':
							flags |= std::ios::left;
							break;
						case '0':
							flags |= std::ios::internal;
							fill = '0';
							break;
						case '#':
							alternate = TRUE;
							break;
						case ' ':
							break;
						default:
							more = FALSE;
							break;
						}
						if (more) i++;
					}
					if (isdigit(_fmt[i]))
					{
						width = atoi(_fmt + i);
						do i++; while (isdigit(_fmt[i]));
					}
					if (_fmt[i] == '.')
					{
						i++;
						precision = atoi(_fmt + i);
						while (isdigit(_fmt[i])) i++;
					}
					switch (_fmt[i])
					{
					case 'd':
						flags |= std::ios::dec;
						break;
					case 'x':
						flags |= std::ios::hex;
						if (alternate) flags |= std::ios::showbase;
						break;
					case 'X':
						flags |= std::ios::hex | std::ios::uppercase;
						if (alternate) flags |= std::ios::showbase;
						break;
					case 'o':
						flags |= std::ios::hex;
						if (alternate) flags |= std::ios::showbase;
						break;
					case 'f':
						flags |= std::ios::fixed;
						if (alternate) flags |= std::ios::showpoint;
						break;
					case 'e':
						flags |= std::ios::scientific;
						if (alternate) flags |= std::ios::showpoint;
						break;
					case 'E':
						flags |= std::ios::scientific | std::ios::uppercase;
						if (alternate) flags |= std::ios::showpoint;
						break;
					case 'g':
						if (alternate) flags |= std::ios::showpoint;
						break;
					case 'G':
						flags |= std::ios::uppercase;
						if (alternate) flags |= std::ios::showpoint;
						break;
					default:
						ok = FALSE;
						break;
					}
					i++;
					if (_fmt[i] != 0) ok = FALSE;
					if (ok)
					{
						os.unsetf(std::ios::adjustfield | std::ios::basefield |
							std::ios::floatfield);
						os.setf(flags);
						os.width(width);
						os.precision(precision);
						os.fill(fill);
					}
					else i = istart;
				}
			}
		}
		return os;
	}
};
using namespace std;
void PrintLastError(const char *msg /* = "Error occurred" */) {
	DWORD errCode = GetLastError();
	char *err;
	if (!FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM,
		NULL,
		errCode,
		MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), // default language
		(LPTSTR)&err,
		0,
		NULL))
		return;

	static char buffer[1024];
	_snprintf_s(buffer, sizeof(buffer), "ERROR: %s: %s\n", msg, err);
	//debug::cerr << ((const char*)buffer) << std::end;

	OutputDebugString(buffer); // or otherwise log it
	LocalFree(err);
}

class DebugStreamWindow
{
public:
	DebugStreamWindow() noexcept : _win(0), _edit(0), _thread_running{ ATOMIC_FLAG_INIT } {  }
	DebugStreamWindow(const DebugStreamWindow& copy) = delete;
	DebugStreamWindow& operator=(const DebugStreamWindow&) = delete;
//	DebugStreamWindow& operator=(const DebugStreamWindow&) volatile = delete;
	void open(const char fname[] = 0);
	void close() { DestroyWindow(_win); }
	void append(const char text[], int count);
	~DebugStreamWindow() { close(); }
private:
	enum { BUFSIZE = 16000 };
	int removeFirst();
	int getLength(); 
	static LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
	static void window_thread(DebugStreamWindow * ptr) {
		std::string title="DebugStream";
		/*
	
		if (fname) {
			title += "DebugStream - (";
			title += fname;
			title += ")";
		}
		*/
		//HINSTANCE hInstance = GetModuleHandle(0);
		WNDCLASS wndclass;
		wndclass.style = CS_HREDRAW | CS_VREDRAW;
		wndclass.lpfnWndProc = WndProc;
		wndclass.cbClsExtra = 0;
		wndclass.cbWndExtra = sizeof(void far*);
		wndclass.hInstance = NULL;
		wndclass.hIcon = LoadIcon(NULL, IDI_ASTERISK);
		wndclass.hCursor = LoadCursor(NULL, IDC_ARROW);
		wndclass.hbrBackground = (HBRUSH)GetStockObject(WHITE_BRUSH);
		wndclass.lpszMenuName = NULL;
		wndclass.lpszClassName = "DEBUGWIN";
		RegisterClass(&wndclass);
		ptr->_win = CreateWindow("DEBUGWIN", title.c_str(),
			WS_OVERLAPPEDWINDOW | WS_VISIBLE,
			CW_USEDEFAULT, CW_USEDEFAULT,
			CW_USEDEFAULT, CW_USEDEFAULT,
			NULL, NULL,
			NULL, (LPSTR)ptr);
		MSG msg;
		BOOL bRet;
		ptr->_thread_running.test_and_set();
		while (ptr->_thread_running.test_and_set() && (bRet = GetMessage(&msg, ptr->_win, 0, 0)) != 0)
		{
			if (bRet == -1)
			{
			//	auto error = GetLastError();
				PrintLastError("ERROR! %s");
				//debug::cerr << "Error message" << std::endl;
				// handle the error and possibly exit
			}
			else
			{
				TranslateMessage(&msg);
				DispatchMessage(&msg);
			}
		}
		debug::cerr << "Window Closed" << std::endl;
		//return msg.wParam;
	}
	std::atomic_flag _thread_running;
	HWND _win, _edit;
	//std::atomic<HWND> _win;
	//std::atomic<HWND> _edit;
};

class DebugStreamBuffer : public std::filebuf
{
public:
	DebugStreamBuffer() { filebuf::open("NUL", ios::out); }
	void open(const char fname[])
	{
		close();
		filebuf::open(fname ? fname : "NUL",
			ios::out | ios::app | ios::trunc);
		_win.open(fname);
	}
	virtual int sync() override
	{
		std::ptrdiff_t n = pptr() - pbase();
		if (n > 0) {
			_win.append(pbase(), (int)n);
		}
		
		return filebuf::sync();
	}
	void close() { _win.close(); filebuf::close(); }
private:
	DebugStreamWindow _win;

};

class DebugStream : public std::ostream
{
	DebugStreamBuffer* _buf;
public:
	DebugStream() : _buf(new DebugStreamBuffer()), std::ostream(_buf) { _buf->open("debug.txt"); }
	~DebugStream() { delete _buf; }
	void open(const char fname[] = 0) { _buf->open(fname); }
	void close() { _buf->close(); }
};

static void window_thread(void * ptr) {

}

void DebugStreamWindow::open(const char fname[])
/* PURPOSE: Open a window for output logging
RECEIVES: fname - the file name (for the window title)
*/
{
	if (_win) close();
	std::thread test(DebugStreamWindow::window_thread,this);
	test.detach();
}
LRESULT CALLBACK DebugStreamWindow::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
	DebugStreamWindow* thisWin = reinterpret_cast<DebugStreamWindow*>(reinterpret_cast<void*>(GetWindowLong(hWnd, 0)));
	// caveat: this pointer is 0 until after WM_CREATE;
	switch (message)
	{
	case WM_CREATE:
	{
		HMENU my_menu = NULL;
		LPCREATESTRUCT lpcs = (LPCREATESTRUCT)lParam;
		thisWin = reinterpret_cast<DebugStreamWindow*>(lpcs->lpCreateParams);
		SetWindowLong(hWnd, 0, reinterpret_cast<LONG>(thisWin));
		thisWin->_edit = CreateWindow("EDIT", NULL,
			WS_CHILD | WS_VISIBLE | WS_HSCROLL | WS_VSCROLL |
			ES_LEFT | ES_MULTILINE | ES_READONLY |
			ES_AUTOHSCROLL | ES_AUTOVSCROLL,
			0, 0, 0, 0,
			hWnd, my_menu,
			lpcs->hInstance, NULL);
		return 0;
	}
	case WM_SIZE:
		MoveWindow(thisWin->_edit, 0, 0, LOWORD(lParam),
			HIWORD(lParam), TRUE);
		return 0;
	case WM_DESTROY:
		thisWin->_thread_running.clear();
		thisWin->_win = 0;
		thisWin->_edit = 0;
		break;
	}
	return DefWindowProc(hWnd, message, wParam, lParam);
}
void DebugStreamWindow::append(const char text[], int count)
/* PURPOSE:    Append text to the output window
RECEIVES:   text - the start of the text
count - the number of bytes
REMARKS:    This function performs \n -> \r\n translation
*/
{
	if (!_win) open();
	if (!_edit || !count) return;
	char* t = new char[2 * count + 1]; // worst case
	if (t == 0) return; // out of memory
	int tlen = 0; // index into t
	for (int i = 0; i < count; i++)
	{
		if (text[i] == '\n')
			t[tlen++] = '\r';
		t[tlen++] = text[i];
	}
	t[tlen] = 0;
	int nchar = getLength();
	while (nchar > 0 && nchar + tlen > BUFSIZE)
		nchar -= removeFirst();
	SendMessage(_edit, EM_SETSEL, 0, MAKELONG(nchar, nchar));
	SendMessage(_edit, EM_REPLACESEL, 0, (long)(const char far*)t);
	SendMessage(_edit, EM_SETREADONLY, TRUE, 0);
	delete[] t;
}
int DebugStreamWindow::getLength()
/* PURPOSE:    Get the length of the text in the edit box
*/
{
	if (!_edit) return 0;
	int linecount = SendMessage(_edit, EM_GETLINECOUNT, 0, 0L);
	int nlast = SendMessage(_edit, EM_LINEINDEX, linecount - 1, 0L);
	if (nlast < 0) nlast = 0;
	else nlast += SendMessage(_edit, EM_LINELENGTH, nlast, 0L);
	return nlast;
}
int DebugStreamWindow::removeFirst()
/* PURPOSE:    Remove the first line in the edit box
RETURNS:    The length of the removed line
*/
{
	if (!_edit) return 0;
	int nfirst = SendMessage(_edit, EM_LINEINDEX, 1, 0L);
	if (nfirst >= 0)
	{
		SendMessage(_edit, EM_SETSEL, 0, MAKELONG(0, nfirst));
		SendMessage(_edit, EM_REPLACESEL, 0,
			(long)(const char far*)"");
		return nfirst;
	}
	else return 0;
}

#include <windows.h>
#include <atldef.h>
#include <windows.h>
#include <atlbase.h>
#include <atlapp.h>
#include <atlimage.h>
#include <atlwinx.h>

#include <atlwin.h>
#include <atlwin.h>        // ATL GUI classes</span>
#include <atlframe.h>      // WTL frame window classes</span>
//#include <atlmisc.h>       // WTL utility classes like CString</span>
#include <atlcrack.h>      // WTL enhanced msg map macros</span>

DebugStreamWindow* _test;
std::vector<uint8_t> s_rom;
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

CAppModule _Module;
typedef CWinTraits<WS_OVERLAPPEDWINDOW, 0> CDxAppWinTraits;
//template <class T>//, class TBase = CWindow, class TWinTraits = CDxAppWinTraits >
class AppWindow : public CWindowImpl<AppWindow, CWindow, CDxAppWinTraits >
{
public:
	BEGIN_MSG_MAP(CDxWindowImpl)
		//MESSAGE_HANDLER(WM_DESTROY, OnDestroy)
		MSG_WM_CREATE(OnCreate)
		MSG_WM_DESTROY(OnDestroy)
		MSG_WM_PAINT(OnPaint)
		MSG_WM_TIMER(OnTimer)
		MSG_WM_ERASEBKGND(OnEraseBkgnd)
		//MESSAGE_HANDLER(WM_DESTROY, OnCreate)
		//ON_PAINT
	END_MSG_MAP()
	DECLARE_WND_CLASS("Main Window")
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
				for (int yy = 0; yy < 10; yy++)
					for (int xx = 0; xx < 8; xx++) {
						COLORREF color = glyph.getPixel(xx, yy) ? RGB(255, 255, 255) : RGB(0, 0, 0);
						_backbuffer.SetPixel(xx + x, off_y + yy, color);
					}
			}
		}
	}
	LRESULT OnCreate(LPCREATESTRUCT lpcs)
	{
		create_backbuffer();
		SetTimer(1, 1000);



		SetMsgHandled(false);
		return 0;
	}
	void OnDestroy() {
		KillTimer(1);
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
};

//ROM_REGION(0x10000, "maincpu", ROMREGION_ERASEFF)
//ROM_DEFAULT_BIOS("vt100")
//ROM_SYSTEM_BIOS(0, "vt100o", "VT100 older roms")
// ROMX_LOAD("23-031e2-00.e56", 0x0000, 0x0800, NO_DUMP, ROM_BIOS(1)) // version 1 1978 'earlier rom', dump needed, correct for earlier vt100s
//ROM_SYSTEM_BIOS(1, "vt100", "VT100 newer roms")
// ROMX_LOAD("23-061e2-00.e56", 0x0000, 0x0800, CRC(3dae97ff) SHA1(e3437850c33565751b86af6c2fe270a491246d15), ROM_BIOS(2)) // version 2 1979 or 1980 'later rom', correct for later vt100s
// ROM_LOAD("23-032e2-00.e52", 0x0800, 0x0800, CRC(3d86db99) SHA1(cdd8bdecdc643442f6e7d2c83cf002baf8101867))
// ROM_LOAD("23-033e2-00.e45", 0x1000, 0x0800, CRC(384dac0a) SHA1(22aaf5ab5f9555a61ec43f91d4dea3029f613e64))
// ROM_LOAD("23-034e2-00.e40", 0x1800, 0x0800, CRC(4643184d) SHA1(27e6c19d9932bf13fdb70305ef4d806e90d60833))

//ROM_REGION(0x1000, "chargen", 0)
//ROM_LOAD("23-018e2-00.e4", 0x0000, 0x0800, CRC(6958458b) SHA1(103429674fc01c215bbc2c91962ae99231f8ae53))

void init_test() {
	std::ifstream rom_file;
	rom_file.open("roms/23-018e2-00.e4", std::ios::binary);
	if (!rom_file.good()) 
		throw 0;
	s_rom.resize(0x800);
	rom_file.read((char*)s_rom.data(), 0x800);
	rom_file.close();
	
	CMessageLoop messageLoop;
	AppWindow mainwnd;

	_Module.Init(NULL, NULL);
	_Module.AddMessageLoop(&messageLoop);


	mainwnd.Create(NULL, CWindow::rcDefault, _T("Main Window"));
	if (mainwnd == NULL)
		return;

	mainwnd.ShowWindow(SW_SHOW);
	mainwnd.UpdateWindow();

	int nRet = messageLoop.Run();

	_Module.RemoveMessageLoop();
	_Module.Term();

	if (_test == nullptr) {
		_test = new DebugStreamWindow();
		_test->open("try");
	}
}
