#include "../headers/includes.hpp"

namespace cmds
{
  namespace client
  {
    namespace connection
    {
      void setup()
      {
        nlohmann::json config = {
            {"NAME", get_console_name()},
            {"FW", get_fw_version()},
            {"TYPE", get_console_type()},
            {"OCAPI", std::to_string(VERSION).substr(0, 4)}};

        send_response(config);
      }

      void status()
      {
        send_response(std::string("Server is running."));
      }

      void version()
      {
        send_response(std::to_string(VERSION).c_str());
      }

      void connect()
      {
        connected = true;
        send_response("true");
      }

      void unload()
      {
        if (data.sockets.client >= 0)
        {
          sceNetSocketClose(data.sockets.client);
          data.sockets.client = -1;

          pthread_cancel(data.threads.client);
        }

        unloaded = true;

        send_response("done");
      }

      void disconnect()
      {

        if (data.sockets.client >= 0)
        {
          sceNetSocketClose(data.sockets.client);
          data.sockets.client = -1;
        }

        connected = false;

        send_response("done");
      }

      void attach()
      {
        attached = false;

        if (is_port_open(RELAYS_PORT))
        {
          std::string response = perform_http_request("attach_relay");

          // if (response != "" && response == "done") /*prolly not needed*/
          attached = true;
        }

        send_response("done");
      }

    }

    namespace sys_info
    {
      void get_name()
      {
        send_response(get_console_name().c_str());
      }

      void get_fw()
      {
        send_response(get_fw_version());
      }

      void get_sys_type()
      {
        send_response(get_console_type());
      }

      void get_disk_info()
      {
        std::string value = extract_param("return", data.buffer);

        if (value.empty())
          value = "all";

        nlohmann::json diskInfo;

        if (value == "all")
        {
          diskInfo["percentUsed"] = ::get_disk_info("percentUsed");
          diskInfo["totalSpace"] = ::get_disk_info("totalSpace");
          diskInfo["usedSpace"] = ::get_disk_info("usedSpace");
          diskInfo["freeSpace"] = ::get_disk_info("freeSpace");
        }
        else
        {
          if (value == "percent")
            diskInfo["percentUsed"] = ::get_disk_info("percentUsed");
          else if (value == "total")
            diskInfo["totalSpace"] = ::get_disk_info("totalSpace");
          else if (value == "used")
            diskInfo["usedSpace"] = ::get_disk_info("usedSpace");
          else if (value == "free")
            diskInfo["freeSpace"] = ::get_disk_info("freeSpace");
        }

        send_response(diskInfo);
      }

      void get_temp()
      {
        std::string type_str = extract_param("type", data.buffer);

        if (type_str.empty())
          return;

        int tempValue = NULL;
        if (strcmp(type_str.c_str(), "cpu") == 0)
          tempValue = get_cpu_temperature();
        else if (strcmp(type_str.c_str(), "soc") == 0)
          tempValue = get_soc_temperature();
        else
          return;

        char message[1024];
        snprintf(message, sizeof(message), "%i", tempValue);
        send_response(message);
      }

      void get_user()
      {
        send_response(get_username());
      }

    }

    namespace sys_control
    {
      void temp_limit()
      {
        uint8_t temp = 0;
        std::string limit = extract_param("limit", data.buffer);

        if (!limit.empty())
        {
          if (sscanf(limit.c_str(), "%hhu", &temp) != 1)
          {
            send_error_response(INVALID_ARGS);

            return;
          }

          set_temperature_limit(temp);

          send_response("done");
        }
        else
          send_error_response(INVALID_ARGS);
      }

      void set_power_state()
      {
        int state = 0;

        std::string state_str = extract_param("state", data.buffer);

        if (!state_str.empty())
        {
          if (sscanf(state_str.c_str(), "%i", &state) != 1)
          {
            send_error_response(INVALID_ARGS);

            return;
          }

          set_power_state((power_state)state);

          send_response("done");
        }
        else
          send_error_response(INVALID_ARGS);
      };

