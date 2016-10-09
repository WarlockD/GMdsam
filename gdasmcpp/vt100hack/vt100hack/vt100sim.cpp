
#include "vt100sim.h"
#include "8080/sim.h"
#include "8080/simglb.h"
#include <stdio.h>
//#include <unistd.h>
#include <string.h>
#include <curses.h>
#include <thread>
#ifdef min
#undef min
#undef max
#endif
#include <time.h>
#include <signal.h>
#include <map>
#include <ctype.h>
bool vsync_happened = false;

extern void int_on(void), int_off(void);
extern void init_io(void), exit_io(void);

extern void cpu_z80(void), cpu_8080(void);
extern void disass(unsigned char **, int);
extern int exatoi(char *);
extern int getkey(void);
extern void int_on(void), int_off(void);
extern int load_file(char *);



Vt100Sim* sim;

WINDOW* regWin;
WINDOW* memWin;
WINDOW* vidWin;
WINDOW* msgWin;
WINDOW* statusBar;
WINDOW* bpWin;

Vt100Sim::Vt100Sim(const char* romPath, bool running, bool avo_on) :
	running(running), inputMode(false),
	dc12(true), controlMode(!running),
	enable_avo(avo_on)
{
  this->romPath = romPath;
  base_attr = 0;
  screen_rev = 0;
  blink_ff = 0;

  //breakpoints.insert(8);
  //breakpoints.insert(0xb);
  initscr();
  int my,mx;
  getmaxyx(stdscr,my,mx);
  start_color();
  raw();
  nonl();
  noecho();
  keypad(stdscr,1);
  nodelay(stdscr,1);
  curs_set(0);

  // Status bar: bottom line of the screen
  statusBar = subwin(stdscr,1,mx,--my,0);
  const int vht = std::min(27,my-12); // video area height (max 27 rows)
  int memw = 7 + 32*3 - 1 + 2; // memory area width: big enough for 32B across
  const int regw = 12;
  const int regh = 8;

  if (memw > mx -regw - 20) memw = 7 + 16*3 - 1 + 2; // Okay, make that 16
  const int msgw = mx - (regw+memw); // message area: mx - memory area - register area (12)

  if (mx > 134)
    vidWin = subwin(stdscr,vht,134,my-vht,0);
  else
    vidWin = subwin(stdscr,vht,mx,my-vht,0);
  regWin = subwin(stdscr,regh,regw,0,0);
  bpWin = subwin(stdscr,my-(vht+regh),regw,regh,0);
  memWin = subwin(stdscr,my-vht,memw,0,regw);
  msgWin = subwin(stdscr,my-vht,msgw,0,regw+memw);

  scrollok(msgWin,1);
  box(regWin,0,0);
  mvwprintw(regWin,0,1,"Registers");
  box(memWin,0,0);
  mvwprintw(memWin,0,1,"Mem");
  box(vidWin,0,0);
  mvwprintw(vidWin,0,1,"Video");
  box(bpWin,0,0);
  mvwprintw(bpWin,0,1,"Brkpts");
  init_pair(1,COLOR_RED,COLOR_BLACK);
  init_pair(2,COLOR_BLUE,COLOR_BLACK);
  init_pair(3,COLOR_YELLOW,COLOR_BLACK);
  init_pair(4,COLOR_GREEN,COLOR_BLACK);
  wattron(regWin,COLOR_PAIR(1));
  wattron(memWin,COLOR_PAIR(2));
  refresh();
}

Vt100Sim::~Vt100Sim() {
  curs_set(1);
  endwin();
}

class Signal {
private:
    bool value;
    bool has_change;
    uint16_t period_half;
    uint16_t ticks;
public:
    Signal(uint16_t period);
    bool add_ticks(uint16_t delta);
    bool get_value() { return value; }
};

Signal::Signal(uint16_t period) : value(false),has_change(false),period_half(period/2),ticks(0) {
}

bool Signal::add_ticks(uint16_t delta) {
    ticks += delta;
    if (ticks >= period_half) {
        ticks -= period_half;
        value = !value;
        return true;
    }
    return false;
}

