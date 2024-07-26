#pragma once

#define PORT 1337
#define BUFFER_SIZE 1024
#define RESPONSE_OK "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: %d\r\n\r\n%s"
#define RESPONSE_404 "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\nContent-Length: 28\r\n\r\n[OCAPI] Web server is alive!"