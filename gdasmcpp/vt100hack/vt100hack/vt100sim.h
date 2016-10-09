#ifndef VT100SIM_H
#define VT100SIM_H

#include "nvr.h"
#include "keyboard.h"
#include "pusart.h"
#include <stdint.h>
#include <set>
#include <chrono>
#include <thread>
#include <atomic>
#include <functional>
#include <vector>
#ifdef max
#undef max
#undef min
#endif

namespace std {
	template<class T> const T& min(const T& a, const T& b) { return (b < a) ? b : a; }
	template<class T> const T& max(const T& a, const T& b) { return (a < b) ? b : a; }
};
class Timer {
public:
	Timer() : _start(std::chrono::system_clock::now()) {}
	virtual ~Timer() {}
	void restart() { _start = std::chrono::system_clock::now(); }
	int64_t elapsed_ns() const { return std::chrono::nanoseconds(std::chrono::system_clock::now() - _start).count();}
//	int64_t elapsed_us() const { return std::chrono::microseconds(std::chrono::system_clock::now() - _start).count();}
//	int64_t elapsed_ms() const { return std::chrono::milliseconds(std::chrono::system_clock::now() - _start).count();}
	int64_t elapsed_us() const { return elapsed_ns() / 1000; }
	int64_t elapsed_ms() const { return elapsed_us() / 1000; }
	double elapsed() const { return std::chrono::duration<double>(std::chrono::system_clock::now() - _start).count(); }
	bool expired_ns(int64_t time) const { return elapsed_ns() > time; }
	bool expired_us(int64_t time) const { return elapsed_us() > time; }
	bool expired_ms(int64_t time) const { return elapsed_ms() > time; }
	bool expired(double time) const { return elapsed() > time; }
private:
	std::chrono::time_point<std::chrono::system_clock> _start;
};
class StopWatch : public Timer {
	std::atomic_flag _running;
	std::atomic_flag _stopping;
	std::function<void(double)> _func;
	
	static void thread_function(StopWatch* timer,double interval) {
		timer->_stopping.test_and_set();
		timer->_running.test_and_set();
		timer->restart();
		while (timer->_stopping.test_and_set()) {
			timer->test_event(interval);
		}
		timer->_running.clear();
	}
public:
	bool test_event(double interval) {
		double elapsed = Timer::elapsed();
		if (elapsed > interval) {
			_func(elapsed);
			restart();
			return true;
		}
		return false;
	}

	StopWatch() : _running{ 0 } , _stopping{ 0 } { _running.clear(); }
	void setFunction(std::function<void(double)> func) { _func = func; }
	void start(double interval) {
		if (!_running.test_and_set()) {
			std::thread(StopWatch::thread_function, this, interval).detach();
		}
	}
	void stop() {
		if (_running.test_and_set()) {
			_stopping.clear();
			while (_running.test_and_set());
		}
	}

};

#include "8080/sim.h"


class Vt100Sim
{
public:
  Vt100Sim(const char* romPath = 0,bool running=false, bool avo_on=true);
  ~Vt100Sim();
  void init();
  BYTE ioIn(BYTE addr);
  void ioOut(BYTE addr, BYTE data);
  NVR nvr;
  Keyboard kbd;
  PUSART uart;
  uint8_t bright;
private:
  const char* romPath;
  bool running;
  bool inputMode;
  bool needsUpdate;
  std::set<uint16_t> breakpoints;
  bool has_breakpoints;
  bool dc12;
  bool controlMode;
  bool enable_avo;
  long long rt_ticks;
  Timer last_sync;
  int scroll_latch;
  int screen_rev;
  int base_attr;
  int blink_ff;
public:
  void getString(const char* prompt, char* buffer, uint8_t sz);
  void step();
  void run();
  void keypress(uint8_t keycode);
  void clearBP(uint16_t bp);
  void addBP(uint16_t bp);
  void clearAllBPs();
public:
    void dispRegisters();
    void dispVideo();
    void dispLEDs();
    void dispStatus();
    void dispBPs();
  void dispMemory();
    void snapMemory();
  void update();
private:
  
};

extern Vt100Sim* sim;

#endif // VT100SIM_H