// In terms of processor cycles:
// LBA4 : period of 22 cycles
// LBA7 : period of 182 cycles
// Vertical interrupt: period of 46084 cycles
Signal lba4(22);
Signal lba7(182);
Signal vertical(46084);
Signal uartclk(5000); // uart clock is super arbitrary

void Vt100Sim::init() {
    i_flag = 1;
    f_flag = 0;
    m_flag = 0;
    tmax = f_flag*10000;
    cpu = I8080;
    wprintw(msgWin,"\nRelease %s, %s\n", RELEASE, COPYR);
#ifdef	USR_COM
    wprintw(msgWin,"\n%s Release %s, %s\n", USR_COM, USR_REL, USR_CPR);
#endif

    //printf("Prep ram\n");
    //fflush(stdout);
    wrk_ram	= PC = ram;
    STACK = ram + 0xffff;
    if (cpu == I8080)	/* the unused flag bits are documented for */
        F = 2;		/* the 8080, so start with bit 1 set */
    memset((char *)	ram, m_flag, 65536);
    memset((char *)	touched, m_flag, 65536);
    // load binary
    wprintw(msgWin,"Loading rom %s...\n",romPath);
    wrefresh(msgWin);
    FILE* romFile = fopen(romPath,"rb");
    if (!romFile) {
      wprintw(msgWin,"Failed to read rom file\n");
      wrefresh(msgWin);
        return;
    }
    uint32_t count = fread((char*)ram,1,2048*4,romFile);
    //printf("Read ROM file; %u bytes\n",count);
    fclose(romFile);
    int_on();
    // add local io hooks
    
    i_flag = 0;

    // We are always running the CPU in single-step mode so we can do the clock toggles when necessary.
    cpu_state = SINGLE_STEP;

    wprintw(msgWin,"Function Key map:\n");
    wprintw(msgWin,"F1..F4 -> PF1..PF4\n");
    wprintw(msgWin,"F6 -> Break\n");
    wprintw(msgWin,"F7 -> S-Break\n");
    wprintw(msgWin,"F8 -> Escape\n");
    wprintw(msgWin,"F9 -> Setup\n");
    wprintw(msgWin,"F10 -> Cmd Mode\n");
    wprintw(msgWin,"F11 -> Keycodes\n");
    wrefresh(msgWin);
}

BYTE Vt100Sim::ioIn(BYTE addr) {
  if (addr == 0x00) {
    uint8_t r = uart.read_data();
    //wprintw(msgWin,"PUSART RD DAT: %x\n", r);
    return r;
  } else if (addr == 0x01) {
    uint8_t r = uart.read_command();
    //wprintw(msgWin,"PUSART RD CMD: %x\n", r);
    return r;
  } else if (addr == 0x42) {
        // Read buffer flag
	/*
	    flags:
		AVO	on=0x00, off=0x02
		GPO	on=0x00, off=0x04
		STP	on=0x08, off=0x00
	 */
        uint8_t flags = 0x06;	/* STP off, GPO off, AVO off */
	if (enable_avo)
	    flags = 0x04;

        if (lba7.get_value()) {
            flags |= 0x40;
        }
        if (nvr.data()) {
            flags |= 0x20;
        }
	if (uart.xmit_ready()) {
	  flags |= 0x01;
	}
        if (t_ticks % 2000 < 100) { flags |= 0x10; flags |= 0x80; }
        if (kbd.get_tx_buf_empty()) {
            flags |= 0x80; // kbd ready?
        }
        //printf(" IN PORT %02x -- %02x\n",addr,flags);fflush(stdout);
        return flags;
    } else if (addr == 0x82) {
      return kbd.get_latch();
    } else {
      //printf(" IN PORT %02x at %04lx\n",addr,PC-ram);fflush(stdout);
    }
    return 0;
}

