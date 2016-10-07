#include <Windows.h>
#include <iostream>
#include <fstream>
#include "global.h"

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

class DebugStreamWindow
{
public:
	DebugStreamWindow() : _win(0), _edit(0) {}
	void open(const char fname[] = 0);
	void close() { DestroyWindow(_win); }
	void append(const char text[], int count);
	~DebugStreamWindow() { close(); }
private:
	enum { BUFSIZE = 16000 };
	int removeFirst();
	int getLength(); 
	static LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
	HWND _win;
	HWND _edit;
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



void DebugStreamWindow::open(const char fname[])
/* PURPOSE: Open a window for output logging
RECEIVES: fname - the file name (for the window title)
*/
{
	if (_win) close();
	std::string title;
	if (fname) {
		title += "DebugStream - (";
		title += fname;
		title += ")";
	}

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
	_win = CreateWindow("DEBUGWIN", title.c_str(),
		WS_OVERLAPPEDWINDOW | WS_VISIBLE,
		CW_USEDEFAULT, CW_USEDEFAULT,
		CW_USEDEFAULT, CW_USEDEFAULT,
		NULL, NULL,
		NULL, (LPSTR) this);
}
LRESULT CALLBACK DebugStreamWindow::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
	DebugStreamWindow* thisWin = reinterpret_cast<DebugStreamWindow*>(reinterpret_cast<void*>(GetWindowLong(hWnd, 0)));
	// caveat: this pointer is 0 until after WM_CREATE;
	switch (message)
	{
	case WM_CREATE:
	{
		HMENU my_menu;
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