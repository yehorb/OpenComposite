#pragma once
#include "stdafx.h"
#include <Audioclient.h>
#include <Mmsystem.h>
#include <Windows.h>
#include <atlbase.h>
#include <cassert>
#include <functiondiscoverykeys_devpkey.h>
#include <iostream>
#include <mmdeviceapi.h>
#include <string>

#pragma comment(lib, "uuid.lib")
#pragma comment(lib, "winmm.lib")
#include <stdint.h>
#include <type_traits>

HRESULT find_output_device(std::wstring& output, string audioOutputName);
void set_app_default_audio_device(std::wstring device_id);
