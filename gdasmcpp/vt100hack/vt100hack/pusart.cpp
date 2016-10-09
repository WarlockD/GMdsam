#include "pusart.h"
#include <stdlib.h>
#include <fcntl.h>
#include <iostream>


PUSART::PUSART() : 
  mode_select_mode(true),
  has_xmit_ready(true),
  mode(0),
  command(0),
  pty_fd(-1),
  has_rx_rdy(false)
{
}

void PUSART::start_shell() {
	/*
	
  if (pty_fd != -1) close(pty_fd);

  pty_fd = posix_openpt( O_RDWR | O_NOCTTY );
  grantpt(pty_fd);
  unlockpt(pty_fd);
  int flags = fcntl(pty_fd, F_GETFL, 0);
  fcntl(pty_fd, F_SETFL, flags | O_NONBLOCK);

  struct termios config;
  struct termios orig_settings;

  if(!isatty(pty_fd)) {}

  int fds = open(ptsname(pty_fd), O_RDWR);
  if(tcgetattr(fds, &orig_settings) < 0) {}

  if(tcgetattr(pty_fd, &config) < 0) {}
  config.c_iflag &= ~(IGNBRK | BRKINT | ICRNL |
		      INLCR | PARMRK | INPCK | ISTRIP | IXON);
  config.c_oflag = 0;
  config.c_lflag &= ~(ECHO | ECHONL | ICANON | IEXTEN | ISIG);
  config.c_cflag &= ~(CSIZE | PARENB);
  config.c_cflag |= CS8;
  config.c_cc[VMIN]  = 1;
  config.c_cc[VTIME] = 0;
  if(tcsetattr(pty_fd, TCSAFLUSH, &config) < 0) {}

  int pid = fork();

  if (pid == 0) {
    // Child process.
    close(pty_fd);  // Close master

    config = orig_settings;
    config.c_iflag &= ~(IGNBRK | BRKINT | ICRNL |
			INLCR | PARMRK | INPCK | ISTRIP | IXON);
    config.c_oflag = 0;
    config.c_lflag &= ~(ECHO | ECHONL | ICANON | IEXTEN | ISIG);
    config.c_cflag &= ~(CSIZE | PARENB);
    config.c_cflag |= CS8;
    config.c_cc[VMIN]  = 1;
    config.c_cc[VTIME] = 0;
    if(tcsetattr(fds, TCSANOW, &config) < 0) {}

    // Reopen stdio to slave pty.
    close(0); close(1); close(2);
    dup(fds); dup(fds); dup(fds);

    setsid();
    ioctl(0, TIOCSCTTY, 1);
    if(tcsetattr(fds, TCSANOW, &orig_settings) < 0) {}
    close(fds);

    // The VT100 is not multi language 
    unsetenv("LANG");
    setenv("TERM", "vt100", 1);

    char * shell = getenv("SHELL");
    if (shell && *shell)
      execl(shell, shell, 0);
    execl("/bin/sh", "/bin/sh", 0);

    exit(128);

  }

  // Don't need the slave here
  close(fds);
  */
}

bool PUSART::xmit_ready() { return has_xmit_ready; }

void PUSART::write_command(uint8_t cmd) {
  if (mode_select_mode) {
    mode_select_mode = false;
    mode = cmd; // like we give a wet turd
    if (pty_fd == -1)
	start_shell();
  } else {
    command = cmd;
    if (cmd & 1<<6) { // INTERNAL RESET
      mode_select_mode = true;
    }
    // Command 0x2f is BREAK  (cmd & 0x08)
    // Command 0x2d is hangup (cmd & 0x02) == 0
  }
}

void PUSART::write_data(uint8_t dat) {
  xoff = (dat == '\023');
  if (dat == '\023' || dat == '\021') return;
  /*
    if (write(pty_fd,&dat,1) < 0) {
    close(pty_fd);
    pty_fd = -1;
  }
  */


}

uint8_t PUSART::read_command() {
  /// always indicate data set ready
  bool tx_empty = false;
  bool sync_det = true;
  bool tx_rdy = true;
  bool rx_rdy = has_rx_rdy;
  return 0x80 | sync_det?0x40:0 | tx_empty?0x04:0 | rx_rdy?0x02:0 | tx_rdy?0x01:0;
}

bool PUSART::clock() {
  char c;
  if (has_rx_rdy || xoff) return false;
  int i = -1;
  /*
  int i = read(pty_fd,&c,1);
  */
  
  if (i != -1) {
    data = c;
    has_rx_rdy = true;
    return true;
  }
  return false;
}

uint8_t PUSART::read_data() {
  has_rx_rdy = false;
  return data;
}

char* PUSART::pty_name() {
 // if (pty_fd == -1)
    return (char*)"<NONE>";
 // return ptsname(pty_fd);
}
