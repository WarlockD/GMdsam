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
		std::ifstream ufile;
		ufile.open(argv[1], std::ios::binary | std::ios::in);

		if (ufile.bad()) return -1;
		file.load(ufile);
		ufile.close();
	}
	if (!file.has_data()) return -1;

	auto object = file.resource_at<gm::Object>(217); // jerry
	std::cout << "Object: " << object.name() << std::endl;
	auto events = object.events();

	for (auto test : events) {
		for (auto al : test.second) {
			for (auto a : al.actions()) {
				//std::cout << "Name: " << a->name() << std::endl;
			}
		}
	}
	return 0;
}