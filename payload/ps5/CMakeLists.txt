# =============================================================================
# NexusCheatFramework — PS5 Native Payload CMakeLists.txt
#
# EXPERIMENTAL — Requires PS5SDK (https://github.com/PS5Dev/PS5SDK)
#
# Build:
#   mkdir build && cd build
#   cmake .. -DPS5_PAYLOAD_SDK=/path/to/PS5SDK
#   make
#
# Output: output/nexus-cheat-ps5.elf
# =============================================================================

cmake_minimum_required(VERSION 3.10)
project(nexus-cheat-ps5 C)

# --- PS5SDK toolchain ---
if(NOT DEFINED ENV{PS5_PAYLOAD_SDK})
    message(FATAL_ERROR "PS5_PAYLOAD_SDK environment variable is not set. "
                        "Set it to your PS5SDK installation path.")
endif()

set(PS5SDK_PATH $ENV{PS5_PAYLOAD_SDK})
set(CMAKE_TOOLCHAIN_FILE "${PS5SDK_PATH}/toolchain/prospero.cmake")

# --- Project settings ---
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

# --- Source files ---
set(SOURCES
    src/main.c
)

# --- Include directories ---
set(INCLUDE_DIRS
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# --- Build target ---
add_executable(${PROJECT_NAME} ${SOURCES})
target_include_directories(${PROJECT_NAME} PRIVATE ${INCLUDE_DIRS})
target_link_libraries(${PROJECT_NAME} c kernel)

# --- Output ---
set_target_properties(${PROJECT_NAME} PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/output"
)

# --- Compiler flags ---
target_compile_options(${PROJECT_NAME} PRIVATE
    -Wall -Wextra -Werror -O2
)
