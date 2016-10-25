#pragma once
#include "global.h"
#include <sstream>


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
	CImage screen;
	CImage background;
	Attribute font;
	int term_width = 82;
	int term_height = 24;
	int x = 0;
	int y = 0;
	int descent = 0;
	int char_width=10;
	int char_height=10;
	boolean underline = false;
	int save_width;
	int style = 0;
	//privatAffineTransform at = new AffineTransform();
	int clip = 0;
	int lampState = 64;
	bool antialiasing = true;
	int line_space = 1;
	double scale_x=1.0, scale_y=1.0;
	virtual int getTermWidth() { return char_width*term_width; }
	virtual int getTermHeight() { return char_height*term_height; }
	virtual int getRowCount() { return term_width; }
	virtual int getColumnCount() { return term_height; }
	virtual int getCharWidth() { return char_width; }
	virtual int getCharHeight() { return char_height; }
	virtual void setFont(Attribute attr) {
		if (attr == 0)
		{
			style = 0;
			underline = false;
		}
		if (attr == 1) {
			style = 1;
		}
		if (attr == 2) {
			underline = true;
		}
		if (attr == 16)
		{
			scale_x = 1.0;  scale_y = 1.0;
			char_width = save_width;
			clip = 0;
		}
		if (attr == 32)
		{
			scale_x = 2.0;  scale_y = 1.0;
			char_width = (2 * save_width);
			clip = 0;
		}
		if (attr == 64)
		{
			scale_x = 2.0;  scale_y = 2.0;
			char_width = (2 * save_width);
			clip = (char_height / 2);
		}
		if (attr == 128)
		{
			scale_x = 2.0;  scale_y = 2.0;
			char_width = (2 * save_width);
			clip = (-char_height / 2);
		}
		font = attr;
	}
	virtual void setCursor(int paramInt1, int paramInt2) = 0;
	virtual void setLEDs(int paramInt) = 0;
	virtual void clear() = 0;
	virtual void draw_cursor() = 0;
	virtual void redraw(int paramInt1, int paramInt2, int paramInt3, int paramInt4) = 0;
	virtual void clear_area(int paramInt1, int paramInt2, int paramInt3, int paramInt4) = 0;
	/*

	void scroll_window(int top, int bot, int dy, COLORREF back, boolean smooth) {
		int w = getTermWidth();
		CDCHandle dc = screen.GetDC();
		if (smooth)
		{
			CBrush c;
			c.CreatePatternBrush
			dc.SetBkColor(back);
			dc.SelectBrush
			Color tcolor = this.graphics.getColor();
			this.graphics.setColor(back);
			try
			{
				if (dy > 0) {
					for (int yt = top; yt < top + dy; yt++)
					{
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


	}
	*/
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
	virtual uint32_t getFGround() = 0;
	virtual uint32_t getBGround() = 0;
};


class VT100
{
	 constexpr  static int ENTER[] = { 13 };
	 constexpr  static int BS[] = { 127 };
	 constexpr  static int DEL[] = { 127 };
	 constexpr  static int ESC[] = { 27 };
	 constexpr  static int UP[] = { 27, 91, 65 };
	 constexpr static int DOWN[] = { 27, 91, 66 };
	constexpr static int RIGHT[] = { 27, 91, 67 };
	constexpr static int LEFT[] = { 27, 91, 68 };
	constexpr static int F1[] = { 27, 91, 80 };
	constexpr static int F2[] = { 27, 91, 81 };
	constexpr static int F3[] = { 27, 91, 82 };
	constexpr static int F4[] = { 27, 91, 83 };
	constexpr static int F5[] = { 27, 91, 116 };
	constexpr static int F6[] = { 27, 91, 117 };
	constexpr static int F7[] = { 27, 91, 118 };
	constexpr static int F8[] = { 27, 91, 73 };
	constexpr static int F9[] = { 27, 91, 119 };
	constexpr static int F10[] = { 27, 91, 120 };
	constexpr static int DSR[] = { 27, 91, 48, 110 };
	constexpr static int CPR[] = { 27, 91 };
	 int term_width = 82;
	 int  term_height = 24;
	 int x = 0;
	 int y = 0;
	 int char_width;
	 int char_height;
	 COLORREF fground = RGB(0, 0xFF, 0x00);
	 COLORREF bground = RGB(0, 0x00, 0x00);
	 COLORREF bfground = RGB(0, 0xFF, 0x00);
	 COLORREF bbground = RGB(0, 0x00, 0x00);
	 uint8_t b;
	 char c;
	 std::string arch;
	 int region_y1 = 1;
	 int region_y2 = 24;
	 int intarg[16];
	 int intargi = 0;
	 bool gotdigits = false;
	 bool on_off = false;
	 bool ignore = false;
	 bool punch_flag = false;
	 bool loop_flag = false;
	 bool cursor_appl = false;
	 bool col132 = false;
	 bool smooth = false;
	 bool reverse = false;
	 bool org_rel = false;
	 bool wrap = false;
	 bool repeat = false;
	 bool interlace = false;
	 bool keypad = false;
	 bool ansi = false;
	 bool newline = false;
	 int char_attr = 0;
	 int char_set = 0;
	 bool g0_graph = false;
	 bool g1_graph = false;
	 bool tab;
	 Term& term;
	 concurrent_queue<uint8_t> m_incomming;
	 concurrent_queue<uint8_t> m_outgoing;
	 uint8_t getChar() { return m_incomming.pop_wait(); }
public:
	VT100(Term& t) : term(t) {}


