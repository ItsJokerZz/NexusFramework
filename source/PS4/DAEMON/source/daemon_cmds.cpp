#include "../headers/includes.hpp"

namespace cmds
{
  namespace daemon
  {
    void attach_relay()
    {
      attached = true;

      text_notify(222, "[OCAPI] Attached!");
      send_response("done");
    }

    void load_module()
    {
      const char *path = nullptr;
      const char *start = strstr(data.buffers.relay.data(), "path=");

      if (start)
      {
        start += 5;
        const char *end = strchr(start, ' ');
        if (end)
        {
          std::string encoded_path(start, end);
          path = decode_url(encoded_path.c_str());
        }
      }

      if (!path)
      {
        log_message("No path provided for SPRX");
        return;
      }

      struct proc_info info;
      sys_sdk_proc_info(&info);
      int pid = info.pid;

      int32_t result = sceKernelLoadStartModule(path, 0, 0, 0, NULL, NULL);
      if (result == 0x80020002)
      {
        log_message("SPRX %s not found", path);
        free((void *)path);
        return;
      }
      else if (result < 0)
      {
        log_message("Error loading SPRX %s! Error code 0x%08x (%i)", path, result,
                    result);
        free((void *)path);
        return;
      }

      char response[BUFFER_SIZE];
      snprintf(response, sizeof(response), "%i,%d,%s", pid, result, path);
      send_response(response);

      int32_t ret;
      int32_t (*module_start_ret)(size_t, const void *);
      int32_t (*module_stop_ret)(size_t, const void *);

      ret = sceKernelDlsym(result, "module_start", (void **)&module_start_ret);
      log_message("module_start Dlsym 0x%08x @ %p", ret, module_start_ret);

      ret = sceKernelDlsym(result, "module_stop", (void **)&module_stop_ret);
      log_message("module_stop Dlsym 0x%08x @ %p", ret, module_stop_ret);

      if (module_start_ret && module_stop_ret)
      {
        log_message("Starting SPRX...");
        int32_t prx_ret = module_start_ret(0, nullptr);
        log_message("module_start returned with 0x%08x", prx_ret);

        if (prx_ret || prx_ret < 0)
        {
          log_message("SPRX returned non-zero, stopping module...");
          prx_ret = module_stop_ret(0, nullptr);
          log_message("module_stop returned with 0x%08x", prx_ret);
        }
        else if (prx_ret == 0)
          log_message("module_start exit successful 0x%08x", prx_ret);
      }
      else
        log_message("Unable to find module_start or module_stop!");

      char notify_msg[BUFFER_SIZE];
      snprintf(notify_msg, sizeof(notify_msg), "[OCAPI] SPRX Loaded:\n%s", path);
      text_notify(222, notify_msg);

      free((void *)path);
    }

    void start_plugin()
    {
      const char *plugin = nullptr;
      const char *start = strstr(data.buffers.relay.data(), "plugin=");

      if (start)
      {
        start += 7;
        const char *end = strchr(start, ' ');
        if (end)
        {
          std::string encoded_plugin(start, end);
          plugin = decode_url(encoded_plugin.c_str());
        }
      }

      if (!plugin)
      {
        log_message("No plugin provided to load");
        send_error_response(_DEBUGGING);
        return;
      }

      struct proc_info info;
      sys_sdk_proc_info(&info);
      int pid = info.pid;

      std::string path = std::string("/data/GoldHEN/plugins/") + plugin;
      int32_t result = sceKernelLoadStartModule(path.c_str(), 0, 0, 0, NULL, NULL);

      if (result == 0x80020002)
      {
        log_message("Plugin %s not found", plugin);
        free((void *)plugin);
        send_error_response(_DEBUGGING);
        return;
      }
      else if (result < 0)
      {
        log_message("Error loading Plugin %s! Error code 0x%08x (%i)", plugin,
                    result, result);
        free((void *)plugin);
        send_error_response(_DEBUGGING);
        return;
      }

      char response[BUFFER_SIZE];
      snprintf(response, sizeof(response), "%i,%d,%s", pid, result, plugin);
      send_response(response);

      int32_t ret;
      int32_t (*plugin_load_ret)(void);
      int32_t (*plugin_unload_ret)(void);

      ret = sceKernelDlsym(result, "plugin_load", (void **)&plugin_load_ret);
      log_message("plugin_load Dlsym 0x%08x @ %p", ret, plugin_load_ret);

      ret = sceKernelDlsym(result, "plugin_unload", (void **)&plugin_unload_ret);
      log_message("plugin_unload Dlsym 0x%08x @ %p", ret, plugin_unload_ret);

      if (plugin_load_ret && plugin_unload_ret)
      {
        log_message("Starting plugin...");
        int32_t prx_ret = plugin_load_ret();
        log_message("plugin_load returned with 0x%08x", prx_ret);

        if (prx_ret || prx_ret < 0)
        {
          log_message("Plugin returned non-zero, stopping module...");
          prx_ret = plugin_unload_ret();
          log_message("plugin_unload returned with 0x%08x", prx_ret);
        }
        else if (prx_ret == 0)
          log_message("plugin_load exit successful 0x%08x", prx_ret);
      }
      else
        log_message("Unable to find plugin_load or plugin_unload!");

      char notify_msg[BUFFER_SIZE];
      snprintf(notify_msg, sizeof(notify_msg), "[OCAPI] Plugin Loaded: %s", plugin);
      text_notify(222, notify_msg);

      free((void *)plugin);
    }

