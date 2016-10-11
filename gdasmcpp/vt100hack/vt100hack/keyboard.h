#ifndef KEYBOARD_H
#define KEYBOARD_H

#include <stdint.h>
#include <set>
#include <array>
#include <mutex>
#include <atomic>
#include <type_traits>


inline constexpr uint8_t VT100_MASK(uint8_t row, uint8_t col) { return ((col&7) << 4) |  row & 0xF ; }
// I hate typing in all these constants but its the best way to do it
enum class VT_KEY : uint8_t {
	VT_NO_KEY = VT100_MASK(0, 0),
	// row 0
	VT_PAD_9 = VT100_MASK(0, 7),
	VT_PAD_PERIOD = VT100_MASK(0, 6),
	VT_PAD_8 = VT100_MASK(0, 5),
	VT_PAD_7 = VT100_MASK(0, 4),
	VT_UP = VT100_MASK(0, 3),
	VT_LEFT = VT100_MASK(0, 2),
	VT_RIGHT = VT100_MASK(0, 1),
	
	// row 1
	VT_PAD_3 = VT100_MASK(1, 7),
	VT_PAD_COMMA = VT100_MASK(1, 6),
	VT_PAD_ENTER = VT100_MASK(1, 5),
	VT_PAD_PF4 = VT100_MASK(1, 4),
	VT_PAD_PF3 = VT100_MASK(1, 3),
	// row 2
	VT_PAD_6 = VT100_MASK(2, 7),
	VT_PAD_5 = VT100_MASK(2, 6),
	VT_PAD_2 = VT100_MASK(2, 5),
	VT_PAD_PF2 = VT100_MASK(2, 4),
	VT_PAD_PF1 = VT100_MASK(2, 3),
	VT_DOWN = VT100_MASK(2, 2),
	// row 3
	VT_PAD_MINUS = VT100_MASK(3, 7),
	VT_PAD_4 = VT100_MASK(3, 6),
	VT_PAD_1 = VT100_MASK(3, 5),
	VT_PAD_0 = VT100_MASK(3, 4),
	VT_BACKSPACE = VT100_MASK(3, 3),
	VT_BREAK = VT100_MASK(3, 2),
	VT_DELETE = VT100_MASK(3, 0),
	// row 4
	VT_RETURN = VT100_MASK(4, 6) | VT100_MASK(4, 0),
	VT_LINEFEED = VT100_MASK(4, 4),
	VT_PLUSEQUAL = VT100_MASK(4, 3),
	VT_TILDE = VT100_MASK(4, 2),
	VT_RBRACK = VT100_MASK(4, 1),
	// row 5, the meat of it
	VT_QUESTION_FSLASH = VT100_MASK(5, 7),
	VT_LARROW_PERIOD = VT100_MASK(5, 6),
	VT_DQUOTE_APOST = VT100_MASK(5, 5),
	VT_BSLASH = VT100_MASK(5, 4),
	VT_0 = VT100_MASK(5, 3),
	VT_MINUS = VT100_MASK(5, 2),
	VT_LBRACK = VT100_MASK(5, 1),
	VT_P = VT100_MASK(5, 0),
	// row 6
};

class Keyboard {
	enum class KbdState { Idle, Sending, Responding };
	std::recursive_mutex _mutex;
	std::set<VT_KEY> keys;
	std::set<VT_KEY> keys_down;
	
	std::set<VT_KEY> scan;
	std::set<VT_KEY>::iterator scan_iter;
	std::set<VT_KEY> last_sent;
	std::atomic<uint8_t> _keys_down;
	KbdState state;
	uint8_t latch;
	uint8_t last_status;
	uint32_t clocks_until_next;
	bool tx_buf_empty;
public:
	Keyboard() : state(KbdState::Idle), latch(0), tx_buf_empty(true), scan_iter(scan.end()) {}
	uint8_t get_latch() const { return latch; }
	uint8_t get_status() const { return last_status;  }
	bool get_tx_buf_empty() const { return tx_buf_empty; }
	template<class C>
	void keys_press(const C& c) {
		std::lock_guard<std::recursive_mutex> _lock(_mutex);
		keys.insert(c.begin(), c.end());
	}
	void key_press(VT_KEY key) {
		std::lock_guard<std::recursive_mutex> _lock(_mutex);
		keys.emplace(key);
	}
	void key_down(VT_KEY key) {
		std::lock_guard<std::recursive_mutex> _lock(_mutex);
		keys_down.emplace(key);
	}
	void key_up(VT_KEY key) {
		std::lock_guard<std::recursive_mutex> _lock(_mutex);
		keys_down.erase(key);
	}
	void set_status(uint8_t status)
	{
		last_status = status; 
		if ((status & (1 << 6)) && state == KbdState::Idle) {
			state = KbdState::Sending;
			clocks_until_next = 160;
		}

		if (status & 0x80) {
			// TODO: FIX too main beeps.
			//beep();
		}
	}

	bool Keyboard::clock(bool rising)
	{
		if (!rising) { return false; }
		switch (state) {
		case KbdState::Idle: break;
		case KbdState::Sending:
			if (clocks_until_next == 0) {
				scan = last_sent;
				// hack around debounce problem
				_mutex.lock();
				last_sent = keys;
				scan.insert(keys.begin(), keys.end());
				scan.insert(keys_down.begin(), keys_down.end());
				keys.clear();
				_mutex.unlock();
				scan_iter = scan.begin();
				state = KbdState::Responding;
				clocks_until_next = 160;
				if (scan_iter == scan.end()) { clocks_until_next += 127; }
				else { clocks_until_next += static_cast<uint8_t>(*scan_iter); }
			}
			else {
				clocks_until_next--;
			}
			break;
		case KbdState::Responding:
			if (clocks_until_next == 0) {
				if (scan_iter != scan.end()) {
					clocks_until_next = 160;
					latch = static_cast<uint8_t>(*scan_iter);
					scan_iter++;
				}
				else {
					latch = 0x7f;
					state = KbdState::Idle;
				}
				return true;
			}
			clocks_until_next--;
			break;
		}
		return false;
	}
	
};

#endif // KEYBOARD_H
