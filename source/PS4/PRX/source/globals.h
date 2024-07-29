#include "includes.h"

jbc_cred g_Cred, g_RootCreds;

bool freeOfSandbox() {
  int fd = open("/user/.jb_check", O_WRONLY | O_CREAT | O_TRUNC, 0777);

  if (fd > 0) {
    unlink("/user/.jb_check");
    close(fd);
    return true;
  } else
    return false;
}

#define SCE_IME_DIALOG_MAX_TEXT_LENGTH 512

uint16_t inputTextBuffer[SCE_IME_DIALOG_MAX_TEXT_LENGTH + 1];
uint16_t input_ime_title[SCE_IME_DIALOG_MAX_TEXT_LENGTH];

bool Keyboard(const char* Title, const char* initialTextBuffer, char* out_buffer) {
  sceSysmoduleLoadModule(ORBIS_SYSMODULE_IME_DIALOG);

  if (initialTextBuffer && strlen(initialTextBuffer) > 254) return false;

  memset(inputTextBuffer, 0, sizeof(inputTextBuffer));
  memset(input_ime_title, 0, sizeof(input_ime_title));

  if (initialTextBuffer) {
    strncpy(out_buffer, initialTextBuffer, 254);
    convert_to_utf16(initialTextBuffer, inputTextBuffer,
                     sizeof(inputTextBuffer));
  }

  if (Title) convert_to_utf16(Title, input_ime_title, sizeof(input_ime_title));

  OrbisImeDialogSetting param;
  memset(&param, 0, sizeof(OrbisImeSetting));

  param.maxTextLength = 254;
  param.inputTextBuffer = (wchar_t*)inputTextBuffer;
  param.title = (wchar_t*)input_ime_title;
  param.userId = 0xFE;
  param.type = ORBIS_TYPE_DEFAULT;
  param.enterLabel = ORBIS_BUTTON_LABEL_DEFAULT;

  sceImeDialogInit(&param, NULL);

  int status;

  while (1) {
    if (sceImeDialogGetStatus() == ORBIS_DIALOG_STATUS_STOPPED) {
      OrbisDialogResult result;
      memset(&result, 0, sizeof(OrbisDialogResult));
      sceImeDialogGetResult(&result);

      if (result.endstatus == ORBIS_DIALOG_OK) {
        convert_from_utf16(inputTextBuffer, 
        out_buffer, sizeof(inputTextBuffer));

        sceImeDialogTerm(); 
        return true;
      } goto Finished;

    } else if (sceImeDialogGetStatus() == 
    ORBIS_DIALOG_STATUS_NONE) goto Finished;
  } Finished: sceImeDialogTerm(); return false;
}

extern "C" {
int32_t 
    sceSysmoduleLoadModule(OrbisSysModule moduleId),
    sceKernelGetSystemSwVersion(OrbisKernelSwVersion* version);

uint32_t sceKernelGetCpuTemperature(uint32_t* celsius);
}