    void read_proc_mem()
    {
      const char *address = nullptr;
      const char *size = nullptr;

      // Extract address param from query string
      const char *start = strstr(data.buffers.relay.data(), "address=");
      if (start)
      {
        start += 8; // Move past "address="
        const char *end = strchr(start, '&');
        if (end)
        {
          address = decode_url(std::string(start, end).c_str());
        }
        else
        {
          address = decode_url(start); // If no '&' found, address is the last param
        }
      }

      if (!address)
      {
        log_message("No address provided");
        send_error_response(_DEBUGGING);
        return;
      }

      // Extract size param from query string
      start = strstr(data.buffers.relay.data(), "size=");
      if (start)
      {
        start += 5; // Move past "size="
        const char *end = strchr(start, ' ');
        if (end)
        {
          size = std::string(start, end).c_str();
        }
        else
        {
          size = start; // If no space found, size is the last param
        }
      }

      if (!size)
      {
        log_message("No size provided");
        send_error_response(_DEBUGGING);
        return;
      }

      uint64_t addressVal = std::stoull(address, nullptr, 16); // Convert hex string to uint64_t
      size_t byteSize = std::stoul(size);                      // Convert string to size_t

      if (byteSize > 1024 * 1024)
      {
        send_response("error: byteSize too large");
        return;
      }

      char *buffer = new char[byteSize];

      proc_rw args;
      args.address = addressVal;
      args.data = static_cast<void *>(buffer);
      args.length = byteSize;
      args.write_flags = 0;

      if (sys_sdk_proc_rw(&args) == 0)
      {
        std::string bufferStr(buffer, byteSize); // Create a string from the buffer

        send_response(bufferStr.c_str());
      }
      else
      {
        send_response("error: failed to read memory");
      }

      delete[] buffer;
    }

    void write_proc_mem()
    {
      std::string address = extract_param("address", data.buffers.relay);
      std::string dataToWrite = extract_param("data", data.buffers.relay);

      if (address.empty())
      {
        log_message("No address provided");
        send_error_response(_DEBUGGING);
        return;
      }

      if (dataToWrite.empty())
      {
        log_message("No data to write provided");
        send_error_response(_DEBUGGING);
        return;
      }

      log_message("Address: %s, Data: %s", address.c_str(), dataToWrite.c_str());

      uint64_t addressVal = strtoull(address.c_str(), nullptr, 16);
      size_t byteSize = dataToWrite.length() / 2;
      char *buffer = new char[byteSize + 1];
      buffer[byteSize] = '\0';

      for (size_t i = 0; i < byteSize; i++)
      {
        unsigned int byte;
        sscanf(dataToWrite.c_str() + i * 2, "%2x", &byte);
        buffer[i] = static_cast<char>(byte);
      }

      proc_rw args;
      args.address = addressVal;
      args.data = static_cast<void *>(buffer);
      args.length = byteSize;
      args.write_flags = 1;

      if (sys_sdk_proc_rw(&args) == 0)
        send_response("success: memory written");
      else
        send_response("error: failed to write memory");

      delete[] buffer;
    }

  }
}