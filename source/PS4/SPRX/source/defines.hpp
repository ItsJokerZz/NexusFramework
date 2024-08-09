#define PORT 1337

#define BUFFER_SIZE 1024

#define RESPONSE_OK                                                 \
  "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " \
  "%d\r\n\r\n%s"

#define RESPONSE_404 "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\nContent-Length: 28\r\n\r\n[OCAPI] Web server is alive!"
#define RESPONSE_CONNECT "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\nContent-Length: 15\r\n\r\n[OCAPI] Connect"
//[OCAPI] The web server is alive! | If you're seeing this and didnt mean to... check your command and/or arguments.