#pragma once

void Connect(int client_sock);

void Notify(int client_sock, int type, const char * msg);