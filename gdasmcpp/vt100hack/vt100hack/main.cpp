#include <stdlib.h>
#include <iostream>
#include "vt100sim.h"
#include "optionparser.h"
#include <curses.h>
#ifdef _XOPEN_CURSES
#include <string.h>
#include <langinfo.h>
#endif

int utf8_term = 0;

option::ArgStatus checkBP(const option::Option& opt, bool msg) {
  char* tail;
  unsigned int bp = strtoul(opt.arg,&tail,16);
  if (*tail != '\0' || bp > 0xffff) return option::ARG_ILLEGAL;
  else return option::ARG_OK;
}

enum OptionIndex { UNKNOWN, HELP, RUN, BREAKPOINT, NOAVO };
const option::Descriptor usage[] = {
  { UNKNOWN, 0, "", "", option::Arg::None, "Usage: vt100sim [options] ROM.bin" },
  { HELP, 0, "h", "help", option::Arg::None, "--help, -h\tPrint usage and exit" },
  { RUN, 0, "r", "run", option::Arg::None, "--run, -r\tImmediately run at startup"},
  { BREAKPOINT, 0, "b", "break", checkBP, "--break, -b\tInsert breakpoint"},
  { NOAVO, 0, "N", "noavo", option::Arg::None, "--noavo, -b\tDisable AVO flag."},
  {0,0,0,0,0,0}
};
void start_windows_system();

int main(int argc, char *argv[])
{
#ifdef _XOPEN_CURSES
  setlocale(LC_ALL, "");
  utf8_term = (!strcmp("UTF-8", nl_langinfo(CODESET)));
#endif

  argc-=(argc>0); argv+=(argc>0); // skip program name argv[0] if present
  option::Stats  stats(usage, argc, argv);
  std::vector<option::Option> options(stats.options_max);
  std::vector<option::Option> buffer(stats.buffer_max);
  option::Parser parse(usage, argc, argv, options.data(), buffer.data());
  if (parse.error()) return 1;
  if (options[HELP] || argc == 0) {
    option::printUsage(std::cout, usage);
    return 0;
  }
  bool running = options[RUN];  
  if (parse.nonOptionsCount() < 1) {
    std::cout << "No ROM specified"; return 1;
  }
  start_windows_system();
  sim = new Vt100Sim(parse.nonOptions()[0],running,!options[NOAVO]);
  sim->init();
  for (option::Option* bpo = options[BREAKPOINT]; bpo != NULL; bpo = bpo->next()) {
    unsigned int bp = strtoul(bpo->arg,NULL,16);
    sim->addBP(bp);
  }

  sim->run();
  delete sim;
}