      void ring_buzzer()
      {
        int type = 0;

        std::string type_str = extract_param("type", data.buffer);

        if (type_str.empty())
          return;

        type = std::stoi(type_str.c_str());

        switch (type)
        {
        case -1: // continuous
          ::ring_buzzer(6);
          break;
        case 0: // stop
          ::ring_buzzer(0);
          break;
        case 1: // single
          ::ring_buzzer(1);
          break;
        case 2: // double
          ::ring_buzzer(1);
          sceKernelUsleep(125000);
          ::ring_buzzer(1);
          break;
        case 3: // triple
          ::ring_buzzer(1);
          sceKernelUsleep(125000);
          ::ring_buzzer(1);
          sceKernelUsleep(125000);
          ::ring_buzzer(1);
          break;
        }

        send_response("done");
      }

      void notify()
      {
        std::string image_uri = extract_param("image", data.buffer);
        std::string message = decode_url(extract_param("msg", data.buffer));

        if (message.empty())
          send_error_response(INVALID_ARGS);

        if (!image_uri.empty())
          image_notify(image_uri.c_str(), message.c_str());

        send_response("done");
      }

    }

    namespace process
    {
      void get_proc_list()
      {
        uint64_t num = 0;
        struct proc_list_entry *procs = nullptr;

        nlohmann::json response = nlohmann::json::object();

        if (::get_proc_list(nullptr, &num) != 0 || num == 0)
          return;

        procs =
            (struct proc_list_entry *)malloc(sizeof(struct proc_list_entry) * num);
        if (!procs)
          return;

        if (::get_proc_list(procs, &num) != 0)
        {
          free(procs);
          return;
        }

        std::vector<std::pair<int, std::string>> sorted_procs;
        for (uint64_t i = 0; i < num; i++)
        {
          if (procs[i].p_comm[sizeof(procs[i].p_comm) - 1] != '\0')
            procs[i].p_comm[sizeof(procs[i].p_comm) - 1] = '\0';

          sorted_procs.push_back({procs[i].pid,
                                  procs[i].p_comm});
        }

        std::sort(sorted_procs.begin(), sorted_procs.end());

        for (const auto &proc : sorted_procs)
          response[std::to_string(proc.first)] = proc.second;

        send_response(generate_json(response).c_str());

        free(procs);
      }

      void get_proc_info()
      {
        std::unordered_map<std::string, std::string> info_map = {
            {"pid", get_app_info("pid")},
            {"region", get_app_info("region")},
            {"titleID", get_app_info("titleID")},
            {"type", get_app_info("type")},
            {"name", get_app_info("name")},
            {"exec", get_app_info("exec")},
            {"version", get_app_info("version")},
            {"minFW", get_app_info("minFW")},
            {"image", get_app_info("image")}};

        std::string return_str = extract_param("return", data.buffer);
        if (return_str.empty())
        {
          send_error_response(INVALID_ARGS);

          return;
        }

        if (return_str == "all")
        {
          nlohmann::json all_info_json;

          for (const auto &pair : info_map)
          {
            if (!pair.second.empty())
              all_info_json[pair.first] = pair.second;
          }

          send_response(all_info_json.dump().c_str());
          return;
        }

        auto it = info_map.find(return_str);
        if (it != info_map.end() && !it->second.empty())
        {
          if (return_str == "image")
          {
            std::string saveAs = get_app_info("titleId") + "_icon0.png";
            send_file_response(it->second.c_str(), saveAs.c_str());
          }
          else
            send_response(it->second.c_str());
        }
        else
          send_error_response(INVALID_ARGS);
      }

      void find_pid_by_name()
      {
        std::string name = extract_param("name", data.buffer);

        if (name.empty())
          return;

        nlohmann::json response =
            {{"PID", find_pid_by_procName(name.c_str())}};

        send_response(generate_json(response).c_str());
      }