void Vt100Sim::ioOut(BYTE addr, BYTE data) {
    switch(addr) {
    case 0x00:
      //wprintw(msgWin,"PUSART DAT: %x\n", data);wrefresh(msgWin);
      uart.write_data(data);
      break;
    case 0x01:
      //wprintw(msgWin,"PUSART CMD: %x\n", data);wrefresh(msgWin);
      uart.write_command(data);
      break;
    case 0x02:
      break;
    case 0x82:
        kbd.set_status(data);
        break;
    case 0x62:
        nvr.set_latch(data);
	break;
    case 0x42:
      bright = data;
      break;

    case 0xa2:
      //wprintw(msgWin,"DC12 %02x\n",data);
      //wrefresh(msgWin);
      dc12 = true;
      switch (data & 0xF) {
      case 0: case 1: case 2: case 3:
         scroll_latch = ((scroll_latch & 0x0C) | (data & 0x3));
	 break;
      case 4: case 5: case 6: case 7:
         scroll_latch = ((scroll_latch & 0x03) | ((data & 0x3)<<2));
	 break;
      case 8: /* Toggle blink FF */
	 blink_ff = ~blink_ff;
         break;
      case 9: /* Vertical retrace clear. */
         break;
      case 10: screen_rev = 0x80; break;
      case 11: screen_rev = 0; break;

      case 12: base_attr = 1; blink_ff = 0; break;	/* Underline */
      case 13: base_attr = 0; blink_ff = 0; break;	/* Reverse */
      case 14: case 15: blink_ff = 0; break;
      }
      break;

    case 0xc2:
      //wprintw(msgWin,"DC11 %02x\n",data);
      //wrefresh(msgWin);
      break;

    default:
	wprintw(msgWin,"OUT PORT %02x <- %02x\n",addr,data);
	wrefresh(msgWin);
	break;
    }
}


static std::atomic_flag sigAlrm = { 0 };

std::map<int,uint8_t> make_code_map() {
  std::map<int,uint8_t> m;
  // 0x01, 0x02 (both) -> del
  m[KEY_DC] = 0x03;
  // ??? m[KEY_ENTER] = 0x04;
  // 0x04 -> nul
  m['p'] = 0x05;
  m['o'] = 0x06;
  m['y'] = 0x07;
  m['t'] = 0x08;
  m['w'] = 0x09;
  m['q'] = 0x0a;

  // 0x0b, 0x0c, 0x0d, 0x0e, 0x0f (Mirror of next 5)

  m[KEY_RIGHT] = 0x10;
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

  m[KEY_LEFT] = 0x20;
  // 0x21 -> nul
  m[KEY_DOWN] = 0x22;
  m[KEY_BREAK] = 0x23; m[KEY_F(6)] = 0x23; m[KEY_F(7)] = 0xA3;

  m['`'] = 0x24; m['~'] = 0xa4;
  m['-'] = 0x25; m['_'] = 0xa5;
  m['9'] = 0x26; m['('] = 0xa6;
  m['7'] = 0x27; m['&'] = 0xa7;
  m['4'] = 0x28; m['$'] = 0xa8;
  m['3'] = 0x29; m['#'] = 0xa9;
  m[KEY_CANCEL] = 0x2a;	m[KEY_F(8)] = 0x2a; // Escape Key

  // 0x2b, 0x2c, 0x2d, 0x2e, 0x2f (Mirror of next 5)

  m[KEY_UP] = 0x30;
  m[KEY_F(3)] = 0x31;
  m[KEY_F(1)] = 0x32;
  m[KEY_BACKSPACE] = 0x33;
  m['='] = 0x34; m['+'] = 0xb4;
  m['0'] = 0x35; m[')'] = 0xb5;
  m['8'] = 0x36; m['*'] = 0xb6;
  m['6'] = 0x37; m['^'] = 0xb7;
  m['5'] = 0x38; m['%'] = 0xb8;
  m['2'] = 0x39; m['@'] = 0xb9;
  m['\t'] = 0x3a;

  // 0x3b, 0x3c, 0x3d, 0x3e, 0x3f (Mirror of next 5)

  // 0x40 -> '7'	(Keypad ^[Ow)
  m[KEY_F(4)] = 0x41;
  m[KEY_F(2)] = 0x42;
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
  m['\r'] = 0x64;
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
  m[KEY_F(9)] = 0x7b;

  // 0x7c   Control Key
  // 0x7d   Shift Key
  // 0x7e
  // 0x7f

  for (int i = 0; i < 26; i++) {
    m['A'+i] = m['a'+i] | 0x80;
  }
  return m;
}

