#ifndef PUSART_H
#define PUSART_H

#include <stdint.h>
#include <stdbool.h>

class PUSART {
private:
  bool mode_select_mode;
  bool has_xmit_ready;
  uint8_t mode;
  uint8_t command;
  int pty_fd;
  uint8_t data;
  bool has_rx_rdy;
  bool xoff;
public:
  PUSART();
  // High if ready to transmit a byte
  bool xmit_ready();
  void write_command(uint8_t b);
  void write_data(uint8_t b);
  bool clock();
  bool rx_ready();
  uint8_t read_data();
  uint8_t read_command();
  char* pty_name();
  void start_shell();
};

#endif // PUSART_H