      void find_name_of_pid()
      {
        std::string pid_str = extract_param("pid", data.buffer);

        if (pid_str.empty())
          return;

        int pid = atoi(pid_str.c_str());

        nlohmann::json response = {{"NAME", find_procName_of_pid(pid)}};
        send_response(generate_json(response).c_str());
      }

      void read_proc_mem()
      {
        std::string address = extract_param("address", data.buffer);
        std::string size = extract_param("size", data.buffer);

        if (address.empty())
          return;

        if (size.empty())
          return;

        std::string request = "read_memory?address=" + address + "&size=" + size;
        std::string response = perform_http_request(request.c_str());

        send_response(response);
      }

      void write_proc_mem()
      {
        std::string address = extract_param("address", data.buffer, false);
        std::string data = extract_param("data", ::data.buffer, false);

        if (address.empty())
          return;

        if (data.empty())
          return;

        std::string request = "address=" + address + "&data=" + data;
        send_response(perform_http_request("write_memory", RELAYS_PORT, false, request).c_str());
      }

      void alloc_proc_mem()
      {
        // check if not ShellUI and if not get the pid of the current app, require param
        std::string pid = get_app_info("pid");
        std::string length = extract_param("length", data.buffer);

        if (length.empty())
          return;

        std::string request = "alloc_memory?length=" + length;
        send_response(perform_http_request(request.c_str()));
      }

      void free_proc_mem()
      {
        // check if not ShellUI and if not get the pid of the current app, require param
        std::string pid = get_app_info("pid");
        std::string address = extract_param("address", data.buffer);
        std::string length = extract_param("length", data.buffer);

        if (address.empty())
          return;
        if (length.empty())
          return;

        std::string request = "free_memory?address=" + address + "&length=" + length;
        send_response(perform_http_request(request.c_str()));
      }

      void stop_plugin() {}

      void start_plugin() {}

      void unload_module() {}

      void load_module()
      {
        std::string exec_path = extract_param("exec", data.buffer);
        std::string prx_path = extract_param("path", data.buffer);

        auto load_prx = [](const std::string &exec_path, const std::string &prx_path) -> bool
        {
          int prx_handle =
              sys_sdk_proc_prx_load(const_cast<char *>(exec_path.c_str()),
                                    const_cast<char *>(prx_path.c_str()));
          if (prx_handle >= 0)
          {
            char notify_msg[1024];
            snprintf(notify_msg, sizeof(notify_msg), "[OCAPI] PRX Loaded: %s",
                     prx_path.c_str());
            text_notify(222, notify_msg);

            nlohmann::json response = {
                {prx_path, prx_handle}};
            send_response(generate_json(response).c_str());

            return true;
          }
          else
          {
            send_error_response(UNKNOWN_ERROR);

            return false;
          }
        };

        if (is_port_open(RELAYS_PORT))
        {
          if (!exec_path.empty() || !prx_path.empty())
          {
            if (!exec_path.empty() && !prx_path.empty())
            {
              load_prx(exec_path, prx_path);

              return;
            }
            else if (!prx_path.empty() && exec_path.empty())
            {
              std::string request = "load_module?path=" + prx_path;
              send_response(perform_http_request("load_module", RELAYS_PORT, false, request));
            }
            else
              send_error_response(INVALID_ARGS);
          }
          else
            send_error_response(INVALID_ARGS);
        }
        else
        {
          if (exec_path.empty() && !prx_path.empty())
          {
            if (!attached)
              send_error_response(NOT_ATTACHED);
            else
            {
              std::string request = "load_module?path=" + prx_path;
              send_response(perform_http_request("load_module", RELAYS_PORT, false, request));
            }
          }
          else if (!exec_path.empty() && !prx_path.empty())
            load_prx(exec_path, prx_path);
          else
            send_error_response(INVALID_ARGS);
        }
      }

    }

  }
}