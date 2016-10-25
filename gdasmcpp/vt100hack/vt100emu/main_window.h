#pragma once
#include "global.h"
#include "terminal_view.h"

//template <class T>//, class TBase = CWindow, class TWinTraits = CDxAppWinTraits >
class AppWindow : public CFrameWindowImpl<AppWindow>    //CWindowImpl<AppWindow, CWindow, CDxAppWinTraits >
{
	TerminalView m_term;

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
			//if (_keys.size() > 0) {
			//	sim->kbd.keys_press(_keys);
			//	_keys.clear();
			//}

			//	TCHAR cArray[1000];
			//	m_edit.SetWindowText(cArray);
			//RedrawWindow();
		}
	}
	bool moving = false;
	void OnSize(UINT func, CSize size) {
		m_term.ResizeClient(size.cx, size.cy, true);
	}
	int inc = 1;
	void OnKeyDown(TCHAR ch, UINT status, UINT status0) {
		if (moving) return;
		moving = true;
		switch (ch) {
		case VK_UP: m_term.scroll(inc); break;
		case VK_DOWN:m_term.scroll(-inc); break;
		case VK_LEFT: inc--; break;
		case VK_RIGHT: inc++; break;

		}
	}
	void OnKeyUp(TCHAR ch, UINT status, UINT status0) {
		if (!moving) return;
		moving = false;
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

		SetTimer(2, 1000 / 15);
//		_memwnd.Create(m_hWnd, CWindow::rcDefault, _T("MemoryViewWindow"));
	//	_memwnd.ShowWindow(SW_HIDE);
	//	_memwnd.UpdateWindow();


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

