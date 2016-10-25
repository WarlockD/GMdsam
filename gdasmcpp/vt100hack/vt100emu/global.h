#pragma once

#include <Windows.h>
// order is important, atl
#include <atltypes.h>
#include <atldef.h>
#include <atlbase.h>

#include <atlstr.h>
// wtl
#include <atlapp.h>
#include <atlimage.h>
#include <cassert>

#include <atlframe.h>
#include <atlctrls.h>
#include <atldlgs.h>
//#include <atlmisc.h>
#include <atlsplit.h>
#include <atlcrack.h>      // WTL enhanced msg map macros</span>
#include <atlwinx.h>

#include <type_traits>
#include <queue>
#define IDD_ABOUTBOX                    100
#define IDR_MAINFRAME                   128
#define IDS_EDITSTRING                  57666
#define IDS_TITLESTRING                 57667

//typedef CWinTraits<WS_OVERLAPPEDWINDOW> CDxAppWinTraits;

#include <type_traits>
#include <cstdint>
#include <mutex>
#include <condition_variable>
#include <vector>
#include <fstream>
#include <iostream>

// bit operations
// useful functions to deal with bit shuffling encryptions
template <typename T, typename U> constexpr T BIT(T x, U n) { return (x >> n) & T(1); }

template<typename T, size_t BIT, typename = std::enable_if<std::is_integral<T>::value>>
constexpr inline void set_bit(T& value) { value |= (1 << BIT); }
template<typename T, size_t BIT, typename = std::enable_if<std::is_integral<T>::value>>
constexpr inline void clear_bit(T& value) { value |= ~(1 << BIT); }
template<typename T, size_t BIT, typename = std::enable_if<std::is_integral<T>::value>>
constexpr inline bool test_bit(T& value) { return (value &(1 << BIT)) != 0 ? true : false; }

#define MAKE_CLASSENUM_OPERATIONS(ET) \
constexpr inline ET operator | (ET l, ET r) { return static_cast<ET>((static_cast<std::underlying_type<ET>::type>(l) | static_cast<std::underlying_type<ET>::type>(r))); }\
constexpr inline ET operator & (ET l, ET r) { return static_cast<ET>((static_cast<std::underlying_type<ET>::type>(l) & static_cast<std::underlying_type<ET>::type>(r))); }\
constexpr inline ET operator ~(ET l) { return static_cast<ET>(~static_cast<std::underlying_type<ET>::type>(l)); }\
constexpr inline bool operator %(ET l, ET r) { return (static_cast<std::underlying_type<ET>::type>(l) & static_cast<std::underlying_type<ET>::type>(r)) !=0; }\
constexpr inline ET operator - (ET l, ET r) { return (l & ~r); }\
constexpr inline ET operator + (ET l, ET r) { return (l | r);  }\
inline ET& operator |= (ET& l, ET r) { l = l | r; return l; }\
 inline ET& operator &= (ET& l, ET r) {  l = l & r; return l; }\
inline ET& operator -= (ET& l, ET r) {  l = l - r; return l; }\
inline ET& operator += (ET& l, ET r) {  l = l + r; return l; }

// Bitmasks
// http://stackoverflow.com/questions/11778763/recursive-template-for-compile-time-bit-mask


template<typename T, typename = std::enable_if<std::is_trivially_copyable<T>::value>::type>
class concurrent_queue {
	std::queue<T> _data;
	std::mutex _mutex;
	std::condition_variable _cv;
public:
	// not sure if we need it copyable or movable but just in case
	concurrent_queue(concurrent_queue&& r) {
		std::lock_guard guard(r._mutex);
		_data = std::move(r._data);
	}
	concurrent_queue& operator= (concurrent_queue&& r)
	{
		if (&l == this) return *this;
		std::unique_lock lk1(_mutex, std::defer_lock);
		std::unique_lock lk2(r._mutex, std::defer_lock);
		std::lock(lk1, lk2);
		_queue = std::move(r._queue);
		return *this;
	}
	concurrent_queue(const concurrent_queue& r)
	{
		std::lock_guard guard(r._mutex);
		_data = r._data;
	}
	concurrent_queue& operator= (const concurrent_queue& r)
	{
		if (&l == this) return *this;
		std::unique_lock lk1(_mutex, std::defer_lock);
		std::unique_lock lk2(r._mutex, std::defer_lock);
		std::lock(lk1, lk2);
		_queue = r._queue;
		return *this;
	}
	bool pop_try(T& data) {
		if (!_mutex.try_lock()) return false;
		_cv.tr
			bool ret = _data.empty();
		if (!ret) {
			data = _data.front();
			_data.pop();
		}
		_mutex.unlock();
		return ret;
	}
	uint8_t pop_wait() {
		std::unique_lock<std::mutex> lk(_mutex); // mutex scope lock
		_cv.wait(lk, [this]() { return !_data.empty(); });
		uint8_t ret = _data.front();
		_data.pop();
		return ret;
	}
	template <class ...Args>
	void emplace(Args&& ...args) {
		std::unique_lock<std::mutex> lk(_mutex);
		_data.emplace(std::forward<Args>(args...));
		lk.unlock();
		_cv.notify_one(); // notify one waiting thread
	}
	void push(const T& data) {
		std::unique_lock<std::mutex> lk(_mutex);
		_data.push(data);
		lk.unlock();
		_cv.notify_one(); // notify one waiting thread
	}
	template<typename U>
	void push(U&& data) {
		std::unique_lock<std::mutex> lk(_mutex);
		_data.push(std::forward<T>(data));
		lk.unlock();
		_cv.notify_one(); // notify one waiting thread
	}
};
struct CharAttributes {
	enum  Attribute {
		NORMAL = 0,
		BOLD = 1,
		UNDER = 2,
		BLINK = 4,
		REVERSE = 8,
		SINGLE_W_H = 16,
		DOUBLE_W = 32,
		DOUBLE_T = 64,
		DOUBLE_B = 128,
		BLANK = 256
	};
	Attribute attrib;
	uint8_t fg;
	uint8_t bg;
	CharAttributes(Attribute attrib) : attrib(attrib),fg(1),bg(0) {}
	CharAttributes() :attrib(NORMAL), fg(1), bg(0) {}
	bool blank() const { return (attrib & BLANK) != 0; }
	bool underline() const { return (attrib & UNDER)!=0; }
	bool bold() const { return (attrib & BOLD) != 0; }
	bool blink() const { return (attrib & BLINK) != 0; }
	bool reverse() const { return (attrib & REVERSE) != 0; }
	bool double_width() const { return (attrib & DOUBLE_W) != 0; }
	bool double_height() const { return (attrib & (DOUBLE_T| DOUBLE_B)) != 0; }
};
