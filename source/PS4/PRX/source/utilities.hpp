#include "includes.hpp"

extern int convert_to_utf16(const char* utf8, uint16_t* utf16, uint32_t available);
extern int convert_from_utf16(const uint16_t* utf16, char* utf8, uint32_t size);

extern void build_iovec(struct iovec** iov, int* iovlen, const char* name, const void* val, size_t len);
extern void mount_large_fs(const char* device, const char* mountpoint, const char* fstype, const char* mode, unsigned int flags);