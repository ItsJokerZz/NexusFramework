#include "utilities.hpp"

int convert_to_utf16(const char* utf8, uint16_t* utf16, uint32_t available) {
  int count = 0;

  while (*utf8) {
    uint8_t ch = (uint8_t)*utf8++;
    uint32_t code;
    uint32_t extra;

    if (ch < 0x80) {
      code = ch;
      extra = 0;
    } else if ((ch & 0xe0) == 0xc0) {
      code = ch & 31;
      extra = 1;
    } else if ((ch & 0xf0) == 0xe0) {
      code = ch & 15;
      extra = 2;
    } else {
      code = ch & 7;
      extra = 3;
    }

    for (uint32_t i = 0; i < extra; i++) {
      uint8_t next = (uint8_t)*utf8++;
      if (next == 0 || (next & 0xc0) != 0x80) 
      goto utf16_end; code = (code << 6) | (next & 0x3f);
    }

    if (code < 0xd800 || code >= 0xe000) {
      if (available < 1) goto utf16_end;
      utf16[count++] = (uint16_t)code;
      available--;
    } else {
      if (available < 2) goto utf16_end;
      code -= 0x10000;
      utf16[count++] = 0xd800 | (code >> 10);
      utf16[count++] = 0xdc00 | (code & 0x3ff);
      available -= 2;
    }
  }

utf16_end:
  utf16[count] = 0;
  return count;
}

int convert_from_utf16(const uint16_t* utf16, char* utf8, uint32_t size) {
  int count = 0;
  while (*utf16) {
    uint32_t code;
    uint16_t ch = *utf16++;
    if (ch < 0xd800 || ch >= 0xe000) {
      code = ch;
    } else {
      uint16_t ch2 = *utf16++;
      if (ch < 0xdc00 || ch > 0xe000 || ch2 < 0xd800 || ch2 > 0xdc00) 
      goto utf8_end; code = 0x10000 + ((ch & 0x03FF) << 10) + (ch2 & 0x03FF);
    }

    if (code < 0x80) {
      if (size < 1) goto utf8_end;
      utf8[count++] = (char)code;
      size--;
    } else if (code < 0x800) {
      if (size < 2) goto utf8_end;
      utf8[count++] = (char)(0xc0 | (code >> 6));
      utf8[count++] = (char)(0x80 | (code & 0x3f));
      size -= 2;
    } else if (code < 0x10000) {
      if (size < 3) goto utf8_end;
      utf8[count++] = (char)(0xe0 | (code >> 12));
      utf8[count++] = (char)(0x80 | ((code >> 6) & 0x3f));
      utf8[count++] = (char)(0x80 | (code & 0x3f));
      size -= 3;
    } else {
      if (size < 4) goto utf8_end;
      utf8[count++] = (char)(0xf0 | (code >> 18));
      utf8[count++] = (char)(0x80 | ((code >> 12) & 0x3f));
      utf8[count++] = (char)(0x80 | ((code >> 6) & 0x3f));
      utf8[count++] = (char)(0x80 | (code & 0x3f));
      size -= 4;
    }
  }

utf8_end:
  utf8[count] = 0;
  return count;
}

void build_iovec(struct iovec** iov, int* iovlen, const char* name, const void* val, size_t len) {
    int i;

    if (*iovlen < 0)
        return;

    i = *iovlen;
    *iov = (struct iovec*)realloc(*iov, sizeof(struct iovec) * (i + 2));
    if (*iov == NULL) {
        *iovlen = -1;
        return;
    }

    (*iov)[i].iov_base = strdup(name);
    (*iov)[i].iov_len = strlen(name) + 1;
    ++i;

    (*iov)[i].iov_base = (void*)val;
    if (len == (size_t)-1) {
        if (val != NULL)
            len = strlen((const char*)val) + 1;
        else
            len = 0;
    }
    (*iov)[i].iov_len = (int)len;

    *iovlen = ++i;
}

void mount_large_fs(const char* device, const char* mountpoint, const char* fstype, const char* mode, unsigned int flags) {
    struct iovec* iov = NULL;
    int iovlen = 0;

    build_iovec(&iov, &iovlen, "fstype", fstype, -1);
    build_iovec(&iov, &iovlen, "fspath", mountpoint, -1);
    build_iovec(&iov, &iovlen, "from", device, -1);
    build_iovec(&iov, &iovlen, "large", "yes", -1);
    build_iovec(&iov, &iovlen, "timezone", "static", -1);
    build_iovec(&iov, &iovlen, "async", "", -1);
    build_iovec(&iov, &iovlen, "ignoreacl", "", -1);
  
    if (mode) {
        build_iovec(&iov, &iovlen, "dirmask", mode, -1);
        build_iovec(&iov, &iovlen, "mask", mode, -1);
    }

    syscall(378, iov, iovlen, flags);
}