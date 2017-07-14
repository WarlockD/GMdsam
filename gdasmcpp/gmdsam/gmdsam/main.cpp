#include "gm_lib.h"
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

int main(int argc, const char* argv[]) {
	gm::DataWinFile file;
	if (argc == 2) {
		std::string filename = argv[1];
		try {
			file.load(filename);
		}
		catch (gm::FileHelperException e) {
			std::cerr << e.what() << std::endl;
			return -1;
		}
	}
	if (!file.has_data()) 
		return -1;

	auto object = file.resource_at<gm::Object>(217); // jerry
	std::cout << "Object: " << object.name() << std::endl;
	auto events = object.events();

	for (auto test : events) {
		std::cout << test.first << ", " << test.second << std::endl;
	}

	auto sprite = file.resource_at<gm::Sprite>(217); // random sprite
	sprite.xml_export(std::cout);

	while (true) {}

	return 0;
}