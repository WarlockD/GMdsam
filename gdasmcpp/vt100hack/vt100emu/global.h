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


#define IDD_ABOUTBOX                    100
#define IDR_MAINFRAME                   128
#define IDS_EDITSTRING                  57666
#define IDS_TITLESTRING                 57667

//typedef CWinTraits<WS_OVERLAPPEDWINDOW> CDxAppWinTraits;

#include <type_traits>
#include <cstdint>
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
