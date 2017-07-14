#include "global.h"
#include <chrono>
#include <array>
#include <mutex>
#include <windows.h>
#include <cassert>

namespace std {
	// https://kjellkod.wordpress.com/2013/01/22/exploring-c11-part-2-localtime-and-time-again/
	// thread safe
	typedef std::chrono::time_point<std::chrono::system_clock>  system_time_point;
	std::tm localtime(const std::time_t& time)
	{
		std::tm tm_snapshot;
#if (defined(WIN32) || defined(_WIN32) || defined(__WIN32__))
		localtime_s(&tm_snapshot, &time);
#else
		localtime_r(&time, &tm_snapshot); // POSIX  
#endif
		return tm_snapshot;
	}
};
namespace debug {
	void enable_windows10_vt100_support() {
		DWORD dwMode = 0;
		GetConsoleMode(GetStdHandle(STD_OUTPUT_HANDLE), &dwMode);
		dwMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | ENABLE_PROCESSED_OUTPUT;
		//	if (_scroll) dwMode |= ENABLE_WRAP_AT_EOL_OUTPUT;
		SetConsoleMode(GetStdHandle(STD_OUTPUT_HANDLE), dwMode);
	}
	// buffer gets lines and outputs them
	class line_stream_buffer : public std::basic_streambuf<char, std::char_traits<char> > {
	public:
		line_stream_buffer() {
			char *base = _buffer.data();
			setp(base, base + _buffer.size() - 1); // -1 to make overflow() easier
		}
		virtual ~line_stream_buffer() {}
	protected:
		void push(int_type ch) {
			if (ch == '\r' || ch == '\n') {
				if (_lastc != ch && (_lastc == '\r' || _lastc == '\n')) ch = 0;
				else {
					if (!_linebuffer.empty()) {
						std::lock_guard<std::mutex> lock(s_mutex);
						output_line(_linebuffer);
						_linebuffer.clear();
					}
				}
			}
			else 
				_linebuffer.push_back(ch);
			_lastc = ch;
		}
		virtual int_type overflow(int_type ch) override
		{	
			push(ch);
			return ch;
		}
		int sync() override { 
			std::ptrdiff_t n = pptr() - pbase();
			for (auto ptr = pbase(); ptr != pptr(); ptr++) {
				push(*ptr);
			}
			pbump(-n);
			return 0; 
		}
	protected:
		virtual void output_line(const std::string& line) = 0;
	private:
		static std::mutex s_mutex;
		std::array<char, 512> _buffer;
		std::string _linebuffer;
		int _lastc = 0;
	};
	std::mutex line_stream_buffer::s_mutex;

	class visual_studio_debugbuf : public line_stream_buffer {
		std::string _name;
		// https://kjellkod.wordpress.com/2013/01/22/exploring-c11-part-2-localtime-and-time-again/
		// thread safe
		std::tm get_now() {
			auto now = std::chrono::system_clock::now();
			auto in_time_t = std::chrono::system_clock::to_time_t(now);
			return std::localtime(in_time_t);
		}
		void output_line(const std::string& line) {
			auto in_time_t = get_now();
			std::stringstream ss;
			ss << '[' << std::put_time(&in_time_t, "%Y-%m-%d %X");
			if (!_name.empty()) ss << " : " << _name;
			ss << "]:" << line << std::endl;
			::OutputDebugStringA(ss.str().c_str());
		}
	public:
		visual_studio_debugbuf(const std::string& name) :_name(name) {}
		virtual ~visual_studio_debugbuf() {}
	};
	ostream::ostream(const std::string& name, bool debug) : _stream(new visual_studio_debugbuf(name)), _debug(debug){ }
	ostream::~ostream() { delete _stream.rdbuf(); }
	debug::ostream cerr(true);
	
	//debug::ostream& cerr = std::cerr;

};