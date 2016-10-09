#include "keyboard.h"
#include <stdio.h>
#include "8080/sim.h"
#include "8080/simglb.h"
#include <curses.h>

Keyboard::Keyboard() : state(KBD_IDLE), latch(0), tx_buf_empty(true)
{
    scan_iter = scan.end();
}

uint8_t Keyboard::get_latch()
{
    return latch;
}

bool Keyboard::get_tx_buf_empty() { return tx_buf_empty; }

extern WINDOW* msgWin;

/*
 * The VT100 triggers keyboard scans by setting bit (1<<6) in the status.
 * It then expects to receive one or more scan codes followed by an 0x7F
 * byte to indicate the end of the scan. Each byte takes 160 clocks to send.
 *
 * Setup and NoScroll are triggered as soon as they are received.
 *
 * The Shift and Control keys are state driven and on independent rows,
 * they do not count toward the three key maximum.
 *
 * Other keys must be NOT asserted for one scan then asserted for TWO scans
 * before they will be triggered. Upto three keys may be pressed at the same
 * time.
 *
 * The double scan and thee key maximum requirements prevent 'ghost'
 * keypresses trigged by the maxtrix being accepted.
 *
 * There is a 9 character buffer for transmitting data to the host; this must
 * have at least 3 bytes free. If it doesn't the keyboard lock is asserted.
 */

void Keyboard::set_status(uint8_t status)
{
  last_status = status;
    //printf("Got kbd status %02x at %04x\n",status,PC-ram); fflush(stdout);
    if ((status & (1<<6)) &&  state == KBD_IDLE) {
        //printf("SCAN START\n");fflush(stdout);
      //wprintw(msgWin,"Scan start\n");wrefresh(msgWin);
        state = KBD_SENDING;
        clocks_until_next = 160;

    }

    if (status & 0x80) {
	// TODO: FIX too main beeps.
	beep();
    }
}

void Keyboard::keypress(uint8_t keycode)
{
  //printf("PRESS %02x\n",keycode);fflush(stdout);
    keys.insert(keycode);
}

bool Keyboard::clock(bool rising)
{
    if (!rising) { return false; }
    switch (state) {
    case KBD_IDLE:
        break;
    case KBD_SENDING:
        if (clocks_until_next == 0) {
	  scan = last_sent;
		// hack around debounce problem
	  last_sent = keys;
	  scan.insert(keys.begin(),keys.end());
            keys.clear();
            scan_iter = scan.begin();
            state = KBD_RESPONDING;
            clocks_until_next = 160;
            if (scan_iter == scan.end()) { clocks_until_next += 127; } else { clocks_until_next += *scan_iter; }
            //printf("RSP MODE\n");fflush(stdout);
        } else {
            clocks_until_next--;
        }
        break;
    case KBD_RESPONDING:
        if (clocks_until_next == 0) {
            if (scan_iter != scan.end()) {
	      //wprintw(msgWin,"Sending %02x\n",*scan_iter);wrefresh(msgWin);
	      //printf("SENDING KEY %02x\n",*scan_iter);fflush(stdout);
                clocks_until_next = 160;
                latch = *scan_iter;
                scan_iter++;
            } else {
                latch = 0x7f;
                state = KBD_IDLE;
		//wprintw(msgWin,"End scan\n");wrefresh(msgWin);
            }
            return true;
        }
        clocks_until_next--;
        return false;
        break;
    default:
      wprintw(msgWin,"Bad state\n");wrefresh(msgWin);
      
    }
    return false;
}