std::map<int,uint8_t> code = make_code_map();



bool hexParse(char* buf, int n, uint16_t& d) {
  d = 0;
  for (int i = 0; i < n; i++) {
    char c = buf[i];
    if (c == 0) return true;
    d = d << 4;
    if (c >= '0' && c <= '9') { d |= (c-'0'); }
    else if (c >= 'a' && c <= 'f') { d |= ((c-'a')+10); }
    else if (c >= 'A' && c <= 'F') { d |= ((c-'A')+10); }
    else {
      return false;
    }
  }
  return true;
}
StopWatch sig_alrm;

void Vt100Sim::run() {
	const int CPUHZ = 1000000;
	const int alrm_interval = 1000000 / 20;   // 20 Times per second.
   // std::chrono::duration<double>(std::chrono::system_clock::time_point::)
	sig_alrm.setFunction([](double dt) {  sigAlrm.clear();  });
	sig_alrm.start(1.0 / 20.0);// 20 Times per second.

	int steps = 0;
	needsUpdate = true;
	last_sync.restart();

	has_breakpoints = (breakpoints.size() != 0);
	while (1) {
		vsync_happened = false;
		if (running) {
			vsync_happened = true;
			step();
			if (steps > 0) {
				if (--steps == 0) { running = false; }
			}
			uint16_t pc = (uint16_t)(PC - ram);
			//wprintw(msgWin,"BP %d PC %d\n",breakpoints.size(),pc);wrefresh(msgWin);
			if (has_breakpoints && breakpoints.find(pc) != breakpoints.end()) {
				wprintw(msgWin, "Breakpoint trace for %04x:\n", pc);
				for (int i = 10; i > 1; i--) {
					uint16_t laddr = his[(HISIZE + h_next - i) % HISIZE].h_adr;
					wprintw(msgWin, "  PC %04x\n", laddr);
				}
				wrefresh(msgWin);
				controlMode = true;
				running = false;
			}
			if (rt_ticks > CPUHZ / 100) {
				int64_t clock_usec = last_sync.elapsed_us();
				int64_t cpu_usec = rt_ticks * 1000000 / CPUHZ;
				if (clock_usec > cpu_usec + 10000) {
					std::this_thread::sleep_for(std::chrono::microseconds(cpu_usec - clock_usec));
					rt_ticks -= clock_usec * CPUHZ / 1000000;
					last_sync.restart();
				}
				else {
					// emu to slow?
				}
			}
		}
		else {
			std::this_thread::sleep_for(std::chrono::microseconds(5000));
			rt_ticks = 0;
		}
		int ch = ERR;
		if (!sigAlrm.test_and_set()) {
			if (needsUpdate) update();
			ch = getch();
			has_breakpoints = (breakpoints.size() != 0);
			if (ch == ERR) continue;
			if (ch == KEY_F(10)) { // Control Mode key
				controlMode = !controlMode;
				dispStatus();
				continue;
			}

			if (controlMode) {
				switch (ch) {
				case 'q':return;
				case ' ': running = !running; break;
				case 'n': 
					running = true; steps = 1; break;
				case 'm': snapMemory(); dispMemory(); break;
				case 'b':
				{
					char bpbuf[10];
					getString("Addr. of breakpoint: ", bpbuf, 4);
					werase(statusBar);
					dispStatus();
					uint16_t bp;
					if (hexParse(bpbuf, 4, bp)) {
						addBP(bp);
						dispBPs();
						mvwprintw(statusBar, 0, 0, "Breakpoint addded at %s\n", bpbuf);
					}
					else {
						mvwprintw(statusBar, 0, 0, "Bad breakpoint %s\n", bpbuf);
					}
					// set up breakpoints
				}
				break;
				case 'd':
				{
					char bpbuf[10];
					getString("Addr. of bp to remove: ", bpbuf, 4);
					werase(statusBar);
					dispStatus();
					uint16_t bp;
					if (hexParse(bpbuf, 4, bp)) {
						if (breakpoints.count(bp) == 0) {
							mvwprintw(statusBar, 0, 0, "No breakpoint %s\n", bpbuf);
						}
						else {
							clearBP(bp);
							dispBPs();
							mvwprintw(statusBar, 0, 0, "Breakpoint removed at %s\n", bpbuf);
						}
					}
					else {
						mvwprintw(statusBar, 0, 0, "Bad breakpoint %s\n", bpbuf);
					}
					// set up breakpoints
				}
				break;
				}
			}
			else {
				static int kstat = 0, ksum = 0;

				if (kstat || ch == KEY_F(11)) {
					if (ch == KEY_F(11)) {
						kstat = 1; ksum = 0;
					}
					else if (ch == '\n' || ch == '\r') {
						//wprintw(msgWin,"KC=%02x\n", ksum); wrefresh(msgWin);
						if (ksum & 0x80) {
							keypress(0x7d);	// Shift Key
							ksum &= 0x7f;
						}
						if (ksum)
							keypress(ksum);
						ksum = kstat = 0;
					}
					else if (ch >= '0' && ch <= '9') {
						ksum = ksum * 10 + ch - '0';
					}
					else
						kstat = 0;
				}
				else {
					uint8_t kc = code[ch];
					if (kc == 0 && ch >= 0 && ch < 32) {
						kc = code[ch + '`'];
						if (kc) {
							//wprintw(msgWin,"KC=7c (Control)\n");
							keypress(0x7c);	// Control Key
						}
					}
					if (kc & 0x80) {
						//wprintw(msgWin,"KC=7d (Shift)\n");
						keypress(0x7d);	// Shift Key
						kc &= 0x7f;
					}
					//wprintw(msgWin,"KC=%02x < %02x\n", kc, ch); wrefresh(msgWin);
					if (kc)
						keypress(kc);
				}
			}

		}
	}
}

