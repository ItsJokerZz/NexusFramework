#include "includes.h"

char* url_decode(const char * url) {
    size_t len = strlen(url);
    char * decoded = (char *)malloc(len + 1); // Allocate enough memory for the decoded string
    if (decoded == NULL) return NULL;

    char * d = decoded;
    for (const char * s = url; *s; ++s) {
        if (*s == '%') {
            if (isxdigit(s[1]) && isxdigit(s[2])) {
                int value;
                sscanf(s + 1, "%2x", &value);
                *d++ = (char)value;
                s += 2; // Skip the next two characters
            } else {
                // Invalid encoding, copy '%' and current character
                *d++ = '%';
            }
        } else if (*s == '+') {
            *d++ = ' '; // Replace '+' with space
        } else {
            *d++ = *s;
        }
    }
    *d = '\0'; // Null-terminate the decoded string

    return decoded;
}