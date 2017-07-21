#include "gm_lib.h"
#include "dsam.h"
#include "new_code.h"

#include <fstream>

template<typename T>
class stream_array {
	std::vector<T> _array;
};

/*

template<typename T, typename = std::enable_if<std::is_arithmetic<T>::value>::type>
friend obstream operator<<(obstream stream, T const&)
{
	return oto_binary_wraper(stream);
}
template<typename T, typename = std::enable_if<std::is_arithmetic<T>::value>::type>
friend ito_binary_wraper operator >> (std::istream& stream, T &)
{
	return ito_binary_wraper(stream);
}
*/
void exit_program(int value) {
	assert(value == 0);
	exit(value);
}

template<typename C>
int search(const char* name) {
	auto code_container = file.resource_container<C>();
	for (auto& e : code_container) {
		if (e.name() == name) {
			debug::cerr << "foundit! " << e.index() << std::endl;
			return e.index();
		}

	}
	debug::cerr << "not found '" << name "'" << std::endl;
	return -1;
}
int main(int argc, const char* argv[]) {
	gm::DataWinFile file;
	if (argc == 2) {
		std::string filename = argv[1];
		try {
			file.load(filename);
		}
		catch (gm::FileHelperException e) {
			std::cerr << e.what() << std::endl;
			exit_program (-1);
		}
	}
	if (!file.has_data()) 
		exit_program (-1);

	auto object = file.resource_at<gm::Object>(217); // jerry
	std::cout << "Object: " << object.name() << std::endl;
	auto events = object.events();

	for (auto test : events) {
		std::cout << test.first << ", " << test.second << std::endl;
	}

	auto sprite = file.resource_at<gm::Sprite>(217); // random sprite
	sprite.xml_export(std::cout);
	//auto code = file.resource_at<gm::Undertale_1_01_Code>(1622);
	auto code = file.resource_at<gm::Undertale_1_01_Code>(0);

	debug::cerr << code << std::endl;
	gm::dsam::undertale_1_01::dsam dd(file, code);

	dd.Dissasemble();

	while (true) {}

	exit_program(0);
}