void Vt100Sim::getString(const char* prompt, char* buf, uint8_t sz) {
  uint8_t l = strlen(prompt);
  mvwprintw(statusBar,0,0,prompt);
  echo();
  curs_set(1);
  wgetnstr(statusBar,buf,sz);
  noecho();
  curs_set(0);
  werase(statusBar);
  dispStatus();
}

void Vt100Sim::step()
{
  const uint32_t start = t_ticks;
  cpu_error = NONE;
  cpu_8080();
  if (int_int == 0) { int_data = 0xc7; }
  const uint16_t t = t_ticks - start;
  rt_ticks += t;
  if (uartclk.add_ticks(t)) {
    if (uart.clock()) {
      int_data |= 0xd7;
      int_int = 1;
      //wprintw(msgWin,"UART interrupt\n");wrefresh(msgWin);
    }
  }
  if (dc12 && lba4.add_ticks(t)) {
    if (kbd.clock(lba4.get_value())) {
      int_data |= 0xcf;
      int_int = 1;
      //wprintw(msgWin,"KBD interrupt\n");wrefresh(msgWin);
    }
  }
  if (dc12 && lba7.add_ticks(t)) {
    nvr.clock(lba7.get_value());
  }
  if (dc12 && vertical.add_ticks(t)) {
    if (vertical.get_value()) {
      int_data |= 0xe7;
      int_int = 1;
    }
  }
  needsUpdate = true;
}

void Vt100Sim::update() {
  needsUpdate = false;
  dispRegisters();
  dispMemory();
  dispVideo();
  dispStatus();
  dispBPs();
}

void Vt100Sim::keypress(uint8_t keycode)
{
    kbd.keypress(keycode);
}

void Vt100Sim::clearBP(uint16_t bp)
{
  breakpoints.erase(bp);
}

void Vt100Sim::addBP(uint16_t bp)
{
  breakpoints.insert(bp);
}

void Vt100Sim::clearAllBPs()
{
  breakpoints.clear();
}

void Vt100Sim::dispRegisters() {
  mvwprintw(regWin,1,1,"A %02x",A);
  mvwprintw(regWin,2,1,"B %02x C %02x",B,C);
  mvwprintw(regWin,3,1,"D %02x E %02x",D,E);
  mvwprintw(regWin,4,1,"H %02x L %02x",H,L);
  mvwprintw(regWin,5,1,"PC %04x",(PC-ram));
  mvwprintw(regWin,6,1,"SP %04x",(STACK-ram));
  wrefresh(regWin);
}

