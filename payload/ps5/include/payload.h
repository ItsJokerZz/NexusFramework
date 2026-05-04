/*
 * payload.h — NexusCheatFramework PS5 Native Payload
 *
 * EXPERIMENTAL — Not buildable without PS5SDK
 *
 * This header defines the shared types and structures for the
 * PS5 native cheat payload. The actual implementation requires
 * PS5SDK headers (<ps5/kernel.h>, <ps5/klog.h>, etc.) which
 * are not available in this repository.
 *
 * See: https://github.com/PS5Dev/PS5SDK
 */

#ifndef NEXUS_CHEAT_PAYLOAD_H
#define NEXUS_CHEAT_PAYLOAD_H

#include <stddef.h>
#include <stdint.h>

/* =========================================================================
 * Payload entry point
 *
 * The exact signature depends on PS5SDK conventions. Verify from
 * PS5SDK examples before implementing.
 *
 * Typical signature (verify from PS5SDK):
 *   int payload_main(struct payload_args *args);
 * ========================================================================= */

/* =========================================================================
 * HTTP request/response types
 *
 * The payload runs a lightweight HTTP server on port 9090 (or configurable).
 * Each endpoint receives a parsed HTTP request and returns a JSON response.
 * ========================================================================= */

#define PAYLOAD_PORT 9090
#define MAX_REQUEST_SIZE (64 * 1024)
#define MAX_RESPONSE_SIZE (256 * 1024)
#define MAX_MATCHES 256

/* =========================================================================
 * Endpoint handlers
 *
 * Each handler function processes one endpoint.
 * Implementations go in src/main.c or separate source files.
 * ========================================================================= */

/* GET /status — Payload health check */
void handle_status(void);

/* GET /setup — System info (name, firmware, type) */
void handle_setup(void);

/* GET /version — Payload version info */
void handle_version(void);

/* GET /connect — Mark connection as active */
void handle_connect(void);

/* GET /disconnect — Clean up client connection */
void handle_disconnect(void);

/* GET /get_process — Get running application PID and info */
void handle_get_process(void);

/* GET /get_vm_maps — Get memory region layout for a process */
void handle_get_vm_maps(void);

/* POST /read_memory — Read memory from a process
 *   Request:  { "pid": int, "address": "0x...", "size": int }
 *   Response: { "data": "hex-encoded-bytes", "bytes_read": int }
 */
void handle_read_memory(void);

/* POST /write_memory — Write memory to a process
 *   Request:  { "pid": int, "address": "0x...", "data": "hex-encoded-bytes" }
 *   Response: { "success": true, "bytes_written": int }
 */
void handle_write_memory(void);

/* POST /aob_scan — Array-of-bytes pattern scan
 *   Request:  { "pattern": "48 8B ?? ?? 89", "start": "0x...", "end": "0x...",
 *               "max_results": 8, "executable_only": true }
 *   Response: { "matches": ["0x..."], "scanned_regions": int,
 *               "skipped_regions": int, "elapsed_ms": int }
 *
 *   TODO: Implement actual AOB scanning logic.
 *   Reference: payload-patches/aob_scan_patch.txt
 */
void handle_aob_scan(void);

/* GET /pad_state — Read controller state
 *   Response: { "connected": bool, "buttons": int, "lx": int, "ly": int,
 *               "rx": int, "ry": int, "l2": int, "r2": int, "timestamp": int }
 *
 *   TODO: Implement scePadOpen/scePadReadState.
 *   Reference: payload-patches/pad_state_patch.txt
 */
void handle_pad_state(void);

/* POST /rpc_call — Remote procedure call (advanced, disabled by default)
 *   Request:  { "address": "0x...", "args": [...] }
 *   Response: { "result": "0x..." }
 *
 *   WARNING: Only enable with explicit config allowAdvancedCodeExecution: true
 */
void handle_rpc_call(void);

/* =========================================================================
 * Memory utility types
 * ========================================================================= */

typedef struct {
    uint64_t start;
    uint64_t end;
    uint64_t offset;
    uint64_t size;
    uint32_t prot;       /* PROT_READ | PROT_WRITE | PROT_EXEC */
    uint32_t flags;      /* MAP_PRIVATE | MAP_SHARED | MAP_ANONYMOUS */
    char     name[256];  /* Region name (e.g., "libSceSomeModule.sprx") */
} MemoryRegion;

/* =========================================================================
 * AOB scan types
 * ========================================================================= */

typedef struct {
    uint8_t  byte;
    int      wildcard;  /* 0 = exact match, 1 = wildcard (??) */
} AobPatternByte;

typedef struct {
    AobPatternByte *bytes;
    size_t          length;
} AobPattern;

/* =========================================================================
 * Controller state types
 * ========================================================================= */

typedef struct {
    int      connected;
    uint32_t buttons;
    uint8_t  lx;
    uint8_t  ly;
    uint8_t  rx;
    uint8_t  ry;
    uint8_t  l2;
    uint8_t  r2;
    uint64_t timestamp;
} PadState;

/* =========================================================================
 * Error codes
 * ========================================================================= */

#define ERR_SUCCESS      0
#define ERR_INVALID_ARGS 1
#define ERR_NOT_FOUND    2
#define ERR_PERMISSION   3
#define ERR_TIMEOUT      4
#define ERR_INTERNAL     5
#define ERR_UNSUPPORTED  6

#endif /* NEXUS_CHEAT_PAYLOAD_H */