	void setPunch()
	{
		punch_flag = true;
	}

	void setLoopback(boolean loop)
	{
		loop_flag = loop;
	}

	void Start()
	{
		term_width = term.getColumnCount();
		term_height = term.getRowCount();

		char_width = term.getCharWidth();
		char_height = term.getCharHeight();

		int rx = 0;
		int ry = 0;
		int w = 0;
		int h = 0;

		x = 0;
		y = char_height;
		try
		{
			for (;;)
			{
				b = getChar();

				c = ((char)(b & 0x7F));
				if (loop_flag)
				{
					int loo[] = { b };
					term.sendKeySeq(loo);
				}
				else if (punch_flag)
				{
					int intbyte = b & 127 + (b < 0 ? 128 : 0);

					punch_flag = term.sendOutByte(intbyte);
				}
				else
				{
					b = ((byte)(b & 0x7F));
					char_width = term.getCharWidth();
					char_height = term.getCharHeight();
					ry = y;
					rx = x;
					if (b != 0) {
						if (b == 27)
						{
							b = getChar();
							if (b == 92)
							{
								ignore = false;
							}
							else if (b == 60)
							{
								ansi = true;
							}
							else if (b == 62)
							{
								keypad = false;
							}
							else if (b == 61)
							{
								keypad = true;
							}
							else if (b == 72)
							{
								tab[(x / char_width)] = true;
							}
							else if (b == 80)
							{
								ignore = true;
							}
							else if (b == 35)
							{
								b = getChar();
								term.draw_cursor();
								if (b == 54) {
									term.setFont(32);
								}
								if (b == 51) {
									term.setFont(64);
								}
								if (b == 52) {
									term.setFont(128);
								}
								if (b == 53) {
									term.setFont(16);
								}
								term.setBGround(term.getBGround());
								term.draw_cursor();
							}
							else if (b == 40)
							{
								b = getChar();
								switch (b)
								{
								case 65:
								case 66:
									g0_graph = false; break;
								case 48:
								case 49:
								case 50:
									g0_graph = true;
								}
							}
							else if (b == 41)
							{
								b = getChar();
								switch (b)
								{
								case 65:
								case 66:
									g1_graph = false; break;
								case 48:
								case 49:
								case 50:
									g1_graph = true;
								}
							}
							else if (b == 77)
							{
								term.draw_cursor();
								term.scroll_window((region_y1 - 1) * char_height, (region_y2 - 1) * char_height, char_height, bground, smooth);
								term.draw_cursor();
							}
							else if (b == 68)
							{
								term.draw_cursor();
								term.scroll_window(region_y1 * char_height, region_y2 * char_height, -char_height, bground, smooth);
								term.draw_cursor();
							}
							else if (b != 91)
							{
								err("ESC", b);
								pushChar(b);
							}
							else
							{
								getDigits();
								b = getChar();
								if (b == 65)
								{
									term.draw_cursor();
									if (!gotdigits) {
										intarg[0] = 1;
									}
									y -= intarg[0] * char_height;
									if (y <= 0) {
										y = char_height;
									}
									term.setCursor(x, y);
									term.draw_cursor();
								}
								else if (b == 66)
								{
									term.draw_cursor();
									if (!gotdigits) {
										intarg[0] = 1;
									}
									y += intarg[0] * char_height;
									if (y > term_height * char_height) {
										y = (term_height * char_height);
									}
									term.setCursor(x, y);
									term.draw_cursor();
								}
								else if (b == 67)
								{
									term.draw_cursor();
									if (!gotdigits) {
										intarg[0] = 1;
									}
									x += intarg[0] * char_width;
									if (x >= term_width * char_width) {
										x = ((term_width - 1) * char_width);
									}
									term.setCursor(x, y);
									term.draw_cursor();
								}
								else if (b == 68)
								{
									term.draw_cursor();
									if (!gotdigits) {
										intarg[0] = 1;
									}
									x -= intarg[0] * char_width;
									if (x < 0) {
										x = 0;
									}
									term.setCursor(x, y);
									term.draw_cursor();
								}
								else if (((b == 72 ? 1 : 0) | (b == 102 ? 1 : 0)) != 0)
								{
									term.draw_cursor();
									if (!gotdigits) {
										intarg[0] = (intarg[1] = 1);
									}
									if (intarg[0] == 0) {
										intarg[0] = 1;
									}
									if (intarg[1] == 0) {
										intarg[1] = 1;
									}
									x = ((intarg[1] - 1) * char_width);
									y = (intarg[0] * char_height);
									term.setCursor(x, y);
									term.draw_cursor();
								}
								else if (b == 74)
								{
									int ystart = 0; int yend = term_height * char_height;
									if (intarg[0] == 0) {
										ystart = y - char_height;
									}
									if (intarg[0] == 1) {
										yend = y;
									}
									term.draw_cursor();
									term.clear_area(0, ystart, term_width * char_width, yend);
									term.redraw(0, ystart - char_height, term_width * char_width, yend - ystart + char_height);

									term.draw_cursor();
								}
								else if (b == 75)
								{
									int xstart = 0; int xend = term_width * char_width;
									if (intarg[0] == 0) {
										xstart = x;
									}
									if (intarg[0] == 1) {
										xend = x + char_width;
									}
									term.draw_cursor();
									term.clear_area(xstart, y - char_height, xend, y);
									term.redraw(xstart, y - char_height, xend - xstart, char_height);
									term.draw_cursor();
								}
								else if (b == 82)
								{
									std::cerr << ("VT100-Cursor Position-Wrong direction");
								}
								else if (b == 99)
								{
									reset();
								}
								else if (b == 103)
								{
									if (intarg[0] == 0) {
										tab[(x / char_width)] = false;
									}
									else {
										for (x = 0; x < term_width; x += 1) {
											tab[x] = false;
										}
									}
								}
								else if (((b == 104 ? 1 : 0) | (b == 108 ? 1 : 0)) != 0)
								{
									if (b == 104) {
										on_off = true;
									}
									else {
										on_off = false;
									}
									term.draw_cursor();
									switch (intarg[0])
									{
									case -1:
										cursor_appl = on_off; break;
									case -2:
										ansi = on_off; break;
									case -3:
										col132 = on_off; break;
									case -4:
										smooth = on_off; break;
									case -5:
										reverse = on_off; break;
									case -6:
										org_rel = on_off; break;
									case -7:
										wrap = on_off; break;
									case -8:
										repeat = on_off; break;
									case -9:
										interlace = on_off; break;
									case 20:
										newline = on_off;
									}
									if (reverse)
									{
										fground = bbground;
										bground = bfground;
									}
									else
									{
										fground = bfground;
										bground = bbground;
									}
									term.setFGround(fground);
									term.setBGround(bground);
									term.clear_area(0, 0, term_width * char_width, term_height * char_height);
									term.redraw(0, 0, term_width * char_width, term_height * char_height);
									term.draw_cursor();
								}
								else if (b == 109)
								{
									for (int i = 0; i <= intargi; i++) {
										if (intarg[i] == 0)
										{
											term.draw_cursor();
											term.setFont(0);
											term.draw_cursor();
											term.setFGround(fground);
											term.setBGround(bground);
										}
										else if (intarg[i] == 1)
										{
											term.setFont(1);
										}
										else if (intarg[i] == 4)
										{
											term.setFont(2);
										}
										else if (intarg[i] == 5)
										{
											term.setFGround(Color.cyan);
										}
										else if (intarg[i] == 7)
										{
											term.setFont(8);
											term.setFGround(bground);
											term.setBGround(fground);
										}
									}
								}
								else if (b == 110)
								{
									if (intarg[0] == 0) {
										std::cerr << ("VT100-Ready-Wrong direction");
									}
									if (intarg[0] == 5) {
										term.sendKeySeq(getDSR());
									}
									if (intarg[0] == 6)
									{
										std::stringstream seq;
										seq <<  "\033[" << (x / char_width + 1) << ";" << y / char_height) <<"R";

									}
								}
								else if (b == 114)
								{
									if (!gotdigits)
									{
										intarg[0] = 1; intarg[1] = term_height;
									}
									if (intarg[1] == 0) {
										intarg[1] = term_height;
									}
									if (intarg[1] <= intarg[0]) {
										intarg[1] = (intarg[0] + 1);
									}
									region_y1 = intarg[0];
									region_y2 = intarg[1];
								}
								else if (b == 113)
								{
									if (!gotdigits) {
										intarg[0] = 0;
									}
									for (int i = 0; i <= intargi; i++)
									{
										term.setLEDs(intarg[i]);
										try
										{
											Thread.sleep(100L);
										}
										catch (Exception e) {}
									}
								}
								else
								{
									err("ESC [", b);
								}
							}
						}
						else if ((!ignore) &&

							(b != 3))
						{
							if (b == 7)
							{
								term.beep();
							}
							else if (b == 8)
							{
								term.draw_cursor();
								x -= char_width;
								if (x < 0)
								{
									y -= char_height;
									if (y > 0)
									{
										x = ((term_width - 1) * char_width);
									}
									else
									{
										x = 0; y = char_height;
									}
								}
								term.setCursor(x, y);
								term.draw_cursor();
							}
							else if (b == 9)
							{
								try
								{
									for (int t = x / char_width + 1; (t < term_width) && (tab[t] != 1); t++) {}
									x = (t * char_width);
								}
								catch (Exception e)
								{
									System.out.println(e);
								}
								if (x >= term_width * char_width)
								{
									x = 0;
									y += char_height;
									if (y > region_y2 * char_height) {
										addLine();
									}
								}
								term.draw_cursor();
								term.setCursor(x, y);
								term.draw_cursor();
							}
							else if (b == 14)
							{
								char_set = 1;
							}
							else if (b == 15)
							{
								char_set = 0;
							}
							else if (b == 13)
							{
								term.draw_cursor();
								term.setFont(16);
								x = 0;
								term.setCursor(x, y);
								term.draw_cursor();
							}
							else
							{
								if (((b != 10 ? 1 : 0) & (b != 11 ? 1 : 0) & (b != 12 ? 1 : 0)) != 0)
								{
									if (((b >= 0 ? 1 : 0) & (b < 31 ? 1 : 0)) != 0) {
										err("CTL", b);
									}
									if (x >= term_width * char_width)
									{
										x = 0;
										y += char_height;
										if (y > region_y2 * char_height) {
											addLine();
										}
										rx = x;
										ry = y;
									}
									term.draw_cursor();
									if ((b & 0x80) != 0)
									{
										term.clear_area(x, y - char_height, x + char_width * 2, y);
										byte[] foo = new byte[2];
										foo[0] = b;
										foo[1] = getChar();
										term.drawString(new String(foo, 0, 2, "EUC-JP"), x, y);
										x += char_width;
										x += char_width;
										w = char_width * 2;
										h = char_height;
									}
									else
									{
										term.clear_area(x, y - char_height, x + char_width, y);
										char[] b2 = new char[1];
										if ((((95 <= b) && (b <= 126)) & (char_set == 0 & g0_graph | char_set == 1 & g1_graph))) {
											b2[0] = vt100_graphics[(b - 95)];
										}
										else {
											b2[0] = c;
										}
										term.drawChars(b2, 0, 1, x, y);
										rx = x;
										ry = y;
										x += char_width;
										w = char_width;
										h = char_height;
									}
									term.redraw(rx, ry - char_height, w, h);
									term.setCursor(x, y);
									term.draw_cursor();
								}
								else
								{
									term.draw_cursor();
									if (b != 12)
									{
										if (newline == true)
										{
											term.clear_area(x, y - char_height, x + char_width, y);
											x = 0;
										}
										y += char_height;
									}
									term.setCursor(x, y);
									term.draw_cursor();
								}
								if (y == (region_y2 + 1) * char_height) {
									addLine();
								}
							}
						}
					}
				}
			}
		}
		catch (Exception e) {}
	}

	void err(std::string what, byte b)
	{
		std::stringstream ss;
		ss << what << ": " << (char)b << ":" << std::hex << (b & 0xFF) << std::endl;

		::OutputDebugStringA(ss.str().c_str());
	}

	void getDigits()
	{
		intargi = 0;
		intarg[0] = 0;
		gotdigits = false;
		bool qm = false;
		for (;;)
		{
			try
			{
				b = getChar();
				if (b == 63)
				{
					qm = true;
				}
				else if (b == 59)
				{
					if (qm) {
						intarg[intargi] = (-intarg[intargi]);
					}
					qm = false;
					intargi += 1;
					intarg[intargi] = 0;
				}
				else if ((48 <= b) && (b <= 57))
				{
					intarg[intargi] = (intarg[intargi] * 10 + (b - 48));
					gotdigits = true;
				}
				else
				{
					if (qm) {
						intarg[intargi] = (-intarg[intargi]);
					}
					pushChar(b);
				}
			}
			catch (Exception e) {}
		}
	}

	void addLine()
	{
		term.draw_cursor();
		y -= char_height;
		term.scroll_window(region_y1 * char_height, region_y2 * char_height, -char_height, bground, smooth);
		term.setCursor(x, y);
		term.draw_cursor();
	}



	int[] getCodeENTER()
	{
		return ENTER;
	}

	int[] getCodeBS()
	{
		return BS;
	}

	int[] getCodeDEL()
	{
		return DEL;
	}

	int[] getCodeESC()
	{
		return ESC;
	}

	int[] getCodeUP()
	{
		return UP;
	}

	int[] getCodeDOWN()
	{
		return DOWN;
	}

	int[] getCodeRIGHT()
	{
		return RIGHT;
	}

	int[] getCodeLEFT()
	{
		return LEFT;
	}

	int[] getCodeF1()
	{
		return F1;
	}

	int[] getCodeF2()
	{
		return F2;
	}

	int[] getCodeF3()
	{
		return F3;
	}

	int[] getCodeF4()
	{
		return F4;
	}

	int[] getCodeF5()
	{
		return F5;
	}

	int[] getCodeF6()
	{
		return F6;
	}

	int[] getCodeF7()
	{
		return F7;
	}

	int[] getCodeF8()
	{
		return F8;
	}

	int[] getCodeF9()
	{
		return F9;
	}

	int[] getCodeF10()
	{
		return F10;
	}

	int[] getDSR()
	{
		return DSR;
	}

	int[] getCPR()
	{
		return CPR;
	}

	void reset()
	{
		arch = System.getProperty("os.name");
		term_width = term.getColumnCount();
		term_height = term.getRowCount();
		char_width = term.getCharWidth();
		char_height = term.getCharHeight();

		fground = bfground;
		term.setFGround(fground);
		bground = bbground;
		term.setBGround(bground);

		cursor_appl = false;
		ansi = false;
		col132 = false;
		smooth = false;
		reverse = false;
		org_rel = false;
		wrap = false;
		repeat = false;
		interlace = false;
		newline = false;

		tab = new boolean[term_width];
		for (x = 0; x < term_width; x += 1) {
			if (x % 8 == 0) {
				tab[x] = true;
			}
			else {
				tab[x] = false;
			}
		}
		term.setFont(0);
		char_set = 0;
		g0_graph = false;
		x = 0;
		y = char_height;
		region_y1 = 1;
		region_y2 = term_height;
		term.clear_area(x, y - char_height, term_width * char_width, term_height * char_height);

		term.redraw(x, y - char_height, term_width * char_width - x, term_height * char_height - y + char_height);

		term.setCursor(x, y);
		term.draw_cursor();
	}
};