void Vt100Sim::dispVideo() {
  uint16_t start = 0x2000;
  int my,mx;
  int lattr = 3;
  int inscroll = 0;
  getmaxyx(vidWin,my,mx);
  werase(vidWin);
  wattron(vidWin,COLOR_PAIR(4));
  uint8_t y = -2;
  for (uint8_t i = 1; i < 27; i++) {
        char* p = (char*)ram + start;
        char* maxp = p + 133;
	//if (*p != 0x7f) y++;
	y++;
	wmove(vidWin,y,(mx>=134));
	if (scroll_latch) {
	    if (inscroll)
		wattron(vidWin,COLOR_PAIR(1));
	    else
		wattron(vidWin,COLOR_PAIR(4));
	}
        while (*p != 0x7f && p != maxp) {
            unsigned char c = *p;
	    int attrs = enable_avo?p[0x1000]:0xF;
	    p++;
	    if (y > 0) {
	      bool inverse = (c & 128);
	      bool blink = !(attrs & 0x1);
	      bool uline = !(attrs & 0x2);
	      bool bold = !(attrs & 0x4);
	      bool altchar = !(attrs & 0x8);
	      c &= 0x7F;

	      if (screen_rev) inverse = ~inverse;

	      if (inverse) wattron(vidWin,A_REVERSE);
	      if (uline) wattron(vidWin,A_UNDERLINE);
	      if (blink) wattron(vidWin,A_BLINK);
	      if (bold) wattron(vidWin,A_BOLD);

	      if (c == 0 || c == 127) {
		waddch(vidWin,' ');
	      } else  {
#ifdef _XOPEN_CURSES
extern int utf8_term;
static int xterm_chars[] = {
	0x2666, 0x2592, 0x2409, 0x240c, 0x240d, 0x240a, 0x00b0, 0x00b1,
	0x2424, 0x240b, 0x2518, 0x2510, 0x250c, 0x2514, 0x253c, 0x23ba,
	0x23bb, 0x2500, 0x23bc, 0x23bd, 0x251c, 0x2524, 0x2534, 0x252c,
	0x2502, 0x2264, 0x2265, 0x03c0, 0x2260, 0x00a3, 0x00b7, 0x0020
	};

		if ((c>=3 && c<=6) || (c<32 && utf8_term)) {
		    wchar_t ubuf[2] = { xterm_chars[c-1], '\0' };
		    waddwstr(vidWin,ubuf);
		} else if (c < 32) { waddch(vidWin,NCURSES_ACS(0x5F+c));
		} else if (altchar) {
		    wchar_t ubuf[2] = { c+128, '\0' };
		    waddwstr(vidWin,ubuf);
		}
#else
		if (c < 32) { waddch(vidWin,0x5F+c | A_ALTCHARSET); }
#endif
		else { waddch(vidWin,c); }
	      }

	      if (lattr!=3) waddch(vidWin,' ');
	      if (inverse) wattroff(vidWin,A_REVERSE);
	      if (uline) wattroff(vidWin,A_UNDERLINE);
	      if (bold) wattroff(vidWin,A_BOLD);
	      if (blink) wattroff(vidWin,A_BLINK);
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
        uint16_t next = (((a1&0x10)!=0)?0x2000:0x4000) | ((a1&0x0f)<<8) | a2;
	lattr = ((a1 >> 5) & 0x3);
	inscroll = ((a1 >> 7) & 0x1);
        if (start == next) break;
        start = next;
    }
  wattroff(vidWin,COLOR_PAIR(4));
  if (mx>=134) box(vidWin,0,0);
  mvwprintw(vidWin,0,1,"Video [bright %x]",bright);
  if (scroll_latch) wprintw(vidWin,"[Scroll %d]",scroll_latch);
  // if (blink_ff) wprintw(vidWin,"[BLINK]");
  wrefresh(vidWin);
}

void displayFlag(const char* text, bool on) {
  if (on) {
    wattron(statusBar,COLOR_PAIR(3));
    wattron(statusBar,A_BOLD);
  } else {
    wattron(statusBar,COLOR_PAIR(2));
    wattroff(statusBar,A_BOLD);
  }
  wprintw(statusBar,text);
  waddch(statusBar,' ');
  waddch(statusBar,' ');
}

void Vt100Sim::dispStatus() {
  // ONLINE LOCAL KBD LOCK L1 L2 L3 L4
  const static char* ledNames[] = { 
    "ONLINE", "LOCAL", "KBD LOCK", "L1", "L2", "L3", "L4" };
  const int lwidth = 8 + 7 + 10 + 4 + 4 + 4 + 4;
  int mx, my;
  getmaxyx(statusBar,my,mx);
  wmove(statusBar,0,mx-lwidth);
  uint8_t flags = kbd.get_status();
  displayFlag(ledNames[0], (flags & (1<<5)) == 0 );
  displayFlag(ledNames[1], (flags & (1<<5)) != 0 );
  displayFlag(ledNames[2], (flags & (1<<4)) != 0 );
  displayFlag(ledNames[3], (flags & (1<<3)) != 0 );
  displayFlag(ledNames[4], (flags & (1<<2)) != 0 );
  displayFlag(ledNames[5], (flags & (1<<1)) != 0 );
  displayFlag(ledNames[6], (flags & (1<<0)) != 0 );

  // Mode information
  wmove(statusBar,0,mx/3);
  wattrset(statusBar,A_BOLD);
  wprintw(statusBar,"| ");
  if (controlMode) {
    wprintw(statusBar,"CONTROL");
  } else {
    wattron(statusBar,A_REVERSE);
    wprintw(statusBar,"TYPING");
    wattroff(statusBar,A_REVERSE);
    wprintw(statusBar," ");
  }
  wprintw(statusBar," | ");
  if (running) {
    wprintw(statusBar,"RUNNING");
  } else {
    wattron(statusBar,A_REVERSE);
    wprintw(statusBar,"STOPPED");
    wattroff(statusBar,A_REVERSE);
  }
  wprintw(statusBar," | %s |",uart.pty_name());
  wrefresh(statusBar);
}

void Vt100Sim::dispBPs() {
  int y = 1;
  werase(bpWin);
  box(bpWin,0,0);
  mvwprintw(bpWin,0,1,"Brkpts");
  for (std::set<uint16_t>::iterator i = breakpoints.begin();
       i != breakpoints.end();
       i++) {
    mvwprintw(bpWin,y++,2,"%04x",*i);
  }
  wrefresh(bpWin);
}

void Vt100Sim::snapMemory() {
  memset(touched+0x2000,0,0x2000);
}

void Vt100Sim::dispMemory() {
  int my,mx;
  getmaxyx(memWin,my,mx);
  int bavail = (mx - 7)/3;
  int bdisp = 8;
  while (bdisp*2 <= bavail) bdisp*=2;
  uint16_t start = 0x2000;
  
  wattrset(memWin,A_NORMAL);
  for (int b = 0; b<bdisp;b++) {
    mvwprintw(memWin,0,7+3*b,"%02x",b);
  }
  wattrset(memWin,COLOR_PAIR(1));

  for (int y = 1; y < my - 1; y++) {
    wattrset(memWin,COLOR_PAIR(1));
    mvwprintw(memWin,y,1,"%04x:",start);
    for (int b = 0; b<bdisp;b++) {
      if (!touched[start]) 
	wattron(memWin,A_STANDOUT);
      if (ram[start] != 00) {
	wattron(memWin,COLOR_PAIR(2));
	wprintw(memWin," %02x",ram[start++]);
	wattron(memWin,COLOR_PAIR(1));
      } else {
	wprintw(memWin," %02x",ram[start++]);
      }
      wattroff(memWin,A_STANDOUT);
    }
  }
  wrefresh(memWin);
}


BYTE io_in(BYTE addr);
BYTE io_out(BYTE addr, BYTE data);
void exit_io();


void exit_io() {}

BYTE io_in(BYTE addr)
{
    return sim->ioIn(addr);
}

BYTE io_out(BYTE addr, BYTE data)
{
    sim->ioOut(addr,data);
	return data;
}
